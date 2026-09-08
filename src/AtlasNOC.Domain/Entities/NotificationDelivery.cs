using AtlasNOC.Domain.Enums;
using AtlasNOC.Domain.ValueObjects;

namespace AtlasNOC.Domain.Entities;

/// <summary>Intento persistente de entregar una alerta a un canal concreto.</summary>
public class NotificationDelivery
{
    public Guid Id { get; private set; }
    public AlertId AlertId { get; private set; } = null!;
    public Guid ChannelId { get; private set; }
    public NotificationDeliveryState State { get; private set; }
    public int AttemptCount { get; private set; }
    public DateTime? LastAttemptUtc { get; private set; }
    public DateTime? SentAtUtc { get; private set; }
    public string? LastError { get; private set; }
    public DateTime? NextRetryUtc { get; private set; }

    private NotificationDelivery() { }

    public NotificationDelivery(AlertId alertId, Guid channelId)
    {
        AlertId = alertId ?? throw new ArgumentNullException(nameof(alertId));
        if (channelId == Guid.Empty) throw new ArgumentException("Canal requerido.", nameof(channelId));
        Id = Guid.NewGuid();
        ChannelId = channelId;
        State = NotificationDeliveryState.Pending;
    }

    public void BeginAttempt(DateTime nowUtc)
    {
        if (State is NotificationDeliveryState.Sent or NotificationDeliveryState.DeadLetter)
            throw new InvalidOperationException("La entrega ya es terminal.");
        State = NotificationDeliveryState.Sending;
        AttemptCount++;
        LastAttemptUtc = nowUtc;
        LastError = null;
        NextRetryUtc = null;
    }

    public void MarkSent(DateTime nowUtc)
    {
        if (State != NotificationDeliveryState.Sending)
            throw new InvalidOperationException("La entrega no está en envío.");
        State = NotificationDeliveryState.Sent;
        SentAtUtc = nowUtc;
        LastError = null;
        NextRetryUtc = null;
    }

    public void MarkFailed(string error, DateTime nowUtc, int maxAttempts)
    {
        LastError = string.IsNullOrWhiteSpace(error) ? "Unknown delivery error" : error.Trim();
        if (AttemptCount >= maxAttempts)
        {
            State = NotificationDeliveryState.DeadLetter;
            NextRetryUtc = null;
            return;
        }
        State = NotificationDeliveryState.Failed;
        var delayMinutes = Math.Min(60, Math.Pow(2, Math.Max(0, AttemptCount - 1)));
        NextRetryUtc = nowUtc.AddMinutes(delayMinutes);
    }

    public void MarkUnsupported(NotificationChannelType type)
    {
        State = NotificationDeliveryState.DeadLetter;
        LastError = $"Unsupported channel type: {type}";
        NextRetryUtc = null;
    }
}
