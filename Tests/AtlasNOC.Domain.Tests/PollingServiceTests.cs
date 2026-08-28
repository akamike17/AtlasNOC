using AtlasNOC.Domain.Services;
using AtlasNOC.Domain.Entities;
using AtlasNOC.Domain.Enums;
using AtlasNOC.Domain.Services.Interfaces;
using Microsoft.Extensions.Options;
using Moq;
using System.Net;
using Xunit;

namespace AtlasNOC.Domain.Tests;

public sealed class PollingServiceTests
{
    [Fact]
    public void ParseInterfaceTable_ExtractsOperationalStateAndCounters()
    {
        var values = new Dictionary<string, string>
        {
            ["1.3.6.1.2.1.2.2.1.2.7"] = "GigabitEthernet0/7",
            ["1.3.6.1.2.1.2.2.1.5.7"] = "1000000000",
            ["1.3.6.1.2.1.2.2.1.7.7"] = "1",
            ["1.3.6.1.2.1.2.2.1.8.7"] = "2",
            ["1.3.6.1.2.1.2.2.1.10.7"] = "4294967295",
            ["1.3.6.1.2.1.2.2.1.14.7"] = "3",
            ["1.3.6.1.2.1.999.1.0"] = "ignored"
        };

        var parsed = PollingService.ParseInterfaceTable(values);
        var item = Assert.IsType<Dictionary<string, object>>(parsed["7"]);

        Assert.Equal("GigabitEthernet0/7", item["description"]);
        Assert.Equal(1_000_000_000UL, item["speed_bps"]);
        Assert.Equal(1UL, item["admin_status"]);
        Assert.Equal(2UL, item["oper_status"]);
        Assert.Equal(4_294_967_295UL, item["in_octets"]);
        Assert.Equal(3UL, item["in_errors"]);
    }

    [Fact]
    public void ParseInterfaceTable_IgnoresMalformedOids()
    {
        var parsed = PollingService.ParseInterfaceTable(new Dictionary<string, string>
        {
            ["1.3.6.1.2.1.2.2.1.bad.1"] = "value",
            ["1.3.6.1.2.1.2.2.1.2.bad"] = "value"
        });

        Assert.Empty(parsed);
    }

    [Fact]
    public async Task PollDeviceAsync_TransitionsDownThenResolvesAlertOnRecovery()
    {
        var devices = new InMemoryRepository<Device>(device => device.Id.Value);
        var device = Device.Create("switch", "192.0.2.20", DeviceType.Switch, "test");
        await devices.AddAsync(device);
        var credentials = new Mock<ICredentialService>();
        credentials.Setup(service => service.GetActiveAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { Credential.CreateV2c("poll", "protected", "test") });
        var snmp = new Mock<ISnmpService>();
        snmp.SetupSequence(service => service.TestConnectionAsync(
                It.IsAny<IPAddress>(), It.IsAny<Credential>(), It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SnmpTestResult(false, "timeout", TimeSpan.FromMilliseconds(100)))
            .ReturnsAsync(new SnmpTestResult(true, null, TimeSpan.FromMilliseconds(12)));
        snmp.Setup(service => service.GetAsync(It.IsAny<IPAddress>(), It.IsAny<Credential>(),
                It.IsAny<string>(), It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SnmpResult(true, "12345", 0, 0, null, TimeSpan.FromMilliseconds(5)));
        snmp.Setup(service => service.WalkAsync(It.IsAny<IPAddress>(), It.IsAny<Credential>(),
                It.IsAny<string>(), It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SnmpWalkResult(true, new Dictionary<string, string>(), null, TimeSpan.FromMilliseconds(5)));
        var alertRepository = new InMemoryRepository<Alert>(alert => alert.Id.Value);
        var audit = new AuditService(new InMemoryRepository<AuditEvent>(item => item.EventId),
            new TestLogger<AuditService>());
        var alerts = new AlertService(alertRepository, audit);
        var history = new Mock<IMetricHistoryService>();
        var polling = new PollingService(devices, snmp.Object, credentials.Object, alerts,
            new TestLogger<PollingService>(), Options.Create(new PollingOptions { MaxConcurrency = 1 }),
            history.Object);

        var failed = await polling.PollDeviceAsync(device.Id);
        var recovered = await polling.PollDeviceAsync(device.Id);

        Assert.False(failed.Success);
        Assert.True(recovered.Success);
        Assert.Equal(DeviceStatus.Up, device.Status);
        var downAlert = Assert.Single(await alertRepository.GetAllAsync());
        Assert.False(downAlert.IsActive);
        Assert.Equal("Device recovered", downAlert.ResolutionNotes);
        history.Verify(service => service.SaveAsync(It.IsAny<PollingResult>(), It.IsAny<CancellationToken>()),
            Times.Exactly(2));
    }
}
