using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using AtlasNOC.Domain.Services.Interfaces;
using AtlasNOC.Domain.Enums;
using Microsoft.Extensions.Options;

namespace AtlasNOC.Domain.Services;

public sealed class PollingHostedService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<PollingHostedService> _logger;
    private readonly PollingOptions _options;

    public PollingHostedService(IServiceProvider serviceProvider, ILogger<PollingHostedService> logger,
        IOptions<PollingOptions> options)
    {
        _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_options.AutoStart)
        {
            _logger.LogInformation("Polling hosted service is disabled by configuration");
            return;
        }

        _logger.LogInformation("Polling hosted service starting with interval {IntervalSeconds}s and concurrency {MaxConcurrency}",
            _options.IntervalSeconds, _options.MaxConcurrency);
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(_options.IntervalSeconds));

        do
        {
            try
            {
                using var scope = _serviceProvider.CreateScope();
                var pollingService = scope.ServiceProvider.GetRequiredService<IPollingService>();
                var results = await pollingService.PollAllAsync(stoppingToken);
                var monitoring = scope.ServiceProvider.GetRequiredService<IMonitoringService>();
                foreach (var result in results)
                {
                    await monitoring.UpdateStateAsync(result.DeviceId,
                        new MonitoringState(result.DeviceId,
                            result.Success ? DeviceStatus.Up : DeviceStatus.Down,
                            result.PollTime, result.PollTime,
                            Array.Empty<ActiveThresholdViolation>(), result.Metrics), stoppingToken);
                }
                var metricHistory = scope.ServiceProvider.GetRequiredService<IMetricHistoryService>();
                await metricHistory.PruneAsync(DateTime.UtcNow.AddDays(-_options.RetentionDays), stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in polling loop");
            }

        }
        while (await timer.WaitForNextTickAsync(stoppingToken));

        _logger.LogInformation("Polling hosted service stopped");
    }
}
