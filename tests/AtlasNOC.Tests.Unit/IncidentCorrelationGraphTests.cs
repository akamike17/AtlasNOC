using AtlasNOC.Domain.Entities;
using AtlasNOC.Domain.Enums;
using AtlasNOC.Infrastructure.Services;
using Xunit;

namespace AtlasNOC.Tests.Unit;

public class IncidentCorrelationGraphTests
{
    [Fact]
    public void ConfirmedHierarchyLinks_CreateTransitiveDownstreamGraph()
    {
        var core = Device("core", DeviceType.Core, 1);
        var distribution = Device("distribution", DeviceType.Distribution, 2);
        var ap = Device("ap", DeviceType.AccessPoint, 3);
        var interfaces = new[]
        {
            new DeviceInterface(core.Id, 1, "uplink"),
            new DeviceInterface(distribution.Id, 1, "core"),
            new DeviceInterface(distribution.Id, 2, "access"),
            new DeviceInterface(ap.Id, 1, "uplink")
        };
        var links = new[]
        {
            new NetworkLink(interfaces[0].Id, interfaces[1].Id, LinkType.Physical, DiscoverySource.Lldp, .95),
            new NetworkLink(interfaces[2].Id, interfaces[3].Id, LinkType.Physical, DiscoverySource.Lldp, .95)
        };

        var graph = IncidentCorrelationEngine.BuildDirectedGraph(
            new[] { core, distribution, ap }, interfaces, links);
        var affected = IncidentCorrelationEngine.FindDownstream(graph, core.Id.Value);

        Assert.Contains(distribution.Id.Value, affected);
        Assert.Contains(ap.Id.Value, affected);
    }

    [Theory]
    [InlineData(DeviceType.Router, DeviceType.Switch)]
    [InlineData(DeviceType.Core, DeviceType.Core)]
    [InlineData(DeviceType.Unknown, DeviceType.Cpe)]
    public void AmbiguousRoles_AreNeverOriented(DeviceType left, DeviceType right)
        => Assert.Null(IncidentCorrelationEngine.Orient(Guid.NewGuid(), left, Guid.NewGuid(), right));

    [Fact]
    public void UnconfirmedOrStaleLinks_DoNotCreateDependencies()
    {
        var core = Device("core", DeviceType.Core, 4);
        var cpe = Device("cpe", DeviceType.Cpe, 5);
        var a = new DeviceInterface(core.Id, 1, "a");
        var b = new DeviceInterface(cpe.Id, 1, "b");
        var unconfirmed = new NetworkLink(a.Id, b.Id, LinkType.Unknown, DiscoverySource.Unknown, .2);
        var stale = new NetworkLink(a.Id, b.Id, LinkType.Physical, DiscoverySource.Lldp, .95);
        stale.MarkStale();

        var graph = IncidentCorrelationEngine.BuildDirectedGraph(
            new[] { core, cpe }, new[] { a, b }, new[] { unconfirmed, stale });

        Assert.Empty(IncidentCorrelationEngine.FindDownstream(graph, core.Id.Value));
    }

    [Fact]
    public void AcknowledgedAvailabilityAlert_RemainsCompatibleEvidence()
    {
        var alert = new Alert(Guid.NewGuid(), "Device", Guid.NewGuid().ToString(),
            "availability", 0, 50, AlertSeverity.High);
        alert.Acknowledge("operator");

        Assert.True(IncidentCorrelationEngine.IsCompatibleDeviceDownAlert(alert));
    }

    private static Device Device(string name, DeviceType type, int host)
        => new(name, $"192.0.2.{host}", type, Vendor.Generic);
}
