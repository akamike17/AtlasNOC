using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using AtlasNOC.Domain.Entities;
using AtlasNOC.Domain.Enums;
using AtlasNOC.Domain.Services.Interfaces;
using AtlasNOC.Domain.ValueObjects;

namespace AtlasNOC.Domain.Services;

public class DeviceService : IDeviceService
{
    private readonly IRepository<Device> _repository;
    private readonly IAuditService _auditService;

    public DeviceService(IRepository<Device> repository, IAuditService auditService)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        _auditService = auditService ?? throw new ArgumentNullException(nameof(auditService));
    }

    public async Task<Device> CreateAsync(string name, string ipAddress, DeviceType type,
        string createdBy, string? location = null, string? description = null,
        CancellationToken cancellationToken = default)
    {
        if (name is null) throw new ArgumentNullException(nameof(name));
        if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("Name is required", nameof(name));
        if (ipAddress is null) throw new ArgumentNullException(nameof(ipAddress));
        if (string.IsNullOrWhiteSpace(ipAddress)) throw new ArgumentException("IP address is required", nameof(ipAddress));
        if (createdBy is null) throw new ArgumentNullException(nameof(createdBy));
        if (string.IsNullOrWhiteSpace(createdBy)) throw new ArgumentException("CreatedBy is required", nameof(createdBy));
        if (!System.Text.RegularExpressions.Regex.IsMatch(ipAddress,
                @"^((25[0-5]|2[0-4][0-9]|[01]?[0-9][0-9]?)\.){3}" +
                @"(25[0-5]|2[0-4][0-9]|[01]?[0-9][0-9]?)$"))
            throw new ArgumentException($"Invalid IP address format: {ipAddress}", nameof(ipAddress));

        var device = Device.Create(name, ipAddress, type, createdBy, location, description);
        await _repository.AddAsync(device, cancellationToken);
        await _auditService.LogSuccessAsync("Device", "Create", createdBy,
            targetResource: device.Id.Value.ToString(), targetResourceType: nameof(Device),
            newValue: $"Name={name}, IP={ipAddress}, Type={type}", cancellationToken: cancellationToken);
        return device;
    }

    public Task<IReadOnlyList<Device>> GetAllAsync(CancellationToken cancellationToken = default)
        => _repository.GetAllAsync(cancellationToken);

    public async Task<IReadOnlyList<Device>> GetActiveAsync(CancellationToken cancellationToken = default)
    {
        var all = await _repository.GetAllAsync(cancellationToken);
        return all.Where(d => d.IsActive).ToList().AsReadOnly();
    }

    public Task<Device?> GetByIdAsync(DeviceId id, CancellationToken cancellationToken = default)
        => _repository.GetByIdAsync(id.Value, cancellationToken);

    public async Task<IReadOnlyList<Device>> GetByStatusAsync(DeviceStatus status,
        CancellationToken cancellationToken = default)
    {
        var all = await _repository.GetAllAsync(cancellationToken);
        return all.Where(d => d.Status == status).ToList().AsReadOnly();
    }

    public async Task<IReadOnlyList<Device>> SearchAsync(string searchTerm,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(searchTerm))
            return await GetAllAsync(cancellationToken);
        var all = await _repository.GetAllAsync(cancellationToken);
        var term = searchTerm.ToLowerInvariant();
        return all.Where(d =>
            d.Name.ToLowerInvariant().Contains(term) ||
            d.IpAddress.Contains(term) ||
            (d.Description?.ToLowerInvariant().Contains(term) ?? false) ||
            (d.Location?.ToLowerInvariant().Contains(term) ?? false))
            .ToList().AsReadOnly();
    }

    public async Task<Device> UpdateStatusAsync(DeviceId deviceId, DeviceStatus newStatus,
        string modifiedBy, CancellationToken cancellationToken = default)
    {
        var device = await _repository.GetByIdAsync(deviceId.Value, cancellationToken)
            ?? throw new KeyNotFoundException($"Device {deviceId} not found");
        var oldStatus = device.Status;
        device.UpdateStatus(newStatus, modifiedBy);
        device.SetLastChecked();
        await _repository.UpdateAsync(device, cancellationToken);
        await _auditService.LogSuccessAsync("Device", "StatusChange", modifiedBy,
            targetResource: device.Id.Value.ToString(), targetResourceType: nameof(Device),
            oldValue: $"Status={oldStatus}", newValue: $"Status={newStatus}",
            cancellationToken: cancellationToken);
        return device;
    }

    public async Task<Device> UpdateDetailsAsync(DeviceId deviceId, string name,
        string? location, string? description, string modifiedBy,
        CancellationToken cancellationToken = default)
    {
        var device = await _repository.GetByIdAsync(deviceId.Value, cancellationToken)
            ?? throw new KeyNotFoundException($"Device {deviceId} not found");
        device.UpdateDetails(name, location, description, modifiedBy);
        await _repository.UpdateAsync(device, cancellationToken);
        await _auditService.LogSuccessAsync("Device", "Update", modifiedBy,
            targetResource: device.Id.Value.ToString(), targetResourceType: nameof(Device),
            newValue: $"Name={name}, Location={location}, Description={description}",
            cancellationToken: cancellationToken);
        return device;
    }

    public async Task DeactivateAsync(DeviceId deviceId, string modifiedBy,
        CancellationToken cancellationToken = default)
    {
        var device = await _repository.GetByIdAsync(deviceId.Value, cancellationToken)
            ?? throw new KeyNotFoundException($"Device {deviceId} not found");
        device.Deactivate(modifiedBy);
        await _repository.UpdateAsync(device, cancellationToken);
        await _auditService.LogSuccessAsync("Device", "Deactivate", modifiedBy,
            targetResource: device.Id.Value.ToString(), targetResourceType: nameof(Device),
            cancellationToken: cancellationToken);
    }

    public async Task ReactivateAsync(DeviceId deviceId, string modifiedBy,
        CancellationToken cancellationToken = default)
    {
        var device = await _repository.GetByIdAsync(deviceId.Value, cancellationToken)
            ?? throw new KeyNotFoundException($"Device {deviceId} not found");
        device.Reactivate(modifiedBy);
        await _repository.UpdateAsync(device, cancellationToken);
        await _auditService.LogSuccessAsync("Device", "Reactivate", modifiedBy,
            targetResource: device.Id.Value.ToString(), targetResourceType: nameof(Device),
            cancellationToken: cancellationToken);
    }

    public async Task<IReadOnlyList<Device>> GetDownDevicesAsync(CancellationToken cancellationToken = default)
    {
        var all = await _repository.GetAllAsync(cancellationToken);
        return all.Where(d => d.Status == DeviceStatus.Down && d.IsActive).ToList().AsReadOnly();
    }
}
