using AtlasNOC.Services.Ui;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AtlasNOC.Web.Controllers.Ui;

[Route("health")]
[Authorize(AuthenticationSchemes = AtlasNocUiAuth.UiScheme, Policy = "ReadOnly")]
public sealed class HealthController : Controller
{
    [HttpGet("")]
    public IActionResult Index()
    {
        ViewData["Nav"] = "Health";
        return View();
    }
}