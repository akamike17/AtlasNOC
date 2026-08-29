using System.Net;
using AtlasNOC.Application.Probes;
using Lextm.SharpSnmpLib;
using Lextm.SharpSnmpLib.Messaging;

namespace AtlasNOC.Infrastructure.Probes;

/// <summary>Adaptador SNMP v2c sobre SharpSnmpLib (MIB-II estándar, read-only).</summary>
public class SnmpProbe : ISnmpProbe
{
    private static readonly string SysNameOid = "1.3.6.1.2.1.1.5.0";
    private static readonly string SysObjectIdOid = "1.3.6.1.2.1.1.2.0";
    private static readonly string SysDescrOid = "1.3.6.1.2.1.1.1.0";

    private static IPEndPoint Endpoint(string ip, int port = 161)
        => new(IPAddress.Parse(ip), port);

    public async Task<DeviceFingerprint?> FingerprintAsync(string ipAddress, string community, int timeoutMs, CancellationToken ct)
    {
        try
        {
            var variables = await Messenger.GetAsync(VersionCode.V2,
                Endpoint(ipAddress),
                new OctetString(community),
                new List<Variable>
                {
                    new(new ObjectIdentifier(SysNameOid)),
                    new(new ObjectIdentifier(SysObjectIdOid)),
                    new(new ObjectIdentifier(SysDescrOid)),
                },
                ct);

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

    public async Task<DeviceIdentity> GetIdentityAsync(string ipAddress, string community, int timeoutMs, CancellationToken ct)
    {
        var fp = await FingerprintAsync(ipAddress, community, timeoutMs, ct);
        return new DeviceIdentity(fp?.SysName ?? ipAddress, null, null, null, fp?.SysObjectId);
    }

    public async Task<IReadOnlyList<InterfaceData>> GetInterfacesAsync(string ipAddress, string community, int timeoutMs, CancellationToken ct)
    {
        var result = new List<InterfaceData>();
        try
        {
            // Walk de ifDescr (1.3.6.1.2.1.2.2.1.2) dentro del subtree de la tabla.
            var walkResult = new List<Variable>();
            await Messenger.WalkAsync(VersionCode.V2,
                Endpoint(ipAddress),
                new OctetString(community),
                new ObjectIdentifier("1.3.6.1.2.1.2.2.1.2"),
                walkResult,
                WalkMode.WithinSubtree,
                ct);

            var index = 0;
            foreach (var v in walkResult)
            {
                index++;
                result.Add(new InterfaceData(
                    index,
                    (v.Data as OctetString)?.ToString() ?? $"if{index}",
                    null, null, null, 1, 1, null, "ethernet"));
            }
        }
        catch
        {
            // SNMP no responde: lista vacía.
        }
        return result;
    }

    public async Task<IReadOnlyList<NeighborData>> GetLldpNeighborsAsync(string ipAddress, string community, int timeoutMs, CancellationToken ct)
    {
        await Task.CompletedTask;
        return Array.Empty<NeighborData>();
    }

    public async Task<HealthData> GetHealthAsync(string ipAddress, string community, int timeoutMs, CancellationToken ct)
    {
        var fp = await FingerprintAsync(ipAddress, community, timeoutMs, ct);
        return new HealthData(null, fp is null ? 0 : 100, null, null, null);
    }

    private static string? AsString(Dictionary<string, Variable> map, string oid)
        => map.TryGetValue(oid, out var v)
            ? (v.Data as OctetString)?.ToString() ?? v.Data?.ToString()
            : null;
}