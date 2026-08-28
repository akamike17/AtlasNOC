using AtlasNOC.Domain.Entities;
using AtlasNOC.Domain.Services;
using AtlasNOC.Domain.Services.Interfaces;
using Moq;
using Xunit;

namespace AtlasNOC.Domain.Tests;

public sealed class DiscoveryServiceTests
{
    [Fact]
    public async Task DiscoverAsync_PreCancelledRequest_ReturnsCancelledState()
    {
        var credentials = new Mock<ICredentialService>();
        credentials.Setup(service => service.GetActiveAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<Credential>());
        var service = CreateService(credentials);
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        var result = await service.DiscoverAsync(Request("192.0.2.0/30"), cancellation.Token);

        Assert.Equal(DiscoveryStatus.Cancelled, result.Status);
        Assert.NotNull(result.CompletedAt);
    }

    [Fact]
    public async Task DiscoverAsync_RejectsSubnetAboveConfiguredTargetLimitBeforeCredentialLookup()
    {
        var credentials = new Mock<ICredentialService>(MockBehavior.Strict);
        var service = CreateService(credentials);

        var result = await service.DiscoverAsync(Request("10.0.0.0/8", maxTargets: 256));

        Assert.Equal(DiscoveryStatus.Failed, result.Status);
        Assert.Contains("exceeding", result.ErrorMessage, StringComparison.OrdinalIgnoreCase);
        credentials.VerifyNoOtherCalls();
    }

    [Fact]
    public void ParseLldpNeighbors_GroupsColumnsByRemoteIndex()
    {
        var root = "1.0.8802.1.1.2.1.4.1.1.";
        var neighbors = DiscoveryService.ParseLldpNeighbors(new Dictionary<string, string>
        {
            [$"{root}5.22.7.1"] = "00:11:22:33:44:55",
            [$"{root}7.22.7.1"] = "Gi0/1",
            [$"{root}9.22.7.1"] = "core-switch"
        });

        var neighbor = Assert.Single(neighbors);
        Assert.Equal("22", neighbor.LocalInterface);
        Assert.Equal("00:11:22:33:44:55", neighbor.RemoteChassisId);
        Assert.Equal("Gi0/1", neighbor.RemotePortId);
        Assert.Equal("core-switch", neighbor.RemoteSystemName);
        Assert.Equal(NeighborProtocol.Lldp, neighbor.Protocol);
    }

    [Fact]
    public void ParseCdpNeighbors_DropsIncompleteRows()
    {
        var root = "1.3.6.1.4.1.9.9.23.1.2.1.1.";
        var neighbors = DiscoveryService.ParseCdpNeighbors(new Dictionary<string, string>
        {
            [$"{root}4.9.1"] = "192.0.2.8",
            [$"{root}6.9.1"] = "edge-router",
            [$"{root}7.9.1"] = "GigabitEthernet0/2",
            [$"{root}4.10.1"] = "192.0.2.9"
        });

        var neighbor = Assert.Single(neighbors);
        Assert.Equal("9", neighbor.LocalInterface);
        Assert.Equal("192.0.2.8", neighbor.RemoteChassisId);
        Assert.Equal(NeighborProtocol.Cdp, neighbor.Protocol);
    }

    [Fact]
    public async Task GetHistoryAsync_SurvivesLogicalRestart()
    {
        var credentials = new Mock<ICredentialService>();
        credentials.Setup(service => service.GetActiveAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<Credential>());
        var shared = new InMemoryRepository<DiscoveryRun>(run => run.Id);

        // First process completes a cancelled (or any) run that is persisted to the repo.
        var first = CreateService(credentials, shared);
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        var firstResult = await first.DiscoverAsync(Request("192.0.2.0/30"), cancellation.Token);

        Assert.Single(await shared.GetAllAsync());

        // Logical restart: a brand-new service instance shares the same persisted repository
        // (representing the database surviving across restarts) and has no in-memory state.
        var restarted = CreateService(new Mock<ICredentialService>(), shared);

        var history = await restarted.GetHistoryAsync();
        Assert.Contains(history, r => r.Id == firstResult.Id);
        Assert.Equal(DiscoveryStatus.Cancelled, history.First(r => r.Id == firstResult.Id).Status);

        // GetDiscoveryAsync also resolves persisted runs after restart.
        var byId = await restarted.GetDiscoveryAsync(firstResult.Id);
        Assert.NotNull(byId);
        Assert.Equal(firstResult.Id, byId!.Id);
    }

    [Fact]
    public async Task GetHistoryAsync_MergesInMemoryAndPersistedRuns()
    {
        var credentials = new Mock<ICredentialService>();
        credentials.Setup(service => service.GetActiveAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<Credential>());
        var shared = new InMemoryRepository<DiscoveryRun>(run => run.Id);

        var first = CreateService(credentials, shared);
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        await first.DiscoverAsync(Request("192.0.2.0/30"), cancellation.Token);

        var restarted = CreateService(new Mock<ICredentialService>(), shared);
        using var cancellation2 = new CancellationTokenSource();
        cancellation2.Cancel();
        var currentRun = await restarted.DiscoverAsync(Request("192.0.2.4/30"), cancellation2.Token);

        var history = await restarted.GetHistoryAsync();
        Assert.Equal(2, history.Count); // one persisted + one in-memory
        Assert.Contains(history, r => r.Id == currentRun.Id);
    }

    private static DiscoveryService CreateService(
        Mock<ICredentialService> credentials,
        InMemoryRepository<DiscoveryRun>? runRepository = null) =>
        new(
            Mock.Of<IRepository<Device>>(),
            runRepository ?? new InMemoryRepository<DiscoveryRun>(run => run.Id),
            credentials.Object,
            Mock.Of<ISnmpService>(),
            new TestLogger<DiscoveryService>());

    private static DiscoveryRequest Request(string cidr, int maxTargets = 4096) =>
        new(cidr, Array.Empty<AtlasNOC.Domain.ValueObjects.CredentialId>(),
            new DiscoveryOptions(
                MaxConcurrency: 2,
                PingTimeout: TimeSpan.FromMilliseconds(10),
                SnmpTimeout: TimeSpan.FromMilliseconds(10),
                CommonPorts: Array.Empty<int>(),
                MaxTargets: maxTargets));
}
