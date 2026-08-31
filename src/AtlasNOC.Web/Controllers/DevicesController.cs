using AtlasNOC.Application.Dtos;
using AtlasNOC.Application.Services;
using AtlasNOC.Domain.Identity;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AtlasNOC.Web.Controllers;

// Lectura abierta a operadores y read-only; escritura restringida por acción.
[Authorize(Roles = ApplicationRole.Administrator + "," + ApplicationRole.NocOperator + "," + ApplicationRole.ReadOnly)]
public class DevicesController : Controller
{
    private readonly IDeviceService _devices;

    public DevicesController(IDeviceService devices) => _devices = devices;

    [HttpGet]
    public async Task<IActionResult> Index() => View(await _devices.ListDevicesAsync());

    [HttpGet]
    public async Task<IActionResult> Detail(Guid id)
    {
        var device = await _devices.GetDeviceAsync(id);
        if (device is null) return NotFound();
        return View(device);
    }

    [HttpGet]
    [Authorize(Roles = ApplicationRole.Administrator + "," + ApplicationRole.NocOperator)]
    public IActionResult Create() => View();

    [HttpPost]
    [Authorize(Roles = ApplicationRole.Administrator + "," + ApplicationRole.NocOperator)]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(CreateDeviceRequest request)
    {
        await _devices.CreateDeviceAsync(request);
        return RedirectToAction(nameof(Index));
    }
}