using System;
using System.Collections.Generic;
using AtlasNOC.Domain.Enums;
using AtlasNOC.Domain.Services;
using AtlasNOC.Domain.ValueObjects;

namespace AtlasNOC.Domain.Entities;

public class Device
{
    public DeviceId Id { get; internal set; } = null!;
    public string Name { get; internal set; } = string.Empty;
    public string IpAddress { get; internal set; } = string.Empty;
    public DeviceType Type { get; internal set; }
    public DeviceStatus Status { get; internal set; }
    public string? Location { get; internal set; }
    public string? Description { get; internal set; }
    public DateTime CreatedAt { get; internal set; }
    public DateTime UpdatedAt { get; internal set; }
    public DateTime? LastCheckedAt { get; internal set; }
    public bool IsActive { get; internal set; }
    public string CreatedBy { get; internal set; } = string.Empty;
    public string? ModifiedBy { get; internal set; }

    private readonly List<Alert> _alerts = new();
    public IReadOnlyCollection<Alert> Alerts => _alerts.AsReadOnly();

    // NHibernate-compatible parameterless constructor
    private Device() { }

    public Device(DeviceId id, string name, string ipAddress, DeviceType type, string createdBy,
        string? location = null, string? description = null)
    {
        Id = id ?? throw new ArgumentNullException(nameof(id));
        Name = name ?? throw new ArgumentNullException(nameof(name));
        IpAddress = ipAddress ?? throw new ArgumentNullException(nameof(ipAddress));
        Type = type;
        Status = DeviceStatus.Unknown;
        Location = location;
        Description = description;
        CreatedAt = DateTime.UtcNow;
        UpdatedAt = DateTime.UtcNow;
        IsActive = true;
        CreatedBy = createdBy ?? throw new ArgumentNullException(nameof(createdBy));
    }

    public static Device Create(string name, string ipAddress, DeviceType type, string createdBy,
        string? location = null, string? description = null)
    {
        return new Device(DeviceId.New(), name, ipAddress, type, createdBy, location, description);
    }

    public static Device CreateForSnmpTest(string ipAddress)
    {
        return new Device(DeviceId.New(), "snmp-test", ipAddress, DeviceType.Unknown, "DiscoveryService");
    }

    // ─── Mutable operations use EntityExtensions (init properties via reflection) ───

    public void UpdateStatus(DeviceStatus newStatus, string modifiedBy)
    {
        EntityExtensions.RecordStatusChange(this, newStatus, modifiedBy);
    }

    public void SetLastChecked()
    {
        EntityExtensions.RecordLastChecked(this);
    }

    public void Deactivate(string modifiedBy)
    {
        EntityExtensions.Deactivate(this, modifiedBy);
    }

    public void Reactivate(string modifiedBy)
    {
        EntityExtensions.Reactivate(this, modifiedBy);
    }

    public void UpdateDetails(string name, string? location, string? description, string modifiedBy)
    {
        EntityExtensions.UpdateDetails(this, name, location, description, modifiedBy);
    }

    public void AddAlert(Alert alert)
    {
        if (alert == null) throw new ArgumentNullException(nameof(alert));
        _alerts.Add(alert);
    }
}
