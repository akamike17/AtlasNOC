using AtlasNOC.Domain.ValueObjects;

namespace AtlasNOC.Domain.Entities;

/// <summary>Asociación inalámbrica confirmada entre un AP/sector y un CPE.</summary>
public class WirelessAssociation
{
    public Guid Id { get; private set; }
    public DeviceId ApDeviceId { get; private set; } = null!;
    public DeviceId CpeDeviceId { get; private set; } = null!;
    public string? SectorName { get; private set; }
    public DateTime FirstSeenAtUtc { get; private set; }
    public DateTime LastSeenAtUtc { get; private set; }
    public bool IsActive { get; private set; } = true;

    private WirelessAssociation() { }

    public WirelessAssociation(DeviceId apDeviceId, DeviceId cpeDeviceId, string? sectorName = null)
    {
        Id = Guid.NewGuid();
        ApDeviceId = apDeviceId ?? throw new ArgumentNullException(nameof(apDeviceId));
        CpeDeviceId = cpeDeviceId ?? throw new ArgumentNullException(nameof(cpeDeviceId));
        SectorName = sectorName;
        FirstSeenAtUtc = DateTime.UtcNow;
        LastSeenAtUtc = FirstSeenAtUtc;
    }

    public void Touch() => LastSeenAtUtc = DateTime.UtcNow;
}