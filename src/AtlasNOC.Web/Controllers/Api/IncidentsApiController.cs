using AtlasNOC.Application.Dtos;
using AtlasNOC.Application.Services;
using AtlasNOC.Web.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace AtlasNOC.Web.Controllers.Api;

/// <summary>API de incidentes. Lectura incidents.read; resolución incidents.write.</summary>
[ApiController]
[Route("api/incidents")]
[Authorize(AuthenticationSchemes = "Identity.Application,ApiKey")]
[EnableRateLimiting("api")]
public class IncidentsApiController : ControllerBase
{
    private readonly IIncidentService _incidents;
    private readonly IAuditService _audit;

    public IncidentsApiController(IIncidentService incidents, IAuditService audit)
    {
        _incidents = incidents;
        _audit = audit;
    }

    [HttpGet]
    [Authorize(Policy = ApiScopes.IncidentsRead)]
    public async Task<ActionResult<IReadOnlyList<IncidentDto>>> List([FromQuery] bool activeOnly = true, CancellationToken ct = default)
        => Ok(await _incidents.ListIncidentsAsync(activeOnly, ct));

    [HttpPost("{id:guid}/resolve")]
    [Authorize(Policy = ApiScopes.IncidentsWrite)]
    public async Task<IActionResult> Resolve(Guid id, CancellationToken ct)
    {
        await _incidents.ResolveAsync(id, ApiActor.Name(User), ct);
        await _audit.RecordAsync("Incident", "Resolve", ApiActor.Id(User), ApiActor.Name(User),
            ApiActor.Role(User), id.ToString(), "Incident", ct);
        return NoContent();
    }
}