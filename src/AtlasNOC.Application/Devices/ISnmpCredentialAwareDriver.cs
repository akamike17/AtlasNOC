using AtlasNOC.Application.Probes;

namespace AtlasNOC.Application.Devices;

/// <summary>
/// Capacidad opcional para drivers cuya adquisición usa la credencial SNMP
/// seleccionada por el discovery. Evita almacenar secretos en el driver.
/// </summary>
public interface ISnmpCredentialAwareDriver
{
    Task<DeviceIdentity> GetIdentityAsync(string managementIp, SnmpConnectionOptions options, CancellationToken ct);
    Task<IReadOnlyList<InterfaceData>> GetInterfacesAsync(string managementIp, SnmpConnectionOptions options, CancellationToken ct);
    Task<IReadOnlyList<NeighborData>> GetNeighborsAsync(string managementIp, SnmpConnectionOptions options, CancellationToken ct);
}
