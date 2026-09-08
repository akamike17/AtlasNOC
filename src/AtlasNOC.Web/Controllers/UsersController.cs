using AtlasNOC.Application.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using AtlasNOC.Application.Dtos;
using AtlasNOC.Domain.Identity;

namespace AtlasNOC.Web.Controllers;

[Authorize(Roles = "Administrator")]
public class UsersController : Controller
{
    private readonly IUserAdministrationService _users;
    private readonly IAuditService _audit;

    public UsersController(IUserAdministrationService users, IAuditService audit)
    {
        _users = users;
        _audit = audit;
    }

    [HttpGet]
    public async Task<IActionResult> Index() => View(await _users.ListUsersAsync());

    [HttpGet]
    public IActionResult Create() => View(new CreateUserRequest("", "", "", "", ApplicationRole.ReadOnly));

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(CreateUserRequest request)
    {
        var result = await _users.CreateAsync(request);
        if (!result.Success) { ModelState.AddModelError("", result.ErrorMessage!); return View(request); }
        await AuditAsync("Create", request.UserName);
        return RedirectToAction(nameof(Index));
    }

    [HttpGet]
    public async Task<IActionResult> Detail(Guid id)
        => await _users.GetUserAsync(id) is { } user ? View(user) : NotFound();

    [HttpGet]
    public async Task<IActionResult> Edit(Guid id)
    {
        var user = await _users.GetUserAsync(id);
        return user is null ? NotFound() : View(new EditUserRequest(user.Id, user.UserName, user.DisplayName ?? ""));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(EditUserRequest request)
    {
        var result = await _users.EditAsync(request);
        if (!result.Success) { ModelState.AddModelError("", result.ErrorMessage!); return View(request); }
        await AuditAsync("Edit", request.Id.ToString());
        return RedirectToAction(nameof(Detail), new { id = request.Id });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> ChangeRole(ChangeUserRoleRequest request)
    {
        var result = await _users.ChangeRoleAsync(request);
        if (!result.Success) TempData["UserError"] = result.ErrorMessage;
        else await AuditAsync("ChangeRole", $"{request.Id}:{request.Role}");
        return RedirectToAction(nameof(Detail), new { id = request.Id });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public Task<IActionResult> Disable(Guid id) => SetEnabledAsync(id, false);

    [HttpPost, ValidateAntiForgeryToken]
    public Task<IActionResult> Enable(Guid id) => SetEnabledAsync(id, true);

    [HttpGet]
    public async Task<IActionResult> ResetPassword(Guid id)
        => await _users.GetUserAsync(id) is null ? NotFound() : View(new ResetUserPasswordRequest(id, "", ""));

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> ResetPassword(ResetUserPasswordRequest request)
    {
        var result = await _users.ResetPasswordAsync(request);
        if (!result.Success) { ModelState.AddModelError("", result.ErrorMessage!); return View(request); }
        await AuditAsync("ResetPassword", request.Id.ToString());
        return RedirectToAction(nameof(Detail), new { id = request.Id });
    }

    private async Task<IActionResult> SetEnabledAsync(Guid id, bool enabled)
    {
        var result = await _users.SetEnabledAsync(id, enabled);
        if (!result.Success) TempData["UserError"] = result.ErrorMessage;
        else await AuditAsync(enabled ? "Enable" : "Disable", id.ToString());
        return RedirectToAction(nameof(Detail), new { id });
    }

    private Task AuditAsync(string action, string target)
        => _audit.RecordAsync("Users", action, User.Identity?.Name ?? "", User.Identity?.Name ?? "",
            ApplicationRole.Administrator, target, "ApplicationUser");
}
