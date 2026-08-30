using AtlasNOC.Application.Dtos;
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

        var result = await _signInManager.PasswordSignInAsync(userName, password, rememberMe, lockoutOnFailure: false);
        if (result.Succeeded)
        {
            await _audit.RecordAsync("Auth", "Login", userName, userName, "—");
            return RedirectToLocal(returnUrl);
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
        return RedirectToAction("Login", "Account");
    }

    [HttpGet]
    public IActionResult AccessDenied() => View();

    private IActionResult RedirectToLocal(string? returnUrl)
        => !string.IsNullOrWhiteSpace(returnUrl) && Url.IsLocalUrl(returnUrl)
            ? Redirect(returnUrl!)
            : RedirectToAction("Index", "Dashboard");
}