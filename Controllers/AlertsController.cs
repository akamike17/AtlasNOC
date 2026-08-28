using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Microsoft.AspNetCore.Authorization;
using AtlasNOC.Domain.Entities;
using AtlasNOC.Domain.Enums;
using AtlasNOC.Domain.Services.Interfaces;
using AtlasNOC.Domain.ValueObjects;
using AtlasNOC.Web.Models;

namespace AtlasNOC.Web.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(Policy = "ReadOnly")]
public class AlertsController : ControllerBase
{
    private readonly IAlertService _service;
    private readonly ILogger<AlertsController> _logger;

    public AlertsController(IAlertService service, ILogger<AlertsController> logger)
    {
        _service = service ?? throw new ArgumentNullException(nameof(service));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    [HttpGet]
    public async Task<IReadOnlyList<Alert>> GetAll(CancellationToken ct = default)
        => await _service.GetAllAsync(ct);

    [HttpGet("active")]
    public async Task<IReadOnlyList<Alert>> GetActive(CancellationToken ct = default)
        => await _service.GetActiveAlertsAsync(ct);

    [HttpGet("critical")]
    public async Task<IReadOnlyList<Alert>> GetCritical(CancellationToken ct = default)
        => await _service.GetCriticalAlertsAsync(ct);

    [HttpGet("unacknowledged")]
    public async Task<IReadOnlyList<Alert>> GetUnacknowledged(CancellationToken ct = default)
        => await _service.GetUnacknowledgedAlertsAsync(ct);

    [HttpGet("recent")]
    public async Task<IReadOnlyList<Alert>> GetRecent(
        [FromQuery] int count = 10,
        CancellationToken ct = default)
        => await _service.GetRecentAlertsAsync(count, ct);

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct = default)
    {
        var alert = await _service.GetByIdAsync(AlertId.From(id), ct);
        return alert is null ? NotFound() : Ok(alert);
    }

    [HttpPost]
    [Authorize(Policy = "OperatorOrAdmin")]
    public async Task<IActionResult> Create(
        [FromBody] AtlasNOC.Web.Models.CreateAlertRequest request,
        CancellationToken ct = default)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);
        try
        {
            var domainRequest = new AtlasNOC.Domain.Services.Interfaces.CreateAlertRequest(
                DeviceId.From(request.DeviceId),
                request.Message,
                request.Severity,
                request.Source ?? "api",
                request.Context
            );
            var alert = await _service.CreateAsync(domainRequest, ct);
            return CreatedAtAction(nameof(GetById), new { id = alert.Id.Value }, alert);
        }
        catch (ArgumentException ex)
        {
            _logger.LogWarning(ex, "Alert creation failed: {Message}", ex.Message);
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpPost("{id:guid}/acknowledge")]
    [Authorize(Policy = "OperatorOrAdmin")]
    public async Task<IActionResult> Acknowledge(
        Guid id, [FromBody] AcknowledgeRequest request,
        CancellationToken ct = default)
    {
        try
        {
            var alert = await _service.AcknowledgeAsync(AlertId.From(id), Actor, ct);
            return Ok(alert);
        }
        catch (KeyNotFoundException ex)
        {
            _logger.LogWarning(ex, "Alert not found: {Id}", id);
            return NotFound(new { error = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogInformation(ex, "Alert already acknowledged: {Id}", id);
            return Conflict(new { error = ex.Message });
        }
    }

    [HttpPost("{id:guid}/resolve")]
    [Authorize(Policy = "OperatorOrAdmin")]
    public async Task<IActionResult> Resolve(
        Guid id, [FromBody] ResolveRequest request,
        CancellationToken ct = default)
    {
        try
        {
            var alert = await _service.ResolveAsync(AlertId.From(id), Actor, request.Notes, ct);
            return Ok(alert);
        }
        catch (KeyNotFoundException ex)
        {
            _logger.LogWarning(ex, "Alert not found: {Id}", id);
            return NotFound(new { error = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogInformation(ex, "Alert already resolved: {Id}", id);
            return Conflict(new { error = ex.Message });
        }
    }

    private string Actor => User.Identity?.Name ?? throw new InvalidOperationException("Authenticated actor is unavailable.");
}
