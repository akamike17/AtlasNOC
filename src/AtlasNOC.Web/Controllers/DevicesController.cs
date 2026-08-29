using AtlasNOC.Application.Dtos;
using AtlasNOC.Application.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AtlasNOC.Web.Controllers;

[Authorize]
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
    public IActionResult Create() => View();

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(CreateDeviceRequest request)
    {
        await _devices.CreateDeviceAsync(request);
        return RedirectToAction(nameof(Index));
    }
}