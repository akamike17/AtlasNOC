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

    public async Task<DiscoveryRun?> ClaimNextAsync(string workerId, DateTime nowUtc,
        TimeSpan leaseDuration, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(workerId)) throw new ArgumentException("Worker requerido.", nameof(workerId));
        var leaseExpiresAtUtc = nowUtc.Add(leaseDuration);

        // MySQL ejecuta UPDATE ... ORDER BY ... LIMIT 1 como una sola sentencia:
        // dos instancias nunca pueden reclamar la misma fila.
        var affected = await _context.Database.ExecuteSqlInterpolatedAsync($@"
UPDATE DiscoveryRuns
SET Status = {(int)AtlasNOC.Domain.Enums.DiscoveryRunStatus.Running},
    ClaimedBy = {workerId},
    ClaimedAtUtc = {nowUtc},
    LeaseExpiresAtUtc = {leaseExpiresAtUtc},
    AttemptCount = AttemptCount + 1
WHERE Status = {(int)AtlasNOC.Domain.Enums.DiscoveryRunStatus.Pending}
   OR (Status = {(int)AtlasNOC.Domain.Enums.DiscoveryRunStatus.Running}
       AND LeaseExpiresAtUtc IS NOT NULL AND LeaseExpiresAtUtc <= {nowUtc})
ORDER BY StartedAtUtc
LIMIT 1", ct);

        if (affected == 0) return null;
        return await _context.DiscoveryRuns
            .AsNoTracking()
            .Where(r => r.Status == AtlasNOC.Domain.Enums.DiscoveryRunStatus.Running && r.ClaimedBy == workerId)
            .OrderByDescending(r => r.ClaimedAtUtc)
            .FirstOrDefaultAsync(ct);
    }

    public async Task<bool> RenewLeaseAsync(Guid runId, string workerId, DateTime nowUtc,
        TimeSpan leaseDuration, CancellationToken ct = default)
        => await _context.DiscoveryRuns
            .Where(r => r.Id == runId
                && r.Status == AtlasNOC.Domain.Enums.DiscoveryRunStatus.Running
                && r.ClaimedBy == workerId)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(r => r.LeaseExpiresAtUtc, nowUtc.Add(leaseDuration)), ct) == 1;
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
            .Where(o => o.Status == AtlasNOC.Domain.Enums.NeighborObservationStatus.Pending
                || o.Status == AtlasNOC.Domain.Enums.NeighborObservationStatus.Ambiguous)
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

public class SubscriberRepository : ISubscriberRepository
{
    private readonly AtlasNOCDbContext _context;
    public SubscriberRepository(AtlasNOCDbContext context) => _context = context;

    public async Task<Subscriber?> GetByIdAsync(Guid id, CancellationToken ct = default)
        => await _context.Subscribers.FirstOrDefaultAsync(s => s.Id == id, ct);

    public async Task<IReadOnlyList<Subscriber>> ListAsync(CancellationToken ct = default)
        => await _context.Subscribers.AsNoTracking().ToListAsync(ct);

    public Task AddAsync(Subscriber subscriber, CancellationToken ct = default)
    {
        _context.Subscribers.Add(subscriber);
        return Task.CompletedTask;
    }

    public Task UpdateAsync(Subscriber subscriber, CancellationToken ct = default)
    {
        _context.Subscribers.Update(subscriber);
        return Task.CompletedTask;
    }
}

public class ServiceEndpointRepository : IServiceEndpointRepository
{
    private readonly AtlasNOCDbContext _context;
    public ServiceEndpointRepository(AtlasNOCDbContext context) => _context = context;

    public async Task<ServiceEndpoint?> GetByIdAsync(Guid id, CancellationToken ct = default)
        => await _context.ServiceEndpoints.FirstOrDefaultAsync(e => e.Id == id, ct);

    public async Task<IReadOnlyList<ServiceEndpoint>> ListAsync(CancellationToken ct = default)
        => await _context.ServiceEndpoints.AsNoTracking().ToListAsync(ct);

    public async Task<IReadOnlyList<ServiceEndpoint>> ListBySubscriberAsync(Guid subscriberId, CancellationToken ct = default)
        => await _context.ServiceEndpoints
            .Where(e => e.SubscriberId == subscriberId)
            .AsNoTracking()
            .ToListAsync(ct);

    public Task AddAsync(ServiceEndpoint endpoint, CancellationToken ct = default)
    {
        _context.ServiceEndpoints.Add(endpoint);
        return Task.CompletedTask;
    }

    public Task UpdateAsync(ServiceEndpoint endpoint, CancellationToken ct = default)
    {
        _context.ServiceEndpoints.Update(endpoint);
        return Task.CompletedTask;
    }
}
