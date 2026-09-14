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
        var interfaceIds = await _db.DeviceInterfaces.AsNoTracking().Where(x => x.DeviceId.Value == deviceId).Select(x => x.Id.Value).ToListAsync(ct);
        var linked = await _db.NetworkLinks.AsNoTracking().Where(x => interfaceIds.Contains(x.AInterfaceId.Value) || interfaceIds.Contains(x.BInterfaceId.Value)).CountAsync(ct);
        var complete = await _db.NetworkLinks.AsNoTracking().AnyAsync(x => x.IsConfirmed && !x.IsStale, ct);
        return new NetworkImpactResult(1 + linked, 0, 0, complete, complete ? $"Impacto estimado para equipo {deviceId}: {linked} enlaces." : "Topología incompleta: impacto parcial; faltan enlaces confirmados.");
    }
}
