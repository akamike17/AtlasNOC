using AtlasNOC.Domain.Entities;

namespace AtlasNOC.Application.Repositories;

public interface ISiteRepository
{
    Task<NetworkSite?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<IReadOnlyList<NetworkSite>> ListAsync(CancellationToken ct = default);
    Task AddAsync(NetworkSite site, CancellationToken ct = default);
    Task UpdateAsync(NetworkSite site, CancellationToken ct = default);
}