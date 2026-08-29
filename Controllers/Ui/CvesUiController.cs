using AtlasNOC.Domain.Entities;
using AtlasNOC.Domain.Services.Interfaces;
using AtlasNOC.Services.Ui;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AtlasNOC.Web.Controllers.Ui;

[Route("cves")]
[Authorize(AuthenticationSchemes = AtlasNocUiAuth.UiScheme, Policy = "ReadOnly")]
public sealed class CvesUiController : Controller
{
    private readonly ICveService _cveService;
    private readonly ILogger<CvesUiController> _logger;

    public CvesUiController(ICveService cveService, ILogger<CvesUiController> logger)
    {
        _cveService = cveService;
        _logger = logger;
    }

    [HttpGet("")]
    public async Task<IActionResult> Index(string? keyword = null, string? severity = null,
        CancellationToken ct = default)
    {
        IReadOnlyList<CveRecord> records;
        if (!string.IsNullOrWhiteSpace(keyword)) records = await _cveService.SearchByKeywordAsync(keyword.Trim(), ct);
        else if (!string.IsNullOrWhiteSpace(severity)) records = await _cveService.GetBySeverityAsync(severity, ct);
        else records = await _cveService.GetAllAsync(ct);

        var stats = await _cveService.GetStatsAsync(ct);
        var ordered = records
            .OrderByDescending(c => c.CvssBaseScore ?? 0)
            .ThenByDescending(c => c.PublishedDate).ToList();
        ViewData["Nav"] = "Cves";
        return View(new CvesIndexVm(ordered, stats, keyword, severity));
    }

    [HttpGet("stats")]
    public async Task<IActionResult> Stats(CancellationToken ct = default)
        => Json(await _cveService.GetStatsAsync(ct));

    [HttpPost("fetch")]
    [Authorize(AuthenticationSchemes = AtlasNocUiAuth.UiScheme, Policy = "OperatorOrAdmin")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Fetch(CancellationToken ct = default)
    {
        try
        {
            var fetched = await _cveService.TriggerFetchAsync(ct);
            TempData["Message"] = $"Se sincronizaron {fetched} registros CVE.";
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "CVE fetch failed");
            TempData["Error"] = "La sincronización CVE no pudo completarse.";
        }
        return RedirectToAction("Index");
    }
}

public sealed record CvesIndexVm(IReadOnlyList<CveRecord> Items, CveStats Stats, string? Keyword, string? Severity);