using AtlasNOC.Application.Dtos;
using AtlasNOC.Application.Services;
using AtlasNOC.Domain.Identity;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AtlasNOC.Web.Controllers;

// Lectura abierta a operadores y read-only; escritura restringida por acción.
[Authorize(Roles = ApplicationRole.Administrator + "," + ApplicationRole.NocOperator + "," + ApplicationRole.ReadOnly)]
public class InterfacesController : Controller
{
    private readonly IInterfaceService _interfaces;
    private readonly IDeviceService _devices;
    private readonly IAuditService _audit;

    public InterfacesController(IInterfaceService interfaces, IDeviceService devices, IAuditService audit)
    {
        _interfaces = interfaces;
        _devices = devices;
        _audit = audit;
    }

    [HttpGet("interfaces/device/{deviceId}")]
    public async Task<IActionResult> ByDevice(Guid deviceId)
    {
        var device = await _devices.GetDeviceAsync(deviceId);
        var interfaces = await _interfaces.ListByDeviceAsync(deviceId);
        ViewBag.Device = device;
        ViewBag.DeviceId = deviceId;
        return View("Index", interfaces);
    }

    [HttpGet("interfaces")]
    public IActionResult Index() => RedirectToAction("Index", "Devices");

    [HttpGet("interfaces/{id}")]
    public async Task<IActionResult> Detail(Guid id)
    {
        var iface = await _interfaces.GetInterfaceAsync(id);
        if (iface is null) return NotFound();
        return View(iface);
    }
}