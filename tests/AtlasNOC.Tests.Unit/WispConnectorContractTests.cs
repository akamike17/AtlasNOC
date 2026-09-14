using AtlasNOC.Application.Wisp;
using AtlasNOC.Infrastructure.Wisp;
using AtlasNOC.Infrastructure.Devices;
using Xunit;

namespace AtlasNOC.Tests.Unit;

public sealed class WispConnectorContractTests
{
    [Fact]
    public void Registry_is_empty_and_safe_when_no_connectors_are_configured()
    {
        var registry = new WispConnectorRegistry(Array.Empty<IWispConnector>());

        Assert.Empty(registry.List());
        Assert.Null(registry.Resolve("mikrotik"));
    }

    [Fact]
    public void Suspend_capability_is_explicit_and_not_part_of_read_defaults()
    {
        var descriptor = new WispConnectorDescriptor(
            "radius", "RADIUS", WispConnectorCapabilities.ReadClients,
            RequiresCredentials: true, ReadOnlyByDefault: true);

        Assert.True(descriptor.ReadOnlyByDefault);
        Assert.False(descriptor.Capabilities.HasFlag(WispConnectorCapabilities.SuspendClient));
    }

    [Fact]
    public void Catalog_lists_supported_integrations_without_claiming_they_are_configured()
    {
        Assert.Contains(WispConnectorCatalog.Supported, x => x.Key == "mikrotik");
        Assert.All(WispConnectorCatalog.Supported, x => Assert.True(x.ReadOnlyByDefault));
    }

    [Fact]
    public void MikroTik_options_can_be_bound_without_exposing_secret_values()
    {
        var options = new MikroTikOptions { Username = "operator", TimeoutSeconds = 7 };

        Assert.Equal("operator", options.Username);
        Assert.Null(options.Password);
        Assert.Equal(7, options.TimeoutSeconds);
    }

    [Fact]
    public void Catalog_contains_read_only_Ubiquiti_support()
    {
        var descriptor = WispConnectorCatalog.Supported.Single(x => x.Key == "ubiquiti");
        Assert.True(descriptor.ReadOnlyByDefault);
        Assert.True(descriptor.Capabilities.HasFlag(WispConnectorCapabilities.ReadClients));
    }
}
