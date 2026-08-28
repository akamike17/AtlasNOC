using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;
using Microsoft.EntityFrameworkCore;
using AtlasNOC.Domain.Data;
using AtlasNOC.Domain.Entities;
using AtlasNOC.Domain.Enums;
using AtlasNOC.Domain.ValueObjects;

namespace AtlasNOC.Services;

/// <summary>
/// Background service that fetches CVE data from NVD API and stores vulnerabilities related to devices.
/// </summary>
public sealed class CveBackgroundService : BackgroundService
{
    private readonly ILogger<CveBackgroundService> _logger;
    private readonly IConfiguration _configuration;
    private readonly IServiceProvider _serviceProvider;
    private readonly HttpClient _httpClient;
    private readonly TimeSpan _fetchInterval;
    private readonly string _nvdApiUrl;
    private readonly string? _nvdApiKey;
    private DateTime _lastFetchTime;

    public CveBackgroundService(
        ILogger<CveBackgroundService> logger,
        IConfiguration configuration,
        IServiceProvider serviceProvider,
        IHttpClientFactory httpClientFactory)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
        _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
        _httpClient = httpClientFactory.CreateClient("NvdCve");

        _fetchInterval = TimeSpan.FromHours(
            _configuration.GetValue<int>("CveFetcher:IntervalHours", 24));
        _nvdApiUrl = _configuration["CveFetcher:NvdApiUrl"]
            ?? "https://services.nvd.nist.gov/rest/json/cves/2.0";
        _nvdApiKey = _configuration["CveFetcher:NvdApiKey"];
        _lastFetchTime = DateTime.UtcNow.AddDays(-1); // Initial fetch will get last 24h

        _httpClient.Timeout = TimeSpan.FromMinutes(5);
        if (!string.IsNullOrEmpty(_nvdApiKey))
        {
            _httpClient.DefaultRequestHeaders.Add("apiKey", _nvdApiKey);
        }
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("CVE Background Service started. Fetch interval: {Interval}", _fetchInterval);

        // Initial delay to let the app start up
        await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await FetchAndStoreCvesAsync(stoppingToken);
                _lastFetchTime = DateTime.UtcNow;
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching CVE data");
            }

            // Wait for next interval
            try
            {
                await Task.Delay(_fetchInterval, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
        }

        _logger.LogInformation("CVE Background Service stopped");
    }

    private async Task FetchAndStoreCvesAsync(CancellationToken cancellationToken)
    {
        using var scope = _serviceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AtlasNOCDbContext>();

        // Get active devices to know what vendors/products to look for
        var devices = await dbContext.Devices
            .Where(d => d.IsActive)
            .Select(d => new { d.Type, d.Name })
            .ToListAsync(cancellationToken);

        if (!devices.Any())
        {
            _logger.LogInformation("No active devices found, skipping CVE fetch");
            return;
        }

        var vendorKeywords = GetVendorKeywords(devices);
        var totalCvesStored = 0;

        foreach (var keyword in vendorKeywords)
        {
            try
            {
                var cves = await FetchCvesForKeywordAsync(keyword, cancellationToken);
                var stored = await StoreCvesAsync(dbContext, cves, keyword, cancellationToken);
                totalCvesStored += stored;

                _logger.LogInformation("Fetched {Count} CVEs for keyword '{Keyword}', stored {Stored}",
                    cves.Count, keyword, stored);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to fetch CVEs for keyword '{Keyword}'", keyword);
            }
        }

        _logger.LogInformation("CVE fetch cycle completed. Total CVEs stored: {Total}", totalCvesStored);
    }

    private List<string> GetVendorKeywords(IEnumerable<dynamic> devices)
    {
        var keywords = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "cisco", "juniper", "arista", "fortinet", "palo alto", "check point",
            "mikrotik", "ubiquiti", "hpe", "aruba", "dell", "emc",
            "linux", "windows", "vmware", "red hat", "ubuntu", "debian",
            "apache", "nginx", "mysql", "postgresql", "mongodb", "redis"
        };

        // Add device-specific keywords based on device types
        foreach (var device in devices)
        {
            switch (device.Type)
            {
                case DeviceType.Router:
                    keywords.Add("router");
                    break;
                case DeviceType.Switch:
                    keywords.Add("switch");
                    break;
                case DeviceType.Firewall:
                    keywords.Add("firewall");
                    break;
                case DeviceType.Server:
                    keywords.Add("server");
                    break;
                case DeviceType.AccessPoint:
                    keywords.Add("access point");
                    keywords.Add("wireless");
                    break;
            }
        }

        return keywords.Take(10).ToList(); // Limit to 10 keywords per cycle
    }

    private async Task<List<CveItem>> FetchCvesForKeywordAsync(string keyword, CancellationToken cancellationToken)
    {
        var url = $"{_nvdApiUrl}?keywordSearch={Uri.EscapeDataString(keyword)}" +
                  $"&pubStartDate={_lastFetchTime:yyyy-MM-ddTHH:mm:ss.fffZ}" +
                  $"&resultsPerPage=50";

        _logger.LogDebug("Fetching CVEs from: {Url}", url);

        var response = await _httpClient.GetFromJsonAsync<NvdResponse>(url, cancellationToken);

        return response?.Vulnerabilities?
            .Where(v => v.Cve is not null)
            .Select(v => v.Cve!)
            .ToList() ?? new List<CveItem>();
    }

    private async Task<int> StoreCvesAsync(AtlasNOCDbContext dbContext, List<CveItem> cves, string keyword, CancellationToken cancellationToken)
    {
        int stored = 0;

        foreach (var cve in cves)
        {
            try
            {
                // Check if CVE already exists
                var existing = await dbContext.CveRecords
                    .FirstOrDefaultAsync(c => c.CveId == cve.Id, cancellationToken);

                if (existing != null)
                {
                    // Update if needed
                    if (existing.LastModifiedDate < cve.LastModified)
                    {
                        UpdateCveRecord(existing, cve);
                        stored++;
                    }
                }
                else
                {
                    // Create new record
                    var record = CreateCveRecord(cve, keyword);
                    dbContext.CveRecords.Add(record);
                    stored++;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to store CVE {CveId}", cve.Id);
            }
        }

        if (stored > 0)
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }

        return stored;
    }

    private static CveRecord CreateCveRecord(CveItem cve, string keyword)
    {
        var metrics = cve.Metrics?.CvssMetricV31?.FirstOrDefault() as dynamic
                     ?? cve.Metrics?.CvssMetricV30?.FirstOrDefault() as dynamic
                     ?? cve.Metrics?.CvssMetricV2?.FirstOrDefault() as dynamic;

        var cvss = metrics?.CvssData;

        var references = cve.References?.Select(r => new
        {
            r.Url,
            r.Source,
            r.Tags
        }).ToList<object>() ?? new List<object>();

        var weaknesses = cve.Weaknesses?.SelectMany(w => w.Description
            .Where(d => d.Lang == "en")
            .Select(d => d.Value)).ToList<string>() ?? new List<string>();

        var configurations = cve.Configurations?.SelectMany(c => c.Nodes
            .SelectMany(n => n.CpeMatch
                .Where(m => m.Vulnerable)
                .Select(m => new { m.Criteria, m.Vulnerable }))
            .ToList<object>()) ?? new List<object>();

        return CveRecord.Create(
            cve.Id,
            cve.SourceIdentifier,
            cve.Published,
            cve.LastModified,
            cve.VulnStatus,
            cve.Descriptions?.FirstOrDefault(d => d.Lang == "en")?.Value
                ?? cve.Descriptions?.FirstOrDefault()?.Value
                ?? string.Empty,
            cvss?.Version,
            cvss?.BaseScore,
            cvss?.BaseSeverity,
            cvss?.VectorString,
            metrics?.ExploitabilityScore,
            metrics?.ImpactScore,
            keyword,
            JsonSerializer.Serialize(references),
            JsonSerializer.Serialize(weaknesses),
            JsonSerializer.Serialize(configurations)
        );
    }

    private static void UpdateCveRecord(CveRecord existing, CveItem cve)
    {
        var metrics = cve.Metrics?.CvssMetricV31?.FirstOrDefault() as dynamic
                     ?? cve.Metrics?.CvssMetricV30?.FirstOrDefault() as dynamic
                     ?? cve.Metrics?.CvssMetricV2?.FirstOrDefault() as dynamic;

        var cvss = metrics?.CvssData;

        var references = cve.References?.Select(r => new
        {
            r.Url,
            r.Source,
            r.Tags
        }).ToList<object>() ?? new List<object>();

        var weaknesses = cve.Weaknesses?.SelectMany(w => w.Description
            .Where(d => d.Lang == "en")
            .Select(d => d.Value)).ToList<string>() ?? new List<string>();

        var configurations = cve.Configurations?.SelectMany(c => c.Nodes
            .SelectMany(n => n.CpeMatch
                .Where(m => m.Vulnerable)
                .Select(m => new { m.Criteria, m.Vulnerable }))
            .ToList<object>()) ?? new List<object>();

        existing.Update(
            cve.VulnStatus,
            cve.Descriptions?.FirstOrDefault(d => d.Lang == "en")?.Value
                ?? cve.Descriptions?.FirstOrDefault()?.Value
                ?? existing.Description,
            cvss?.Version,
            cvss?.BaseScore,
            cvss?.BaseSeverity,
            cvss?.VectorString,
            metrics?.ExploitabilityScore,
            metrics?.ImpactScore,
            JsonSerializer.Serialize(references),
            JsonSerializer.Serialize(weaknesses),
            JsonSerializer.Serialize(configurations)
        );
    }

    // DTOs for NVD API
    private class NvdResponse
    {
        public string? ResultsPerPage { get; set; }
        public string? StartIndex { get; set; }
        public string? TotalResults { get; set; }
        public string? Format { get; set; }
        public string? Version { get; set; }
        public string? Timestamp { get; set; }
        public List<NvdVulnerability>? Vulnerabilities { get; set; }
    }

    private class NvdVulnerability
    {
        public CveItem? Cve { get; set; }
    }

    private class CveItem
    {
        public string Id { get; set; } = string.Empty;
        public string SourceIdentifier { get; set; } = string.Empty;
        public DateTime Published { get; set; }
        public DateTime LastModified { get; set; }
        public string VulnStatus { get; set; } = string.Empty;
        public List<CveDescription>? Descriptions { get; set; }
        public CveMetrics? Metrics { get; set; }
        public List<CveWeakness>? Weaknesses { get; set; }
        public List<CveConfiguration>? Configurations { get; set; }
        public List<CveReference>? References { get; set; }
    }

    private class CveDescription
    {
        public string Lang { get; set; } = string.Empty;
        public string Value { get; set; } = string.Empty;
    }

    private class CveMetrics
    {
        public List<CvssMetricV31>? CvssMetricV31 { get; set; }
        public List<CvssMetricV30>? CvssMetricV30 { get; set; }
        public List<CvssMetricV2>? CvssMetricV2 { get; set; }
    }

    private class CvssMetricV31
    {
        public CvssDataV31? CvssData { get; set; }
        public double? ExploitabilityScore { get; set; }
        public double? ImpactScore { get; set; }
    }

    private class CvssMetricV30
    {
        public CvssDataV30? CvssData { get; set; }
        public double? ExploitabilityScore { get; set; }
        public double? ImpactScore { get; set; }
    }

    private class CvssMetricV2
    {
        public CvssDataV2? CvssData { get; set; }
        public double? ExploitabilityScore { get; set; }
        public double? ImpactScore { get; set; }
    }

    private class CvssDataV31
    {
        public string Version { get; set; } = string.Empty;
        public double? BaseScore { get; set; }
        public string BaseSeverity { get; set; } = string.Empty;
        public string VectorString { get; set; } = string.Empty;
    }

    private class CvssDataV30
    {
        public string Version { get; set; } = string.Empty;
        public double? BaseScore { get; set; }
        public string BaseSeverity { get; set; } = string.Empty;
        public string VectorString { get; set; } = string.Empty;
    }

    private class CvssDataV2
    {
        public string Version { get; set; } = string.Empty;
        public double? BaseScore { get; set; }
        public string VectorString { get; set; } = string.Empty;
    }

    private class CveWeakness
    {
        public string Source { get; set; } = string.Empty;
        public string Type { get; set; } = string.Empty;
        public List<CweDescription> Description { get; set; } = new();
    }

    private class CweDescription
    {
        public string Lang { get; set; } = string.Empty;
        public string Value { get; set; } = string.Empty;
    }

    private class CveConfiguration
    {
        public List<CveNode> Nodes { get; set; } = new();
    }

    private class CveNode
    {
        public string Operator { get; set; } = string.Empty;
        public List<CpeMatch> CpeMatch { get; set; } = new();
    }

    private class CpeMatch
    {
        public bool Vulnerable { get; set; }
        public string Criteria { get; set; } = string.Empty;
        public string? MatchCriteriaId { get; set; }
    }

    private class CveReference
    {
        public string Url { get; set; } = string.Empty;
        public string Source { get; set; } = string.Empty;
        public List<string>? Tags { get; set; }
    }
}