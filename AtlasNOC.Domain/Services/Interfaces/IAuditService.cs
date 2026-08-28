using System;
using System.Threading;
using System.Threading.Tasks;
using AtlasNOC.Domain.Entities;

namespace AtlasNOC.Domain.Services.Interfaces;

public interface IAuditService
{
    Task LogAsync(AuditEvent auditEvent, CancellationToken cancellationToken = default);
    Task LogSuccessAsync(string category, string action, string userId,
        string? userEmail = null, string? userRole = null, string? targetResource = null,
        string? targetResourceType = null, string? oldValue = null, string? newValue = null,
        string? ipAddress = null, string? userAgent = null,
        CancellationToken cancellationToken = default);
    Task LogFailureAsync(string category, string action, string userId, string reason,
        string? userEmail = null, string? userRole = null, string? targetResource = null,
        string? targetResourceType = null, string? ipAddress = null, string? userAgent = null,
        CancellationToken cancellationToken = default);
    Task LogDeniedAsync(string category, string action, string userId, string reason,
        string? userEmail = null, string? userRole = null, string? targetResource = null,
        string? targetResourceType = null, string? ipAddress = null, string? userAgent = null,
        CancellationToken cancellationToken = default);
    Task<IReadOnlyList<AuditEvent>> GetRecentAsync(int count,
        CancellationToken cancellationToken = default);
    Task<IReadOnlyList<AuditEvent>> GetByCategoryAsync(string category,
        CancellationToken cancellationToken = default);
    Task<IReadOnlyList<AuditEvent>> GetByUserIdAsync(string userId,
        CancellationToken cancellationToken = default);
}
