using AtlasNOC.Domain.Entities;
using AtlasNOC.Domain.Services.Interfaces;
using AtlasNOC.Domain.ValueObjects;
using AtlasNOC.Services.Ui;
using AtlasNOC.Web.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace AtlasNOC.Web.Controllers.Ui;

[Route("discovery")]
[Authorize(AuthenticationSchemes = AtlasNocUiAuth.UiScheme, Policy = "ReadOnly")]
public sealed class DiscoveryUiController : Controller
{
    private readonly IDiscoveryService _discoveryService;
    private readonly ICredentialService _credentialService;
    private readonly IDeviceService _deviceService;
    private readonly IRepository<DiscoveryRun> _runRepository;
    private readonly IAuditService _auditService;
    private readonly ILogger<DiscoveryUiController> _logger;

    public DiscoveryUiController(IDiscoveryService discoveryService,
        ICredentialService credentialService, IDeviceService deviceService,
        IRepository<DiscoveryRun> runRepository, IAuditService auditService,
        ILogger<DiscoveryUiController> logger)
    {
        _discoveryService = discoveryService;
        _credentialService = credentialService;
        _deviceService = deviceService;
        _runRepository = runRepository;
        _auditService = auditService;
        _logger = logger;
    }

    [HttpGet("")]
    public async Task<IActionResult> Index(CancellationToken ct = default)
    {
        var runs = (await _runRepository.GetAllAsync(ct))
            .OrderByDescending(r => r.StartedAt).Take(20).ToList();
        var discovered = await _discoveryService.GetDiscoveredDevicesAsync(ct);
        var credentials = await _credentialService.GetActiveAsync(ct);
        var monitored = await _deviceService.GetAllAsync(ct);
        var monitoredIps = new System.Collections.Generic.HashSet<string>(
            monitored.Select(d => d.IpAddress), StringComparer.OrdinalIgnoreCase);

        ViewData["Nav"] = "Discovery";
        return View(new DiscoveryIndexVm(runs, discovered, credentials, monitoredIps));
    }

    [HttpPost("start")]
    [Authorize(AuthenticationSchemes = AtlasNocUiAuth.UiScheme, Policy = "OperatorOrAdmin")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Start(StartDiscoveryUiRequest request, CancellationToken ct = default)
    {
        if (!ModelState.IsValid)
            return RedirectToAction("Index");

        try
        {
            var domainRequest = new DiscoveryRequest(
                request.SubnetCidr,
                request.CredentialIds?.Select(CredentialId.From) ?? Array.Empty<CredentialId>(),
                new DiscoveryOptions(
                    request.MaxConcurrency,
                    TimeSpan.FromSeconds(2),
                    TimeSpan.FromSeconds(5),
                    request.EnableLldp, request.EnableCdp, request.EnableArp,
                    Array.Empty<int>(), 1024)
            );
            var result = await _discoveryService.DiscoverAsync(domainRequest, ct);
            TempData["DiscoveryMessage"] =
                $"Se escanearon {result.TargetsScanned} objetivos; {result.TargetsReachable} alcanzables; {result.Devices.Count} dispositivos detectados.";
        }
        catch (ArgumentException ex)
        {
            TempData["Error"] = ex.Message;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Discovery failed");
            TempData["Error"] = "El descubrimiento no pudo completarse.";
        }
        return RedirectToAction("Index");
    }

    [HttpPost("promote/{id:guid}")]
    [Authorize(AuthenticationSchemes = AtlasNocUiAuth.UiScheme, Policy = "OperatorOrAdmin")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Promote(Guid id, CancellationToken ct = default)
    {
        var discovered = await _discoveryService.GetDiscoveredDeviceAsync(id, ct);
        if (discovered is null) return NotFound();

        try
        {
            var name = !string.IsNullOrWhiteSpace(discovered.Hostname)
                ? discovered.Hostname.Trim()
                : discovered.IpAddress;
            var type = discovered.DeviceType ?? Domain.Enums.DeviceType.Unknown;
            var existing = await _deviceService.GetAllAsync(ct);
            if (existing.Any(d => string.Equals(d.IpAddress, discovered.IpAddress, StringComparison.OrdinalIgnoreCase)))
            {
                TempData["Error"] = "Ya existe un dispositivo monitoreado con esa dirección IP.";
                return RedirectToAction("Index");
            }
            await _deviceService.CreateAsync(name, discovered.IpAddress, type, Actor,
                null, string.IsNullOrWhiteSpace(discovered.SysDescr) ? null : discovered.SysDescr, ct);
            await _auditService.LogSuccessAsync("Device", "PromoteFromDiscovery", Actor,
                userRole: User.FindFirstValue(System.Security.Claims.ClaimTypes.Role),
                targetResource: discovered.IpAddress, targetResourceType: "Device");
            TempData["DiscoveryMessage"] = $"Se agregó {name} a monitoreo.";
        }
        catch (ArgumentException ex)
        {
            TempData["Error"] = ex.Message;
        }
        return RedirectToAction("Index");
    }

    private string Actor => User.Identity?.Name ?? "operator";
}

public sealed record DiscoveryIndexVm(
    IReadOnlyList<DiscoveryRun> Runs,
    System.Collections.Generic.IReadOnlyList<DiscoveredDevice> Discovered,
    IReadOnlyList<Credential> Credentials,
    System.Collections.Generic.HashSet<string> MonitoredIps);