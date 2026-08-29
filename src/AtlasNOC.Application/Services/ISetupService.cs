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
    Task CreateAdministratorAsync(string userName, string password, string displayName, CancellationToken ct = default);
}