using AtlasNOC.Domain.Enums;
using AtlasNOC.Domain.ValueObjects;

namespace AtlasNOC.Domain.Entities;

/// <summary>Interfaz física/lógica de un dispositivo.</summary>
public class DeviceInterface
{
    public InterfaceId Id { get; private set; } = null!;
    public DeviceId DeviceId { get; private set; } = null!;
    public int IfIndex { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public string? Description { get; private set; }
    public string? MacAddress { get; private set; }
    public string? IpAddress { get; private set; }
    public InterfaceAdminStatus AdminStatus { get; private set; }
    public InterfaceOperStatus OperStatus { get; private set; }
    public ulong? SpeedBps { get; private set; }
    public string? InterfaceType { get; private set; }
    public DateTime? LastSeenAtUtc { get; private set; }

    private DeviceInterface() { }

    public DeviceInterface(DeviceId deviceId, int ifIndex, string name,
        string? description = null, string? macAddress = null, string? ipAddress = null,
        InterfaceAdminStatus adminStatus = InterfaceAdminStatus.Down,
        InterfaceOperStatus operStatus = InterfaceOperStatus.Unknown,
        ulong? speedBps = null, string? interfaceType = null)
    {
        Id = InterfaceId.New();
        DeviceId = deviceId ?? throw new ArgumentNullException(nameof(deviceId));
        IfIndex = ifIndex;
        Name = name ?? throw new ArgumentNullException(nameof(name));
        Description = description;
        MacAddress = macAddress;
        IpAddress = ipAddress;
        AdminStatus = adminStatus;
        OperStatus = operStatus;
        SpeedBps = speedBps;
        InterfaceType = interfaceType;
        LastSeenAtUtc = DateTime.UtcNow;
    }
}