using AtlasNOC.Application.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using AtlasNOC.Infrastructure.Services;

namespace AtlasNOC.Infrastructure.Workers;

/// <summary>Worker de polling: ejecuta el ciclo de polling en segundo plano sin bloquear la UI.</summary>
public class PollingWorker : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<PollingWorker> _logger;
    private readonly PollingOptions _options;

    public PollingWorker(IServiceScopeFactory scopeFactory, ILogger<PollingWorker> logger,
        IOptions<PollingOptions> options)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
        _options = options.Value;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("PollingWorker iniciado.");
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var svc = scope.ServiceProvider.GetRequiredService<IPollingService>();
                await svc.PollAllManagedAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error en ciclo de polling");
            }

            try { await Task.Delay(TimeSpan.FromSeconds(Math.Max(1, _options.SchedulerTickSeconds)), stoppingToken); }
            catch (OperationCanceledException) { break; }
        }
    }
}
