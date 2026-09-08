using AtlasNOC.Application.Dtos;
using AtlasNOC.Application.Services;
using AtlasNOC.Domain.Entities;
using AtlasNOC.Domain.Identity;
using System.Data;
using System.Data.Common;
using Microsoft.Extensions.Logging;
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
    private readonly ILogger<SetupService> _logger;
    private const string SetupLockPrefix = "atlasnoc:setup:";

    public SetupService(AtlasNOCDbContext context, UserManager<ApplicationUser> userManager,
        RoleManager<ApplicationRole> roleManager, ILogger<SetupService> logger)
    {
        _context = context;
        _userManager = userManager;
        _roleManager = roleManager;
        _logger = logger;
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

        var connection = _context.Database.GetDbConnection();
        var openedHere = connection.State != ConnectionState.Open;
        if (openedHere) await _context.Database.OpenConnectionAsync(ct);
        var lockAcquired = false;
        try
        {
            lockAcquired = await TryAcquireSetupLockAsync(connection, ct);
            if (!lockAcquired)
                return new SetupResult(false, "Otro proceso está realizando la configuración inicial.");

            await using var transaction = await _context.Database
                .BeginTransactionAsync(IsolationLevel.Serializable, ct);
            try
            {
                // Revalidación obligatoria dentro de la sección crítica y transacción.
                if (!await IsSetupRequiredAsync(ct))
                {
                    await transaction.RollbackAsync(ct);
                    return new SetupResult(false, "El sistema ya está configurado.");
                }

                foreach (var roleName in new[] { ApplicationRole.Administrator, ApplicationRole.NocOperator, ApplicationRole.ReadOnly })
                {
                    if (await _roleManager.RoleExistsAsync(roleName)) continue;
                    var roleResult = await _roleManager.CreateAsync(new ApplicationRole(roleName));
                    EnsureSucceeded(roleResult, $"crear el rol {roleName}");
                }

                _context.Organizations.Add(new WispOrganization(request.WispName));

                var admin = new ApplicationUser(request.AdminUserName)
                {
                    DisplayName = request.AdminDisplayName,
                    Email = request.AdminUserName,
                    EmailConfirmed = true
                };
                EnsureSucceeded(await _userManager.CreateAsync(admin, request.Password), "crear el administrador");
                EnsureSucceeded(await _userManager.AddToRoleAsync(admin, ApplicationRole.Administrator),
                    "asignar el rol Administrator");

                await _context.SaveChangesAsync(ct);
                await transaction.CommitAsync(ct);
                return new SetupResult(true, null);
            }
            catch (OperationCanceledException)
            {
                await transaction.RollbackAsync(CancellationToken.None);
                throw;
            }
            catch (SetupOperationException ex)
            {
                await transaction.RollbackAsync(CancellationToken.None);
                return new SetupResult(false, ex.Message);
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync(CancellationToken.None);
                _logger.LogError(ex, "Falló la configuración inicial transaccional");
                return new SetupResult(false, "No se pudo completar la configuración inicial.");
            }
        }
        finally
        {
            if (lockAcquired) await ReleaseSetupLockAsync(connection);
            if (openedHere) await _context.Database.CloseConnectionAsync();
        }
    }

    private static void EnsureSucceeded(IdentityResult result, string operation)
    {
        if (result.Succeeded) return;
        var errors = string.Join("; ", result.Errors.Select(e => e.Description));
        throw new SetupOperationException($"No se pudo {operation}: {errors}");
    }

    private static async Task<bool> TryAcquireSetupLockAsync(DbConnection connection, CancellationToken ct)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT GET_LOCK(@lockName, 10);";
        AddParameter(command, "@lockName", GetSetupLockName(connection));
        return Convert.ToInt32(await command.ExecuteScalarAsync(ct)) == 1;
    }

    private static async Task ReleaseSetupLockAsync(DbConnection connection)
    {
        if (connection.State != ConnectionState.Open) return;
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT RELEASE_LOCK(@lockName);";
        AddParameter(command, "@lockName", GetSetupLockName(connection));
        await command.ExecuteScalarAsync(CancellationToken.None);
    }

    private static void AddParameter(DbCommand command, string name, object value)
    {
        var parameter = command.CreateParameter();
        parameter.ParameterName = name;
        parameter.Value = value;
        command.Parameters.Add(parameter);
    }

    private static string GetSetupLockName(DbConnection connection)
    {
        var name = SetupLockPrefix + connection.Database;
        return name.Length <= 64 ? name : name[..64];
    }

    private sealed class SetupOperationException(string message) : Exception(message);
}

public class UserAdministrationService : IUserAdministrationService
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly RoleManager<ApplicationRole> _roleManager;
    private readonly AtlasNOCDbContext _context;
    private static readonly string[] AllowedRoles =
        { ApplicationRole.Administrator, ApplicationRole.NocOperator, ApplicationRole.ReadOnly };

    public UserAdministrationService(UserManager<ApplicationUser> userManager,
        RoleManager<ApplicationRole> roleManager, AtlasNOCDbContext context)
    {
        _userManager = userManager;
        _roleManager = roleManager;
        _context = context;
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
        => (await _userManager.GetUsersInRoleAsync(ApplicationRole.Administrator)).Any(u => u.IsActive);

    public async Task<OperationResult> CreateAdministratorAsync(string userName, string password, string displayName, CancellationToken ct = default)
        => await CreateAsync(new CreateUserRequest(userName, displayName, password, password,
            ApplicationRole.Administrator), ct);

    public async Task<UserDetailDto?> GetUserAsync(Guid id, CancellationToken ct = default)
    {
        var user = await _userManager.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == id, ct);
        if (user is null) return null;
        var roles = await _userManager.GetRolesAsync(user);
        return new UserDetailDto(user.Id, user.UserName ?? string.Empty, user.Email,
            user.DisplayName, roles.ToList(), user.IsActive, user.CreatedAtUtc);
    }

    public Task<OperationResult> CreateAsync(CreateUserRequest request, CancellationToken ct = default)
        => ExecuteProtectedAsync(async () =>
        {
            if (string.IsNullOrWhiteSpace(request.UserName)) return Failure("El usuario es obligatorio.");
            if (request.Password != request.ConfirmPassword) return Failure("Las contraseñas no coinciden.");
            if (!AllowedRoles.Contains(request.Role)) return Failure("Rol inválido.");
            var user = new ApplicationUser(request.UserName.Trim())
            {
                DisplayName = request.DisplayName?.Trim(),
                Email = request.UserName.Trim(),
                EmailConfirmed = true
            };
            var created = await _userManager.CreateAsync(user, request.Password);
            if (!created.Succeeded) return FromIdentity(created);
            var assigned = await _userManager.AddToRoleAsync(user, request.Role);
            return assigned.Succeeded ? new OperationResult(true) : FromIdentity(assigned);
        }, ct);

    public Task<OperationResult> EditAsync(EditUserRequest request, CancellationToken ct = default)
        => ExecuteProtectedAsync(async () =>
        {
            var user = await _userManager.FindByIdAsync(request.Id.ToString());
            if (user is null) return Failure("Usuario no encontrado.");
            if (string.IsNullOrWhiteSpace(request.UserName)) return Failure("El usuario es obligatorio.");
            var nameResult = await _userManager.SetUserNameAsync(user, request.UserName.Trim());
            if (!nameResult.Succeeded) return FromIdentity(nameResult);
            var emailResult = await _userManager.SetEmailAsync(user, request.UserName.Trim());
            if (!emailResult.Succeeded) return FromIdentity(emailResult);
            user.DisplayName = request.DisplayName?.Trim();
            var updated = await _userManager.UpdateAsync(user);
            return updated.Succeeded ? new OperationResult(true) : FromIdentity(updated);
        }, ct);

    public Task<OperationResult> ChangeRoleAsync(ChangeUserRoleRequest request, CancellationToken ct = default)
        => ExecuteProtectedAsync(async () =>
        {
            if (!AllowedRoles.Contains(request.Role)) return Failure("Rol inválido.");
            var user = await _userManager.FindByIdAsync(request.Id.ToString());
            if (user is null) return Failure("Usuario no encontrado.");
            var roles = await _userManager.GetRolesAsync(user);
            if (user.IsActive && roles.Contains(ApplicationRole.Administrator)
                && request.Role != ApplicationRole.Administrator
                && !await HasAnotherActiveAdministratorAsync(user.Id))
                return Failure("No se puede cambiar el rol del último Administrator activo.");
            if (roles.Count > 0)
            {
                var removed = await _userManager.RemoveFromRolesAsync(user, roles);
                if (!removed.Succeeded) return FromIdentity(removed);
            }
            var added = await _userManager.AddToRoleAsync(user, request.Role);
            if (!added.Succeeded) return FromIdentity(added);
            await _userManager.UpdateSecurityStampAsync(user);
            return new OperationResult(true);
        }, ct);

    public Task<OperationResult> SetEnabledAsync(Guid id, bool enabled, CancellationToken ct = default)
        => ExecuteProtectedAsync(async () =>
        {
            var user = await _userManager.FindByIdAsync(id.ToString());
            if (user is null) return Failure("Usuario no encontrado.");
            var roles = await _userManager.GetRolesAsync(user);
            if (!enabled && user.IsActive && roles.Contains(ApplicationRole.Administrator)
                && !await HasAnotherActiveAdministratorAsync(user.Id))
                return Failure("No se puede desactivar el último Administrator activo.");
            user.IsActive = enabled;
            var updated = await _userManager.UpdateAsync(user);
            if (!updated.Succeeded) return FromIdentity(updated);
            // Invalida todas las cookies existentes del usuario.
            var stamp = await _userManager.UpdateSecurityStampAsync(user);
            return stamp.Succeeded ? new OperationResult(true) : FromIdentity(stamp);
        }, ct);

    public Task<OperationResult> ResetPasswordAsync(ResetUserPasswordRequest request, CancellationToken ct = default)
        => ExecuteProtectedAsync(async () =>
        {
            if (request.NewPassword != request.ConfirmPassword) return Failure("Las contraseñas no coinciden.");
            var user = await _userManager.FindByIdAsync(request.Id.ToString());
            if (user is null) return Failure("Usuario no encontrado.");
            var token = await _userManager.GeneratePasswordResetTokenAsync(user);
            var reset = await _userManager.ResetPasswordAsync(user, token, request.NewPassword);
            return reset.Succeeded ? new OperationResult(true) : FromIdentity(reset);
        }, ct);

    private async Task<bool> HasAnotherActiveAdministratorAsync(Guid excludedId)
        => (await _userManager.GetUsersInRoleAsync(ApplicationRole.Administrator))
            .Any(user => user.Id != excludedId && user.IsActive);

    private async Task<OperationResult> ExecuteProtectedAsync(Func<Task<OperationResult>> operation, CancellationToken ct)
    {
        var connection = _context.Database.GetDbConnection();
        var openedHere = connection.State != ConnectionState.Open;
        if (openedHere) await _context.Database.OpenConnectionAsync(ct);
        var lockName = ("atlasnoc:user-admin:" + connection.Database);
        if (lockName.Length > 64) lockName = lockName[..64];
        var acquired = false;
        try
        {
            await using (var command = connection.CreateCommand())
            {
                command.CommandText = "SELECT GET_LOCK(@name, 10);";
                AddLockParameter(command, lockName);
                acquired = Convert.ToInt32(await command.ExecuteScalarAsync(ct)) == 1;
            }
            if (!acquired) return Failure("Otra operación de usuarios está en curso.");

            await using var transaction = await _context.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
            try
            {
                var result = await operation();
                if (!result.Success) { await transaction.RollbackAsync(CancellationToken.None); return result; }
                await _context.SaveChangesAsync(ct);
                await transaction.CommitAsync(ct);
                return result;
            }
            catch
            {
                await transaction.RollbackAsync(CancellationToken.None);
                throw;
            }
        }
        finally
        {
            if (acquired && connection.State == ConnectionState.Open)
            {
                await using var release = connection.CreateCommand();
                release.CommandText = "SELECT RELEASE_LOCK(@name);";
                AddLockParameter(release, lockName);
                await release.ExecuteScalarAsync(CancellationToken.None);
            }
            if (openedHere) await _context.Database.CloseConnectionAsync();
        }
    }

    private static void AddLockParameter(DbCommand command, string lockName)
    {
        var parameter = command.CreateParameter();
        parameter.ParameterName = "@name";
        parameter.Value = lockName;
        command.Parameters.Add(parameter);
    }

    private static OperationResult Failure(string message) => new(false, message);
    private static OperationResult FromIdentity(IdentityResult result)
        => Failure(string.Join("; ", result.Errors.Select(error => error.Description)));
}
