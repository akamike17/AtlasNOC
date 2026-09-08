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
    public string? ClaimedBy { get; private set; }
    public DateTime? ClaimedAtUtc { get; private set; }
    public DateTime? LeaseExpiresAtUtc { get; private set; }
    public int AttemptCount { get; private set; }

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

    public void Claim(string workerId, DateTime claimedAtUtc, TimeSpan leaseDuration)
    {
        if (string.IsNullOrWhiteSpace(workerId)) throw new ArgumentException("Worker requerido.", nameof(workerId));
        if (leaseDuration <= TimeSpan.Zero) throw new ArgumentOutOfRangeException(nameof(leaseDuration));
        if (Status != DiscoveryRunStatus.Pending
            && !(Status == DiscoveryRunStatus.Running && LeaseExpiresAtUtc <= claimedAtUtc))
            throw new InvalidOperationException("La corrida no está disponible para claim.");
        Status = DiscoveryRunStatus.Running;
        ClaimedBy = workerId;
        ClaimedAtUtc = claimedAtUtc;
        LeaseExpiresAtUtc = claimedAtUtc.Add(leaseDuration);
        AttemptCount++;
    }

    public void RenewLease(string workerId, DateTime nowUtc, TimeSpan leaseDuration)
    {
        if (Status != DiscoveryRunStatus.Running || ClaimedBy != workerId)
            throw new InvalidOperationException("El worker no posee esta corrida.");
        LeaseExpiresAtUtc = nowUtc.Add(leaseDuration);
    }

    public void Cancel()
    {
        if (Status is DiscoveryRunStatus.Completed or DiscoveryRunStatus.Failed) return;
        Status = DiscoveryRunStatus.Cancelled;
        CompletedAtUtc = DateTime.UtcNow;
        LeaseExpiresAtUtc = null;
    }

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
        LeaseExpiresAtUtc = null;
    }

    public void Fail(string? summaryJson = null)
    {
        Status = DiscoveryRunStatus.Failed;
        CompletedAtUtc = DateTime.UtcNow;
        SummaryJson = summaryJson;
        LeaseExpiresAtUtc = null;
    }
}
