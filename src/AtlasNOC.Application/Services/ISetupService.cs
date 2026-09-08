using AtlasNOC.Application.Dtos;

namespace AtlasNOC.Application.Services;

/// <summary>Configuración de primer arranque: crea WISP + admin; se deshabilita luego.</summary>
public interface ISetupService
{
    Task<bool> IsSetupRequiredAsync(CancellationToken ct = default);
    Task<SetupResult> SetupAsync(SetupRequest request, CancellationToken ct = default);
}

/// <summary>Administración de usuarios y roles.</summary>
public interface IUserAdministrationService
{
    Task<IReadOnlyList<UserLiteDto>> ListUsersAsync(CancellationToken ct = default);
    Task<bool> AnyAdministratorAsync(CancellationToken ct = default);
    Task<OperationResult> CreateAdministratorAsync(string userName, string password, string displayName, CancellationToken ct = default);
    Task<UserDetailDto?> GetUserAsync(Guid id, CancellationToken ct = default);
    Task<OperationResult> CreateAsync(CreateUserRequest request, CancellationToken ct = default);
    Task<OperationResult> EditAsync(EditUserRequest request, CancellationToken ct = default);
    Task<OperationResult> ChangeRoleAsync(ChangeUserRoleRequest request, CancellationToken ct = default);
    Task<OperationResult> SetEnabledAsync(Guid id, bool enabled, CancellationToken ct = default);
    Task<OperationResult> ResetPasswordAsync(ResetUserPasswordRequest request, CancellationToken ct = default);
}

public sealed record OperationResult(bool Success, string? ErrorMessage = null);
