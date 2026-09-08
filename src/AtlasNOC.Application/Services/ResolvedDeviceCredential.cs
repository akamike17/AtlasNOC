using AtlasNOC.Domain.Enums;

namespace AtlasNOC.Application.Services;

/// <summary>
/// Credencial de dispositivo resuelta y descifrada, lista para su uso efímero
/// por el pipeline de descubrimiento o polling. Los secretos sólo existen en
/// memoria el tiempo necesario y nunca se devuelven a una vista.
/// </summary>
public sealed record ResolvedDeviceCredential(
    Guid Id,
    string Name,
    SnmpVersion SnmpVersion,
    string? Community,
    string? UserName,
    string? AuthProtocol,
    string? AuthPassword,
    string? PrivProtocol,
    string? PrivPassword)
{
    /// <summary>Convierte a las opciones de conexión usadas por el probe SNMP.</summary>
    public Probes.SnmpConnectionOptions ToConnectionOptions() => new(
        SnmpVersion,
        Community,
        UserName,
        AuthProtocol,
        AuthPassword,
        PrivProtocol,
        PrivPassword);
}