using AtlasNOC.Domain.Enums;

namespace AtlasNOC.Domain.Entities;

/// <summary>Regla de alerta declarativa (umbral + severidad).</summary>
public class AlertRule
{
    public Guid Id { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public string MetricName { get; private set; } = string.Empty;
    public string ComparisonOperator { get; private set; } = ">";
    public double Threshold { get; private set; }
    public AlertSeverity Severity { get; private set; }
    public int ConsecutiveFaults { get; private set; } = 2;
    public bool IsEnabled { get; private set; } = true;

    private AlertRule() { }

    public AlertRule(string name, string metricName, string comparisonOperator,
        double threshold, AlertSeverity severity, int consecutiveFaults = 2)
    {
        Id = Guid.NewGuid();
        Name = name ?? throw new ArgumentNullException(nameof(name));
        MetricName = metricName ?? throw new ArgumentNullException(nameof(metricName));
        ComparisonOperator = comparisonOperator;
        Threshold = threshold;
        Severity = severity;
        ConsecutiveFaults = consecutiveFaults;
    }
}