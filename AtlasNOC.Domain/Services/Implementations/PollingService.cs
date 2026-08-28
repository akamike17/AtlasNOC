using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using AtlasNOC.Domain.Entities;
using AtlasNOC.Domain.Enums;
using AtlasNOC.Domain.Services.Interfaces;
using AtlasNOC.Domain.ValueObjects;

namespace AtlasNOC.Domain.Services;

public sealed class PollingService : IPollingService
{
    private readonly IRepository<Device> _deviceRepository;
    private readonly ISnmpService _snmpService;
    private readonly ICredentialService _credentialService;
    private readonly IAlertService _alertService;
    private readonly ILogger<PollingService> _logger;
    private readonly PollingOptions _options;
    private readonly SemaphoreSlim _pollSemaphore;
    private readonly IMetricHistoryService _metricHistory;

    public PollingService(
        IRepository<Device> deviceRepository,
        ISnmpService snmpService,
        ICredentialService credentialService,
        IAlertService alertService,
        ILogger<PollingService> logger,
        IOptions<PollingOptions> options,
        IMetricHistoryService metricHistory)
    {
        _deviceRepository = deviceRepository ?? throw new ArgumentNullException(nameof(deviceRepository));
        _snmpService = snmpService ?? throw new ArgumentNullException(nameof(snmpService));
        _credentialService = credentialService ?? throw new ArgumentNullException(nameof(credentialService));
        _alertService = alertService ?? throw new ArgumentNullException(nameof(alertService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _options = options?.Value ?? new PollingOptions();
        _metricHistory = metricHistory ?? throw new ArgumentNullException(nameof(metricHistory));
        _pollSemaphore = new SemaphoreSlim(_options.MaxConcurrency);
    }

    public async Task<PollingResult> PollDeviceAsync(DeviceId deviceId, CancellationToken cancellationToken = default)
    {
        await _pollSemaphore.WaitAsync(cancellationToken);
        try
        {
            var device = await _deviceRepository.GetByIdAsync(deviceId, cancellationToken);
            if (device == null || !device.IsActive)
            {
                return new PollingResult(
                    deviceId,
                    DateTime.UtcNow,
                    false,
                    new PollingMetrics(null, 0.0, null, null, null),
                    "Device not found or inactive",
                    Array.Empty<PollingAlert>()
                );
            }

            var credentials = await _credentialService.GetActiveAsync(cancellationToken);
            var usableCredentials = credentials.Where(c => c.CanUse).ToList();

            if (!usableCredentials.Any())
            {
                return new PollingResult(
                    deviceId,
                    DateTime.UtcNow,
                    false,
                    new PollingMetrics(null, 0.0, null, null, null),
                    "No usable credentials",
                    Array.Empty<PollingAlert>()
                );
            }

            var pollStart = DateTime.UtcNow;
            var metrics = new Dictionary<string, object>();
            var generatedAlerts = new List<PollingAlert>();
            string? errorMessage = null;
            bool success = false;

            foreach (var credential in usableCredentials)
            {
                if (cancellationToken.IsCancellationRequested) break;

                try
                {
                    var ipAddress = IPAddress.Parse(device.IpAddress);
                    var testResult = await _snmpService.TestConnectionAsync(
                        ipAddress,
                        credential,
                        _options.SnmpTimeout,
                        cancellationToken);

                    if (testResult.Success)
                    {
                        success = true;
                        var snmpMetrics = await CollectSnmpMetricsAsync(device, credential, cancellationToken);
                        snmpMetrics.LatencyMs = testResult.Elapsed.TotalMilliseconds;

                        if (snmpMetrics.LatencyMs.HasValue)
                            metrics["latency_ms"] = snmpMetrics.LatencyMs.Value;

                        if (snmpMetrics.InterfaceUtilization?.Any() == true)
                            metrics["interfaces"] = snmpMetrics.InterfaceUtilization;

                        // Check thresholds and generate alerts
                        var alerts = CheckThresholds(deviceId, snmpMetrics, device);
                        generatedAlerts.AddRange(alerts);

                        break;
                    }
                    else
                    {
                        errorMessage = testResult.ErrorMessage;
                    }
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Polling failed for device {DeviceId} with credential {CredentialId}", deviceId, credential.Id);
                    errorMessage = ex.Message;
                }
            }

            var previousStatus = device.Status;
            var newStatus = success ? DeviceStatus.Up : DeviceStatus.Down;
            if (previousStatus != newStatus)
            {
                device.UpdateStatus(newStatus, "PollingService");
                device.SetLastChecked();
                await _deviceRepository.UpdateAsync(device, cancellationToken);

                if (!success)
                {
                    generatedAlerts.Add(new PollingAlert(
                        "device_down", AlertSeverity.High, "Device unreachable",
                        new Dictionary<string, object> { ["previous_status"] = previousStatus.ToString() }));
                }
                else
                {
                    var activeAlerts = await _alertService.GetAlertsForDeviceAsync(deviceId, cancellationToken);
                    foreach (var downAlert in activeAlerts.Where(alert => alert.IsActive &&
                                 string.Equals(alert.Message, "Device unreachable", StringComparison.OrdinalIgnoreCase)))
                    {
                        await _alertService.ResolveAsync(downAlert.Id, "PollingService", "Device recovered", cancellationToken);
                    }
                }
            }
            else
            {
                device.SetLastChecked();
                await _deviceRepository.UpdateAsync(device, cancellationToken);
            }

            var result = new PollingResult(
                deviceId,
                pollStart,
                success,
                new PollingMetrics(
                    LatencyMs: metrics.TryGetValue("latency_ms", out var lat) ? (double?)lat : null,
                    AvailabilityPercent: success ? 100.0 : 0.0,
                    InterfaceUtilization: metrics.TryGetValue("interfaces", out var ifaces) ? (IDictionary<string, object>?)ifaces : null,
                    CpuMemory: null,
                    Environment: null
                ),
                errorMessage,
                generatedAlerts
            );

            // Fire alerts
            foreach (var alert in generatedAlerts)
            {
                await _alertService.CreateAsync(new CreateAlertRequest(
                    deviceId,
                    alert.Message,
                    alert.Severity,
                    "polling",
                    alert.Context ?? new Dictionary<string, object>()
                ), cancellationToken);
            }

            await _metricHistory.SaveAsync(result, cancellationToken);

            return result;
        }
        finally
        {
            _pollSemaphore.Release();
        }
    }

    public async Task<IReadOnlyList<PollingResult>> PollAllAsync(CancellationToken cancellationToken = default)
    {
        var devices = await _deviceRepository.GetAllAsync(cancellationToken);
        var activeDevices = devices.Where(d => d.IsActive).ToList();
        var results = new ConcurrentBag<PollingResult>();
        await Parallel.ForEachAsync(activeDevices,
            new ParallelOptions
            {
                MaxDegreeOfParallelism = _options.MaxConcurrency,
                CancellationToken = cancellationToken
            },
            async (device, token) =>
            {
                var result = await PollDeviceAsync(device.Id, token);
                results.Add(result);
            });
        return results.OrderBy(r => r.DeviceId.Value).ToList();
    }

    private async Task<SnmpMetrics> CollectSnmpMetricsAsync(Device device, Credential credential, CancellationToken cancellationToken)
    {
        var metrics = new SnmpMetrics();

        // System uptime
        var uptimeOid = "1.3.6.1.2.1.1.3.0";
        var uptimeResult = await _snmpService.GetAsync(IPAddress.Parse(device.IpAddress), credential, uptimeOid, _options.SnmpTimeout, cancellationToken);
        if (uptimeResult.Success && TimeSpan.TryParse(uptimeResult.Value, out var uptime))
        {
            metrics.Uptime = uptime;
        }

        // Interface table
        var ifTableOid = "1.3.6.1.2.1.2.2.1";
        var ifResult = await _snmpService.WalkAsync(IPAddress.Parse(device.IpAddress), credential, ifTableOid, _options.SnmpTimeout, cancellationToken);
        if (ifResult.Success)
        {
            var interfaces = ParseInterfaceTable(ifResult.Values);
            metrics.InterfaceUtilization = interfaces;
        }

        return metrics;
    }

    internal static IDictionary<string, object> ParseInterfaceTable(IReadOnlyDictionary<string, string> values)
    {
        var result = new Dictionary<string, object>();
        var columns = new Dictionary<int, string>
        {
            [1] = "index",
            [2] = "description",
            [3] = "type",
            [4] = "mtu",
            [5] = "speed_bps",
            [6] = "mac_address",
            [7] = "admin_status",
            [8] = "oper_status",
            [10] = "in_octets",
            [13] = "in_discards",
            [14] = "in_errors",
            [16] = "out_octets",
            [19] = "out_discards",
            [20] = "out_errors"
        };
        var interfaces = new Dictionary<int, Dictionary<string, object>>();
        const string prefix = "1.3.6.1.2.1.2.2.1.";

        foreach (var (oid, value) in values)
        {
            if (!oid.StartsWith(prefix, StringComparison.Ordinal)) continue;
            var suffix = oid[prefix.Length..].Split('.');
            if (suffix.Length != 2 || !int.TryParse(suffix[0], out var column) ||
                !int.TryParse(suffix[1], out var index) || !columns.TryGetValue(column, out var name)) continue;
            if (!interfaces.TryGetValue(index, out var item))
            {
                item = new Dictionary<string, object> { ["if_index"] = index };
                interfaces[index] = item;
            }

            item[name] = ulong.TryParse(value, out var number) ? number : value;
        }

        foreach (var (index, item) in interfaces.OrderBy(pair => pair.Key))
            result[index.ToString(System.Globalization.CultureInfo.InvariantCulture)] = item;
        return result;
    }

    private IReadOnlyList<PollingAlert> CheckThresholds(DeviceId deviceId, SnmpMetrics metrics, Device device)
    {
        var alerts = new List<PollingAlert>();

        // Example threshold checks - would be configured per device
        if (metrics.LatencyMs.HasValue && metrics.LatencyMs.Value > 1000)
        {
            alerts.Add(new PollingAlert(
                "high_latency",
                AlertSeverity.Medium,
                $"High latency detected: {metrics.LatencyMs.Value}ms",
                new Dictionary<string, object> { ["latency_ms"] = metrics.LatencyMs.Value }
            ));
        }

        return alerts;
    }
}

public sealed class PollingOptions
{
    public int IntervalSeconds { get; set; } = 60;
    public int MaxConcurrency { get; set; } = 50;
    public TimeSpan SnmpTimeout { get; set; } = TimeSpan.FromSeconds(5);
    public bool AutoStart { get; set; } = true;
    public int RetentionDays { get; set; } = 90;
}

internal sealed class SnmpMetrics
{
    public TimeSpan? Uptime { get; set; }
    public double? LatencyMs { get; set; }
    public IDictionary<string, object>? InterfaceUtilization { get; set; }
}
