namespace AtlasNOC.Domain.Entities;

/// <summary>API key de integración. Nunca se usa como login humano; solo para automatización externa.</summary>
public class ApiKey
{
    public Guid Id { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public string OwnerUserId { get; private set; } = string.Empty;
    public string KeyHash { get; private set; } = string.Empty;
    public string KeyPrefix { get; private set; } = string.Empty;
    public string Description { get; private set; } = string.Empty;
    public string Scopes { get; private set; } = string.Empty;
    public DateTime CreatedAtUtc { get; private set; }
    public DateTime? ExpiresAtUtc { get; private set; }
    public DateTime? RevokedAtUtc { get; private set; }
    public bool IsActive { get; private set; }

    private ApiKey() { }

    public ApiKey(Guid id, string name, string ownerUserId, string keyHash, string keyPrefix,
        string description, string scopes, DateTime? expiresAtUtc)
    {
        Id = id;
        Name = name ?? throw new ArgumentNullException(nameof(name));
        OwnerUserId = ownerUserId ?? throw new ArgumentNullException(nameof(ownerUserId));
        KeyHash = keyHash ?? throw new ArgumentNullException(nameof(keyHash));
        KeyPrefix = keyPrefix ?? throw new ArgumentNullException(nameof(keyPrefix));
        Description = description ?? string.Empty;
        Scopes = scopes ?? string.Empty;
        CreatedAtUtc = DateTime.UtcNow;
        ExpiresAtUtc = expiresAtUtc;
        IsActive = true;
    }

    public static ApiKey Create(string name, string ownerUserId, string keyHash, string keyPrefix,
        string description, string scopes, DateTime? expiresAtUtc = null)
        => new(Guid.NewGuid(), name, ownerUserId, keyHash, keyPrefix, description, scopes, expiresAtUtc);

    public bool IsExpired => ExpiresAtUtc.HasValue && ExpiresAtUtc.Value < DateTime.UtcNow;
    public bool CanUse => IsActive && !IsExpired;

    public void Revoke() => RevokedAtUtc = DateTime.UtcNow;
}