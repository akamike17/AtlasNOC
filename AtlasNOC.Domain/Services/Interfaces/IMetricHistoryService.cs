using AtlasNOC.Domain.Entities;
using AtlasNOC.Domain.ValueObjects;

namespace AtlasNOC.Domain.Services.Interfaces;

public interface IMetricHistoryService
{
    Task SaveAsync(PollingResult result, CancellationToken cancellationToken = default);
    Task<MetricSamplePage> QueryAsync(DeviceId deviceId, DateTime from, DateTime to,
        int page, int pageSize, CancellationToken cancellationToken = default);
    Task<int> PruneAsync(DateTime olderThan, CancellationToken cancellationToken = default);
}

public sealed record MetricSamplePage(IReadOnlyList<MetricSample> Items, int Page, int PageSize, int TotalCount);
