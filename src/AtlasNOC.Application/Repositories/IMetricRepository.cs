using AtlasNOC.Domain.Entities;

namespace AtlasNOC.Application.Repositories;

public interface IMetricRepository
{
    Task AddSampleAsync(MetricSample sample, CancellationToken ct = default);
    Task AddSamplesAsync(IEnumerable<MetricSample> samples, CancellationToken ct = default);
    Task<IReadOnlyList<MetricSample>> QueryAsync(string resourceType, string resourceId,
        string metricName, DateTime fromUtc, DateTime toUtc, CancellationToken ct = default);
}