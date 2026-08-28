using AtlasNOC.Domain.Services;
using Xunit;

namespace AtlasNOC.Domain.Tests;

public sealed class IPNetworkTests
{
    [Fact]
    public void EnumerateIPAddresses_UsesNetworkByteOrderAndExcludesNetworkAndBroadcast()
    {
        Assert.True(IPNetwork.TryParse("192.168.10.0/30", out var network));

        var addresses = network!.EnumerateIPAddresses().Select(address => address.ToString()).ToArray();

        Assert.Equal(2UL, network.UsableAddressCount);
        Assert.Equal(new[] { "192.168.10.1", "192.168.10.2" }, addresses);
    }

    [Fact]
    public void EnumerateIPAddresses_IncludesBothPointToPointAddresses()
    {
        Assert.True(IPNetwork.TryParse("10.0.0.4/31", out var network));

        var addresses = network!.EnumerateIPAddresses().Select(address => address.ToString()).ToArray();

        Assert.Equal(new[] { "10.0.0.4", "10.0.0.5" }, addresses);
    }

    [Fact]
    public void TryParse_RejectsIpv6UntilIpv6DiscoveryIsImplemented()
    {
        Assert.False(IPNetwork.TryParse("2001:db8::/64", out _));
    }
}
