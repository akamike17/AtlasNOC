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
            if (!AlertRule.SupportedOperators.Contains(rule.ComparisonOperator))
            {
                _logger.LogError("Regla {RuleId} omitida por operador inválido {Operator}", rule.Id, rule.ComparisonOperator);
                continue;
            }

            var resources = await _context.MetricSamples
                .Where(m => m.MetricName == rule.MetricName)
                .Select(m => new { m.ResourceType, m.ResourceId })
                .Distinct()
                .ToListAsync(ct);

            foreach (var resource in resources)
            {
                var samples = await _context.MetricSamples
                    .Where(m => m.MetricName == rule.MetricName
                        && m.ResourceType == resource.ResourceType && m.ResourceId == resource.ResourceId)
                    .OrderByDescending(m => m.TimestampUtc)
                    .ThenByDescending(m => m.Id)
                    .Take(rule.ConsecutiveFaults)
                    .ToListAsync(ct);
                if (samples.Count == 0) continue;
                var latest = samples[0];
                var latestTriggered = Compare(latest.ValueDouble, rule.ComparisonOperator, rule.Threshold);
                var consecutiveTriggered = HasConsecutiveFaults(samples.Select(s => s.ValueDouble),
                    rule.ConsecutiveFaults, rule.ComparisonOperator, rule.Threshold);

                var existing = await _context.Alerts
                    .FirstOrDefaultAsync(a => a.RuleId == rule.Id
                        && a.ResourceType == resource.ResourceType
                        && a.ResourceId == resource.ResourceId
                        && a.State != AlertState.Resolved, ct);

                if (consecutiveTriggered)
                {
                    if (existing is null)
                    {
                        var alert = new Alert(rule.Id, resource.ResourceType, resource.ResourceId,
                            rule.MetricName, latest.ValueDouble, rule.Threshold, rule.Severity,
                            $"Regla '{rule.Name}' disparada tras {rule.ConsecutiveFaults} fallos consecutivos: {latest.ValueDouble} {rule.ComparisonOperator} {rule.Threshold}");
                        await _alerts.AddAsync(alert, ct);
                        _logger.LogInformation("Alerta {Metric} en {Resource} ({Severity})",
                            rule.MetricName, resource.ResourceId, rule.Severity);
                    }
                    else
                    {
                        existing.Touch(latest.ValueDouble);
                        await _alerts.UpdateAsync(existing, ct);
                    }
                }
                else if (!latestTriggered && existing is not null)
                {
                    // Reconocimiento y recuperación son estados independientes:
                    // una alerta Acknowledged también se resuelve al normalizarse.
                    existing.Resolve("system");
                    await _alerts.UpdateAsync(existing, ct);
                }
            }
        }

        await _context.SaveChangesAsync(ct);
    }

    internal static bool HasConsecutiveFaults(IEnumerable<double> newestFirst, int required,
        string op, double threshold)
    {
        if (required < 1) throw new ArgumentOutOfRangeException(nameof(required));
        var values = newestFirst.Take(required).ToList();
        return values.Count == required && values.All(value => Compare(value, op, threshold));
    }

    internal static bool Compare(double value, string op, double threshold) => op switch
    {
        ">" => value > threshold,
        ">=" => value >= threshold,
        "<" => value < threshold,
        "<=" => value <= threshold,
        "==" => Math.Abs(value - threshold) < 0.0001,
        _ => throw new ArgumentException("Operador de comparación inválido.", nameof(op))
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
        var activeAlerts = await _context.Alerts
            .Where(a => a.State != AlertState.Resolved).AsNoTracking().ToListAsync(ct);
        var downDeviceIds = activeAlerts
            .Where(IsCompatibleDeviceDownAlert)
            .Select(a => Guid.TryParse(a.ResourceId, out var id) ? id : Guid.Empty)
            .Where(id => id != Guid.Empty)
            .ToHashSet();
        var devices = await _context.Devices.AsNoTracking().ToListAsync(ct);
        var interfaces = await _context.DeviceInterfaces.AsNoTracking().ToListAsync(ct);
        var links = await _context.NetworkLinks.AsNoTracking()
            .Where(link => link.IsConfirmed && !link.IsStale)
            .ToListAsync(ct);
        var graph = BuildDirectedGraph(devices, interfaces, links);

        var candidatesSeen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var downDeviceId in downDeviceIds)
        {
            ct.ThrowIfCancellationRequested();
            var device = devices.FirstOrDefault(d => d.Id.Value == downDeviceId);
            if (device is null) continue;
            var affected = FindDownstream(graph, downDeviceId)
                .Where(downDeviceIds.Contains)
                .ToList();
            // Sin orientación y sin una alerta compatible downstream no hay
            // evidencia para declarar siquiera un candidato.
            if (affected.Count == 0) continue;
            var downDevice = downDeviceId.ToString();
            candidatesSeen.Add(downDevice);
            var affectedNames = affected.Select(id =>
                devices.FirstOrDefault(d => d.Id.Value == id)?.Hostname ?? id.ToString()).ToList();
            var evidence = $"Candidato, no causa definitiva: {device.Hostname} está Down y " +
                $"{affected.Count} dependencias orientadas presentan alertas compatibles: {string.Join(", ", affectedNames)}. " +
                "Orientación basada únicamente en enlaces confirmados y jerarquía Core/Distribution/AccessPoint/CPE.";

            var incident = await _context.Incidents
                .FirstOrDefaultAsync(i => i.RootCauseDeviceId == downDevice && i.Status != IncidentStatus.Resolved, ct);

            if (incident is null)
            {
                incident = new Incident($"Posible causa raíz: {device.Hostname}", "system", evidence,
                    downDevice);
                incident.MarkRootCauseCandidate();
                _context.Incidents.Add(incident);
                _logger.LogInformation("Incidente con candidato de causa raíz {Device}; {Affected} dependencias con alerta",
                    downDevice, affected.Count);
            }
            else incident.RefreshEvidence(evidence);
        }

        // Si desaparece la evidencia correlacionada, resolver únicamente los
        // incidentes automáticos creados por este motor.
        var stale = await _context.Incidents
            .Where(i => i.CreatedBy == "system" && i.IsRootCauseCandidate
                && i.Status != IncidentStatus.Resolved && i.RootCauseDeviceId != null)
            .ToListAsync(ct);
        foreach (var incident in stale.Where(i => !candidatesSeen.Contains(i.RootCauseDeviceId!)))
            incident.Resolve("system");

        await _context.SaveChangesAsync(ct);
    }

    internal static bool IsCompatibleDeviceDownAlert(Alert alert)
        => alert.ResourceType.Equals("Device", StringComparison.OrdinalIgnoreCase)
            && alert.MetricName.Equals("availability", StringComparison.OrdinalIgnoreCase)
            && alert.Value <= 0;

    internal static (Guid Upstream, Guid Downstream)? Orient(Guid leftId, DeviceType leftType,
        Guid rightId, DeviceType rightType)
    {
        var leftRank = HierarchyRank(leftType);
        var rightRank = HierarchyRank(rightType);
        if (!leftRank.HasValue || !rightRank.HasValue || leftRank == rightRank) return null;
        return leftRank < rightRank ? (leftId, rightId) : (rightId, leftId);
    }

    internal static IReadOnlyDictionary<Guid, HashSet<Guid>> BuildDirectedGraph(
        IReadOnlyList<Device> devices, IReadOnlyList<DeviceInterface> interfaces,
        IReadOnlyList<NetworkLink> links)
    {
        var deviceById = devices.ToDictionary(device => device.Id.Value);
        var deviceByInterface = interfaces.ToDictionary(iface => iface.Id.Value, iface => iface.DeviceId.Value);
        var graph = devices.ToDictionary(device => device.Id.Value, _ => new HashSet<Guid>());
        foreach (var link in links.Where(link => link.IsConfirmed && !link.IsStale))
        {
            if (!deviceByInterface.TryGetValue(link.AInterfaceId.Value, out var leftId)
                || !deviceByInterface.TryGetValue(link.BInterfaceId.Value, out var rightId)
                || !deviceById.TryGetValue(leftId, out var left)
                || !deviceById.TryGetValue(rightId, out var right)) continue;
            var orientation = Orient(leftId, left.DeviceType, rightId, right.DeviceType);
            if (orientation.HasValue)
                graph[orientation.Value.Upstream].Add(orientation.Value.Downstream);
        }
        return graph;
    }

    internal static IReadOnlySet<Guid> FindDownstream(
        IReadOnlyDictionary<Guid, HashSet<Guid>> graph, Guid root)
    {
        var visited = new HashSet<Guid>();
        var pending = new Queue<Guid>();
        if (graph.TryGetValue(root, out var direct))
            foreach (var id in direct) pending.Enqueue(id);
        while (pending.TryDequeue(out var current))
        {
            if (!visited.Add(current)) continue;
            if (graph.TryGetValue(current, out var children))
                foreach (var child in children) pending.Enqueue(child);
        }
        return visited;
    }

    private static int? HierarchyRank(DeviceType type) => type switch
    {
        DeviceType.Core => 0,
        DeviceType.Distribution => 1,
        DeviceType.AccessPoint => 2,
        DeviceType.Cpe => 3,
        _ => null
    };
}
