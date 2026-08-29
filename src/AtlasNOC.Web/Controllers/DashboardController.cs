using AtlasNOC.Application.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AtlasNOC.Web.Controllers;

[Authorize]
public class DashboardController : Controller
{
    private readonly ISystemHealthService _health;

    public DashboardController(ISystemHealthService health) => _health = health;

    [HttpGet]
    public async Task<IActionResult> Index()
    {
        var health = await _health.GetHealthAsync();
        return View(health);
    }
}