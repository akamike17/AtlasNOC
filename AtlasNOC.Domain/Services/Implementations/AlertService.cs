using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using AtlasNOC.Domain.Entities;
using AtlasNOC.Domain.Enums;
using AtlasNOC.Domain.Services.Interfaces;
using AtlasNOC.Domain.ValueObjects;

namespace AtlasNOC.Domain.Services;

public class AlertService : IAlertService
{
    private readonly IRepository<Alert> _repository;
    private readonly IAuditService _auditService;

    public AlertService(IRepository<Alert> repository, IAuditService auditService)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        _auditService = auditService ?? throw new ArgumentNullException(nameof(auditService));
    }

    public async Task<Alert> CreateAsync(CreateAlertRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(request.Title);
        var existingAlerts = await _repository.GetAllAsync(cancellationToken);
        var duplicate = existingAlerts.FirstOrDefault(alert =>
            alert.IsActive &&
            alert.DeviceId == request.DeviceId &&
            string.Equals(alert.Message, request.Title, StringComparison.OrdinalIgnoreCase));
        if (duplicate is not null)
        {
            return duplicate;
        }

        var alert = Alert.Create(request.DeviceId, request.Title, request.Severity);
        await _repository.AddAsync(alert, cancellationToken);
        await _auditService.LogSuccessAsync("Alert", "Create", "System",
            targetResource: alert.Id.Value.ToString(), targetResourceType: nameof(Alert),
            newValue: $"Device={request.DeviceId}, Severity={request.Severity}, Message={request.Title}, Source={request.Source}",
            cancellationToken: cancellationToken);
        return alert;
    }

    public async Task<IReadOnlyList<Alert>> GetActiveAlertsAsync(
        CancellationToken cancellationToken = default)
    {
        var all = await _repository.GetAllAsync(cancellationToken);
        return all.Where(a => a.IsActive).ToList().AsReadOnly();
    }

    public async Task<IReadOnlyList<Alert>> GetAlertsForDeviceAsync(DeviceId deviceId,
        CancellationToken cancellationToken = default)
    {
        var all = await _repository.GetAllAsync(cancellationToken);
        return all.Where(a => a.DeviceId == deviceId).ToList().AsReadOnly();
    }

    public Task<Alert?> GetByIdAsync(AlertId id, CancellationToken cancellationToken = default)
        => _repository.GetByIdAsync(id.Value, cancellationToken);

    public async Task<IReadOnlyList<Alert>> GetCriticalAlertsAsync(
        CancellationToken cancellationToken = default)
    {
        var all = await _repository.GetAllAsync(cancellationToken);
        return all.Where(a => a.Severity >= AlertSeverity.High && a.IsActive)
            .OrderByDescending(a => a.OccurredAt).ToList().AsReadOnly();
    }

    public async Task<IReadOnlyList<Alert>> GetUnacknowledgedAlertsAsync(
        CancellationToken cancellationToken = default)
    {
        var all = await _repository.GetAllAsync(cancellationToken);
        return all.Where(a => a.IsActive && a.AcknowledgedAt == null)
            .OrderByDescending(a => a.OccurredAt).ToList().AsReadOnly();
    }

    public Task<IReadOnlyList<Alert>> GetAllAsync(CancellationToken cancellationToken = default)
        => _repository.GetAllAsync(cancellationToken);

    public async Task<IReadOnlyList<Alert>> GetRecentAlertsAsync(int count,
        CancellationToken cancellationToken = default)
    {
        if (count is < 1 or > 500)
            throw new ArgumentOutOfRangeException(nameof(count), "Count must be between 1 and 500.");
        var all = await _repository.GetAllAsync(cancellationToken);
        return all.OrderByDescending(a => a.OccurredAt).Take(count).ToList().AsReadOnly();
    }

    public async Task<Alert> AcknowledgeAsync(AlertId alertId, string acknowledgedBy,
        CancellationToken cancellationToken = default)
    {
        var alert = await _repository.GetByIdAsync(alertId.Value, cancellationToken)
            ?? throw new KeyNotFoundException($"Alert {alertId} not found");
        if (!alert.IsActive)
            throw new InvalidOperationException("Cannot acknowledge resolved alert");
        if (alert.AcknowledgedAt != null)
            throw new InvalidOperationException("Alert already acknowledged");
        alert.Acknowledge(acknowledgedBy);
        await _repository.UpdateAsync(alert, cancellationToken);
        await _auditService.LogSuccessAsync("Alert", "Acknowledge", acknowledgedBy,
            targetResource: alert.Id.Value.ToString(), targetResourceType: nameof(Alert),
            cancellationToken: cancellationToken);
        return alert;
    }

    public async Task<Alert> ResolveAsync(AlertId alertId, string resolvedBy,
        string? notes = null, CancellationToken cancellationToken = default)
    {
        var alert = await _repository.GetByIdAsync(alertId.Value, cancellationToken)
            ?? throw new KeyNotFoundException($"Alert {alertId} not found");
        if (!alert.IsActive)
            throw new InvalidOperationException("Alert already resolved");
        alert.Resolve(resolvedBy, notes);
        await _repository.UpdateAsync(alert, cancellationToken);
        await _auditService.LogSuccessAsync("Alert", "Resolve", resolvedBy,
            targetResource: alert.Id.Value.ToString(), targetResourceType: nameof(Alert),
            newValue: $"Notes={notes}", cancellationToken: cancellationToken);
        return alert;
    }

    public Task<IReadOnlyList<Alert>> GroupByDeviceAsync(IReadOnlyList<Alert> alerts)
        => Task.FromResult<IReadOnlyList<Alert>>(alerts);
}
