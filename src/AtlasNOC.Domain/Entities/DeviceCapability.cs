using AtlasNOC.Domain.ValueObjects;

namespace AtlasNOC.Domain.Entities;

/// <summary>Capacidad detectada de un dispositivo (qué protocolos/adquisiciones soporta).</summary>
public class DeviceCapability
{
    public Guid Id { get; private set; }
    public DeviceId DeviceId { get; private set; } = null!;
    public string CapabilityKey { get; private set; } = string.Empty;
    public string? Value { get; private set; }
    public DateTime DetectedAtUtc { get; private set; }

    private DeviceCapability() { }

    public DeviceCapability(DeviceId deviceId, string capabilityKey, string? value = null)
    {
        Id = Guid.NewGuid();
        DeviceId = deviceId ?? throw new ArgumentNullException(nameof(deviceId));
        CapabilityKey = capabilityKey ?? throw new ArgumentNullException(nameof(capabilityKey));
        Value = value;
        DetectedAtUtc = DateTime.UtcNow;
    }
}