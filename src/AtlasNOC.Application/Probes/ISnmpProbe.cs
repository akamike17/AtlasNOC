namespace AtlasNOC.Application.Probes;

/// <summary>Probe SNMP genérico (sysName, sysObjectID, interfaces, uptime, LLDP/CDP).</summary>
public interface ISnmpProbe
{
    Task<DeviceFingerprint?> FingerprintAsync(string ipAddress, string community, int timeoutMs, CancellationToken ct);

    Task<IReadOnlyList<InterfaceData>> GetInterfacesAsync(string ipAddress, string community, int timeoutMs, CancellationToken ct);

    Task<IReadOnlyList<NeighborData>> GetLldpNeighborsAsync(string ipAddress, string community, int timeoutMs, CancellationToken ct);

    Task<DeviceIdentity> GetIdentityAsync(string ipAddress, string community, int timeoutMs, CancellationToken ct);

    Task<HealthData> GetHealthAsync(string ipAddress, string community, int timeoutMs, CancellationToken ct);
}