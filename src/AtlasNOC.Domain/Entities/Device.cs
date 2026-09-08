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
    public DateTime? LastHealthPolledAtUtc { get; private set; }
    public DateTime? LastInterfacePolledAtUtc { get; private set; }
    public DateTime? LastWirelessPolledAtUtc { get; private set; }
    public Guid? PollingProfileId { get; private set; }
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
        => LastPolledAtUtc = atUtc ?? DateTime.UtcNow;

    public void MarkHealthPolled(DateTime? atUtc = null) => LastHealthPolledAtUtc = atUtc ?? DateTime.UtcNow;
    public void MarkInterfacesPolled(DateTime? atUtc = null) => LastInterfacePolledAtUtc = atUtc ?? DateTime.UtcNow;
    public void MarkWirelessPolled(DateTime? atUtc = null) => LastWirelessPolledAtUtc = atUtc ?? DateTime.UtcNow;
    public void SetPollingProfile(Guid? pollingProfileId) => PollingProfileId = pollingProfileId;

    public void SetStatus(DeviceStatus status) => Status = status;

    public void SetSite(SiteId? siteId) => SiteId = siteId;

    public void UpdateDiscoveredIdentity(string? hostname, DeviceType deviceType, Vendor vendor,
        string? model, string? serialNumber, string? firmwareVersion, string? driverKey)
    {
        if (!string.IsNullOrWhiteSpace(hostname) && !hostname.Equals(ManagementIp, StringComparison.OrdinalIgnoreCase))
            Hostname = hostname.Trim();
        if (deviceType != DeviceType.Unknown) DeviceType = deviceType;
        if (vendor is not Vendor.Unknown and not Vendor.Generic) Vendor = vendor;
        if (!string.IsNullOrWhiteSpace(model)) Model = model.Trim();
        if (!string.IsNullOrWhiteSpace(serialNumber)) SerialNumber = serialNumber.Trim();
        if (!string.IsNullOrWhiteSpace(firmwareVersion)) FirmwareVersion = firmwareVersion.Trim();
        if (!string.IsNullOrWhiteSpace(driverKey)) DriverKey = driverKey.Trim();
    }
}
