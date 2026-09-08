using AtlasNOC.Domain.Enums;

namespace AtlasNOC.Domain.Entities;

/// <summary>Regla de alerta declarativa (umbral + severidad).</summary>
public class AlertRule
{
    public static readonly IReadOnlySet<string> SupportedOperators =
        new HashSet<string>(StringComparer.Ordinal) { ">", ">=", "<", "<=", "==" };
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
        if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("El nombre es obligatorio.", nameof(name));
        if (string.IsNullOrWhiteSpace(metricName)) throw new ArgumentException("La métrica es obligatoria.", nameof(metricName));
        if (!SupportedOperators.Contains(comparisonOperator)) throw new ArgumentException("Operador de comparación inválido.", nameof(comparisonOperator));
        if (consecutiveFaults < 1) throw new ArgumentOutOfRangeException(nameof(consecutiveFaults));
        if (!Enum.IsDefined(severity)) throw new ArgumentOutOfRangeException(nameof(severity));
        Id = Guid.NewGuid();
        Name = name.Trim();
        MetricName = metricName.Trim();
        ComparisonOperator = comparisonOperator;
        Threshold = threshold;
        Severity = severity;
        ConsecutiveFaults = consecutiveFaults;
    }

    public void SetEnabled(bool enabled) => IsEnabled = enabled;
}
