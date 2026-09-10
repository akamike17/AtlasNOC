using AtlasNOC.Application.Probes;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AtlasNOC.Web.Controllers.Api;

[ApiController]
[Route("api/lab/control")]
[Authorize(Roles = "Administrator")]
public sealed class LabControlApiController : ControllerBase
{
    private readonly IWebHostEnvironment _environment;
    private readonly ILabNetworkControl _control;

    public LabControlApiController(IWebHostEnvironment environment, ILabNetworkControl control)
    {
        _environment = environment;
        _control = control;
    }

    [HttpPost("{ipAddress}/reachable/{reachable:bool}")]
    public IActionResult Set(string ipAddress, bool reachable)
    {
        if (!_environment.IsEnvironment("Testing")) return NotFound();
        _control.SetReachability(ipAddress, reachable);
        return NoContent();
    }
}
