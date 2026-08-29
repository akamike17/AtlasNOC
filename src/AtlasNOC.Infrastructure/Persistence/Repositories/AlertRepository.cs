using AtlasNOC.Application.Repositories;
using AtlasNOC.Domain.Entities;
using AtlasNOC.Domain.Enums;
using AtlasNOC.Domain.ValueObjects;
using AtlasNOC.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace AtlasNOC.Infrastructure.Persistence.Repositories;

public class AlertRepository : IAlertRepository
{
    private readonly AtlasNOCDbContext _context;
    public AlertRepository(AtlasNOCDbContext context) => _context = context;

    public async Task<Alert?> GetByIdAsync(Guid id, CancellationToken ct = default)
        => await _context.Alerts.FirstOrDefaultAsync(a => a.Id == AlertId.From(id), ct);

    public async Task<IReadOnlyList<Alert>> ListOpenAsync(CancellationToken ct = default)
        => await _context.Alerts
            .Where(a => a.State != AlertState.Resolved)
            .AsNoTracking()
            .ToListAsync(ct);

    public async Task<IReadOnlyList<Alert>> ListAsync(CancellationToken ct = default)
        => await _context.Alerts.AsNoTracking().ToListAsync(ct);

    public Task AddAsync(Alert alert, CancellationToken ct = default)
    {
        _context.Alerts.Add(alert);
        return Task.CompletedTask;
    }

    public Task UpdateAsync(Alert alert, CancellationToken ct = default)
    {
        _context.Alerts.Update(alert);
        return Task.CompletedTask;
    }
}