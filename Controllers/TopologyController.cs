using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using AtlasNOC.Domain.Services.Interfaces;

namespace AtlasNOC.Web.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(Policy = "ReadOnly")]
public class TopologyController : ControllerBase
{
    private readonly ITopologyService _topology;

    public TopologyController(ITopologyService topology) =>
        _topology = topology ?? throw new ArgumentNullException(nameof(topology));

    /// <summary>
    /// Returns the current topology. Uses the cached/rebuilt topology without
    /// forcing a new discovery; call POST /api/topology/rebuild to refresh it.
    /// </summary>
    [HttpGet]
    public Task<TopologyMap> Get(CancellationToken ct = default)
        => _topology.GetTopologyAsync(ct);

    /// <summary>
    /// Returns the current topology filtered to nodes inside a CIDR subnet.
    /// </summary>
    [HttpGet("subnet/{subnetCidr}")]
    public async Task<IActionResult> GetSubnet(string subnetCidr, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(subnetCidr))
            return BadRequest(new { error = "A CIDR subnet is required." });
        try
        {
            return Ok(await _topology.GetTopologyForSubnetAsync(subnetCidr, ct));
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    /// <summary>
    /// Returns a single node by device id.
    /// </summary>
    [HttpGet("nodes/{deviceId:guid}")]
    public async Task<IActionResult> GetNode(Guid deviceId, CancellationToken ct = default)
    {
        var node = await _topology.GetNodeAsync(deviceId, ct);
        return node is null ? NotFound() : Ok(node);
    }

    /// <summary>
    /// Returns all links in the current topology.
    /// </summary>
    [HttpGet("links")]
    public Task<IReadOnlyList<TopologyLink>> GetLinks(CancellationToken ct = default)
        => _topology.GetLinksAsync(ct);

    /// <summary>
    /// Finds a path between two devices in the current topology.
    /// </summary>
    [HttpGet("path/{sourceId:guid}/{targetId:guid}")]
    public async Task<IActionResult> GetPath(Guid sourceId, Guid targetId, CancellationToken ct = default)
    {
        var path = await _topology.FindPathAsync(sourceId, targetId, ct);
        return path is null ? NotFound() : Ok(path);
    }

    /// <summary>
    /// Forces a fresh rebuild of the topology from persisted devices and the most
    /// recent discovery evidence, without running a new discovery scan.
    /// </summary>
    [HttpPost("rebuild")]
    [Authorize(Policy = "OperatorOrAdmin")]
    public Task<TopologyMap> Rebuild(CancellationToken ct = default)
        => _topology.RebuildTopologyAsync(ct);
}