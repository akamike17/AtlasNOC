using AtlasNOC.Application.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AtlasNOC.Web.Controllers;

[Authorize]
public class IncidentsController : Controller
{
    private readonly IIncidentService _incidents;

    public IncidentsController(IIncidentService incidents) => _incidents = incidents;

    [HttpGet]
    public async Task<IActionResult> Index(bool activeOnly = true)
        => View(await _incidents.ListIncidentsAsync(activeOnly));

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Resolve(Guid id)
    {
        await _incidents.ResolveAsync(id, User.Identity?.Name ?? "system");
        return RedirectToAction(nameof(Index));
    }
}