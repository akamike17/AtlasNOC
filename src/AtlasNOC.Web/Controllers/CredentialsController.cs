using AtlasNOC.Application.Dtos;
using AtlasNOC.Application.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AtlasNOC.Web.Controllers;

/// <summary>Gestión de credenciales de equipos (SNMP v2/v3, etc.). Los secretos se guardan cifrados.</summary>
[Authorize(Roles = "Administrator,NocOperator")]
public class CredentialsController : Controller
{
    private readonly ICredentialService _credentials;
    private readonly IAuditService _audit;

    public CredentialsController(ICredentialService credentials, IAuditService audit)
    {
        _credentials = credentials;
        _audit = audit;
    }

    [HttpGet]
    public async Task<IActionResult> Index() => View(await _credentials.ListCredentialsAsync());

    [HttpGet]
    public IActionResult Create()
    {
        var ip = HttpContext.Connection.LocalIpAddress?.ToString();
        var suggestedName = string.IsNullOrWhiteSpace(ip) || ip is "::1" or "127.0.0.1"
            ? "Windows-SNMP"
            : $"Windows-SNMP-{ip}";
        return View(new CreateCredentialRequest(suggestedName, 1, null, null, null, null, null, null));
    }

    [HttpGet]
    public IActionResult Edit(Guid id)
    {
        ViewData["EditId"] = id;
        return View("Create", new CreateCredentialRequest("", 1, null, null, null, null, null, null));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(CreateCredentialRequest request)
    {
        Guid id;
        try
        {
            id = await _credentials.CreateCredentialAsync(request);
        }
        catch (InvalidOperationException ex)
        {
            ModelState.AddModelError(nameof(request.Name), ex.Message);
            return View(request);
        }

        await _audit.RecordAsync("Credential", "Create", User.Identity?.Name ?? "", User.Identity?.Name ?? "",
            User.IsInRole("Administrator") ? "Administrator" : "NocOperator",
            id.ToString(), "Credential");
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(Guid id, CreateCredentialRequest request)
    {
        try { await _credentials.UpdateCredentialAsync(id, request); }
        catch (Exception ex) when (ex is InvalidOperationException or ArgumentException or KeyNotFoundException)
        { ModelState.AddModelError(string.Empty, ex.Message); ViewData["EditId"] = id; return View("Create", request); }
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Toggle(Guid id, bool active)
    {
        await _credentials.SetCredentialActiveAsync(id, active);
        return RedirectToAction(nameof(Index));
    }
}
