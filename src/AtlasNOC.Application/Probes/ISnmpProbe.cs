namespace AtlasNOC.Application.Probes;

/// <summary>Probe SNMP genérico (sysName, sysObjectID, interfaces, uptime, LLDP/CDP).</summary>
public interface ISnmpProbe
{
    Task<DeviceFingerprint?> FingerprintAsync(string ipAddress, SnmpConnectionOptions options, int timeoutMs, CancellationToken ct);

    Task<IReadOnlyList<InterfaceData>> GetInterfacesAsync(string ipAddress, SnmpConnectionOptions options, int timeoutMs, CancellationToken ct);

    Task<IReadOnlyList<NeighborData>> GetLldpNeighborsAsync(string ipAddress, SnmpConnectionOptions options, int timeoutMs, CancellationToken ct);

    Task<DeviceIdentity> GetIdentityAsync(string ipAddress, SnmpConnectionOptions options, int timeoutMs, CancellationToken ct);

    Task<HealthData> GetHealthAsync(string ipAddress, SnmpConnectionOptions options, int timeoutMs, CancellationToken ct);
}