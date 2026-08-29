using AtlasNOC.Application.Repositories;
using AtlasNOC.Domain.Entities;
using AtlasNOC.Domain.Enums;
using AtlasNOC.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace AtlasNOC.Infrastructure.Persistence.Repositories;

public class IncidentRepository : IIncidentRepository
{
    private readonly AtlasNOCDbContext _context;
    public IncidentRepository(AtlasNOCDbContext context) => _context = context;

    public async Task<Incident?> GetByIdAsync(Guid id, CancellationToken ct = default)
        => await _context.Incidents.FirstOrDefaultAsync(i => i.Id == id, ct);

    public async Task<IReadOnlyList<Incident>> ListActiveAsync(CancellationToken ct = default)
        => await _context.Incidents
            .Where(i => i.Status != IncidentStatus.Resolved && i.Status != IncidentStatus.Closed)
            .AsNoTracking()
            .ToListAsync(ct);

    public Task AddAsync(Incident incident, CancellationToken ct = default)
    {
        _context.Incidents.Add(incident);
        return Task.CompletedTask;
    }

    public Task UpdateAsync(Incident incident, CancellationToken ct = default)
    {
        _context.Incidents.Update(incident);
        return Task.CompletedTask;
    }
}