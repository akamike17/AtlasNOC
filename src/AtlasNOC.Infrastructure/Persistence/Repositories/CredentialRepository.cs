using AtlasNOC.Application.Repositories;
using AtlasNOC.Domain.Entities;
using AtlasNOC.Domain.ValueObjects;
using AtlasNOC.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace AtlasNOC.Infrastructure.Persistence.Repositories;

public class CredentialRepository : ICredentialRepository
{
    private readonly AtlasNOCDbContext _context;
    public CredentialRepository(AtlasNOCDbContext context) => _context = context;

    public async Task<DeviceCredential?> GetByIdAsync(Guid id, CancellationToken ct = default)
        => await _context.DeviceCredentials.FirstOrDefaultAsync(c => c.Id == CredentialId.From(id), ct);

    public async Task<IReadOnlyList<DeviceCredential>> ListAsync(CancellationToken ct = default)
        => await _context.DeviceCredentials.AsNoTracking().ToListAsync(ct);

    public Task AddAsync(DeviceCredential credential, CancellationToken ct = default)
    {
        _context.DeviceCredentials.Add(credential);
        return Task.CompletedTask;
    }

    public Task UpdateAsync(DeviceCredential credential, CancellationToken ct = default)
    {
        _context.DeviceCredentials.Update(credential);
        return Task.CompletedTask;
    }
}