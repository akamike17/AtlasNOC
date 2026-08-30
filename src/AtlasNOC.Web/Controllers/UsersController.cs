using AtlasNOC.Application.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AtlasNOC.Web.Controllers;

[Authorize(Roles = "Administrator")]
public class UsersController : Controller
{
    private readonly IUserAdministrationService _users;

    public UsersController(IUserAdministrationService users) => _users = users;

    [HttpGet]
    public async Task<IActionResult> Index() => View(await _users.ListUsersAsync());
}