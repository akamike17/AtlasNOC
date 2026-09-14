using AtlasNOC.Application.Services;
using AtlasNOC.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace AtlasNOC.Infrastructure.Services;

public sealed class NetworkImpactService : INetworkImpactService
{
    private readonly AtlasNOCDbContext _db;
    public NetworkImpactService(AtlasNOCDbContext db) => _db = db;
    public async Task<NetworkImpactResult> PreviewAsync(Guid deviceId, CancellationToken ct = default)
    {
        var devices = await _db.Devices.AsNoTracking().ToListAsync(ct);
        if (!devices.Any(x => x.Id.Value == deviceId))
            return new NetworkImpactResult(0, 0, 0, false, "Impacto parcial: la topología de este camino está incompleta.");
        var interfaces = await _db.DeviceInterfaces.AsNoTracking().ToListAsync(ct);
        var links = await _db.NetworkLinks.AsNoTracking().Where(x => x.IsConfirmed && !x.IsStale).ToListAsync(ct);
        var graph = IncidentCorrelationEngine.BuildDirectedGraph(devices, interfaces, links);
        var downstream = IncidentCorrelationEngine.FindDownstream(graph, deviceId);
        var affected = downstream.Append(deviceId).ToHashSet();
        var targetInterfaceIds = interfaces.Where(i => i.DeviceId.Value == deviceId).Select(i => i.Id.Value).ToHashSet();
        var allAdjacentLinks = await _db.NetworkLinks.AsNoTracking().Where(x => targetInterfaceIds.Contains(x.AInterfaceId.Value) || targetInterfaceIds.Contains(x.BInterfaceId.Value)).ToListAsync(ct);
        var relevantLinks = links.Where(link => interfaces.Any(i => i.Id.Value == link.AInterfaceId.Value && affected.Contains(i.DeviceId.Value))
            || interfaces.Any(i => i.Id.Value == link.BInterfaceId.Value && affected.Contains(i.DeviceId.Value))).Count();
        var siteCount = devices.Where(x => affected.Contains(x.Id.Value) && x.SiteId != null).Select(x => x.SiteId!.Value).Distinct().Count();
        var endpointDeviceIds = await _db.ServiceEndpoints.AsNoTracking().Where(x => affected.Contains(x.DeviceId.Value)).Select(x => x.DeviceId.Value).ToListAsync(ct);
        var complete = relevantLinks > 0 && allAdjacentLinks.All(x => x.IsConfirmed && !x.IsStale);
        var summary = complete
            ? $"Impacto downstream: {downstream.Count} dispositivos, {endpointDeviceIds.Count} servicios, {siteCount} sitios, {relevantLinks} enlaces confirmados."
            : "Impacto parcial: la topología de este camino está incompleta.";
        return new NetworkImpactResult(affected.Count, endpointDeviceIds.Count, siteCount, complete, summary);
    }
}
