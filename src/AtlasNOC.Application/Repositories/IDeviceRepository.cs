using AtlasNOC.Domain.Entities;

namespace AtlasNOC.Application.Repositories;

public interface IDeviceRepository
{
    Task<Device?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<Device?> GetByManagementIpAsync(string ip, CancellationToken ct = default);
    Task<IReadOnlyList<Device>> ListAsync(CancellationToken ct = default);
    Task<IReadOnlyList<Device>> ListManagedAsync(CancellationToken ct = default);
    Task AddAsync(Device device, CancellationToken ct = default);
    Task UpdateAsync(Device device, CancellationToken ct = default);
}