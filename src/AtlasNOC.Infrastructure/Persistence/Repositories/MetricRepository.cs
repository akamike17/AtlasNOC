using AtlasNOC.Application.Repositories;
using AtlasNOC.Domain.Entities;
using AtlasNOC.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace AtlasNOC.Infrastructure.Persistence.Repositories;

public class MetricRepository : IMetricRepository
{
    private readonly AtlasNOCDbContext _context;
    public MetricRepository(AtlasNOCDbContext context) => _context = context;

    public Task AddSampleAsync(MetricSample sample, CancellationToken ct = default)
    {
        _context.MetricSamples.Add(sample);
        return Task.CompletedTask;
    }

    public Task AddSamplesAsync(IEnumerable<MetricSample> samples, CancellationToken ct = default)
    {
        _context.MetricSamples.AddRange(samples);
        return Task.CompletedTask;
    }

    public async Task<IReadOnlyList<MetricSample>> QueryAsync(string resourceType, string resourceId,
        string metricName, DateTime fromUtc, DateTime toUtc, CancellationToken ct = default)
        => await _context.MetricSamples
            .Where(m => m.ResourceType == resourceType
                && m.ResourceId == resourceId
                && m.MetricName == metricName
                && m.TimestampUtc >= fromUtc
                && m.TimestampUtc <= toUtc)
            .OrderBy(m => m.TimestampUtc)
            .AsNoTracking()
            .ToListAsync(ct);
}