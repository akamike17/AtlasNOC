using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using AtlasNOC.Domain.Data;
using AtlasNOC.Domain.Entities;
using AtlasNOC.Domain.Services.Interfaces;

namespace AtlasNOC.Domain.Services;

public class CveService : ICveService
{
    private readonly IRepository<CveRecord> _repository;
    private readonly ILogger<CveService> _logger;

    public CveService(IRepository<CveRecord> repository, ILogger<CveService> logger)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public Task<IReadOnlyList<CveRecord>> GetAllAsync(CancellationToken cancellationToken = default)
        => _repository.GetAllAsync(cancellationToken);

    public async Task<IReadOnlyList<CveRecord>> GetBySeverityAsync(string severity, CancellationToken cancellationToken = default)
    {
        var all = await _repository.GetAllAsync(cancellationToken);
        return all.Where(c => string.Equals(c.CvssBaseSeverity, severity, StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(c => c.PublishedDate)
            .ToList()
            .AsReadOnly();
    }

    public async Task<IReadOnlyList<CveRecord>> GetSinceAsync(DateTime since, CancellationToken cancellationToken = default)
    {
        var all = await _repository.GetAllAsync(cancellationToken);
        return all.Where(c => c.PublishedDate >= since)
            .OrderByDescending(c => c.PublishedDate)
            .ToList()
            .AsReadOnly();
    }

    public async Task<IReadOnlyList<CveRecord>> SearchByKeywordAsync(string keyword, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(keyword))
            return await GetAllAsync(cancellationToken);

        var all = await _repository.GetAllAsync(cancellationToken);
        var term = keyword.ToLowerInvariant();
        return all.Where(c =>
            c.Keywords.ToLowerInvariant().Contains(term) ||
            c.CveId.ToLowerInvariant().Contains(term) ||
            (c.Description?.ToLowerInvariant().Contains(term) ?? false))
            .OrderByDescending(c => c.PublishedDate)
            .ToList()
            .AsReadOnly();
    }

    public async Task<CveRecord?> GetByCveIdAsync(string cveId, CancellationToken cancellationToken = default)
    {
        var all = await _repository.GetAllAsync(cancellationToken);
        return all.FirstOrDefault(c => string.Equals(c.CveId, cveId, StringComparison.OrdinalIgnoreCase));
    }

    public async Task<IReadOnlyList<CveRecord>> GetCriticalAsync(CancellationToken cancellationToken = default)
    {
        var all = await _repository.GetAllAsync(cancellationToken);
        return all.Where(c => c.CvssBaseSeverity is "CRITICAL" or "HIGH")
            .OrderByDescending(c => c.CvssBaseScore ?? 0)
            .ThenByDescending(c => c.PublishedDate)
            .ToList()
            .AsReadOnly();
    }

    public async Task<IReadOnlyList<CveRecord>> GetRecentAsync(int count, CancellationToken cancellationToken = default)
    {
        var all = await _repository.GetAllAsync(cancellationToken);
        return all.OrderByDescending(c => c.PublishedDate).Take(count).ToList().AsReadOnly();
    }

    public async Task<CveStats> GetStatsAsync(CancellationToken cancellationToken = default)
    {
        var all = await _repository.GetAllAsync(cancellationToken);

        return new CveStats
        {
            Total = all.Count,
            Critical = all.Count(c => c.CvssBaseSeverity == "CRITICAL"),
            High = all.Count(c => c.CvssBaseSeverity == "HIGH"),
            Medium = all.Count(c => c.CvssBaseSeverity == "MEDIUM"),
            Low = all.Count(c => c.CvssBaseSeverity == "LOW"),
            None = all.Count(c => c.CvssBaseSeverity == "NONE" || string.IsNullOrEmpty(c.CvssBaseSeverity)),
            LastFetched = all.Any() ? all.Max(c => c.CreatedAt) : null
        };
    }

    public async Task<int> TriggerFetchAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Manual CVE fetch triggered via API");
        // This would need to be implemented to trigger the background service
        // For now, return 0 as this requires BackgroundService interaction
        return await Task.FromResult(0);
    }
}