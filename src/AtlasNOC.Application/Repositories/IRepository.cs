using AtlasNOC.Domain.Entities;

namespace AtlasNOC.Application.Repositories;

/// <summary>Generic repository contract. Domain entities are persisted via Infrastructure.</summary>
public interface IRepository<T> where T : class
{
    Task<T?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<IReadOnlyList<T>> ListAsync(CancellationToken ct = default);
    Task AddAsync(T entity, CancellationToken ct = default);
    Task UpdateAsync(T entity, CancellationToken ct = default);
    Task DeleteAsync(T entity, CancellationToken ct = default);
    Task<int> CountAsync(CancellationToken ct = default);
}