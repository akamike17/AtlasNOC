using System.Net.Http.Json;
using System.Text.Json;
using AtlasNOC.Domain.Entities;
using AtlasNOC.Domain.Enums;
using AtlasNOC.Infrastructure.Persistence;
using AtlasNOC.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AtlasNOC.Infrastructure.Workers;

/// <summary>Procesa entregas persistentes; nunca deduce éxito sin envío real.</summary>
public sealed class NotificationWorker : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<NotificationWorker> _logger;
    private readonly NotificationOptions _options;

    public NotificationWorker(IServiceScopeFactory scopeFactory, IHttpClientFactory httpClientFactory,
        ILogger<NotificationWorker> logger, IOptions<NotificationOptions> options)
    {
        _scopeFactory = scopeFactory;
        _httpClientFactory = httpClientFactory;
        _logger = logger;
        _options = options.Value;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try { await ProcessBatchAsync(stoppingToken); }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
            catch (Exception ex) { _logger.LogError(ex, "Error en NotificationWorker"); }

            try { await Task.Delay(TimeSpan.FromSeconds(Math.Max(1, _options.WorkerIntervalSeconds)), stoppingToken); }
            catch (OperationCanceledException) { break; }
        }
    }

    internal async Task ProcessBatchAsync(CancellationToken ct)
    {
        await using var scope = _scopeFactory.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AtlasNOCDbContext>();
        await CreateMissingDeliveriesAsync(db, ct);

        var now = DateTime.UtcNow;
        var abandonedBefore = now.AddMinutes(-Math.Max(1, _options.SendingLeaseMinutes));
        var candidates = await db.NotificationDeliveries.AsNoTracking()
            .Where(d => d.State == NotificationDeliveryState.Pending
                || (d.State == NotificationDeliveryState.Failed && d.NextRetryUtc <= now)
                || (d.State == NotificationDeliveryState.Sending && d.LastAttemptUtc <= abandonedBefore))
            .OrderBy(d => d.NextRetryUtc).ThenBy(d => d.Id)
            .Select(d => d.Id)
            .Take(Math.Max(1, _options.BatchSize))
            .ToListAsync(ct);

        foreach (var id in candidates)
        {
            ct.ThrowIfCancellationRequested();
            var claimed = await db.NotificationDeliveries
                .Where(d => d.Id == id && (d.State == NotificationDeliveryState.Pending
                    || (d.State == NotificationDeliveryState.Failed && d.NextRetryUtc <= now)
                    || (d.State == NotificationDeliveryState.Sending && d.LastAttemptUtc <= abandonedBefore)))
                .ExecuteUpdateAsync(setters => setters
                    .SetProperty(d => d.State, NotificationDeliveryState.Sending)
                    .SetProperty(d => d.AttemptCount, d => d.AttemptCount + 1)
                    .SetProperty(d => d.LastAttemptUtc, now)
                    .SetProperty(d => d.NextRetryUtc, (DateTime?)null)
                    .SetProperty(d => d.LastError, (string?)null), ct);
            if (claimed != 1) continue;

            var delivery = await db.NotificationDeliveries.FirstAsync(d => d.Id == id, ct);
            var alert = await db.Alerts.FirstAsync(a => a.Id == delivery.AlertId, ct);
            var channel = await db.NotificationChannels.AsNoTracking().FirstAsync(c => c.Id == delivery.ChannelId, ct);
            try
            {
                if (channel.Type != NotificationChannelType.Webhook)
                {
                    delivery.MarkUnsupported(channel.Type);
                    _logger.LogWarning("Entrega {Delivery} no enviada: tipo {Type} no soportado", id, channel.Type);
                }
                else
                {
                    await SendWebhookAsync(channel, alert, ct);
                    delivery.MarkSent(DateTime.UtcNow);
                    alert.MarkNotified();
                }
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
            catch (Exception ex)
            {
                delivery.MarkFailed(ex.Message, DateTime.UtcNow, Math.Max(1, _options.MaxAttempts));
                _logger.LogWarning(ex, "Fallo entrega {Delivery} para alerta {Alert}", id, alert.Id.Value);
            }
            await db.SaveChangesAsync(ct);
            db.ChangeTracker.Clear();
        }
    }

    private static Task CreateMissingDeliveriesAsync(AtlasNOCDbContext db, CancellationToken ct)
        => db.Database.ExecuteSqlRawAsync($@"
INSERT IGNORE INTO NotificationDeliveries (Id, AlertId, ChannelId, State, AttemptCount)
SELECT UUID(), alert.Id, channel.Id, {(int)NotificationDeliveryState.Pending}, 0
FROM Alerts AS alert
CROSS JOIN NotificationChannels AS channel
LEFT JOIN NotificationDeliveries AS delivery
  ON delivery.AlertId = alert.Id AND delivery.ChannelId = channel.Id
WHERE alert.State <> {(int)AlertState.Resolved}
  AND alert.Severity IN ({(int)AlertSeverity.High}, {(int)AlertSeverity.Critical})
  AND channel.IsEnabled = 1
  AND delivery.Id IS NULL;", ct);

    private async Task SendWebhookAsync(NotificationChannel channel, Alert alert, CancellationToken ct)
    {
        var uri = ParseWebhookUri(channel.ConfigurationJson);
        var payload = new
        {
            title = $"[AtlasNOC] Alerta {alert.Severity}", alertId = alert.Id.Value,
            alert.ResourceType, alert.ResourceId, metric = alert.MetricName, alert.Value, alert.Threshold
        };
        var client = _httpClientFactory.CreateClient("notifications");
        using var response = await client.PostAsJsonAsync(uri, payload, ct);
        response.EnsureSuccessStatusCode();
    }

    internal static Uri ParseWebhookUri(string configuration)
    {
        string? value = configuration;
        if (configuration.TrimStart().StartsWith('{'))
        {
            using var json = JsonDocument.Parse(configuration);
            value = json.RootElement.TryGetProperty("url", out var url) ? url.GetString() : null;
        }
        if (!Uri.TryCreate(value, UriKind.Absolute, out var uri)
            || (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
            throw new InvalidOperationException("Webhook URL inválida o ausente.");
        return uri;
    }
}
