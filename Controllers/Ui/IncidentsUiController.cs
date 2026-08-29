using AtlasNOC.Domain.Entities;
using AtlasNOC.Domain.Enums;
using AtlasNOC.Domain.Services.Interfaces;
using AtlasNOC.Services.Ui;
using AtlasNOC.Web.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AtlasNOC.Web.Controllers.Ui;

[Route("incidents")]
[Authorize(AuthenticationSchemes = AtlasNocUiAuth.UiScheme, Policy = "ReadOnly")]
public sealed class IncidentsUiController : Controller
{
    private readonly IIncidentService _incidentService;
    private readonly ILogger<IncidentsUiController> _logger;

    public IncidentsUiController(IIncidentService incidentService, ILogger<IncidentsUiController> logger)
    {
        _incidentService = incidentService;
        _logger = logger;
    }

    [HttpGet("")]
    public async Task<IActionResult> Index(IncidentStatus? status = null, CancellationToken ct = default)
    {
        var all = await _incidentService.GetAllAsync(ct);
        if (status.HasValue) all = all.Where(i => i.Status == status.Value).ToList();
        ViewData["Nav"] = "Incidents";
        return View(new IncidentsIndexVm(all.OrderByDescending(i => i.CreatedAt).ToList(), status));
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Detail(Guid id, CancellationToken ct = default)
    {
        var incident = await _incidentService.GetByIdAsync(id, ct);
        if (incident is null) return NotFound();
        ViewData["Nav"] = "Incidents";
        return View(incident);
    }

    [HttpGet("new")]
    [Authorize(AuthenticationSchemes = AtlasNocUiAuth.UiScheme, Policy = "OperatorOrAdmin")]
    public IActionResult CreateRequest()
    {
        ViewData["Nav"] = "Incidents";
        return View("Create", new CreateIncidentUiRequest());
    }

    [HttpPost("new")]
    [Authorize(AuthenticationSchemes = AtlasNocUiAuth.UiScheme, Policy = "OperatorOrAdmin")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateRequest(CreateIncidentUiRequest request, CancellationToken ct = default)
    {
        if (!ModelState.IsValid)
        {
            ViewData["Nav"] = "Incidents";
            return View("Create", request);
        }
        try
        {
            var incident = await _incidentService.CreateAsync(request.Title, request.Description, Actor, ct);
            return RedirectToAction("Detail", new { id = incident.Id });
        }
        catch (ArgumentException ex)
        {
            ModelState.AddModelError(string.Empty, ex.Message);
            ViewData["Nav"] = "Incidents";
            return View("Create", request);
        }
    }

    [HttpPost("{id:guid}/investigating")]
    [Authorize(AuthenticationSchemes = AtlasNocUiAuth.UiScheme, Policy = "OperatorOrAdmin")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ToInvestigating(Guid id, CancellationToken ct = default)
    {
        try { await _incidentService.UpdateStatusToInvestigatingAsync(id, Actor, ct); }
        catch (KeyNotFoundException) { return NotFound(); }
        catch (InvalidOperationException ex) { TempData["Error"] = ex.Message; }
        return RedirectToAction("Detail", new { id });
    }

    [HttpPost("{id:guid}/monitoring")]
    [Authorize(AuthenticationSchemes = AtlasNocUiAuth.UiScheme, Policy = "OperatorOrAdmin")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ToMonitoring(Guid id, CancellationToken ct = default)
    {
        try { await _incidentService.UpdateStatusToMonitoringAsync(id, Actor, ct); }
        catch (KeyNotFoundException) { return NotFound(); }
        catch (InvalidOperationException ex) { TempData["Error"] = ex.Message; }
        return RedirectToAction("Detail", new { id });
    }

    [HttpPost("{id:guid}/resolve")]
    [Authorize(AuthenticationSchemes = AtlasNocUiAuth.UiScheme, Policy = "OperatorOrAdmin")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Resolve(Guid id, [FromForm] string? notes, CancellationToken ct = default)
    {
        try { await _incidentService.ResolveAsync(id, Actor, notes, ct); TempData["Message"] = "Incidente resuelto."; }
        catch (KeyNotFoundException) { return NotFound(); }
        catch (InvalidOperationException ex) { TempData["Error"] = ex.Message; }
        return RedirectToAction("Detail", new { id });
    }

    private string Actor => User.Identity?.Name ?? "operator";
}

public sealed record IncidentsIndexVm(IReadOnlyList<Incident> Items, IncidentStatus? Status);