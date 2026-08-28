using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using AtlasNOC.Domain.Entities;

namespace AtlasNOC.Domain.Services.Interfaces;

public interface ICveService
{
    Task<IReadOnlyList<CveRecord>> GetAllAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<CveRecord>> GetBySeverityAsync(string severity, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<CveRecord>> GetSinceAsync(DateTime since, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<CveRecord>> SearchByKeywordAsync(string keyword, CancellationToken cancellationToken = default);
    Task<CveRecord?> GetByCveIdAsync(string cveId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<CveRecord>> GetCriticalAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<CveRecord>> GetRecentAsync(int count, CancellationToken cancellationToken = default);
    Task<CveStats> GetStatsAsync(CancellationToken cancellationToken = default);
    Task<int> TriggerFetchAsync(CancellationToken cancellationToken = default);
}

public class CveStats
{
    public int Total { get; init; }
    public int Critical { get; init; }
    public int High { get; init; }
    public int Medium { get; init; }
    public int Low { get; init; }
    public int None { get; init; }
    public DateTime? LastFetched { get; init; }
}