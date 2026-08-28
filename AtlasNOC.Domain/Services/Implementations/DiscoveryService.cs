using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using AtlasNOC.Domain.Entities;
using AtlasNOC.Domain.Enums;
using AtlasNOC.Domain.Services.Interfaces;
using AtlasNOC.Domain.ValueObjects;

namespace AtlasNOC.Domain.Services;

public sealed class DiscoveryService : IDiscoveryService
{
    private readonly IRepository<Device> _deviceRepository;
    private readonly IRepository<DiscoveryRun> _runRepository;
    private readonly ICredentialService _credentialService;
    private readonly ISnmpService _snmpService;
    private readonly ILogger<DiscoveryService> _logger;
    private readonly ConcurrentDictionary<Guid, DiscoveryResult> _runningDiscoveries = new();

    public DiscoveryService(
        IRepository<Device> deviceRepository,
        IRepository<DiscoveryRun> runRepository,
        ICredentialService credentialService,
        ISnmpService snmpService,
        ILogger<DiscoveryService> logger)
    {
        _deviceRepository = deviceRepository ?? throw new ArgumentNullException(nameof(deviceRepository));
        _runRepository = runRepository ?? throw new ArgumentNullException(nameof(runRepository));
        _credentialService = credentialService ?? throw new ArgumentNullException(nameof(credentialService));
        _snmpService = snmpService ?? throw new ArgumentNullException(nameof(snmpService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<DiscoveryResult> DiscoverAsync(DiscoveryRequest request, CancellationToken cancellationToken = default)
    {
        var discoveryId = Guid.NewGuid();
        var startedAt = DateTime.UtcNow;

        var result = new DiscoveryResult(
            Id: discoveryId,
            StartedAt: startedAt,
            CompletedAt: null,
            Status: DiscoveryStatus.Running,
            Devices: Array.Empty<DiscoveredDevice>(),
            TargetsScanned: 0,
            TargetsReachable: 0,
            ErrorMessage: null
        );

        _runningDiscoveries[discoveryId] = result;
        var persistedRun = DiscoveryRun.Start(discoveryId, request.SubnetCidr, startedAt);
        // Persist the run envelope even when the caller is already cancelled so
        // cancelled executions remain auditable across restarts.
        await _runRepository.AddAsync(persistedRun, CancellationToken.None);

        try
        {
            if (!IPNetwork.TryParse(request.SubnetCidr, out var network))
            {
                throw new ArgumentException($"Invalid CIDR notation: {request.SubnetCidr}", nameof(request.SubnetCidr));
            }

            if (request.Options.MaxConcurrency is < 1 or > 256)
                throw new ArgumentOutOfRangeException(nameof(request.Options.MaxConcurrency),
                    "Discovery concurrency must be between 1 and 256.");
            if (request.Options.MaxTargets is < 1 or > 65536)
                throw new ArgumentOutOfRangeException(nameof(request.Options.MaxTargets),
                    "Discovery target limit must be between 1 and 65536.");
            if (network!.UsableAddressCount > (ulong)request.Options.MaxTargets)
                throw new ArgumentException(
                    $"CIDR contains {network.UsableAddressCount} targets, exceeding the configured limit of {request.Options.MaxTargets}.",
                    nameof(request.SubnetCidr));

            var targetCount = checked((int)network.UsableAddressCount);
            var requestedCredentialIds = request.CredentialIds
                .Select(id => id.Value)
                .ToHashSet();
            var credentials = requestedCredentialIds.Count == 0
                ? Array.Empty<Credential>()
                : (await _credentialService.GetActiveAsync(cancellationToken))
                    .Where(credential => requestedCredentialIds.Contains(credential.Id.Value))
                    .ToArray();
            var discoveredDevices = new ConcurrentBag<DiscoveredDevice>();
            await Parallel.ForEachAsync(
                network.EnumerateIPAddresses(),
                new ParallelOptions
                {
                    MaxDegreeOfParallelism = request.Options.MaxConcurrency,
                    CancellationToken = cancellationToken
                },
                async (ip, token) =>
                {
                    var discovered = await DiscoverSingleIpAsync(ip, credentials, request.Options, token);
                    if (discovered != null)
                    {
                        discoveredDevices.Add(discovered);
                    }
                });

            await PersistDiscoveredDevicesAsync(discoveredDevices, cancellationToken);

            var completed = new DiscoveryResult(
                Id: discoveryId,
                StartedAt: startedAt,
                CompletedAt: DateTime.UtcNow,
                Status: DiscoveryStatus.Completed,
                Devices: discoveredDevices.ToList(),
                TargetsScanned: targetCount,
                TargetsReachable: discoveredDevices.Count,
                ErrorMessage: null
            );

            _runningDiscoveries[discoveryId] = completed;
            persistedRun.Complete(completed);
            await _runRepository.UpdateAsync(persistedRun, cancellationToken);
            TrimHistory();
            return completed;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            var cancelled = new DiscoveryResult(
                discoveryId, startedAt, DateTime.UtcNow, DiscoveryStatus.Cancelled,
                Array.Empty<DiscoveredDevice>(), 0, 0, "Discovery was cancelled.");
            _runningDiscoveries[discoveryId] = cancelled;
            persistedRun.Complete(cancelled);
            await _runRepository.UpdateAsync(persistedRun, CancellationToken.None);
            TrimHistory();
            return cancelled;
        }
        catch (Exception ex)
        {
            var failed = new DiscoveryResult(
                Id: discoveryId,
                StartedAt: startedAt,
                CompletedAt: DateTime.UtcNow,
                Status: DiscoveryStatus.Failed,
                Devices: Array.Empty<DiscoveredDevice>(),
                TargetsScanned: 0,
                TargetsReachable: 0,
                ErrorMessage: ex.Message
            );

            _runningDiscoveries[discoveryId] = failed;
            persistedRun.Complete(failed);
            await _runRepository.UpdateAsync(persistedRun, CancellationToken.None);
            TrimHistory();
            _logger.LogError(ex, "Discovery {DiscoveryId} failed", discoveryId);
            return failed;
        }
    }

    public async Task<DiscoveryResult?> GetDiscoveryAsync(Guid discoveryId, CancellationToken cancellationToken = default)
    {
        if (_runningDiscoveries.TryGetValue(discoveryId, out var result)) return result;
        var persisted = await _runRepository.GetByIdAsync(discoveryId, cancellationToken);
        return persisted is null ? null : FromRun(persisted);
    }

    public async Task<IReadOnlyList<DiscoveredDevice>> GetDiscoveredDevicesAsync(CancellationToken cancellationToken = default)
    {
        var latest = (await _runRepository.GetAllAsync(cancellationToken))
            .Where(r => r.Status == DiscoveryStatus.Completed)
            .OrderByDescending(r => r.StartedAt)
            .FirstOrDefault();
        return latest is null ? Array.Empty<DiscoveredDevice>() : latest.ToResult().Devices;
    }

    public async Task<DiscoveredDevice?> GetDiscoveredDeviceAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var latest = (await _runRepository.GetAllAsync(cancellationToken))
            .Where(r => r.Status == DiscoveryStatus.Completed)
            .OrderByDescending(r => r.StartedAt)
            .FirstOrDefault();
        return latest is null ? null : latest.ToResult().Devices.FirstOrDefault(d => d.Id == id);
    }

    public async Task<IReadOnlyList<DiscoveryResult>> GetHistoryAsync(int count = 50, CancellationToken cancellationToken = default)
    {
        var persisted = (await _runRepository.GetAllAsync(cancellationToken))
            .Select(FromRun)
            .ToList();
        var byId = persisted.ToDictionary(r => r.Id);
        foreach (var running in _runningDiscoveries.Values)
            byId[running.Id] = running; // in-memory state overrides persisted envelope for the current process
        return byId.Values
            .OrderByDescending(r => r.StartedAt)
            .Take(count)
            .ToList();
    }

    private static DiscoveryResult FromRun(DiscoveryRun run) => run.ToResult();

    private async Task<DiscoveredDevice?> DiscoverSingleIpAsync(
        IPAddress ip,
        IReadOnlyList<Credential> credentials,
        DiscoveryOptions options,
        CancellationToken cancellationToken)
    {
        var ipStr = ip.ToString();

        // Ping test
        var pingable = await PingAsync(ip, options.PingTimeout, cancellationToken);
        if (!pingable)
            return null;

        // Try SNMP discovery with available credentials
        var snmpTimeout = options.SnmpTimeout != default ? options.SnmpTimeout : TimeSpan.FromSeconds(5);

        foreach (var credential in credentials.Where(c => c.CanUse))
        {
            cancellationToken.ThrowIfCancellationRequested();

            var testResult = await _snmpService.TestConnectionAsync(ip, credential, snmpTimeout, cancellationToken);

            if (!testResult.Success)
                continue;

            var sysDescr = await TryGetSnmpValueAsync(ip, credential, "1.3.6.1.2.1.1.1.0", snmpTimeout, cancellationToken); // sysDescr
            var sysObjectId = await TryGetSnmpValueAsync(ip, credential, "1.3.6.1.2.1.1.2.0", snmpTimeout, cancellationToken); // sysObjectID
            var sysName = await TryGetSnmpValueAsync(ip, credential, "1.3.6.1.2.1.1.5.0", snmpTimeout, cancellationToken); // sysName
            var sysUpTime = await TryGetSnmpValueAsync(ip, credential, "1.3.6.1.2.1.1.3.0", snmpTimeout, cancellationToken); // sysUpTime

            if (!string.IsNullOrEmpty(sysDescr) || !string.IsNullOrEmpty(sysObjectId))
            {
                var vendor = IdentifyVendor(sysDescr, sysObjectId);
                var deviceType = InferDeviceType(sysDescr, sysObjectId);

                var interfaces = await DiscoverInterfacesAsync(ip, credential, snmpTimeout, cancellationToken);
                var neighbors = new List<DiscoveredNeighbor>();

                if (options.EnableLldp)
                    neighbors.AddRange(await DiscoverLldpNeighborsAsync(ip, credential, snmpTimeout, cancellationToken));

                if (options.EnableCdp)
                    neighbors.AddRange(await DiscoverCdpNeighborsAsync(ip, credential, snmpTimeout, cancellationToken));

                if (options.EnableArp)
                    neighbors.AddRange(await DiscoverArpNeighborsAsync(ip, credential, snmpTimeout, cancellationToken));

                var evidence = new DiscoveryEvidence(
                    HasPing: true,
                    HasSnmp: true,
                    HasLldp: options.EnableLldp && neighbors.Any(n => n.Protocol == NeighborProtocol.Lldp),
                    HasCdp: options.EnableCdp && neighbors.Any(n => n.Protocol == NeighborProtocol.Cdp),
                    HasArp: options.EnableArp && neighbors.Any(n => n.Protocol == NeighborProtocol.Arp),
                    HasMacTable: false,
                    OidsQueried: new[] { "1.3.6.1.2.1.1.1.0", "1.3.6.1.2.1.1.2.0", "1.3.6.1.2.1.1.5.0" },
                    OidsResponded: new[] { sysDescr, sysObjectId, sysName }.Where(s => !string.IsNullOrEmpty(s)).Select(s => s!).ToList()
                );

                return new DiscoveredDevice(
                    Id: Guid.NewGuid(),
                    IpAddress: ipStr,
                    Hostname: sysName,
                    SysDescr: sysDescr,
                    SysObjectId: sysObjectId,
                    Vendor: vendor,
                    DeviceType: deviceType,
                    Interfaces: interfaces,
                    Neighbors: neighbors,
                    DiscoveredAt: DateTime.UtcNow,
                    Evidence: evidence
                );
            }
        }

        // Only ping reachable
        return new DiscoveredDevice(
            Id: Guid.NewGuid(),
            IpAddress: ipStr,
            Hostname: null,
            SysDescr: null,
            SysObjectId: null,
            Vendor: null,
            DeviceType: null,
            Interfaces: Array.Empty<DiscoveredInterface>(),
            Neighbors: Array.Empty<DiscoveredNeighbor>(),
            DiscoveredAt: DateTime.UtcNow,
            Evidence: new DiscoveryEvidence(
                HasPing: true,
                HasSnmp: false,
                HasLldp: false,
                HasCdp: false,
                HasArp: false,
                HasMacTable: false,
                OidsQueried: Array.Empty<string>(),
                OidsResponded: Array.Empty<string>()
            )
        );
    }

    private async Task<string?> TryGetSnmpValueAsync(IPAddress ip, Credential credential, string oid, TimeSpan timeout, CancellationToken cancellationToken)
    {
        try
        {
            var result = await _snmpService.GetAsync(ip, credential, oid, timeout, cancellationToken);
            return result.Success ? result.Value : null;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception)
        {
            return null;
        }
    }

    private async Task<IReadOnlyList<DiscoveredInterface>> DiscoverInterfacesAsync(
        IPAddress ip, Credential credential, TimeSpan timeout, CancellationToken cancellationToken)
    {
        var interfaces = new List<DiscoveredInterface>();

        try
        {
            var ifTableOid = "1.3.6.1.2.1.2.2.1"; // ifEntry
            var walkResult = await _snmpService.WalkAsync(ip, credential, ifTableOid, timeout, cancellationToken);

            if (!walkResult.Success || walkResult.Values == null)
                return Array.Empty<DiscoveredInterface>();

            // IF-MIB ifTable columns are encoded as ...1.<column>.<ifIndex>.
            var grouped = walkResult.Values
                .GroupBy(kvp => GetIfIndexFromOid(kvp.Key))
                .ToDictionary(g => g.Key, g => g.ToDictionary(kvp => GetIfAttributeFromOid(kvp.Key), kvp => kvp.Value));

            foreach (var kvp in grouped)
            {
                var attrs = kvp.Value;

                var ifIndex = kvp.Key;
                var name = attrs.GetValueOrDefault("2") ?? $"if{attrs.GetValueOrDefault("1") ?? ifIndex.ToString()}";
                var descr = name;
                var mac = attrs.GetValueOrDefault("6");
                var adminStatus = ParseInterfaceAdminStatus(attrs.GetValueOrDefault("7"));
                var operStatus = ParseInterfaceOperStatus(attrs.GetValueOrDefault("8"));
                var speed = ParseSpeed(attrs.GetValueOrDefault("5"));

                interfaces.Add(new DiscoveredInterface(
                    IfIndex: ifIndex.ToString(),
                    Name: name,
                    Description: descr,
                    MacAddress: mac,
                    IpAddress: null,
                    AdminStatus: adminStatus,
                    OperStatus: operStatus,
                    Speed: speed,
                    Alias: descr,
                    Vlans: Array.Empty<VlanInfo>()
                ));
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to discover interfaces for {Ip}", ip);
        }

        return interfaces;
    }

    private async Task<IReadOnlyList<DiscoveredNeighbor>> DiscoverLldpNeighborsAsync(
        IPAddress ip, Credential credential, TimeSpan timeout, CancellationToken cancellationToken)
    {
        var neighbors = new List<DiscoveredNeighbor>();

        try
        {
            // LLDP MIB: lldpRemTable (1.0.8802.1.1.2.1.4.1)
            var lldpOid = "1.0.8802.1.1.2.1.4.1.1";
            var walkResult = await _snmpService.WalkAsync(ip, credential, lldpOid, timeout, cancellationToken);

            if (walkResult.Success && walkResult.Values != null)
            {
                neighbors.AddRange(ParseLldpNeighbors(walkResult.Values));
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "LLDP discovery failed for {Ip}", ip);
        }

        return neighbors;
    }

    private async Task<IReadOnlyList<DiscoveredNeighbor>> DiscoverCdpNeighborsAsync(
        IPAddress ip, Credential credential, TimeSpan timeout, CancellationToken cancellationToken)
    {
        var neighbors = new List<DiscoveredNeighbor>();

        try
        {
            // CISCO-CDP-MIB cdpCacheTable.
            var walkResult = await _snmpService.WalkAsync(ip, credential,
                "1.3.6.1.4.1.9.9.23.1.2.1.1", timeout, cancellationToken);
            if (walkResult.Success && walkResult.Values != null)
                neighbors.AddRange(ParseCdpNeighbors(walkResult.Values));
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "CDP discovery failed for {Ip}", ip);
        }

        return neighbors;
    }

    private async Task<IReadOnlyList<DiscoveredNeighbor>> DiscoverArpNeighborsAsync(
        IPAddress ip, Credential credential, TimeSpan timeout, CancellationToken cancellationToken)
    {
        var neighbors = new List<DiscoveredNeighbor>();

        try
        {
            // ARP table: ipNetToMediaTable (1.3.6.1.2.1.4.22)
            var arpOid = "1.3.6.1.2.1.4.22.1.2";
            var walkResult = await _snmpService.WalkAsync(ip, credential, arpOid, timeout, cancellationToken);

            if (walkResult.Success && walkResult.Values != null)
            {
                foreach (var kvp in walkResult.Values)
                {
                    // Parse ARP entries
                    neighbors.Add(new DiscoveredNeighbor(
                        LocalInterface: "unknown",
                        RemoteChassisId: kvp.Value,
                        RemotePortId: "unknown",
                        RemoteSystemName: null,
                        Protocol: NeighborProtocol.Arp,
                        Confidence: 0.7
                    ));
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "ARP discovery failed for {Ip}", ip);
        }

        return neighbors;
    }

    private async Task<bool> PingAsync(IPAddress ip, TimeSpan timeout, CancellationToken cancellationToken)
    {
        try
        {
            using var ping = new Ping();
            var reply = await ping.SendPingAsync(ip, (int)timeout.TotalMilliseconds)
                .WaitAsync(cancellationToken);
            return reply.Status == IPStatus.Success;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception)
        {
            return false;
        }
    }

    private static string IdentifyVendor(string? sysDescr, string? sysObjectId)
    {
        if (string.IsNullOrWhiteSpace(sysDescr) && string.IsNullOrWhiteSpace(sysObjectId))
            return "Unknown";

        var text = (sysDescr ?? "") + " " + (sysObjectId ?? "");
        text = text.ToLowerInvariant();

        if (text.Contains("cisco")) return "Cisco";
        if (text.Contains("juniper")) return "Juniper";
        if (text.Contains("arista")) return "Arista";
        if (text.Contains("huawei")) return "Huawei";
        if (text.Contains("hp") || text.Contains("hewlett")) return "HP";
        if (text.Contains("dell")) return "Dell";
        if (text.Contains("fortinet") || text.Contains("fortigate")) return "Fortinet";
        if (text.Contains("palo alto") || text.Contains("panos")) return "Palo Alto";
        if (text.Contains("ubiquiti") || text.Contains("ubnt")) return "Ubiquiti";
        if (text.Contains("mikrotik")) return "MikroTik";

        return "Unknown";
    }

    private static DeviceType? InferDeviceType(string? sysDescr, string? sysObjectId)
    {
        var text = ((sysDescr ?? "") + " " + (sysObjectId ?? "")).ToLowerInvariant();

        if (text.Contains("router")) return DeviceType.Router;
        if (text.Contains("switch")) return DeviceType.Switch;
        if (text.Contains("firewall")) return DeviceType.Firewall;
        if (text.Contains("access point") || text.Contains("ap ") || text.Contains("wireless")) return DeviceType.AccessPoint;
        if (text.Contains("load balancer") || text.Contains("lb ")) return DeviceType.Other;
        if (text.Contains("server")) return DeviceType.Server;

        return null;
    }

    private static int GetIfIndexFromOid(string oid)
    {
        var parts = oid.Split('.');
        return parts.Length > 0 && int.TryParse(parts[^1], out var index) ? index : 0;
    }

    private static string GetIfAttributeFromOid(string oid)
    {
        var parts = oid.Split('.');
        return parts.Length > 1 ? parts[^2] : "";
    }

    internal static IReadOnlyList<DiscoveredNeighbor> ParseLldpNeighbors(
        IReadOnlyDictionary<string, string> values)
    {
        const string prefix = "1.0.8802.1.1.2.1.4.1.1.";
        return ParseNeighborTable(values, prefix, chassisColumn: 5, portColumn: 7,
            nameColumn: 9, NeighborProtocol.Lldp, 0.98);
    }

    internal static IReadOnlyList<DiscoveredNeighbor> ParseCdpNeighbors(
        IReadOnlyDictionary<string, string> values)
    {
        const string prefix = "1.3.6.1.4.1.9.9.23.1.2.1.1.";
        // Index is local ifIndex + remote device index. Columns: address=4,
        // deviceId=6 and devicePort=7.
        return ParseNeighborTable(values, prefix, chassisColumn: 4, portColumn: 7,
            nameColumn: 6, NeighborProtocol.Cdp, 0.95);
    }

    private static IReadOnlyList<DiscoveredNeighbor> ParseNeighborTable(
        IReadOnlyDictionary<string, string> values, string prefix, int chassisColumn,
        int portColumn, int nameColumn, NeighborProtocol protocol, double confidence)
    {
        var rows = new Dictionary<string, Dictionary<int, string>>(StringComparer.Ordinal);
        foreach (var pair in values)
        {
            if (!pair.Key.StartsWith(prefix, StringComparison.Ordinal)) continue;
            var suffix = pair.Key[prefix.Length..];
            var separator = suffix.IndexOf('.');
            if (separator <= 0 || !int.TryParse(suffix[..separator], out var column)) continue;
            var index = suffix[(separator + 1)..];
            if (!rows.TryGetValue(index, out var row)) rows[index] = row = new();
            row[column] = pair.Value;
        }

        var result = new List<DiscoveredNeighbor>();
        foreach (var pair in rows.OrderBy(item => item.Key, StringComparer.Ordinal))
        {
            var row = pair.Value;
            if (!row.TryGetValue(chassisColumn, out var chassis) || string.IsNullOrWhiteSpace(chassis) ||
                !row.TryGetValue(portColumn, out var remotePort) || string.IsNullOrWhiteSpace(remotePort))
                continue;
            var localInterface = pair.Key.Split('.')[0];
            row.TryGetValue(nameColumn, out var systemName);
            result.Add(new DiscoveredNeighbor(localInterface, chassis.Trim(), remotePort.Trim(),
                string.IsNullOrWhiteSpace(systemName) ? null : systemName.Trim(), protocol, confidence));
        }
        return result;
    }

    private static InterfaceAdminStatus ParseInterfaceAdminStatus(string? value)
    {
        return value switch
        {
            "1" => InterfaceAdminStatus.Up,
            "2" => InterfaceAdminStatus.Down,
            "3" => InterfaceAdminStatus.Testing,
            _ => InterfaceAdminStatus.Up
        };
    }

    private static InterfaceOperStatus ParseInterfaceOperStatus(string? value)
    {
        return value switch
        {
            "1" => InterfaceOperStatus.Up,
            "2" => InterfaceOperStatus.Down,
            "3" => InterfaceOperStatus.Testing,
            "4" => InterfaceOperStatus.Unknown,
            "5" => InterfaceOperStatus.Dormant,
            "6" => InterfaceOperStatus.NotPresent,
            "7" => InterfaceOperStatus.LowerLayerDown,
            _ => InterfaceOperStatus.Unknown
        };
    }

    private static long? ParseSpeed(string? value)
    {
        if (long.TryParse(value, out var speed))
            return speed;
        return null;
    }

    private async Task PersistDiscoveredDevicesAsync(
        IEnumerable<DiscoveredDevice> discoveredDevices,
        CancellationToken cancellationToken)
    {
        var existingByIp = (await _deviceRepository.GetAllAsync(cancellationToken))
            .GroupBy(device => device.IpAddress, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.OrdinalIgnoreCase);

        foreach (var discovered in discoveredDevices.OrderBy(device => device.IpAddress, StringComparer.OrdinalIgnoreCase))
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (existingByIp.TryGetValue(discovered.IpAddress, out var existing))
            {
                existing.UpdateStatus(DeviceStatus.Up, "DiscoveryService");
                existing.SetLastChecked();
                await _deviceRepository.UpdateAsync(existing, cancellationToken);
                continue;
            }

            var name = string.IsNullOrWhiteSpace(discovered.Hostname)
                ? discovered.IpAddress
                : discovered.Hostname.Trim();
            var description = string.IsNullOrWhiteSpace(discovered.SysDescr)
                ? "Discovered by authorized network scan."
                : discovered.SysDescr.Length <= 1000
                    ? discovered.SysDescr
                    : discovered.SysDescr[..1000];
            var device = Device.Create(
                name,
                discovered.IpAddress,
                discovered.DeviceType ?? DeviceType.Unknown,
                "DiscoveryService",
                description: description);
            device.UpdateStatus(DeviceStatus.Up, "DiscoveryService");
            device.SetLastChecked();
            await _deviceRepository.AddAsync(device, cancellationToken);
            existingByIp[device.IpAddress] = device;
        }
    }

    private void TrimHistory()
    {
        const int maximumRetainedRuns = 100;
        foreach (var id in _runningDiscoveries.Values
                     .OrderByDescending(run => run.StartedAt)
                     .Skip(maximumRetainedRuns)
                     .Select(run => run.Id))
        {
            _runningDiscoveries.TryRemove(id, out _);
        }
    }
}

// Helper class for IP network parsing
internal sealed class IPNetwork
{
    public IPAddress NetworkAddress { get; }
    public int PrefixLength { get; }
    public ulong UsableAddressCount => PrefixLength switch
    {
        32 => 1,
        31 => 2,
        _ => (1UL << (32 - PrefixLength)) - 2
    };

    private IPNetwork(IPAddress address, int prefix)
    {
        NetworkAddress = address;
        PrefixLength = prefix;
    }

    public static bool TryParse(string cidr, out IPNetwork? network)
    {
        network = null;
        try
        {
            var parts = cidr.Split('/');
            if (parts.Length != 2) return false;

            if (!IPAddress.TryParse(parts[0], out var ip)) return false;
            if (ip.GetAddressBytes().Length != 4) return false;
            if (!int.TryParse(parts[1], out var prefix) || prefix < 0 || prefix > 32) return false;

            network = new IPNetwork(ip, prefix);
            return true;
        }
        catch
        {
            return false;
        }
    }

    public IEnumerable<IPAddress> EnumerateIPAddresses()
    {
        var bytes = NetworkAddress.GetAddressBytes();
        if (bytes.Length != 4) yield break;

        var address = ((uint)bytes[0] << 24) | ((uint)bytes[1] << 16) |
                      ((uint)bytes[2] << 8) | bytes[3];
        var mask = PrefixLength == 0 ? 0U : uint.MaxValue << (32 - PrefixLength);
        var network = address & mask;
        var first = PrefixLength >= 31 ? network : network + 1;

        for (ulong offset = 0; offset < UsableAddressCount; offset++)
        {
            var current = (uint)(first + offset);
            yield return new IPAddress(new[]
            {
                (byte)(current >> 24), (byte)(current >> 16),
                (byte)(current >> 8), (byte)current
            });
        }
    }
}
