using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using AtlasNOC.Domain.Entities;
using AtlasNOC.Domain.Services.Interfaces;

namespace AtlasNOC.Domain.Services;

public class AuditService : IAuditService
{
    private readonly IRepository<AuditEvent> _repository;
    private readonly ILogger<AuditService> _logger;

    public AuditService(IRepository<AuditEvent> repository, ILogger<AuditService> logger)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task LogAsync(AuditEvent auditEvent,
        CancellationToken cancellationToken = default)
    {
        await _repository.AddAsync(auditEvent, cancellationToken);
        _logger.LogInformation("{Category} {Action} by {UserId} - {Result}: {Reason}",
            auditEvent.Category, auditEvent.Action, auditEvent.UserId,
            auditEvent.Result, auditEvent.Reason ?? "");
    }

    public async Task LogSuccessAsync(string category, string action, string userId,
        string? userEmail = null, string? userRole = null, string? targetResource = null,
        string? targetResourceType = null, string? oldValue = null, string? newValue = null,
        string? ipAddress = null, string? userAgent = null,
        CancellationToken cancellationToken = default)
    {
        var evt = AuditEvent.CreateSuccess(category, action, userId, userEmail, userRole,
            targetResource, targetResourceType, oldValue, newValue, ipAddress, userAgent);
        await LogAsync(evt, cancellationToken);
    }

    public async Task LogFailureAsync(string category, string action, string userId,
        string reason, string? userEmail = null, string? userRole = null,
        string? targetResource = null, string? targetResourceType = null,
        string? ipAddress = null, string? userAgent = null,
        CancellationToken cancellationToken = default)
    {
        var evt = AuditEvent.CreateFailure(category, action, userId, reason,
            userEmail, userRole, targetResource, targetResourceType, ipAddress, userAgent);
        await LogAsync(evt, cancellationToken);
    }

    public async Task LogDeniedAsync(string category, string action, string userId,
        string reason, string? userEmail = null, string? userRole = null,
        string? targetResource = null, string? targetResourceType = null,
        string? ipAddress = null, string? userAgent = null,
        CancellationToken cancellationToken = default)
    {
        var evt = AuditEvent.CreateDenied(category, action, userId, reason,
            userEmail, userRole, targetResource, targetResourceType, ipAddress, userAgent);
        await LogAsync(evt, cancellationToken);
    }

    public async Task<IReadOnlyList<AuditEvent>> GetRecentAsync(int count,
        CancellationToken cancellationToken = default)
    {
        var all = await _repository.GetAllAsync(cancellationToken);
        return all.OrderByDescending(e => e.Timestamp).Take(count).ToList().AsReadOnly();
    }

    public async Task<IReadOnlyList<AuditEvent>> GetByCategoryAsync(string category,
        CancellationToken cancellationToken = default)
    {
        var all = await _repository.GetAllAsync(cancellationToken);
        return all.Where(e => e.Category == category)
            .OrderByDescending(e => e.Timestamp).ToList().AsReadOnly();
    }

    public async Task<IReadOnlyList<AuditEvent>> GetByUserIdAsync(string userId,
        CancellationToken cancellationToken = default)
    {
        var all = await _repository.GetAllAsync(cancellationToken);
        return all.Where(e => e.UserId == userId)
            .OrderByDescending(e => e.Timestamp).ToList().AsReadOnly();
    }
}
