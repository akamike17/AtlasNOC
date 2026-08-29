using AtlasNOC.Domain.Entities;

namespace AtlasNOC.Application.Repositories;

public interface IIncidentRepository
{
    Task<Incident?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<IReadOnlyList<Incident>> ListActiveAsync(CancellationToken ct = default);
    Task AddAsync(Incident incident, CancellationToken ct = default);
    Task UpdateAsync(Incident incident, CancellationToken ct = default);
}