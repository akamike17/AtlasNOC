using AtlasNOC.Application.Repositories;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace AtlasNOC.Infrastructure.Workers;

/// <summary>Worker de retención de métricas: purga muestras más antiguas que la ventana configurada.</summary>
public class MetricRetentionWorker : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<MetricRetentionWorker> _logger;
    private static readonly TimeSpan DefaultRetention = TimeSpan.FromDays(30);

    public MetricRetentionWorker(IServiceScopeFactory scopeFactory, ILogger<MetricRetentionWorker> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var metrics = scope.ServiceProvider.GetRequiredService<IMetricRepository>();
                var cutoff = DateTime.UtcNow.Subtract(DefaultRetention);
                var purged = await metrics.PurgeOlderThanAsync(cutoff, stoppingToken);
                if (purged > 0)
                    _logger.LogInformation("Métricas purgadas: {Purged} muestras anteriores a {Cutoff:u}", purged, cutoff);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
            catch (Exception ex) { _logger.LogError(ex, "Error en MetricRetentionWorker"); }

            try { await Task.Delay(TimeSpan.FromHours(6), stoppingToken); }
            catch (OperationCanceledException) { break; }
        }
    }
}