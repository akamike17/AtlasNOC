using AtlasNOC.Application.Dtos;
using AtlasNOC.Application.Services;
using AtlasNOC.Web.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace AtlasNOC.Web.Controllers.Api;

/// <summary>API de sitios/torres. Lectura con scope sites.read; creación con sites.write.</summary>
[ApiController]
[Route("api/sites")]
[Authorize(AuthenticationSchemes = "Identity.Application,ApiKey")]
[EnableRateLimiting("api")]
public class SitesApiController : ControllerBase
{
    private readonly ISiteService _sites;
    private readonly IAuditService _audit;

    public SitesApiController(ISiteService sites, IAuditService audit)
    {
        _sites = sites;
        _audit = audit;
    }

    [HttpGet]
    [Authorize(Policy = "Api.SitesRead")]
    public async Task<ActionResult<IReadOnlyList<SiteDto>>> List(CancellationToken ct)
        => Ok(await _sites.ListSitesAsync(ct));

    [HttpGet("{id:guid}")]
    [Authorize(Policy = "Api.SitesRead")]
    public async Task<ActionResult<SiteDto>> Get(Guid id, CancellationToken ct)
    {
        var site = await _sites.GetSiteAsync(id, ct);
        return site is null ? NotFound() : Ok(site);
    }

    [HttpPost]
    [Authorize(Policy = "Api.SitesWrite")]
    public async Task<ActionResult<SiteDto>> Create(CreateSiteRequest request, CancellationToken ct)
    {
        var site = await _sites.CreateSiteAsync(request, ct);
        await _audit.RecordAsync("Site", "Create", ApiActor.Id(User), ApiActor.Name(User),
            ApiActor.Role(User), site.Id.ToString(), "Site", ct);
        return CreatedAtAction(nameof(Get), new { id = site.Id }, site);
    }
}