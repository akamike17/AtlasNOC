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

    public CredentialsController(ICredentialService credentials) => _credentials = credentials;

    [HttpGet]
    public async Task<IActionResult> Index() => View(await _credentials.ListCredentialsAsync());

    [HttpGet]
    public IActionResult Create() => View();

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(CreateCredentialRequest request)
    {
        await _credentials.CreateCredentialAsync(request);
        return RedirectToAction(nameof(Index));
    }
}