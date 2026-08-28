using System;
using System.Text.Json.Serialization;

namespace AtlasNOC.Domain.Entities;

/// <summary>
/// CVE (Common Vulnerabilities and Exposures) record fetched from NVD API.
/// </summary>
public class CveRecord
{
    public Guid Id { get; init; }
    public string CveId { get; init; } = string.Empty;           // e.g., "CVE-2024-12345"
    public string SourceIdentifier { get; init; } = string.Empty;
    public DateTime PublishedDate { get; init; }
    public DateTime LastModifiedDate { get; set; }
    public string VulnStatus { get; set; } = string.Empty;       // e.g., "Analyzed", "Modified"
    public string Description { get; set; } = string.Empty;

    // CVSS Scores
    public string? CvssVersion { get; set; }                     // "3.1", "3.0", "2.0"
    public double? CvssBaseScore { get; set; }
    public string? CvssBaseSeverity { get; set; }                // "CRITICAL", "HIGH", "MEDIUM", "LOW", "NONE"
    public string? CvssVectorString { get; set; }
    public double? ExploitabilityScore { get; set; }
    public double? ImpactScore { get; set; }

    // Search/Filter
    public string Keywords { get; init; } = string.Empty;         // Keywords used to find this CVE

    // JSON serialized complex data
    public string References { get; set; } = "[]";               // JSON array of {url, source, tags}
    public string Weaknesses { get; set; } = "[]";               // JSON array of CWE IDs
    public string Configurations { get; set; } = "[]";           // JSON array of vulnerable CPEs

    // Metadata
    public DateTime CreatedAt { get; init; }
    public DateTime? UpdatedAt { get; set; }

    // NHibernate-compatible parameterless constructor
    private CveRecord() { }

    public CveRecord(Guid id, string cveId, string sourceIdentifier, DateTime publishedDate,
        DateTime lastModifiedDate, string vulnStatus, string description,
        string? cvssVersion, double? cvssBaseScore, string? cvssBaseSeverity,
        string? cvssVectorString, double? exploitabilityScore, double? impactScore,
        string keywords, string references, string weaknesses, string configurations,
        DateTime createdAt)
    {
        Id = id;
        CveId = cveId ?? throw new ArgumentNullException(nameof(cveId));
        SourceIdentifier = sourceIdentifier ?? throw new ArgumentNullException(nameof(sourceIdentifier));
        PublishedDate = publishedDate;
        LastModifiedDate = lastModifiedDate;
        VulnStatus = vulnStatus ?? throw new ArgumentNullException(nameof(vulnStatus));
        Description = description ?? throw new ArgumentNullException(nameof(description));
        CvssVersion = cvssVersion;
        CvssBaseScore = cvssBaseScore;
        CvssBaseSeverity = cvssBaseSeverity;
        CvssVectorString = cvssVectorString;
        ExploitabilityScore = exploitabilityScore;
        ImpactScore = impactScore;
        Keywords = keywords ?? throw new ArgumentNullException(nameof(keywords));
        References = references ?? "[]";
        Weaknesses = weaknesses ?? "[]";
        Configurations = configurations ?? "[]";
        CreatedAt = createdAt;
    }

    public static CveRecord Create(string cveId, string sourceIdentifier, DateTime publishedDate,
        DateTime lastModifiedDate, string vulnStatus, string description,
        string? cvssVersion, double? cvssBaseScore, string? cvssBaseSeverity,
        string? cvssVectorString, double? exploitabilityScore, double? impactScore,
        string keywords, string references, string weaknesses, string configurations)
        => new(Guid.NewGuid(), cveId, sourceIdentifier, publishedDate, lastModifiedDate,
            vulnStatus, description, cvssVersion, cvssBaseScore, cvssBaseSeverity,
            cvssVectorString, exploitabilityScore, impactScore, keywords,
            references, weaknesses, configurations, DateTime.UtcNow);

    public void Update(string vulnStatus, string description, string? cvssVersion,
        double? cvssBaseScore, string? cvssBaseSeverity, string? cvssVectorString,
        double? exploitabilityScore, double? impactScore, string references,
        string weaknesses, string configurations)
    {
        VulnStatus = vulnStatus ?? throw new ArgumentNullException(nameof(vulnStatus));
        Description = description ?? throw new ArgumentNullException(nameof(description));
        CvssVersion = cvssVersion ?? CvssVersion;
        CvssBaseScore = cvssBaseScore ?? CvssBaseScore;
        CvssBaseSeverity = cvssBaseSeverity ?? CvssBaseSeverity;
        CvssVectorString = cvssVectorString ?? CvssVectorString;
        ExploitabilityScore = exploitabilityScore ?? ExploitabilityScore;
        ImpactScore = impactScore ?? ImpactScore;
        References = references ?? References;
        Weaknesses = weaknesses ?? Weaknesses;
        Configurations = configurations ?? Configurations;
        UpdatedAt = DateTime.UtcNow;
    }
}