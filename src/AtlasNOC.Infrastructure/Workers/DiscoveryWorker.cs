using AtlasNOC.Application.Services;
using AtlasNOC.Domain.Enums;
using AtlasNOC.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace AtlasNOC.Infrastructure.Workers;

/// <summary>Worker de descubrimiento: ejecuta DiscoveryRuns pendientes en segundo plano.</summary>
public class DiscoveryWorker : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<DiscoveryWorker> _logger;

    public DiscoveryWorker(IServiceScopeFactory scopeFactory, ILogger<DiscoveryWorker> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("DiscoveryWorker iniciado.");
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<AtlasNOCDbContext>();
                var executor = scope.ServiceProvider.GetRequiredService<IDiscoveryExecutor>();

                var pending = await db.DiscoveryRuns
                    .Where(r => r.Status == DiscoveryRunStatus.Pending || r.Status == DiscoveryRunStatus.Running)
                    .OrderBy(r => r.StartedAtUtc)
                    .Take(5)
                    .ToListAsync(stoppingToken);

                foreach (var run in pending)
                {
                    try { await executor.ExecuteAsync(run.Id, stoppingToken); }
                    catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
                    catch (Exception ex) { _logger.LogError(ex, "Error ejecutando DiscoveryRun {Id}", run.Id); }
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
            catch (Exception ex) { _logger.LogError(ex, "Error en DiscoveryWorker"); }

            try { await Task.Delay(TimeSpan.FromSeconds(10), stoppingToken); }
            catch (OperationCanceledException) { break; }
        }
    }
}