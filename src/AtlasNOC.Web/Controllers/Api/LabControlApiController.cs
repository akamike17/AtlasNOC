using AtlasNOC.Application.Probes;
using System.Net;
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
        if (!IPAddress.TryParse(ipAddress, out var address)
            || address.AddressFamily != System.Net.Sockets.AddressFamily.InterNetwork)
            return BadRequest("La dirección LAB debe ser una IPv4 válida.");
        _control.SetReachability(ipAddress, reachable);
        return NoContent();
    }
}
