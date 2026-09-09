using AtlasNOC.Domain.Entities;

namespace AtlasNOC.Application.Repositories;

public interface IDiscoveryRunRepository
{
    Task<DiscoveryRun?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task AddAsync(DiscoveryRun run, CancellationToken ct = default);
    Task UpdateAsync(DiscoveryRun run, CancellationToken ct = default);
    Task<DiscoveryRun?> ClaimNextAsync(string workerId, DateTime nowUtc, TimeSpan leaseDuration, CancellationToken ct = default);
    Task<bool> RenewLeaseAsync(Guid runId, string workerId, DateTime nowUtc, TimeSpan leaseDuration, CancellationToken ct = default);
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

public interface ISubscriberRepository
{
    Task<Subscriber?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<IReadOnlyList<Subscriber>> ListAsync(CancellationToken ct = default);
    Task AddAsync(Subscriber subscriber, CancellationToken ct = default);
    Task UpdateAsync(Subscriber subscriber, CancellationToken ct = default);
}

public interface IServiceEndpointRepository
{
    Task<ServiceEndpoint?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<IReadOnlyList<ServiceEndpoint>> ListAsync(CancellationToken ct = default);
    Task<IReadOnlyList<ServiceEndpoint>> ListBySubscriberAsync(Guid subscriberId, CancellationToken ct = default);
    Task AddAsync(ServiceEndpoint endpoint, CancellationToken ct = default);
    Task UpdateAsync(ServiceEndpoint endpoint, CancellationToken ct = default);
}
