using AtlasNOC.Application.Dtos;
using AtlasNOC.Application.Services;
using AtlasNOC.Web.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace AtlasNOC.Web.Controllers.Api;

/// <summary>API de salud del sistema (solo lectura con scope system.read).</summary>
[ApiController]
[Route("api/system")]
[Authorize(AuthenticationSchemes = "Identity.Application,ApiKey", Policy = "Api.SystemRead")]
[EnableRateLimiting("api")]
public class SystemApiController : ControllerBase
{
    private readonly ISystemHealthService _health;

    public SystemApiController(ISystemHealthService health) => _health = health;

    [HttpGet("health")]
    public async Task<ActionResult<SystemHealthDto>> Health(CancellationToken ct)
        => Ok(await _health.GetHealthAsync(ct));
}