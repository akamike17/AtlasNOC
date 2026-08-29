using Microsoft.AspNetCore.Mvc;

namespace AtlasNOC.Web.Controllers;

public class HomeController : Controller
{
    [HttpGet]
    public IActionResult Index() => RedirectToAction("Index", "Dashboard");

    [HttpGet]
    public IActionResult Error() => View();
}