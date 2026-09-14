using AtlasNOC.Application.Wisp;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace AtlasNOC.Infrastructure.Workers;

public sealed class WispObservationWorker(
    IServiceScopeFactory scopeFactory,
    ILogger<WispObservationWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await RunOnceAsync(stoppingToken);
        using var timer = new PeriodicTimer(TimeSpan.FromMinutes(5));
        while (await timer.WaitForNextTickAsync(stoppingToken))
            await RunOnceAsync(stoppingToken);
    }

    private async Task RunOnceAsync(CancellationToken ct)
    {
        using var scope = scopeFactory.CreateScope();
        var registry = scope.ServiceProvider.GetRequiredService<IWispConnectorRegistry>();
        var store = scope.ServiceProvider.GetRequiredService<IWispObservationService>();
        foreach (var descriptor in registry.List())
        {
            ct.ThrowIfCancellationRequested();
            var connector = registry.Resolve(descriptor.Key);
            if (connector is null) continue;
            try
            {
                var evidence = await connector.ReadClientsAsync(ct);
                var saved = await store.RecordAsync(evidence, ct);
                logger.LogInformation("WISP connector {Connector}: observed={Observed} persisted={Persisted}",
                    descriptor.Key, evidence.Count, saved);
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "WISP connector {Connector}: read failed", descriptor.Key);
            }
        }
    }
}
