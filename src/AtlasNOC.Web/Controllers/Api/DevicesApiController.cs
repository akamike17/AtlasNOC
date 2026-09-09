using AtlasNOC.Application.Dtos;
using AtlasNOC.Application.Services;
using AtlasNOC.Web.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace AtlasNOC.Web.Controllers.Api;

/// <summary>API de inventario de dispositivos (solo lectura; la escritura es vía MVC).</summary>
[ApiController]
[Route("api/devices")]
[Authorize(AuthenticationSchemes = "Identity.Application,ApiKey", Policy = "Api.DevicesRead")]
[EnableRateLimiting("api")]
public class DevicesApiController : ControllerBase
{
    private readonly IDeviceService _devices;

    public DevicesApiController(IDeviceService devices) => _devices = devices;

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<DeviceDto>>> List(CancellationToken ct)
        => Ok(await _devices.ListDevicesAsync(ct));

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<DeviceDto>> Get(Guid id, CancellationToken ct)
    {
        var device = await _devices.GetDeviceAsync(id, ct);
        return device is null ? NotFound() : Ok(device);
    }
}