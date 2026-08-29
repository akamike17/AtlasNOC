using AtlasNOC.Application.Repositories;
using AtlasNOC.Domain.Entities;
using AtlasNOC.Domain.ValueObjects;
using AtlasNOC.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace AtlasNOC.Infrastructure.Persistence.Repositories;

public class SiteRepository : ISiteRepository
{
    private readonly AtlasNOCDbContext _context;
    public SiteRepository(AtlasNOCDbContext context) => _context = context;

    public async Task<NetworkSite?> GetByIdAsync(Guid id, CancellationToken ct = default)
        => await _context.Sites.FirstOrDefaultAsync(s => s.Id == SiteId.From(id), ct);

    public async Task<IReadOnlyList<NetworkSite>> ListAsync(CancellationToken ct = default)
        => await _context.Sites.AsNoTracking().ToListAsync(ct);

    public Task AddAsync(NetworkSite site, CancellationToken ct = default)
    {
        _context.Sites.Add(site);
        return Task.CompletedTask;
    }

    public Task UpdateAsync(NetworkSite site, CancellationToken ct = default)
    {
        _context.Sites.Update(site);
        return Task.CompletedTask;
    }
}