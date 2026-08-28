using System.Text.Json;
using AtlasNOC.Domain.Services.Interfaces;

namespace AtlasNOC.Domain.Entities;

public sealed class DiscoveryRun
{
    public Guid Id { get; private set; }
    public string SubnetCidr { get; private set; } = string.Empty;
    public DateTime StartedAt { get; private set; }
    public DateTime? CompletedAt { get; private set; }
    public DiscoveryStatus Status { get; private set; }
    public int TargetsScanned { get; private set; }
    public int TargetsReachable { get; private set; }
    public string DevicesJson { get; private set; } = "[]";
    public string? ErrorMessage { get; private set; }

    private DiscoveryRun() { }

    public static DiscoveryRun Start(Guid id, string subnetCidr, DateTime startedAt) => new()
    {
        Id = id,
        SubnetCidr = subnetCidr,
        StartedAt = startedAt,
        Status = DiscoveryStatus.Running
    };

    public void Complete(DiscoveryResult result)
    {
        CompletedAt = result.CompletedAt;
        Status = result.Status;
        TargetsScanned = result.TargetsScanned;
        TargetsReachable = result.TargetsReachable;
        DevicesJson = JsonSerializer.Serialize(result.Devices);
        ErrorMessage = result.ErrorMessage is { Length: > 1000 }
            ? result.ErrorMessage[..1000]
            : result.ErrorMessage;
    }

    public DiscoveryResult ToResult() => new(Id, StartedAt, CompletedAt, Status,
        JsonSerializer.Deserialize<IReadOnlyList<DiscoveredDevice>>(DevicesJson) ?? Array.Empty<DiscoveredDevice>(),
        TargetsScanned, TargetsReachable, ErrorMessage);
}
