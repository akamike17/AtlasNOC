using AtlasNOC.Application.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace AtlasNOC.Infrastructure.Workers;

/// <summary>Worker de evaluación de alertas: ejecuta el motor de reglas contra las métricas recientes.</summary>
public class AlertEvaluationWorker : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<AlertEvaluationWorker> _logger;

    public AlertEvaluationWorker(IServiceScopeFactory scopeFactory, ILogger<AlertEvaluationWorker> logger)
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
                var engine = scope.ServiceProvider.GetRequiredService<IAlertEvaluationEngine>();
                await engine.EvaluateAllAsync(stoppingToken);

                var correlator = scope.ServiceProvider.GetRequiredService<IIncidentCorrelationEngine>();
                await correlator.CorrelateAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
            catch (Exception ex) { _logger.LogError(ex, "Error en AlertEvaluationWorker"); }

            try { await Task.Delay(TimeSpan.FromSeconds(60), stoppingToken); }
            catch (OperationCanceledException) { break; }
        }
    }
}