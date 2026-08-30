using AtlasNOC.Application.Dtos;
using AtlasNOC.Application.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AtlasNOC.Web.Controllers;

[Authorize(Roles = "Administrator,NocOperator")]
public class AlertRulesController : Controller
{
    private readonly IAlertRuleService _rules;

    public AlertRulesController(IAlertRuleService rules) => _rules = rules;

    [HttpGet]
    public async Task<IActionResult> Index() => View(await _rules.ListRulesAsync());

    [HttpGet]
    public IActionResult Create() => View();

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(CreateAlertRuleRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.MetricName))
            ModelState.AddModelError("MetricName", "La métrica es obligatoria.");
        if (!ModelState.IsValid)
            return View(request);

        await _rules.CreateRuleAsync(request);
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Toggle(Guid id, bool enabled)
    {
        await _rules.ToggleRuleAsync(id, enabled);
        return RedirectToAction(nameof(Index));
    }
}