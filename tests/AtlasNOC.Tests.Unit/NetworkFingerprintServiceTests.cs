using AtlasNOC.Application.Probes;
using AtlasNOC.Infrastructure.Services;
using Xunit;

namespace AtlasNOC.Tests.Unit;

public class NetworkFingerprintServiceTests
{
    private readonly NetworkFingerprintService _service = new();

    [Theory]
    [InlineData("1.3.6.1.4.1.14988.1", "mikrotik")]
    [InlineData("RouterOS RB4011", "mikrotik")]
    [InlineData("1.3.6.1.4.1.41112.1", "ubiquiti")]
    [InlineData("UniFi AP-AC-Lite", "ubiquiti")]
    [InlineData("1.3.6.1.4.1.9.1", "cisco")]
    [InlineData("Cisco IOS", "cisco")]
    [InlineData("unknown thing", "generic")]
    public void ResolveVendor_returns_expected(string sysDescr, string expectedVendor)
    {
        var fp = new DeviceFingerprint("10.0.0.1", null, null, sysDescr, null);
        Assert.Equal(expectedVendor, _service.ResolveVendor(fp));
    }

    [Theory]
    [InlineData("switch 24 ports", 2)]
    [InlineData("router", 1)]
    [InlineData("access point", 6)]
    [InlineData("cpe station", 8)]
    [InlineData("unifi ap", 6)]
    public void ResolveDeviceType_returns_expected(string sysDescr, int expectedType)
    {
        var fp = new DeviceFingerprint("10.0.0.1", null, null, sysDescr, null);
        Assert.Equal(expectedType, _service.ResolveDeviceType(fp));
    }
}