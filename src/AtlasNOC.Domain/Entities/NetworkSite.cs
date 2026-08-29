using AtlasNOC.Domain.Enums;
using AtlasNOC.Domain.ValueObjects;

namespace AtlasNOC.Domain.Entities;

/// <summary>Sitio/torre de primer nivel (POP, torre, datacenter, gabinete, nodo remoto, oficina).</summary>
public class NetworkSite
{
    public SiteId Id { get; private set; } = null!;
    public OrganizationId OrganizationId { get; private set; } = null!;
    public string Name { get; private set; } = string.Empty;
    public string Code { get; private set; } = string.Empty;
    public SiteType SiteType { get; private set; }
    public SiteId? ParentSiteId { get; private set; }
    public double? Latitude { get; private set; }
    public double? Longitude { get; private set; }
    public string? Address { get; private set; }
    public bool IsActive { get; private set; } = true;
    public DateTime CreatedAtUtc { get; private set; }

    private NetworkSite() { }

    public NetworkSite(OrganizationId organizationId, string name, string code,
        SiteType siteType, SiteId? parentSiteId = null, double? latitude = null,
        double? longitude = null, string? address = null)
    {
        Id = SiteId.New();
        OrganizationId = organizationId ?? throw new ArgumentNullException(nameof(organizationId));
        Name = name ?? throw new ArgumentNullException(nameof(name));
        Code = code ?? throw new ArgumentNullException(nameof(code));
        SiteType = siteType;
        ParentSiteId = parentSiteId;
        Latitude = latitude;
        Longitude = longitude;
        Address = address;
        CreatedAtUtc = DateTime.UtcNow;
    }
}