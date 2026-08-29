using AtlasNOC.Application.Repositories;
using AtlasNOC.Domain.Entities;
using AtlasNOC.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace AtlasNOC.Infrastructure.Persistence.Repositories;

public class ApiKeyRepository : IApiKeyRepository
{
    private readonly AtlasNOCDbContext _context;
    public ApiKeyRepository(AtlasNOCDbContext context) => _context = context;

    public async Task<ApiKey?> GetByHashAsync(string keyHash, CancellationToken ct = default)
        => await _context.ApiKeys.FirstOrDefaultAsync(k => k.KeyHash == keyHash, ct);

    public async Task<ApiKey?> GetByIdAsync(Guid id, CancellationToken ct = default)
        => await _context.ApiKeys.FirstOrDefaultAsync(k => k.Id == id, ct);

    public async Task<IReadOnlyList<ApiKey>> ListAsync(CancellationToken ct = default)
        => await _context.ApiKeys.AsNoTracking().ToListAsync(ct);

    public Task AddAsync(ApiKey apiKey, CancellationToken ct = default)
    {
        _context.ApiKeys.Add(apiKey);
        return Task.CompletedTask;
    }

    public Task UpdateAsync(ApiKey apiKey, CancellationToken ct = default)
    {
        _context.ApiKeys.Update(apiKey);
        return Task.CompletedTask;
    }
}