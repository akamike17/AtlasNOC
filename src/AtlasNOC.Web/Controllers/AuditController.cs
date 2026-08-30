using AtlasNOC.Application.Services;
using AtlasNOC.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AtlasNOC.Web.Controllers;

[Authorize(Roles = "Administrator")]
public class AuditController : Controller
{
    private readonly AtlasNOCDbContext _context;

    public AuditController(AtlasNOCDbContext context) => _context = context;

    [HttpGet]
    public async Task<IActionResult> Index()
    {
        var events = await _context.AuditEvents
            .AsNoTracking()
            .OrderByDescending(a => a.TimestampUtc)
            .Take(500)
            .ToListAsync();
        return View(events);
    }
}