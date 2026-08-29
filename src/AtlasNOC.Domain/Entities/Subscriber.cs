using AtlasNOC.Domain.ValueObjects;

namespace AtlasNOC.Domain.Entities;

/// <summary>Suscriptor/CPE cliente del WISP.</summary>
public class Subscriber
{
    public Guid Id { get; private set; }
    public OrganizationId OrganizationId { get; private set; } = null!;
    public string Name { get; private set; } = string.Empty;
    public SiteId? SiteId { get; private set; }
    public bool IsActive { get; private set; } = true;
    public DateTime CreatedAtUtc { get; private set; }

    private Subscriber() { }

    public Subscriber(OrganizationId organizationId, string name, SiteId? siteId = null)
    {
        Id = Guid.NewGuid();
        OrganizationId = organizationId ?? throw new ArgumentNullException(nameof(organizationId));
        Name = name ?? throw new ArgumentNullException(nameof(name));
        SiteId = siteId;
        CreatedAtUtc = DateTime.UtcNow;
    }
}