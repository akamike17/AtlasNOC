using AtlasNOC.Domain.Enums;

namespace AtlasNOC.Domain.Entities;

/// <summary>Registro inmutable de una acción administrativa o crítica.</summary>
public class AuditEvent
{
    public Guid Id { get; private set; }
    public string Category { get; private set; } = string.Empty;
    public string Action { get; private set; } = string.Empty;
    public string ActorUserId { get; private set; } = string.Empty;
    public string ActorEmail { get; private set; } = string.Empty;
    public string ActorRole { get; private set; } = string.Empty;
    public string? TargetResource { get; private set; }
    public string? TargetResourceType { get; private set; }
    public string? OldValue { get; private set; }
    public string? NewValue { get; private set; }
    public DateTime TimestampUtc { get; private set; }
    public AuditResult Result { get; private set; }
    public string? Reason { get; private set; }
    public string? IpAddress { get; private set; }
    public string? UserAgent { get; private set; }

    private AuditEvent() { }

    public AuditEvent(string category, string action, string actorUserId,
        string actorEmail, string actorRole, string? targetResource = null,
        string? targetResourceType = null, string? oldValue = null, string? newValue = null,
        AuditResult result = AuditResult.Success, string? reason = null,
        string? ipAddress = null, string? userAgent = null)
    {
        Id = Guid.NewGuid();
        Category = category ?? throw new ArgumentNullException(nameof(category));
        Action = action ?? throw new ArgumentNullException(nameof(action));
        ActorUserId = actorUserId ?? throw new ArgumentNullException(nameof(actorUserId));
        ActorEmail = actorEmail;
        ActorRole = actorRole;
        TargetResource = targetResource;
        TargetResourceType = targetResourceType;
        OldValue = oldValue;
        NewValue = newValue;
        TimestampUtc = DateTime.UtcNow;
        Result = result;
        Reason = reason;
        IpAddress = ipAddress;
        UserAgent = userAgent;
    }
}