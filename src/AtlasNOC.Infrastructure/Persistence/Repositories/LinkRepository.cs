using AtlasNOC.Application.Repositories;
using AtlasNOC.Domain.Entities;
using AtlasNOC.Domain.ValueObjects;
using AtlasNOC.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace AtlasNOC.Infrastructure.Persistence.Repositories;

public class LinkRepository : ILinkRepository
{
    private readonly AtlasNOCDbContext _context;
    public LinkRepository(AtlasNOCDbContext context) => _context = context;

    public async Task<NetworkLink?> GetByIdAsync(Guid id, CancellationToken ct = default)
        => await _context.NetworkLinks.FirstOrDefaultAsync(l => l.Id == LinkId.From(id), ct);

    public async Task<IReadOnlyList<NetworkLink>> ListAsync(CancellationToken ct = default)
        => await _context.NetworkLinks.AsNoTracking().ToListAsync(ct);

    public async Task<IReadOnlyList<NetworkLink>> ListByDeviceAsync(Guid deviceId, CancellationToken ct = default)
    {
        var interfaceIds = await _context.DeviceInterfaces
            .Where(i => i.DeviceId == DeviceId.From(deviceId))
            .Select(i => i.Id)
            .ToListAsync(ct);

        return await _context.NetworkLinks
            .Where(l => interfaceIds.Contains(l.AInterfaceId) || interfaceIds.Contains(l.BInterfaceId))
            .AsNoTracking()
            .ToListAsync(ct);
    }

    public Task AddAsync(NetworkLink link, CancellationToken ct = default)
    {
        _context.NetworkLinks.Add(link);
        return Task.CompletedTask;
    }

    public Task UpdateAsync(NetworkLink link, CancellationToken ct = default)
    {
        _context.NetworkLinks.Update(link);
        return Task.CompletedTask;
    }
}