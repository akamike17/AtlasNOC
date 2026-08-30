using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using AtlasNOC.Application.Devices;
using AtlasNOC.Application.Probes;

namespace AtlasNOC.Infrastructure.Devices;

/// <summary>
/// Driver Ubiquiti UniFi vía API del UniFi Controller (solo-lectura).
/// Devuelve DTOs neutrales; no expone objetos UniFi específicos a la capa de negocio.
/// </summary>
public class UbiquitiDriver : IDeviceDriver
{
    private readonly IHttpClientFactory _http;
    private readonly UbiquitiOptions _options;

    public UbiquitiDriver(IHttpClientFactory http, UbiquitiOptions options)
    {
        _http = http;
        _options = options;
    }

    public string DriverKey => "ubiquiti";

    public bool CanHandle(DeviceFingerprint fp)
    {
        var text = $"{fp.SysObjectId} {fp.SysDescription} {fp.SysName}".ToLowerInvariant();
        return text.Contains("ubiquiti") || text.Contains("unifi") || text.Contains("airmax")
            || (fp.SysObjectId?.Contains("1.3.6.1.4.1.41112") ?? false);
    }

    private HttpClient CreateClient()
    {
        var client = _http.CreateClient("ubiquiti");
        client.BaseAddress = new Uri(_options.ControllerUrl ??
            throw new InvalidOperationException("Ubiquiti controller URL is not configured."));
        return client;
    }

    public async Task<DeviceIdentity> GetIdentityAsync(string ip, CancellationToken ct)
    {
        var client = CreateClient();
        try
        {
            var resp = await client.GetAsync($"/api/s/default/stat/device/{ip}", ct);
            if (!resp.IsSuccessStatusCode) return new DeviceIdentity(ip, null, null, null, null);
            var data = await resp.Content.ReadFromJsonAsync<JsonElement>(cancellationToken: ct);
            var root = GetObject(data, "data");
            var first = root.EnumerateArray().FirstOrDefault();
            return new DeviceIdentity(
                GetString(first, "name") ?? GetString(first, "hostname") ?? ip,
                GetString(first, "model"),
                GetString(first, "serial"),
                GetString(first, "version"),
                "1.3.6.1.4.1.41112");
        }
        catch { return new DeviceIdentity(ip, null, null, null, null); }
    }

    public async Task<IReadOnlyList<InterfaceData>> GetInterfacesAsync(string ip, CancellationToken ct)
    {
        var client = CreateClient();
        var result = new List<InterfaceData>();
        try
        {
            var resp = await client.GetAsync($"/api/s/default/stat/device/{ip}", ct);
            if (!resp.IsSuccessStatusCode) return result;
            var data = await resp.Content.ReadFromJsonAsync<JsonElement>(cancellationToken: ct);
            var root = GetObject(data, "data");
            foreach (var d in root.EnumerateArray())
            {
                var idx = GetInt(d, "sys_stats.ifindex") ?? 0;
                var name = GetString(d, "name") ?? GetString(d, "hostname") ?? $"if{idx}";
                var mac = GetString(d, "mac");
                var up = GetString(d, "state") == "1" || GetString(d, "up") == "1";
                var speed = GetLong(d, "uplink.speed") ?? GetLong(d, "sys_stats.max_speed");
                result.Add(new InterfaceData(idx, name, null, mac, null, up ? 1 : 0, up ? 1 : 2, (ulong?)speed, null));
            }
        }
        catch { }
        return result;
    }

    public async Task<IReadOnlyList<NeighborData>> GetNeighborsAsync(string ip, CancellationToken ct)
    {
        // UniFi "insights" de radio/uplink.
        var client = CreateClient();
        var result = new List<NeighborData>();
        try
        {
            var resp = await client.GetAsync($"/api/s/default/stat/device/{ip}", ct);
            if (!resp.IsSuccessStatusCode) return result;
            var data = await resp.Content.ReadFromJsonAsync<JsonElement>(cancellationToken: ct);
            var root = GetObject(data, "data");
            foreach (var d in root.EnumerateArray())
            {
                var uplinkMac = GetString(d, "uplink.mac");
                var uplinkName = GetString(d, "uplink.remote_name");
                if (string.IsNullOrWhiteSpace(uplinkMac) && string.IsNullOrWhiteSpace(uplinkName)) continue;
                var remote = uplinkName ?? uplinkMac ?? string.Empty;
                result.Add(new NeighborData(remote, null, GetString(d, "name") ?? "uplink", "ubiquiti",
                    Hash($"{ip}:{remote}")));
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
            var resp = await client.GetAsync($"/api/s/default/stat/device/{ip}", ct);
            if (!resp.IsSuccessStatusCode) return new HealthData(null, null, null, null, null);
            var data = await resp.Content.ReadFromJsonAsync<JsonElement>(cancellationToken: ct);
            var root = GetObject(data, "data");
            var d = root.EnumerateArray().FirstOrDefault();
            double? cpu = GetDouble(d, "sys_stats.cpu");
            double? mem = GetDouble(d, "sys_stats.mem_used");
            double? uptime = GetDouble(d, "uptime");
            return new HealthData(null, null, cpu, mem, uptime is { } u ? (long)u : null);
        }
        catch { return new HealthData(null, null, null, null, null); }
    }

    public async Task<IReadOnlyList<MetricDatum>> GetMetricsAsync(string ip, CancellationToken ct)
    {
        var h = await GetHealthAsync(ip, ct);
        var result = new List<MetricDatum>();
        if (h.CpuPercent.HasValue) result.Add(new MetricDatum("cpu_usage", h.CpuPercent.Value, "%"));
        if (h.MemoryPercent.HasValue) result.Add(new MetricDatum("memory_usage", h.MemoryPercent.Value, "%"));
        return result;
    }

    public async Task<IReadOnlyList<WirelessClientData>> GetWirelessAssociationsAsync(string ip, CancellationToken ct)
    {
        var client = CreateClient();
        var result = new List<WirelessClientData>();
        try
        {
            var resp = await client.GetAsync("/api/s/default/stat/sta", ct);
            if (!resp.IsSuccessStatusCode) return result;
            var data = await resp.Content.ReadFromJsonAsync<JsonElement>(cancellationToken: ct);
            var root = GetObject(data, "data");
            foreach (var s in root.EnumerateArray())
            {
                result.Add(new WirelessClientData(
                    GetString(s, "mac") ?? string.Empty,
                    GetString(s, "hostname") ?? GetString(s, "name"),
                    GetDouble(s, "rssi") is { } rssi ? rssi : null,
                    GetDouble(s, "noise") is { } noise ? noise : null,
                    GetDouble(s, "snr") is { } snr ? snr : null,
                    GetDouble(s, "tx_rate") is { } tx ? tx / 1_000_000 : null,
                    GetDouble(s, "rx_rate") is { } rx ? rx / 1_000_000 : null,
                    GetString(s, "ap_mac") ?? GetString(s, "essid")));
            }
        }
        catch { }
        return result;
    }

    private void AddAuth(HttpRequestMessage req)
    {
        if (string.IsNullOrWhiteSpace(_options.Username) || string.IsNullOrWhiteSpace(_options.Password))
            return;
        var token = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{_options.Username}:{_options.Password}"));
        req.Headers.Authorization = new AuthenticationHeaderValue("Basic", token);
    }

    private static JsonElement GetObject(JsonElement e, string prop)
        => e.TryGetProperty(prop, out var v) && v.ValueKind == JsonValueKind.Object ? v : default;

    private static string? GetString(JsonElement e, string path)
    {
        if (e.ValueKind == JsonValueKind.Undefined) return null;
        var current = e;
        foreach (var part in path.Split('.'))
        {
            if (current.ValueKind != JsonValueKind.Object || !current.TryGetProperty(part, out current))
                return null;
        }
        return current.ValueKind == JsonValueKind.String ? current.GetString() : null;
    }

    private static long? GetLong(JsonElement e, string path) => TryNum(e, path) is { } v && v.ValueKind == JsonValueKind.Number ? v.GetInt64() : null;
    private static double? GetDouble(JsonElement e, string path) => TryNum(e, path) is { } v && v.ValueKind == JsonValueKind.Number ? v.GetDouble() : null;
    private static int? GetInt(JsonElement e, string path) => TryNum(e, path) is { } v && v.ValueKind == JsonValueKind.Number ? v.GetInt32() : null;

    private static JsonElement? TryNum(JsonElement e, string path)
    {
        if (e.ValueKind == JsonValueKind.Undefined) return null;
        var current = e;
        foreach (var part in path.Split('.'))
        {
            if (current.ValueKind != JsonValueKind.Object || !current.TryGetProperty(part, out current))
                return null;
        }
        return current;
    }

    private static string Hash(string input)
    {
        var bytes = System.Security.Cryptography.SHA256.HashData(Encoding.UTF8.GetBytes(input));
        return Convert.ToHexString(bytes)[..16];
    }
}

public sealed class UbiquitiOptions
{
    public string? ControllerUrl { get; set; }
    public string? Username { get; set; }
    public string? Password { get; set; }
    public int TimeoutSeconds { get; set; } = 10;
}