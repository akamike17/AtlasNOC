using System.Net;
using System.Net.Http;
using AtlasNOC.Infrastructure.Devices;
using AtlasNOC.Infrastructure.Wisp;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace AtlasNOC.Tests.Unit;

public sealed class UbiquitiWispConnectorTests
{
    [Fact]
    public async Task Read_clients_returns_association_evidence_using_get_only()
    {
        var handler = new StubHandler("{\"data\":[{\"mac\":\"aa:bb:cc:dd:ee:ff\",\"hostname\":\"cpe-1\",\"ap_mac\":\"11:22:33:44:55:66\"}]}");
        var connector = new UbiquitiWispConnector(
            new StubHttpClientFactory(new HttpClient(handler)),
            new UbiquitiOptions { ControllerUrl = "https://192.0.2.20", Site = "default", ApiKey = "secret" },
            NullLogger<UbiquitiWispConnector>.Instance);

        var evidence = await connector.ReadClientsAsync();

        var item = Assert.Single(evidence);
        Assert.Equal("AA:BB:CC:DD:EE:FF", item.ExternalId);
        Assert.Equal("cpe-1", item.AccountReference);
        Assert.Equal("11:22:33:44:55:66", item.SessionReference);
        Assert.Equal("ubiquiti-unifi", item.Source);
        Assert.Equal(HttpMethod.Get, handler.Method);
    }

    [Fact]
    public void Missing_api_key_leaves_connector_unconfigured()
    {
        var connector = new UbiquitiWispConnector(
            new StubHttpClientFactory(new HttpClient()),
            new UbiquitiOptions { ControllerUrl = "https://192.0.2.20", Site = "default" },
            NullLogger<UbiquitiWispConnector>.Instance);

        Assert.False(connector.IsConfigured);
    }

    [Fact]
    public void Loopback_controller_is_not_accepted()
    {
        var connector = new UbiquitiWispConnector(
            new StubHttpClientFactory(new HttpClient()),
            new UbiquitiOptions { ControllerUrl = "https://127.0.0.1", Site = "default", ApiKey = "secret" },
            NullLogger<UbiquitiWispConnector>.Instance);

        Assert.False(connector.IsConfigured);
    }

    [Fact]
    public void Http_controller_is_rejected_unless_explicitly_allowed()
    {
        var connector = new UbiquitiWispConnector(
            new StubHttpClientFactory(new HttpClient()),
            new UbiquitiOptions { ControllerUrl = "http://192.0.2.20", Site = "default", ApiKey = "secret" },
            NullLogger<UbiquitiWispConnector>.Instance);

        Assert.False(connector.IsConfigured);
    }

    private sealed class StubHttpClientFactory(HttpClient client) : IHttpClientFactory
    { public HttpClient CreateClient(string name) => client; }

    private sealed class StubHandler(string json) : HttpMessageHandler
    {
        public HttpMethod? Method { get; private set; }
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Method = request.Method;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(json) });
        }
    }
}
