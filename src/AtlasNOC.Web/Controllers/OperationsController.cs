using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AtlasNOC.Web.Controllers;

[Authorize]
public sealed class OperationsController : Controller
{
    [HttpGet("operations")]
    public IActionResult Index() => View();
    [HttpGet("operations/customers")]
    public IActionResult Customers() => View("Index");
    [HttpGet("support")]
    public IActionResult Support() => View("Index");
    [HttpGet("inventory")]
    public IActionResult Inventory() => View("Index");
    [HttpGet("coverage")]
    public IActionResult Coverage() => View("Index");
    [HttpGet("wisp")]
    public IActionResult Wisp() => View("Index");
    [HttpGet("settings")]
    public IActionResult Settings() => View("Index");
}
