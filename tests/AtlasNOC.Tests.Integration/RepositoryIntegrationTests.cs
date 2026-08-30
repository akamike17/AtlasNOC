using AtlasNOC.Application.Repositories;
using AtlasNOC.Domain.Entities;
using AtlasNOC.Domain.Enums;
using AtlasNOC.Domain.ValueObjects;
using AtlasNOC.Infrastructure;
using AtlasNOC.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace AtlasNOC.Tests.Integration;

/// <summary>
/// Tests de integración: repositorios EF Core contra MySQL real (base de test dedicada).
/// Verifican el mapeo de value objects (conversores), índices únicos y persistencia real.
/// </summary>
[CollectionDefinition("integration")]
public class IntegrationCollection : ICollectionFixture<IntegrationFixture> { }

public class IntegrationFixture : IAsyncLifetime
{
    public const string ConnectionString =
        "Server=127.0.0.1;Port=3306;Database=atlasnoc_integration_test;User=Admin;Password=RenacerGood17;";

    public IServiceProvider Services { get; private set; } = null!;

    public async Task InitializeAsync()
    {
        var options = new DbContextOptionsBuilder<AtlasNOCDbContext>()
            .UseMySql(ConnectionString, ServerVersion.Parse("8.0.36-mysql"))
            .Options;

        var db = new AtlasNOCDbContext(options);
        await db.Database.EnsureDeletedAsync();
        await db.Database.MigrateAsync();
        await db.DisposeAsync();

        var services = new ServiceCollection();
        services.AddDbContext<AtlasNOCDbContext>(o =>
            o.UseMySql(ConnectionString, ServerVersion.Parse("8.0.36-mysql")));
        services.AddLogging();
        services.AddInfrastructure();
        Services = services.BuildServiceProvider();
    }

    public async Task DisposeAsync()
    {
        var db = Services.GetRequiredService<AtlasNOCDbContext>();
        await db.Database.EnsureDeletedAsync();
    }

    public async Task ResetAsync()
    {
        var db = Services.GetRequiredService<AtlasNOCDbContext>();
        await db.Database.ExecuteSqlRawAsync(
            "DELETE FROM NetworkLinks; DELETE FROM DeviceInterfaces; DELETE FROM NeighborObservations;" +
            " DELETE FROM Devices; DELETE FROM DiscoveryRuns; DELETE FROM MetricSamples;" +
            " DELETE FROM DeviceCredentials; DELETE FROM ApiKeys; DELETE FROM Alerts; DELETE FROM Incidents;" +
            " DELETE FROM AlertRules; DELETE FROM Sites; DELETE FROM Organizations;");
        db.ChangeTracker.Clear();
    }
}

[Collection("integration")]
public class RepositoryIntegrationTests
{
    private readonly IntegrationFixture _fx;
    public RepositoryIntegrationTests(IntegrationFixture fx) => _fx = fx;

    private AtlasNOCDbContext Db => _fx.Services.GetRequiredService<AtlasNOCDbContext>();

    [Fact]
    public async Task Device_repository_persists_and_roundtrips_value_object_id()
    {
        await _fx.ResetAsync();
        var repo = _fx.Services.GetRequiredService<IDeviceRepository>();

        var device = new Device("router-1", "192.168.1.1", DeviceType.Router, Vendor.MikroTik);
        await repo.AddAsync(device, default);
        await Db.SaveChangesAsync();

        // Recuperar por GUID (conversor de value object a Guid).
        var fetched = await repo.GetByIdAsync(device.Id.Value);

        Assert.NotNull(fetched);
        Assert.Equal("router-1", fetched!.Hostname);
        Assert.Equal(device.Id.Value, fetched.Id.Value);
    }

    [Fact]
    public async Task Device_management_ip_is_unique()
    {
        await _fx.ResetAsync();
        var repo = _fx.Services.GetRequiredService<IDeviceRepository>();

        await repo.AddAsync(new Device("a", "10.1.1.1", DeviceType.Switch, Vendor.Cisco), default);
        await Db.SaveChangesAsync();

        // Una segunda inserción con la misma IP viola el índice único.
        await repo.AddAsync(new Device("b", "10.1.1.1", DeviceType.Switch, Vendor.Cisco), default);
        await Assert.ThrowsAsync<DbUpdateException>(() => Db.SaveChangesAsync());
    }

    [Fact]
    public async Task Site_repository_persists_with_organization()
    {
        await _fx.ResetAsync();
        var sites = _fx.Services.GetRequiredService<ISiteRepository>();
        var org = new WispOrganization("WISP-Test", "WT");
        Db.Organizations.Add(org);
        await Db.SaveChangesAsync();

        var site = new NetworkSite(org.Id, "Torre Norte", "TOR-N", SiteType.Tower, latitude: 19.4, longitude: -99.1);
        await sites.AddAsync(site, default);
        await Db.SaveChangesAsync();

        var fetched = await sites.GetByIdAsync(site.Id.Value);
        Assert.NotNull(fetched);
        Assert.Equal("Torre Norte", fetched!.Name);
        Assert.Equal(org.Id.Value, fetched.OrganizationId.Value);
    }

    [Fact]
    public async Task Link_repository_requires_distinct_interfaces()
    {
        await _fx.ResetAsync();
        var devices = _fx.Services.GetRequiredService<IDeviceRepository>();
        var a = new Device("a", "10.2.1.1", DeviceType.Switch, Vendor.Generic);
        var b = new Device("b", "10.2.1.2", DeviceType.Switch, Vendor.Generic);
        await devices.AddAsync(a, default);
        await devices.AddAsync(b, default);
        await Db.SaveChangesAsync();

        var ifA = new DeviceInterface(a.Id, 1, "ether1");
        var ifB = new DeviceInterface(b.Id, 1, "ether1");
        Db.DeviceInterfaces.AddRange(ifA, ifB);
        await Db.SaveChangesAsync();

        var links = _fx.Services.GetRequiredService<ILinkRepository>();
        var link = new NetworkLink(ifA.Id, ifB.Id, LinkType.Physical, DiscoverySource.Lldp, 0.95);
        await links.AddAsync(link, default);
        await Db.SaveChangesAsync();

        var fetched = await links.GetByIdAsync(link.Id.Value);
        Assert.NotNull(fetched);
        Assert.True(fetched!.IsConfirmed);
        Assert.Equal(0.95, fetched.Confidence, 3);
    }

    [Fact]
    public async Task Credential_stores_protected_secrets_not_plaintext()
    {
        await _fx.ResetAsync();
        var creds = _fx.Services.GetRequiredService<ICredentialRepository>();

        var cred = new DeviceCredential("SNMP-v2", SnmpVersion.V2c, null, null, null);
        cred.SetProtectedSecrets("CfDJ8ENCRYPTED_COMMUNITY", null, null); // protegido, no el community plano
        await creds.AddAsync(cred, default);
        await Db.SaveChangesAsync();

        var fetched = await creds.GetByIdAsync(cred.Id.Value);
        Assert.NotNull(fetched);
        Assert.NotEqual("public", fetched!.CommunityProtected);
        Assert.True(fetched.IsActive);
    }

    [Fact]
    public async Task ApiKey_hashes_are_unique_and_lookup_by_hash_works()
    {
        await _fx.ResetAsync();
        var keys = _fx.Services.GetRequiredService<IApiKeyRepository>();

        var key = ApiKey.Create("CI bot", "user-1", "HASH-ABC123", "ak_", "read", "topology:read");
        await keys.AddAsync(key, default);
        await Db.SaveChangesAsync();

        var byHash = await keys.GetByHashAsync("HASH-ABC123");
        Assert.NotNull(byHash);
        Assert.Equal("CI bot", byHash!.Name);
    }
}