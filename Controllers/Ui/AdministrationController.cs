using AtlasNOC.Domain.Entities;
using AtlasNOC.Domain.Enums;
using AtlasNOC.Domain.Services;
using AtlasNOC.Domain.Services.Interfaces;
using AtlasNOC.Domain.ValueObjects;
using AtlasNOC.Services.Ui;
using AtlasNOC.Web.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace AtlasNOC.Web.Controllers.Ui;

[Route("administration")]
[Authorize(AuthenticationSchemes = AtlasNocUiAuth.UiScheme, Policy = "AdminOnly")]
public sealed class AdministrationController : Controller
{
    private readonly ICredentialService _credentialService;
    private readonly ApiKeyStore _apiKeyStore;
    private readonly IAuditService _auditService;
    private readonly ILogger<AdministrationController> _logger;

    public AdministrationController(ICredentialService credentialService,
        ApiKeyStore apiKeyStore, IAuditService auditService,
        ILogger<AdministrationController> logger)
    {
        _credentialService = credentialService;
        _apiKeyStore = apiKeyStore;
        _auditService = auditService;
        _logger = logger;
    }

    // ─── Credentials ────────────────────────────────────────────────────
    [HttpGet("credentials")]
    public async Task<IActionResult> Credentials(CancellationToken ct = default)
    {
        var credentials = await _credentialService.GetAllAsync(ct);
        ViewData["Nav"] = "Credentials";
        return View(credentials.OrderByDescending(c => c.CreatedAt).ToList());
    }

    [HttpPost("credentials/v2c")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateV2c(CreateCredentialV2cUiRequest request, CancellationToken ct = default)
    {
        if (!ModelState.IsValid) return RedirectToAction("Credentials");
        try
        {
            await _credentialService.CreateV2cAsync(request.Name, request.Community, Actor, request.ExpiresAt, ct);
            await LogAudit("Credential", "CreateV2c", request.Name);
            TempData["Message"] = "Credencial SNMPv2c creada.";
        }
        catch (ArgumentException ex) { TempData["Error"] = ex.Message; }
        return RedirectToAction("Credentials");
    }

    [HttpPost("credentials/v3")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateV3(CreateCredentialV3UiRequest request, CancellationToken ct = default)
    {
        if (!ModelState.IsValid) return RedirectToAction("Credentials");
        try
        {
            await _credentialService.CreateV3Async(request.Name, request.UserName, request.AuthProtocol,
                request.AuthPassword, request.PrivProtocol, request.PrivPassword, Actor, request.ExpiresAt, ct);
            await LogAudit("Credential", "CreateV3", request.Name);
            TempData["Message"] = "Credencial SNMPv3 creada.";
        }
        catch (ArgumentException ex) { TempData["Error"] = ex.Message; }
        return RedirectToAction("Credentials");
    }

    [HttpPost("credentials/{id:guid}/rotate-v2c")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RotateV2c(Guid id, [FromForm] string? newCommunity, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(newCommunity)) { TempData["Error"] = "El community es obligatorio."; return RedirectToAction("Credentials"); }
        try { await _credentialService.RotateV2cAsync(CredentialId.From(id), newCommunity, Actor, ct); await LogAudit("Credential", "RotateV2c", id.ToString()); TempData["Message"] = "Community rotado."; }
        catch (KeyNotFoundException) { return NotFound(); }
        return RedirectToAction("Credentials");
    }

    [HttpPost("credentials/{id:guid}/deactivate")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeactivateCredential(Guid id, CancellationToken ct = default)
    {
        try { await _credentialService.DeactivateAsync(CredentialId.From(id), Actor, ct); await LogAudit("Credential", "Deactivate", id.ToString()); TempData["Message"] = "Credencial desactivada."; }
        catch (KeyNotFoundException) { return NotFound(); }
        return RedirectToAction("Credentials");
    }

    // ─── API Keys ────────────────────────────────────────────────────────
    [HttpGet("apikeys")]
    public async Task<IActionResult> ApiKeys(CancellationToken ct = default)
    {
        var keys = await _apiKeyStore.ListActiveKeysAsync(ct);
        ViewData["Nav"] = "ApiKeys";
        return View(keys);
    }

    [HttpPost("apikeys/new")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateApiKey(CreateApiKeyUiRequest request, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(request.Owner)) { TempData["Error"] = "El propietario es obligatorio."; return RedirectToAction("ApiKeys"); }
        try
        {
            var (id, plaintext) = await _apiKeyStore.CreateKeyAsync(request.Owner, request.Description ?? "", request.Role, ct);
            await LogAudit("ApiKey", "Create", id.ToString());
            TempData["NewApiKey"] = plaintext;
            TempData["Message"] = $"API key creada para {request.Owner}. Se muestra una sola vez.";
        }
        catch (ArgumentException ex) { TempData["Error"] = ex.Message; }
        return RedirectToAction("ApiKeys");
    }

    [HttpPost("apikeys/{id:guid}/revoke")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RevokeApiKey(Guid id, CancellationToken ct = default)
    {
        try { await _apiKeyStore.RevokeKeyAsync(id, ct); await LogAudit("ApiKey", "Revoke", id.ToString()); TempData["Message"] = "API key revocada."; }
        catch (KeyNotFoundException) { return NotFound(); }
        return RedirectToAction("ApiKeys");
    }

    // ─── Audit ───────────────────────────────────────────────────────────
    [HttpGet("audit")]
    public async Task<IActionResult> Audit(string? category = null, int count = 100, CancellationToken ct = default)
    {
        IReadOnlyList<AuditEvent> events = string.IsNullOrWhiteSpace(category)
            ? await _auditService.GetRecentAsync(count, ct)
            : await _auditService.GetByCategoryAsync(category, ct);
        ViewData["Nav"] = "Audit";
        return View(new AuditIndexVm(events.OrderByDescending(e => e.Timestamp).ToList(), category, count));
    }

    private string Actor => User.Identity?.Name ?? "operator";
    private async Task LogAudit(string category, string action, string resource)
    {
        try
        {
            await _auditService.LogSuccessAsync(category, action, Actor,
                userRole: User.FindFirstValue(System.Security.Claims.ClaimTypes.Role),
                targetResource: resource, targetResourceType: category,
                ipAddress: HttpContext.Connection.RemoteIpAddress?.ToString());
        }
        catch (Exception ex) { _logger.LogWarning(ex, "Audit log failed"); }
    }
}

public sealed record AuditIndexVm(IReadOnlyList<AuditEvent> Items, string? Category, int Count);