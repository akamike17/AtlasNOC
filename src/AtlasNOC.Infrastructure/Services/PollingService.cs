using AtlasNOC.Application.Devices;
using AtlasNOC.Application.Probes;
using AtlasNOC.Application.Repositories;
using AtlasNOC.Application.Services;
using AtlasNOC.Infrastructure.Persistence;
using AtlasNOC.Domain.Entities;
using AtlasNOC.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace AtlasNOC.Infrastructure.Services;

/// <summary>Polling de dispositivos gestionados: ICMP + driver + métricas.</summary>
public class PollingService : IPollingService
{
    private readonly IDeviceRepository _devices;
    private readonly IIcmpProbe _icmp;
    private readonly IDeviceDriverRegistry _drivers;
    private readonly IMetricWriter _metricWriter;
    private readonly AtlasNOCDbContext _context;
    private readonly ILogger<PollingService> _logger;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly PollingOptions _options;

    public PollingService(IDeviceRepository devices, IIcmpProbe icmp,
        IDeviceDriverRegistry drivers, IMetricWriter metricWriter,
        AtlasNOCDbContext context, ILogger<PollingService> logger,
        IServiceScopeFactory scopeFactory, IOptions<PollingOptions> options)
    {
        _devices = devices;
        _icmp = icmp;
        _drivers = drivers;
        _metricWriter = metricWriter;
        _context = context;
        _logger = logger;
        _scopeFactory = scopeFactory;
        _options = options.Value;
    }

    public async Task PollAllManagedAsync(CancellationToken ct = default)
    {
        var devices = await _devices.ListManagedAsync(ct);
        var profiles = await _context.PollingProfiles.AsNoTracking().ToListAsync(ct);
        var defaultProfile = profiles.FirstOrDefault(p => p.IsDefault);
        var now = DateTime.UtcNow;
        var due = devices.Where(device => IsDue(device.LastPolledAtUtc, now,
            device.PollingProfileId is { } profileId
                ? profiles.FirstOrDefault(p => p.Id == profileId)?.IcmpIntervalSeconds ?? EffectiveDefaultInterval
                : defaultProfile?.IcmpIntervalSeconds ?? EffectiveDefaultInterval)).ToList();

        await Parallel.ForEachAsync(due,
            new ParallelOptions { MaxDegreeOfParallelism = EffectiveMaxConcurrency, CancellationToken = ct },
            async (device, token) =>
        {
            try
            {
                await using var scope = _scopeFactory.CreateAsyncScope();
                var polling = scope.ServiceProvider.GetRequiredService<IPollingService>();
                await polling.PollDeviceAsync(device.Id.Value, token);
            }
            catch (OperationCanceledException) { throw; }
            catch (Exception ex)
            {
                // Fallos de un dispositivo no detienen el ciclo completo.
                _logger.LogError(ex, "Fallo de polling en dispositivo {Hostname}", device.Hostname);
            }
        });
    }

    public async Task PollDeviceAsync(Guid deviceId, CancellationToken ct = default)
    {
        var device = await _devices.GetByIdAsync(deviceId, ct);
        if (device is null || !device.IsManaged) return;

        var ip = device.ManagementIp;
        var samples = new List<MetricSampleInput>();
        var timestamp = DateTime.UtcNow;
        var responseObserved = false;
        var profile = await ResolveProfileAsync(device, ct);

        var ping = await PingWithRetriesAsync(ip, profile.TimeoutMs, profile.RetryCount, ct);
        if (ping.Success)
        {
            responseObserved = true;
            device.SetStatus(Domain.Enums.DeviceStatus.Up);
            samples.Add(new MetricSampleInput("Device", deviceId.ToString(), "availability", 100, timestamp, "%"));
            if (ping.RoundTripMs.HasValue)
                samples.Add(new MetricSampleInput("Device", deviceId.ToString(), "rtt", ping.RoundTripMs.Value, timestamp, "ms"));
        }
        else
        {
            device.SetStatus(Domain.Enums.DeviceStatus.Down);
            samples.Add(new MetricSampleInput("Device", deviceId.ToString(), "availability", 0, timestamp, "%"));
        }

        // Health indica si el dispositivo respondió; GetMetricsAsync es la única
        // fuente de métricas del driver para no duplicar CPU/memoria.
        IDeviceDriver driver;
        try
        {
            driver = _drivers.Resolve(new DeviceFingerprint(ip, device.Hostname, null, null, device.Vendor.ToString().ToLowerInvariant()));
        }
        catch
        {
            driver = _drivers.Resolve(new DeviceFingerprint(ip, device.Hostname, null, null, "generic"));
        }

        if (IsDue(device.LastHealthPolledAtUtc, timestamp, profile.HealthIntervalSeconds))
        {
            try
            {
                var health = await ExecuteWithRetriesAsync(() => driver.GetHealthAsync(ip, ct), profile.TimeoutMs, profile.RetryCount, ct);
                responseObserved |= HasHealthEvidence(health);

                var driverMetrics = await ExecuteWithRetriesAsync(() => driver.GetMetricsAsync(ip, ct), profile.TimeoutMs, profile.RetryCount, ct);
                responseObserved |= driverMetrics.Count > 0;
                foreach (var metric in driverMetrics)
                {
                    if (samples.Any(s => s.ResourceType == "Device"
                        && s.MetricName.Equals(metric.Name, StringComparison.OrdinalIgnoreCase))) continue;
                    samples.Add(new MetricSampleInput("Device", deviceId.ToString(), metric.Name,
                        metric.Value, timestamp, metric.Unit));
                }
            }
            catch (Exception ex) { _logger.LogDebug(ex, "Sin health/métricas para {Hostname}", device.Hostname); }
            device.MarkHealthPolled(timestamp);
        }

        if (IsDue(device.LastInterfacePolledAtUtc, timestamp, profile.InterfaceIntervalSeconds))
        {
            try
            {
                var interfaces = await ExecuteWithRetriesAsync(() => driver.GetInterfacesAsync(ip, ct), profile.TimeoutMs, profile.RetryCount, ct);
                responseObserved |= interfaces.Count > 0;
                await RefreshInterfacesAsync(device, interfaces, timestamp, ct);
            }
            catch (Exception ex) { _logger.LogDebug(ex, "Sin inventario de interfaces para {Hostname}", device.Hostname); }
            device.MarkInterfacesPolled(timestamp);
        }

        if (IsDue(device.LastWirelessPolledAtUtc, timestamp, profile.WirelessIntervalSeconds))
        {
            try
            {
                var wireless = await ExecuteWithRetriesAsync(() => driver.GetWirelessAssociationsAsync(ip, ct), profile.TimeoutMs, profile.RetryCount, ct);
                responseObserved |= wireless.Count > 0;
                AddWirelessMetrics(samples, deviceId, wireless, timestamp);
            }
            catch (Exception ex) { _logger.LogDebug(ex, "Sin asociaciones inalámbricas para {Hostname}", device.Hostname); }
            device.MarkWirelessPolled(timestamp);
        }

        device.MarkPolled(timestamp);
        if (responseObserved)
            device.MarkSeen(timestamp);
        await _devices.UpdateAsync(device, ct);

        if (samples.Count > 0)
            await _metricWriter.WriteAsync(samples, ct);
        else
            await _context.SaveChangesAsync(ct);
    }

    private int EffectiveMaxConcurrency => Math.Max(1, _options.MaxConcurrency);
    private int EffectiveDefaultInterval => Math.Max(1, _options.DefaultIntervalSeconds);

    internal static bool IsDue(DateTime? lastAtUtc, DateTime nowUtc, int intervalSeconds)
        => !lastAtUtc.HasValue || lastAtUtc.Value.AddSeconds(Math.Max(1, intervalSeconds)) <= nowUtc;

    private async Task<PollingSchedule> ResolveProfileAsync(Device device, CancellationToken ct)
    {
        PollingProfile? profile = null;
        if (device.PollingProfileId.HasValue)
            profile = await _context.PollingProfiles.AsNoTracking().FirstOrDefaultAsync(p => p.Id == device.PollingProfileId.Value, ct);
        profile ??= await _context.PollingProfiles.AsNoTracking().FirstOrDefaultAsync(p => p.IsDefault, ct);
        return profile is null
            ? new(EffectiveDefaultInterval, EffectiveDefaultInterval, Math.Max(1, _options.InterfaceRefreshIntervalSeconds),
                Math.Max(1, _options.WirelessRefreshIntervalSeconds), Math.Max(1, _options.DefaultTimeoutMs), Math.Max(0, _options.DefaultRetries))
            : new(profile.IcmpIntervalSeconds, profile.HealthIntervalSeconds, profile.InterfaceIntervalSeconds,
                profile.WirelessIntervalSeconds, profile.TimeoutMs, profile.RetryCount);
    }

    private async Task<PingResult> PingWithRetriesAsync(string ip, int timeoutMs, int retryCount, CancellationToken ct)
    {
        PingResult result = new(false, null, "not attempted");
        for (var attempt = 0; attempt <= retryCount; attempt++)
        {
            result = await _icmp.PingAsync(ip, timeoutMs, ct);
            if (result.Success) break;
        }
        return result;
    }

    private static async Task<T> ExecuteWithRetriesAsync<T>(Func<Task<T>> operation, int timeoutMs, int retryCount, CancellationToken ct)
    {
        for (var attempt = 0; ; attempt++)
        {
            try { return await operation().WaitAsync(TimeSpan.FromMilliseconds(Math.Max(1, timeoutMs)), ct); }
            catch (OperationCanceledException) { throw; }
            catch when (attempt < retryCount) { ct.ThrowIfCancellationRequested(); }
        }
    }

    private sealed record PollingSchedule(int IcmpIntervalSeconds, int HealthIntervalSeconds,
        int InterfaceIntervalSeconds, int WirelessIntervalSeconds, int TimeoutMs, int RetryCount);

    private async Task RefreshInterfacesAsync(Device device, IReadOnlyList<InterfaceData> observed,
        DateTime timestamp, CancellationToken ct)
    {
        var existing = await _context.DeviceInterfaces
            .Where(i => i.DeviceId == device.Id)
            .ToListAsync(ct);

        foreach (var data in observed)
        {
            var current = existing.FirstOrDefault(i => i.IfIndex == data.IfIndex);
            if (current is null)
            {
                _context.DeviceInterfaces.Add(new DeviceInterface(device.Id, data.IfIndex, data.Name,
                    data.Description, data.MacAddress, data.IpAddress,
                    (InterfaceAdminStatus)data.AdminStatus, (InterfaceOperStatus)data.OperStatus,
                    data.SpeedBps, data.InterfaceType));
            }
            else
            {
                current.Refresh(data.Name, data.Description, data.MacAddress, data.IpAddress,
                    (InterfaceAdminStatus)data.AdminStatus, (InterfaceOperStatus)data.OperStatus,
                    data.SpeedBps, data.InterfaceType, timestamp);
            }
        }
    }

    internal static bool HasHealthEvidence(HealthData health)
        => health.LatencyMs.HasValue || health.AvailabilityPercent.HasValue
            || health.CpuPercent.HasValue || health.MemoryPercent.HasValue || health.UptimeSeconds.HasValue;

    private static void AddWirelessMetrics(List<MetricSampleInput> samples, Guid deviceId,
        IReadOnlyList<WirelessClientData> associations, DateTime timestamp)
    {
        foreach (var client in associations)
        {
            var resourceId = $"{deviceId}:{client.CpeMacAddress}";
            if (client.SignalDbm.HasValue) samples.Add(new("WirelessClient", resourceId, "signal", client.SignalDbm.Value, timestamp, "dBm"));
            if (client.NoiseDbm.HasValue) samples.Add(new("WirelessClient", resourceId, "noise", client.NoiseDbm.Value, timestamp, "dBm"));
            if (client.Snr.HasValue) samples.Add(new("WirelessClient", resourceId, "snr", client.Snr.Value, timestamp, "dB"));
            if (client.TxRateMbps.HasValue) samples.Add(new("WirelessClient", resourceId, "tx_rate", client.TxRateMbps.Value, timestamp, "Mbps"));
            if (client.RxRateMbps.HasValue) samples.Add(new("WirelessClient", resourceId, "rx_rate", client.RxRateMbps.Value, timestamp, "Mbps"));
        }
    }
}
