using System.Net;
using System.Net.Http;
using AtlasNOC.Infrastructure.Devices;
using AtlasNOC.Infrastructure.Wisp;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace AtlasNOC.Tests.Unit;

public sealed class MikroTikWispConnectorTests
{
    [Fact]
    public async Task Read_clients_returns_arp_evidence_without_mutating_router()
    {
        var handler = new StubHandler("[{\"address\":\"192.168.88.10\",\"mac-address\":\"AA:BB:CC:DD:EE:FF\",\"interface\":\"bridge\"}]");
        var factory = new StubHttpClientFactory(new HttpClient(handler));
        var options = new MikroTikOptions { ManagementIp = "192.168.88.1", Username = "reader", Password = "secret" };
        var connector = new MikroTikWispConnector(factory, options, NullLogger<MikroTikWispConnector>.Instance);

        var evidence = await connector.ReadClientsAsync();

        var item = Assert.Single(evidence);
        Assert.Equal("192.168.88.10", item.CpeAddress);
        Assert.Equal("mikrotik-arp", item.Source);
        Assert.Equal(0.65, item.Confidence);
        Assert.Equal(HttpMethod.Get, handler.Method);
        Assert.Contains("/rest/ip/arp", handler.RequestUri!.AbsolutePath);
    }

    [Fact]
    public void Hostname_is_not_accepted_as_management_endpoint()
    {
        var connector = new MikroTikWispConnector(
            new StubHttpClientFactory(new HttpClient()),
            new MikroTikOptions { ManagementIp = "localhost", Username = "reader", Password = "secret" },
            NullLogger<MikroTikWispConnector>.Instance);

        Assert.False(connector.IsConfigured);
    }

    [Fact]
    public void Loopback_is_not_accepted_as_management_endpoint()
    {
        var connector = new MikroTikWispConnector(
            new StubHttpClientFactory(new HttpClient()),
            new MikroTikOptions { ManagementIp = "127.0.0.1", Username = "reader", Password = "secret" },
            NullLogger<MikroTikWispConnector>.Instance);

        Assert.False(connector.IsConfigured);
    }

    private sealed class StubHttpClientFactory(HttpClient client) : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => client;
    }

    private sealed class StubHandler(string json) : HttpMessageHandler
    {
        public HttpMethod? Method { get; private set; }
        public Uri? RequestUri { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Method = request.Method;
            RequestUri = request.RequestUri;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(json)
            });
        }
    }
}
