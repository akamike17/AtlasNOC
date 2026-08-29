using AtlasNOC.Application.Devices;
using AtlasNOC.Application.Probes;
using AtlasNOC.Application.Repositories;
using AtlasNOC.Application.Services;
using AtlasNOC.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

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

    public PollingService(IDeviceRepository devices, IIcmpProbe icmp,
        IDeviceDriverRegistry drivers, IMetricWriter metricWriter,
        AtlasNOCDbContext context, ILogger<PollingService> logger)
    {
        _devices = devices;
        _icmp = icmp;
        _drivers = drivers;
        _metricWriter = metricWriter;
        _context = context;
        _logger = logger;
    }

    public async Task PollAllManagedAsync(CancellationToken ct = default)
    {
        var devices = await _devices.ListManagedAsync(ct);
        foreach (var device in devices)
        {
            ct.ThrowIfCancellationRequested();
            try
            {
                await PollDeviceAsync(device.Id.Value, ct);
            }
            catch (OperationCanceledException) { throw; }
            catch (Exception ex)
            {
                // Fallos de un dispositivo no detienen el ciclo completo.
                _logger.LogError(ex, "Fallo de polling en dispositivo {Hostname}", device.Hostname);
            }
        }
    }

    public async Task PollDeviceAsync(Guid deviceId, CancellationToken ct = default)
    {
        var device = await _devices.GetByIdAsync(deviceId, ct);
        if (device is null || !device.IsManaged) return;

        var ip = device.ManagementIp;
        var samples = new List<MetricSampleInput>();
        var timestamp = DateTime.UtcNow;

        var ping = await _icmp.PingAsync(ip, 2000, ct);
        if (ping.Success)
        {
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

        // Adquisición por driver (health + métricas).
        IDeviceDriver driver;
        try
        {
            driver = _drivers.Resolve(new DeviceFingerprint(ip, device.Hostname, null, null, device.Vendor.ToString().ToLowerInvariant()));
        }
        catch
        {
            driver = _drivers.Resolve(new DeviceFingerprint(ip, device.Hostname, null, null, "generic"));
        }

        try
        {
            var health = await driver.GetHealthAsync(ip, ct);
            if (ping.Success)
            {
                samples.Add(new MetricSampleInput("Device", deviceId.ToString(), "cpu_usage", health.CpuPercent ?? 0, timestamp, "%"));
                samples.Add(new MetricSampleInput("Device", deviceId.ToString(), "memory_usage", health.MemoryPercent ?? 0, timestamp, "%"));
            }
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Sin health para {Hostname}", device.Hostname);
        }

        device.MarkPolled(timestamp);
        await _devices.UpdateAsync(device, ct);

        if (samples.Count > 0)
            await _metricWriter.WriteAsync(samples, ct);
    }
}