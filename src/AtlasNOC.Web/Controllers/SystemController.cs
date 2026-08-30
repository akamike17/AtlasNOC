using AtlasNOC.Application.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AtlasNOC.Web.Controllers;

[Authorize]
public class SystemController : Controller
{
    private readonly ISystemHealthService _health;

    public SystemController(ISystemHealthService health) => _health = health;

    [HttpGet]
    public async Task<IActionResult> Index() => View(await _health.GetHealthAsync());
}