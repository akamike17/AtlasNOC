using AtlasNOC.Domain.Enums;

namespace AtlasNOC.Domain.Entities;

/// <summary>Ejecución de un descubrimiento (workbook de hallazgos por corrida).</summary>
public class DiscoveryRun
{
    public Guid Id { get; private set; }
    public string ScopeIp { get; private set; } = string.Empty;
    public string? TargetSiteId { get; private set; }
    public string? CredentialId { get; private set; }
    public DiscoveryRunStatus Status { get; private set; }
    public DateTime StartedAtUtc { get; private set; }
    public DateTime? CompletedAtUtc { get; private set; }
    public int FoundCount { get; private set; }
    public int NewCount { get; private set; }
    public int UpdatedCount { get; private set; }
    public int ConfirmedLinkCount { get; private set; }
    public int PendingRelationCount { get; private set; }
    public int FailureCount { get; private set; }
    public string? SummaryJson { get; private set; }

    private DiscoveryRun() { }

    public DiscoveryRun(string scopeIp, string? targetSiteId, string? credentialId)
    {
        Id = Guid.NewGuid();
        ScopeIp = scopeIp ?? throw new ArgumentNullException(nameof(scopeIp));
        TargetSiteId = targetSiteId;
        CredentialId = credentialId;
        Status = DiscoveryRunStatus.Pending;
        StartedAtUtc = DateTime.UtcNow;
    }

    public void Start() => Status = DiscoveryRunStatus.Running;

    public void Complete(int found, int added, int updated, int confirmedLinks,
        int pendingRelations, int failures, string? summaryJson = null)
    {
        Status = DiscoveryRunStatus.Completed;
        CompletedAtUtc = DateTime.UtcNow;
        FoundCount = found;
        NewCount = added;
        UpdatedCount = updated;
        ConfirmedLinkCount = confirmedLinks;
        PendingRelationCount = pendingRelations;
        FailureCount = failures;
        SummaryJson = summaryJson;
    }

    public void Fail(string? summaryJson = null)
    {
        Status = DiscoveryRunStatus.Failed;
        CompletedAtUtc = DateTime.UtcNow;
        SummaryJson = summaryJson;
    }
}