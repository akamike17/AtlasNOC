using AtlasNOC.Application.Devices;
using AtlasNOC.Application.Probes;

namespace AtlasNOC.Infrastructure.Devices;

/// <summary>Driver SNMP genérico: cubre cualquier equipo que exponga MIB-II estándar.</summary>
public class GenericSnmpDriver : IDeviceDriver
{
    private readonly ISnmpProbe _snmp;

    public GenericSnmpDriver(ISnmpProbe snmp) => _snmp = snmp;

    public string DriverKey => "generic-snmp";

    public bool CanHandle(DeviceFingerprint fingerprint) => true;

    public Task<DeviceIdentity> GetIdentityAsync(string managementIp, CancellationToken ct)
        => _snmp.GetIdentityAsync(managementIp, "public", 2000, ct);

    public Task<IReadOnlyList<InterfaceData>> GetInterfacesAsync(string managementIp, CancellationToken ct)
        => _snmp.GetInterfacesAsync(managementIp, "public", 2000, ct);

    public Task<IReadOnlyList<NeighborData>> GetNeighborsAsync(string managementIp, CancellationToken ct)
        => _snmp.GetLldpNeighborsAsync(managementIp, "public", 2000, ct);

    public Task<HealthData> GetHealthAsync(string managementIp, CancellationToken ct)
        => _snmp.GetHealthAsync(managementIp, "public", 2000, ct);

    public Task<IReadOnlyList<MetricDatum>> GetMetricsAsync(string managementIp, CancellationToken ct)
        => _snmp.GetHealthAsync(managementIp, "public", 2000, ct)
            .ContinueWith(h => ToMetrics(h.Result), ct);

    public Task<IReadOnlyList<WirelessClientData>> GetWirelessAssociationsAsync(string managementIp, CancellationToken ct)
        => Task.FromResult<IReadOnlyList<WirelessClientData>>(Array.Empty<WirelessClientData>());

    private static IReadOnlyList<MetricDatum> ToMetrics(HealthData health)
    {
        var result = new List<MetricDatum>();
        if (health.LatencyMs.HasValue) result.Add(new MetricDatum("rtt", health.LatencyMs.Value, "ms"));
        if (health.CpuPercent.HasValue) result.Add(new MetricDatum("cpu_usage", health.CpuPercent.Value, "%"));
        if (health.MemoryPercent.HasValue) result.Add(new MetricDatum("memory_usage", health.MemoryPercent.Value, "%"));
        return result;
    }
}