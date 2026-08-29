using AtlasNOC.Domain.Entities;

namespace AtlasNOC.Application.Repositories;

public interface IAlertRepository
{
    Task<Alert?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<IReadOnlyList<Alert>> ListOpenAsync(CancellationToken ct = default);
    Task<IReadOnlyList<Alert>> ListAsync(CancellationToken ct = default);
    Task AddAsync(Alert alert, CancellationToken ct = default);
    Task UpdateAsync(Alert alert, CancellationToken ct = default);
}