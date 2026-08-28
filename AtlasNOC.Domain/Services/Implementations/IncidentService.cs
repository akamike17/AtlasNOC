using System;
using System.Threading;
using System.Threading.Tasks;
using AtlasNOC.Domain.Entities;
using AtlasNOC.Domain.Enums;
using AtlasNOC.Domain.Services.Interfaces;

namespace AtlasNOC.Domain.Services;

public class IncidentService : IIncidentService
{
    private readonly IRepository<Incident> _repository;
    private readonly IAuditService _auditService;

    public IncidentService(IRepository<Incident> repository, IAuditService auditService)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        _auditService = auditService ?? throw new ArgumentNullException(nameof(auditService));
    }

    public async Task<Incident> CreateAsync(string title, string description, string createdBy,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(title))
            throw new ArgumentException("Title is required", nameof(title));
        if (string.IsNullOrWhiteSpace(description))
            throw new ArgumentException("Description is required", nameof(description));
        if (string.IsNullOrWhiteSpace(createdBy))
            throw new ArgumentException("CreatedBy is required", nameof(createdBy));

        var incident = Incident.Create(title, description, createdBy);
        await _repository.AddAsync(incident, cancellationToken);
        await _auditService.LogSuccessAsync("Incident", "Create", createdBy,
            targetResource: incident.Id.ToString(),
            targetResourceType: nameof(Incident),
            newValue: $"Title={title}, Status={incident.Status}",
            cancellationToken: cancellationToken);
        return incident;
    }

    public Task<IReadOnlyList<Incident>> GetAllAsync(CancellationToken cancellationToken = default)
        => _repository.GetAllAsync(cancellationToken);

    public async Task<IReadOnlyList<Incident>> GetOpenAsync(CancellationToken cancellationToken = default)
    {
        var all = await _repository.GetAllAsync(cancellationToken);
        return all.Where(i => i.Status != IncidentStatus.Resolved && i.Status != IncidentStatus.Closed)
            .ToList().AsReadOnly();
    }

    public Task<Incident?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
        => _repository.GetByIdAsync(id, cancellationToken);

    public async Task<Incident> UpdateStatusToInvestigatingAsync(Guid incidentId, string modifiedBy,
        CancellationToken cancellationToken = default)
    {
        var incident = await _repository.GetByIdAsync(incidentId, cancellationToken)
            ?? throw new KeyNotFoundException($"Incident {incidentId} not found");
        if (incident.Status == IncidentStatus.Resolved || incident.Status == IncidentStatus.Closed)
            throw new InvalidOperationException($"Cannot investigate a {incident.Status} incident");

        var oldStatus = incident.Status;
        incident.SetInvestigating(modifiedBy);
        await _repository.UpdateAsync(incident, cancellationToken);
        await _auditService.LogSuccessAsync("Incident", "StatusChange",
            modifiedBy, targetResource: incidentId.ToString(),
            targetResourceType: nameof(Incident),
            oldValue: $"Status={oldStatus}", newValue: $"Status={IncidentStatus.Investigating}",
            cancellationToken: cancellationToken);
        return incident;
    }

    public async Task<Incident> UpdateStatusToMonitoringAsync(Guid incidentId, string modifiedBy,
        CancellationToken cancellationToken = default)
    {
        var incident = await _repository.GetByIdAsync(incidentId, cancellationToken)
            ?? throw new KeyNotFoundException($"Incident {incidentId} not found");
        if (incident.Status == IncidentStatus.Resolved || incident.Status == IncidentStatus.Closed)
            throw new InvalidOperationException($"Cannot monitor a {incident.Status} incident");

        var oldStatus = incident.Status;
        incident.SetMonitoring(modifiedBy);
        await _repository.UpdateAsync(incident, cancellationToken);
        await _auditService.LogSuccessAsync("Incident", "StatusChange",
            modifiedBy, targetResource: incidentId.ToString(),
            targetResourceType: nameof(Incident),
            oldValue: $"Status={oldStatus}", newValue: $"Status={IncidentStatus.Monitoring}",
            cancellationToken: cancellationToken);
        return incident;
    }

    public async Task<Incident> ResolveAsync(Guid incidentId, string resolvedBy, string? notes = null,
        CancellationToken cancellationToken = default)
    {
        var incident = await _repository.GetByIdAsync(incidentId, cancellationToken)
            ?? throw new KeyNotFoundException($"Incident {incidentId} not found");
        incident.Resolve(resolvedBy, notes);
        await _repository.UpdateAsync(incident, cancellationToken);
        await _auditService.LogSuccessAsync("Incident", "Resolve", resolvedBy,
            targetResource: incidentId.ToString(),
            targetResourceType: nameof(Incident),
            newValue: $"Resolved by {resolvedBy}, Notes={notes}",
            cancellationToken: cancellationToken);
        return incident;
    }
}
