using AtlasNOC.Domain.Enums;

namespace AtlasNOC.Domain.Entities;

/// <summary>Incidente que correlaciona una o varias alertas y sus dependencias.</summary>
public class Incident
{
    public Guid Id { get; private set; }
    public string Title { get; private set; } = string.Empty;
    public string? Description { get; private set; }
    public IncidentStatus Status { get; private set; }
    public string? RootCauseDeviceId { get; private set; }
    public bool IsRootCauseCandidate { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }
    public DateTime? ResolvedAtUtc { get; private set; }
    public string CreatedBy { get; private set; } = string.Empty;
    public string? ResolvedBy { get; private set; }

    private Incident() { }

    public Incident(string title, string createdBy, string? description = null,
        string? rootCauseDeviceId = null)
    {
        Id = Guid.NewGuid();
        Title = title ?? throw new ArgumentNullException(nameof(title));
        CreatedBy = createdBy ?? throw new ArgumentNullException(nameof(createdBy));
        Description = description;
        RootCauseDeviceId = rootCauseDeviceId;
        Status = IncidentStatus.New;
        CreatedAtUtc = DateTime.UtcNow;
    }

    public void MarkRootCauseCandidate() => IsRootCauseCandidate = true;
    public void Investigate() => Status = IncidentStatus.Investigating;
    public void Monitor() => Status = IncidentStatus.Monitoring;

    public void Resolve(string by)
    {
        ResolvedBy = by;
        ResolvedAtUtc = DateTime.UtcNow;
        Status = IncidentStatus.Resolved;
    }
}