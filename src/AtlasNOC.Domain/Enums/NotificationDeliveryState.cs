namespace AtlasNOC.Domain.Enums;

public enum NotificationDeliveryState
{
    Pending = 0,
    Sending = 1,
    Sent = 2,
    Failed = 3,
    DeadLetter = 4
}
