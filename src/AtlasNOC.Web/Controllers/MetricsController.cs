using AtlasNOC.Application.Services;
using AtlasNOC.Domain.Identity;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AtlasNOC.Web.Controllers;

// Lectura abierta a operadores y read-only; escritura restringida por acción.
[Authorize(Roles = ApplicationRole.Administrator + "," + ApplicationRole.NocOperator + "," + ApplicationRole.ReadOnly)]
public class MetricsController : Controller
{
    private readonly IMetricQueryService _metrics;
    private readonly IDeviceService _devices;
    private readonly IInterfaceService _interfaces;

    public MetricsController(IMetricQueryService metrics, IDeviceService devices, IInterfaceService interfaces)
    {
        _metrics = metrics;
        _devices = devices;
        _interfaces = interfaces;
    }

    [HttpGet("metrics")]
    public async Task<IActionResult> Index()
    {
        ViewBag.Devices = await _devices.ListDevicesAsync();
        return View();
    }

    [HttpGet("metrics/device/{id}")]
    public async Task<IActionResult> Device(Guid id)
    {
        var device = await _devices.GetDeviceAsync(id);
        if (device is null) return NotFound();
        ViewBag.Device = device;
        return View();
    }

    [HttpGet("metrics/interface/{id}")]
    public async Task<IActionResult> Interface(Guid id)
    {
        var iface = await _interfaces.GetInterfaceAsync(id);
        if (iface is null) return NotFound();
        ViewBag.Interface = iface;
        return View();
    }
}