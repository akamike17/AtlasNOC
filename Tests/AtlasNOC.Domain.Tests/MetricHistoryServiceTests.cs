using AtlasNOC.Domain.Data;
using AtlasNOC.Domain.Entities;
using AtlasNOC.Domain.Services;
using AtlasNOC.Domain.Services.Interfaces;
using AtlasNOC.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace AtlasNOC.Domain.Tests;

public sealed class MetricHistoryServiceTests
{
    [Fact]
    public async Task SaveAndQueryAsync_PersistsAndPaginatesSamples()
    {
        await using var context = Context();
        var device = Device.Create("router", "192.0.2.10", AtlasNOC.Domain.Enums.DeviceType.Router, "test");
        context.Devices.Add(device);
        await context.SaveChangesAsync();
        var service = new MetricHistoryService(context);
        var now = DateTime.UtcNow;

        await service.SaveAsync(Result(device.Id, now.AddMinutes(-2), 10));
        await service.SaveAsync(Result(device.Id, now.AddMinutes(-1), 20));

        var page = await service.QueryAsync(device.Id, now.AddHours(-1), now.AddHours(1), 1, 1);

        Assert.Equal(2, page.TotalCount);
        Assert.Single(page.Items);
        Assert.Equal(20, page.Items[0].LatencyMs);
    }

    [Fact]
    public async Task QueryAsync_RejectsUnboundedPageSize()
    {
        await using var context = Context();
        var service = new MetricHistoryService(context);

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => service.QueryAsync(
            DeviceId.New(), DateTime.UtcNow.AddHours(-1), DateTime.UtcNow, 1, 501));
    }

    private static AtlasNOCDbContext Context()
    {
        var options = new DbContextOptionsBuilder<AtlasNOCDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options;
        return new AtlasNOCDbContext(options);
    }

    private static PollingResult Result(DeviceId deviceId, DateTime time, double latency) =>
        new(deviceId, time, true,
            new PollingMetrics(latency, 100, new Dictionary<string, object>
            {
                ["1"] = new Dictionary<string, object> { ["in_octets"] = 123UL }
            }, null, null), null, Array.Empty<PollingAlert>());
}
