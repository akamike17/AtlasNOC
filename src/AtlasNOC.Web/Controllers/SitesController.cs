using AtlasNOC.Application.Dtos;
using AtlasNOC.Application.Services;
using AtlasNOC.Domain.Identity;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AtlasNOC.Web.Controllers;

// Lectura abierta a operadores y read-only; escritura restringida por acción.
[Authorize(Roles = ApplicationRole.Administrator + "," + ApplicationRole.NocOperator + "," + ApplicationRole.ReadOnly)]
public class SitesController : Controller
{
    private readonly ISiteService _sites;

    public SitesController(ISiteService sites) => _sites = sites;

    [HttpGet]
    public async Task<IActionResult> Index()
        => View(await _sites.ListSitesAsync());

    [HttpGet]
    [Authorize(Roles = ApplicationRole.Administrator + "," + ApplicationRole.NocOperator)]
    public IActionResult Create() => View();

    [HttpPost]
    [Authorize(Roles = ApplicationRole.Administrator + "," + ApplicationRole.NocOperator)]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(CreateSiteRequest request)
    {
        await _sites.CreateSiteAsync(request);
        return RedirectToAction(nameof(Index));
    }
}