using System.Text.Json;
using AtlasNOC.Domain.Services.Interfaces;
using AtlasNOC.Domain.ValueObjects;

namespace AtlasNOC.Domain.Entities;

public sealed class MetricSample
{
    public Guid Id { get; private set; }
    public DeviceId DeviceId { get; private set; } = null!;
    public DateTime Timestamp { get; private set; }
    public bool Success { get; private set; }
    public double? LatencyMs { get; private set; }
    public double AvailabilityPercent { get; private set; }
    public string? InterfaceMetricsJson { get; private set; }
    public string? ErrorMessage { get; private set; }

    private MetricSample() { }

    public static MetricSample FromPollingResult(PollingResult result)
    {
        ArgumentNullException.ThrowIfNull(result);
        return new MetricSample
        {
            Id = Guid.NewGuid(),
            DeviceId = result.DeviceId,
            Timestamp = result.PollTime,
            Success = result.Success,
            LatencyMs = result.Metrics.LatencyMs,
            AvailabilityPercent = result.Metrics.AvailabilityPercent ?? (result.Success ? 100 : 0),
            InterfaceMetricsJson = result.Metrics.InterfaceUtilization is null
                ? null : JsonSerializer.Serialize(result.Metrics.InterfaceUtilization),
            ErrorMessage = string.IsNullOrWhiteSpace(result.ErrorMessage)
                ? null : result.ErrorMessage[..Math.Min(result.ErrorMessage.Length, 500)]
        };
    }
}
