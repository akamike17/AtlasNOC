using AtlasNOC.Domain.Entities;

namespace AtlasNOC.Application.Repositories;

public interface IApiKeyRepository
{
    Task<ApiKey?> GetByHashAsync(string keyHash, CancellationToken ct = default);
    Task<ApiKey?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<IReadOnlyList<ApiKey>> ListAsync(CancellationToken ct = default);
    Task AddAsync(ApiKey apiKey, CancellationToken ct = default);
    Task UpdateAsync(ApiKey apiKey, CancellationToken ct = default);
}