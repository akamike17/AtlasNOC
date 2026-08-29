using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using AtlasNOC.Domain.Services;
using AtlasNOC.Domain.Services.Interfaces;
using AtlasNOC.Services.Ui;
using AtlasNOC.Web.Models;

namespace AtlasNOC.Web.Controllers.Ui;

[Route("account")]
public sealed class AccountController : Controller
{
    private readonly ApiKeyStore _apiKeyStore;
    private readonly IAuditService _auditService;

    public AccountController(ApiKeyStore apiKeyStore, IAuditService auditService)
    {
        _apiKeyStore = apiKeyStore;
        _auditService = auditService;
    }

    [HttpGet("login")]
    [AllowAnonymous]
    public IActionResult Login(string? returnUrl = null)
    {
        if (User.Identity?.IsAuthenticated == true)
            return RedirectToAction("Index", "Dashboard");
        return View(new LoginViewModel { ReturnUrl = returnUrl });
    }

    [HttpPost("login")]
    [AllowAnonymous]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Login(LoginViewModel model, CancellationToken ct = default)
    {
        if (!ModelState.IsValid)
            return View(model);

        var info = await _apiKeyStore.ValidateAsync(model.ApiKey, ct);
        if (info is null)
        {
            await _auditService.LogFailureAsync(
                "Auth", "UiLogin", "anonymous", "Invalid API key",
                userRole: null,
                ipAddress: HttpContext.Connection.RemoteIpAddress?.ToString(),
                userAgent: Request.Headers.UserAgent.FirstOrDefault(),
                cancellationToken: ct);
            ModelState.AddModelError(string.Empty, "La API key no es válida.");
            return View(model);
        }

        var claims = new[]
        {
            new System.Security.Claims.Claim(
                System.Security.Claims.ClaimTypes.NameIdentifier, info.Id.ToString()),
            new System.Security.Claims.Claim(System.Security.Claims.ClaimTypes.Name, info.Owner),
            new System.Security.Claims.Claim(System.Security.Claims.ClaimTypes.Role, info.Role)
        };
        var principle = new System.Security.Claims.ClaimsPrincipal(
            new System.Security.Claims.ClaimsIdentity(claims, AtlasNocUiAuth.UiScheme));

        await HttpContext.SignInAsync(AtlasNocUiAuth.UiScheme, principle);

        await _auditService.LogSuccessAsync(
            "Auth", "UiLogin", info.Owner, userRole: info.Role,
            ipAddress: HttpContext.Connection.RemoteIpAddress?.ToString(),
            userAgent: Request.Headers.UserAgent.FirstOrDefault(),
            cancellationToken: ct);

        if (!string.IsNullOrWhiteSpace(model.ReturnUrl) && Url.IsLocalUrl(model.ReturnUrl))
            return Redirect(model.ReturnUrl);
        return RedirectToAction("Index", "Dashboard");
    }

    [HttpPost("logout")]
    [Authorize(AuthenticationSchemes = AtlasNocUiAuth.UiScheme)]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Logout()
    {
        await _auditService.LogSuccessAsync(
            "Auth", "UiLogout", User.Identity?.Name ?? "Unknown",
            userRole: User.FindFirstValue(System.Security.Claims.ClaimTypes.Role),
            ipAddress: HttpContext.Connection.RemoteIpAddress?.ToString());
        await HttpContext.SignOutAsync(AtlasNocUiAuth.UiScheme);
        return RedirectToAction("Login", "Account");
    }

    [HttpGet("access-denied")]
    [AllowAnonymous]
    public IActionResult AccessDenied()
    {
        Response.StatusCode = StatusCodes.Status403Forbidden;
        return View();
    }
}