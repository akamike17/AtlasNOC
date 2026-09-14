using AtlasNOC.Infrastructure.Probes;
using Xunit;

namespace AtlasNOC.Tests.Unit;

public sealed class SsdpPresenceProbeTests
{
    [Fact]
    public async Task Empty_scope_does_not_emit_network_traffic()
    {
        var result = await new SsdpPresenceProbe().DiscoverAsync(
            new HashSet<string>(StringComparer.OrdinalIgnoreCase), 100);

        Assert.Empty(result);
    }
}
