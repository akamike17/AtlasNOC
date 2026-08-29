using AtlasNOC.Application.Dtos;
using AtlasNOC.Application.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AtlasNOC.Web.Controllers;

[Authorize]
public class SitesController : Controller
{
    private readonly ISiteService _sites;

    public SitesController(ISiteService sites) => _sites = sites;

    [HttpGet]
    public async Task<IActionResult> Index()
        => View(await _sites.ListSitesAsync());

    [HttpGet]
    public IActionResult Create() => View();

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(CreateSiteRequest request)
    {
        await _sites.CreateSiteAsync(request);
        return RedirectToAction(nameof(Index));
    }
}