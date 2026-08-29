using AtlasNOC.Application.Repositories;
using AtlasNOC.Domain.Entities;
using AtlasNOC.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace AtlasNOC.Infrastructure.Persistence.Repositories;

public class DiscoveryRunRepository : IDiscoveryRunRepository
{
    private readonly AtlasNOCDbContext _context;
    public DiscoveryRunRepository(AtlasNOCDbContext context) => _context = context;

    public async Task<DiscoveryRun?> GetByIdAsync(Guid id, CancellationToken ct = default)
        => await _context.DiscoveryRuns.FirstOrDefaultAsync(r => r.Id == id, ct);

    public Task AddAsync(DiscoveryRun run, CancellationToken ct = default)
    {
        _context.DiscoveryRuns.Add(run);
        return Task.CompletedTask;
    }

    public Task UpdateAsync(DiscoveryRun run, CancellationToken ct = default)
    {
        _context.DiscoveryRuns.Update(run);
        return Task.CompletedTask;
    }
}

public class NeighborObservationRepository : INeighborObservationRepository
{
    private readonly AtlasNOCDbContext _context;
    public NeighborObservationRepository(AtlasNOCDbContext context) => _context = context;

    public Task AddAsync(NeighborObservation observation, CancellationToken ct = default)
    {
        _context.NeighborObservations.Add(observation);
        return Task.CompletedTask;
    }

    public async Task<IReadOnlyList<NeighborObservation>> ListUnresolvedAsync(CancellationToken ct = default)
        => await _context.NeighborObservations
            .Where(o => !o.IsResolved)
            .AsNoTracking()
            .ToListAsync(ct);

    public Task UpdateAsync(NeighborObservation observation, CancellationToken ct = default)
    {
        _context.NeighborObservations.Update(observation);
        return Task.CompletedTask;
    }
}

public class AuditRepository : IAuditRepository
{
    private readonly AtlasNOCDbContext _context;
    public AuditRepository(AtlasNOCDbContext context) => _context = context;

    public Task AddAsync(AuditEvent auditEvent, CancellationToken ct = default)
    {
        _context.AuditEvents.Add(auditEvent);
        return Task.CompletedTask;
    }

    public async Task<IReadOnlyList<AuditEvent>> ListRecentAsync(int count, CancellationToken ct = default)
        => await _context.AuditEvents
            .OrderByDescending(a => a.TimestampUtc)
            .Take(count)
            .AsNoTracking()
            .ToListAsync(ct);
}