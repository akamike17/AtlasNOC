using System.Net.Http.Json;
using AtlasNOC.Application.Services;
using AtlasNOC.Domain.Entities;
using AtlasNOC.Domain.Enums;
using AtlasNOC.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace AtlasNOC.Infrastructure.Workers;

/// <summary>
/// Worker de notificación: genera notificaciones salientes (webhook/email) para alertas críticas
/// no notificadas. Implementación base: webhook genérico; los canales se amplían por configuración.
/// </summary>
public class NotificationWorker : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<NotificationWorker> _logger;

    public NotificationWorker(IServiceScopeFactory scopeFactory, ILogger<NotificationWorker> logger)
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
                var db = scope.ServiceProvider.GetRequiredService<AtlasNOCDbContext>();

                // Alertas críticas abiertas (severidad High/Critical) aún sin notificar.
                var pending = await db.Alerts
                    .Where(a => a.State == AlertState.Open
                        && (a.Severity == AlertSeverity.High || a.Severity == AlertSeverity.Critical)
                        && a.NotificationSentAtUtc == null)
                    .Take(50)
                    .ToListAsync(stoppingToken);

                var channels = await db.NotificationChannels.Where(c => c.IsEnabled).ToListAsync(stoppingToken);

                foreach (var alert in pending)
                {
                    foreach (var channel in channels)
                    {
                        try
                        {
                            await DispatchAsync(channel, alert);
                            alert.MarkNotified();
                        }
                        catch (Exception ex)
                        {
                            _logger.LogWarning(ex, "Fallo al notificar alerta {Alert} vía {Channel}",
                                alert.Id.Value, channel.Name);
                        }
                    }
                }

                if (pending.Count > 0)
                    await db.SaveChangesAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
            catch (Exception ex) { _logger.LogError(ex, "Error en NotificationWorker"); }

            try { await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken); }
            catch (OperationCanceledException) { break; }
        }
    }

    private async Task DispatchAsync(NotificationChannel channel, Alert alert)
    {
        using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(10) };
        var payload = new
        {
            title = $"[AtlasNOC] Alerta {alert.Severity}",
            alertId = alert.Id.Value,
            resourceType = alert.ResourceType,
            resourceId = alert.ResourceId,
            metric = alert.MetricName,
            value = alert.Value,
            threshold = alert.Threshold
        };

        if (channel.Type == NotificationChannelType.Webhook)
            {
                await http.PostAsJsonAsync(channel.ConfigurationJson, payload);
            }
        // Email/Slack/PagerDuty: extensión; aquí se registra el intento sin envío real.
    }
}