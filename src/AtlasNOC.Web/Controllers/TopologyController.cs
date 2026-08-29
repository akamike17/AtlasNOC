using AtlasNOC.Application.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AtlasNOC.Web.Controllers;

[Authorize]
public class TopologyController : Controller
{
    private readonly ITopologyService _topology;

    public TopologyController(ITopologyService topology) => _topology = topology;

    [HttpGet]
    public async Task<IActionResult> Index()
    {
        var graph = await _topology.GetGraphAsync(null);
        return View(graph);
    }
}