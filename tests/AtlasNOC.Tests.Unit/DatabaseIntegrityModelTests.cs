using AtlasNOC.Domain.Entities;
using AtlasNOC.Domain.Enums;
using AtlasNOC.Domain.ValueObjects;
using AtlasNOC.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Xunit;

namespace AtlasNOC.Tests.Unit;

public class DatabaseIntegrityModelTests
{
    [Fact]
    public void NetworkLink_NormalizesEndpointOrder()
    {
        var lower = InterfaceId.From(Guid.Parse("00000000-0000-0000-0000-000000000001"));
        var higher = InterfaceId.From(Guid.Parse("ffffffff-ffff-ffff-ffff-ffffffffffff"));

        var forward = new NetworkLink(lower, higher, LinkType.Physical, DiscoverySource.Lldp, .95);
        var reverse = new NetworkLink(higher, lower, LinkType.Physical, DiscoverySource.Lldp, .95);

        Assert.Equal(forward.AInterfaceId, reverse.AInterfaceId);
        Assert.Equal(forward.BInterfaceId, reverse.BInterfaceId);
        Assert.Equal(lower, reverse.AInterfaceId);
    }

    [Fact]
    public void Model_EnforcesUniqueCanonicalLinkAndRequiredForeignKeys()
    {
        var options = new DbContextOptionsBuilder<AtlasNOCDbContext>()
            .UseMySql("Server=localhost;Database=model_test",
                ServerVersion.Parse("8.0.36-mysql"))
            .Options;
        using var db = new AtlasNOCDbContext(options);

        var link = db.Model.FindEntityType(typeof(NetworkLink))!;
        Assert.Contains(link.GetIndexes(), index => index.IsUnique
            && index.Properties.Select(p => p.Name).SequenceEqual(new[] { "AInterfaceId", "BInterfaceId" }));

        Assert.Equal(2, link.GetForeignKeys().Count(fk =>
            fk.PrincipalEntityType.ClrType == typeof(DeviceInterface)
            && fk.DeleteBehavior == DeleteBehavior.Restrict));

        var observation = db.Model.FindEntityType(typeof(NeighborObservation))!;
        Assert.Equal(2, observation.GetForeignKeys().Count());
        Assert.All(observation.GetForeignKeys(), fk => Assert.Equal(DeleteBehavior.Restrict, fk.DeleteBehavior));
    }
}
