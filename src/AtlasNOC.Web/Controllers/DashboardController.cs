using AtlasNOC.Application.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AtlasNOC.Web.Controllers;

[Authorize]
public class DashboardController : Controller
{
    private readonly ISystemHealthService _health;
    private readonly ILocalNetworkProfileService _localNetwork;
    private readonly ITopologyService _topology;

    public DashboardController(ISystemHealthService health, ILocalNetworkProfileService localNetwork, ITopologyService topology)
    {
        _health = health;
        _localNetwork = localNetwork;
        _topology = topology;
    }

    [HttpGet]
    public async Task<IActionResult> Index()
    {
        var health = await _health.GetHealthAsync();
        ViewBag.LocalProfiles = _localNetwork.GetProfiles();
        ViewBag.Topology = await _topology.GetGraphAsync(null);
        return View(health);
    }
}
