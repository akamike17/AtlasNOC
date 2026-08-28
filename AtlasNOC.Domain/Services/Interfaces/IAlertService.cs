using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using AtlasNOC.Domain.Entities;
using AtlasNOC.Domain.Enums;
using AtlasNOC.Domain.ValueObjects;

namespace AtlasNOC.Domain.Services.Interfaces;

public interface IAlertService
{
    Task<Alert> CreateAsync(CreateAlertRequest request, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Alert>> GetAllAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Alert>> GetActiveAlertsAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Alert>> GetAlertsForDeviceAsync(DeviceId deviceId,
        CancellationToken cancellationToken = default);
    Task<Alert?> GetByIdAsync(AlertId id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Alert>> GetCriticalAlertsAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Alert>> GetUnacknowledgedAlertsAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Alert>> GetRecentAlertsAsync(int count,
        CancellationToken cancellationToken = default);
    Task<Alert> AcknowledgeAsync(AlertId alertId, string acknowledgedBy,
        CancellationToken cancellationToken = default);
    Task<Alert> ResolveAsync(AlertId alertId, string resolvedBy, string? notes = null,
        CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Alert>> GroupByDeviceAsync(IReadOnlyList<Alert> alerts);
}

public sealed record CreateAlertRequest
(
    DeviceId DeviceId,
    string Title,
    AlertSeverity Severity,
    string Source,
    IDictionary<string, object>? Context
);
