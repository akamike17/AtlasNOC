namespace AtlasNOC.Domain.Entities;

public class ApiKey
{
    public Guid Id { get; init; }
    public string Owner { get; init; } = string.Empty;
    public string Role { get; init; } = "ReadOnly";
    public string? Description { get; init; }
    public string KeyHash { get; init; } = string.Empty;
    public DateTime CreatedAt { get; init; }
    public bool IsActive { get; private set; }

    private ApiKey() { }

    public ApiKey(Guid id, string owner, string? description, string keyHash,
        DateTime createdAt, bool isActive, string role = "ReadOnly")
    {
        Id = id;
        Owner = owner ?? throw new ArgumentNullException(nameof(owner));
        Role = role ?? throw new ArgumentNullException(nameof(role));
        Description = description;
        KeyHash = keyHash ?? throw new ArgumentNullException(nameof(keyHash));
        CreatedAt = createdAt;
        IsActive = isActive;
    }

    public void Revoke() => IsActive = false;
}
