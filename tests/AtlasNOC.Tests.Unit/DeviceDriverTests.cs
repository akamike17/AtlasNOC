using AtlasNOC.Application.Probes;
using AtlasNOC.Infrastructure.Devices;
using Microsoft.Extensions.DependencyInjection;
using System.Net;
using System.Text;
using AtlasNOC.Application.Services;
using AtlasNOC.Domain.Enums;
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
        Assert.Equal("ubiquiti-unifi", Ub().DriverKey);
    }

    [Fact]
    public void MikroTik_driver_rejects_unrelated_fingerprint()
    {
        var fp = new DeviceFingerprint("10.0.0.3", "sw1", "1.3.6.1.4.1.9.1", "Cisco IOS", null);
        Assert.False(Mk().CanHandle(fp));
        Assert.False(Ub().CanHandle(fp));
    }

    [Theory]
    [InlineData("1w2d03:04:05", 788645L)]
    [InlineData("3d12:34:56", 304496L)]
    [InlineData("12:34:56", 45296L)]
    public void RouterOs_uptime_text_is_parsed(string value, long expected)
        => Assert.Equal(expected, MikroTikDriver.ParseRouterOsDuration(value));

    [Theory]
    [InlineData("*1", 1)]
    [InlineData("*A", 10)]
    [InlineData("*10", 16)]
    public void RouterOs_interface_id_is_stable_hex(string value, int expected)
        => Assert.Equal(expected, MikroTikDriver.ParseInterfaceId(value));

    [Theory]
    [InlineData("1Gbps", 1_000_000_000UL)]
    [InlineData("100Mbps", 100_000_000UL)]
    public void RouterOs_rate_is_converted_to_bps(string value, ulong expected)
        => Assert.Equal(expected, MikroTikDriver.ParseRate(value));

    [Fact]
    public async Task RouterOs_interfaces_use_real_id_and_disabled_state()
    {
        const string json = """
            [{".id":"*A","name":"ether1","type":"ether","running":"false","disabled":"true","mac-address":"00:11:22:33:44:55","speed":"1Gbps"}]
            """;
        var driver = new MikroTikDriver(new ResponseFactory(HttpStatusCode.OK, json), new MikroTikOptions());

        var item = Assert.Single(await driver.GetInterfacesAsync("192.0.2.1", CancellationToken.None));

        Assert.Equal(10, item.IfIndex);
        Assert.Equal(2, item.AdminStatus);
        Assert.Equal(2, item.OperStatus);
        Assert.Equal(1_000_000_000UL, item.SpeedBps);
    }

    [Theory]
    [InlineData(HttpStatusCode.Unauthorized)]
    [InlineData(HttpStatusCode.Forbidden)]
    [InlineData(HttpStatusCode.NotFound)]
    public async Task RouterOs_http_errors_do_not_fabricate_identity(HttpStatusCode status)
    {
        var driver = new MikroTikDriver(new ResponseFactory(status, "{}"), new MikroTikOptions());
        var identity = await driver.GetIdentityAsync("192.0.2.1", CancellationToken.None);
        Assert.Equal("192.0.2.1", identity.Hostname);
        Assert.Null(identity.Model);
    }

    [Fact]
    public async Task RouterOs_uses_execution_credential_for_basic_auth()
    {
        var factory = new ResponseFactory(HttpStatusCode.OK, "{\"board-name\":\"RB5009\"}");
        var driver = new MikroTikDriver(factory, new MikroTikOptions());
        var credential = new ResolvedDeviceCredential(Guid.NewGuid(), "router", SnmpVersion.V3,
            null, "api-user", null, "api-pass", null, null);

        await driver.GetIdentityAsync("192.0.2.1", credential, CancellationToken.None);

        Assert.Equal("Basic", factory.LastAuthorizationScheme);
        Assert.Equal(Convert.ToBase64String(Encoding.UTF8.GetBytes("api-user:api-pass")), factory.LastAuthorizationParameter);
    }

    [Fact]
    public async Task RouterOs_timeout_propagates_cancellation()
    {
        var driver = new MikroTikDriver(new ResponseFactory(TimeSpan.FromSeconds(5)), new MikroTikOptions());
        using var cancellation = new CancellationTokenSource(TimeSpan.FromMilliseconds(30));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            driver.GetInterfacesAsync("192.0.2.1", cancellation.Token));
    }

    [Fact]
    public async Task RouterOs_incomplete_json_does_not_create_fake_interfaces()
    {
        var driver = new MikroTikDriver(new ResponseFactory(HttpStatusCode.OK, "{}"), new MikroTikOptions());
        Assert.Empty(await driver.GetInterfacesAsync("192.0.2.1", CancellationToken.None));
    }

    [Fact]
    public void RouterOs_certificate_bypass_is_disabled_by_default()
        => Assert.False(new MikroTikOptions().SkipCertificateValidation);

    [Fact]
    public async Task UniFi_modern_envelope_finds_device_by_ip_and_parses_ports()
    {
        const string json = """
        {"data":[
          {"_id":"other","ip":"192.0.2.2","name":"other"},
          {"_id":"abc","ip":"192.0.2.10","mac":"aa:bb:cc:dd:ee:ff","name":"ap-office","model":"U6-Pro","version":"7.0",
           "port_table":[{"port_idx":3,"name":"eth0","up":true,"enabled":true,"speed":1000,"type":"ethernet"}],
           "uplink":{"name":"eth0","remote_name":"core-sw","remote_port":"Gi1/0/3"}}
        ]}
        """;
        var factory = new ResponseFactory(HttpStatusCode.OK, json);
        var driver = new UbiquitiDriver(factory, new UbiquitiOptions { ControllerUrl = "https://unifi.local" });

        var identity = await driver.GetIdentityAsync("192.0.2.10", CancellationToken.None);
        var port = Assert.Single(await driver.GetInterfacesAsync("192.0.2.10", CancellationToken.None));
        var neighbor = Assert.Single(await driver.GetNeighborsAsync("192.0.2.10", CancellationToken.None));

        Assert.Equal("ap-office", identity.Hostname);
        Assert.Equal(3, port.IfIndex);
        Assert.Equal(1_000_000_000UL, port.SpeedBps);
        Assert.Equal("core-sw", neighbor.RemoteIdentity);
        Assert.Equal("/proxy/network/api/s/default/stat/device", factory.LastPath);
    }

    [Fact]
    public async Task UniFi_empty_or_missing_device_does_not_fabricate_data()
    {
        var driver = new UbiquitiDriver(new ResponseFactory(HttpStatusCode.OK, "{\"data\":[]}"),
            new UbiquitiOptions { ControllerUrl = "https://unifi.local" });
        var identity = await driver.GetIdentityAsync("192.0.2.10", CancellationToken.None);
        Assert.Equal("192.0.2.10", identity.Hostname);
        Assert.Empty(await driver.GetInterfacesAsync("192.0.2.10", CancellationToken.None));
    }

    [Fact]
    public async Task UniFi_auth_failure_returns_unknown_identity()
    {
        var driver = new UbiquitiDriver(new ResponseFactory(HttpStatusCode.Unauthorized, "{}"),
            new UbiquitiOptions { ControllerUrl = "https://unifi.local" });
        Assert.Null((await driver.GetIdentityAsync("192.0.2.10", CancellationToken.None)).Model);
    }

    [Fact]
    public async Task UniFi_wireless_client_fixture_is_parsed()
    {
        const string json = "{\"data\":[{\"mac\":\"00:11:22:33:44:55\",\"hostname\":\"phone\",\"signal\":-55,\"noise\":-95,\"tx_rate\":300000000,\"rx_rate\":200000000,\"ap_mac\":\"aa:bb\"}]}";
        var driver = new UbiquitiDriver(new ResponseFactory(HttpStatusCode.OK, json),
            new UbiquitiOptions { ControllerUrl = "https://unifi.local" });
        var client = Assert.Single(await driver.GetWirelessAssociationsAsync("192.0.2.10", CancellationToken.None));
        Assert.Equal("phone", client.CpeName);
        Assert.Equal(40, client.Snr);
        Assert.Equal(300, client.TxRateMbps);
    }

    [Fact]
    public async Task UniFi_execution_api_key_is_sent_to_controller()
    {
        var factory = new ResponseFactory(HttpStatusCode.OK, "{\"data\":[]}");
        var driver = new UbiquitiDriver(factory, new UbiquitiOptions { ControllerUrl = "https://unifi.local" });
        var credential = new ResolvedDeviceCredential(Guid.NewGuid(), "unifi", SnmpVersion.V3, null, "unused", null, "secret-key", null, null);
        await driver.GetIdentityAsync("192.0.2.10", credential, CancellationToken.None);
        Assert.Equal("secret-key", factory.LastApiKey);
    }

    private sealed class TestHttpFactory : IHttpClientFactory
    {
        public System.Net.Http.HttpClient CreateClient(string name) => new();
    }

    private sealed class ResponseFactory : IHttpClientFactory
    {
        private readonly HttpClient _client;
        public string? LastAuthorizationScheme { get; private set; }
        public string? LastAuthorizationParameter { get; private set; }
        public string? LastApiKey { get; private set; }
        public string? LastPath { get; private set; }

        public ResponseFactory(HttpStatusCode status, string json)
        {
            _client = new HttpClient(new DelegateHandler(async (request, _) =>
            {
                LastAuthorizationScheme = request.Headers.Authorization?.Scheme;
                LastAuthorizationParameter = request.Headers.Authorization?.Parameter;
                LastApiKey = request.Headers.TryGetValues("X-API-Key", out var keys) ? keys.Single() : null;
                LastPath = request.RequestUri?.AbsolutePath;
                await Task.CompletedTask;
                return new HttpResponseMessage(status) { Content = new StringContent(json, Encoding.UTF8, "application/json") };
            }));
        }

        public ResponseFactory(TimeSpan delay)
            => _client = new HttpClient(new DelegateHandler(async (_, ct) =>
            {
                await Task.Delay(delay, ct);
                return new HttpResponseMessage(HttpStatusCode.OK);
            }));

        public HttpClient CreateClient(string name) => _client;
    }

    private sealed class DelegateHandler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> callback) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            => callback(request, cancellationToken);
    }
}
