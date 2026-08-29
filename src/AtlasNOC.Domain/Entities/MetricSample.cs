using AtlasNOC.Domain.ValueObjects;

namespace AtlasNOC.Domain.Entities;

/// <summary>Muestra de métrica time-series de un recurso monitorizado.</summary>
public class MetricSample
{
    public long Id { get; private set; }
    public string ResourceType { get; private set; } = string.Empty;
    public string ResourceId { get; private set; } = string.Empty;
    public string MetricName { get; private set; } = string.Empty;
    public DateTime TimestampUtc { get; private set; }
    public double ValueDouble { get; private set; }
    public string? Unit { get; private set; }
    public string? Quality { get; private set; }

    private MetricSample() { }

    public MetricSample(string resourceType, string resourceId, string metricName,
        double valueDouble, DateTime timestampUtc, string? unit = null, string? quality = null)
    {
        ResourceType = resourceType ?? throw new ArgumentNullException(nameof(resourceType));
        ResourceId = resourceId ?? throw new ArgumentNullException(nameof(resourceId));
        MetricName = metricName ?? throw new ArgumentNullException(nameof(metricName));
        ValueDouble = valueDouble;
        TimestampUtc = timestampUtc;
        Unit = unit;
        Quality = quality;
    }

    public static MetricSample ForDevice(DeviceId deviceId, string metricName,
        double value, DateTime timestampUtc, string? unit = null, string? quality = null)
        => new("Device", deviceId.Value.ToString(), metricName, value, timestampUtc, unit, quality);
}