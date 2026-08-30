using AtlasNOC.Application.Dtos;
using AtlasNOC.Application.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace AtlasNOC.Web.Controllers.Api;

/// <summary>API de series de tiempo para gráficas (Chart.js).</summary>
[ApiController]
[Route("api/metrics")]
[Authorize]
[EnableRateLimiting("api")]
public class MetricsApiController : ControllerBase
{
    private readonly IMetricQueryService _metrics;

    public MetricsApiController(IMetricQueryService metrics) => _metrics = metrics;

    /// <summary>Serie de una métrica para un recurso (e.g. Device) en un intervalo.</summary>
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<MetricPointDto>>> GetSeries(
        [FromQuery] string resourceType,
        [FromQuery] string resourceId,
        [FromQuery] string metric,
        [FromQuery] DateTime? from,
        [FromQuery] DateTime? to)
    {
        var now = DateTime.UtcNow;
        var fromUtc = from ?? now.Subtract(TimeSpan.FromHours(24));
        var toUtc = to ?? now;

        var points = await _metrics.QueryAsync(resourceType, resourceId, metric, fromUtc, toUtc);
        return Ok(points);
    }
}