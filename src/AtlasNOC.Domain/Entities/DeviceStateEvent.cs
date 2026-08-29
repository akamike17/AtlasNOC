using AtlasNOC.Domain.Enums;
using AtlasNOC.Domain.ValueObjects;

namespace AtlasNOC.Domain.Entities;

/// <summary>Evento de transición de estado de un dispositivo.</summary>
public class DeviceStateEvent
{
    public Guid Id { get; private set; }
    public DeviceId DeviceId { get; private set; } = null!;
    public DeviceStatus FromStatus { get; private set; }
    public DeviceStatus ToStatus { get; private set; }
    public DateTime OccurredAtUtc { get; private set; }
    public string? Reason { get; private set; }

    private DeviceStateEvent() { }

    public DeviceStateEvent(DeviceId deviceId, DeviceStatus fromStatus, DeviceStatus toStatus,
        string? reason = null)
    {
        Id = Guid.NewGuid();
        DeviceId = deviceId ?? throw new ArgumentNullException(nameof(deviceId));
        FromStatus = fromStatus;
        ToStatus = toStatus;
        Reason = reason;
        OccurredAtUtc = DateTime.UtcNow;
    }
}