using AtlasNOC.Domain.Data;
using AtlasNOC.Domain.Entities;
using AtlasNOC.Domain.Services.Interfaces;
using AtlasNOC.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;

namespace AtlasNOC.Domain.Services;

public sealed class MetricHistoryService : IMetricHistoryService
{
    private readonly AtlasNOCDbContext _dbContext;
    public MetricHistoryService(AtlasNOCDbContext dbContext) =>
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));

    public async Task SaveAsync(PollingResult result, CancellationToken cancellationToken = default)
    {
        await _dbContext.MetricSamples.AddAsync(MetricSample.FromPollingResult(result), cancellationToken);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<MetricSamplePage> QueryAsync(DeviceId deviceId, DateTime from, DateTime to,
        int page, int pageSize, CancellationToken cancellationToken = default)
    {
        if (from.Kind != DateTimeKind.Utc || to.Kind != DateTimeKind.Utc || from >= to)
            throw new ArgumentException("A valid UTC time range is required.");
        if (page < 1) throw new ArgumentOutOfRangeException(nameof(page));
        if (pageSize is < 1 or > 500) throw new ArgumentOutOfRangeException(nameof(pageSize));
        var query = _dbContext.MetricSamples.AsNoTracking()
            .Where(sample => sample.DeviceId == deviceId && sample.Timestamp >= from && sample.Timestamp <= to);
        var total = await query.CountAsync(cancellationToken);
        var items = await query.OrderByDescending(sample => sample.Timestamp)
            .Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(cancellationToken);
        return new MetricSamplePage(items, page, pageSize, total);
    }

    public Task<int> PruneAsync(DateTime olderThan, CancellationToken cancellationToken = default)
    {
        if (olderThan.Kind != DateTimeKind.Utc)
            throw new ArgumentException("Retention cutoff must be UTC.", nameof(olderThan));
        return _dbContext.MetricSamples.Where(sample => sample.Timestamp < olderThan)
            .ExecuteDeleteAsync(cancellationToken);
    }
}
