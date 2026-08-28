using AtlasNOC.Domain.Data;
using Microsoft.EntityFrameworkCore;
using Pomelo.EntityFrameworkCore.MySql.Infrastructure;
using Xunit;

namespace AtlasNOC.Domain.Tests;

public sealed class PersistenceModelTests
{
    [Fact]
    public void MySqlModel_CanBeFinalized_WithAllProductionEntities()
    {
        var options = new DbContextOptionsBuilder<AtlasNOCDbContext>()
            .UseMySql(
                "Server=127.0.0.1;Database=atlasnoc_model_test;User=test;Password=test;",
                new MySqlServerVersion(new Version(8, 0, 0)))
            .Options;

        using var context = new AtlasNOCDbContext(options);
        var entityNames = context.Model.GetEntityTypes().Select(entity => entity.ClrType).ToHashSet();

        Assert.Contains(typeof(AtlasNOC.Domain.Entities.Device), entityNames);
        Assert.Contains(typeof(AtlasNOC.Domain.Entities.Credential), entityNames);
        Assert.Contains(typeof(AtlasNOC.Domain.Entities.Alert), entityNames);
        Assert.Contains(typeof(AtlasNOC.Domain.Entities.Incident), entityNames);
        Assert.Contains(typeof(AtlasNOC.Domain.Entities.AuditEvent), entityNames);
        Assert.Contains(typeof(AtlasNOC.Domain.Entities.ApiKey), entityNames);
        Assert.Contains(typeof(AtlasNOC.Domain.Entities.CveRecord), entityNames);
    }
}
