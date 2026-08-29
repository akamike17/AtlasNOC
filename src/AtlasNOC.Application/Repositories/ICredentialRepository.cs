using AtlasNOC.Domain.Entities;

namespace AtlasNOC.Application.Repositories;

public interface ICredentialRepository
{
    Task<DeviceCredential?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<IReadOnlyList<DeviceCredential>> ListAsync(CancellationToken ct = default);
    Task AddAsync(DeviceCredential credential, CancellationToken ct = default);
    Task UpdateAsync(DeviceCredential credential, CancellationToken ct = default);
}