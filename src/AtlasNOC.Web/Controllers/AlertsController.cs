using AtlasNOC.Application.Services;
using AtlasNOC.Domain.Identity;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AtlasNOC.Web.Controllers;

// Lectura abierta a operadores y read-only; reconocer/resolver restringido.
[Authorize(Roles = ApplicationRole.Administrator + "," + ApplicationRole.NocOperator + "," + ApplicationRole.ReadOnly)]
public class AlertsController : Controller
{
    private readonly IAlertService _alerts;
    private readonly IAuditService _audit;

    public AlertsController(IAlertService alerts, IAuditService audit)
    {
        _alerts = alerts;
        _audit = audit;
    }

    [HttpGet]
    public async Task<IActionResult> Index(bool openOnly = true)
        => View(await _alerts.ListAlertsAsync(openOnly));

    [HttpPost]
    [Authorize(Roles = ApplicationRole.Administrator + "," + ApplicationRole.NocOperator)]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Acknowledge(Guid id)
    {
        await _alerts.AcknowledgeAsync(id, User.Identity?.Name ?? "system");
        await _audit.RecordAsync("Alert", "Acknowledge", User.Identity?.Name ?? "", User.Identity?.Name ?? "",
            User.IsInRole(ApplicationRole.Administrator) ? ApplicationRole.Administrator : ApplicationRole.NocOperator,
            id.ToString(), "Alert");
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [Authorize(Roles = ApplicationRole.Administrator + "," + ApplicationRole.NocOperator)]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Resolve(Guid id)
    {
        await _alerts.ResolveAsync(id, User.Identity?.Name ?? "system");
        await _audit.RecordAsync("Alert", "Resolve", User.Identity?.Name ?? "", User.Identity?.Name ?? "",
            User.IsInRole(ApplicationRole.Administrator) ? ApplicationRole.Administrator : ApplicationRole.NocOperator,
            id.ToString(), "Alert");
        return RedirectToAction(nameof(Index));
    }
}