using AtlasNOC.Application.Services;
using AtlasNOC.Domain.Enums;
using AtlasNOC.Application.Repositories;
using AtlasNOC.Infrastructure.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AtlasNOC.Infrastructure.Workers;

/// <summary>Worker de descubrimiento: ejecuta DiscoveryRuns pendientes en segundo plano.</summary>
public class DiscoveryWorker : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<DiscoveryWorker> _logger;
    private readonly DiscoveryOptions _options;
    private readonly string _workerId = $"{Environment.MachineName}:{Environment.ProcessId}:{Guid.NewGuid():N}";

    public DiscoveryWorker(IServiceScopeFactory scopeFactory, ILogger<DiscoveryWorker> logger,
        IOptions<DiscoveryOptions> options)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
        _options = options.Value;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("DiscoveryWorker iniciado.");
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var runs = scope.ServiceProvider.GetRequiredService<IDiscoveryRunRepository>();
                var executor = scope.ServiceProvider.GetRequiredService<IDiscoveryExecutor>();
                var lease = TimeSpan.FromSeconds(Math.Max(10, _options.LeaseSeconds));
                var run = await runs.ClaimNextAsync(_workerId, DateTime.UtcNow, lease, stoppingToken);
                if (run is not null)
                {
                    using var executionCts = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);
                    using var heartbeatCts = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);
                    var heartbeat = RenewLeaseAsync(run.Id, lease, executionCts, heartbeatCts.Token);
                    try { await executor.ExecuteAsync(run.Id, executionCts.Token); }
                    catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
                    catch (OperationCanceledException) { _logger.LogInformation("DiscoveryRun {Id} cancelado", run.Id); }
                    catch (Exception ex) { _logger.LogError(ex, "Error ejecutando DiscoveryRun {Id}", run.Id); }
                    finally
                    {
                        heartbeatCts.Cancel();
                        try { await heartbeat; } catch (OperationCanceledException) { }
                    }
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
            catch (Exception ex) { _logger.LogError(ex, "Error en DiscoveryWorker"); }

            try { await Task.Delay(TimeSpan.FromSeconds(10), stoppingToken); }
            catch (OperationCanceledException) { break; }
        }
    }

    private async Task RenewLeaseAsync(Guid runId, TimeSpan lease,
        CancellationTokenSource executionCts, CancellationToken ct)
    {
        var interval = TimeSpan.FromSeconds(Math.Clamp(_options.LeaseRenewalSeconds, 1, Math.Max(1, _options.LeaseSeconds - 1)));
        while (!ct.IsCancellationRequested)
        {
            await Task.Delay(interval, ct);
            await using var scope = _scopeFactory.CreateAsyncScope();
            var runs = scope.ServiceProvider.GetRequiredService<IDiscoveryRunRepository>();
            if (!await runs.RenewLeaseAsync(runId, _workerId, DateTime.UtcNow, lease, ct))
            {
                // Cancelled/completada o lease ya no pertenece a esta instancia.
                executionCts.Cancel();
                return;
            }
        }
    }
}
