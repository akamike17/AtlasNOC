using AtlasNOC.Domain.Entities;

namespace AtlasNOC.Application.Repositories;

public interface IDiscoveryRunRepository
{
    Task<DiscoveryRun?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task AddAsync(DiscoveryRun run, CancellationToken ct = default);
    Task UpdateAsync(DiscoveryRun run, CancellationToken ct = default);
}

public interface INeighborObservationRepository
{
    Task AddAsync(NeighborObservation observation, CancellationToken ct = default);
    Task<IReadOnlyList<NeighborObservation>> ListUnresolvedAsync(CancellationToken ct = default);
    Task UpdateAsync(NeighborObservation observation, CancellationToken ct = default);
}

public interface IAuditRepository
{
    Task AddAsync(AuditEvent auditEvent, CancellationToken ct = default);
    Task<IReadOnlyList<AuditEvent>> ListRecentAsync(int count, CancellationToken ct = default);
}