using AtlasNOC.Domain.Entities;

namespace AtlasNOC.Application.Repositories;

public interface ILinkRepository
{
    Task<NetworkLink?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<IReadOnlyList<NetworkLink>> ListAsync(CancellationToken ct = default);
    Task<IReadOnlyList<NetworkLink>> ListByDeviceAsync(Guid deviceId, CancellationToken ct = default);
    Task AddAsync(NetworkLink link, CancellationToken ct = default);
    Task UpdateAsync(NetworkLink link, CancellationToken ct = default);
}