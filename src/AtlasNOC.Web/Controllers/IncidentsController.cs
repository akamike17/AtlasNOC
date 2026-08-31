using AtlasNOC.Application.Services;
using AtlasNOC.Domain.Identity;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AtlasNOC.Web.Controllers;

// Lectura abierta a operadores y read-only; resolver restringido.
[Authorize(Roles = ApplicationRole.Administrator + "," + ApplicationRole.NocOperator + "," + ApplicationRole.ReadOnly)]
public class IncidentsController : Controller
{
    private readonly IIncidentService _incidents;
    private readonly IAuditService _audit;

    public IncidentsController(IIncidentService incidents, IAuditService audit)
    {
        _incidents = incidents;
        _audit = audit;
    }

    [HttpGet]
    public async Task<IActionResult> Index(bool activeOnly = true)
        => View(await _incidents.ListIncidentsAsync(activeOnly));

    [HttpPost]
    [Authorize(Roles = ApplicationRole.Administrator + "," + ApplicationRole.NocOperator)]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Resolve(Guid id)
    {
        await _incidents.ResolveAsync(id, User.Identity?.Name ?? "system");
        await _audit.RecordAsync("Incident", "Resolve", User.Identity?.Name ?? "", User.Identity?.Name ?? "",
            User.IsInRole(ApplicationRole.Administrator) ? ApplicationRole.Administrator : ApplicationRole.NocOperator,
            id.ToString(), "Incident");
        return RedirectToAction(nameof(Index));
    }
}