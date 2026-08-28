using System;
using AtlasNOC.Domain.Enums;
using AtlasNOC.Domain.ValueObjects;

namespace AtlasNOC.Domain.Entities;

public class Alert
{
    public AlertId Id { get; private set; } = null!;
    public DeviceId DeviceId { get; private set; } = null!;
    public string Message { get; private set; } = string.Empty;
    public AlertSeverity Severity { get; private set; }
    public DateTime OccurredAt { get; private set; }
    public DateTime? AcknowledgedAt { get; private set; }
    public DateTime? ResolvedAt { get; private set; }
    public string? AcknowledgedBy { get; private set; }
    public string? ResolvedBy { get; private set; }
    public string? ResolutionNotes { get; private set; }

    public bool IsActive => ResolvedAt == null;

    private Alert() { }

    public Alert(AlertId id, DeviceId deviceId, string message,
        AlertSeverity severity, DateTime occurredAt)
    {
        Id = id ?? throw new ArgumentNullException(nameof(id));
        DeviceId = deviceId ?? throw new ArgumentNullException(nameof(deviceId));
        Message = message ?? throw new ArgumentNullException(nameof(message));
        Severity = severity;
        OccurredAt = occurredAt;
    }

    public static Alert Create(DeviceId deviceId, string message,
        AlertSeverity severity)
    {
        return new Alert(
            AlertId.New(),
            deviceId,
            message,
            severity,
            DateTime.UtcNow);
    }

    public void Acknowledge(string acknowledgedBy)
    {
        if (string.IsNullOrWhiteSpace(acknowledgedBy))
            throw new ArgumentException("AcknowledgedBy is required", nameof(acknowledgedBy));
        if (AcknowledgedAt != null)
            throw new InvalidOperationException("Alert has already been acknowledged");
        AcknowledgedBy = acknowledgedBy;
        AcknowledgedAt = DateTime.UtcNow;
    }

    public void Resolve(string resolvedBy, string? notes = null)
    {
        if (string.IsNullOrWhiteSpace(resolvedBy))
            throw new ArgumentException("ResolvedBy is required", nameof(resolvedBy));
        if (ResolvedAt != null)
            throw new InvalidOperationException("Alert has already been resolved");
        ResolvedBy = resolvedBy;
        ResolutionNotes = notes;
        ResolvedAt = DateTime.UtcNow;
    }
}
