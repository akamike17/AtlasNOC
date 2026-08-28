using System;
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
[Authorize(Policy = "ReadOnly")]
public class IncidentsController : ControllerBase
{
    private readonly IIncidentService _service;
    private readonly ILogger<IncidentsController> _logger;

    public IncidentsController(IIncidentService service, ILogger<IncidentsController> logger)
    {
        _service = service ?? throw new ArgumentNullException(nameof(service));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    [HttpGet]
    public async Task<IReadOnlyList<Incident>> GetAll(CancellationToken ct = default)
        => await _service.GetAllAsync(ct);

    [HttpGet("open")]
    public async Task<IReadOnlyList<Incident>> GetOpen(CancellationToken ct = default)
        => await _service.GetOpenAsync(ct);

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct = default)
    {
        var incident = await _service.GetByIdAsync(id, ct);
        return incident is null ? NotFound() : Ok(incident);
    }

    [HttpPost]
    [Authorize(Policy = "OperatorOrAdmin")]
    public async Task<IActionResult> Create(
        [FromBody] CreateIncidentRequest request,
        CancellationToken ct = default)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);
        try
        {
            var incident = await _service.CreateAsync(
                request.Title, request.Description, Actor, ct);
            return CreatedAtAction(nameof(GetById), new { id = incident.Id }, incident);
        }
        catch (ArgumentException ex)
        {
            _logger.LogWarning(ex, "Incident creation failed: {Message}", ex.Message);
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpPost("{id:guid}/investigating")]
    [Authorize(Policy = "OperatorOrAdmin")]
    public async Task<IActionResult> ToInvestigating(
        Guid id,
        CancellationToken ct = default)
    {
        try
        {
            var incident = await _service.UpdateStatusToInvestigatingAsync(id, Actor, ct);
            return Ok(incident);
        }
        catch (KeyNotFoundException ex)
        {
            _logger.LogWarning(ex, "Incident not found: {Id}", id);
            return NotFound(new { error = ex.Message });
        }
    }

    [HttpPost("{id:guid}/monitoring")]
    [Authorize(Policy = "OperatorOrAdmin")]
    public async Task<IActionResult> ToMonitoring(
        Guid id,
        CancellationToken ct = default)
    {
        try
        {
            var incident = await _service.UpdateStatusToMonitoringAsync(id, Actor, ct);
            return Ok(incident);
        }
        catch (KeyNotFoundException ex)
        {
            _logger.LogWarning(ex, "Incident not found: {Id}", id);
            return NotFound(new { error = ex.Message });
        }
    }

    [HttpPost("{id:guid}/resolve")]
    [Authorize(Policy = "OperatorOrAdmin")]
    public async Task<IActionResult> Resolve(
        Guid id, [FromBody] ResolveIncidentRequest request,
        CancellationToken ct = default)
    {
        try
        {
            var incident = await _service.ResolveAsync(id, Actor, request.Notes, ct);
            return Ok(incident);
        }
        catch (KeyNotFoundException ex)
        {
            _logger.LogWarning(ex, "Incident not found: {Id}", id);
            return NotFound(new { error = ex.Message });
        }
    }

    private string Actor => User.Identity?.Name ?? throw new InvalidOperationException("Authenticated actor is unavailable.");
}
