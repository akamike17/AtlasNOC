using AtlasNOC.Application.Dtos;
using AtlasNOC.Application.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace AtlasNOC.Web.Controllers;

/// <summary>Primer arranque: solo existe mientras no haya administradores.</summary>
public class SetupController : Controller
{
    private readonly ISetupService _setup;

    public SetupController(ISetupService setup) => _setup = setup;

    [HttpGet]
    public async Task<IActionResult> Index()
    {
        if (!await _setup.IsSetupRequiredAsync())
            return RedirectToAction("Login", "Account");
        return View(new SetupRequest("", "", "", "", ""));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [EnableRateLimiting("setup")]
    public async Task<IActionResult> Index(SetupRequest request)
    {
        if (!await _setup.IsSetupRequiredAsync())
            return RedirectToAction("Login", "Account");

        var result = await _setup.SetupAsync(request);
        if (!result.Success)
        {
            ModelState.AddModelError(string.Empty, result.ErrorMessage ?? "No se pudo completar el setup.");
            return View(request);
        }

        return RedirectToAction("Login", "Account");
    }
}