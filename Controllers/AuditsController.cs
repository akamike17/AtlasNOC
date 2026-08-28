using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Microsoft.AspNetCore.Authorization;
using AtlasNOC.Domain.Entities;
using AtlasNOC.Domain.Services.Interfaces;
using AtlasNOC.Web.Models;

namespace AtlasNOC.Web.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(Policy = "AdminOnly")]
public class AuditsController : ControllerBase
{
    private readonly IAuditService _auditService;
    private readonly ILogger<AuditsController> _logger;

    public AuditsController(IAuditService auditService, ILogger<AuditsController> logger)
    {
        _auditService = auditService;
        _logger = logger;
    }

    /// <summary>
    /// Get recent audit events.
    /// </summary>
    [HttpGet("recent")]
    public async Task<IReadOnlyList<AuditEvent>> GetRecent(
        [FromQuery] int count = 50,
        CancellationToken ct = default)
    {
        return await _auditService.GetRecentAsync(count, ct);
    }

    /// <summary>
    /// Get audit events by category.
    /// </summary>
    [HttpGet("category/{category}")]
    public async Task<IReadOnlyList<AuditEvent>> GetByCategory(
        string category,
        CancellationToken ct = default)
    {
        return await _auditService.GetByCategoryAsync(category, ct);
    }

    /// <summary>
    /// Get audit events by user ID.
    /// </summary>
    [HttpGet("user/{userId}")]
    public async Task<IReadOnlyList<AuditEvent>> GetByUser(
        string userId,
        CancellationToken ct = default)
    {
        return await _auditService.GetByUserIdAsync(userId, ct);
    }
}
