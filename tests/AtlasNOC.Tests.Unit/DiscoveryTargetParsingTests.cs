using AtlasNOC.Infrastructure.Probes;
using AtlasNOC.Infrastructure.Services;
using AtlasNOC.Application.Probes;
using System.Diagnostics;
using Xunit;

namespace AtlasNOC.Tests.Unit;

public class DiscoveryTargetParsingTests
{
    [Theory]
    [InlineData("192.0.2.1/32", 1)]
    [InlineData("192.0.2.0/31", 2)]
    [InlineData("192.0.2.0/30", 2)]
    [InlineData("192.0.2.0/24", 254)]
    public void Cidr_returns_usable_hosts(string scope, int expected)
    {
        Assert.True(CidrSubnet.TryParse(scope, out var subnet));
        Assert.Equal(expected, subnet.EnumerateHosts().Count());
    }

    [Fact]
    public void Mixed_ip_and_cidr_are_combined_without_duplicates()
    {
        var targets = DiscoveryExecutor.ParseTargets("192.0.2.1, 198.51.100.0/31;192.0.2.1", 10);
        Assert.Equal(3, targets.Count);
    }

    [Fact]
    public void Oversized_scope_is_rejected_before_enumeration()
        => Assert.Throws<ArgumentException>(() => DiscoveryExecutor.ParseTargets("10.0.0.0/8", 4096));

    [Fact]
    public void Invalid_hostname_is_rejected()
        => Assert.Throws<ArgumentException>(() => DiscoveryExecutor.ParseTargets("not-a-host", 10));

    [Fact]
    public async Task Hundred_hosts_are_probed_with_bounded_parallelism()
    {
        var probe = new DelayedProbe(TimeSpan.FromMilliseconds(20));
        var targets = Enumerable.Range(1, 100).Select(i => $"192.0.2.{i}").ToList();
        var options = new DiscoveryOptions { MaxConcurrentPing = 20, PingTimeoutMs = 1000 };
        var watch = Stopwatch.StartNew();

        var live = await DiscoveryExecutor.ProbeLiveTargetsAsync(targets, probe, options, CancellationToken.None);

        Assert.Equal(100, live.Count);
        Assert.True(watch.Elapsed < TimeSpan.FromSeconds(1), $"El barrido tardó {watch.Elapsed}.");
        Assert.InRange(probe.PeakConcurrency, 2, 20);
    }

    [Fact]
    public async Task Ping_sweep_honors_cancellation()
    {
        var probe = new DelayedProbe(TimeSpan.FromSeconds(5));
        using var cancellation = new CancellationTokenSource(TimeSpan.FromMilliseconds(30));
        var options = new DiscoveryOptions { MaxConcurrentPing = 4, PingTimeoutMs = 10000 };

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            DiscoveryExecutor.ProbeLiveTargetsAsync(new[] { "192.0.2.1", "192.0.2.2" }, probe, options, cancellation.Token));
    }

    private sealed class DelayedProbe(TimeSpan delay) : IIcmpProbe
    {
        private int _current;
        private int _peak;
        public int PeakConcurrency => _peak;

        public async Task<PingResult> PingAsync(string ipAddress, int timeoutMs, CancellationToken ct)
        {
            var current = Interlocked.Increment(ref _current);
            int observed;
            while (current > (observed = _peak))
                Interlocked.CompareExchange(ref _peak, current, observed);
            try
            {
                await Task.Delay(delay, ct);
                return new PingResult(true, delay.TotalMilliseconds, null);
            }
            finally
            {
                Interlocked.Decrement(ref _current);
            }
        }
    }
}
