using AtlasNOC.Application.Services;
using AtlasNOC.Domain.Enums;
using AtlasNOC.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace AtlasNOC.Infrastructure.Services;

public sealed class OperationsSnapshotService : IOperationsSnapshotService
{
    private readonly AtlasNOCDbContext _db;
    public OperationsSnapshotService(AtlasNOCDbContext db) => _db = db;

    public async Task<OperationsSnapshotDto> GetAsync(CancellationToken ct = default)
    {
        var devices = await _db.Devices.AsNoTracking().ToListAsync(ct);
        var openAlerts = await _db.Alerts.AsNoTracking().CountAsync(a => a.State != AlertState.Resolved, ct);
        var confirmedLinks = await _db.NetworkLinks.AsNoTracking().CountAsync(l => l.IsConfirmed && !l.IsStale, ct);
        var online = devices.Count(d => d.Status == DeviceStatus.Up);
        var offline = devices.Count(d => d.Status == DeviceStatus.Down);
        var states = devices.Select(d => new OperationsDeviceDto(d.Id.Value, d.Hostname, d.ManagementIp,
            (int)d.Status, d.IsManaged, d.IsManaged ? "MONITORED" : "OBSERVED")).ToList();
        return new OperationsSnapshotDto(DateTime.UtcNow, devices.Count, online, offline,
            devices.Count - online - offline, openAlerts, confirmedLinks, states);
    }
}
