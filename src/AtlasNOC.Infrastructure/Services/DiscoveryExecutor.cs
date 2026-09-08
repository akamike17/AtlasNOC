using System.Net;
using System.Collections.Concurrent;
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
using Microsoft.Extensions.Options;

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
    private readonly ICredentialService _credentials;
    private readonly IDeviceDriverRegistry _drivers;
    private readonly INetworkFingerprintService _fingerprint;
    private readonly ITopologyCorrelationEngine _correlation;
    private readonly ILogger<DiscoveryExecutor> _logger;
    private readonly DiscoveryOptions _options;

    public DiscoveryExecutor(AtlasNOCDbContext context, IIcmpProbe icmp, ISnmpProbe snmp,
        ICredentialService credentials,
        IDeviceDriverRegistry drivers, INetworkFingerprintService fingerprint,
        ITopologyCorrelationEngine correlation, ILogger<DiscoveryExecutor> logger,
        IOptions<DiscoveryOptions> options)
    {
        _context = context;
        _icmp = icmp;
        _snmp = snmp;
        _credentials = credentials;
        _drivers = drivers;
        _fingerprint = fingerprint;
        _correlation = correlation;
        _logger = logger;
        _options = options.Value;
    }

    public async Task ExecuteAsync(Guid runId, CancellationToken ct = default)
    {
        var run = await _context.DiscoveryRuns.FirstOrDefaultAsync(r => r.Id == runId, ct);
        if (run is null) return;
        if (run.Status != DiscoveryRunStatus.Running
            || string.IsNullOrWhiteSpace(run.ClaimedBy)
            || !run.LeaseExpiresAtUtc.HasValue
            || run.LeaseExpiresAtUtc <= DateTime.UtcNow)
            throw new InvalidOperationException("DiscoveryRun no tiene un claim/lease vigente.");

        try
        {
            var connectionOptions = SnmpConnectionOptions.Anonymous();
            ResolvedDeviceCredential? resolvedCredential = null;
            if (!string.IsNullOrWhiteSpace(run.CredentialId))
            {
                if (!Guid.TryParse(run.CredentialId, out var credentialId))
                    throw new InvalidOperationException("La credencial del discovery no es válida.");

                resolvedCredential = await _credentials.ResolveAsync(credentialId, ct);
                if (resolvedCredential is null)
                    throw new InvalidOperationException("La credencial del discovery no existe o está inactiva.");

                connectionOptions = resolvedCredential.ToConnectionOptions();
            }

            SiteId? targetSiteId = null;
            if (!string.IsNullOrWhiteSpace(run.TargetSiteId))
            {
                if (!Guid.TryParse(run.TargetSiteId, out var siteGuid))
                    throw new InvalidOperationException("El sitio del discovery no es válido.");

                targetSiteId = SiteId.From(siteGuid);
                if (!await _context.Sites.AnyAsync(s => s.Id == targetSiteId, ct))
                    throw new InvalidOperationException("El sitio del discovery no existe.");
            }

            var targets = ParseTargets(run.ScopeIp, _options.MaxTargetsPerRun);
            // 1. ICMP concurrente con límite.
            var live = await ProbeLiveTargetsAsync(targets, _icmp, _options, ct);

            int found = 0, added = 0, updated = 0, linkCount = 0, pending = 0, failures = 0;
            var observations = new List<NeighborObservationInput>();

            // 2-9. Por host vivo: fingerprint, driver, upsert, observaciones.
            foreach (var ip in live)
            {
                ct.ThrowIfCancellationRequested();
                try
                {
                    var fp = await _snmp.FingerprintAsync(ip, connectionOptions, 2000, ct);
                    var fingerprint = fp ?? new DeviceFingerprint(ip, ip, null, null, null);

                    var vendorKey = _fingerprint.ResolveVendor(fingerprint);
                    var vendor = ParseVendor(vendorKey);
                    var deviceType = _fingerprint.ResolveDeviceType(fingerprint);

                    var driver = _drivers.Resolve(fingerprint);
                    var credentialAwareDriver = driver as ISnmpCredentialAwareDriver;
                    var deviceCredentialDriver = driver as IDeviceCredentialAwareDriver;
                    var identity = deviceCredentialDriver is not null && resolvedCredential is not null
                        ? await deviceCredentialDriver.GetIdentityAsync(ip, resolvedCredential, ct)
                        : credentialAwareDriver is not null
                            ? await credentialAwareDriver.GetIdentityAsync(ip, connectionOptions, ct)
                            : await driver.GetIdentityAsync(ip, ct);

                    var existing = await _context.Devices.FirstOrDefaultAsync(d => d.ManagementIp == ip, ct);
                    Device device;
                    if (existing is null)
                    {
                        device = new Device(identity.Hostname, ip, (DeviceType)deviceType, vendor,
                            siteId: targetSiteId,
                            model: identity.Model, serialNumber: identity.SerialNumber,
                            firmwareVersion: identity.FirmwareVersion, driverKey: driver.DriverKey);
                        _context.Devices.Add(device);
                        added++;
                    }
                    else
                    {
                        device = existing;
                        device.UpdateDiscoveredIdentity(identity.Hostname, (DeviceType)deviceType, vendor,
                            identity.Model, identity.SerialNumber, identity.FirmwareVersion, driver.DriverKey);
                        updated++;
                    }
                    device.MarkSeen();
                    found++;

                    var interfaces = deviceCredentialDriver is not null && resolvedCredential is not null
                        ? await deviceCredentialDriver.GetInterfacesAsync(ip, resolvedCredential, ct)
                        : credentialAwareDriver is not null
                            ? await credentialAwareDriver.GetInterfacesAsync(ip, connectionOptions, ct)
                            : await driver.GetInterfacesAsync(ip, ct);
                    // Interfaces ya conocidas del dispositivo para upsert idempotente.
                    var existingInterfaces = await _context.DeviceInterfaces
                        .Where(i => i.DeviceId == device.Id)
                        .ToListAsync(ct);
                    foreach (var iface in interfaces)
                    {
                        var ei = existingInterfaces.FirstOrDefault(i => i.IfIndex == iface.IfIndex);
                        if (ei is null)
                        {
                            ei = new DeviceInterface(device.Id, iface.IfIndex, iface.Name,
                                iface.Description, iface.MacAddress, iface.IpAddress,
                                (InterfaceAdminStatus)iface.AdminStatus, (InterfaceOperStatus)iface.OperStatus,
                                iface.SpeedBps, iface.InterfaceType);
                            _context.DeviceInterfaces.Add(ei);
                            existingInterfaces.Add(ei);
                        }
                        else
                        {
                            ei.Refresh(iface.Name, iface.Description, iface.MacAddress, iface.IpAddress,
                                (InterfaceAdminStatus)iface.AdminStatus, (InterfaceOperStatus)iface.OperStatus,
                                iface.SpeedBps, iface.InterfaceType);
                        }
                    }

                    if (interfaces.Count > 0)
                    {
                        var seenIndexes = interfaces.Select(i => i.IfIndex).ToHashSet();
                        foreach (var missing in existingInterfaces.Where(i => !seenIndexes.Contains(i.IfIndex)))
                            missing.MarkStale();
                    }

                    // Observaciones de vecinos: una sola vez por dispositivo y enlazadas
                    // a la interfaz local correcta (por nombre de puerto).
                    // LocalDeviceId usa el hostname (identidad estable que el vecino referencia
                    // como RemoteIdentity); LocalInterfaceId usa el GUID de la interfaz persistida.
                    var neighbors = deviceCredentialDriver is not null && resolvedCredential is not null
                        ? await deviceCredentialDriver.GetNeighborsAsync(ip, resolvedCredential, ct)
                        : credentialAwareDriver is not null
                            ? await credentialAwareDriver.GetNeighborsAsync(ip, connectionOptions, ct)
                            : await driver.GetNeighborsAsync(ip, ct);
                    foreach (var neighbor in neighbors)
                    {
                        var localIface = existingInterfaces.FirstOrDefault(
                            i => i.Name.Equals(neighbor.LocalInterfaceName, StringComparison.OrdinalIgnoreCase));
                        if (localIface is null) continue;

                        observations.Add(new NeighborObservationInput(
                            device.Id.Value.ToString(), device.Hostname, localIface.Id.Value.ToString(),
                            neighbor.RemoteIdentity, neighbor.RemotePortIdentity,
                            neighbor.Protocol, neighbor.RawEvidenceHash));

                        var duplicateSince = DateTime.UtcNow.AddMinutes(-5);
                        var duplicate = await _context.NeighborObservations.AnyAsync(o =>
                            o.LocalInterfaceId == localIface.Id
                            && o.RawEvidenceHash == neighbor.RawEvidenceHash
                            && o.ObservedAtUtc >= duplicateSince, ct);
                        if (!duplicate)
                        {
                            _context.NeighborObservations.Add(new NeighborObservation(
                                device.Id, localIface.Id, neighbor.RemoteIdentity,
                                ParseNeighborProtocol(neighbor.Protocol), neighbor.RawEvidenceHash,
                                neighbor.RemotePortIdentity));
                        }
                    }
                }
                catch (OperationCanceledException) { throw; }
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

                var already = await _context.NetworkLinks.FirstOrDefaultAsync(l =>
                    (l.AInterfaceId == a.Id && l.BInterfaceId == b.Id)
                    || (l.AInterfaceId == b.Id && l.BInterfaceId == a.Id), ct);
                if (already is not null)
                {
                    already.RefreshEvidence((LinkType)c.LinkType, (DiscoverySource)c.DiscoverySource, c.Confidence);
                    continue;
                }

                _context.NetworkLinks.Add(new NetworkLink(a.Id, b.Id, (LinkType)c.LinkType,
                    (DiscoverySource)c.DiscoverySource, c.Confidence));
                linkCount++;
            }


            var staleBefore = DateTime.UtcNow.AddHours(-Math.Max(1, _options.LinkStaleAfterHours));
            var expiredLinks = await _context.NetworkLinks
                .Where(l => !l.IsManual && l.LastSeenAtUtc < staleBefore)
                .ToListAsync(ct);
            foreach (var expired in expiredLinks) expired.MarkStale();

            await _context.SaveChangesAsync(ct);

            run.Complete(found, added, updated, linkCount, pending, failures,
                $"Encontrados {found}, nuevos {added}, actualizados {updated}, enlaces {linkCount}");
        }
        catch (OperationCanceledException)
        {
            // Una cancelación solicitada sobre la corrida ya fue persistida por
            // DiscoveryService. La cancelación del host deja Running + lease para
            // que otra instancia la recupere al vencer.
            await _context.Entry(run).ReloadAsync(CancellationToken.None);
            if (run.Status == DiscoveryRunStatus.Cancelled) return;
            throw;
        }
        catch (Exception ex)
        {
            run.Fail(ex.Message);
            _logger.LogError(ex, "Fallo en DiscoveryRun {Id}", runId);
        }

        await _context.SaveChangesAsync(ct);
    }

    internal static IReadOnlyList<string> ParseTargets(string scopeIp, int maxTargets)
    {
        if (maxTargets < 1) throw new ArgumentOutOfRangeException(nameof(maxTargets));
        var result = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var part in scopeIp.Split(new[] { ',', ';', ' ', '\n' }, StringSplitOptions.RemoveEmptyEntries))
        {
            var p = part.Trim();
            if (string.IsNullOrEmpty(p)) continue;
            if (IPAddress.TryParse(p, out var singleIp))
            {
                if (singleIp.AddressFamily != System.Net.Sockets.AddressFamily.InterNetwork)
                    throw new ArgumentException($"Sólo se admiten destinos IPv4: {p}", nameof(scopeIp));
                AddTarget(singleIp.ToString());
                continue;
            }

            if (p.Contains('/') && CidrSubnet.TryParse(p, out var network))
            {
                if (network.UsableHostCount > (ulong)maxTargets)
                    throw new ArgumentException($"El scope supera el máximo de {maxTargets} destinos.", nameof(scopeIp));
                foreach (var addr in network.EnumerateHosts()) AddTarget(addr.ToString());
            }
            else
            {
                throw new ArgumentException($"Destino IPv4/CIDR inválido: {p}", nameof(scopeIp));
            }
        }
        return result.ToList();

        void AddTarget(string target)
        {
            result.Add(target);
            if (result.Count > maxTargets)
                throw new ArgumentException($"El scope supera el máximo de {maxTargets} destinos.", nameof(scopeIp));
        }
    }

    internal static async Task<IReadOnlyList<string>> ProbeLiveTargetsAsync(
        IReadOnlyList<string> targets, IIcmpProbe probe, DiscoveryOptions options, CancellationToken ct)
    {
        var live = new ConcurrentBag<string>();
        await Parallel.ForEachAsync(targets, new ParallelOptions
        {
            CancellationToken = ct,
            MaxDegreeOfParallelism = Math.Max(1, options.MaxConcurrentPing)
        }, async (ip, token) =>
        {
            var ping = await probe.PingAsync(ip, Math.Max(1, options.PingTimeoutMs), token);
            if (ping.Success) live.Add(ip);
        });
        return live.ToList();
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

    private static NeighborProtocol ParseNeighborProtocol(string protocol) => protocol.ToLowerInvariant() switch
    {
        "lldp" => NeighborProtocol.Lldp,
        "cdp" => NeighborProtocol.Cdp,
        "mikrotik" => NeighborProtocol.MikroTik,
        "ubiquiti" => NeighborProtocol.Ubiquiti,
        "wireless" => NeighborProtocol.Wireless,
        _ => NeighborProtocol.Unknown
    };
}
