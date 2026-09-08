using AtlasNOC.Application.Services;
using AtlasNOC.Domain.Entities;
using AtlasNOC.Domain.Enums;
using AtlasNOC.Domain.ValueObjects;
using AtlasNOC.Infrastructure.Services;
using AtlasNOC.Infrastructure.Workers;
using Xunit;

namespace AtlasNOC.Tests.Unit;

public class NeighborObservationTests
{
    private readonly ITopologyCorrelationEngine _engine = new TopologyCorrelationEngine();

    [Fact]
    public async Task Pending_observation_resolves_when_counterpart_arrives_later()
    {
        var first = new NeighborObservationInput("guid-a", "switch-a", "if-a", "switch-b", "if-b", "lldp", "h1");
        Assert.Empty(await _engine.CorrelateAsync(new[] { first }));

        var second = new NeighborObservationInput("guid-b", "switch-b", "if-b", "switch-a", "if-a", "lldp", "h2");
        Assert.Single(await _engine.CorrelateAsync(new[] { first, second }));
    }

    [Fact]
    public void Duplicate_remote_hostname_is_ambiguous()
    {
        var observation = NewObservation("duplicate");
        TopologyCorrelationWorker.ClassifyObservation(observation,
            new Dictionary<string, int> { ["duplicate"] = 2 }, new HashSet<string>());
        Assert.Equal(NeighborObservationStatus.Ambiguous, observation.Status);
    }

    [Fact]
    public void Bidirectional_result_marks_only_participating_observation_resolved()
    {
        var observation = NewObservation("switch-b");
        TopologyCorrelationWorker.ClassifyObservation(observation,
            new Dictionary<string, int> { ["switch-b"] = 1 },
            new HashSet<string> { observation.LocalInterfaceId.Value.ToString() });
        Assert.Equal(NeighborObservationStatus.Resolved, observation.Status);
    }

    [Fact]
    public void Observation_without_counterpart_stays_pending()
    {
        var observation = NewObservation("switch-b");
        TopologyCorrelationWorker.ClassifyObservation(observation,
            new Dictionary<string, int> { ["switch-b"] = 1 }, new HashSet<string>());
        Assert.Equal(NeighborObservationStatus.Pending, observation.Status);
    }

    private static NeighborObservation NewObservation(string remote) => new(
        DeviceId.New(), InterfaceId.New(), remote, NeighborProtocol.Lldp, Guid.NewGuid().ToString("N"), "Gi1/0/1");
}
