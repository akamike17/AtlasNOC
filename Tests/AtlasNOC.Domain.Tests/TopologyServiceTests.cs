using AtlasNOC.Domain.Entities;
using AtlasNOC.Domain.Enums;
using AtlasNOC.Domain.Services;
using AtlasNOC.Domain.Services.Interfaces;
using Moq;
using Xunit;

namespace AtlasNOC.Domain.Tests;

public sealed class TopologyServiceTests
{
    [Fact]
    public async Task RebuildTopologyAsync_CreatesEvidenceBasedDeterministicLldpLink()
    {
        var repository = new InMemoryRepository<Device>(device => device.Id.Value);
        var source = Device.Create("switch-a", "192.0.2.1", DeviceType.Switch, "test");
        var target = Device.Create("switch-b", "192.0.2.2", DeviceType.Switch, "test");
        source.UpdateStatus(DeviceStatus.Up, "test");
        target.UpdateStatus(DeviceStatus.Up, "test");
        await repository.AddAsync(source);
        await repository.AddAsync(target);

        var discovered = new[]
        {
            Discovered("192.0.2.1", "switch-a", new DiscoveredNeighbor(
                "Gi0/1", "192.0.2.2", "Gi0/2", "switch-b", NeighborProtocol.Lldp, 0.95)),
            Discovered("192.0.2.2", "switch-b")
        };
        var discovery = new Mock<IDiscoveryService>();
        discovery.Setup(service => service.GetDiscoveredDevicesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(discovered);
        var service = new TopologyService(repository, discovery.Object, new TestLogger<TopologyService>());

        var first = await service.RebuildTopologyAsync();
        var second = await service.RebuildTopologyAsync();

        var link = Assert.Single(first.Links);
        Assert.Equal(LinkType.Lldp, link.Type);
        Assert.Equal(0.95, link.Confidence);
        Assert.Equal(LinkStatus.Up, link.Status);
        Assert.Equal(link.Id, Assert.Single(second.Links).Id);
        Assert.Equal(2, first.Nodes.Count);
    }

    [Fact]
    public async Task RebuildTopologyAsync_DoesNotInventLinkForAmbiguousNeighbor()
    {
        var repository = new InMemoryRepository<Device>(device => device.Id.Value);
        await repository.AddAsync(Device.Create("same", "192.0.2.1", DeviceType.Switch, "test"));
        await repository.AddAsync(Device.Create("same", "192.0.2.2", DeviceType.Switch, "test"));
        await repository.AddAsync(Device.Create("source", "192.0.2.3", DeviceType.Switch, "test"));
        var discovery = new Mock<IDiscoveryService>();
        discovery.Setup(service => service.GetDiscoveredDevicesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[]
            {
                Discovered("192.0.2.3", "source", new DiscoveredNeighbor(
                    "1", "unknown", "2", "same", NeighborProtocol.Lldp, 0.9))
            });
        var service = new TopologyService(repository, discovery.Object, new TestLogger<TopologyService>());

        var topology = await service.RebuildTopologyAsync();

        Assert.Empty(topology.Links);
    }

    private static DiscoveredDevice Discovered(
        string ip, string hostname, params DiscoveredNeighbor[] neighbors) =>
        new(Guid.NewGuid(), ip, hostname, "test", null, "Generic", DeviceType.Switch,
            Array.Empty<DiscoveredInterface>(), neighbors, DateTime.UtcNow,
            new DiscoveryEvidence(true, true, neighbors.Any(item => item.Protocol == NeighborProtocol.Lldp),
                false, false, false, Array.Empty<string>(), Array.Empty<string>()));
}
