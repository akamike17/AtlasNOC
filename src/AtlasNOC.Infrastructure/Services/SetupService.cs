using AtlasNOC.Application.Dtos;
using AtlasNOC.Application.Services;
using AtlasNOC.Domain.Entities;
using AtlasNOC.Domain.Identity;
using AtlasNOC.Domain.ValueObjects;
using AtlasNOC.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace AtlasNOC.Infrastructure.Services;

public class SetupService : ISetupService
{
    private readonly AtlasNOCDbContext _context;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly RoleManager<ApplicationRole> _roleManager;

    public SetupService(AtlasNOCDbContext context, UserManager<ApplicationUser> userManager,
        RoleManager<ApplicationRole> roleManager)
    {
        _context = context;
        _userManager = userManager;
        _roleManager = roleManager;
    }

    public async Task<bool> IsSetupRequiredAsync(CancellationToken ct = default)
    {
        var adminRole = await _roleManager.Roles.SingleOrDefaultAsync(r => r.Name == ApplicationRole.Administrator, ct);
        if (adminRole is null) return true;
        var admins = await _userManager.GetUsersInRoleAsync(ApplicationRole.Administrator);
        return admins.Count == 0;
    }

    public async Task<SetupResult> SetupAsync(SetupRequest request, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(request.WispName))
            return new SetupResult(false, "El nombre del WISP es obligatorio.");
        if (string.IsNullOrWhiteSpace(request.AdminUserName))
            return new SetupResult(false, "El usuario administrador es obligatorio.");
        if (string.IsNullOrEmpty(request.Password) || request.Password.Length < 8)
            return new SetupResult(false, "La contraseña debe tener al menos 8 caracteres.");
        if (request.Password != request.ConfirmPassword)
            return new SetupResult(false, "Las contraseñas no coinciden.");

        if (!await IsSetupRequiredAsync(ct))
            return new SetupResult(false, "El sistema ya está configurado.");

        // Crear roles si no existen.
        foreach (var roleName in new[] { ApplicationRole.Administrator, ApplicationRole.NocOperator, ApplicationRole.ReadOnly })
        {
            if (!await _roleManager.RoleExistsAsync(roleName))
                await _roleManager.CreateAsync(new ApplicationRole(roleName));
        }

        // Crear organización WISP.
        var org = new WispOrganization(request.WispName);
        _context.Organizations.Add(org);

        // Crear usuario administrador.
        var admin = new ApplicationUser(request.AdminUserName)
        {
            DisplayName = request.AdminDisplayName,
            Email = request.AdminUserName,
            EmailConfirmed = true
        };
        var createResult = await _userManager.CreateAsync(admin, request.Password);
        if (!createResult.Succeeded)
            return new SetupResult(false, string.Join("; ", createResult.Errors.Select(e => e.Description)));

        var addRoleResult = await _userManager.AddToRoleAsync(admin, ApplicationRole.Administrator);
        if (!addRoleResult.Succeeded)
            return new SetupResult(false, string.Join("; ", addRoleResult.Errors.Select(e => e.Description)));

        await _context.SaveChangesAsync(ct);
        return new SetupResult(true, null);
    }
}

public class UserAdministrationService : IUserAdministrationService
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly RoleManager<ApplicationRole> _roleManager;

    public UserAdministrationService(UserManager<ApplicationUser> userManager, RoleManager<ApplicationRole> roleManager)
    {
        _userManager = userManager;
        _roleManager = roleManager;
    }

    public async Task<IReadOnlyList<UserLiteDto>> ListUsersAsync(CancellationToken ct = default)
    {
        var result = new List<UserLiteDto>();
        foreach (var user in _userManager.Users.ToList())
        {
            var roles = await _userManager.GetRolesAsync(user);
            result.Add(new UserLiteDto(user.Id, user.UserName!, user.DisplayName,
                roles.FirstOrDefault() ?? string.Empty, user.IsActive));
        }
        return result;
    }

    public async Task<bool> AnyAdministratorAsync(CancellationToken ct = default)
        => (await _userManager.GetUsersInRoleAsync(ApplicationRole.Administrator)).Count > 0;

    public async Task CreateAdministratorAsync(string userName, string password, string displayName, CancellationToken ct = default)
    {
        var user = new ApplicationUser(userName) { DisplayName = displayName, Email = userName, EmailConfirmed = true };
        await _userManager.CreateAsync(user, password);
        await _userManager.AddToRoleAsync(user, ApplicationRole.Administrator);
    }
}