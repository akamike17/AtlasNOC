using AtlasNOC.Domain.Enums;

namespace AtlasNOC.Application.Probes;

/// <summary>
/// Opciones de conexión SNMP (v1/v2c/v3) para el probe. Sustituye al parámetro
/// único <c>community</c>: permite transportar la credencial completa resuelta
/// (community o user/auth/privacy para v3) sin exponer secretos fuera del
/// pipeline de adquisición.
/// </summary>
public sealed record SnmpConnectionOptions(
    SnmpVersion Version,
    string? Community,
    string? UserName,
    string? AuthProtocol,
    string? AuthPassword,
    string? PrivacyProtocol,
    string? PrivacyPassword)
{
    public void Validate()
    {
        if (Version == SnmpVersion.V2c)
        {
            if (string.IsNullOrWhiteSpace(Community))
                throw new InvalidOperationException("SNMP v2c requiere community string.");
            return;
        }

        if (Version != SnmpVersion.V3) return;
        if (string.IsNullOrWhiteSpace(UserName))
            throw new ArgumentException("SNMP v3 requiere nombre de usuario.");

        var hasAuth = !string.IsNullOrWhiteSpace(AuthProtocol) || !string.IsNullOrWhiteSpace(AuthPassword);
        if (hasAuth && (string.IsNullOrWhiteSpace(AuthProtocol) || string.IsNullOrWhiteSpace(AuthPassword)))
            throw new ArgumentException("SNMP v3 requiere protocolo y contraseña de autenticación juntos.");
        if (!string.IsNullOrWhiteSpace(AuthProtocol)
            && !AuthProtocol.Equals("SHA1", StringComparison.OrdinalIgnoreCase)
            && !AuthProtocol.Equals("SHA-1", StringComparison.OrdinalIgnoreCase)
            && !AuthProtocol.Equals("SHA256", StringComparison.OrdinalIgnoreCase)
            && !AuthProtocol.Equals("SHA-256", StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException("El protocolo de autenticación SNMP v3 no está soportado.");

        var hasPrivacy = !string.IsNullOrWhiteSpace(PrivacyProtocol) || !string.IsNullOrWhiteSpace(PrivacyPassword);
        if (hasPrivacy && !hasAuth)
            throw new ArgumentException("SNMP v3 authPriv requiere autenticación.");
        if (hasPrivacy && (string.IsNullOrWhiteSpace(PrivacyProtocol) || string.IsNullOrWhiteSpace(PrivacyPassword)))
            throw new ArgumentException("SNMP v3 requiere protocolo y contraseña de privacidad juntos.");
        if (!string.IsNullOrWhiteSpace(PrivacyProtocol)
            && !PrivacyProtocol.Equals("AES", StringComparison.OrdinalIgnoreCase)
            && !PrivacyProtocol.Equals("AES128", StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException("El protocolo de privacidad SNMP v3 no está soportado.");
    }

    /// <summary>Opciones "anónimas" (sin credencial): usadas sólo en modo LAB
    /// donde el probe simulado no requiere credencial real.</summary>
    public static SnmpConnectionOptions Anonymous()
        => new(SnmpVersion.V2c, null, null, null, null, null, null);
}
