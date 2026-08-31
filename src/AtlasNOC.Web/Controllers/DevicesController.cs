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
    private readonly IAuditService _audit;

    public DevicesController(IDeviceService devices, IAuditService audit)
    {
        _devices = devices;
        _audit = audit;
    }

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
        var device = await _devices.CreateDeviceAsync(request);
        await _audit.RecordAsync("Device", "Create", User.Identity?.Name ?? "", User.Identity?.Name ?? "",
            User.IsInRole(ApplicationRole.Administrator) ? ApplicationRole.Administrator : ApplicationRole.NocOperator,
            device.Id.ToString(), "Device");
        return RedirectToAction(nameof(Index));
    }
}