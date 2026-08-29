using AtlasNOC.Domain.Entities;

namespace AtlasNOC.Application.Repositories;

public interface IInterfaceRepository
{
    Task<DeviceInterface?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<IReadOnlyList<DeviceInterface>> ListByDeviceAsync(Guid deviceId, CancellationToken ct = default);
    Task AddAsync(DeviceInterface iface, CancellationToken ct = default);
    Task UpdateAsync(DeviceInterface iface, CancellationToken ct = default);
}