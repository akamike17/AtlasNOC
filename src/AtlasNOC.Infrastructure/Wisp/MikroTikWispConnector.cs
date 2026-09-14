using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Net;
using System.Text;
using System.Text.Json;
using AtlasNOC.Application.Wisp;
using AtlasNOC.Infrastructure.Devices;
using Microsoft.Extensions.Logging;

namespace AtlasNOC.Infrastructure.Wisp;

/// <summary>Consulta sólo lectura de CPE presentes en la tabla ARP de RouterOS.</summary>
public sealed class MikroTikWispConnector : IWispConnector
{
    private readonly IHttpClientFactory _http;
    private readonly MikroTikOptions _options;
    private readonly ILogger<MikroTikWispConnector> _logger;

    public MikroTikWispConnector(IHttpClientFactory http, MikroTikOptions options,
        ILogger<MikroTikWispConnector> logger)
    {
        _http = http;
        _options = options;
        _logger = logger;
    }

    public WispConnectorDescriptor Descriptor { get; } = new(
        "mikrotik", "MikroTik RouterOS",
        WispConnectorCapabilities.ReadClients | WispConnectorCapabilities.ReadSessions | WispConnectorCapabilities.ReadTopology,
        RequiresCredentials: true, ReadOnlyByDefault: true);

    public bool IsConfigured => IsSafeManagementIp(_options.ManagementIp)
        && !string.IsNullOrWhiteSpace(_options.Username)
        && !string.IsNullOrWhiteSpace(_options.Password);

    public async Task<IReadOnlyList<WispClientEvidence>> ReadClientsAsync(CancellationToken ct = default)
    {
        if (!IsConfigured) return Array.Empty<WispClientEvidence>();

        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get,
                $"https://{_options.ManagementIp}/rest/ip/arp");
            var token = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{_options.Username}:{_options.Password}"));
            request.Headers.Authorization = new AuthenticationHeaderValue("Basic", token);
            var response = await _http.CreateClient("mikrotik").SendAsync(request, ct);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("MikroTik WISP connector devolvió HTTP {StatusCode} en lectura ARP", (int)response.StatusCode);
                return Array.Empty<WispClientEvidence>();
            }

            var payload = await response.Content.ReadFromJsonAsync<JsonElement>(cancellationToken: ct);
            var observed = DateTime.UtcNow;
            return payload.EnumerateArray()
                .Select(item => Evidence(item, observed))
                .Where(e => e is not null)
                .Cast<WispClientEvidence>()
                .ToList();
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "No se pudo leer la tabla ARP de MikroTik {ManagementIp}", _options.ManagementIp);
            return Array.Empty<WispClientEvidence>();
        }
    }

    private WispClientEvidence? Evidence(JsonElement item, DateTime observed)
    {
        var address = String(item, "address");
        var mac = String(item, "mac-address");
        if (string.IsNullOrWhiteSpace(address) || string.IsNullOrWhiteSpace(mac)) return null;
        return new WispClientEvidence(
            ExternalId: $"{_options.ManagementIp}:{mac.ToUpperInvariant()}",
            AccountReference: null,
            CpeAddress: address,
            SessionReference: String(item, "interface"),
            ObservedAtUtc: observed,
            Source: "mikrotik-arp",
            Confidence: 0.65);
    }

    private static string? String(JsonElement item, string property)
        => item.TryGetProperty(property, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString() : null;

    private static bool IsSafeManagementIp(string? value)
        => IPAddress.TryParse(value, out var address)
           && address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork
           && !IPAddress.IsLoopback(address)
           && !address.Equals(IPAddress.Any)
           && !address.Equals(IPAddress.Broadcast)
           && address.GetAddressBytes()[0] is < 224 or > 239;
}
