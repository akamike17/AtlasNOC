using AtlasNOC.Application.Dtos;
using AtlasNOC.Application.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AtlasNOC.Web.Controllers.Api;

/// <summary>API de topología: expone el grafo normalizado (nodes+edges+groups) de relaciones persistidas.</summary>
[ApiController]
[Route("api/topology")]
[Authorize]
public class TopologyApiController : ControllerBase
{
    private readonly ITopologyService _topology;

    public TopologyApiController(ITopologyService topology) => _topology = topology;

    /// <summary>Grafo completo (o filtrado). Solo relaciones persistidas, nunca deducidas visualmente.</summary>
    [HttpGet("graph")]
    public async Task<ActionResult<TopologyGraphDto>> GetGraph(
        [FromQuery] Guid? siteId,
        [FromQuery] int? status,
        [FromQuery] int? deviceType,
        [FromQuery] int? vendor,
        [FromQuery] string? q,
        [FromQuery] bool hideCpe = false)
    {
        var filter = new TopologyFilter(siteId, status, deviceType, vendor, q, hideCpe);
        var graph = await _topology.GetGraphAsync(filter);
        return Ok(graph);
    }
}