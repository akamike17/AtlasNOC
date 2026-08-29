using AtlasNOC.Application.Dtos;
using AtlasNOC.Application.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AtlasNOC.Web.Controllers;

[Authorize(Roles = "Administrator,NocOperator")]
public class DiscoveryController : Controller
{
    private readonly IDiscoveryService _discovery;
    private readonly ISiteService _sites;
    private readonly ICredentialService _credentials;

    public DiscoveryController(IDiscoveryService discovery, ISiteService sites, ICredentialService credentials)
    {
        _discovery = discovery;
        _sites = sites;
        _credentials = credentials;
    }

    [HttpGet]
    public async Task<IActionResult> Index()
        => View(await _discovery.ListRunsAsync());

    [HttpGet]
    public async Task<IActionResult> Start()
    {
        ViewBag.Sites = await _sites.ListSitesAsync();
        ViewBag.Credentials = await _credentials.ListCredentialsAsync();
        return View();
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Start(StartDiscoveryRequest request)
    {
        var id = await _discovery.StartDiscoveryAsync(request);
        return RedirectToAction(nameof(Index));
    }
}