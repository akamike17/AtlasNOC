using System;
using System.Collections.ObjectModel;
using AtlasNOC.Domain.Enums;

namespace AtlasNOC.Domain.Entities;

public class Incident
{
    public Guid Id { get; private set; }
    public string Title { get; private set; } = string.Empty;
    public string Description { get; private set; } = string.Empty;
    public IncidentStatus Status { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime? ResolvedAt { get; private set; }
    public string CreatedBy { get; private set; } = string.Empty;
    public string? ResolvedBy { get; private set; }
    public string? ResolutionNotes { get; private set; }
    public IReadOnlyCollection<Alert> RelatedAlerts { get; private set; } = Array.Empty<Alert>();

    private Incident() { }

    public Incident(
        Guid id, string title, string description,
        IncidentStatus status, DateTime createdAt, string createdBy,
        IReadOnlyCollection<Alert> relatedAlerts)
    {
        Id = id;
        Title = title ?? throw new ArgumentNullException(nameof(title));
        Description = description ?? throw new ArgumentNullException(nameof(description));
        Status = status;
        CreatedAt = createdAt;
        CreatedBy = createdBy ?? throw new ArgumentNullException(nameof(createdBy));
        RelatedAlerts = relatedAlerts ?? new List<Alert>().AsReadOnly();
    }

    public static Incident Create(string title, string description, string createdBy)
    {
        return new Incident(
            Guid.NewGuid(),
            title,
            description,
            IncidentStatus.New,
            DateTime.UtcNow,
            createdBy,
            new List<Alert>().AsReadOnly());
    }

    public void AddAlert(Alert alert)
    {
        if (alert == null) throw new ArgumentNullException(nameof(alert));
        // En una implementación real esto actualizaría la colección
        // y persistiría el cambio.
    }

    public void Resolve(string resolvedBy, string? notes = null)
    {
        if (Status == IncidentStatus.Resolved)
            throw new InvalidOperationException("Incident already resolved");
        if (Status == IncidentStatus.Closed)
            throw new InvalidOperationException("Cannot resolve a closed incident");
        ResolvedBy = resolvedBy ?? throw new ArgumentNullException(nameof(resolvedBy));
        ResolutionNotes = notes;
        ResolvedAt = DateTime.UtcNow;
        Status = IncidentStatus.Resolved;
    }

    public void SetInvestigating(string modifiedBy)
    {
        if (Status == IncidentStatus.Resolved || Status == IncidentStatus.Closed)
            throw new InvalidOperationException(
                $"Cannot investigate a {Status} incident");
        Status = IncidentStatus.Investigating;
    }

    public void SetMonitoring(string modifiedBy)
    {
        if (Status == IncidentStatus.Resolved || Status == IncidentStatus.Closed)
            throw new InvalidOperationException(
                $"Cannot monitor a {Status} incident");
        Status = IncidentStatus.Monitoring;
    }
}
