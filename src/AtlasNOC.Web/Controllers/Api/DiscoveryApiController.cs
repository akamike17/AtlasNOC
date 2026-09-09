using AtlasNOC.Application.Dtos;
using AtlasNOC.Application.Services;
using AtlasNOC.Web.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace AtlasNOC.Web.Controllers.Api;

/// <summary>API de descubrimiento. Lectura con scope discovery.run implícito (discovery.run para disparar).</summary>
[ApiController]
[Route("api/discovery")]
[Authorize(AuthenticationSchemes = "Identity.Application,ApiKey")]
[EnableRateLimiting("api")]
public class DiscoveryApiController : ControllerBase
{
    private readonly IDiscoveryService _discovery;
    private readonly IAuditService _audit;

    public DiscoveryApiController(IDiscoveryService discovery, IAuditService audit)
    {
        _discovery = discovery;
        _audit = audit;
    }

    /// <summary>Inicia un discovery run. Requiere scope discovery.run (API key) O rol humano NocOperator/Administrator.</summary>
    [HttpPost("run")]
    [Authorize(Policy = "Api.DiscoveryRun")]
    public async Task<ActionResult<Guid>> Start(StartDiscoveryRequest request, CancellationToken ct)
    {
        var runId = await _discovery.StartDiscoveryAsync(request, ct);
        await _audit.RecordAsync("Discovery", "Start", ApiActor.Id(User), ApiActor.Name(User),
            ApiActor.Role(User), runId.ToString(), "DiscoveryRun", ct);
        return Accepted(new { runId });
    }

    /// <summary>Lista discovery runs. Requiere scope discovery.run (API key) O rol humano NocOperator/Administrator/ReadOnly.</summary>
    [HttpGet("runs")]
    [Authorize(Policy = "Api.DiscoveryRead")]
    public async Task<ActionResult<IReadOnlyList<DiscoveryRunDto>>> List(CancellationToken ct)
        => Ok(await _discovery.ListRunsAsync(ct));

    /// <summary>Obtiene un discovery run. Requiere scope discovery.run (API key) O rol humano NocOperator/Administrator/ReadOnly.</summary>
    [HttpGet("runs/{id:guid}")]
    [Authorize(Policy = "Api.DiscoveryRead")]
    public async Task<ActionResult<DiscoveryRunDto>> Get(Guid id, CancellationToken ct)
    {
        var run = await _discovery.GetRunAsync(id, ct);
        return run is null ? NotFound() : Ok(run);
    }

    /// <summary>Cancela un discovery run. Requiere scope discovery.run (API key) O rol humano NocOperator/Administrator.</summary>
    [HttpPost("runs/{id:guid}/cancel")]
    [Authorize(Policy = "Api.DiscoveryRun")]
    public async Task<IActionResult> Cancel(Guid id, CancellationToken ct)
    {
        await _discovery.CancelAsync(id, ct);
        await _audit.RecordAsync("Discovery", "Cancel", ApiActor.Id(User), ApiActor.Name(User),
            ApiActor.Role(User), id.ToString(), "DiscoveryRun", ct);
        return NoContent();
    }
}