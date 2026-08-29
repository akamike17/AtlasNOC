using AtlasNOC.Application.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AtlasNOC.Web.Controllers;

[Authorize]
public class AlertsController : Controller
{
    private readonly IAlertService _alerts;

    public AlertsController(IAlertService alerts) => _alerts = alerts;

    [HttpGet]
    public async Task<IActionResult> Index(bool openOnly = true)
        => View(await _alerts.ListAlertsAsync(openOnly));

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Acknowledge(Guid id)
    {
        await _alerts.AcknowledgeAsync(id, User.Identity?.Name ?? "system");
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Resolve(Guid id)
    {
        await _alerts.ResolveAsync(id, User.Identity?.Name ?? "system");
        return RedirectToAction(nameof(Index));
    }
}