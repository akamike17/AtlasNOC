using AtlasNOC.Application.Dtos;
using AtlasNOC.Application.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AtlasNOC.Web.Controllers;

[Authorize(Roles = "Administrator,NocOperator")]
public class AlertRulesController : Controller
{
    private readonly IAlertRuleService _rules;
    private readonly IAuditService _audit;

    public AlertRulesController(IAlertRuleService rules, IAuditService audit)
    {
        _rules = rules;
        _audit = audit;
    }

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
        if (!AtlasNOC.Domain.Entities.AlertRule.SupportedOperators.Contains(request.ComparisonOperator))
            ModelState.AddModelError("ComparisonOperator", "Operador inválido. Usa >, >=, <, <= o ==.");
        if (request.ConsecutiveFaults < 1)
            ModelState.AddModelError("ConsecutiveFaults", "Debe requerirse al menos un fallo.");
        if (!ModelState.IsValid)
            return View(request);

        await _rules.CreateRuleAsync(request);
        await _audit.RecordAsync("AlertRule", "Create", User.Identity?.Name ?? "", User.Identity?.Name ?? "",
            User.IsInRole("Administrator") ? "Administrator" : "NocOperator",
            null, "AlertRule");
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Toggle(Guid id, bool enabled)
    {
        await _rules.ToggleRuleAsync(id, enabled);
        await _audit.RecordAsync("AlertRule", enabled ? "Enable" : "Disable",
            User.Identity?.Name ?? "", User.Identity?.Name ?? "",
            User.IsInRole("Administrator") ? "Administrator" : "NocOperator",
            id.ToString(), "AlertRule");
        return RedirectToAction(nameof(Index));
    }
}
