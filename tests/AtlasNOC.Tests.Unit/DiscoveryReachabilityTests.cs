using AtlasNOC.Application.Probes;
using AtlasNOC.Application.Services;
using AtlasNOC.Infrastructure.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace AtlasNOC.Tests.Unit;

public sealed class DiscoveryReachabilityTests
{
    [Fact]
    public async Task Arp_responsive_target_is_kept_when_icmp_is_blocked()
    {
        var icmp = new StubIcmpProbe(new PingResult(false, null, "timeout"));
        var arp = new StubArpProbe(true);
        var options = new DiscoveryOptions { MaxConcurrentPing = 1, PingTimeoutMs = 50 };

        var result = await DiscoveryExecutor.ProbeReachableTargetsAsync(
            ["192.0.2.10"], icmp, arp, options, CancellationToken.None,
            NullLogger.Instance, Guid.NewGuid());

        Assert.Single(result);
        Assert.Equal("192.0.2.10", result[0]);
    }

    private sealed class StubIcmpProbe(PingResult result) : IIcmpProbe
    {
        public Task<PingResult> PingAsync(string ipAddress, int timeoutMs, CancellationToken ct) => Task.FromResult(result);
    }

    private sealed class StubArpProbe(bool result) : IArpProbe
    {
        public Task<bool> ResolveAsync(string ipAddress, CancellationToken ct = default) => Task.FromResult(result);
    }
}
