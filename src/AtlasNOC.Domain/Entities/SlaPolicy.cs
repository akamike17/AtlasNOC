namespace AtlasNOC.Domain.Entities;

public sealed record SlaTarget(IncidentPriority Priority, int ResponseMinutes, int ResolutionTargetHours);

public sealed class SlaPolicy
{
    private readonly IReadOnlyDictionary<IncidentPriority, SlaTarget> _targets;
    public SlaPolicy(IReadOnlyDictionary<IncidentPriority, SlaTarget>? targets = null) => _targets = targets ?? new Dictionary<IncidentPriority, SlaTarget>
    {
        [IncidentPriority.P1Critical] = new(IncidentPriority.P1Critical, 15, 4),
        [IncidentPriority.P2High] = new(IncidentPriority.P2High, 30, 8),
        [IncidentPriority.P3Medium] = new(IncidentPriority.P3Medium, 60, 24),
        [IncidentPriority.P4Low] = new(IncidentPriority.P4Low, 240, 48)
    };
    public SlaTarget For(IncidentPriority priority) => _targets.TryGetValue(priority, out var target) ? target : throw new ArgumentOutOfRangeException(nameof(priority));
}
