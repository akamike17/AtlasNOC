using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using AtlasNOC.Domain.Entities;
using AtlasNOC.Domain.Enums;
using AtlasNOC.Domain.Services.Interfaces;
using AtlasNOC.Domain.ValueObjects;
using System.Net;
using System.Text.Json;

namespace AtlasNOC.Domain.Services;

public sealed class MonitoringService : IMonitoringService
{
    private readonly IRepository<Device> _deviceRepository;
    private readonly ILogger<MonitoringService> _logger;
    private readonly MonitoringOptions _options;
    private readonly ConcurrentDictionary<DeviceId, MonitoringState> _states = new();
    private readonly ConcurrentDictionary<DeviceId, MonitoringThresholds> _thresholds = new();

    public MonitoringService(
        IRepository<Device> deviceRepository,
        ILogger<MonitoringService> logger,
        IOptions<MonitoringOptions> options)
    {
        _deviceRepository = deviceRepository ?? throw new ArgumentNullException(nameof(deviceRepository));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _options = options?.Value ?? new MonitoringOptions();
    }

    public async Task<MonitoringState> GetDeviceStateAsync(DeviceId deviceId, CancellationToken cancellationToken = default)
    {
        if (_states.TryGetValue(deviceId, out var state))
        {
            return state;
        }

        var device = await _deviceRepository.GetByIdAsync(deviceId, cancellationToken);
        if (device == null)
        {
            throw new KeyNotFoundException($"Device {deviceId} not found");
        }

        return new MonitoringState(
            deviceId,
            device.Status,
            DateTime.UtcNow,
            device.CreatedAt,
            Array.Empty<ActiveThresholdViolation>(),
            null
        );
    }

    public async Task<IReadOnlyList<MonitoringState>> GetAllStatesAsync(CancellationToken cancellationToken = default)
    {
        var devices = await _deviceRepository.GetAllAsync(cancellationToken);
        var states = new List<MonitoringState>();

        foreach (var device in devices.Where(d => d.IsActive))
        {
            states.Add(await GetDeviceStateAsync(device.Id, cancellationToken));
        }

        return states;
    }

    public async Task<MonitoringState?> UpdateStateAsync(DeviceId deviceId, MonitoringState newState, CancellationToken cancellationToken = default)
    {
        var device = await _deviceRepository.GetByIdAsync(deviceId, cancellationToken);
        if (device == null) return null;

        var oldState = _states.TryGetValue(deviceId, out var existing) ? existing : null;

        var violations = EvaluateViolations(deviceId, newState.LastMetrics);
        var updatedState = new MonitoringState(
            newState.DeviceId,
            newState.Status,
            newState.LastPolled,
            oldState?.LastStateChange ?? newState.LastStateChange,
            violations,
            newState.LastMetrics
        );

        if (oldState?.Status != newState.Status)
        {
            updatedState = updatedState with { LastStateChange = DateTime.UtcNow };
            _logger.LogInformation("Device {DeviceId} status changed from {OldStatus} to {NewStatus}",
                deviceId, oldState?.Status, newState.Status);
        }

        _states[deviceId] = updatedState;
        return updatedState;
    }

    private IReadOnlyList<ActiveThresholdViolation> EvaluateViolations(
        DeviceId deviceId, PollingMetrics? metrics)
    {
        if (metrics is null) return Array.Empty<ActiveThresholdViolation>();
        var configured = _thresholds.TryGetValue(deviceId, out var thresholds)
            ? thresholds
            : DefaultThresholds(deviceId);
        var values = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);
        if (metrics.LatencyMs is { } latency) values["latency_ms"] = latency;
        if (metrics.AvailabilityPercent is { } availability) values["availability_percent"] = availability;
        AddNumericValues(values, metrics.CpuMemory);
        AddNumericValues(values, metrics.Environment);

        var violations = new List<ActiveThresholdViolation>();
        foreach (var rule in configured.Rules)
        {
            if (!values.TryGetValue(rule.MetricName, out var value)) continue;
            var critical = IsViolation(value, rule.CriticalThreshold, rule.Operator);
            var warning = IsViolation(value, rule.WarningThreshold, rule.Operator);
            if (!critical && !warning) continue;
            violations.Add(new ActiveThresholdViolation(rule.MetricName, value,
                critical ? rule.CriticalThreshold : rule.WarningThreshold, rule.Operator,
                critical ? AlertSeverity.Critical : AlertSeverity.Medium, DateTime.UtcNow));
        }
        return violations;
    }

    private static void AddNumericValues(IDictionary<string, double> target,
        IDictionary<string, object>? source)
    {
        if (source is null) return;
        foreach (var pair in source)
        {
            if (pair.Value is JsonElement element && element.ValueKind == JsonValueKind.Number &&
                element.TryGetDouble(out var jsonNumber)) target[pair.Key] = jsonNumber;
            else if (pair.Value is IConvertible convertible)
            {
                try { target[pair.Key] = convertible.ToDouble(System.Globalization.CultureInfo.InvariantCulture); }
                catch (FormatException) { }
                catch (InvalidCastException) { }
            }
        }
    }

    private static bool IsViolation(double value, double threshold, ThresholdOperator operation) => operation switch
    {
        ThresholdOperator.GreaterThan => value > threshold,
        ThresholdOperator.LessThan => value < threshold,
        ThresholdOperator.Equal => Math.Abs(value - threshold) < 0.000001,
        ThresholdOperator.NotEqual => Math.Abs(value - threshold) >= 0.000001,
        ThresholdOperator.GreaterThanOrEqual => value >= threshold,
        ThresholdOperator.LessThanOrEqual => value <= threshold,
        _ => false
    };

    public async Task RegisterThresholdsAsync(DeviceId deviceId, MonitoringThresholds thresholds, CancellationToken cancellationToken = default)
    {
        var device = await _deviceRepository.GetByIdAsync(deviceId, cancellationToken);
        if (device == null)
        {
            throw new KeyNotFoundException($"Device {deviceId} not found");
        }

        _thresholds[deviceId] = thresholds;
        _logger.LogInformation("Thresholds registered for device {DeviceId}", deviceId);
    }

    public async Task<MonitoringThresholds?> GetThresholdsAsync(DeviceId deviceId, CancellationToken cancellationToken = default)
    {
        if (_thresholds.TryGetValue(deviceId, out var thresholds))
        {
            return thresholds;
        }

        // Return defaults if none configured
        return DefaultThresholds(deviceId);
    }

    private static MonitoringThresholds DefaultThresholds(DeviceId deviceId) => new(
            deviceId,
            new[]
            {
                new ThresholdRule("latency_ms", 500, 1000, ThresholdOperator.GreaterThan, TimeSpan.FromMinutes(5)),
                new ThresholdRule("availability_percent", 99.9, 99.0, ThresholdOperator.LessThan, TimeSpan.FromMinutes(1)),
                new ThresholdRule("cpu_percent", 80, 95, ThresholdOperator.GreaterThan, TimeSpan.FromMinutes(5)),
                new ThresholdRule("memory_percent", 85, 95, ThresholdOperator.GreaterThan, TimeSpan.FromMinutes(5))
            },
            DateTime.UtcNow
        );
}

public sealed class MonitoringOptions
{
    public int StateCacheTtlSeconds { get; set; } = 300;
    public int MaxViolationHistory { get; set; } = 100;
}

public sealed class NotificationService : INotificationService
{
    private readonly IRepository<AtlasNOC.Domain.Entities.NotificationChannel> _channelRepository;
    private readonly IAuditService _auditService;
    private readonly ILogger<NotificationService> _logger;
    private readonly NotificationOptions _options;
    private readonly HttpClient _httpClient;
    private readonly ICredentialProtector _secretProtector;

    public NotificationService(
        IRepository<AtlasNOC.Domain.Entities.NotificationChannel> channelRepository,
        IAuditService auditService,
        ILogger<NotificationService> logger,
        IOptions<NotificationOptions> options,
        HttpClient httpClient,
        ICredentialProtector secretProtector)
    {
        _channelRepository = channelRepository ?? throw new ArgumentNullException(nameof(channelRepository));
        _auditService = auditService ?? throw new ArgumentNullException(nameof(auditService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _options = options?.Value ?? new NotificationOptions();
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _secretProtector = secretProtector ?? throw new ArgumentNullException(nameof(secretProtector));
    }

    public async Task<NotificationResult> SendAsync(NotificationRequest request, CancellationToken cancellationToken = default)
    {
        var notificationId = Guid.NewGuid();
        var sentAt = DateTime.UtcNow;
        var channelResults = new List<ChannelDeliveryResult>();

        var entities = await _channelRepository.GetAllAsync(cancellationToken);
        var targetEntities = request.ChannelIds.Any()
            ? entities.Where(e => request.ChannelIds.Contains(e.Id)).ToList()
            : entities.Where(e => e.IsEnabled).ToList();

        foreach (var entity in targetEntities)
        {
            var attemptAt = DateTime.UtcNow;
            bool success = false;
            string? error = null;

            try
            {
                success = await DeliverToChannelAsync(entity, request, cancellationToken);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError("Failed to deliver notification {NotificationId} to channel {ChannelId}. ErrorType={ErrorType}",
                    notificationId, entity.Id, ex.GetType().Name);
                error = "Delivery failed.";
            }

            channelResults.Add(new ChannelDeliveryResult(
                entity.Id,
                entity.Name,
                success,
                error,
                attemptAt
            ));
        }

        var overallSuccess = channelResults.Any(r => r.Success);

        await _auditService.LogSuccessAsync("Notification", "Send", "system",
            targetResource: notificationId.ToString(),
            targetResourceType: "notification",
            newValue: $"Title={request.Title}, Severity={request.Severity}, Channels={channelResults.Count}",
            cancellationToken: cancellationToken);

        return new NotificationResult(
            overallSuccess,
            notificationId,
            channelResults,
            sentAt
        );
    }

    private async Task<bool> DeliverToChannelAsync(AtlasNOC.Domain.Entities.NotificationChannel channel, NotificationRequest request, CancellationToken cancellationToken)
    {
        return channel.Type switch
        {
            NotificationChannelType.Email => await DeliverEmailAsync(channel, request, cancellationToken),
            NotificationChannelType.Webhook => await DeliverWebhookAsync(channel, request, cancellationToken),
            NotificationChannelType.Slack => await DeliverSlackAsync(channel, request, cancellationToken),
            NotificationChannelType.Teams => await DeliverTeamsAsync(channel, request, cancellationToken),
            _ => await DeliverGenericWebhookAsync(channel, request, cancellationToken)
        };
    }

    private Task<bool> DeliverEmailAsync(AtlasNOC.Domain.Entities.NotificationChannel channel, NotificationRequest request, CancellationToken cancellationToken)
    {
        _logger.LogWarning("Email channel {ChannelId} is not configured with a production transport", channel.Id);
        return Task.FromResult(false);
    }

    private async Task<bool> DeliverWebhookAsync(AtlasNOC.Domain.Entities.NotificationChannel channel, NotificationRequest request, CancellationToken cancellationToken)
    {
        var configuration = GetProtectedConfiguration(channel);
        if (!configuration.TryGetValue("url", out var url) || !IsAllowedWebhook(url)) return false;

        var payload = new
        {
            id = Guid.NewGuid(),
            title = request.Title,
            message = request.Message,
            severity = request.Severity.ToString(),
            recipients = request.Recipients,
            context = request.Context,
            timestamp = DateTime.UtcNow
        };

        var json = JsonSerializer.Serialize(payload);
        var content = new StringContent(json, System.Text.Encoding.UTF8, "application/json");
        using var httpRequest = new HttpRequestMessage(HttpMethod.Post, url) { Content = content };

        if (configuration.TryGetValue("headers", out var headersJson))
        {
            var headers = JsonSerializer.Deserialize<Dictionary<string, string>>(headersJson);
            foreach (var (key, value) in headers ?? new Dictionary<string, string>())
            {
                httpRequest.Headers.TryAddWithoutValidation(key, value);
            }
        }

        using var response = await _httpClient.SendAsync(httpRequest, cancellationToken);
        return response.IsSuccessStatusCode;
    }

    private Task<bool> DeliverSlackAsync(AtlasNOC.Domain.Entities.NotificationChannel channel, NotificationRequest request, CancellationToken cancellationToken)
    {
        // Slack webhook format
        return DeliverWebhookAsync(channel, request, cancellationToken);
    }

    private Task<bool> DeliverTeamsAsync(AtlasNOC.Domain.Entities.NotificationChannel channel, NotificationRequest request, CancellationToken cancellationToken)
    {
        // Teams webhook format
        return DeliverWebhookAsync(channel, request, cancellationToken);
    }

    private Task<bool> DeliverGenericWebhookAsync(AtlasNOC.Domain.Entities.NotificationChannel channel, NotificationRequest request, CancellationToken cancellationToken)
    {
        return DeliverWebhookAsync(channel, request, cancellationToken);
    }

    public async Task<IReadOnlyList<AtlasNOC.Domain.Services.Interfaces.NotificationChannel>> GetChannelsAsync(CancellationToken cancellationToken = default)
    {
        var entities = await _channelRepository.GetAllAsync(cancellationToken);
        return entities.Select(e => new AtlasNOC.Domain.Services.Interfaces.NotificationChannel(
            e.Id,
            e.Name,
            e.Type,
            new Dictionary<string, string> { ["configured"] = "true" },
            e.IsEnabled,
            e.CreatedAt
        )).ToList();
    }

    public async Task<AtlasNOC.Domain.Services.Interfaces.NotificationChannel?> RegisterChannelAsync(AtlasNOC.Domain.Services.Interfaces.NotificationChannel channel, CancellationToken cancellationToken = default)
    {
        ValidateChannelConfiguration(channel);
        var serializedConfiguration = JsonSerializer.Serialize(channel.Configuration);
        var protectedConfiguration = new Dictionary<string, string>
        {
            ["__protected"] = _secretProtector.Protect(serializedConfiguration)
        };
        var entity = new AtlasNOC.Domain.Entities.NotificationChannel(
            channel.Name,
            channel.Type,
            protectedConfiguration,
            channel.IsEnabled
        );

        await _channelRepository.AddAsync(entity, cancellationToken);
        _logger.LogInformation("Registered notification channel {ChannelId} ({Name})", entity.Id, entity.Name);

        return new AtlasNOC.Domain.Services.Interfaces.NotificationChannel(
            entity.Id,
            entity.Name,
            entity.Type,
            new Dictionary<string, string> { ["configured"] = "true" },
            entity.IsEnabled,
            entity.CreatedAt
        );
    }

    public async Task<bool> TestChannelAsync(Guid channelId, CancellationToken cancellationToken = default)
    {
        var channels = await GetChannelsAsync(cancellationToken);
        var channel = channels.FirstOrDefault(c => c.Id == channelId);

        if (channel == null) return false;

        var testRequest = new NotificationRequest(
            "Test Notification",
            "This is a test notification from AtlasNOC",
            AlertSeverity.Info,
            new[] { "test" },
            new Dictionary<string, object> { ["test"] = true },
            new[] { channelId }
        );

        var result = await SendAsync(testRequest, cancellationToken);
        return result.ChannelResults.FirstOrDefault(r => r.ChannelId == channelId)?.Success ?? false;
    }

    private IDictionary<string, string> GetProtectedConfiguration(
        AtlasNOC.Domain.Entities.NotificationChannel channel)
    {
        if (!channel.Configuration.TryGetValue("__protected", out var ciphertext))
            throw new InvalidOperationException("Notification channel contains legacy plaintext configuration and must be rotated.");
        var json = _secretProtector.Unprotect(ciphertext);
        return JsonSerializer.Deserialize<Dictionary<string, string>>(json)
            ?? throw new InvalidOperationException("Notification channel configuration is invalid.");
    }

    private static void ValidateChannelConfiguration(AtlasNOC.Domain.Services.Interfaces.NotificationChannel channel)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(channel.Name);
        if (channel.Type is NotificationChannelType.Webhook or NotificationChannelType.Slack or
            NotificationChannelType.Teams or NotificationChannelType.Custom)
        {
            if (!channel.Configuration.TryGetValue("url", out var url) || !IsAllowedWebhook(url))
                throw new ArgumentException("Webhook channel requires a public HTTPS URL.", nameof(channel));
        }
    }

    private static bool IsAllowedWebhook(string url)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) || uri.Scheme != Uri.UriSchemeHttps ||
            string.IsNullOrWhiteSpace(uri.Host) || uri.IsLoopback ||
            string.Equals(uri.Host, "localhost", StringComparison.OrdinalIgnoreCase)) return false;
        if (!IPAddress.TryParse(uri.Host, out var address)) return true;
        var bytes = address.GetAddressBytes();
        return address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork &&
               bytes[0] != 10 && bytes[0] != 127 &&
               !(bytes[0] == 192 && bytes[1] == 168) &&
               !(bytes[0] == 172 && bytes[1] is >= 16 and <= 31) &&
               !(bytes[0] == 169 && bytes[1] == 254);
    }
}

public sealed class NotificationOptions
{
    public int MaxRetries { get; set; } = 3;
    public TimeSpan RetryDelay { get; set; } = TimeSpan.FromSeconds(30);
    public TimeSpan HttpTimeout { get; set; } = TimeSpan.FromSeconds(30);
}
