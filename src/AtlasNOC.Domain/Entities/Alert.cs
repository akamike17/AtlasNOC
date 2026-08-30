using AtlasNOC.Domain.Enums;
using AtlasNOC.Domain.ValueObjects;

namespace AtlasNOC.Domain.Entities;

/// <summary>Alerta activa basada en una regla + recurso + métrica.</summary>
public class Alert
{
    public AlertId Id { get; private set; } = null!;
    public Guid? RuleId { get; private set; }
    public string ResourceType { get; private set; } = string.Empty;
    public string ResourceId { get; private set; } = string.Empty;
    public string MetricName { get; private set; } = string.Empty;
    public double Value { get; private set; }
    public double Threshold { get; private set; }
    public AlertSeverity Severity { get; private set; }
    public AlertState State { get; private set; }
    public DateTime FirstSeenUtc { get; private set; }
    public DateTime LastSeenUtc { get; private set; }
    public string? Evidence { get; private set; }
    public string? AcknowledgedBy { get; private set; }
    public DateTime? AcknowledgedAtUtc { get; private set; }
    public string? ResolvedBy { get; private set; }
    public DateTime? ResolvedAtUtc { get; private set; }
    public DateTime? NotificationSentAtUtc { get; private set; }

    private Alert() { }

    public Alert(Guid? ruleId, string resourceType, string resourceId, string metricName,
        double value, double threshold, AlertSeverity severity, string? evidence = null)
    {
        Id = AlertId.New();
        RuleId = ruleId;
        ResourceType = resourceType ?? throw new ArgumentNullException(nameof(resourceType));
        ResourceId = resourceId ?? throw new ArgumentNullException(nameof(resourceId));
        MetricName = metricName ?? throw new ArgumentNullException(nameof(metricName));
        Value = value;
        Threshold = threshold;
        Severity = severity;
        State = AlertState.Open;
        FirstSeenUtc = DateTime.UtcNow;
        LastSeenUtc = FirstSeenUtc;
        Evidence = evidence;
    }

    public void Touch(double? value = null)
    {
        LastSeenUtc = DateTime.UtcNow;
        if (value.HasValue) Value = value.Value;
    }

    public void Acknowledge(string by)
    {
        AcknowledgedBy = by;
        AcknowledgedAtUtc = DateTime.UtcNow;
        State = AlertState.Acknowledged;
    }

    public void Resolve(string by)
    {
        ResolvedBy = by;
        ResolvedAtUtc = DateTime.UtcNow;
        State = AlertState.Resolved;
    }

    public void MarkNotified() => NotificationSentAtUtc = DateTime.UtcNow;
}