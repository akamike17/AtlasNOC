using AtlasNOC.Application.Dtos;

namespace AtlasNOC.Application.Services;

/// <summary>Evalúa reglas de alerta contra el estado/métricas actuales y levanta o resuelve alertas.</summary>
public interface IAlertEvaluationEngine
{
    Task EvaluateAllAsync(CancellationToken ct = default);
}

/// <summary>Correlaciona alertas abiertas en incidentes y marca candidatos a causa raíz por dependencias.</summary>
public interface IIncidentCorrelationEngine
{
    Task CorrelateAsync(CancellationToken ct = default);
}

/// <summary>Gestión de reglas de alerta (CRUD).</summary>
public interface IAlertRuleService
{
    Task<IReadOnlyList<AlertRuleDto>> ListRulesAsync(CancellationToken ct = default);
    Task<Guid> CreateRuleAsync(CreateAlertRuleRequest request, CancellationToken ct = default);
    Task ToggleRuleAsync(Guid id, bool enabled, CancellationToken ct = default);
}

/// <summary>Notificación saliente de una alerta o incidente.</summary>
public interface INotificationService
{
    Task<IReadOnlyList<PendingNotification>> DequeueDueAsync(CancellationToken ct = default);
    Task MarkSentAsync(Guid notificationId, CancellationToken ct = default);
    Task MarkFailedAsync(Guid notificationId, string error, CancellationToken ct = default);
}

public sealed record PendingNotification(Guid Id, int ChannelType, string Recipient, string Subject, string Body);