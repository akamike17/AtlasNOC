using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using AtlasNOC.Domain.Entities;
using AtlasNOC.Domain.Enums;
using AtlasNOC.Domain.ValueObjects;

namespace AtlasNOC.Domain.Services.Interfaces;

public interface IDeviceService
{
    Task<Device> CreateAsync(string name, string ipAddress, DeviceType type, string createdBy,
        string? location = null, string? description = null,
        CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Device>> GetAllAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Device>> GetActiveAsync(CancellationToken cancellationToken = default);
    Task<Device?> GetByIdAsync(DeviceId id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Device>> GetByStatusAsync(DeviceStatus status,
        CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Device>> SearchAsync(string searchTerm,
        CancellationToken cancellationToken = default);
    Task<Device> UpdateStatusAsync(DeviceId deviceId, DeviceStatus newStatus, string modifiedBy,
        CancellationToken cancellationToken = default);
    Task<Device> UpdateDetailsAsync(DeviceId deviceId, string name, string? location,
        string? description, string modifiedBy, CancellationToken cancellationToken = default);
    Task DeactivateAsync(DeviceId deviceId, string modifiedBy,
        CancellationToken cancellationToken = default);
    Task ReactivateAsync(DeviceId deviceId, string modifiedBy,
        CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Device>> GetDownDevicesAsync(CancellationToken cancellationToken = default);
}
