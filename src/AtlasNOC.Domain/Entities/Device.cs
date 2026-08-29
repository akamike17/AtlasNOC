using AtlasNOC.Domain.Enums;
using AtlasNOC.Domain.ValueObjects;

namespace AtlasNOC.Domain.Entities;

/// <summary>Dispositivo de red gestionado. Su estado se descubre, no se inventa.</summary>
public class Device
{
    public DeviceId Id { get; private set; } = null!;
    public SiteId? SiteId { get; private set; }
    public string Hostname { get; private set; } = string.Empty;
    public string ManagementIp { get; private set; } = string.Empty;
    public DeviceType DeviceType { get; private set; }
    public Vendor Vendor { get; private set; }
    public string? Model { get; private set; }
    public string? SerialNumber { get; private set; }
    public string? FirmwareVersion { get; private set; }
    public DeviceStatus Status { get; private set; }
    public DateTime? LastSeenAtUtc { get; private set; }
    public DateTime? LastPolledAtUtc { get; private set; }
    public string? DriverKey { get; private set; }
    public bool IsManaged { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }

    private Device() { }

    public Device(string hostname, string managementIp, DeviceType deviceType, Vendor vendor,
        SiteId? siteId = null, string? model = null, string? serialNumber = null,
        string? firmwareVersion = null, string? driverKey = null, bool isManaged = true)
    {
        Id = DeviceId.New();
        Hostname = hostname ?? throw new ArgumentNullException(nameof(hostname));
        ManagementIp = managementIp ?? throw new ArgumentNullException(nameof(managementIp));
        DeviceType = deviceType;
        Vendor = vendor;
        SiteId = siteId;
        Model = model;
        SerialNumber = serialNumber;
        FirmwareVersion = firmwareVersion;
        DriverKey = driverKey;
        IsManaged = isManaged;
        Status = DeviceStatus.Unknown;
        CreatedAtUtc = DateTime.UtcNow;
    }

    public void MarkSeen(DateTime? atUtc = null)
        => LastSeenAtUtc = atUtc ?? DateTime.UtcNow;

    public void MarkPolled(DateTime? atUtc = null)
    {
        LastPolledAtUtc = atUtc ?? DateTime.UtcNow;
        MarkSeen(LastPolledAtUtc);
    }

    public void SetStatus(DeviceStatus status) => Status = status;

    public void SetSite(SiteId? siteId) => SiteId = siteId;
}