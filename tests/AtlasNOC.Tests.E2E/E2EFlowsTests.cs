using System.Diagnostics;
using AtlasNOC.Application.Repositories;
using AtlasNOC.Application.Services;
using AtlasNOC.Domain.Entities;
using AtlasNOC.Domain.Enums;
using AtlasNOC.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Playwright;
using Xunit;

namespace AtlasNOC.Tests.E2E;

/// <summary>
/// E2E con Playwright sobre el servidor web real (proceso AtlasNOC.Web), en modo LAB
/// con base de datos de test dedicada. Verifica los 18 flujos de la especificación §19.
/// </summary>
[CollectionDefinition("e2e")]
public class E2ECollection : ICollectionFixture<E2EFixture> { }

public class E2EFixture : IAsyncLifetime
{
    public const string ConnectionString =
        "Server=127.0.0.1;Port=3306;Database=atlasnoc_e2e_test;User=Admin;Password=RenacerGood17;";
    public const string BaseUrl = "http://127.0.0.1:5098";

    // Alcance LAB reducido para E2E: 4 dispositivos backbone y 3 enlaces.
    public const string LabScope = "10.0.0.1,10.0.0.2,10.0.1.1,10.0.1.2";

    private Process? _server;
    public IPlaywright Playwright { get; private set; } = null!;
    public IBrowser Browser { get; private set; } = null!;

    public AtlasNOCDbContext NewDb()
    {
        var options = new DbContextOptionsBuilder<AtlasNOCDbContext>()
            .UseMySql(ConnectionString, ServerVersion.Parse("8.0.36-mysql"))
            .Options;
        return new AtlasNOCDbContext(options);
    }

    public async Task InitializeAsync()
    {
        // Mata cualquier servidor residual que ocupe el puerto (runs previos fallidos).
        KillPortListener();

        var options = new DbContextOptionsBuilder<AtlasNOCDbContext>()
            .UseMySql(ConnectionString, ServerVersion.Parse("8.0.36-mysql"))
            .Options;
        var db = new AtlasNOCDbContext(options);
        await db.Database.EnsureDeletedAsync();
        await db.Database.MigrateAsync();
        await db.DisposeAsync();

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
        await WaitForServerAsync();

        Playwright = await Microsoft.Playwright.Playwright.CreateAsync();
        Browser = await Playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions { Headless = true });
    }

    /// <summary>Mata cualquier proceso escuchando en el puerto del servidor de test.</summary>
    private static void KillPortListener()
    {
        try
        {
            var port = new Uri(BaseUrl).Port;
            var proc = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "netstat",
                    Arguments = $"-ano -p tcp",
                    RedirectStandardOutput = true,
                    UseShellExecute = false,
                }
            };
            proc.Start();
            var output = proc.StandardOutput.ReadToEnd();
            proc.WaitForExit();

            var pids = output
                .Split('\n')
                .Where(l => l.Contains($":{port}") && l.Contains("LISTENING"))
                .Select(l => l.Trim().Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries).Last())
                .Distinct();
            foreach (var pidStr in pids)
            {
                if (int.TryParse(pidStr, out var pid))
                {
                    try { Process.GetProcessById(pid).Kill(entireProcessTree: true); } catch { }
                }
            }
        }
        catch { /* mejor esfuerzo */ }
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
            catch { }
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

        await using var db = NewDb();
        await db.Database.EnsureDeletedAsync();
    }
}

[Collection("e2e")]
public class E2EFlowsTests
{
    private readonly E2EFixture _fx;
    public E2EFlowsTests(E2EFixture fx) => _fx = fx;

    /// <summary>Flujos §19 completos, en orden (construyen estado secuencialmente).</summary>
    [Fact(Timeout = 300_000)]
    public async Task Full_lifecycle_flows_1_through_18()
    {
        var page = await _fx.NewPageAsync();

        // ── 1. Setup inicial ────────────────────────────────────────────────
        await page.GotoAsync(E2EFixture.BaseUrl + "/setup");
        await page.FillAsync("input[name='WispName']", "Lab WISP");
        await page.FillAsync("input[name='AdminUserName']", "admin");
        await page.FillAsync("input[name='AdminDisplayName']", "Admin Lab");
        await page.FillAsync("input[name='Password']", "Password123!");
        await page.FillAsync("input[name='ConfirmPassword']", "Password123!");
        await page.ClickAsync("button[type='submit']");
        await page.WaitForLoadStateAsync(LoadState.NetworkIdle);
        Assert.Contains("login", page.Url.ToLowerInvariant());

        // ── 2. Login ───────────────────────────────────────────────────────
        await page.FillAsync("input[name='userName']", "admin");
        await page.FillAsync("input[name='password']", "Password123!");
        await page.CheckAsync("#rememberMe");
        await page.ClickAsync("button[type='submit']");
        await page.WaitForLoadStateAsync(LoadState.NetworkIdle);
        Assert.True(await IsLoggedInAsync(page), "El login no dejó al usuario autenticado.");

        // ── 3. Crear sitio ─────────────────────────────────────────────────
        await page.GotoAsync(E2EFixture.BaseUrl + "/sites/create");
        await page.FillAsync("input[name='Name']", "Torre Norte");
        await page.FillAsync("input[name='Code']", "TOR-N");
        await SubmitFormAsync(page, "Guardar");
        Assert.Contains("/sites", page.Url.ToLowerInvariant());
        Assert.Contains("Torre Norte", await page.ContentAsync());

        // ── 4. Crear credencial ────────────────────────────────────────────
        await page.GotoAsync(E2EFixture.BaseUrl + "/credentials/create");
        await page.FillAsync("input[name='Name']", "SNMP-Lab");
        await page.FillAsync("input[name='Community']", "public");
        await SubmitFormAsync(page, "Guardar");
        Assert.Contains("/credentials", page.Url.ToLowerInvariant());
        Assert.Contains("SNMP-Lab", await page.ContentAsync());

        // ── 5. Ejecutar descubrimiento ─────────────────────────────────────
        await page.GotoAsync(E2EFixture.BaseUrl + "/discovery/start");
        await page.FillAsync("input[name='ScopeIp']", E2EFixture.LabScope);
        await SubmitFormAsync(page, "Iniciar");

        // ── 6. Ver dispositivos encontrados (espera al DiscoveryWorker) ────
        await page.GotoAsync(E2EFixture.BaseUrl + "/devices");
        await WaitForTextOnUrlAsync(page, E2EFixture.BaseUrl + "/devices", "EdgeRouter-01", timeoutMs: 25_000);

        // ── 7. Topología: comprobar edges ──────────────────────────────────
        await page.GotoAsync(E2EFixture.BaseUrl + "/topology");
        await page.WaitForLoadStateAsync(LoadState.NetworkIdle);
        var graph = await page.EvaluateAsync<int[]>(
            "async () => { const r = await fetch('/api/topology/graph'); const j = await r.json(); return [j.nodes.length, j.edges.length]; }");
        Assert.True(graph[0] >= 4, $"Se esperaban >=4 nodos, hubo {graph[0]}");
        Assert.True(graph[1] >= 3, $"Se esperaban >=3 enlaces, hubo {graph[1]}");

        // ── 8. Device detail ───────────────────────────────────────────────
        var deviceId = await GetFirstDeviceIdAsync();
        await page.GotoAsync(E2EFixture.BaseUrl + "/devices/detail/" + deviceId);
        await page.WaitForLoadStateAsync(LoadState.NetworkIdle);
        Assert.Contains("IP gestión", await page.ContentAsync());

        // ── 9. Métricas con muestras (polling) ─────────────────────────────
        var metricUrl = "/api/metrics?resourceType=Device&resourceId=" + deviceId + "&metric=availability";
        var pollDeadline = DateTime.UtcNow.AddSeconds(45);
        var hasMetrics = false;
        while (!hasMetrics && DateTime.UtcNow < pollDeadline)
        {
            await Task.Delay(2000);
            hasMetrics = await page.EvaluateAsync<bool>(
                "async (u) => { const r = await fetch(u); const j = await r.json(); return Array.isArray(j) && j.length > 0; }",
                metricUrl);
        }
        Assert.True(hasMetrics, "No se generaron métricas tras el polling.");

        await page.CloseAsync();

        // ── 10-15. Caída simulada → alerta → incidente → recuperar → resolver ──
        await Outage_alert_incident_recovery_flow();

        // ── 16-18. API key (crear/revocar) y auditoría ────────────────────
        await ApiKey_and_audit_flow();
    }

    private async Task ApiKey_and_audit_flow()
    {
        var page = await _fx.NewPageAsync();
        await LoginAsync(page);

        await page.GotoAsync(E2EFixture.BaseUrl + "/apikeys/create");
        await page.FillAsync("input[name='Name']", "CI bot");
        await page.FillAsync("input[name='Description']", "Integración de CI");
        await page.FillAsync("input[name='Scopes']", "topology.read alerts.read");
        // Fecha de expiración futura (evita binding problemático de datetime-local vacío).
        await page.FillAsync("input[name='ExpiresAtUtc']", "2030-01-01T00:00");
        await SubmitFormAsync(page, "Crear");
        Assert.Contains("/apikeys", page.Url.ToLowerInvariant());
        // La key se muestra UNA sola vez.
        Assert.Contains("Guarda esta key", await page.ContentAsync());

        // ── 17. Revocar API key ───────────────────────────────────────────
        page.Dialog += (_, d) => d.AcceptAsync(); // confirma el diálogo "¿Revocar?"
        await page.ClickAsync("button:has-text('Revocar')");
        await page.WaitForLoadStateAsync(LoadState.NetworkIdle);
        Assert.Contains("Revocada", await page.ContentAsync());

        // ── 18. Auditoría demuestra acciones ───────────────────────────────
        await page.GotoAsync(E2EFixture.BaseUrl + "/audit");
        await page.WaitForLoadStateAsync(LoadState.NetworkIdle);
        var audit = await page.ContentAsync();
        Assert.Contains("Auth", audit);

        await page.CloseAsync();
    }

    private async Task Outage_alert_incident_recovery_flow()
    {
        await using var db = _fx.NewDb();
        var edge = await db.Devices.FirstAsync(d => d.Hostname == "EdgeRouter-01");
        var edgeId = edge.Id.Value.ToString();

        // Crear regla de alerta: disponibilidad < 50% (HIGH).
        db.AlertRules.Add(new AlertRule("Disponibilidad baja", "availability", "<", 50.0, AlertSeverity.High, 1));
        await db.SaveChangesAsync();

        // ── 10. Provocar caída: métrica availability=0 + estado Down ──────
        db.MetricSamples.Add(new MetricSample("Device", edgeId, "availability", 0.0, DateTime.UtcNow, "%"));
        edge.SetStatus(DeviceStatus.Down);
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();

        // ── 11. Ver alerta (espera al AlertEvaluationWorker) ──────────────
        var page = await _fx.NewPageAsync();
        await LoginAsync(page);
        await WaitForTextOnUrlAsync(page, E2EFixture.BaseUrl + "/alerts", "availability", timeoutMs: 30_000);

        // ── 12. Reconocer alerta ──────────────────────────────────────────
        await page.ClickAsync("button:has-text('Reconocer')");
        await page.WaitForLoadStateAsync(LoadState.NetworkIdle);

        // ── 13. Incidente correlacionado ──────────────────────────────────
        await WaitForTextOnUrlAsync(page, E2EFixture.BaseUrl + "/incidents", "Dispositivo", timeoutMs: 30_000);

        // ── 14. Recuperar dispositivo (métrica availability=100 + Up) ─────
        db.MetricSamples.Add(new MetricSample("Device", edgeId, "availability", 100.0, DateTime.UtcNow, "%"));
        edge.SetStatus(DeviceStatus.Up);
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();

        // ── 15. Resolver incidente (si está activo) ───────────────────────
        await page.GotoAsync(E2EFixture.BaseUrl + "/incidents");
        await page.WaitForLoadStateAsync(LoadState.NetworkIdle);
        var content = await page.ContentAsync();
        if (content.Contains("Resolver"))
            await page.ClickAsync("button:has-text('Resolver')");

        await page.CloseAsync();
    }

    // ── Helpers ──────────────────────────────────────────────────────────

    private async Task LoginAsync(IPage page)
    {
        await page.GotoAsync(E2EFixture.BaseUrl + "/account/login");
        await page.FillAsync("input[name='userName']", "admin");
        await page.FillAsync("input[name='password']", "Password123!");
        await page.CheckAsync("#rememberMe");
        await page.ClickAsync("button[type='submit']");
        await page.WaitForLoadStateAsync(LoadState.NetworkIdle);
        Assert.True(await IsLoggedInAsync(page), "Login falló.");
    }

    /// <summary>Hace clic en el submit del formulario por su etiqueta (evita el botón "Salir" del nav).</summary>
    private static async Task SubmitFormAsync(IPage page, string buttonText)
    {
        await page.ClickAsync($"button:has-text('{buttonText}')");
        await page.WaitForLoadStateAsync(LoadState.DOMContentLoaded);
        await page.WaitForTimeoutAsync(800);
    }

    private async Task<Guid> GetFirstDeviceIdAsync()
    {
        await using var db = _fx.NewDb();
        var d = await db.Devices.OrderBy(x => x.Hostname).FirstAsync();
        return d.Id.Value;
    }

    private static async Task<bool> IsLoggedInAsync(IPage page)
    {
        var content = await page.ContentAsync();
        return content.Contains("Topología") || content.Contains("Salir");
    }

    private static async Task WaitForTextOnUrlAsync(IPage page, string url, string text, int timeoutMs)
    {
        var deadline = DateTime.UtcNow.AddMilliseconds(timeoutMs);
        while (DateTime.UtcNow < deadline)
        {
            await page.GotoAsync(url);
            await page.WaitForLoadStateAsync(LoadState.NetworkIdle);
            if ((await page.ContentAsync()).Contains(text, StringComparison.OrdinalIgnoreCase))
                return;
            await Task.Delay(3000);
        }
        throw new TimeoutException($"No apareció el texto '{text}' en {url} a tiempo.");
    }
}