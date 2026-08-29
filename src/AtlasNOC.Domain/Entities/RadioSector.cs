using AtlasNOC.Domain.ValueObjects;

namespace AtlasNOC.Domain.Entities;

/// <summary>Sector de radio (AP/antena sectorial).</summary>
public class RadioSector
{
    public Guid Id { get; private set; }
    public DeviceId DeviceId { get; private set; } = null!;
    public string Name { get; private set; } = string.Empty;
    public string? Ssid { get; private set; }
    public int? FrequencyMhz { get; private set; }
    public int? ChannelWidthMhz { get; private set; }
    public double? AzimuthDegrees { get; private set; }
    public bool IsActive { get; private set; } = true;

    private RadioSector() { }

    public RadioSector(DeviceId deviceId, string name, string? ssid = null,
        int? frequencyMhz = null, int? channelWidthMhz = null, double? azimuthDegrees = null)
    {
        Id = Guid.NewGuid();
        DeviceId = deviceId ?? throw new ArgumentNullException(nameof(deviceId));
        Name = name ?? throw new ArgumentNullException(nameof(name));
        Ssid = ssid;
        FrequencyMhz = frequencyMhz;
        ChannelWidthMhz = channelWidthMhz;
        AzimuthDegrees = azimuthDegrees;
    }
}