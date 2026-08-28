using System;

namespace AtlasNOC.Domain.Entities;

public class AuditEvent
{
    public Guid EventId { get; init; }
    public string Category { get; init; } = string.Empty;
    public string Action { get; init; } = string.Empty;
    public string UserId { get; init; } = string.Empty;
    public string? UserEmail { get; init; }
    public string? UserRole { get; init; }
    public string? TargetResource { get; init; }
    public string? TargetResourceType { get; init; }
    public string? OldValue { get; init; }
    public string? NewValue { get; init; }
    public DateTimeOffset Timestamp { get; init; }
    public AuditResult Result { get; init; }
    public string? Reason { get; init; }
    public string? IpAddress { get; init; }
    public string? UserAgent { get; init; }

    // Dapper-compatible parameterless constructor
    private AuditEvent() { }

    public AuditEvent(Guid eventId, string category, string action, string userId,
        string? userEmail = null, string? userRole = null, string? targetResource = null,
        string? targetResourceType = null, string? oldValue = null, string? newValue = null,
        DateTimeOffset? timestamp = null, AuditResult result = AuditResult.Success,
        string? reason = null, string? ipAddress = null, string? userAgent = null)
    {
        EventId = eventId;
        Category = category ?? throw new ArgumentNullException(nameof(category));
        Action = action ?? throw new ArgumentNullException(nameof(action));
        UserId = userId ?? throw new ArgumentNullException(nameof(userId));
        UserEmail = userEmail;
        UserRole = userRole;
        TargetResource = targetResource;
        TargetResourceType = targetResourceType;
        OldValue = oldValue;
        NewValue = newValue;
        Timestamp = timestamp ?? DateTimeOffset.UtcNow;
        Result = result;
        Reason = reason;
        IpAddress = ipAddress;
        UserAgent = userAgent;
    }

    public static AuditEvent CreateSuccess(string category, string action, string userId,
        string? userEmail = null, string? userRole = null, string? targetResource = null,
        string? targetResourceType = null, string? oldValue = null, string? newValue = null,
        string? ipAddress = null, string? userAgent = null)
        => new(Guid.NewGuid(), category, action, userId, userEmail, userRole,
            targetResource, targetResourceType, oldValue, newValue, null,
            AuditResult.Success, null, ipAddress, userAgent);

    public static AuditEvent CreateFailure(string category, string action, string userId,
        string reason, string? userEmail = null, string? userRole = null,
        string? targetResource = null, string? targetResourceType = null,
        string? ipAddress = null, string? userAgent = null)
        => new(Guid.NewGuid(), category, action, userId, userEmail, userRole,
            targetResource, targetResourceType, null, null, null,
            AuditResult.Failure, reason, ipAddress, userAgent);

    public static AuditEvent CreateDenied(string category, string action, string userId,
        string reason, string? userEmail = null, string? userRole = null,
        string? targetResource = null, string? targetResourceType = null,
        string? ipAddress = null, string? userAgent = null)
        => new(Guid.NewGuid(), category, action, userId, userEmail, userRole,
            targetResource, targetResourceType, null, null, null,
            AuditResult.Denied, reason, ipAddress, userAgent);
}

public enum AuditResult
{
    Success = 0,
    Failure = 1,
    Denied = 2,
    NotFound = 3
}
