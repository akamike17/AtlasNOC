using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using AtlasNOC.Application.Devices;
using AtlasNOC.Application.Probes;
using AtlasNOC.Application.Services;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace AtlasNOC.Infrastructure.Devices;

/// <summary>
/// Driver MikroTik RouterOS vía API REST (solo-lectura). Devuelve DTOs neutrales;
/// las credenciales se inyectan por opciones y nunca se persisten aquí.
/// </summary>
public class MikroTikDriver : IDeviceDriver, IDeviceCredentialAwareDriver
{
    private readonly IHttpClientFactory _http;
    private readonly MikroTikOptions _options;
    private readonly ILogger<MikroTikDriver> _logger;

    public MikroTikDriver(IHttpClientFactory http, MikroTikOptions options, ILogger<MikroTikDriver>? logger = null)
    {
        _http = http;
        _options = options;
        _logger = logger ?? NullLogger<MikroTikDriver>.Instance;
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
        => await GetIdentityCoreAsync(ip, _options.Username, _options.Password, ct);

    public Task<DeviceIdentity> GetIdentityAsync(string ip, ResolvedDeviceCredential credential, CancellationToken ct)
        => GetIdentityCoreAsync(ip, credential.UserName, credential.AuthPassword, ct);

    private async Task<DeviceIdentity> GetIdentityCoreAsync(string ip, string? username, string? password, CancellationToken ct)
    {
        var client = CreateClient();
        try
        {
            var req = new HttpRequestMessage(HttpMethod.Get, $"https://{ip}/rest/system/resource");
            AddAuth(req, username, password);
            var resp = await client.SendAsync(req, ct);
            if (!resp.IsSuccessStatusCode)
            {
                _logger.LogWarning("RouterOS {Ip} devolvió HTTP {StatusCode} al consultar identidad", ip, (int)resp.StatusCode);
                return new DeviceIdentity(ip, null, null, null, null);
            }
            var data = await resp.Content.ReadFromJsonAsync<JsonElement>(cancellationToken: ct);
            return new DeviceIdentity(
                GetString(data, "board-name") ?? ip,
                GetString(data, "board-name"),
                GetString(data, "serial-number"),
                GetString(data, "version"),
                "1.3.6.1.4.1.14988");
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "No se pudo consultar identidad RouterOS en {Ip}", ip);
            return new DeviceIdentity(ip, null, null, null, null);
        }
    }

    public async Task<IReadOnlyList<InterfaceData>> GetInterfacesAsync(string ip, CancellationToken ct)
        => await GetInterfacesCoreAsync(ip, _options.Username, _options.Password, ct);

    public Task<IReadOnlyList<InterfaceData>> GetInterfacesAsync(string ip, ResolvedDeviceCredential credential, CancellationToken ct)
        => GetInterfacesCoreAsync(ip, credential.UserName, credential.AuthPassword, ct);

    private async Task<IReadOnlyList<InterfaceData>> GetInterfacesCoreAsync(string ip, string? username, string? password, CancellationToken ct)
    {
        var client = CreateClient();
        var result = new List<InterfaceData>();
        try
        {
            var req = new HttpRequestMessage(HttpMethod.Get, $"https://{ip}/rest/interface");
            AddAuth(req, username, password);
            var resp = await client.SendAsync(req, ct);
            if (!resp.IsSuccessStatusCode)
            {
                _logger.LogWarning("RouterOS {Ip} devolvió HTTP {StatusCode} al consultar interfaces", ip, (int)resp.StatusCode);
                return result;
            }
            var items = await resp.Content.ReadFromJsonAsync<JsonElement>(cancellationToken: ct);
            foreach (var it in items.EnumerateArray())
            {
                var index = ParseInterfaceId(GetString(it, ".id"));
                if (!index.HasValue) continue;
                var name = GetString(it, "name") ?? $"if{index.Value}";
                var running = GetBool(it, "running");
                var disabled = GetBool(it, "disabled");
                var mac = GetString(it, "mac-address");
                var speed = ParseRate(GetString(it, "speed") ?? GetString(it, "rate"));
                result.Add(new InterfaceData(
                    index.Value, name, GetString(it, "comment"), mac, null,
                    disabled ? 2 : 1,
                    running ? 1 : 2,
                    speed, GetString(it, "type")));
            }
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
        catch (Exception ex) { _logger.LogWarning(ex, "No se pudieron consultar interfaces RouterOS en {Ip}", ip); }
        return result;
    }

    public async Task<IReadOnlyList<NeighborData>> GetNeighborsAsync(string ip, CancellationToken ct)
        => await GetNeighborsCoreAsync(ip, _options.Username, _options.Password, ct);

    public Task<IReadOnlyList<NeighborData>> GetNeighborsAsync(string ip, ResolvedDeviceCredential credential, CancellationToken ct)
        => GetNeighborsCoreAsync(ip, credential.UserName, credential.AuthPassword, ct);

    private async Task<IReadOnlyList<NeighborData>> GetNeighborsCoreAsync(string ip, string? username, string? password, CancellationToken ct)
    {
        var client = CreateClient();
        var result = new List<NeighborData>();
        try
        {
            // MikroTik neighbor discovery (MNDP) vía /ip/neighbor + interfaces.
            var req = new HttpRequestMessage(HttpMethod.Get, $"https://{ip}/rest/ip/neighbor");
            AddAuth(req, username, password);
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
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
        catch (Exception ex) { _logger.LogWarning(ex, "No se pudieron consultar vecinos RouterOS en {Ip}", ip); }
        return result;
    }

    public async Task<HealthData> GetHealthAsync(string ip, CancellationToken ct)
    {
        var client = CreateClient();
        try
        {
            var req = new HttpRequestMessage(HttpMethod.Get, $"https://{ip}/rest/system/resource");
            AddAuth(req, _options.Username, _options.Password);
            var resp = await client.SendAsync(req, ct);
            if (!resp.IsSuccessStatusCode) return new HealthData(null, null, null, null, null);
            var data = await resp.Content.ReadFromJsonAsync<JsonElement>(cancellationToken: ct);
            double? cpu = GetDouble(data, "cpu-load");
            double? mem = GetDouble(data, "free-memory-percent") is { } f ? 100 - f : null;
            long? uptime = ParseRouterOsDuration(GetString(data, "uptime"));
            return new HealthData(null, null, cpu, mem, uptime);
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

    private static void AddAuth(HttpRequestMessage req, string? username, string? password)
    {
        if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password))
            return;
        var token = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{username}:{password}"));
        req.Headers.Authorization = new AuthenticationHeaderValue("Basic", token);
    }

    private static string? GetString(JsonElement e, string prop)
        => e.TryGetProperty(prop, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;
    private static bool GetBool(JsonElement e, string prop)
        => e.TryGetProperty(prop, out var v) && (v.ValueKind switch
        {
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            JsonValueKind.String => bool.TryParse(v.GetString(), out var parsed) && parsed,
            _ => false
        });
    private static long? GetLong(JsonElement e, string prop)
        => e.TryGetProperty(prop, out var v) && v.ValueKind == JsonValueKind.Number ? (long?)v.GetInt64() : null;
    private static double? GetDouble(JsonElement e, string prop)
        => e.TryGetProperty(prop, out var v) && (v.ValueKind == JsonValueKind.Number || v.ValueKind == JsonValueKind.String)
            && double.TryParse(v.ToString(), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var value) ? value : null;

    internal static int? ParseInterfaceId(string? id)
    {
        if (string.IsNullOrWhiteSpace(id)) return null;
        var value = id.TrimStart('*');
        return int.TryParse(value, System.Globalization.NumberStyles.HexNumber,
            System.Globalization.CultureInfo.InvariantCulture, out var parsed) && parsed > 0 ? parsed : null;
    }

    internal static ulong? ParseRate(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        var normalized = value.Trim().ToLowerInvariant().Replace(" ", string.Empty);
        var multiplier = normalized.EndsWith("gbps") ? 1_000_000_000UL
            : normalized.EndsWith("mbps") ? 1_000_000UL
            : normalized.EndsWith("kbps") ? 1_000UL
            : normalized.EndsWith("bps") ? 1UL : 0UL;
        if (multiplier == 0) return null;
        var number = normalized[..normalized.IndexOf("bps", StringComparison.Ordinal)].TrimEnd('g', 'm', 'k');
        return decimal.TryParse(number, System.Globalization.NumberStyles.Float,
            System.Globalization.CultureInfo.InvariantCulture, out var parsed)
            ? checked((ulong)(parsed * multiplier)) : null;
    }

    internal static long? ParseRouterOsDuration(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        var remaining = value.Trim().ToLowerInvariant();
        long total = 0;
        foreach (var unit in new[] { ('w', 604800L), ('d', 86400L) })
        {
            var position = remaining.IndexOf(unit.Item1);
            if (position < 0) continue;
            if (!long.TryParse(remaining[..position], out var amount)) return null;
            total = checked(total + amount * unit.Item2);
            remaining = remaining[(position + 1)..];
        }
        if (TimeSpan.TryParseExact(remaining, new[] { @"h\:mm\:ss", @"hh\:mm\:ss" },
            System.Globalization.CultureInfo.InvariantCulture, out var time))
            return checked(total + (long)time.TotalSeconds);
        return remaining.Length == 0 ? total : null;
    }

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
    public bool SkipCertificateValidation { get; set; }
    public string? PinnedCertificateThumbprint { get; set; }
    public int TimeoutSeconds { get; set; } = 10;
}
