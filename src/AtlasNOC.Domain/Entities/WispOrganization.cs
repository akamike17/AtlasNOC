using AtlasNOC.Domain.ValueObjects;

namespace AtlasNOC.Domain.Entities;

/// <summary>Organización WISP propietaria de la instancia.</summary>
public class WispOrganization
{
    public OrganizationId Id { get; private set; } = null!;
    public string Name { get; private set; } = string.Empty;
    public string? Code { get; private set; }
    public string? TimeZoneId { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }
    public bool IsActive { get; private set; } = true;

    private WispOrganization() { }

    public WispOrganization(string name, string? code = null, string? timeZoneId = null)
    {
        Id = OrganizationId.New();
        Name = name ?? throw new ArgumentNullException(nameof(name));
        Code = code;
        TimeZoneId = timeZoneId;
        CreatedAtUtc = DateTime.UtcNow;
    }
}