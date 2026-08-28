using AtlasNOC.Domain.Data;
using AtlasNOC.Domain.Entities;
using AtlasNOC.Domain.Enums;
using AtlasNOC.Domain.Services;
using AtlasNOC.Domain.Services.Interfaces;
using Microsoft.EntityFrameworkCore;
using Moq;
using Xunit;

namespace AtlasNOC.Domain.Tests;

/// <summary>
/// Integration tests that exercise persistence against a real MySQL database.
/// They are gated on the ConnectionStrings__DefaultConnection environment variable so
/// they run only when a live database is available (local verification) and are
/// skipped (as no-ops) in CI environments with no database.
/// </summary>
public sealed class MySqlPersistenceIntegrationTests
{
    private static readonly string? ConnectionString =
        Environment.GetEnvironmentVariable("ConnectionStrings__DefaultConnection");

    private static bool HasDatabase => !string.IsNullOrWhiteSpace(ConnectionString);

    private static AtlasNOCDbContext CreateContext() =>
        new(new DbContextOptionsBuilder<AtlasNOCDbContext>()
            .UseMySql(ConnectionString!,
                new MySqlServerVersion(new Version(8, 0, 0)),
                mysql => mysql.MigrationsAssembly("AtlasNOC.Domain"))
            .Options);

    private static DiscoveryResult SampleRun(Guid id)
    {
        var device = new DiscoveredDevice(
            Id: Guid.NewGuid(),
            IpAddress: "192.0.2.25",
            Hostname: "core-sw-01",
            SysDescr: "Router example",
            SysObjectId: "1.3.6.1.4.1.9",
            Vendor: "Example",
            DeviceType: DeviceType.Switch,
            Interfaces: new[]
            {
                new DiscoveredInterface(
                    "1", "Gi0/1", null, "00:11:22:33:44:55", null,
                    InterfaceAdminStatus.Up, InterfaceOperStatus.Up, 1_000_000_000, null,
                    Array.Empty<VlanInfo>())
            },
            Neighbors: new[]
            {
                new DiscoveredNeighbor("Gi0/1", "192.0.2.26", "Gi0/1", "edge-02",
                    NeighborProtocol.Lldp, 0.9)
            },
            DiscoveredAt: DateTime.UtcNow,
            Evidence: new DiscoveryEvidence(
                HasPing: true, HasSnmp: true, HasLldp: true, HasCdp: false, HasArp: false,
                HasMacTable: false, OidsQueried: new[] { "1.3.6.1.2.1.1.1.0" },
                OidsResponded: new[] { "1.3.6.1.2.1.1.1.0" }));
        return new DiscoveryResult(id, DateTime.UtcNow, DateTime.UtcNow,
            DiscoveryStatus.Completed, new[] { device }, 1, 1, null);
    }

    private static void GuardDatabase() { } // no-op; used to keep the gate self-documenting.

    [Fact]
    public async Task DiscoveryRun_PersistedToMySql_IsReconstructableAcrossLogicalRestart()
    {
        GuardDatabase();
        if (!HasDatabase)
        {
            // Integration gate: skipped as a no-op when no live database is configured.
            return;
        }

        var runId = Guid.NewGuid();
        var cidr = $"198.51.100.{DateTime.UtcNow.Second}/32"; // unique subnet per run
        try
        {
            // ── "First process": persist a completed discovery run through the service repo.
            Guid runIdentifier;
            using (var context = CreateContext())
            {
                await context.Database.MigrateAsync();
                var repo = new EfCoreRepository<DiscoveryRun>(context);
                var run = DiscoveryRun.Start(runId, cidr, DateTime.UtcNow);
                run.Complete(SampleRun(runId));
                await repo.AddAsync(run);
                runIdentifier = run.Id;
            }

            // ── "Logical restart": brand-new DbContext + repository (new process) over the same DB.
            using (var context2 = CreateContext())
            {
                var repo2 = new EfCoreRepository<DiscoveryRun>(context2);
                var persisted = await repo2.GetByIdAsync(runIdentifier);
                Assert.NotNull(persisted);
                var reconstructed = persisted!.ToResult();
                Assert.Equal(DiscoveryStatus.Completed, reconstructed.Status);
                Assert.Equal(cidr, persisted.SubnetCidr);
                Assert.NotNull(reconstructed.CompletedAt);
                var device = Assert.Single(reconstructed.Devices);
                Assert.Equal("192.0.2.25", device.IpAddress);
                Assert.Equal("core-sw-01", device.Hostname);

                // IfIndex/MAC survive JSON round-trip through the DB column.
                var iface = Assert.Single(device.Interfaces);
                Assert.Equal("1", iface.IfIndex);
                Assert.Equal("00:11:22:33:44:55", iface.MacAddress);
            }
        }
        finally
        {
            // clean up the row so the integration test is idempotent
            using var cleanup = CreateContext();
            var existing = cleanup.DiscoveryRuns.FirstOrDefault(r => r.Id == runId);
            if (existing is not null)
            {
                cleanup.DiscoveryRuns.Remove(existing);
                await cleanup.SaveChangesAsync();
            }
        }
    }

    [Fact]
    public async Task Topology_RebuildsConfirmedLinks_FromMySqlAfterLogicalRestart()
    {
        GuardDatabase();
        if (!HasDatabase) return;

        var edgeIp = "192.0.2.42";
        var runId = Guid.NewGuid();
        try
        {
            // ── Persist the two devices and a completed run (with an LLDP neighbor) via the repos.
            var core = Device.Create("core-sw-01", "192.0.2.41", DeviceType.Switch, "IntegrationTest");
            var edge = Device.Create("edge-02", edgeIp, DeviceType.Switch, "IntegrationTest");
            core.UpdateStatus(DeviceStatus.Up, "IntegrationTest");
            edge.UpdateStatus(DeviceStatus.Up, "IntegrationTest");
            var neighbor = new DiscoveredNeighbor(
                LocalInterface: "Gi0/1", RemoteChassisId: edgeIp, RemotePortId: "Gi0/1",
                RemoteSystemName: "edge-02", Protocol: NeighborProtocol.Lldp, Confidence: 0.9);
            var discovered = new DiscoveredDevice(
                Id: Guid.NewGuid(), IpAddress: core.IpAddress, Hostname: core.Name,
                SysDescr: "Router example", SysObjectId: null, Vendor: "Example",
                DeviceType: DeviceType.Switch,
                Interfaces: Array.Empty<DiscoveredInterface>(),
                Neighbors: new[] { neighbor },
                DiscoveredAt: DateTime.UtcNow,
                Evidence: new DiscoveryEvidence(true, true, true, false, false, false,
                    new[] { "1.0.8802.1.1.2.1.4.1.1.7" }, new[] { "1.0.8802.1.1.2.1.4.1.1.7" }));
            var result = new DiscoveryResult(runId, DateTime.UtcNow, DateTime.UtcNow,
                DiscoveryStatus.Completed, new[] { discovered }, 1, 1, null);

            using (var context = CreateContext())
            {
                await context.Database.MigrateAsync();
                var run = DiscoveryRun.Start(runId, "192.0.2.40/30", DateTime.UtcNow);
                run.Complete(result);
                await new EfCoreRepository<DiscoveryRun>(context).AddAsync(run);
                await new EfCoreRepository<Device>(context).AddAsync(core);
                await new EfCoreRepository<Device>(context).AddAsync(edge);
            }

            // ── Logical restart: fresh service graph over the same DB (no in-memory state).
            using (var context = CreateContext())
            {
                var deviceRepo = new EfCoreRepository<Device>(context);
                var runRepo = new EfCoreRepository<DiscoveryRun>(context);
                var discovery = new DiscoveryService(deviceRepo, runRepo,
                    Mock.Of<ICredentialService>(), Mock.Of<ISnmpService>(),
                    new TestLogger<DiscoveryService>());
                var topology = new TopologyService(deviceRepo, discovery, new TestLogger<TopologyService>());

                var map = await topology.RebuildTopologyAsync();

                Assert.Equal(2, map.Nodes.Count);
                Assert.Equal(DeviceStatus.Up, map.Nodes.Single(n => n.IpAddress == core.IpAddress).Status);
                var link = Assert.Single(map.Links);
                Assert.Equal(LinkType.Lldp, link.Type);
                // Deterministic + stable id between reconstructions (idempotent rebuild).
                var map2 = await topology.RebuildTopologyAsync();
                Assert.Equal(link.Id, map2.Links.Single().Id);
            }
        }
        finally
        {
            using var cleanup = CreateContext();
            var repo = new EfCoreRepository<DiscoveryRun>(cleanup);
            if (await repo.GetByIdAsync(runId) is { } r) await repo.DeleteAsync(r);
            foreach (var device in cleanup.Devices.Where(d =>
                         d.Name == "core-sw-01" || d.Name == "edge-02"))
                cleanup.Devices.Remove(device);
            await cleanup.SaveChangesAsync();
        }
    }
}