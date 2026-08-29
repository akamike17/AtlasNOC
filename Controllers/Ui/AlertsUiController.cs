using AtlasNOC.Domain.Entities;
using AtlasNOC.Domain.Enums;
using AtlasNOC.Domain.Services.Interfaces;
using AtlasNOC.Domain.ValueObjects;
using AtlasNOC.Services.Ui;
using AtlasNOC.Web.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AtlasNOC.Web.Controllers.Ui;

[Route("alerts")]
[Authorize(AuthenticationSchemes = AtlasNocUiAuth.UiScheme, Policy = "ReadOnly")]
public sealed class AlertsUiController : Controller
{
    private readonly IAlertService _alertService;
    private readonly IDeviceService _deviceService;
    private readonly IIncidentService _incidentService;
    private readonly ILogger<AlertsUiController> _logger;

    public AlertsUiController(IAlertService alertService, IDeviceService deviceService,
        IIncidentService incidentService, ILogger<AlertsUiController> logger)
    {
        _alertService = alertService;
        _deviceService = deviceService;
        _incidentService = incidentService;
        _logger = logger;
    }

    [HttpGet("")]
    public async Task<IActionResult> Index(AlertSeverity? severity = null, bool? onlyActive = null,
        Guid? deviceId = null, DateTime? from = null, DateTime? to = null, CancellationToken ct = default)
    {
        var alerts = await _alertService.GetAllAsync(ct);
        if (severity.HasValue) alerts = alerts.Where(a => a.Severity == severity.Value).ToList();
        if (onlyActive == true) alerts = alerts.Where(a => a.IsActive).ToList();
        if (deviceId.HasValue) alerts = alerts.Where(a => a.DeviceId.Value == deviceId.Value).ToList();

        var items = alerts.OrderByDescending(a => a.OccurredAt).ToList();
        var devices = await _deviceService.GetAllAsync(ct);
        ViewData["Nav"] = "Alerts";
        return View(new AlertsIndexVm(items, devices, severity, onlyActive, deviceId, from, to));
    }

    [HttpPost("new")]
    [Authorize(AuthenticationSchemes = AtlasNocUiAuth.UiScheme, Policy = "OperatorOrAdmin")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(CreateAlertUiRequest request, CancellationToken ct = default)
    {
        if (!ModelState.IsValid)
        {
            TempData["Error"] = "Datos de alerta inválidos.";
            return RedirectToAction("Index");
        }
        try
        {
            var domain = new AtlasNOC.Domain.Services.Interfaces.CreateAlertRequest(
                DeviceId.From(request.DeviceId), request.Message, request.Severity,
                request.Source ?? "ui", null);
            await _alertService.CreateAsync(domain, ct);
            TempData["Message"] = "Alerta creada.";
        }
        catch (ArgumentException ex) { TempData["Error"] = ex.Message; }
        return RedirectToAction("Index");
    }

    [HttpPost("{id:guid}/acknowledge")]
    [Authorize(AuthenticationSchemes = AtlasNocUiAuth.UiScheme, Policy = "OperatorOrAdmin")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Acknowledge(Guid id, CancellationToken ct = default)
    {
        try
        {
            await _alertService.AcknowledgeAsync(AlertId.From(id), Actor, ct);
            TempData["Message"] = "Alerta reconocida.";
        }
        catch (KeyNotFoundException) { return NotFound(); }
        catch (InvalidOperationException ex) { TempData["Error"] = ex.Message; }
        return RedirectToAction("Index");
    }

    [HttpPost("{id:guid}/resolve")]
    [Authorize(AuthenticationSchemes = AtlasNocUiAuth.UiScheme, Policy = "OperatorOrAdmin")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Resolve(Guid id, [FromForm] string? notes, CancellationToken ct = default)
    {
        try
        {
            await _alertService.ResolveAsync(AlertId.From(id), Actor, notes, ct);
            TempData["Message"] = "Alerta resuelta.";
        }
        catch (KeyNotFoundException) { return NotFound(); }
        catch (InvalidOperationException ex) { TempData["Error"] = ex.Message; }
        return RedirectToAction("Index");
    }

    [HttpPost("{id:guid}/to-incident")]
    [Authorize(AuthenticationSchemes = AtlasNocUiAuth.UiScheme, Policy = "OperatorOrAdmin")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ToIncident(Guid id, CancellationToken ct = default)
    {
        var alert = await _alertService.GetByIdAsync(AlertId.From(id), ct);
        if (alert is null) return NotFound();
        var device = await _deviceService.GetByIdAsync(alert.DeviceId, ct);
        try
        {
            var incident = await _incidentService.CreateAsync(
                $"Alerta: {alert.Message}", $"Generado desde la alerta del dispositivo {(device?.Name ?? "desconocido")}.", Actor, ct);
            TempData["Message"] = $"Incidente {incident.Id:N} creado.";
        }
        catch (ArgumentException ex) { TempData["Error"] = ex.Message; }
        return RedirectToAction("Index");
    }

    private string Actor => User.Identity?.Name ?? "operator";
}

public sealed record AlertsIndexVm(
    IReadOnlyList<Alert> Items, IReadOnlyList<Device> Devices,
    AlertSeverity? Severity, bool? OnlyActive, Guid? DeviceId, DateTime? From, DateTime? To);