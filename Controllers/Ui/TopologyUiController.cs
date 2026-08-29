using AtlasNOC.Domain.Services.Interfaces;
using AtlasNOC.Domain.ValueObjects;
using AtlasNOC.Services.Ui;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace AtlasNOC.Web.Controllers.Ui;

[Route("topology")]
[Authorize(AuthenticationSchemes = AtlasNocUiAuth.UiScheme, Policy = "ReadOnly")]
public sealed class TopologyUiController : Controller
{
    private readonly ITopologyService _topologyService;
    private readonly IDeviceService _deviceService;
    private readonly IAuditService _auditService;
    private readonly ILogger<TopologyUiController> _logger;

    public TopologyUiController(ITopologyService topologyService, IDeviceService deviceService,
        IAuditService auditService, ILogger<TopologyUiController> logger)
    {
        _topologyService = topologyService;
        _deviceService = deviceService;
        _auditService = auditService;
        _logger = logger;
    }

    [HttpGet("")]
    public IActionResult Index()
    {
        ViewData["Nav"] = "Topology";
        return View();
    }

    /// <summary>JSON consumed by the Cytoscape canvas (nodes + links, no secrets).</summary>
    [HttpGet("json")]
    public async Task<IActionResult> Json(CancellationToken ct = default)
    {
        try
        {
            var map = await _topologyService.GetTopologyAsync(ct);
            return Json(map);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Topology json failed");
            return StatusCode(503, new { error = "Topología no disponible" });
        }
    }

    [HttpPost("rebuild")]
    [Authorize(AuthenticationSchemes = AtlasNocUiAuth.UiScheme, Policy = "OperatorOrAdmin")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Rebuild(CancellationToken ct = default)
    {
        try
        {
            await _topologyService.RebuildTopologyAsync(ct);
            await _auditService.LogSuccessAsync("Topology", "Rebuild", Actor,
                userRole: User.FindFirstValue(System.Security.Claims.ClaimTypes.Role),
                ipAddress: HttpContext.Connection.RemoteIpAddress?.ToString());
            return RedirectToAction("Index");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Topology rebuild failed");
            TempData["Error"] = "No se pudo reconstruir la topología.";
            return RedirectToAction("Index");
        }
    }

    private string Actor => User.Identity?.Name ?? "operator";
}