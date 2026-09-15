using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using AtlasNOC.Application.Repositories;
using AtlasNOC.Application.Services;
using AtlasNOC.Domain.Entities;
using AtlasNOC.Domain.Enums;
using AtlasNOC.Infrastructure.Persistence;
using AtlasNOC.Tests.Shared;
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
    public static string BaseUrl { get; private set; } = null!;

    // Alcance LAB reducido para E2E: 4 dispositivos backbone y 3 enlaces.
    public const string LabScope = "10.0.0.1,10.0.0.2,10.0.1.1,10.0.1.2";

    private Process? _server;
    private ProcessStartInfo? _serverStartInfo;
    private Task<string>? _serverOutput;
    private Task<string>? _serverError;
    private string? _skipReason;
    public IPlaywright Playwright { get; private set; } = null!;
    public IBrowser Browser { get; private set; } = null!;

    /// <summary>Cadena de conexión de test resuelta vía <c>ATLASNOC_TEST_CONNECTION</c>.</summary>
    public string? ConnectionString { get; private set; }

    public AtlasNOCDbContext NewDb()
    {
        var options = new DbContextOptionsBuilder<AtlasNOCDbContext>()
            .UseMySql(ConnectionString, ServerVersion.Parse("8.0.36-mysql"))
            .Options;
        return new AtlasNOCDbContext(options);
    }

    public async Task InitializeAsync()
    {
        ConnectionString = TestDatabaseConfiguration.TryResolve();
        if (ConnectionString is null)
        {
            _skipReason =
                $"Omitting E2E tests: '{TestDatabaseConfiguration.EnvironmentVariableName}' " +
                "environment variable is not set.";
            return;
        }

        // Aísla esta suite de las demás (Integration/Runtime) con una base propia.
        ConnectionString = TestDatabaseConfiguration.WithDatabaseSuffix(ConnectionString, "_e2e");
        TestDatabaseConfiguration.EnsureDatabaseExists(ConnectionString);

        BaseUrl = GetFreeLoopbackUrl();

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
        startInfo.Environment["ASPNETCORE_ENVIRONMENT"] = "Testing";
        startInfo.Environment["ConnectionStrings__DefaultConnection"] = ConnectionString;
        startInfo.Environment["LabMode"] = "true";
        startInfo.Environment["RunWorkersInWebForTests"] = "true";
        startInfo.Environment["Polling__DefaultIntervalSeconds"] = "1";
        _serverStartInfo = startInfo;
        _server = Process.Start(_serverStartInfo)!;
        // Consume stdout/stderr en segundo plano para evitar el deadlock del buffer.
        _serverOutput = _server.StandardOutput.ReadToEndAsync();
        _serverError = _server.StandardError.ReadToEndAsync();
        await WaitForServerAsync();

        Playwright = await Microsoft.Playwright.Playwright.CreateAsync();
        Browser = await Playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions { Headless = true });
    }

    private static string GetFreeLoopbackUrl()
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();
        return $"http://127.0.0.1:{port}";
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

    public async Task RestartServerAsync()
    {
        if (_server is not null && !_server.HasExited)
        {
            _server.Kill(entireProcessTree: true);
            await _server.WaitForExitAsync();
        }
        _server = Process.Start(_serverStartInfo ?? throw new InvalidOperationException("E2E server was not initialized."))!;
        _serverOutput = _server.StandardOutput.ReadToEndAsync();
        _serverError = _server.StandardError.ReadToEndAsync();
        await WaitForServerAsync();
    }

    public async Task DisposeAsync()
    {
        if (Browser is not null) await Browser.DisposeAsync();
        Playwright?.Dispose();

        if (_server is not null && !_server.HasExited)
        {
            _server.Kill(entireProcessTree: true);
            await _server.WaitForExitAsync();
        }

        if (_serverOutput is not null)
            await File.WriteAllTextAsync(Path.Combine(FindRepoRoot(), ".e2e-server.stdout.log"), await _serverOutput);
        if (_serverError is not null)
            await File.WriteAllTextAsync(Path.Combine(FindRepoRoot(), ".e2e-server.stderr.log"), await _serverError);

        if (ConnectionString is not null)
        {
            await using var db = NewDb();
            await db.Database.EnsureDeletedAsync();
        }
    }

    /// <summary>Razón de omisión, o <c>null</c> si hay base de test.</summary>
    public string? SkipReason => _skipReason;

    public bool IsSkipped(out string reason)
    {
        reason = _skipReason ?? string.Empty;
        return _skipReason is not null;
    }
}

[Collection("e2e")]
public class E2EFlowsTests
{
    private readonly E2EFixture _fx;
    public E2EFlowsTests(E2EFixture fx) => _fx = fx;

    /// <summary>Flujos §19 completos, en orden (construyen estado secuencialmente).</summary>
    [SkippableFact(Timeout = 300_000)]
    public async Task Full_lifecycle_flows_1_through_18()
    {
        if (_fx.IsSkipped(out var reason))
        {
            throw new SkipTestException(reason);
        }

        var page = await _fx.NewPageAsync();
        var pageErrors = new List<string>();
        var consoleErrors = new List<string>();
        var failedRequests = new List<string>();
        var forbiddenResponses = new List<string>();
        page.PageError += (_, error) => pageErrors.Add(error);
        page.Console += (_, message) =>
        {
            if (message.Type == "error" && message.Location.StartsWith(E2EFixture.BaseUrl, StringComparison.OrdinalIgnoreCase))
                consoleErrors.Add($"{message.Location}: {message.Text}");
        };
        page.RequestFailed += (_, request) =>
        {
            if (request.Url.StartsWith(E2EFixture.BaseUrl, StringComparison.OrdinalIgnoreCase))
                failedRequests.Add($"{request.Method} {request.Url}: {request.Failure}");
        };
        page.Response += (_, response) =>
        {
            if (response.Status is 401 or 403 or 404)
                forbiddenResponses.Add($"{response.Status} {response.Url}");
        };

        // ── 1. Setup inicial ────────────────────────────────────────────────
        await page.GotoAsync(E2EFixture.BaseUrl + "/setup");
        if (page.Url.EndsWith("/setup", StringComparison.OrdinalIgnoreCase))
        {
            await page.FillAsync("input[name='WispName']", "Lab WISP");
            await page.FillAsync("input[name='AdminUserName']", "admin");
            await page.FillAsync("input[name='AdminDisplayName']", "Admin Lab");
            await page.FillAsync("input[name='Password']", "Password123!");
            await page.FillAsync("input[name='ConfirmPassword']", "Password123!");
            await page.ClickAsync("button[type='submit']");
            await page.WaitForLoadStateAsync(LoadState.NetworkIdle);
        }
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
        var cyType = await page.EvaluateAsync<string>("() => typeof window.cytoscape");
        Assert.Equal("function", cyType);
        var graph = await page.EvaluateAsync<int[]>(
            "async () => { const r = await fetch('/api/topology/graph'); const j = await r.json(); return [j.nodes.length, j.edges.length]; }");
        Assert.True(graph[0] >= 4, $"Se esperaban >=4 nodos, hubo {graph[0]}");
        Assert.True(graph[1] >= 3, $"Se esperaban >=3 enlaces, hubo {graph[1]}");
        await page.WaitForTimeoutAsync(1_000);
        var uiGraph = await page.EvaluateAsync<int[]>("() => { const cy = document.getElementById('cy')._atlasCy; if (!cy) throw new Error('Topology render failed: ' + JSON.stringify(window.__topologyDebug || window.__topologyError || 'unknown')); return [cy.nodes().length, cy.edges().length]; }");
        Assert.Equal(graph[0], uiGraph[0]);
        Assert.Equal(graph[1], uiGraph[1]);
        Assert.Contains("dispositivos", await page.Locator("#cy-count").TextContentAsync());
        await page.EvaluateAsync("() => document.getElementById('cy')._atlasCy.nodes()[0].emit('tap')");
        Assert.Contains("IP", await page.Locator("#cy-detail").TextContentAsync());
        Assert.Equal(1, await page.Locator("#cy-detail a[href^='/devices/detail/']").CountAsync());

        await page.GotoAsync(E2EFixture.BaseUrl + "/operations");
        await page.WaitForLoadStateAsync(LoadState.NetworkIdle);
        Assert.Equal(1, await page.Locator("#operation-preview").CountAsync());
        await page.Locator("#operation-device option").First.WaitForAsync(new LocatorWaitForOptions { State = WaitForSelectorState.Attached, Timeout = 5_000 });
        Assert.NotEmpty(await page.Locator("#operation-device").InputValueAsync());
        await page.ClickAsync("#operation-preview");
        var previewDeadline = DateTime.UtcNow.AddSeconds(5);
        string preview = string.Empty;
        while (string.IsNullOrWhiteSpace(preview) && DateTime.UtcNow < previewDeadline)
        {
            await page.WaitForTimeoutAsync(100);
            preview = await page.Locator("#operation-preview-result").TextContentAsync() ?? string.Empty;
        }
        Assert.NotEmpty(preview);

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

        Assert.True(pageErrors.Count == 0, string.Join(" | ", pageErrors));
        Assert.Empty(forbiddenResponses);

        await page.CloseAsync();

        // ── 10-15. Caída simulada → alerta → incidente → recuperar → resolver ──
        await Outage_alert_incident_recovery_flow();

        // ── 16-18. API key (crear/revocar) y auditoría ────────────────────
        await ApiKey_and_audit_flow();
    }

    [SkippableFact(Timeout = 120_000)]
    public async Task All_primary_views_are_navigable_for_admin()
    {
        if (_fx.IsSkipped(out var reason)) throw new SkipTestException(reason);
        var page = await _fx.NewPageAsync();
        var pageErrors = new List<string>();
        var consoleErrors = new List<string>();
        var unexpectedResponses = new List<string>();
        page.PageError += (_, error) => pageErrors.Add(error);
        page.Console += (_, message) =>
        {
            if (message.Type == "error" && message.Location.StartsWith(E2EFixture.BaseUrl, StringComparison.OrdinalIgnoreCase))
                consoleErrors.Add(message.Text);
        };
        page.Response += (_, response) =>
        {
            if (response.Status is 401 or 403 or 404 or >= 500) unexpectedResponses.Add($"{response.Status} {response.Url}");
        };
        await page.GotoAsync(E2EFixture.BaseUrl + "/setup");
        if (page.Url.EndsWith("/setup", StringComparison.OrdinalIgnoreCase))
        {
            await page.FillAsync("input[name='WispName']", "Views WISP");
            await page.FillAsync("input[name='AdminUserName']", "admin");
            await page.FillAsync("input[name='AdminDisplayName']", "Views Admin");
            await page.FillAsync("input[name='Password']", "Password123!");
            await page.FillAsync("input[name='ConfirmPassword']", "Password123!");
            await page.ClickAsync("button[type='submit']");
            await page.WaitForLoadStateAsync(LoadState.NetworkIdle);
        }
        await LoginAsync(page);
        await using (var db = _fx.NewDb())
        {
            var organization = await db.Organizations.FirstAsync();
            var site = new NetworkSite(organization.Id, "E2E Isolated " + Guid.NewGuid().ToString("N")[..8], "E2E-ISO-" + Guid.NewGuid().ToString("N")[..6], SiteType.Pop);
            var isolatedDevices = Enumerable.Range(1, 6)
                .Select(i => new Device($"E2E-Isolated-{i}", $"198.51.100.{i}", DeviceType.Router, Vendor.Generic, site.Id))
                .ToList();
            db.Sites.Add(site);
            db.Devices.AddRange(isolatedDevices);
            await db.SaveChangesAsync();
            try
            {
                await page.GotoAsync(E2EFixture.BaseUrl + "/topology");
                await page.SelectOptionAsync("#siteFilter", site.Id.Value.ToString());
                await page.WaitForTimeoutAsync(500);
                var isolatedGraph = await page.EvaluateAsync<int[]>("() => { const cy = document.getElementById('cy')._atlasCy; return [cy.nodes().length, cy.edges().length]; }");
                Assert.Equal(new[] { 6, 0 }, isolatedGraph);
            }
            finally
            {
                db.Devices.RemoveRange(isolatedDevices);
                db.Sites.Remove(site);
                await db.SaveChangesAsync();
            }
        }
        await using (var db = _fx.NewDb())
        {
            var probe = new Device("Views-Interface-Probe", "192.0.2.250", DeviceType.Router, Vendor.Generic);
            var iface = new DeviceInterface(probe.Id, 1, "ether1", "probe", "02:00:00:00:00:01", "192.0.2.250");
            db.Devices.Add(probe);
            db.DeviceInterfaces.Add(iface);
            await db.SaveChangesAsync();
            var interfaceResponse = await page.GotoAsync(E2EFixture.BaseUrl + $"/interfaces/device/{probe.Id.Value}");
            Assert.Equal(200, interfaceResponse?.Status);
            Assert.Contains("ether1", await page.ContentAsync());
            var detailResponse = await page.GotoAsync(E2EFixture.BaseUrl + $"/interfaces/{iface.Id.Value}");
            Assert.Equal(200, detailResponse?.Status);
            Assert.Contains("192.0.2.250", await page.ContentAsync());
            db.DeviceInterfaces.Remove(iface);
            db.Devices.Remove(probe);
            await db.SaveChangesAsync();
        }
        var paths = new[] { "/dashboard", "/devices", "/discovery", "/topology", "/interfaces",
            "/links", "/metrics", "/alerts", "/alertrules", "/incidents", "/sites", "/integrations",
            "/credentials", "/apikeys", "/subscribers", "/operations", "/system", "/users", "/audit" };
        foreach (var path in paths)
        {
            var response = await page.GotoAsync(E2EFixture.BaseUrl + path);
            Assert.NotNull(response);
            Assert.True(response!.Status == 200, $"Vista rota: {path} ({response.Status})");
            Assert.DoesNotContain("Error", await page.TitleAsync(), StringComparison.OrdinalIgnoreCase);
            if (path == "/dashboard")
            {
                Assert.Contains("Dashboard", await page.ContentAsync());
                Assert.Equal(1, await page.Locator("#topology-map").CountAsync());
                Assert.Equal(1, await page.Locator("#topology-map-count").CountAsync());
            }
        }
        foreach (var formPath in new[] { "/alertrules/create", "/links/create-manual", "/subscribers/create", "/users/create" })
        {
            var response = await page.GotoAsync(E2EFixture.BaseUrl + formPath);
            Assert.True(response?.Status == 200, $"Formulario roto: {formPath} ({response?.Status})");
            Assert.True(await page.Locator("form button[type='submit']").CountAsync() >= 1, $"Sin submit: {formPath}");
        }
        var suffix = Guid.NewGuid().ToString("N")[..8];
        var ruleName = "E2E Rule " + suffix;
        await page.GotoAsync(E2EFixture.BaseUrl + "/alertrules/create");
        await page.FillAsync("input[name='Name']", ruleName);
        await page.FillAsync("input[name='MetricName']", "availability");
        await page.FillAsync("input[name='Threshold']", "50");
        await page.FillAsync("input[name='ConsecutiveFaults']", "2");
        await SubmitFormAsync(page, "Guardar");
        Assert.Contains("alertrules", page.Url, StringComparison.OrdinalIgnoreCase);
        await using (var db = _fx.NewDb())
        {
            var rule = await db.AlertRules.FirstOrDefaultAsync(x => x.Name == ruleName);
            Assert.NotNull(rule);
            db.AlertRules.Remove(rule!);
            await db.SaveChangesAsync();
        }
        var subscriberName = "E2E Subscriber " + suffix;
        await page.GotoAsync(E2EFixture.BaseUrl + "/subscribers/create");
        await page.FillAsync("input[name='Name']", subscriberName);
        await SubmitFormAsync(page, "Crear");
        Assert.Contains("subscribers", page.Url, StringComparison.OrdinalIgnoreCase);
        await using (var db = _fx.NewDb())
        {
            var subscriber = await db.Subscribers.FirstOrDefaultAsync(x => x.Name == subscriberName);
            Assert.NotNull(subscriber);
            db.Subscribers.Remove(subscriber!);
            await db.SaveChangesAsync();
        }
        var linkA = new Device("Views-Link-A-" + suffix, "192.0.2.251", DeviceType.Router, Vendor.Generic);
        var linkB = new Device("Views-Link-B-" + suffix, "192.0.2.252", DeviceType.Router, Vendor.Generic);
        var linkIfaceA = new DeviceInterface(linkA.Id, 1, "ether1");
        var linkIfaceB = new DeviceInterface(linkB.Id, 1, "ether1");
        await using (var db = _fx.NewDb())
        {
            db.Devices.AddRange(linkA, linkB);
            db.DeviceInterfaces.AddRange(linkIfaceA, linkIfaceB);
            await db.SaveChangesAsync();
        }
        await page.GotoAsync(E2EFixture.BaseUrl + "/links/create-manual");
        await page.FillAsync("input[name='AInterfaceId']", linkIfaceA.Id.Value.ToString());
        await page.FillAsync("input[name='BInterfaceId']", linkIfaceB.Id.Value.ToString());
        await SubmitFormAsync(page, "Crear");
        Assert.Contains("links", page.Url, StringComparison.OrdinalIgnoreCase);
        await using (var db = _fx.NewDb())
        {
            var manual = await db.NetworkLinks.FirstOrDefaultAsync(x =>
                (x.AInterfaceId == linkIfaceA.Id && x.BInterfaceId == linkIfaceB.Id) ||
                (x.AInterfaceId == linkIfaceB.Id && x.BInterfaceId == linkIfaceA.Id));
            Assert.NotNull(manual);
            Assert.True(manual!.IsManual);
            db.NetworkLinks.Remove(manual);
            db.DeviceInterfaces.RemoveRange(linkIfaceA, linkIfaceB);
            db.Devices.RemoveRange(linkA, linkB);
            await db.SaveChangesAsync();
        }
        var userName = "e2e-" + suffix;
        await LoginAsync(page);
        await page.GotoAsync(E2EFixture.BaseUrl + "/users/create");
        await page.FillAsync("input[name='UserName']", userName);
        await page.FillAsync("input[name='DisplayName']", "E2E User");
        await page.FillAsync("input[name='Password']", "Password123!");
        await page.FillAsync("input[name='ConfirmPassword']", "Password123!");
        await page.SelectOptionAsync("select[name='Role']", "ReadOnly");
        await page.GetByRole(AriaRole.Button, new() { Name = "Crear", Exact = true }).ClickAsync();
        await page.WaitForTimeoutAsync(1_000);
        Assert.Contains("users", page.Url, StringComparison.OrdinalIgnoreCase);
        await using (var db = _fx.NewDb())
        {
            var user = await db.Users.FirstOrDefaultAsync(x => x.UserName == userName);
            Assert.NotNull(user);
            db.Users.Remove(user!);
            await db.SaveChangesAsync();
        }
        Assert.True(unexpectedResponses.Count == 0, string.Join(" | ", unexpectedResponses));
        Assert.True(pageErrors.Count == 0, $"Page errors: {string.Join(" | ", pageErrors)}");
        Assert.True(consoleErrors.Count == 0, $"Console errors: {string.Join(" | ", consoleErrors)}");
        await page.CloseAsync();
    }

    [SkippableFact(Timeout = 120_000)]
    public async Task Restart_preserves_schema_and_admin_login()
    {
        if (_fx.IsSkipped(out var reason)) throw new SkipTestException(reason);
        var page = await _fx.NewPageAsync();
        await page.GotoAsync(E2EFixture.BaseUrl + "/setup");
        if (page.Url.EndsWith("/setup", StringComparison.OrdinalIgnoreCase))
        {
            await page.FillAsync("input[name='WispName']", "Restart WISP");
            await page.FillAsync("input[name='AdminUserName']", "admin");
            await page.FillAsync("input[name='AdminDisplayName']", "Restart Admin");
            await page.FillAsync("input[name='Password']", "Password123!");
            await page.FillAsync("input[name='ConfirmPassword']", "Password123!");
            await page.ClickAsync("button[type='submit']");
            await page.WaitForLoadStateAsync(LoadState.NetworkIdle);
        }
        await _fx.RestartServerAsync();
        using var http = new HttpClient();
        var health = await http.GetAsync(E2EFixture.BaseUrl + "/health/live");
        Assert.True(health.IsSuccessStatusCode);
        await LoginAsync(page);
        Assert.Contains("/", page.Url);
        await page.CloseAsync();
    }

    [SkippableFact(Timeout = 120_000)]
    public async Task Topology_six_isolated_devices_render_six_nodes_and_zero_edges()
    {
        if (_fx.IsSkipped(out var reason)) throw new SkipTestException(reason);
        await using (var db = _fx.NewDb())
        {
            await db.Database.ExecuteSqlRawAsync(
                "DELETE FROM NeighborObservations; DELETE FROM NetworkLinks; DELETE FROM DeviceInterfaces; DELETE FROM Devices;");
            db.Devices.AddRange(Enumerable.Range(1, 6).Select(i =>
                new Device($"Isolated-{i}", $"198.51.100.{i}", DeviceType.Router, Vendor.Generic)));
            await db.SaveChangesAsync();
        }
        var page = await _fx.NewPageAsync();
        await page.GotoAsync(E2EFixture.BaseUrl + "/setup");
        if (page.Url.EndsWith("/setup", StringComparison.OrdinalIgnoreCase))
        {
            await page.FillAsync("input[name='WispName']", "Topology WISP");
            await page.FillAsync("input[name='AdminUserName']", "admin");
            await page.FillAsync("input[name='AdminDisplayName']", "Topology Admin");
            await page.FillAsync("input[name='Password']", "Password123!");
            await page.FillAsync("input[name='ConfirmPassword']", "Password123!");
            await page.ClickAsync("button[type='submit']");
            await page.WaitForLoadStateAsync(LoadState.NetworkIdle);
        }
        await LoginAsync(page);
        await page.GotoAsync(E2EFixture.BaseUrl + "/topology");
        var countLocator = page.Locator("#cy-count");
        await countLocator.WaitForAsync(new LocatorWaitForOptions { State = WaitForSelectorState.Visible });
        var deadline = DateTime.UtcNow.AddSeconds(30);
        string countText;
        do
        {
            countText = await countLocator.InnerTextAsync();
            if (countText.Contains("6 dispositivos / 0 enlaces", StringComparison.Ordinal)) break;
            await page.WaitForTimeoutAsync(100);
        } while (DateTime.UtcNow < deadline);
        Assert.Contains("6 dispositivos / 0 enlaces", countText);
        var graph = await page.EvaluateAsync<string>("() => JSON.stringify({nodes: window.document.querySelector('#cy')?._atlasCy?.nodes().length ?? -1, edges: window.document.querySelector('#cy')?._atlasCy?.edges().length ?? -1})");
        Assert.Contains("\"nodes\":6", graph);
        Assert.Contains("\"edges\":0", graph);
        await page.CloseAsync();
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
        var page = await _fx.NewPageAsync();

        // Crear regla de alerta: disponibilidad < 50% (HIGH).
        db.AlertRules.Add(new AlertRule("Disponibilidad baja", "availability", "<", 50.0, AlertSeverity.High, 1));
        await db.SaveChangesAsync();
        await LoginAsync(page);

        // ── 10. Provocar caída desde el simulador LAB; polling generará estado/métrica ──
        var controlStatus = await page.EvaluateAsync<int>("async (url) => (await fetch(url, { method: 'POST' })).status",
            $"{E2EFixture.BaseUrl}/api/lab/control/{edge.ManagementIp}/reachable/false");
        Assert.Equal(204, controlStatus);

        // ── 11. Ver alerta (espera al AlertEvaluationWorker) ──────────────
        await WaitForTextOnUrlAsync(page, E2EFixture.BaseUrl + "/alerts", "availability", timeoutMs: 30_000);

        // ── 12. Reconocer alerta ──────────────────────────────────────────
        await page.ClickAsync("button:has-text('Reconocer')");
        await page.WaitForLoadStateAsync(LoadState.NetworkIdle);

        // ── 13. Incidente correlacionado ──────────────────────────────────
        await WaitForTextOnUrlAsync(page, E2EFixture.BaseUrl + "/incidents", "Dispositivo", timeoutMs: 30_000);

        // ── 14. Recuperar conectividad desde LAB; polling generará recovery ──
        var recoveryStatus = await page.EvaluateAsync<int>("async (url) => (await fetch(url, { method: 'POST' })).status",
            $"{E2EFixture.BaseUrl}/api/lab/control/{edge.ManagementIp}/reachable/true");
        Assert.Equal(204, recoveryStatus);

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
