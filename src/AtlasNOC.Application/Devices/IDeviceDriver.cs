using AtlasNOC.Application.Probes;

namespace AtlasNOC.Application.Devices;

/// <summary>
/// Contrato central de adquisición por fabricante. Devuelve DTOs neutrales;
/// el dominio nunca recibe objetos RouterOS/UniFi/SNMP específicos.
/// </summary>
public interface IDeviceDriver
{
    /// <summary>Clave estable del driver (p. ej. "generic-snmp", "mikrotik", "ubiquiti").</summary>
    string DriverKey { get; }

    bool CanHandle(DeviceFingerprint fingerprint);

    Task<DeviceIdentity> GetIdentityAsync(string managementIp, CancellationToken ct);

    Task<IReadOnlyList<InterfaceData>> GetInterfacesAsync(string managementIp, CancellationToken ct);

    Task<IReadOnlyList<NeighborData>> GetNeighborsAsync(string managementIp, CancellationToken ct);

    Task<HealthData> GetHealthAsync(string managementIp, CancellationToken ct);

    Task<IReadOnlyList<MetricDatum>> GetMetricsAsync(string managementIp, CancellationToken ct);

    Task<IReadOnlyList<WirelessClientData>> GetWirelessAssociationsAsync(
        string managementIp, CancellationToken ct);
}