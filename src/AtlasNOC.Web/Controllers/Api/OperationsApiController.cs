using AtlasNOC.Application.Services;
using AtlasNOC.Web.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace AtlasNOC.Web.Controllers.Api;

[ApiController]
[Route("api/operations")]
[Authorize(AuthenticationSchemes = "Identity.Application,ApiKey", Policy = "Api.TopologyRead")]
[EnableRateLimiting("api")]
public sealed class OperationsApiController : ControllerBase
{
    private readonly IOperationsSnapshotService _snapshot;
    public OperationsApiController(IOperationsSnapshotService snapshot) => _snapshot = snapshot;

    [HttpGet("snapshot")]
    public Task<OperationsSnapshotDto> Snapshot(CancellationToken ct) => _snapshot.GetAsync(ct);
}
