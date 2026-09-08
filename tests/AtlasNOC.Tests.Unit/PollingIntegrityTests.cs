using AtlasNOC.Application.Probes;
using AtlasNOC.Domain.Entities;
using AtlasNOC.Domain.Enums;
using AtlasNOC.Infrastructure.Services;
using Xunit;

namespace AtlasNOC.Tests.Unit;

public class PollingIntegrityTests
{
    [Fact]
    public void MarkPolled_DoesNotInventLastSeen()
    {
        var device = new Device("router", "192.0.2.1", DeviceType.Router, Vendor.Generic);
        var polledAt = new DateTime(2026, 9, 1, 12, 0, 0, DateTimeKind.Utc);

        device.MarkPolled(polledAt);

        Assert.Equal(polledAt, device.LastPolledAtUtc);
        Assert.Null(device.LastSeenAtUtc);
    }

    [Fact]
    public void EmptyHealth_DoesNotCountAsResponseEvidence()
    {
        var health = new HealthData(null, null, null, null, null);

        Assert.False(PollingService.HasHealthEvidence(health));
    }

    [Fact]
    public void NullableHealthValue_CountsAsResponseEvidence_EvenWhenZero()
    {
        var health = new HealthData(null, null, 0, null, null);

        Assert.True(PollingService.HasHealthEvidence(health));
    }

    [Fact]
    public void PollingSchedule_IsDueOnlyAfterConfiguredInterval()
    {
        var last = new DateTime(2026, 9, 1, 12, 0, 0, DateTimeKind.Utc);

        Assert.False(PollingService.IsDue(last, last.AddSeconds(29), 30));
        Assert.True(PollingService.IsDue(last, last.AddSeconds(30), 30));
        Assert.True(PollingService.IsDue(null, last, 30));
    }

    [Fact]
    public void Device_CanInheritOrUseExplicitPollingProfile()
    {
        var device = new Device("router", "192.0.2.2", DeviceType.Router, Vendor.Generic);
        var profileId = Guid.NewGuid();

        device.SetPollingProfile(profileId);
        Assert.Equal(profileId, device.PollingProfileId);

        device.SetPollingProfile(null);
        Assert.Null(device.PollingProfileId);
    }
}
