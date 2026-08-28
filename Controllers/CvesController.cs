using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using AtlasNOC.Domain.Entities;
using AtlasNOC.Domain.Services.Interfaces;
using AtlasNOC.Domain.ValueObjects;
using Microsoft.AspNetCore.Authorization;

namespace AtlasNOC.Web.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(Policy = "ReadOnly")]
public class CvesController : ControllerBase
{
    private readonly ICveService _service;
    private readonly ILogger<CvesController> _logger;

    public CvesController(ICveService service, ILogger<CvesController> logger)
    {
        _service = service ?? throw new ArgumentNullException(nameof(service));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Get all CVE records with optional filtering.
    /// </summary>
    [HttpGet]
    public async Task<IReadOnlyList<CveRecord>> GetAll(
        [FromQuery] string? keyword = null,
        [FromQuery] string? severity = null,
        [FromQuery] DateTime? since = null,
        CancellationToken ct = default)
    {
        if (!string.IsNullOrWhiteSpace(keyword))
            return await _service.SearchByKeywordAsync(keyword, ct);

        if (!string.IsNullOrWhiteSpace(severity))
            return await _service.GetBySeverityAsync(severity, ct);

        if (since.HasValue)
            return await _service.GetSinceAsync(since.Value, ct);

        return await _service.GetAllAsync(ct);
    }

    /// <summary>
    /// Get a specific CVE by CVE ID (e.g., CVE-2024-12345).
    /// </summary>
    [HttpGet("{cveId}")]
    public async Task<IActionResult> GetByCveId(string cveId, CancellationToken ct = default)
    {
        var cve = await _service.GetByCveIdAsync(cveId, ct);
        return cve is null ? NotFound() : Ok(cve);
    }

    /// <summary>
    /// Get CVE records for a specific device keyword.
    /// </summary>
    [HttpGet("keyword/{keyword}")]
    public async Task<IReadOnlyList<CveRecord>> GetByKeyword(string keyword, CancellationToken ct = default)
        => await _service.SearchByKeywordAsync(keyword, ct);

    /// <summary>
    /// Get critical and high severity CVEs.
    /// </summary>
    [HttpGet("critical")]
    public async Task<IReadOnlyList<CveRecord>> GetCritical(CancellationToken ct = default)
        => await _service.GetCriticalAsync(ct);

    /// <summary>
    /// Get recent CVEs.
    /// </summary>
    [HttpGet("recent")]
    public async Task<IReadOnlyList<CveRecord>> GetRecent(
        [FromQuery] int count = 50,
        CancellationToken ct = default)
        => await _service.GetRecentAsync(count, ct);

    /// <summary>
    /// Get CVE statistics.
    /// </summary>
    [HttpGet("stats")]
    public async Task<CveStats> GetStats(CancellationToken ct = default)
        => await _service.GetStatsAsync(ct);

    /// <summary>
    /// Trigger manual CVE fetch.
    /// </summary>
    [HttpPost("fetch")]
    [Authorize(Policy = "OperatorOrAdmin")]
    public async Task<IActionResult> TriggerFetch(CancellationToken ct = default)
    {
        var fetched = await _service.TriggerFetchAsync(ct);
        return Ok(new { fetched, message = $"Fetched {fetched} CVE records" });
    }
}