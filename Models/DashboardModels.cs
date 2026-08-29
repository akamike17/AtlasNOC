using AtlasNOC.Domain.Entities;
using AtlasNOC.Domain.Services.Interfaces;

namespace AtlasNOC.Web.Models;

public sealed class DashboardActivityItem
{
    public required string Kind { get; init; }      // device | alert | incident | discovery
    public required string Label { get; init; }
    public string? Subtext { get; init; }
    public DateTime At { get; init; }
    public string? SeverityBadge { get; init; }
    public string? Link { get; init; }
    public string? Extra { get; init; }
}

public sealed class DashboardViewModel
{
    public int TotalDevices { get; init; }
    public int Up { get; init; }
    public int Down { get; init; }
    public int Unknown { get; init; }
    public int Inactive { get; init; }
    public int ActiveAlerts { get; init; }
    public int CriticalAlerts { get; init; }
    public int OpenIncidents { get; init; }
    public int CriticalCves { get; init; }
    public int TotalCves { get; init; }
    public CveStats? CveStats { get; init; }
    public bool TopologyKnown { get; init; }
    public int TopologyNodes { get; init; }
    public int TopologyLinks { get; init; }
    public int TopologyUp { get; init; }
    public int TopologyDown { get; init; }
    public bool MySqlHealthy { get; init; }
    public DateTime? LastPolledAt { get; init; }
    public DateTime? LastDataUpdate { get; init; }
    public IReadOnlyList<DiscoveryRun> RecentRuns { get; init; } = Array.Empty<DiscoveryRun>();
    public IReadOnlyList<DashboardActivityItem> Activity { get; init; } = Array.Empty<DashboardActivityItem>();
    public IReadOnlyList<CveRecord> CriticalCveSamples { get; init; } = Array.Empty<CveRecord>();
    public IReadOnlyList<Device> DownDevices { get; init; } = Array.Empty<Device>();
}