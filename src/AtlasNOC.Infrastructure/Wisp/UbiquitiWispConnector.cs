using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Net;
using System.Text.Json;
using AtlasNOC.Application.Wisp;
using AtlasNOC.Infrastructure.Devices;
using Microsoft.Extensions.Logging;

namespace AtlasNOC.Infrastructure.Wisp;

/// <summary>Consulta sólo lectura de clientes asociados desde UniFi Network.</summary>
public sealed class UbiquitiWispConnector : IWispConnector
{
    private readonly IHttpClientFactory _http;
    private readonly UbiquitiOptions _options;
    private readonly ILogger<UbiquitiWispConnector> _logger;

    public UbiquitiWispConnector(IHttpClientFactory http, UbiquitiOptions options,
        ILogger<UbiquitiWispConnector> logger)
    { _http = http; _options = options; _logger = logger; }

    public WispConnectorDescriptor Descriptor { get; } = new(
        "ubiquiti", "Ubiquiti UniFi",
        WispConnectorCapabilities.ReadClients | WispConnectorCapabilities.ReadTopology,
        RequiresCredentials: true, ReadOnlyByDefault: true);

    public bool IsConfigured => Uri.TryCreate(_options.ControllerUrl, UriKind.Absolute, out var uri)
        && (uri.Scheme == "https" || (uri.Scheme == "http" && _options.AllowInsecureHttp))
        && IsAllowedControllerHost(uri.Host)
        && !string.IsNullOrWhiteSpace(_options.Site)
        && !string.IsNullOrWhiteSpace(_options.ApiKey);

    public async Task<IReadOnlyList<WispClientEvidence>> ReadClientsAsync(CancellationToken ct = default)
    {
        if (!IsConfigured) return Array.Empty<WispClientEvidence>();
        try
        {
            var client = _http.CreateClient("ubiquiti");
            if (client.BaseAddress is null) client.BaseAddress = new Uri(_options.ControllerUrl!);
            var prefix = _options.UseUniFiOs ? "/proxy/network" : string.Empty;
            var path = $"{prefix}/api/s/{Uri.EscapeDataString(_options.Site)}/stat/sta";
            using var request = new HttpRequestMessage(HttpMethod.Get, path);
            request.Headers.TryAddWithoutValidation("X-API-Key", _options.ApiKey);
            using var response = await client.SendAsync(request, ct);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("UniFi WISP connector devolvió HTTP {StatusCode} en lectura de clientes", (int)response.StatusCode);
                return Array.Empty<WispClientEvidence>();
            }
            var payload = await response.Content.ReadFromJsonAsync<JsonElement>(cancellationToken: ct);
            var observed = DateTime.UtcNow;
            return payload.TryGetProperty("data", out var data) && data.ValueKind == JsonValueKind.Array
                ? data.EnumerateArray().Select(x => Evidence(x, observed)).Where(x => x is not null).Cast<WispClientEvidence>().ToList()
                : Array.Empty<WispClientEvidence>();
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
        catch (Exception ex) { _logger.LogWarning(ex, "No se pudieron leer clientes UniFi"); return Array.Empty<WispClientEvidence>(); }
    }

    private static WispClientEvidence? Evidence(JsonElement item, DateTime observed)
    {
        var mac = String(item, "mac");
        if (string.IsNullOrWhiteSpace(mac)) return null;
        return new WispClientEvidence(mac.ToUpperInvariant(), String(item, "hostname"), null,
            String(item, "ap_mac"), observed, "ubiquiti-unifi", 0.8);
    }

    private static string? String(JsonElement item, string property)
        => item.TryGetProperty(property, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;

    private static bool IsAllowedControllerHost(string host)
    {
        if (!IPAddress.TryParse(host, out var address)) return !string.IsNullOrWhiteSpace(host);
        if (IPAddress.IsLoopback(address) || address.Equals(IPAddress.Any)
            || address.Equals(IPAddress.Broadcast)) return false;
        var bytes = address.GetAddressBytes();
        return address.AddressFamily != System.Net.Sockets.AddressFamily.InterNetwork
            || bytes[0] is < 224 or > 239;
    }
}
