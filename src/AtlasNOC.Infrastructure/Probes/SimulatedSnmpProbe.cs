using AtlasNOC.Application.Probes;
using AtlasNOC.Infrastructure.Devices;

namespace AtlasNOC.Infrastructure.Probes;

/// <summary>
/// Probe SNMP simulado para el modo LAB (§18). Devuelve fingerprint e identidad deterministas
/// para las IPs de LAB-01 y marca el vendor hint como "simulated" para que el
/// SimulatedNetworkDriver se seleccione. Fuera del modo LAB delega en el SNMP real.
/// </summary>
public class SimulatedSnmpProbe : ISnmpProbe
{
    private readonly bool _enabled;
    private readonly ISnmpProbe _real;

    public SimulatedSnmpProbe(bool enabled, ISnmpProbe real)
    {
        _enabled = enabled;
        _real = real;
    }

    public Task<DeviceFingerprint?> FingerprintAsync(string ipAddress, string community, int timeoutMs, CancellationToken ct)
    {
        if (!_enabled)
            return _real.FingerprintAsync(ipAddress, community, timeoutMs, ct);

        var node = LabTopology.Find(ipAddress);
        if (node is null) return Task.FromResult<DeviceFingerprint?>(null);

        var fp = new DeviceFingerprint(
            ipAddress,
            node.Value.Hostname,
            node.Value.Vendor == "ubiquiti" ? "1.3.6.1.4.1.41112" : "1.3.6.1.4.1.LAB",
            node.Value.SysDescription,
            "simulated");
        return Task.FromResult<DeviceFingerprint?>(fp);
    }

    public Task<IReadOnlyList<InterfaceData>> GetInterfacesAsync(string ipAddress, string community, int timeoutMs, CancellationToken ct)
        => _enabled
            ? Task.FromResult<IReadOnlyList<InterfaceData>>(Array.Empty<InterfaceData>())
            : _real.GetInterfacesAsync(ipAddress, community, timeoutMs, ct);

    public Task<IReadOnlyList<NeighborData>> GetLldpNeighborsAsync(string ipAddress, string community, int timeoutMs, CancellationToken ct)
        => _enabled
            ? Task.FromResult<IReadOnlyList<NeighborData>>(Array.Empty<NeighborData>())
            : _real.GetLldpNeighborsAsync(ipAddress, community, timeoutMs, ct);

    public Task<DeviceIdentity> GetIdentityAsync(string ipAddress, string community, int timeoutMs, CancellationToken ct)
    {
        if (!_enabled)
            return _real.GetIdentityAsync(ipAddress, community, timeoutMs, ct);

        var node = LabTopology.Find(ipAddress);
        return Task.FromResult(new DeviceIdentity(
            node?.Hostname ?? ipAddress, node?.DeviceType, $"LAB-{node?.Hostname}", "1.0.0",
            node?.Vendor == "ubiquiti" ? "1.3.6.1.4.1.41112" : "1.3.6.1.4.1.LAB"));
    }

    public Task<HealthData> GetHealthAsync(string ipAddress, string community, int timeoutMs, CancellationToken ct)
        => _enabled
            ? Task.FromResult(new HealthData(1.0, 100.0, 45.0, 55.0, 1_000_000))
            : _real.GetHealthAsync(ipAddress, community, timeoutMs, ct);
}