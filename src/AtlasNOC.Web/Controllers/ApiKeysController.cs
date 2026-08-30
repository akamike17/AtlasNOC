using AtlasNOC.Application.Dtos;
using AtlasNOC.Application.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AtlasNOC.Web.Controllers;

[Authorize(Roles = "Administrator")]
public class ApiKeysController : Controller
{
    private readonly IApiKeyService _apiKeys;

    public ApiKeysController(IApiKeyService apiKeys) => _apiKeys = apiKeys;

    [HttpGet]
    public async Task<IActionResult> Index() => View(await _apiKeys.ListApiKeysAsync());

    [HttpGet]
    public IActionResult Create() => View();

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(CreateApiKeyRequest request)
    {
        var result = await _apiKeys.CreateApiKeyAsync(request);
        TempData["NewKey"] = result.PlainTextKey;
        TempData["NewKeyName"] = result.Name;
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Revoke(Guid id)
    {
        await _apiKeys.RevokeAsync(id);
        return RedirectToAction(nameof(Index));
    }
}