using System.Net;
using AtlasNOC.Domain.Entities;
using AtlasNOC.Infrastructure.Persistence;
using AtlasNOC.Infrastructure.Services;
using AtlasNOC.Tests.Shared;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace AtlasNOC.Tests.E2E;

/// <summary>
/// Verifica la autenticación real por API key (especificación §2) contra el host
/// in-process (WebApplicationFactory) y una base de test dedicada. Cubre:
///   sin key -> 401; key válida con scope -> 200; key sin scope -> 403;
///   revocada -> 401; expirada -> 401; inexistente -> 401;
///   API key no accede a vistas MVC protegidas.
/// </summary>
public class ApiKeyAuthenticationTests : IClassFixture<ApiKeyAuthFactory>, IAsyncLifetime
{
    private readonly ApiKeyAuthFactory _factory;
    public ApiKeyAuthenticationTests(ApiKeyAuthFactory factory) => _factory = factory;

    public async Task InitializeAsync()
    {
        if (_factory.IsSkipped(out var reason)) throw new SkipTestException(reason);
        await _factory.SeedAsync();
    }

    public Task DisposeAsync() => _factory.ClearAsync();

    private void SkipIfNoDb()
    {
        if (_factory.IsSkipped(out var reason)) throw new SkipTestException(reason);
    }

    private HttpRequestMessage TopologyRequest(string? headerValue = null)
    {
        var req = new HttpRequestMessage(HttpMethod.Get, "/api/topology/graph");
        if (headerValue is not null)
            req.Headers.TryAddWithoutValidation("X-Api-Key", headerValue);
        return req;
    }

    [SkippableFact]
    public async Task Missing_key_returns_401()
    {
        var resp = await _factory.CreateClient().SendAsync(TopologyRequest());
        Assert.Equal(HttpStatusCode.Unauthorized, resp.StatusCode);
    }

    [SkippableFact]
    public async Task Valid_key_with_correct_scope_returns_200()
    {
        var resp = await _factory.CreateClient().SendAsync(TopologyRequest(_factory.TopologyKey));
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
    }

    [SkippableFact]
    public async Task Valid_key_without_required_scope_returns_403()
    {
        var resp = await _factory.CreateClient().SendAsync(TopologyRequest(_factory.MetricsOnlyKey));
        Assert.Equal(HttpStatusCode.Forbidden, resp.StatusCode);
    }

    [SkippableFact]
    public async Task Revoked_key_returns_401()
    {
        var resp = await _factory.CreateClient().SendAsync(TopologyRequest(_factory.RevokedKey));
        Assert.Equal(HttpStatusCode.Unauthorized, resp.StatusCode);
    }

    [SkippableFact]
    public async Task Expired_key_returns_401()
    {
        var resp = await _factory.CreateClient().SendAsync(TopologyRequest(_factory.ExpiredKey));
        Assert.Equal(HttpStatusCode.Unauthorized, resp.StatusCode);
    }

    [SkippableFact]
    public async Task Non_existent_key_returns_401()
    {
        var resp = await _factory.CreateClient().SendAsync(TopologyRequest("atn_does_not_exist_1234567890"));
        Assert.Equal(HttpStatusCode.Unauthorized, resp.StatusCode);
    }

    [SkippableFact]
    public async Task Api_key_cannot_access_protected_mvc_view()
    {
        // Desactiva el seguimiento automático de redirecciones para observar la
        // respuesta de autorización real (challenge), no la página de login final.
        var client = _factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
        });

        var req = new HttpRequestMessage(HttpMethod.Get, "/devices");
        req.Headers.TryAddWithoutValidation("X-Api-Key", _factory.TopologyKey);

        var resp = await client.SendAsync(req);

        // Una API key no es un login humano: el endpoint MVC exige cookie/roles;
        // sin cookie, responde 401 o redirige a login (302), nunca 200.
        Assert.NotEqual(HttpStatusCode.OK, resp.StatusCode);
        if (resp.StatusCode == HttpStatusCode.Redirect)
        {
            Assert.Contains("login", resp.Headers.Location?.ToString() ?? "", StringComparison.OrdinalIgnoreCase);
        }
    }
}

/// <summary>
/// Host in-process con base de test dedicada (LabMode) que siembra API keys
/// con estados concretos y expone sus claves en claro para las aserciones.
/// </summary>
public class ApiKeyAuthFactory : WebApplicationFactory<Program>
{
    public string TopologyKey { get; private set; } = null!;
    public string MetricsOnlyKey { get; private set; } = null!;
    public string RevokedKey { get; private set; } = null!;
    public string ExpiredKey { get; private set; } = null!;

    private string _connectionString = null!;
    private string? _skipReason;

    public ApiKeyAuthFactory()
    {
        var resolved = TestDatabaseConfiguration.TryResolve();
        if (resolved is null)
        {
            _skipReason =
                $"Omitting E2E API-key tests: '{TestDatabaseConfiguration.EnvironmentVariableName}' " +
                "environment variable is not set.";
            return;
        }

        _connectionString = TestDatabaseConfiguration.WithDatabaseSuffix(resolved, "_apikey");
    }

    /// <summary><c>true</c> si el fixture debe omitirse por falta de base de test.</summary>
    public bool IsSkipped(out string reason)
    {
        reason = _skipReason ?? string.Empty;
        return _skipReason is not null;
    }

    private DbContextOptions<AtlasNOCDbContext> Options()
        => new DbContextOptionsBuilder<AtlasNOCDbContext>()
            .UseMySql(_connectionString, ServerVersion.Parse("8.0.36-mysql"))
            .Options;

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        if (IsSkipped(out _)) return;

        builder.UseSetting("LabMode", "true");
        builder.UseEnvironment("Development");
        builder.ConfigureServices(services =>
        {
            // Reemplaza la cadena de conexión por la base de test validada.
            var descriptor = services.Single(d =>
                d.ServiceType == typeof(DbContextOptions<AtlasNOCDbContext>));
            services.Remove(descriptor);
            services.AddDbContext<AtlasNOCDbContext>(o =>
                o.UseMySql(_connectionString, ServerVersion.Parse("8.0.36-mysql")));
        });
    }

    /// <summary>Asegura esquema y siembra keys con estados concretos.</summary>
    public async Task SeedAsync()
    {
        // Fuerza el arranque del host (y su Migrate() en Program.cs).
        _ = CreateClient();

        await using var db = new AtlasNOCDbContext(Options());
        await db.Database.MigrateAsync();

        TopologyKey = "atn_" + Guid.NewGuid().ToString("N").ToLowerInvariant();
        MetricsOnlyKey = "atn_" + Guid.NewGuid().ToString("N").ToLowerInvariant();
        RevokedKey = "atn_" + Guid.NewGuid().ToString("N").ToLowerInvariant();
        ExpiredKey = "atn_" + Guid.NewGuid().ToString("N").ToLowerInvariant();

        db.ApiKeys.RemoveRange(await db.ApiKeys.ToListAsync());

        db.ApiKeys.Add(ApiKey.Create("topology", "user-1",
            ApiKeyService.HashKey(TopologyKey), "atn_", "", "topology.read"));
        db.ApiKeys.Add(ApiKey.Create("metrics", "user-1",
            ApiKeyService.HashKey(MetricsOnlyKey), "atn_", "", "metrics.read"));

        var revoked = ApiKey.Create("revoked", "user-1",
            ApiKeyService.HashKey(RevokedKey), "atn_", "", "topology.read");
        revoked.Revoke();
        db.ApiKeys.Add(revoked);

        db.ApiKeys.Add(ApiKey.Create("expired", "user-1",
            ApiKeyService.HashKey(ExpiredKey), "atn_", "",
            "topology.read", DateTime.UtcNow.AddDays(-1)));

        await db.SaveChangesAsync();
    }

    public async Task ClearAsync()
    {
        if (IsSkipped(out _)) return;
        await using var db = new AtlasNOCDbContext(Options());
        await db.Database.EnsureDeletedAsync();
    }
}