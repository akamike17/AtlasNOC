using AtlasNOC.Application.Devices;
using AtlasNOC.Application.Probes;

namespace AtlasNOC.Infrastructure.Devices;

/// <summary>Driver directo para equipos airOS/AirMAX mediante SNMP; no usa el UniFi Controller.</summary>
public sealed class AirOsDeviceDriver(ISnmpProbe snmp) : IDeviceDriver, ISnmpCredentialAwareDriver
{
    private static readonly SnmpConnectionOptions Fallback = SnmpConnectionOptions.Anonymous();
    public string DriverKey => "ubiquiti-airos";
    public bool CanHandle(DeviceFingerprint fp)
    {
        var text = $"{fp.SysDescription} {fp.SysName}".ToLowerInvariant();
        return text.Contains("airos") || text.Contains("airmax");
    }
    public Task<DeviceIdentity> GetIdentityAsync(string ip, CancellationToken ct) => GetIdentityAsync(ip, Fallback, ct);
    public Task<DeviceIdentity> GetIdentityAsync(string ip, SnmpConnectionOptions options, CancellationToken ct) => snmp.GetIdentityAsync(ip, options, 2000, ct);
    public Task<IReadOnlyList<InterfaceData>> GetInterfacesAsync(string ip, CancellationToken ct) => GetInterfacesAsync(ip, Fallback, ct);
    public Task<IReadOnlyList<InterfaceData>> GetInterfacesAsync(string ip, SnmpConnectionOptions options, CancellationToken ct) => snmp.GetInterfacesAsync(ip, options, 2000, ct);
    public Task<IReadOnlyList<NeighborData>> GetNeighborsAsync(string ip, CancellationToken ct) => GetNeighborsAsync(ip, Fallback, ct);
    public Task<IReadOnlyList<NeighborData>> GetNeighborsAsync(string ip, SnmpConnectionOptions options, CancellationToken ct) => snmp.GetLldpNeighborsAsync(ip, options, 2000, ct);
    public Task<HealthData> GetHealthAsync(string ip, CancellationToken ct) => snmp.GetHealthAsync(ip, Fallback, 2000, ct);
    public async Task<IReadOnlyList<MetricDatum>> GetMetricsAsync(string ip, CancellationToken ct)
    {
        var health = await GetHealthAsync(ip, ct); var result = new List<MetricDatum>();
        if (health.CpuPercent.HasValue) result.Add(new("cpu_usage", health.CpuPercent.Value, "%"));
        if (health.MemoryPercent.HasValue) result.Add(new("memory_usage", health.MemoryPercent.Value, "%"));
        return result;
    }
    public Task<IReadOnlyList<WirelessClientData>> GetWirelessAssociationsAsync(string ip, CancellationToken ct)
        => Task.FromResult<IReadOnlyList<WirelessClientData>>(Array.Empty<WirelessClientData>());
}
