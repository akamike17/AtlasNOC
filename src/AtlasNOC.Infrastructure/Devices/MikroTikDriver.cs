using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using AtlasNOC.Application.Devices;
using AtlasNOC.Application.Probes;

namespace AtlasNOC.Infrastructure.Devices;

/// <summary>
/// Driver MikroTik RouterOS vía API REST (solo-lectura). Devuelve DTOs neutrales;
/// las credenciales se inyectan por opciones y nunca se persisten aquí.
/// </summary>
public class MikroTikDriver : IDeviceDriver
{
    private readonly IHttpClientFactory _http;
    private readonly MikroTikOptions _options;

    public MikroTikDriver(IHttpClientFactory http, MikroTikOptions options)
    {
        _http = http;
        _options = options;
    }

    public string DriverKey => "mikrotik";

    public bool CanHandle(DeviceFingerprint fp)
    {
        var text = $"{fp.SysObjectId} {fp.SysDescription} {fp.SysName}".ToLowerInvariant();
        return text.Contains("mikrotik") || text.Contains("routeros")
            || (fp.SysObjectId?.Contains("1.3.6.1.4.1.14988") ?? false);
    }

    private HttpClient CreateClient() => _http.CreateClient("mikrotik");

    public async Task<DeviceIdentity> GetIdentityAsync(string ip, CancellationToken ct)
    {
        var client = CreateClient();
        try
        {
            var req = new HttpRequestMessage(HttpMethod.Get, $"https://{ip}/rest/system/resource");
            AddAuth(req);
            var resp = await client.SendAsync(req, ct);
            if (!resp.IsSuccessStatusCode) return new DeviceIdentity(ip, null, null, null, null);
            var data = await resp.Content.ReadFromJsonAsync<JsonElement>(cancellationToken: ct);
            return new DeviceIdentity(
                GetString(data, "board-name") ?? ip,
                GetString(data, "board-name"),
                GetString(data, "serial-number"),
                GetString(data, "version"),
                "1.3.6.1.4.1.14988");
        }
        catch
        {
            return new DeviceIdentity(ip, null, null, null, null);
        }
    }

    public async Task<IReadOnlyList<InterfaceData>> GetInterfacesAsync(string ip, CancellationToken ct)
    {
        var client = CreateClient();
        var result = new List<InterfaceData>();
        try
        {
            var req = new HttpRequestMessage(HttpMethod.Get, $"https://{ip}/rest/interface");
            AddAuth(req);
            var resp = await client.SendAsync(req, ct);
            if (!resp.IsSuccessStatusCode) return result;
            var items = await resp.Content.ReadFromJsonAsync<JsonElement>(cancellationToken: ct);
            var idx = 1;
            foreach (var it in items.EnumerateArray())
            {
                var name = GetString(it, "name") ?? $"if{idx}";
                var running = GetBool(it, "running");
                var disabled = GetBool(it, "disabled");
                var mac = GetString(it, "mac-address");
                var speed = GetLong(it, "actual-mtu") is { } mtu ? (ulong?)null : null;
                result.Add(new InterfaceData(
                    idx++, name, GetString(it, "comment"), mac, null,
                    disabled ? 0 : 1,
                    running ? 1 : 2,
                    speed ?? null, GetString(it, "type")));
            }
        }
        catch { /* read-only: swipe bajo error */ }
        return result;
    }

    public async Task<IReadOnlyList<NeighborData>> GetNeighborsAsync(string ip, CancellationToken ct)
    {
        var client = CreateClient();
        var result = new List<NeighborData>();
        try
        {
            // MikroTik neighbor discovery (MNDP) vía /ip/neighbor + interfaces.
            var req = new HttpRequestMessage(HttpMethod.Get, $"https://{ip}/rest/ip/neighbor");
            AddAuth(req);
            var resp = await client.SendAsync(req, ct);
            if (!resp.IsSuccessStatusCode) return result;
            var items = await resp.Content.ReadFromJsonAsync<JsonElement>(cancellationToken: ct);
            foreach (var it in items.EnumerateArray())
            {
                var remote = GetString(it, "identity") ?? GetString(it, "address");
                if (string.IsNullOrWhiteSpace(remote)) continue;
                var iface = GetString(it, "interface-name") ?? string.Empty;
                result.Add(new NeighborData(remote, null, iface, "mikrotik",
                    Hash($"{ip}:{remote}:{iface}")));
            }
        }
        catch { }
        return result;
    }

    public async Task<HealthData> GetHealthAsync(string ip, CancellationToken ct)
    {
        var client = CreateClient();
        try
        {
            var req = new HttpRequestMessage(HttpMethod.Get, $"https://{ip}/rest/system/resource");
            AddAuth(req);
            var resp = await client.SendAsync(req, ct);
            if (!resp.IsSuccessStatusCode) return new HealthData(null, null, null, null, null);
            var data = await resp.Content.ReadFromJsonAsync<JsonElement>(cancellationToken: ct);
            double? cpu = GetDouble(data, "cpu-load");
            double? mem = GetDouble(data, "free-memory-percent") is { } f ? 100 - f : null;
            long? uptime = GetLong(data, "uptime");
            return new HealthData(null, null, cpu, mem, uptime is { } u ? u / 1000 : null);
        }
        catch { return new HealthData(null, null, null, null, null); }
    }

    public async Task<IReadOnlyList<MetricDatum>> GetMetricsAsync(string ip, CancellationToken ct)
    {
        var h = await GetHealthAsync(ip, ct);
        var result = new List<MetricDatum>();
        if (h.CpuPercent.HasValue) result.Add(new MetricDatum("cpu_usage", h.CpuPercent.Value, "%"));
        if (h.MemoryPercent.HasValue) result.Add(new MetricDatum("memory_usage", h.MemoryPercent.Value, "%"));
        if (h.UptimeSeconds.HasValue) result.Add(new MetricDatum("uptime", h.UptimeSeconds.Value, "s"));
        return result;
    }

    public Task<IReadOnlyList<WirelessClientData>> GetWirelessAssociationsAsync(string ip, CancellationToken ct)
        => Task.FromResult<IReadOnlyList<WirelessClientData>>(Array.Empty<WirelessClientData>());

    private void AddAuth(HttpRequestMessage req)
    {
        if (string.IsNullOrWhiteSpace(_options.Username) || string.IsNullOrWhiteSpace(_options.Password))
            return;
        var token = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{_options.Username}:{_options.Password}"));
        req.Headers.Authorization = new AuthenticationHeaderValue("Basic", token);
    }

    private static string? GetString(JsonElement e, string prop)
        => e.TryGetProperty(prop, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;
    private static bool GetBool(JsonElement e, string prop)
        => e.TryGetProperty(prop, out var v) && v.ValueKind is JsonValueKind.True or JsonValueKind.False ? v.GetBoolean() : false;
    private static long? GetLong(JsonElement e, string prop)
        => e.TryGetProperty(prop, out var v) && v.ValueKind == JsonValueKind.Number ? (long?)v.GetInt64() : null;
    private static double? GetDouble(JsonElement e, string prop)
        => e.TryGetProperty(prop, out var v) && v.ValueKind == JsonValueKind.Number ? v.GetDouble() : null;

    private static string Hash(string input)
    {
        var bytes = System.Security.Cryptography.SHA256.HashData(Encoding.UTF8.GetBytes(input));
        return Convert.ToHexString(bytes)[..16];
    }
}

public sealed class MikroTikOptions
{
    public string? Username { get; set; }
    public string? Password { get; set; }
    public bool SkipCertificateValidation { get; set; } = true;
    public int TimeoutSeconds { get; set; } = 10;
}