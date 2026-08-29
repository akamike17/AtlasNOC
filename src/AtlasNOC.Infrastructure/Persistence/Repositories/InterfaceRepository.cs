using AtlasNOC.Application.Repositories;
using AtlasNOC.Domain.Entities;
using AtlasNOC.Domain.ValueObjects;
using AtlasNOC.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace AtlasNOC.Infrastructure.Persistence.Repositories;

public class InterfaceRepository : IInterfaceRepository
{
    private readonly AtlasNOCDbContext _context;
    public InterfaceRepository(AtlasNOCDbContext context) => _context = context;

    public async Task<DeviceInterface?> GetByIdAsync(Guid id, CancellationToken ct = default)
        => await _context.DeviceInterfaces.FirstOrDefaultAsync(i => i.Id == InterfaceId.From(id), ct);

    public async Task<IReadOnlyList<DeviceInterface>> ListByDeviceAsync(Guid deviceId, CancellationToken ct = default)
        => await _context.DeviceInterfaces
            .Where(i => i.DeviceId == DeviceId.From(deviceId))
            .AsNoTracking()
            .ToListAsync(ct);

    public Task AddAsync(DeviceInterface iface, CancellationToken ct = default)
    {
        _context.DeviceInterfaces.Add(iface);
        return Task.CompletedTask;
    }

    public Task UpdateAsync(DeviceInterface iface, CancellationToken ct = default)
    {
        _context.DeviceInterfaces.Update(iface);
        return Task.CompletedTask;
    }
}