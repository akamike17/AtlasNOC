using AtlasNOC.Domain.Services.Interfaces;
using AtlasNOC.Domain.ValueObjects;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AtlasNOC.Web.Controllers;

[ApiController]
[Route("api/devices/{deviceId:guid}/metrics")]
[Authorize(Policy = "ReadOnly")]
public sealed class MetricsController : ControllerBase
{
    private readonly IMetricHistoryService _history;
    public MetricsController(IMetricHistoryService history) => _history = history;

    [HttpGet]
    public async Task<IActionResult> Get(Guid deviceId, [FromQuery] DateTime from, [FromQuery] DateTime to,
        [FromQuery] int page = 1, [FromQuery] int pageSize = 100,
        CancellationToken cancellationToken = default)
    {
        try
        {
            return Ok(await _history.QueryAsync(DeviceId.From(deviceId), from.ToUniversalTime(),
                to.ToUniversalTime(), page, pageSize, cancellationToken));
        }
        catch (ArgumentException exception)
        {
            return BadRequest(new { error = exception.Message });
        }
    }
}
