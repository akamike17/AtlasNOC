using AtlasNOC.Application.Dtos;
using AtlasNOC.Application.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AtlasNOC.Web.Controllers;

[Authorize(Roles = "Administrator")]
public class ApiKeysController : Controller
{
    private readonly IApiKeyService _apiKeys;
    private readonly IAuditService _audit;

    public ApiKeysController(IApiKeyService apiKeys, IAuditService audit)
    {
        _apiKeys = apiKeys;
        _audit = audit;
    }

    [HttpGet]
    public async Task<IActionResult> Index() => View(await _apiKeys.ListApiKeysAsync());

    [HttpGet]
    public IActionResult Create() => View();

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(CreateApiKeyRequest request)
    {
        var result = await _apiKeys.CreateApiKeyAsync(request);
        await _audit.RecordAsync("ApiKey", "Create", User.Identity?.Name ?? "", User.Identity?.Name ?? "",
            "Administrator", result.Id.ToString(), "ApiKey");
        TempData["NewKey"] = result.PlainTextKey;
        TempData["NewKeyName"] = result.Name;
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Revoke(Guid id)
    {
        await _apiKeys.RevokeAsync(id);
        await _audit.RecordAsync("ApiKey", "Revoke", User.Identity?.Name ?? "", User.Identity?.Name ?? "",
            "Administrator", id.ToString(), "ApiKey");
        return RedirectToAction(nameof(Index));
    }
}