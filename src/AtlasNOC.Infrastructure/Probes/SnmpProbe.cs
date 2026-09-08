using System.Net;
using System.Security.Cryptography;
using System.Text;
using AtlasNOC.Application.Probes;
using AtlasNOC.Domain.Enums;
using Lextm.SharpSnmpLib;
using Lextm.SharpSnmpLib.Messaging;

namespace AtlasNOC.Infrastructure.Probes;

/// <summary>
/// Adaptador SNMP sobre SharpSnmpLib (MIB-II/ifXTable/LLDP-MIB, read-only).
/// La versión v3 se admite en el contrato <see cref="ISnmpProbe"/>, pero el
/// adaptador real actual todavía no la implementa: si se recibe una credencial
/// v3, devuelve <c>null</c> (no fabrica datos) en lugar de fallar con una
/// codificación de autenticación no verificada.
/// </summary>
public class SnmpProbe : ISnmpProbe
{
    private static readonly string SysNameOid = "1.3.6.1.2.1.1.5.0";
    private static readonly string SysObjectIdOid = "1.3.6.1.2.1.1.2.0";
    private static readonly string SysDescrOid = "1.3.6.1.2.1.1.1.0";
    private const string IfTable = "1.3.6.1.2.1.2.2.1";
    private const string IfXTable = "1.3.6.1.2.1.31.1.1.1";
    private const string LldpLocalPortId = "1.0.8802.1.1.2.1.3.7.1.3";
    private const string LldpRemChassisId = "1.0.8802.1.1.2.1.4.1.1.5";
    private const string LldpRemPortId = "1.0.8802.1.1.2.1.4.1.1.7";
    private const string LldpRemSysName = "1.0.8802.1.1.2.1.4.1.1.9";
    private const string LldpRemSysDesc = "1.0.8802.1.1.2.1.4.1.1.10";

    private static IPEndPoint Endpoint(string ip, int port = 161)
        => new(IPAddress.Parse(ip), port);

    public async Task<DeviceFingerprint?> FingerprintAsync(string ipAddress, SnmpConnectionOptions options, int timeoutMs, CancellationToken ct)
    {
        options.Validate();
        if (options.Version == SnmpVersion.V3)
        {
            // v3 aún no implementado en el adaptador real: no fabricar datos.
            return null;
        }

        try
        {
            using var timeout = CreateTimeout(timeoutMs, ct);
            var variables = await Messenger.GetAsync(VersionCode.V2,
                Endpoint(ipAddress),
                new OctetString(options.Community ?? string.Empty),
                new List<Variable>
                {
                    new(new ObjectIdentifier(SysNameOid)),
                    new(new ObjectIdentifier(SysObjectIdOid)),
                    new(new ObjectIdentifier(SysDescrOid)),
                },
                timeout.Token);

            var map = variables.ToDictionary(v => v.Id.ToString());
            var sysName = AsString(map, SysNameOid);
            var sysObjectId = AsString(map, SysObjectIdOid);
            var sysDescr = AsString(map, SysDescrOid);

            if (sysName is null && sysObjectId is null && sysDescr is null)
                return null;

            return new DeviceFingerprint(ipAddress, sysName, sysObjectId, sysDescr, null);
        }
        catch
        {
            return null; // SNMP no disponible: no fabricar datos.
        }
    }

    public async Task<DeviceIdentity> GetIdentityAsync(string ipAddress, SnmpConnectionOptions options, int timeoutMs, CancellationToken ct)
    {
        var fp = await FingerprintAsync(ipAddress, options, timeoutMs, ct);
        return new DeviceIdentity(fp?.SysName ?? ipAddress, null, null, null, fp?.SysObjectId);
    }

    public async Task<IReadOnlyList<InterfaceData>> GetInterfacesAsync(string ipAddress, SnmpConnectionOptions options, int timeoutMs, CancellationToken ct)
    {
        options.Validate();
        var result = new List<InterfaceData>();
        if (options.Version == SnmpVersion.V3)
        {
            return result; // v3 aún no implementado.
        }

        try
        {
            using var timeout = CreateTimeout(timeoutMs, ct);
            var columns = new Dictionary<string, IReadOnlyDictionary<int, Variable>>
            {
                ["descr"] = await WalkIndexedAsync(ipAddress, options, $"{IfTable}.2", timeout.Token),
                ["type"] = await WalkIndexedAsync(ipAddress, options, $"{IfTable}.3", timeout.Token),
                ["speed"] = await WalkIndexedAsync(ipAddress, options, $"{IfTable}.5", timeout.Token),
                ["mac"] = await WalkIndexedAsync(ipAddress, options, $"{IfTable}.6", timeout.Token),
                ["admin"] = await WalkIndexedAsync(ipAddress, options, $"{IfTable}.7", timeout.Token),
                ["oper"] = await WalkIndexedAsync(ipAddress, options, $"{IfTable}.8", timeout.Token),
                ["inDiscards"] = await WalkIndexedAsync(ipAddress, options, $"{IfTable}.13", timeout.Token),
                ["inErrors"] = await WalkIndexedAsync(ipAddress, options, $"{IfTable}.14", timeout.Token),
                ["outDiscards"] = await WalkIndexedAsync(ipAddress, options, $"{IfTable}.19", timeout.Token),
                ["outErrors"] = await WalkIndexedAsync(ipAddress, options, $"{IfTable}.20", timeout.Token),
                ["name"] = await WalkIndexedAsync(ipAddress, options, $"{IfXTable}.1", timeout.Token),
                ["highSpeed"] = await WalkIndexedAsync(ipAddress, options, $"{IfXTable}.15", timeout.Token),
                ["alias"] = await WalkIndexedAsync(ipAddress, options, $"{IfXTable}.18", timeout.Token)
            };

            foreach (var index in columns.Values.SelectMany(c => c.Keys).Distinct().OrderBy(i => i))
            {
                var name = Text(columns["name"], index) ?? Text(columns["descr"], index) ?? $"if{index}";
                var highSpeedMbps = Number(columns["highSpeed"], index);
                var speed = highSpeedMbps.HasValue && highSpeedMbps.Value > 0
                    ? checked(highSpeedMbps.Value * 1_000_000UL)
                    : Number(columns["speed"], index);
                result.Add(new InterfaceData(
                    index,
                    name,
                    Text(columns["alias"], index) ?? Text(columns["descr"], index),
                    Mac(columns["mac"], index),
                    null,
                    checked((int)(Number(columns["admin"], index) ?? 0)),
                    checked((int)(Number(columns["oper"], index) ?? 0)),
                    speed,
                    Number(columns["type"], index)?.ToString(),
                    Number(columns["inErrors"], index),
                    Number(columns["outErrors"], index),
                    Number(columns["inDiscards"], index),
                    Number(columns["outDiscards"], index)));
            }
        }
        catch
        {
            // SNMP no responde: lista vacía.
        }
        return result;
    }

    public async Task<IReadOnlyList<NeighborData>> GetLldpNeighborsAsync(string ipAddress, SnmpConnectionOptions options, int timeoutMs, CancellationToken ct)
    {
        options.Validate();
        if (options.Version == SnmpVersion.V3) return Array.Empty<NeighborData>();
        try
        {
            using var timeout = CreateTimeout(timeoutMs, ct);
            var localPorts = await WalkRawAsync(ipAddress, options, LldpLocalPortId, timeout.Token);
            var localByNumber = localPorts
                .Select(v => (Index: LastOidPart(v.Id.ToString()), Name: ValueText(v)))
                .Where(x => x.Index.HasValue && !string.IsNullOrWhiteSpace(x.Name))
                .ToDictionary(x => x.Index!.Value, x => x.Name!);

            var chassis = await WalkRawAsync(ipAddress, options, LldpRemChassisId, timeout.Token);
            var portIds = ToRemoteDictionary(await WalkRawAsync(ipAddress, options, LldpRemPortId, timeout.Token));
            var sysNames = ToRemoteDictionary(await WalkRawAsync(ipAddress, options, LldpRemSysName, timeout.Token));
            var sysDescriptions = ToRemoteDictionary(await WalkRawAsync(ipAddress, options, LldpRemSysDesc, timeout.Token));
            var neighbors = new List<NeighborData>();
            foreach (var item in chassis)
            {
                var key = RemoteKey(item.Id.ToString());
                var localPortNumber = RemoteLocalPort(item.Id.ToString());
                if (key is null || !localPortNumber.HasValue || !localByNumber.TryGetValue(localPortNumber.Value, out var localName)) continue;
                var chassisId = ValueText(item);
                var systemName = ValueText(sysNames.GetValueOrDefault(key));
                var remoteIdentity = systemName ?? chassisId;
                if (string.IsNullOrWhiteSpace(remoteIdentity)) continue;
                var remotePort = ValueText(portIds.GetValueOrDefault(key));
                var description = ValueText(sysDescriptions.GetValueOrDefault(key));
                var evidence = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(
                    $"{localName}|{chassisId}|{remotePort}|{systemName}|{description}"))).ToLowerInvariant();
                neighbors.Add(new NeighborData(remoteIdentity, remotePort, localName, "LLDP", evidence));
            }
            return neighbors;
        }
        catch
        {
            return Array.Empty<NeighborData>();
        }
    }

    public async Task<HealthData> GetHealthAsync(string ipAddress, SnmpConnectionOptions options, int timeoutMs, CancellationToken ct)
    {
        var fp = await FingerprintAsync(ipAddress, options, timeoutMs, ct);
        return new HealthData(null, fp is null ? 0 : 100, null, null, null);
    }

    private static string? AsString(Dictionary<string, Variable> map, string oid)
        => map.TryGetValue(oid, out var v)
            ? (v.Data as OctetString)?.ToString() ?? v.Data?.ToString()
            : null;

    private static CancellationTokenSource CreateTimeout(int timeoutMs, CancellationToken ct)
    {
        var linked = CancellationTokenSource.CreateLinkedTokenSource(ct);
        linked.CancelAfter(Math.Max(1, timeoutMs));
        return linked;
    }

    private static async Task<IReadOnlyList<Variable>> WalkRawAsync(string ip, SnmpConnectionOptions options, string oid, CancellationToken ct)
    {
        var values = new List<Variable>();
        await Messenger.WalkAsync(VersionCode.V2, Endpoint(ip), new OctetString(options.Community ?? string.Empty),
            new ObjectIdentifier(oid), values, WalkMode.WithinSubtree, ct);
        return values;
    }

    private static async Task<IReadOnlyDictionary<int, Variable>> WalkIndexedAsync(string ip, SnmpConnectionOptions options, string oid, CancellationToken ct)
        => (await WalkRawAsync(ip, options, oid, ct))
            .Select(v => (Index: LastOidPart(v.Id.ToString()), Value: v))
            .Where(x => x.Index.HasValue)
            .ToDictionary(x => x.Index!.Value, x => x.Value);

    private static int? LastOidPart(string oid)
        => int.TryParse(oid[(oid.LastIndexOf('.') + 1)..], out var value) ? value : null;

    private static string? RemoteKey(string oid)
    {
        var parts = oid.Split('.');
        return parts.Length >= 3 ? string.Join('.', parts[^3..]) : null;
    }

    private static IReadOnlyDictionary<string, Variable> ToRemoteDictionary(IEnumerable<Variable> values)
        => values.Select(v => (Key: RemoteKey(v.Id.ToString()), Value: v))
            .Where(x => x.Key is not null)
            .ToDictionary(x => x.Key!, x => x.Value);

    private static int? RemoteLocalPort(string oid)
    {
        var parts = oid.Split('.');
        return parts.Length >= 3 && int.TryParse(parts[^2], out var value) ? value : null;
    }

    private static string? Text(IReadOnlyDictionary<int, Variable> values, int index)
        => values.TryGetValue(index, out var value) ? ValueText(value) : null;

    private static ulong? Number(IReadOnlyDictionary<int, Variable> values, int index)
        => values.TryGetValue(index, out var value) && ulong.TryParse(value.Data.ToString(), out var number) ? number : null;

    private static string? Mac(IReadOnlyDictionary<int, Variable> values, int index)
    {
        if (!values.TryGetValue(index, out var value) || value.Data is not OctetString octets) return null;
        var bytes = octets.GetRaw();
        return bytes.Length == 0 ? null : string.Join(':', bytes.Select(b => b.ToString("X2")));
    }

    private static string? ValueText(Variable? value)
    {
        if (value?.Data is null) return null;
        var text = value.Data is OctetString octets ? octets.ToString() : value.Data.ToString();
        return string.IsNullOrWhiteSpace(text) ? null : text;
    }
}
