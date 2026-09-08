using AtlasNOC.Domain.Entities;
using AtlasNOC.Domain.Enums;
using Xunit;

namespace AtlasNOC.Tests.Unit;

public class DiscoveryRunLeaseTests
{
    [Fact]
    public void PendingRun_CanBeClaimedAndLeaseRenewedByOwner()
    {
        var run = new DiscoveryRun("192.0.2.0/30", null, null);
        var now = new DateTime(2026, 9, 1, 12, 0, 0, DateTimeKind.Utc);

        run.Claim("worker-a", now, TimeSpan.FromMinutes(1));
        run.RenewLease("worker-a", now.AddSeconds(20), TimeSpan.FromMinutes(1));

        Assert.Equal(DiscoveryRunStatus.Running, run.Status);
        Assert.Equal("worker-a", run.ClaimedBy);
        Assert.Equal(1, run.AttemptCount);
        Assert.Equal(now.AddSeconds(80), run.LeaseExpiresAtUtc);
    }

    [Fact]
    public void ActiveLease_CannotBeStolen()
    {
        var run = new DiscoveryRun("192.0.2.1", null, null);
        var now = DateTime.UtcNow;
        run.Claim("worker-a", now, TimeSpan.FromMinutes(1));

        Assert.Throws<InvalidOperationException>(() =>
            run.Claim("worker-b", now.AddSeconds(30), TimeSpan.FromMinutes(1)));
    }

    [Fact]
    public void ExpiredLease_CanBeRecoveredAndCountsAttempt()
    {
        var run = new DiscoveryRun("192.0.2.1", null, null);
        var now = DateTime.UtcNow;
        run.Claim("worker-a", now, TimeSpan.FromSeconds(10));

        run.Claim("worker-b", now.AddSeconds(11), TimeSpan.FromMinutes(1));

        Assert.Equal("worker-b", run.ClaimedBy);
        Assert.Equal(2, run.AttemptCount);
    }

    [Fact]
    public void Cancellation_IsTerminalAndReleasesLease()
    {
        var run = new DiscoveryRun("192.0.2.1", null, null);
        run.Claim("worker-a", DateTime.UtcNow, TimeSpan.FromMinutes(1));

        run.Cancel();

        Assert.Equal(DiscoveryRunStatus.Cancelled, run.Status);
        Assert.NotNull(run.CompletedAtUtc);
        Assert.Null(run.LeaseExpiresAtUtc);
    }
}
