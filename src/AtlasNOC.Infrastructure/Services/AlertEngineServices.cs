using AtlasNOC.Application.Dtos;
using AtlasNOC.Application.Repositories;
using AtlasNOC.Application.Services;
using AtlasNOC.Domain.Entities;
using AtlasNOC.Domain.Enums;
using AtlasNOC.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace AtlasNOC.Infrastructure.Services;

/// <summary>
/// Evaluación de alertas: recorre reglas habilitadas, compara la última muestra de cada recurso,
/// levanta alertas (o las resuelve cuando la condición deja de cumplirse) y dispara incidentes por severidad.
/// </summary>
public class AlertEvaluationEngine : IAlertEvaluationEngine
{
    private readonly AtlasNOCDbContext _context;
    private readonly IAlertRepository _alerts;
    private readonly ILogger<AlertEvaluationEngine> _logger;

    public AlertEvaluationEngine(AtlasNOCDbContext context, IAlertRepository alerts, ILogger<AlertEvaluationEngine> logger)
    {
        _context = context;
        _alerts = alerts;
        _logger = logger;
    }

    public async Task EvaluateAllAsync(CancellationToken ct = default)
    {
        var rules = await _context.AlertRules.Where(r => r.IsEnabled).ToListAsync(ct);
        if (rules.Count == 0) return;

        foreach (var rule in rules)
        {
            ct.ThrowIfCancellationRequested();
            var latest = await _context.MetricSamples
                .Where(m => m.MetricName == rule.MetricName)
                .OrderByDescending(m => m.TimestampUtc)
                .Take(500)
                .ToListAsync(ct);

            // Agrupar por recurso y tomar la última muestra.
            var latestPerResource = latest
                .GroupBy(m => (m.ResourceType, m.ResourceId))
                .Select(g => g.First())
                .ToList();

            foreach (var sample in latestPerResource)
            {
                var triggered = Compare(sample.ValueDouble, rule.ComparisonOperator, rule.Threshold);

                var existing = await _context.Alerts
                    .FirstOrDefaultAsync(a => a.MetricName == rule.MetricName
                        && a.ResourceType == sample.ResourceType
                        && a.ResourceId == sample.ResourceId
                        && a.State != AlertState.Resolved, ct);

                if (triggered)
                {
                    if (existing is null)
                    {
                        var alert = new Alert(rule.Id, sample.ResourceType, sample.ResourceId,
                            rule.MetricName, sample.ValueDouble, rule.Threshold, rule.Severity,
                            $"Regla '{rule.Name}' disparada: {sample.ValueDouble} {rule.ComparisonOperator} {rule.Threshold}");
                        await _alerts.AddAsync(alert, ct);
                        _logger.LogInformation("Alerta {Metric} en {Resource} ({Severity})",
                            rule.MetricName, sample.ResourceId, rule.Severity);
                    }
                    else
                    {
                        existing.Touch(sample.ValueDouble);
                        await _alerts.UpdateAsync(existing, ct);
                    }
                }
                else if (existing is not null && existing.State != AlertState.Acknowledged)
                {
                    existing.Resolve("system");
                    await _alerts.UpdateAsync(existing, ct);
                }
            }
        }

        await _context.SaveChangesAsync(ct);
    }

    private static bool Compare(double value, string op, double threshold) => op switch
    {
        ">" => value > threshold,
        ">=" => value >= threshold,
        "<" => value < threshold,
        "<=" => value <= threshold,
        "==" => Math.Abs(value - threshold) < 0.0001,
        _ => value > threshold
    };
}

public class IncidentCorrelationEngine : IIncidentCorrelationEngine
{
    private readonly AtlasNOCDbContext _context;
    private readonly ILogger<IncidentCorrelationEngine> _logger;

    public IncidentCorrelationEngine(AtlasNOCDbContext context, ILogger<IncidentCorrelationEngine> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task CorrelateAsync(CancellationToken ct = default)
    {
        var openAlerts = await _context.Alerts.Where(a => a.State == AlertState.Open).ToListAsync(ct);
        if (openAlerts.Count == 0) return;

        // Agrupar por downstream dependency: dispositivos Down son causa raíz candidata;
        // los enlaces que dependen de ellos (BInterface/AInterface) heredan su severidad.
        // Regla: un dispositivo caído que es upstream de otros enlaces en Down/Unreachable es causa raíz.
        var downDevices = openAlerts
            .Where(a => a.ResourceType == "Device" && a.MetricName == "availability" && a.Value <= 0)
            .Select(a => a.ResourceId)
            .ToHashSet();

        foreach (var downDevice in downDevices)
        {
            ct.ThrowIfCancellationRequested();

            // Interfaces del dispositivo caído (upstream y downstream).
            var deviceId = AtlasNOC.Domain.ValueObjects.DeviceId.From(Guid.Parse(downDevice));
            var ifaceIds = await _context.DeviceInterfaces
                .Where(i => i.DeviceId == deviceId)
                .Select(i => i.Id)
                .ToListAsync(ct);

            var downstreamCount = await _context.NetworkLinks
                .CountAsync(l => ifaceIds.Contains(l.AInterfaceId) || ifaceIds.Contains(l.BInterfaceId), ct);

            var incident = await _context.Incidents
                .FirstOrDefaultAsync(i => i.RootCauseDeviceId == downDevice && i.Status != IncidentStatus.Resolved, ct);

            if (incident is null)
            {
                incident = new Incident($"Dispositivo {downDevice} caído", "system",
                    $"Indisponibilidad detectada; {downstreamCount} enlaces conectados potencialmente afectados.",
                    downDevice);
                incident.MarkRootCauseCandidate();
                _context.Incidents.Add(incident);
                _logger.LogInformation("Incidente por causa raíz: {Device} ({Connected} enlaces)",
                    downDevice, downstreamCount);
            }
        }

        await _context.SaveChangesAsync(ct);
    }
}