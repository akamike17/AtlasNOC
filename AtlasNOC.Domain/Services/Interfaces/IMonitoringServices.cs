using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using AtlasNOC.Domain.Enums;
using AtlasNOC.Domain.ValueObjects;

namespace AtlasNOC.Domain.Services.Interfaces;

public interface IPollingService
{
    Task<PollingResult> PollDeviceAsync(DeviceId deviceId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<PollingResult>> PollAllAsync(CancellationToken cancellationToken = default);
}

public sealed record PollingResult
(
    DeviceId DeviceId,
    DateTime PollTime,
    bool Success,
    PollingMetrics Metrics,
    string? ErrorMessage,
    IReadOnlyList<PollingAlert> GeneratedAlerts
);

public sealed record PollingMetrics
(
    double? LatencyMs,
    double? AvailabilityPercent,
    IDictionary<string, object>? InterfaceUtilization,
    IDictionary<string, object>? CpuMemory,
    IDictionary<string, object>? Environment
);

public sealed record PollingAlert
(
    string AlertKey,
    AlertSeverity Severity,
    string Message,
    IDictionary<string, object>? Context
);

public interface IMonitoringService
{
    Task<MonitoringState> GetDeviceStateAsync(DeviceId deviceId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<MonitoringState>> GetAllStatesAsync(CancellationToken cancellationToken = default);
    Task<MonitoringState?> UpdateStateAsync(DeviceId deviceId, MonitoringState newState, CancellationToken cancellationToken = default);
    Task RegisterThresholdsAsync(DeviceId deviceId, MonitoringThresholds thresholds, CancellationToken cancellationToken = default);
    Task<MonitoringThresholds?> GetThresholdsAsync(DeviceId deviceId, CancellationToken cancellationToken = default);
}

public sealed record MonitoringState
(
    DeviceId DeviceId,
    DeviceStatus Status,
    DateTime LastPolled,
    DateTime LastStateChange,
    IReadOnlyList<ActiveThresholdViolation> Violations,
    PollingMetrics? LastMetrics
);

public sealed record ActiveThresholdViolation
(
    string MetricName,
    double CurrentValue,
    double ThresholdValue,
    ThresholdOperator Operator,
    AlertSeverity Severity,
    DateTime TriggeredAt
);

public sealed record MonitoringThresholds
(
    DeviceId DeviceId,
    IReadOnlyList<ThresholdRule> Rules,
    DateTime UpdatedAt
);

public sealed record ThresholdRule
(
    string MetricName,
    double WarningThreshold,
    double CriticalThreshold,
    ThresholdOperator Operator,
    TimeSpan Duration
);

public enum ThresholdOperator
{
    GreaterThan = 1,
    LessThan = 2,
    Equal = 3,
    NotEqual = 4,
    GreaterThanOrEqual = 5,
    LessThanOrEqual = 6
}

public interface INotificationService
{
    Task<NotificationResult> SendAsync(NotificationRequest request, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<NotificationChannel>> GetChannelsAsync(CancellationToken cancellationToken = default);
    Task<NotificationChannel?> RegisterChannelAsync(NotificationChannel channel, CancellationToken cancellationToken = default);
    Task<bool> TestChannelAsync(Guid channelId, CancellationToken cancellationToken = default);
}

public sealed record NotificationRequest
(
    string Title,
    string Message,
    AlertSeverity Severity,
    IReadOnlyList<string> Recipients,
    IDictionary<string, object>? Context,
    IReadOnlyList<Guid> ChannelIds
);

public sealed record NotificationResult
(
    bool Success,
    Guid NotificationId,
    IReadOnlyList<ChannelDeliveryResult> ChannelResults,
    DateTime SentAt
);

public sealed record ChannelDeliveryResult
(
    Guid ChannelId,
    string ChannelName,
    bool Success,
    string? ErrorMessage,
    DateTime AttemptedAt
);

public sealed record NotificationChannel
(
    Guid Id,
    string Name,
    NotificationChannelType Type,
    IDictionary<string, string> Configuration,
    bool IsEnabled,
    DateTime CreatedAt
);