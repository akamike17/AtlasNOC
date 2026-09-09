using AtlasNOC.Application.Dtos;
using AtlasNOC.Application.Services;
using AtlasNOC.Web.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace AtlasNOC.Web.Controllers.Api;

/// <summary>API de alertas. Lectura alerts.read; acknowledge/resolve alerts.write.</summary>
[ApiController]
[Route("api/alerts")]
[Authorize(AuthenticationSchemes = "Identity.Application,ApiKey")]
[EnableRateLimiting("api")]
public class AlertsApiController : ControllerBase
{
    private readonly IAlertService _alerts;
    private readonly IAuditService _audit;

    public AlertsApiController(IAlertService alerts, IAuditService audit)
    {
        _alerts = alerts;
        _audit = audit;
    }

    [HttpGet]
    [Authorize(Policy = "Api.AlertsRead")]
    public async Task<ActionResult<IReadOnlyList<AlertDto>>> List([FromQuery] bool openOnly = false, CancellationToken ct = default)
        => Ok(await _alerts.ListAlertsAsync(openOnly, ct));

    [HttpPost("{id:guid}/acknowledge")]
    [Authorize(Policy = "Api.AlertsWrite")]
    public async Task<IActionResult> Acknowledge(Guid id, CancellationToken ct)
    {
        await _alerts.AcknowledgeAsync(id, ApiActor.Name(User), ct);
        await _audit.RecordAsync("Alert", "Acknowledge", ApiActor.Id(User), ApiActor.Name(User),
            ApiActor.Role(User), id.ToString(), "Alert", ct);
        return NoContent();
    }

    [HttpPost("{id:guid}/resolve")]
    [Authorize(Policy = "Api.AlertsWrite")]
    public async Task<IActionResult> Resolve(Guid id, CancellationToken ct)
    {
        await _alerts.ResolveAsync(id, ApiActor.Name(User), ct);
        await _audit.RecordAsync("Alert", "Resolve", ApiActor.Id(User), ApiActor.Name(User),
            ApiActor.Role(User), id.ToString(), "Alert", ct);
        return NoContent();
    }
}