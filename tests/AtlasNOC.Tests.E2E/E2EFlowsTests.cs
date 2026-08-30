using System.Diagnostics;
using AtlasNOC.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Playwright;
using Xunit;

namespace AtlasNOC.Tests.E2E;

/// <summary>
/// E2E con Playwright sobre el servidor web real (proceso AtlasNOC.Web), en modo LAB
/// con base de datos de test dedicada. Verifica los flujos de la especificación §19.
/// </summary>
[CollectionDefinition("e2e")]
public class E2ECollection : ICollectionFixture<E2EFixture> { }

public class E2EFixture : IAsyncLifetime
{
    public const string ConnectionString =
        "Server=127.0.0.1;Port=3306;Database=atlasnoc_e2e_test;User=Admin;Password=RenacerGood17;";
    public const string BaseUrl = "http://127.0.0.1:5098";

    private Process? _server;
    public IPlaywright Playwright { get; private set; } = null!;
    public IBrowser Browser { get; private set; } = null!;

    public async Task InitializeAsync()
    {
        // BD de test dedicada.
        var options = new DbContextOptionsBuilder<AtlasNOCDbContext>()
            .UseMySql(ConnectionString, ServerVersion.Parse("8.0.36-mysql"))
            .Options;
        var db = new AtlasNOCDbContext(options);
        await db.Database.EnsureDeletedAsync();
        await db.Database.MigrateAsync();
        await db.DisposeAsync();

        // Arranca el servidor web real como proceso hijo.
        var webDir = Path.Combine(FindRepoRoot(), "src", "AtlasNOC.Web");
        var dll = Path.Combine(webDir, "bin", "Release", "net8.0", "AtlasNOC.Web.dll");

        var startInfo = new ProcessStartInfo
        {
            FileName = "dotnet",
            WorkingDirectory = webDir,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };
        startInfo.ArgumentList.Add(dll);
        startInfo.ArgumentList.Add("--urls");
        startInfo.ArgumentList.Add(BaseUrl);
        startInfo.Environment["ASPNETCORE_ENVIRONMENT"] = "Development";
        startInfo.Environment["ConnectionStrings__DefaultConnection"] = ConnectionString;
        startInfo.Environment["LabMode"] = "true";

        _server = Process.Start(startInfo)!;

        // Espera a que el servidor responda en /health/live.
        await WaitForServerAsync();

        Playwright = await Microsoft.Playwright.Playwright.CreateAsync();
        Browser = await Playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions { Headless = true });
    }

    private static async Task WaitForServerAsync()
    {
        using var http = new HttpClient();
        var deadline = DateTime.UtcNow.AddSeconds(30);
        while (DateTime.UtcNow < deadline)
        {
            try
            {
                var resp = await http.GetAsync(BaseUrl + "/health/live");
                if (resp.IsSuccessStatusCode) return;
            }
            catch { /* aún no listo */ }
            await Task.Delay(500);
        }
        throw new TimeoutException("El servidor web no estuvo listo a tiempo.");
    }

    private static string FindRepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "AtlasNOC.sln")))
            dir = dir.Parent;
        return dir?.FullName ?? throw new InvalidOperationException("No se encontró la raíz del repositorio.");
    }

    public async Task<IPage> NewPageAsync() => await Browser.NewPageAsync();

    public async Task DisposeAsync()
    {
        if (Browser is not null) await Browser.DisposeAsync();
        Playwright?.Dispose();

        if (_server is not null && !_server.HasExited)
        {
            _server.Kill(entireProcessTree: true);
            await _server.WaitForExitAsync();
        }

        var options = new DbContextOptionsBuilder<AtlasNOCDbContext>()
            .UseMySql(ConnectionString, ServerVersion.Parse("8.0.36-mysql"))
            .Options;
        var db = new AtlasNOCDbContext(options);
        await db.Database.EnsureDeletedAsync();
    }
}

[Collection("e2e")]
public class E2EFlowsTests
{
    private readonly E2EFixture _fx;
    public E2EFlowsTests(E2EFixture fx) => _fx = fx;

    [Fact]
    public async Task Setup_login_and_dashboard_flow()
    {
        var page = await _fx.NewPageAsync();

        // 1. Primer arranque: /setup disponible.
        await page.GotoAsync(E2EFixture.BaseUrl + "/setup");

        // 2. Completar setup (WISP + admin).
        await page.FillAsync("input[name='WispName']", "Lab WISP");
        await page.FillAsync("input[name='AdminUserName']", "admin");
        await page.FillAsync("input[name='AdminDisplayName']", "Admin Lab");
        await page.FillAsync("input[name='Password']", "Password123!");
        await page.FillAsync("input[name='ConfirmPassword']", "Password123!");
        await page.ClickAsync("button[type='submit']");
        await page.WaitForLoadStateAsync(LoadState.NetworkIdle);

        // 3. Tras el setup, redirige a login.
        Assert.Contains("login", page.Url.ToLowerInvariant());

        // 4. Login.
        await page.FillAsync("input[name='userName']", "admin");
        await page.FillAsync("input[name='password']", "Password123!");
        await page.ClickAsync("button[type='submit']");
        await page.WaitForLoadStateAsync(LoadState.NetworkIdle);

        // 5. Dashboard: la ruta por defecto (/) es el Dashboard. Verificamos que estemos
        // autenticados comprobando contenido (nav de navegación NOC) en vez de la URL.
        var content = await page.ContentAsync();
        Assert.Contains("Topología", content);
        Assert.Contains("Dashboard", content, StringComparison.OrdinalIgnoreCase);

        await page.CloseAsync();
    }

    [Fact]
    public async Task Topology_page_requires_auth()
    {
        // Sin sesión, /topology debe redirigir a login; si aún no hay admin, a /setup.
        var page = await _fx.NewPageAsync();
        await page.GotoAsync(E2EFixture.BaseUrl + "/topology");

        var url = page.Url.ToLowerInvariant();
        Assert.True(url.Contains("login") || url.Contains("setup"),
            $"Se esperaba redirección a login/setup, pero la URL fue: {url}");

        await page.CloseAsync();
    }
}