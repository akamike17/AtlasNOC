using AtlasNOC.Domain.Services.Interfaces;
using AtlasNOC.Domain.ValueObjects;
using AtlasNOC.Services.Ui;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AtlasNOC.Web.Controllers.Ui;

[Route("metrics")]
[Authorize(AuthenticationSchemes = AtlasNocUiAuth.UiScheme, Policy = "ReadOnly")]
public sealed class MetricsUiController : Controller
{
    private readonly IMetricHistoryService _history;
    private readonly IDeviceService _deviceService;
    private readonly ILogger<MetricsUiController> _logger;

    public MetricsUiController(IMetricHistoryService history, IDeviceService deviceService,
        ILogger<MetricsUiController> logger)
    {
        _history = history;
        _deviceService = deviceService;
        _logger = logger;
    }

    [HttpGet("")]
    public async Task<IActionResult> Index(Guid? deviceId, CancellationToken ct = default)
    {
        var devices = await _deviceService.GetActiveAsync(ct);
        ViewData["Nav"] = "Metrics";
        ViewData["SelectedDevice"] = deviceId;
        var vm = new MetricsIndexVm(devices, deviceId);
        return View(vm);
    }

    /// <summary>Aggregated series for the Metrics chart. Defaults to the last 24h.</summary>
    [HttpGet("series/{deviceId:guid}")]
    public async Task<IActionResult> Series(Guid deviceId, DateTime? from = null, DateTime? to = null,
        CancellationToken ct = default)
    {
        var now = DateTime.UtcNow;
        var fromU = from?.ToUniversalTime() ?? now.AddDays(-1);
        var toU = to?.ToUniversalTime() ?? now;
        if (fromU > toU) return BadRequest(new { error = "El rango desde es posterior al hasta." });
        if (toU - fromU > TimeSpan.FromDays(45)) return BadRequest(new { error = "El rango máximo es de 45 días." });

        try
        {
            var page = await _history.QueryAsync(DeviceId.From(deviceId), fromU, toU, 1, 2000, ct);
            return Json(new { total = page.TotalCount, items = page.Items });
        }
        catch (ArgumentException ex) { return BadRequest(new { error = ex.Message }); }
    }
}

public sealed record MetricsIndexVm(IReadOnlyList<AtlasNOC.Domain.Entities.Device> Devices, Guid? SelectedDevice);