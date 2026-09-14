using AtlasNOC.Domain.Identity;
using AtlasNOC.Application.Wisp;
using AtlasNOC.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AtlasNOC.Web.Controllers;

// Landing de integraciones: credenciales, API keys, canales de notificación, drivers.
// Lectura disponible para operadores y read-only; la gestión concreta de cada recurso
// vive en sus controllers propios (Credentials, ApiKeys) con su propia autorización.
[Authorize(Roles = ApplicationRole.Administrator + "," + ApplicationRole.NocOperator + "," + ApplicationRole.ReadOnly)]
public class IntegrationsController : Controller
{
    private readonly IWispConnectorRegistry _wisp;
    private readonly AtlasNOCDbContext _context;

    public IntegrationsController(IWispConnectorRegistry wisp, AtlasNOCDbContext context)
    {
        _wisp = wisp;
        _context = context;
    }

    [HttpGet("integrations")]
    public IActionResult Index()
    {
        ViewBag.SupportedWisp = WispConnectorCatalog.Supported;
        ViewBag.RecentWispObservations = _context.WispClientObservations
            .AsNoTracking()
            .OrderByDescending(x => x.ObservedAtUtc)
            .Take(50)
            .ToList();
        return View(_wisp.List());
    }
}
