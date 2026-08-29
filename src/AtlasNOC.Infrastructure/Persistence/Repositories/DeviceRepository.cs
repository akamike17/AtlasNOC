using AtlasNOC.Application.Repositories;
using AtlasNOC.Domain.Entities;
using AtlasNOC.Domain.ValueObjects;
using AtlasNOC.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace AtlasNOC.Infrastructure.Persistence.Repositories;

public class DeviceRepository : IDeviceRepository
{
    private readonly AtlasNOCDbContext _context;

    public DeviceRepository(AtlasNOCDbContext context) => _context = context;

    public async Task<Device?> GetByIdAsync(Guid id, CancellationToken ct = default)
        => await _context.Devices
            .FirstOrDefaultAsync(d => d.Id == DeviceId.From(id), ct);

    public async Task<Device?> GetByManagementIpAsync(string ip, CancellationToken ct = default)
        => await _context.Devices
            .FirstOrDefaultAsync(d => d.ManagementIp == ip, ct);

    public async Task<IReadOnlyList<Device>> ListAsync(CancellationToken ct = default)
        => await _context.Devices.AsNoTracking().ToListAsync(ct);

    public async Task<IReadOnlyList<Device>> ListManagedAsync(CancellationToken ct = default)
        => await _context.Devices.Where(d => d.IsManaged).AsNoTracking().ToListAsync(ct);

    public Task AddAsync(Device device, CancellationToken ct = default)
    {
        _context.Devices.Add(device);
        return Task.CompletedTask;
    }

    public Task UpdateAsync(Device device, CancellationToken ct = default)
    {
        _context.Devices.Update(device);
        return Task.CompletedTask;
    }
}