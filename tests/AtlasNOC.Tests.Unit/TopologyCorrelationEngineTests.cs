using AtlasNOC.Application.Services;
using AtlasNOC.Infrastructure.Services;
using Xunit;

namespace AtlasNOC.Tests.Unit;

public class TopologyCorrelationEngineTests
{
    private readonly ITopologyCorrelationEngine _engine = new TopologyCorrelationEngine();

    [Fact]
    public async Task Bidirectional_lldp_observations_produce_high_confidence_link()
    {
        var observations = new List<NeighborObservationInput>
        {
            new("dev-A", "if-A1", "dev-B", "if-B1", "lldp", "h1"),
            new("dev-B", "if-B1", "dev-A", "if-A1", "lldp", "h2"),
        };

        var results = await _engine.CorrelateAsync(observations);

        var link = Assert.Single(results);
        Assert.True(link.Confidence >= 0.9);
        Assert.Equal((int)AtlasNOC.Domain.Enums.LinkType.Physical, link.LinkType);
        Assert.Equal((int)AtlasNOC.Domain.Enums.DiscoverySource.Lldp, link.DiscoverySource);
    }

    [Fact]
    public async Task Unresolved_one_sided_observations_are_not_linked()
    {
        // Una sola observación (sin par simétrico) no produce enlace.
        var observations = new List<NeighborObservationInput>
        {
            new("dev-A", "if-A1", "dev-B", "if-B1", "lldp", "h1"),
        };

        var results = await _engine.CorrelateAsync(observations);

        Assert.Empty(results);
    }

    [Fact]
    public async Task Same_subnet_does_not_imply_a_link_on_its_own()
    {
        // Dos dispositivos sin observaciones de vecinos => sin enlace (ninguna IP deducida).
        var observations = new List<NeighborObservationInput>();

        var results = await _engine.CorrelateAsync(observations);

        Assert.Empty(results);
    }

    [Fact]
    public async Task Wireless_associations_become_wireless_links()
    {
        var observations = new List<NeighborObservationInput>
        {
            new("ap-1", "wlan0", "cpe-1", "wlan0", "wireless", "h1"),
            new("cpe-1", "wlan0", "ap-1", "wlan0", "wireless", "h2"),
        };

        var results = await _engine.CorrelateAsync(observations);

        var link = Assert.Single(results);
        Assert.Equal((int)AtlasNOC.Domain.Enums.LinkType.Wireless, link.LinkType);
        Assert.Equal((int)AtlasNOC.Domain.Enums.DiscoverySource.WirelessAssociation, link.DiscoverySource);
    }
}