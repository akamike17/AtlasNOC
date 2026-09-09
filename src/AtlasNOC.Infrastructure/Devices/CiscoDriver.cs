using AtlasNOC.Application.Devices;
using AtlasNOC.Application.Probes;

namespace AtlasNOC.Infrastructure.Devices;

/// <summary>
/// Driver para equipos Cisco (IOS, IOS-XE, NX-OS) vía SNMP v2c/v3.
/// Soporta CDP y LLDP para descubrimiento de vecinos.
/// </summary>
public sealed class CiscoDriver(ISnmpProbe snmp) : IDeviceDriver, ISnmpCredentialAwareDriver
{
    private static readonly SnmpConnectionOptions Fallback = SnmpConnectionOptions.Anonymous();

    public string DriverKey => "cisco";

    public bool CanHandle(DeviceFingerprint fp)
    {
        var text = $"{fp.SysObjectId} {fp.SysDescription} {fp.SysName}".ToLowerInvariant();
        return text.Contains("cisco")
            || (fp.SysObjectId?.StartsWith("1.3.6.1.4.1.9") ?? false); // Cisco enterprise OID
    }

    public Task<DeviceIdentity> GetIdentityAsync(string ip, CancellationToken ct)
        => GetIdentityAsync(ip, Fallback, ct);

    public Task<DeviceIdentity> GetIdentityAsync(string ip, SnmpConnectionOptions options, CancellationToken ct)
        => snmp.GetIdentityAsync(ip, options, 2000, ct);

    public Task<IReadOnlyList<InterfaceData>> GetInterfacesAsync(string ip, CancellationToken ct)
        => GetInterfacesAsync(ip, Fallback, ct);

    public Task<IReadOnlyList<InterfaceData>> GetInterfacesAsync(string ip, SnmpConnectionOptions options, CancellationToken ct)
        => snmp.GetInterfacesAsync(ip, options, 2000, ct);

    public Task<IReadOnlyList<NeighborData>> GetNeighborsAsync(string ip, CancellationToken ct)
        => GetNeighborsAsync(ip, Fallback, ct);

    public Task<IReadOnlyList<NeighborData>> GetNeighborsAsync(string ip, SnmpConnectionOptions options, CancellationToken ct)
        => snmp.GetCdpNeighborsAsync(ip, options, 2000, ct)
            .ContinueWith(t =>
            {
                var cdp = t.Result;
                var lldp = snmp.GetLldpNeighborsAsync(ip, options, 2000, ct).Result;
                return MergeNeighbors(cdp, lldp);
            }, ct);

    public Task<HealthData> GetHealthAsync(string ip, CancellationToken ct)
        => snmp.GetHealthAsync(ip, Fallback, 2000, ct);

    public async Task<IReadOnlyList<MetricDatum>> GetMetricsAsync(string ip, CancellationToken ct)
    {
        var health = await GetHealthAsync(ip, ct);
        var result = new List<MetricDatum>();
        if (health.LatencyMs.HasValue) result.Add(new("rtt", health.LatencyMs.Value, "ms"));
        if (health.CpuPercent.HasValue) result.Add(new("cpu_usage", health.CpuPercent.Value, "%"));
        if (health.MemoryPercent.HasValue) result.Add(new("memory_usage", health.MemoryPercent.Value, "%"));
        return result;
    }

    public Task<IReadOnlyList<WirelessClientData>> GetWirelessAssociationsAsync(string ip, CancellationToken ct)
        => Task.FromResult<IReadOnlyList<WirelessClientData>>(Array.Empty<WirelessClientData>());

    private static IReadOnlyList<NeighborData> MergeNeighbors(IReadOnlyList<NeighborData> cdp, IReadOnlyList<NeighborData> lldp)
    {
        var merged = new List<NeighborData>();
        merged.AddRange(cdp);
        foreach (var l in lldp)
        {
            // Evitar duplicados por RemoteIdentity + LocalInterfaceName
            if (!merged.Any(m => m.RemoteIdentity == l.RemoteIdentity && m.LocalInterfaceName == l.LocalInterfaceName))
                merged.Add(l);
        }
        return merged;
    }
}