using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Serialization;
using AtlasNOC.Application.Devices;
using AtlasNOC.Application.Probes;
using AtlasNOC.Application.Services;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace AtlasNOC.Infrastructure.Devices;

/// <summary>Driver del UniFi Network Controller. No representa dispositivos airOS.</summary>
public sealed class UbiquitiDriver : IDeviceDriver, IDeviceCredentialAwareDriver
{
    private readonly IHttpClientFactory _http;
    private readonly UbiquitiOptions _options;
    private readonly ILogger<UbiquitiDriver> _logger;

    public UbiquitiDriver(IHttpClientFactory http, UbiquitiOptions options, ILogger<UbiquitiDriver>? logger = null)
    {
        _http = http; _options = options; _logger = logger ?? NullLogger<UbiquitiDriver>.Instance;
    }

    public string DriverKey => "ubiquiti-unifi";
    public bool CanHandle(DeviceFingerprint fp)
    {
        var text = $"{fp.SysObjectId} {fp.SysDescription} {fp.SysName}".ToLowerInvariant();
        return text.Contains("unifi") || (fp.SysObjectId?.Contains("1.3.6.1.4.1.41112") ?? false);
    }

    public Task<DeviceIdentity> GetIdentityAsync(string ip, CancellationToken ct) => GetIdentityCoreAsync(ip, _options.ApiKey, ct);
    public Task<DeviceIdentity> GetIdentityAsync(string ip, ResolvedDeviceCredential credential, CancellationToken ct)
        => GetIdentityCoreAsync(ip, credential.AuthPassword ?? credential.PrivPassword, ct);

    private async Task<DeviceIdentity> GetIdentityCoreAsync(string ip, string? key, CancellationToken ct)
    {
        var d = await FindDeviceAsync(ip, key, ct);
        return d is null ? new DeviceIdentity(ip, null, null, null, null)
            : new DeviceIdentity(d.Name ?? d.Hostname ?? ip, d.Model, d.Serial, d.Version, "1.3.6.1.4.1.41112");
    }

    public Task<IReadOnlyList<InterfaceData>> GetInterfacesAsync(string ip, CancellationToken ct) => GetInterfacesCoreAsync(ip, _options.ApiKey, ct);
    public Task<IReadOnlyList<InterfaceData>> GetInterfacesAsync(string ip, ResolvedDeviceCredential credential, CancellationToken ct)
        => GetInterfacesCoreAsync(ip, credential.AuthPassword ?? credential.PrivPassword, ct);

    private async Task<IReadOnlyList<InterfaceData>> GetInterfacesCoreAsync(string ip, string? key, CancellationToken ct)
    {
        var d = await FindDeviceAsync(ip, key, ct);
        if (d?.Ports is null) return Array.Empty<InterfaceData>();
        return d.Ports.Where(p => p.Index > 0).Select(p => new InterfaceData(p.Index, p.Name ?? $"port{p.Index}", null,
            p.Mac, null, p.Enabled == false ? 2 : p.Enabled == true ? 1 : 0, p.Up == true ? 1 : p.Up == false ? 2 : 0,
            p.SpeedMbps is > 0 ? checked((ulong)p.SpeedMbps.Value * 1_000_000UL) : null, p.Type)).ToList();
    }

    public Task<IReadOnlyList<NeighborData>> GetNeighborsAsync(string ip, CancellationToken ct) => GetNeighborsCoreAsync(ip, _options.ApiKey, ct);
    public Task<IReadOnlyList<NeighborData>> GetNeighborsAsync(string ip, ResolvedDeviceCredential credential, CancellationToken ct)
        => GetNeighborsCoreAsync(ip, credential.AuthPassword ?? credential.PrivPassword, ct);

    private async Task<IReadOnlyList<NeighborData>> GetNeighborsCoreAsync(string ip, string? key, CancellationToken ct)
    {
        var uplink = (await FindDeviceAsync(ip, key, ct))?.Uplink;
        var remote = uplink?.RemoteName ?? uplink?.Mac;
        if (string.IsNullOrWhiteSpace(remote)) return Array.Empty<NeighborData>();
        var local = uplink?.Name ?? "uplink";
        return new[] { new NeighborData(remote, uplink?.RemotePort, local, "ubiquiti", Hash($"{ip}|{local}|{remote}|{uplink?.RemotePort}")) };
    }

    public async Task<HealthData> GetHealthAsync(string ip, CancellationToken ct)
    {
        var d = await FindDeviceAsync(ip, _options.ApiKey, ct);
        return d is null ? new HealthData(null, null, null, null, null)
            : new HealthData(null, null, d.Stats?.Cpu, d.Stats?.Memory, d.Uptime);
    }

    public async Task<IReadOnlyList<MetricDatum>> GetMetricsAsync(string ip, CancellationToken ct)
    {
        var h = await GetHealthAsync(ip, ct); var result = new List<MetricDatum>();
        if (h.CpuPercent.HasValue) result.Add(new("cpu_usage", h.CpuPercent.Value, "%"));
        if (h.MemoryPercent.HasValue) result.Add(new("memory_usage", h.MemoryPercent.Value, "%"));
        if (h.UptimeSeconds.HasValue) result.Add(new("uptime", h.UptimeSeconds.Value, "s"));
        return result;
    }

    public async Task<IReadOnlyList<WirelessClientData>> GetWirelessAssociationsAsync(string ip, CancellationToken ct)
    {
        var envelope = await GetAsync<UniFiEnvelope<UniFiClient>>(Path("stat/sta"), _options.ApiKey, ct);
        return (IReadOnlyList<WirelessClientData>?)envelope?.Data?.Where(c => !string.IsNullOrWhiteSpace(c.Mac)).Select(c => new WirelessClientData(c.Mac!,
            c.Hostname ?? c.Name, c.Signal, c.Noise, c.Signal.HasValue && c.Noise.HasValue ? c.Signal - c.Noise : null,
            c.TxRate / 1_000_000d, c.RxRate / 1_000_000d, c.ApMac ?? c.Essid)).ToList()
            ?? Array.Empty<WirelessClientData>();
    }

    private async Task<UniFiDevice?> FindDeviceAsync(string identity, string? key, CancellationToken ct)
    {
        var envelope = await GetAsync<UniFiEnvelope<UniFiDevice>>(Path("stat/device"), key, ct);
        return envelope?.Data?.FirstOrDefault(d => identity.Equals(d.Ip, StringComparison.OrdinalIgnoreCase)
            || identity.Equals(d.Mac, StringComparison.OrdinalIgnoreCase) || identity.Equals(d.Id, StringComparison.OrdinalIgnoreCase));
    }

    private async Task<T?> GetAsync<T>(string path, string? key, CancellationToken ct)
    {
        try
        {
            if (!Uri.TryCreate(_options.ControllerUrl, UriKind.Absolute, out var uri))
            { _logger.LogWarning("UniFi ControllerUrl no está configurado o no es válido"); return default; }
            var client = _http.CreateClient("ubiquiti");
            if (client.BaseAddress is null) client.BaseAddress = uri;
            using var request = new HttpRequestMessage(HttpMethod.Get, path);
            if (!string.IsNullOrWhiteSpace(key)) request.Headers.TryAddWithoutValidation("X-API-Key", key);
            using var response = await client.SendAsync(request, ct);
            if (!response.IsSuccessStatusCode)
            { _logger.LogWarning("UniFi devolvió HTTP {StatusCode} en {Path}", (int)response.StatusCode, path); return default; }
            return await response.Content.ReadFromJsonAsync<T>(cancellationToken: ct);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
        catch (Exception ex) { _logger.LogWarning(ex, "Fallo consultando UniFi en {Path}", path); return default; }
    }

    private string Path(string endpoint) => $"{(_options.UseUniFiOs ? "/proxy/network" : string.Empty)}/api/s/{Uri.EscapeDataString(_options.Site)}/{endpoint}";
    private static string Hash(string input) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(input))).ToLowerInvariant();
}

public sealed class UbiquitiOptions
{
    public string? ControllerUrl { get; set; }
    public string Site { get; set; } = "default";
    public string? ApiKey { get; set; }
    public bool UseUniFiOs { get; set; } = true;
    public bool SkipCertificateValidation { get; set; }
    public string? PinnedCertificateThumbprint { get; set; }
    public int TimeoutSeconds { get; set; } = 10;
}

internal sealed record UniFiEnvelope<T>([property: JsonPropertyName("data")] List<T>? Data);
internal sealed record UniFiDevice([property: JsonPropertyName("_id")] string? Id, [property: JsonPropertyName("ip")] string? Ip,
    [property: JsonPropertyName("mac")] string? Mac, [property: JsonPropertyName("name")] string? Name,
    [property: JsonPropertyName("hostname")] string? Hostname, [property: JsonPropertyName("model")] string? Model,
    [property: JsonPropertyName("serial")] string? Serial, [property: JsonPropertyName("version")] string? Version,
    [property: JsonPropertyName("uptime")] long? Uptime, [property: JsonPropertyName("system-stats")] UniFiStats? Stats,
    [property: JsonPropertyName("uplink")] UniFiUplink? Uplink, [property: JsonPropertyName("port_table")] List<UniFiPort>? Ports);
internal sealed record UniFiStats([property: JsonPropertyName("cpu")] double? Cpu, [property: JsonPropertyName("mem_used")] double? Memory);
internal sealed record UniFiPort([property: JsonPropertyName("port_idx")] int Index, [property: JsonPropertyName("name")] string? Name,
    [property: JsonPropertyName("mac")] string? Mac, [property: JsonPropertyName("up")] bool? Up,
    [property: JsonPropertyName("enabled")] bool? Enabled, [property: JsonPropertyName("speed")] long? SpeedMbps,
    [property: JsonPropertyName("type")] string? Type);
internal sealed record UniFiUplink([property: JsonPropertyName("name")] string? Name, [property: JsonPropertyName("mac")] string? Mac,
    [property: JsonPropertyName("remote_name")] string? RemoteName, [property: JsonPropertyName("remote_port")] string? RemotePort);
internal sealed record UniFiClient([property: JsonPropertyName("mac")] string? Mac, [property: JsonPropertyName("hostname")] string? Hostname,
    [property: JsonPropertyName("name")] string? Name, [property: JsonPropertyName("signal")] double? Signal,
    [property: JsonPropertyName("noise")] double? Noise, [property: JsonPropertyName("tx_rate")] double? TxRate,
    [property: JsonPropertyName("rx_rate")] double? RxRate, [property: JsonPropertyName("ap_mac")] string? ApMac,
    [property: JsonPropertyName("essid")] string? Essid);
