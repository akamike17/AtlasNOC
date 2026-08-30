using AtlasNOC.Application.Probes;
using AtlasNOC.Infrastructure.Devices;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace AtlasNOC.Tests.Unit;

public class DeviceDriverTests
{
    private static MikroTikDriver Mk() => new(new TestHttpFactory(), new MikroTikOptions());
    private static UbiquitiDriver Ub() => new(new TestHttpFactory(), new UbiquitiOptions { ControllerUrl = "https://unifi.local" });

    [Fact]
    public void MikroTik_driver_matches_routeros_fingerprint()
    {
        var fp = new DeviceFingerprint("10.0.0.1", "rb4011", "1.3.6.1.4.1.14988.1", "RouterOS 7.10", null);
        Assert.True(Mk().CanHandle(fp));
        Assert.Equal("mikrotik", Mk().DriverKey);
    }

    [Fact]
    public void Ubiquiti_driver_matches_unifi_fingerprint()
    {
        var fp = new DeviceFingerprint("10.0.0.2", "ap-office", "1.3.6.1.4.1.41112.1", "UniFi AP", null);
        Assert.True(Ub().CanHandle(fp));
        Assert.Equal("ubiquiti", Ub().DriverKey);
    }

    [Fact]
    public void MikroTik_driver_rejects_unrelated_fingerprint()
    {
        var fp = new DeviceFingerprint("10.0.0.3", "sw1", "1.3.6.1.4.1.9.1", "Cisco IOS", null);
        Assert.False(Mk().CanHandle(fp));
        Assert.False(Ub().CanHandle(fp));
    }

    private sealed class TestHttpFactory : IHttpClientFactory
    {
        public System.Net.Http.HttpClient CreateClient(string name) => new();
    }
}