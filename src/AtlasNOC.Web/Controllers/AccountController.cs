using AtlasNOC.Application.Services;
using AtlasNOC.Domain.Identity;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace AtlasNOC.Web.Controllers;

public class AccountController : Controller
{
    private readonly SignInManager<ApplicationUser> _signInManager;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly ISetupService _setup;
    private readonly IAuditService _audit;

    public AccountController(SignInManager<ApplicationUser> signInManager,
        UserManager<ApplicationUser> userManager, ISetupService setup, IAuditService audit)
    {
        _signInManager = signInManager;
        _userManager = userManager;
        _setup = setup;
        _audit = audit;
    }

    [HttpGet]
    public async Task<IActionResult> Login()
    {
        if (await _setup.IsSetupRequiredAsync())
            return RedirectToAction("Index", "Setup");
        return View();
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [EnableRateLimiting("login")]
    public async Task<IActionResult> Login(string userName, string password, bool rememberMe, string? returnUrl = null)
    {
        if (string.IsNullOrWhiteSpace(userName) || string.IsNullOrEmpty(password))
        {
            ModelState.AddModelError(string.Empty, "Usuario y contraseña son obligatorios.");
            return View();
        }

        // ─── Fase A4: IsActive. No revelar si el usuario existe. ─────────────
        var user = await _userManager.FindByNameAsync(userName);
        if (user is not null && !user.IsActive)
        {
            // Resultado idéntico al de credenciales inválidas: no delata la cuenta.
            ModelState.AddModelError(string.Empty, "Usuario o contraseña incorrectos.");
            return View();
        }

        // ─── Fase A4: lockout habilitado de verdad. ──────────────────────────
        var result = await _signInManager.PasswordSignInAsync(userName, password, rememberMe, lockoutOnFailure: true);

        if (result.Succeeded)
        {
            await _audit.RecordAsync("Auth", "Login", userName, userName, "—");
            return RedirectToLocal(returnUrl);
        }

        if (result.IsLockedOut)
        {
            await _audit.RecordAsync("Auth", "Lockout", userName, userName, "—");
            ModelState.AddModelError(string.Empty, "Cuenta bloqueada por demasiados intentos fallidos. Intenta de nuevo más tarde.");
            return View();
        }

        if (result.IsNotAllowed)
        {
            ModelState.AddModelError(string.Empty, "Usuario o contraseña incorrectos.");
            return View();
        }

        if (result.RequiresTwoFactor)
        {
            // No se usa 2FA en esta versión, pero se gestiona explícitamente.
            ModelState.AddModelError(string.Empty, "Se requiere un segundo factor de autenticación.");
            return View();
        }

        ModelState.AddModelError(string.Empty, "Usuario o contraseña incorrectos.");
        return View();
    }

    [Authorize]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Logout()
    {
        await _signInManager.SignOutAsync();
        await _audit.RecordAsync("Auth", "Logout", User.Identity?.Name ?? "", User.Identity?.Name ?? "", "—");
        return RedirectToAction("Login", "Account");
    }

    [HttpGet]
    public IActionResult AccessDenied() => View();

    private IActionResult RedirectToLocal(string? returnUrl)
        => !string.IsNullOrWhiteSpace(returnUrl) && Url.IsLocalUrl(returnUrl)
            ? Redirect(returnUrl!)
            : RedirectToAction("Index", "Dashboard");
}