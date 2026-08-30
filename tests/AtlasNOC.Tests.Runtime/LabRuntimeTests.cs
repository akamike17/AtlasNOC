using AtlasNOC.Application.Services;
using AtlasNOC.Domain.Entities;
using AtlasNOC.Domain.Enums;
using AtlasNOC.Infrastructure;
using AtlasNOC.Infrastructure.Devices;
using AtlasNOC.Infrastructure.Persistence;
using AtlasNOC.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace AtlasNOC.Tests.Runtime;

/// <summary>
/// Laboratorio runtime real: MySQL + EF + servicios + drivers/probes simulados (modo LAB).
/// Ejecuta el pipeline de descubrimiento contra la topología LAB-01 y verifica las
/// garantías de la especificación §18/§21 (no declarar "producción" por compilar).
/// </summary>
[CollectionDefinition("lab-runtime")]
public class LabRuntimeCollection : ICollectionFixture<LabRuntimeFixture> { }

public class LabRuntimeFixture : IAsyncLifetime
{
    public const string ConnectionString =
        "Server=127.0.0.1;Port=3306;Database=atlasnoc_test;User=Admin;Password=RenacerGood17;";

    public IServiceProvider Services { get; private set; } = null!;
    public AtlasNOCDbContext Db { get; private set; } = null!;

    public async Task InitializeAsync()
    {
        // Base de datos de test dedicada, limpia en cada ejecución.
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
        services.AddInfrastructure(labMode: true);

        Services = services.BuildServiceProvider();
        Db = Services.GetRequiredService<AtlasNOCDbContext>();
    }

    public Task DisposeAsync()
    {
        if (Db is not null)
            return Db.Database.EnsureDeletedAsync();
        return Task.CompletedTask;
    }

    /// <summary>Reinicia el estado de inventario/topología (sin recrear la BD) para un test limpio.</summary>
    public async Task ResetAsync()
    {
        // Orden por dependencias: enlaces → interfaces → observaciones → dispositivos → runs.
        await Db.Database.ExecuteSqlRawAsync(
            "DELETE FROM NetworkLinks; DELETE FROM DeviceInterfaces; DELETE FROM NeighborObservations;" +
            " DELETE FROM Devices; DELETE FROM DiscoveryRuns; DELETE FROM MetricSamples;" +
            " DELETE FROM Alerts; DELETE FROM Incidents; DELETE FROM AlertRules;");
    }

    /// <summary>Dispara el descubrimiento de toda la LAB-01 por lista de IPs y devuelve el resumen.</summary>
    public async Task<DiscoveryRun> RunDiscoveryAsync()
    {
        var scope = Services.CreateScope();
        var discoveryService = scope.ServiceProvider.GetRequiredService<IDiscoveryService>();
        var executor = scope.ServiceProvider.GetRequiredService<IDiscoveryExecutor>();

        var ips = string.Join(',', LabTopology.All.Select(n => n.Ip));
        var runId = await discoveryService.StartDiscoveryAsync(
            new AtlasNOC.Application.Dtos.StartDiscoveryRequest(ips, null, null));

        await executor.ExecuteAsync(runId);

        var run = await Db.DiscoveryRuns.SingleAsync(r => r.Id == runId);
        return run;
    }
}

[Collection("lab-runtime")]
public class LabDiscoveryTests
{
    private readonly LabRuntimeFixture _fx;
    public LabDiscoveryTests(LabRuntimeFixture fx) => _fx = fx;

    [Fact]
    public async Task Discovery_finds_all_61_nodes_without_loss()
    {
        await _fx.ResetAsync();
        var run = await _fx.RunDiscoveryAsync();

        Assert.Equal(61, run.FoundCount);
        Assert.Equal(61, run.NewCount);

        var deviceCount = await _fx.Db.Devices.CountAsync();
        Assert.Equal(61, deviceCount);
    }

    [Fact]
    public async Task Discovery_creates_expected_60_links_from_evidence()
    {
        await _fx.ResetAsync();
        var run = await _fx.RunDiscoveryAsync();

        // 10 físicos (backbone) + 50 wireless (AP↔CPE) = 60 enlaces confirmados.
        Assert.Equal(60, run.ConfirmedLinkCount);

        var physical = await _fx.Db.NetworkLinks.CountAsync(l => l.LinkType == Domain.Enums.LinkType.Physical);
        var wireless = await _fx.Db.NetworkLinks.CountAsync(l => l.LinkType == Domain.Enums.LinkType.Wireless);
        Assert.Equal(10, physical);
        Assert.Equal(50, wireless);
    }

    [Fact]
    public async Task Discovery_produces_no_duplicate_ids_or_ips()
    {
        await _fx.ResetAsync();
        await _fx.RunDiscoveryAsync();

        var ips = await _fx.Db.Devices.Select(d => d.ManagementIp).ToListAsync();
        Assert.Equal(ips.Count, ips.Distinct().Count());

        var ids = await _fx.Db.Devices.Select(d => d.Id.Value).ToListAsync();
        Assert.Equal(ids.Count, ids.Distinct().Count());
    }

    [Fact]
    public async Task Discovery_is_idempotent_re_rerunning_does_not_duplicate()
    {
        await _fx.ResetAsync();
        await _fx.RunDiscoveryAsync();
        var before = await _fx.Db.Devices.CountAsync();

        // Segunda corrida: nada se duplica.
        await _fx.RunDiscoveryAsync();

        var after = await _fx.Db.Devices.CountAsync();
        Assert.Equal(before, after);

        var linkCount = await _fx.Db.NetworkLinks.CountAsync();
        Assert.Equal(60, linkCount);
    }

    [Fact]
    public async Task Restart_preserves_inventory_and_topology()
    {
        await _fx.ResetAsync();
        await _fx.RunDiscoveryAsync();

        // Simula "reinicio": nuevo proveedor de servicios sobre la misma BD.
        var services = new ServiceCollection();
        services.AddDbContext<AtlasNOCDbContext>(o =>
            o.UseMySql(LabRuntimeFixture.ConnectionString, ServerVersion.Parse("8.0.36-mysql")));
        services.AddInfrastructure(labMode: true);
        using var newProvider = services.BuildServiceProvider();
        var db2 = newProvider.GetRequiredService<AtlasNOCDbContext>();

        Assert.Equal(61, await db2.Devices.CountAsync());
        Assert.Equal(60, await db2.NetworkLinks.CountAsync());
    }
}

[Collection("lab-runtime")]
public class LabPollingAndAlertTests
{
    private readonly LabRuntimeFixture _fx;
    public LabPollingAndAlertTests(LabRuntimeFixture fx) => _fx = fx;

    [Fact]
    public async Task Polling_writes_metrics_for_managed_devices()
    {
        await _fx.ResetAsync();
        await _fx.RunDiscoveryAsync();

        var scope = _fx.Services.CreateScope();
        var polling = scope.ServiceProvider.GetRequiredService<IPollingService>();

        // Poll a un nodo concreto (EdgeRouter-01).
        var edge = await _fx.Db.Devices.SingleAsync(d => d.Hostname == "EdgeRouter-01");
        await polling.PollDeviceAsync(edge.Id.Value);

        var metrics = await _fx.Db.MetricSamples
            .Where(m => m.ResourceId == edge.Id.Value.ToString() && m.MetricName == "availability")
            .ToListAsync();

        Assert.NotEmpty(metrics);
        Assert.All(metrics, m => Assert.Equal(100.0, m.ValueDouble));
    }

    [Fact]
    public async Task Alert_rule_creates_alert_from_metric()
    {
        await _fx.ResetAsync();
        await _fx.RunDiscoveryAsync();

        var scope = _fx.Services.CreateScope();
        var polling = scope.ServiceProvider.GetRequiredService<IPollingService>();
        var alertEngine = scope.ServiceProvider.GetRequiredService<IAlertEvaluationEngine>();
        var alertRuleService = scope.ServiceProvider.GetRequiredService<IAlertRuleService>();

        // Regla: CPU > 1% (siempre cierto en LAB) → alerta.
        await alertRuleService.CreateRuleAsync(new AtlasNOC.Application.Dtos.CreateAlertRuleRequest(
            "CPU alta (LAB)", "cpu_usage", ">", 1.0, (int)Domain.Enums.AlertSeverity.High, 1));

        var edge = await _fx.Db.Devices.SingleAsync(d => d.Hostname == "EdgeRouter-01");
        await polling.PollDeviceAsync(edge.Id.Value);
        await alertEngine.EvaluateAllAsync();

        var alert = await _fx.Db.Alerts
            .AnyAsync(a => a.ResourceId == edge.Id.Value.ToString() && a.State == Domain.Enums.AlertState.Open);

        Assert.True(alert, "Se esperaba una alerta abierta por la métrica de CPU.");
    }
}