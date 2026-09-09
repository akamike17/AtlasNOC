using AtlasNOC.Application.Repositories;
using AtlasNOC.Domain.Entities;
using AtlasNOC.Domain.Enums;
using AtlasNOC.Domain.ValueObjects;
using AtlasNOC.Domain.Identity;
using AtlasNOC.Application.Services;
using AtlasNOC.Application.Dtos;
using AtlasNOC.Infrastructure;
using AtlasNOC.Infrastructure.Persistence;
using AtlasNOC.Tests.Shared;
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
    public IServiceProvider Services { get; private set; } = null!;

    private string? ConnectionString { get; set; }

    private string? _skipReason;

    public async Task InitializeAsync()
    {
        ConnectionString = TestDatabaseConfiguration.TryResolve();
        if (ConnectionString is null)
        {
            _skipReason =
                $"Omitting integration tests: '{TestDatabaseConfiguration.EnvironmentVariableName}' " +
                "environment variable is not set.";
            return;
        }

        // Aísla esta suite de las demás (Runtime/E2E) con una base propia.
        ConnectionString = TestDatabaseConfiguration.WithDatabaseSuffix(ConnectionString, "_integration");

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
        services.AddIdentity<ApplicationUser, ApplicationRole>()
            .AddEntityFrameworkStores<AtlasNOCDbContext>();
        services.AddInfrastructure();
        Services = services.BuildServiceProvider();
    }

    public async Task DisposeAsync()
    {
        if (Services is not null)
        {
            var db = Services.GetRequiredService<AtlasNOCDbContext>();
            await db.Database.EnsureDeletedAsync();
        }
    }

    /// <summary>Razón de omisión (cuando no hay base de test configurada), o <c>null</c>.</summary>
    public string? SkipReason => _skipReason;

    /// <summary><c>true</c> si los tests deben omitirse por falta de base de test.</summary>
    public bool Skipped => _skipReason is not null;

    public async Task ResetAsync()
    {
        if (Services is null)
        {
            return;
        }

        var db = Services.GetRequiredService<AtlasNOCDbContext>();
        await db.Database.ExecuteSqlRawAsync(
            "DELETE FROM AspNetUserRoles; DELETE FROM AspNetUserClaims; DELETE FROM AspNetUserLogins;" +
            " DELETE FROM AspNetUserTokens; DELETE FROM AspNetRoleClaims; DELETE FROM AspNetUsers; DELETE FROM AspNetRoles;" +
            " DELETE FROM NetworkLinks; DELETE FROM DeviceInterfaces; DELETE FROM NeighborObservations;" +
            " DELETE FROM Devices; DELETE FROM DiscoveryRuns; DELETE FROM MetricSamples; DELETE FROM NotificationDeliveries;" +
            " DELETE FROM DeviceCredentials; DELETE FROM ApiKeys; DELETE FROM Alerts; DELETE FROM Incidents;" +
            " DELETE FROM AlertRules; DELETE FROM Sites; DELETE FROM Organizations;");
        db.ChangeTracker.Clear();
    }

    /// <summary>
    /// Lanza con un mensaje claro si la base de test no está disponible: los tests
    /// de Integration/Runtime/E2E dependen de <c>ATLASNOC_TEST_CONNECTION</c>.
    /// Cuando falta, el test se omite de forma explícita (no falla, no toca producción).
    /// </summary>
    public bool IsSkipped(out string reason)
    {
        reason = _skipReason ?? string.Empty;
        return _skipReason is not null;
    }
}

[Collection("integration")]
public class RepositoryIntegrationTests
{
    private readonly IntegrationFixture _fx;
    public RepositoryIntegrationTests(IntegrationFixture fx) => _fx = fx;

    private AtlasNOCDbContext Db => _fx.Services.GetRequiredService<AtlasNOCDbContext>();

    private void SkipIfNoDb()
    {
        if (_fx.IsSkipped(out var reason))
        {
            throw new SkipTestException(reason);
        }
    }

    [SkippableFact]
    public async Task Device_repository_persists_and_roundtrips_value_object_id()
    {
        SkipIfNoDb();
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

    [SkippableFact]
    public async Task Device_management_ip_is_unique()
    {
        SkipIfNoDb();
        await _fx.ResetAsync();
        var repo = _fx.Services.GetRequiredService<IDeviceRepository>();

        await repo.AddAsync(new Device("a", "10.1.1.1", DeviceType.Switch, Vendor.Cisco), default);
        await Db.SaveChangesAsync();

        // Una segunda inserción con la misma IP viola el índice único.
        await repo.AddAsync(new Device("b", "10.1.1.1", DeviceType.Switch, Vendor.Cisco), default);
        await Assert.ThrowsAsync<DbUpdateException>(() => Db.SaveChangesAsync());
    }

    [SkippableFact]
    public async Task Site_repository_persists_with_organization()
    {
        SkipIfNoDb();
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

    [SkippableFact]
    public async Task Link_repository_requires_distinct_interfaces()
    {
        SkipIfNoDb();
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

    [SkippableFact]
    public async Task Credential_stores_protected_secrets_not_plaintext()
    {
        SkipIfNoDb();
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

    [SkippableFact]
    public async Task ApiKey_hashes_are_unique_and_lookup_by_hash_works()
    {
        SkipIfNoDb();
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

[Collection("integration")]
public class SetupConcurrencyIntegrationTests
{
    private readonly IntegrationFixture _fixture;
    public SetupConcurrencyIntegrationTests(IntegrationFixture fixture) => _fixture = fixture;

    [SkippableFact]
    public async Task ConcurrentSetup_HasExactlyOneWinnerAndNoPartialState()
    {
        if (_fixture.Skipped)
            throw new SkipTestException("Omitting integration test: ATLASNOC_TEST_CONNECTION is not set.");
        await _fixture.ResetAsync();

        async Task<SetupResult> ExecuteAsync(string userName)
        {
            await using var scope = _fixture.Services.CreateAsyncScope();
            var setup = scope.ServiceProvider.GetRequiredService<ISetupService>();
            return await setup.SetupAsync(new SetupRequest(
                "Concurrent WISP", userName, userName, "Password123!", "Password123!"));
        }

        var results = await Task.WhenAll(ExecuteAsync("admin-one@example.test"), ExecuteAsync("admin-two@example.test"));

        Assert.Single(results, r => r.Success);
        await using var verificationScope = _fixture.Services.CreateAsyncScope();
        var db = verificationScope.ServiceProvider.GetRequiredService<AtlasNOCDbContext>();
        Assert.Equal(1, await db.Organizations.CountAsync());
        Assert.Equal(1, await db.Users.CountAsync());
        Assert.Equal(3, await db.Roles.CountAsync());
        Assert.Equal(1, await db.UserRoles.CountAsync());
    }

    [SkippableFact]
    public async Task UserAdministration_ProtectsLastAdminAndSupportsRoleLifecycle()
    {
        if (_fixture.Skipped)
            throw new SkipTestException("Omitting integration test: ATLASNOC_TEST_CONNECTION is not set.");
        await _fixture.ResetAsync();

        await using var scope = _fixture.Services.CreateAsyncScope();
        var setup = scope.ServiceProvider.GetRequiredService<ISetupService>();
        Assert.True((await setup.SetupAsync(new SetupRequest("WISP", "admin@example.test", "Admin",
            "Password123!", "Password123!"))).Success);
        var users = scope.ServiceProvider.GetRequiredService<IUserAdministrationService>();
        var admin = Assert.Single(await users.ListUsersAsync());

        Assert.False((await users.SetEnabledAsync(admin.Id, false)).Success);
        Assert.False((await users.ChangeRoleAsync(new ChangeUserRoleRequest(admin.Id, ApplicationRole.ReadOnly))).Success);

        Assert.True((await users.CreateAsync(new CreateUserRequest("admin2@example.test", "Admin 2",
            "Password123!", "Password123!", ApplicationRole.Administrator))).Success);
        Assert.True((await users.CreateAsync(new CreateUserRequest("operator@example.test", "Operator",
            "Password123!", "Password123!", ApplicationRole.NocOperator))).Success);
        Assert.True((await users.CreateAsync(new CreateUserRequest("reader@example.test", "Reader",
            "Password123!", "Password123!", ApplicationRole.ReadOnly))).Success);

        Assert.True((await users.ChangeRoleAsync(new ChangeUserRoleRequest(admin.Id, ApplicationRole.NocOperator))).Success);
        Assert.True((await users.SetEnabledAsync(admin.Id, false)).Success);
        var detail = await users.GetUserAsync(admin.Id);
        Assert.NotNull(detail);
        Assert.False(detail.IsActive);
        Assert.Equal(ApplicationRole.NocOperator, Assert.Single(detail.Roles));
    }
}
