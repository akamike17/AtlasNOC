using AtlasNOC.Application.Probes;
using AtlasNOC.Domain.Enums;
using Xunit;

namespace AtlasNOC.Tests.Unit;

public sealed class SnmpOptionalPresenceTests
{
    [Fact]
    public void Anonymous_presence_options_are_valid_read_only_defaults()
    {
        var options = SnmpConnectionOptions.Anonymous();

        Assert.Equal(SnmpVersion.V2c, options.Version);
        Assert.Equal("public", options.Community);
        options.Validate();
    }
}
