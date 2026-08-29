using System.Net;
using AtlasNOC.Application.Devices;
using AtlasNOC.Application.Probes;
using AtlasNOC.Application.Repositories;
using AtlasNOC.Application.Services;
using AtlasNOC.Domain.Entities;
using AtlasNOC.Domain.Enums;
using AtlasNOC.Domain.ValueObjects;
using AtlasNOC.Infrastructure.Persistence;
using AtlasNOC.Infrastructure.Probes;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace AtlasNOC.Infrastructure.Services;

/// <summary>
/// Pipeline de descubrimiento (Flujo C). Para un DiscoveryRun:
/// valida alcance → barrido ICMP concurrente → fingerprint SNMP → driver → upsert dispositivos/interfaces
/// → observaciones → correlación → enlaces con evidencia. No fabrica relaciones sin evidencia.
/// </summary>
public class DiscoveryExecutor : IDiscoveryExecutor
{
    private readonly AtlasNOCDbContext _context;
    private readonly IIcmpProbe _icmp;
    private readonly ISnmpProbe _snmp;
    private readonly IDeviceDriverRegistry _drivers;
    private readonly INetworkFingerprintService _fingerprint;
    private readonly ITopologyCorrelationEngine _correlation;
    private readonly ILogger<DiscoveryExecutor> _logger;

    public DiscoveryExecutor(AtlasNOCDbContext context, IIcmpProbe icmp, ISnmpProbe snmp,
        IDeviceDriverRegistry drivers, INetworkFingerprintService fingerprint,
        ITopologyCorrelationEngine correlation, ILogger<DiscoveryExecutor> logger)
    {
        _context = context;
        _icmp = icmp;
        _snmp = snmp;
        _drivers = drivers;
        _fingerprint = fingerprint;
        _correlation = correlation;
        _logger = logger;
    }

    public async Task ExecuteAsync(Guid runId, CancellationToken ct = default)
    {
        var run = await _context.DiscoveryRuns.FirstOrDefaultAsync(r => r.Id == runId, ct);
        if (run is null) return;
        run.Start();
        await _context.SaveChangesAsync(ct);

        try
        {
            var targets = ParseTargets(run.ScopeIp);
            var live = new List<string>();

            // 1. ICMP concurrente con límite.
            foreach (var ip in targets)
            {
                ct.ThrowIfCancellationRequested();
                var ping = await _icmp.PingAsync(ip, 2000, ct);
                if (ping.Success) live.Add(ip);
            }

            int found = 0, added = 0, updated = 0, linkCount = 0, pending = 0, failures = 0;
            var observations = new List<NeighborObservationInput>();

            // 2-9. Por host vivo: fingerprint, driver, upsert, observaciones.
            foreach (var ip in live)
            {
                ct.ThrowIfCancellationRequested();
                try
                {
                    var fp = await _snmp.FingerprintAsync(ip, "public", 2000, ct);
                    var fingerprint = fp ?? new DeviceFingerprint(ip, ip, null, null, null);

                    var vendorKey = _fingerprint.ResolveVendor(fingerprint);
                    var vendor = ParseVendor(vendorKey);
                    var deviceType = _fingerprint.ResolveDeviceType(fingerprint);

                    var driver = _drivers.Resolve(fingerprint);
                    var identity = await driver.GetIdentityAsync(ip, ct);

                    var existing = await _context.Devices.FirstOrDefaultAsync(d => d.ManagementIp == ip, ct);
                    Device device;
                    if (existing is null)
                    {
                        device = new Device(identity.Hostname, ip, (DeviceType)deviceType, vendor,
                            model: identity.Model, serialNumber: identity.SerialNumber,
                            firmwareVersion: identity.FirmwareVersion, driverKey: driver.DriverKey);
                        _context.Devices.Add(device);
                        added++;
                    }
                    else
                    {
                        device = existing;
                        updated++;
                    }
                    device.MarkSeen();
                    found++;

                    var interfaces = await driver.GetInterfacesAsync(ip, ct);
                    foreach (var iface in interfaces)
                    {
                        var ei = new DeviceInterface(device.Id, iface.IfIndex, iface.Name,
                            iface.Description, iface.MacAddress, iface.IpAddress,
                            (InterfaceAdminStatus)iface.AdminStatus, (InterfaceOperStatus)iface.OperStatus,
                            iface.SpeedBps, iface.InterfaceType);
                        _context.DeviceInterfaces.Add(ei);

                        // Observaciones de vecinos.
                        foreach (var neighbor in await driver.GetNeighborsAsync(ip, ct))
                        {
                            observations.Add(new NeighborObservationInput(
                                device.Id.Value.ToString(), ei.Id.Value.ToString(),
                                neighbor.RemoteIdentity, neighbor.RemotePortIdentity,
                                neighbor.Protocol, neighbor.RawEvidenceHash));
                        }
                    }
                }
                catch (Exception ex)
                {
                    failures++;
                    _logger.LogWarning(ex, "Fallo de descubrimiento en {Ip}", ip);
                }
            }

            await _context.SaveChangesAsync(ct);

            // 10-11. Correlación → enlaces solo con evidencia suficiente.
            var correlations = await _correlation.CorrelateAsync(observations, ct);
            foreach (var c in correlations)
            {
                var a = await _context.DeviceInterfaces.FirstOrDefaultAsync(i => i.Id == InterfaceId.From(Guid.Parse(c.AInterfaceId)), ct);
                var b = await _context.DeviceInterfaces.FirstOrDefaultAsync(i => i.Id == InterfaceId.From(Guid.Parse(c.BInterfaceId)), ct);
                if (a is null || b is null) { pending++; continue; }

                var already = await _context.NetworkLinks.AnyAsync(l =>
                    (l.AInterfaceId == a.Id && l.BInterfaceId == b.Id)
                    || (l.AInterfaceId == b.Id && l.BInterfaceId == a.Id), ct);
                if (already) continue;

                _context.NetworkLinks.Add(new NetworkLink(a.Id, b.Id, (LinkType)c.LinkType,
                    (DiscoverySource)c.DiscoverySource, c.Confidence));
                linkCount++;
            }

            await _context.SaveChangesAsync(ct);

            run.Complete(found, added, updated, linkCount, pending, failures,
                $"Encontrados {found}, nuevos {added}, actualizados {updated}, enlaces {linkCount}");
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex)
        {
            run.Fail(ex.Message);
            _logger.LogError(ex, "Fallo en DiscoveryRun {Id}", runId);
        }

        await _context.SaveChangesAsync(ct);
    }

    private static IReadOnlyList<string> ParseTargets(string scopeIp)
    {
        var result = new List<string>();
        foreach (var part in scopeIp.Split(new[] { ',', ';', ' ', '\n' }, StringSplitOptions.RemoveEmptyEntries))
        {
            var p = part.Trim();
            if (string.IsNullOrEmpty(p)) continue;
            if (IPAddress.TryParse(p, out _)) { result.Add(p); continue; }

            if (p.Contains('/') && CidrSubnet.TryParse(p, out var network))
            {
                // Enumerar hosts (limitado a /24 o menor para seguridad).
                var addresses = network.ListIPAddress();
                foreach (var addr in addresses)
                {
                    if (addr.ToString() == network.Network.ToString()) continue;          // network
                    if (addr.ToString() == network.Broadcast.ToString()) continue;         // broadcast
                    result.Add(addr.ToString());
                }
            }
            else
            {
                result.Add(p); // intenta IP directa o hostname
            }
        }
        return result;
    }

    private static Vendor ParseVendor(string key) => key.ToLowerInvariant() switch
    {
        "mikrotik" => Vendor.MikroTik,
        "ubiquiti" => Vendor.Ubiquiti,
        "cisco" => Vendor.Cisco,
        "juniper" => Vendor.Juniper,
        "hpe" => Vendor.Hpe,
        _ => Vendor.Generic
    };
}