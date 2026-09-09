using AtlasNOC.Application.Dtos;
using AtlasNOC.Application.Repositories;
using AtlasNOC.Application.Services;
using AtlasNOC.Domain.Entities;
using AtlasNOC.Domain.Enums;
using AtlasNOC.Domain.ValueObjects;
using AtlasNOC.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace AtlasNOC.Infrastructure.Services;

public class SiteService : ISiteService
{
    private readonly ISiteRepository _sites;
    private readonly AtlasNOCDbContext _context;

    public SiteService(ISiteRepository sites, AtlasNOCDbContext context)
    {
        _sites = sites;
        _context = context;
    }

    public async Task<IReadOnlyList<SiteDto>> ListSitesAsync(CancellationToken ct = default)
    {
        var sites = await _sites.ListAsync(ct);
        var deviceCounts = await _context.Devices
            .Where(d => d.SiteId != null)
            .GroupBy(d => d.SiteId!)
            .Select(g => new { SiteId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.SiteId, x => x.Count, ct);

        return sites.Select(s => new SiteDto(s.Id.Value, s.Name, s.Code, (int)s.SiteType,
            deviceCounts.TryGetValue(s.Id, out var c) ? c : 0)).ToList();
    }

    public async Task<SiteDto?> GetSiteAsync(Guid id, CancellationToken ct = default)
    {
        var s = await _sites.GetByIdAsync(id, ct);
        if (s is null) return null;
        var count = await _context.Devices.CountAsync(d => d.SiteId == s.Id, ct);
        return new SiteDto(s.Id.Value, s.Name, s.Code, (int)s.SiteType, count);
    }

    public async Task<SiteDto> CreateSiteAsync(CreateSiteRequest request, CancellationToken ct = default)
    {
        var org = await _context.Organizations.FirstOrDefaultAsync(ct);
        var orgId = org?.Id ?? OrganizationId.New();

        var site = new NetworkSite(orgId, request.Name, request.Code,
            (SiteType)request.SiteType, latitude: request.Latitude,
            longitude: request.Longitude, address: request.Address);

        await _sites.AddAsync(site, ct);
        await _context.SaveChangesAsync(ct);
        return new SiteDto(site.Id.Value, site.Name, site.Code, (int)site.SiteType, 0);
    }
}

public class DeviceService : IDeviceService
{
    private readonly IDeviceRepository _devices;
    private readonly AtlasNOCDbContext _context;

    public DeviceService(IDeviceRepository devices, AtlasNOCDbContext context)
    {
        _devices = devices;
        _context = context;
    }

    public async Task<IReadOnlyList<DeviceDto>> ListDevicesAsync(CancellationToken ct = default)
    {
        var devices = await _devices.ListAsync(ct);
        return devices.Select(ToDto).ToList();
    }

    public async Task<DeviceDto?> GetDeviceAsync(Guid id, CancellationToken ct = default)
    {
        var d = await _devices.GetByIdAsync(id, ct);
        return d is null ? null : ToDto(d);
    }

    public async Task<DeviceDto> CreateDeviceAsync(CreateDeviceRequest request, CancellationToken ct = default)
    {
        var existing = await _devices.GetByManagementIpAsync(request.ManagementIp, ct);
        if (existing is not null)
            return ToDto(existing);

        var device = new Device(request.Hostname, request.ManagementIp,
            (DeviceType)request.DeviceType, (Vendor)request.Vendor,
            siteId: request.SiteId.HasValue ? SiteId.From(request.SiteId.Value) : null,
            model: request.Model);

        await _devices.AddAsync(device, ct);
        await _context.SaveChangesAsync(ct);
        return ToDto(device);
    }

    private static DeviceDto ToDto(Device d) => new(
        d.Id.Value, d.Hostname, d.ManagementIp, (int)d.DeviceType, (int)d.Vendor,
        d.Model, (int)d.Status, d.LastSeenAtUtc, d.SiteId?.Value, d.IsManaged);
}

public class LinkService : ILinkService
{
    private readonly ILinkRepository _links;
    private readonly IInterfaceRepository _interfaces;
    private readonly AtlasNOCDbContext _context;

    public LinkService(ILinkRepository links, IInterfaceRepository interfaces, AtlasNOCDbContext context)
    {
        _links = links;
        _interfaces = interfaces;
        _context = context;
    }

    public async Task<IReadOnlyList<LinkDto>> ListLinksAsync(CancellationToken ct = default)
    {
        var links = await _links.ListAsync(ct);
        return links.Select(l => new LinkDto(l.Id.Value, l.AInterfaceId.Value, l.BInterfaceId.Value,
            (int)l.LinkType, (int)l.DiscoverySource, l.Confidence, l.IsConfirmed, l.IsStale, l.IsManual)).ToList();
    }

    public async Task<LinkDetailDto?> GetLinkAsync(Guid id, CancellationToken ct = default)
    {
        var link = await _links.GetByIdAsync(id, ct);
        return link is null ? null : ToDetailDto(link);
    }

    public async Task ConfirmLinkAsync(Guid id, CancellationToken ct = default)
    {
        var link = await _links.GetByIdAsync(id, ct);
        if (link is null) return;
        link.Confirm();
        await _links.UpdateAsync(link, ct);
        await _context.SaveChangesAsync(ct);
    }

    public async Task RejectLinkAsync(Guid id, CancellationToken ct = default)
    {
        var link = await _links.GetByIdAsync(id, ct);
        if (link is null) return;
        link.Reject();
        await _links.UpdateAsync(link, ct);
        await _context.SaveChangesAsync(ct);
    }

    public async Task<LinkDetailDto?> CreateManualLinkAsync(CreateManualLinkRequest request, CancellationToken ct = default)
    {
        // Validación: endpoints distintos y existentes.
        if (request.AInterfaceId == request.BInterfaceId) return null;
        var a = await _interfaces.GetByIdAsync(request.AInterfaceId, ct);
        var b = await _interfaces.GetByIdAsync(request.BInterfaceId, ct);
        if (a is null || b is null) return null;

        var link = new NetworkLink(a.Id, b.Id, (LinkType)request.LinkType,
            DiscoverySource.Manual, confidence: 1.0, isManual: true);

        await _links.AddAsync(link, ct);
        await _context.SaveChangesAsync(ct);
        return ToDetailDto(link);
    }

    public async Task<LinkDetailDto?> UpdateLinkMetadataAsync(UpdateLinkMetadataRequest request, CancellationToken ct = default)
    {
        var link = await _links.GetByIdAsync(request.Id, ct);
        if (link is null) return null;
        link.UpdateMetadata((LinkType)request.LinkType, request.CapacityBps);
        await _links.UpdateAsync(link, ct);
        await _context.SaveChangesAsync(ct);
        return ToDetailDto(link);
    }

    private static LinkDetailDto ToDetailDto(NetworkLink l) => new(
        l.Id.Value, l.AInterfaceId.Value, l.BInterfaceId.Value, (int)l.LinkType,
        (int)l.DiscoverySource, l.Confidence, (int)l.AdminStatus, (int)l.OperStatus,
        l.CapacityBps, l.LastSeenAtUtc, l.IsConfirmed, l.IsStale, l.IsManual);
}

public class InterfaceService : IInterfaceService
{
    private readonly IInterfaceRepository _interfaces;
    private readonly ILinkRepository _links;

    public InterfaceService(IInterfaceRepository interfaces, ILinkRepository links)
    {
        _interfaces = interfaces;
        _links = links;
    }

    public async Task<IReadOnlyList<InterfaceDto>> ListByDeviceAsync(Guid deviceId, CancellationToken ct = default)
    {
        var interfaces = await _interfaces.ListByDeviceAsync(deviceId, ct);
        var ifaceIdSet = interfaces.Select(i => i.Id).ToHashSet();

        // Cuenta enlaces reales que tocan estas interfaces (evidencia, no heurística).
        var allLinks = await _links.ListByDeviceAsync(deviceId, ct);
        var counts = allLinks
            .Where(l => ifaceIdSet.Contains(l.AInterfaceId) || ifaceIdSet.Contains(l.BInterfaceId))
            .SelectMany(l => new[] { l.AInterfaceId, l.BInterfaceId })
            .GroupBy(id => id)
            .ToDictionary(g => g.Key, g => g.Count());

        return interfaces.Select(i => new InterfaceDto(
            i.Id.Value, i.DeviceId.Value, i.IfIndex, i.Name, i.Description, i.MacAddress, i.IpAddress,
            (int)i.AdminStatus, (int)i.OperStatus, i.SpeedBps, i.InterfaceType, i.LastSeenAtUtc,
            counts.TryGetValue(i.Id, out var c) ? c : 0)).ToList();
    }

    public async Task<InterfaceDto?> GetInterfaceAsync(Guid id, CancellationToken ct = default)
    {
        var i = await _interfaces.GetByIdAsync(id, ct);
        if (i is null) return null;

        // Enlaces asociados (los que tocan esta interfaz).
        var allLinks = await _links.ListByDeviceAsync(i.DeviceId.Value, ct);
        var count = allLinks.Count(l => l.AInterfaceId == i.Id || l.BInterfaceId == i.Id);

        return new InterfaceDto(i.Id.Value, i.DeviceId.Value, i.IfIndex, i.Name, i.Description,
            i.MacAddress, i.IpAddress, (int)i.AdminStatus, (int)i.OperStatus, i.SpeedBps,
            i.InterfaceType, i.LastSeenAtUtc, count);
    }
}

public class TopologyService : ITopologyService
{
    private readonly AtlasNOCDbContext _context;

    public TopologyService(AtlasNOCDbContext context) => _context = context;

    public async Task<TopologyGraphDto> GetGraphAsync(TopologyFilter? filter, CancellationToken ct = default)
    {
        var devices = await _context.Devices.AsNoTracking().ToListAsync(ct);
        var interfaces = await _context.DeviceInterfaces.AsNoTracking().ToListAsync(ct);
        var links = await _context.NetworkLinks.AsNoTracking().ToListAsync(ct);
        var sites = await _context.Sites.AsNoTracking().ToListAsync(ct);

        // Map interface -> device for edge endpoint resolution.
        var ifaceDevice = interfaces.ToDictionary(i => i.Id, i => i.DeviceId);

        var filtered = devices.AsEnumerable();
        if (filter is not null)
        {
            if (filter.SiteId.HasValue) filtered = filtered.Where(d => d.SiteId?.Value == filter.SiteId.Value);
            if (filter.Status.HasValue) filtered = filtered.Where(d => (int)d.Status == filter.Status.Value);
            if (filter.DeviceType.HasValue) filtered = filtered.Where(d => (int)d.DeviceType == filter.DeviceType.Value);
            if (filter.Vendor.HasValue) filtered = filtered.Where(d => (int)d.Vendor == filter.Vendor.Value);
            if (!string.IsNullOrWhiteSpace(filter.Query))
                filtered = filtered.Where(d =>
                    d.Hostname.Contains(filter.Query!, StringComparison.OrdinalIgnoreCase)
                    || d.ManagementIp.Contains(filter.Query!, StringComparison.OrdinalIgnoreCase));
            if (filter.HideCpe) filtered = filtered.Where(d => d.DeviceType != DeviceType.Cpe);
        }

        var deviceList = filtered.ToList();
        var deviceIdSet = deviceList.Select(d => d.Id).ToHashSet();

        var edges = new List<TopologyEdgeDto>();
        foreach (var link in links)
        {
            if (!ifaceDevice.TryGetValue(link.AInterfaceId, out var aDev)) continue;
            if (!ifaceDevice.TryGetValue(link.BInterfaceId, out var bDev)) continue;
            if (!deviceIdSet.Contains(aDev) || !deviceIdSet.Contains(bDev)) continue;
            edges.Add(new TopologyEdgeDto(link.Id.Value, aDev.Value, bDev.Value,
                (int)link.LinkType, (int)link.OperStatus, link.IsConfirmed));
        }

        var linkedIds = new HashSet<Guid>();
        foreach (var e in edges) { linkedIds.Add(e.Source); linkedIds.Add(e.Target); }
        var unlinked = deviceList.Count(d => !linkedIds.Contains(d.Id.Value));

        var nodes = deviceList.Select(d => new TopologyNodeDto(d.Id.Value, d.Hostname, d.ManagementIp,
            (int)d.DeviceType, (int)d.Vendor, (int)d.Status, d.SiteId?.Value)).ToList();

        var groups = sites.Select(s => new TopologyGroupDto(s.Id.Value, s.Name)).ToList();

        return new TopologyGraphDto(nodes, edges, groups, unlinked);
    }
}