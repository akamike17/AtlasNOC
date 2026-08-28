using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using AtlasNOC.Domain.Entities;
using AtlasNOC.Domain.Services.Interfaces;
using AtlasNOC.Web.Models;
using AtlasNOC.Models;

namespace AtlasNOC.Controllers;

public class HomeController : Controller
{
    private readonly ILogger<HomeController> _logger;
    private readonly IAuditService _auditService;

    public HomeController(ILogger<HomeController> logger, IAuditService auditService)
    {
        _logger = logger;
        _auditService = auditService;
    }

    public IActionResult Index()
    {
        return View();
    }

    public IActionResult Privacy()
    {
        return View();
    }

    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error()
    {
        return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
    }
}
