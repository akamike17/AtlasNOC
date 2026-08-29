using AtlasNOC.Domain.Entities;
using AtlasNOC.Domain.Enums;
using AtlasNOC.Domain.Services.Interfaces;
using AtlasNOC.Domain.ValueObjects;
using AtlasNOC.Services.Ui;
using AtlasNOC.Web.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MySqlConnector;
using System.Security.Claims;

namespace AtlasNOC.Web.Controllers.Ui;

[Route("devices")]
[Authorize(AuthenticationSchemes = AtlasNocUiAuth.UiScheme, Policy = "ReadOnly")]
public sealed class DevicesUiController : Controller
{
    private readonly IDeviceService _deviceService;
    private readonly IAlertService _alertService;
    private readonly IMetricHistoryService _metricHistory;
    private readonly IPollingService _pollingService;
    private readonly IAuditService _auditService;
    private readonly ILogger<DevicesUiController> _logger;

    public DevicesUiController(IDeviceService deviceService, IAlertService alertService,
        IMetricHistoryService metricHistory, IPollingService pollingService,
        IAuditService auditService, ILogger<DevicesUiController> logger)
    {
        _deviceService = deviceService;
        _alertService = alertService;
        _metricHistory = metricHistory;
        _pollingService = pollingService;
        _auditService = auditService;
        _logger = logger;
    }

    [HttpGet("")]
    public async Task<IActionResult> Index(string? search = null, DeviceStatus? status = null,
        DeviceType? type = null, int page = 1, CancellationToken ct = default)
    {
        page = page < 1 ? 1 : page;
        const int pageSize = 20;
        var all = string.IsNullOrWhiteSpace(search)
            ? await _deviceService.GetAllAsync(ct)
            : await _deviceService.SearchAsync(search.Trim(), ct);

        if (status.HasValue) all = all.Where(d => d.Status == status.Value).ToList();
        if (type.HasValue) all = all.Where(d => d.Type == type.Value).ToList();

        var ordered = all.OrderBy(d => d.IsActive == false).ThenBy(d => d.Name).ToList();
        var total = ordered.Count;
        var items = ordered.Skip((page - 1) * pageSize).Take(pageSize).ToList();
        ViewData["Nav"] = "Devices";

        return View(new DeviceIndexVm(items, total, page, pageSize, search, status, type));
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Detail(Guid id, CancellationToken ct = default)
    {
        var device = await _deviceService.GetByIdAsync(DeviceId.From(id), ct);
        if (device is null) return NotFound();
        var alerts = await _alertService.GetAlertsForDeviceAsync(DeviceId.From(id), ct);
        ViewData["Nav"] = "Devices";
        return View(new DeviceDetailVm(device, alerts.OrderByDescending(a => a.OccurredAt).ToList()));
    }

    [HttpGet("new")]
    [Authorize(AuthenticationSchemes = AtlasNocUiAuth.UiScheme, Policy = "OperatorOrAdmin")]
    public IActionResult Create()
    {
        ViewData["Nav"] = "Devices";
        return View(new CreateDeviceUiRequest());
    }

    [HttpPost("new")]
    [Authorize(AuthenticationSchemes = AtlasNocUiAuth.UiScheme, Policy = "OperatorOrAdmin")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(CreateDeviceUiRequest request, CancellationToken ct = default)
    {
        if (!ModelState.IsValid)
        {
            ViewData["Nav"] = "Devices";
            return View(request);
        }
        try
        {
            var device = await _deviceService.CreateAsync(
                request.Name, request.IpAddress, request.Type, Actor,
                request.Location, request.Description, ct);
            await LogAudit("Device", "Create", device.Id.Value.ToString());
            return RedirectToAction("Detail", new { id = device.Id.Value });
        }
        catch (ArgumentException ex)
        {
            ModelState.AddModelError(string.Empty, ex.Message);
            ViewData["Nav"] = "Devices";
            return View(request);
        }
        catch (DbUpdateException ex) when (ex.InnerException is MySqlException { Number: 1062 })
        {
            _logger.LogInformation(ex, "Device creation rejected because IP {IpAddress} already exists", request.IpAddress);
            ModelState.AddModelError(nameof(request.IpAddress), "Ya existe un dispositivo con esta dirección IP.");
            ViewData["Nav"] = "Devices";
            Response.StatusCode = StatusCodes.Status409Conflict;
            return View(request);
        }
    }

    [HttpGet("{id:guid}/edit")]
    [Authorize(AuthenticationSchemes = AtlasNocUiAuth.UiScheme, Policy = "OperatorOrAdmin")]
    public async Task<IActionResult> Edit(Guid id, CancellationToken ct = default)
    {
        var device = await _deviceService.GetByIdAsync(DeviceId.From(id), ct);
        if (device is null) return NotFound();
        ViewData["Nav"] = "Devices";
        ViewData["DeviceId"] = id;
        return View(new EditDeviceUiRequest { Name = device.Name, Location = device.Location, Description = device.Description });
    }

    [HttpPost("{id:guid}/edit")]
    [Authorize(AuthenticationSchemes = AtlasNocUiAuth.UiScheme, Policy = "OperatorOrAdmin")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(Guid id, EditDeviceUiRequest request, CancellationToken ct = default)
    {
        if (!ModelState.IsValid)
        {
            ViewData["Nav"] = "Devices";
            return View(request);
        }
        try
        {
            await _deviceService.UpdateDetailsAsync(DeviceId.From(id), request.Name, request.Location, request.Description, Actor, ct);
            await LogAudit("Device", "UpdateDetails", id.ToString());
            return RedirectToAction("Detail", new { id });
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
    }

    [HttpPost("{id:guid}/deactivate")]
    [Authorize(AuthenticationSchemes = AtlasNocUiAuth.UiScheme, Policy = "OperatorOrAdmin")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Deactivate(Guid id, CancellationToken ct = default)
    {
        try
        {
            await _deviceService.DeactivateAsync(DeviceId.From(id), Actor, ct);
            await LogAudit("Device", "Deactivate", id.ToString());
            return RedirectToAction("Detail", new { id });
        }
        catch (KeyNotFoundException) { return NotFound(); }
    }

    [HttpPost("{id:guid}/reactivate")]
    [Authorize(AuthenticationSchemes = AtlasNocUiAuth.UiScheme, Policy = "OperatorOrAdmin")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Reactivate(Guid id, CancellationToken ct = default)
    {
        try
        {
            await _deviceService.ReactivateAsync(DeviceId.From(id), Actor, ct);
            await LogAudit("Device", "Reactivate", id.ToString());
            return RedirectToAction("Detail", new { id });
        }
        catch (KeyNotFoundException) { return NotFound(); }
    }

    [HttpPost("{id:guid}/poll")]
    [Authorize(AuthenticationSchemes = AtlasNocUiAuth.UiScheme, Policy = "OperatorOrAdmin")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Poll(Guid id, CancellationToken ct = default)
    {
        try
        {
            var device = await _deviceService.GetByIdAsync(DeviceId.From(id), ct);
            if (device is null) return NotFound();

            var result = await _pollingService.PollDeviceAsync(DeviceId.From(id), ct);
            if (result.Success)
            {
                TempData["Message"] = $"Polling completado (latencia {(result.Metrics.LatencyMs?.ToString("0.#") ?? "n/d")} ms).";
            }
            else
            {
                TempData["Error"] = $"Polling no pudo completarse: {result.ErrorMessage}";
            }
            await LogAudit("Device", "PollNow", id.ToString());
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Device poll failed for {Id}", id);
            TempData["Error"] = "No se pudo completar el polling del dispositivo.";
        }
        return RedirectToAction("Detail", new { id });
    }

    private string Actor => User.Identity?.Name ?? "operator";
    private async Task LogAudit(string category, string action, string resource)
    {
        try
        {
            await _auditService.LogSuccessAsync(category, action, Actor,
                userRole: User.FindFirstValue(System.Security.Claims.ClaimTypes.Role),
                targetResource: resource, targetResourceType: category,
                ipAddress: HttpContext.Connection.RemoteIpAddress?.ToString());
        }
        catch (Exception ex) { _logger.LogWarning(ex, "Audit log failed"); }
    }
}

public sealed record DeviceIndexVm(
    IReadOnlyList<Device> Items, int Total, int Page, int PageSize,
    string? Search, DeviceStatus? Status, DeviceType? Type);

public sealed record DeviceDetailVm(Device Device, IReadOnlyList<Alert> Alerts);
