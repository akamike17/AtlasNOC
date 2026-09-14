using AtlasNOC.Application.Dtos;
using AtlasNOC.Application.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AtlasNOC.Web.Controllers;

[Authorize(Roles = "Administrator,NocOperator")]
public class DiscoveryController : Controller
{
    private readonly IDiscoveryService _discovery;
    private readonly ISiteService _sites;
    private readonly ICredentialService _credentials;
    private readonly IAuditService _audit;
    private readonly ILocalNetworkProfileService _localNetwork;
    private readonly ILocalNetworkContextService _networkContext;

    public DiscoveryController(IDiscoveryService discovery, ISiteService sites, ICredentialService credentials,
        IAuditService audit, ILocalNetworkProfileService localNetwork, ILocalNetworkContextService networkContext)
    {
        _discovery = discovery;
        _sites = sites;
        _credentials = credentials;
        _audit = audit;
        _localNetwork = localNetwork;
        _networkContext = networkContext;
    }

    [HttpGet]
    public async Task<IActionResult> Index()
        => View(await _discovery.ListRunsAsync());

    [HttpGet("/discover")]
    public async Task<IActionResult> Start()
    {
        ViewBag.Sites = await _sites.ListSitesAsync();
        ViewBag.Credentials = await _credentials.ListCredentialsAsync();
        ViewBag.LocalProfiles = _localNetwork.GetProfiles();
        ViewBag.NetworkContext = _networkContext.GetPreferredContext();
        return View();
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Start(StartDiscoveryRequest request)
    {
        var id = await _discovery.StartDiscoveryAsync(request);
        await _audit.RecordAsync("Discovery", "Start", User.Identity?.Name ?? "", User.Identity?.Name ?? "",
            User.IsInRole("Administrator") ? "Administrator" : "NocOperator",
            id.ToString(), "DiscoveryRun");
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DetectLocalNetwork()
    {
        var profile = _localNetwork.GetProfiles().FirstOrDefault();
        if (profile is null)
        {
            TempData["DiscoveryError"] = "No se encontró una interfaz IPv4 activa para proponer una red.";
            return RedirectToAction(nameof(Index));
        }

        // La detección automática debe reutilizar una credencial SNMP activa
        // cuando exista; de lo contrario el Worker sólo puede hacer presencia
        // ICMP/ARP y siempre caerá en generic-snmp.
        var credentials = await _credentials.ListCredentialsAsync();
        var credential = credentials
            .Where(c => c.IsActive)
            .OrderByDescending(c => c.Name.Equals("Windows-SNMP", StringComparison.OrdinalIgnoreCase))
            .ThenBy(c => c.Name, StringComparer.OrdinalIgnoreCase)
            .FirstOrDefault();

        var id = await _discovery.StartDiscoveryAsync(
            new StartDiscoveryRequest(profile.SuggestedScope, null, credential?.Id));
        await _audit.RecordAsync("Discovery", "StartLocalNetwork", User.Identity?.Name ?? "", User.Identity?.Name ?? "",
            User.IsInRole("Administrator") ? "Administrator" : "NocOperator", id.ToString(), "DiscoveryRun");
        TempData["DiscoveryInfo"] = $"Barrido iniciado para {profile.SuggestedScope}. Sólo se consultará la red local detectada.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Cancel(Guid id)
    {
        await _discovery.CancelAsync(id);
        await _audit.RecordAsync("Discovery", "Cancel", User.Identity?.Name ?? "", User.Identity?.Name ?? "",
            User.IsInRole("Administrator") ? "Administrator" : "NocOperator", id.ToString(), "DiscoveryRun");
        return RedirectToAction(nameof(Index));
    }
}
