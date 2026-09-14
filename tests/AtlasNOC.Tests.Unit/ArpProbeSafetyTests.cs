using System.Net;
using AtlasNOC.Infrastructure.Probes;
using Xunit;

namespace AtlasNOC.Tests.Unit;

public sealed class ArpProbeSafetyTests
{
    [Theory]
    [InlineData("127.0.0.1")]
    [InlineData("0.0.0.0")]
    [InlineData("255.255.255.255")]
    [InlineData("224.0.0.1")]
    public void Rejects_non_unicast_targets(string value)
        => Assert.False(ArpProbe.IsEligibleTarget(IPAddress.Parse(value)));

    [Fact]
    public void Accepts_private_unicast_target()
        => Assert.True(ArpProbe.IsEligibleTarget(IPAddress.Parse("192.168.1.1")));
}
