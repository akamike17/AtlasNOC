using AtlasNOC.Application.Dtos;
using AtlasNOC.Application.Services;
using AtlasNOC.Domain.Entities;
using AtlasNOC.Domain.Enums;
using AtlasNOC.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace AtlasNOC.Infrastructure.Services;

public class AlertRuleService : IAlertRuleService
{
    private readonly AtlasNOCDbContext _context;

    public AlertRuleService(AtlasNOCDbContext context) => _context = context;

    public async Task<IReadOnlyList<AlertRuleDto>> ListRulesAsync(CancellationToken ct = default)
        => (await _context.AlertRules.AsNoTracking().OrderBy(r => r.Name).ToListAsync(ct))
            .Select(r => new AlertRuleDto(r.Id, r.Name, r.MetricName, r.ComparisonOperator,
                r.Threshold, (int)r.Severity, r.ConsecutiveFaults, r.IsEnabled))
            .ToList();

    public async Task<Guid> CreateRuleAsync(CreateAlertRuleRequest request, CancellationToken ct = default)
    {
        var rule = new AlertRule(request.Name, request.MetricName, request.ComparisonOperator,
            request.Threshold, (AlertSeverity)request.Severity, request.ConsecutiveFaults);
        _context.AlertRules.Add(rule);
        await _context.SaveChangesAsync(ct);
        return rule.Id;
    }

    public async Task ToggleRuleAsync(Guid id, bool enabled, CancellationToken ct = default)
    {
        var rule = await _context.AlertRules.FirstOrDefaultAsync(r => r.Id == id, ct);
        if (rule is null) return;
        rule.SetEnabled(enabled);
        await _context.SaveChangesAsync(ct);
    }
}

/// <summary>Notificación: cola ligera en base de datos (NotificationQueue tabla/fila).</summary>
public class NotificationService : INotificationService
{
    private readonly AtlasNOCDbContext _context;

    public NotificationService(AtlasNOCDbContext context) => _context = context;

    public Task<IReadOnlyList<PendingNotification>> DequeueDueAsync(CancellationToken ct = default)
        // Sin cola persistida todavía: notificaciones se generan en el worker al vuelo.
        => Task.FromResult<IReadOnlyList<PendingNotification>>(Array.Empty<PendingNotification>());

    public Task MarkSentAsync(Guid notificationId, CancellationToken ct = default) => Task.CompletedTask;
    public Task MarkFailedAsync(Guid notificationId, string error, CancellationToken ct = default) => Task.CompletedTask;
}