using AtlasNOC.Domain.Enums;
using AtlasNOC.Domain.ValueObjects;

namespace AtlasNOC.Domain.Entities;

/// <summary>Credencial de acceso a un equipo. Sus secretos se guardan cifrados (nunca en claro).</summary>
public class DeviceCredential
{
    public CredentialId Id { get; private set; } = null!;
    public string Name { get; private set; } = string.Empty;
    public SnmpVersion SnmpVersion { get; private set; }
    public string? UserName { get; private set; }
    public string? CommunityProtected { get; private set; }
    public string? AuthProtocol { get; private set; }
    public string? AuthPasswordProtected { get; private set; }
    public string? PrivProtocol { get; private set; }
    public string? PrivPasswordProtected { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }
    public DateTime? LastUsedAtUtc { get; private set; }
    public bool IsActive { get; private set; } = true;

    private DeviceCredential() { }

    public DeviceCredential(string name, SnmpVersion snmpVersion, string? userName,
        string? authProtocol, string? privProtocol)
    {
        Id = CredentialId.New();
        Name = name ?? throw new ArgumentNullException(nameof(name));
        SnmpVersion = snmpVersion;
        UserName = userName;
        AuthProtocol = authProtocol;
        PrivProtocol = privProtocol;
        CreatedAtUtc = DateTime.UtcNow;
    }

    public void SetProtectedSecrets(string? communityProtected, string? authPasswordProtected,
        string? privPasswordProtected)
    {
        CommunityProtected = communityProtected;
        AuthPasswordProtected = authPasswordProtected;
        PrivPasswordProtected = privPasswordProtected;
    }

    public void Touch() => LastUsedAtUtc = DateTime.UtcNow;
    public void Deactivate() => IsActive = false;

    public bool CanUse => IsActive;
}