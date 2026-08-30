using AtlasNOC.Application.Probes;

namespace AtlasNOC.Infrastructure.Devices;

/// <summary>
/// Topología determinista LAB-01 (especificación §18). Define los 61 nodos, sus identidades
/// y las relaciones confirmadas entre ellos. La usan el SimulatedNetworkDriver y los probes
/// simulados (ICMP/SNMP) para que el descubrimiento produzca nodos y enlaces reales, sin
/// ningún dato fabricado fuera del modo LAB.
/// </summary>
public static class LabTopology
{
    public readonly record struct LabNode(
        string Hostname, string Ip, string Vendor, string DeviceType, string SysDescription);

    /// <summary>Nodos de infraestructura (backbone), en orden de dibujo upstream→downstream.</summary>
    public static IReadOnlyList<LabNode> Backbone { get; } = new List<LabNode>
    {
        new("EdgeRouter-01",   "10.0.0.1",  "generic",  "Router",    "Edge Router"),
        new("CoreSwitch-01",   "10.0.0.2",  "generic",  "Switch",    "Core Switch"),
        new("TowerA-Backhaul", "10.0.1.1",  "generic",  "Backhaul",  "Tower A Backhaul"),
        new("TowerA-Switch",   "10.0.1.2",  "generic",  "Switch",    "Tower A Switch"),
        new("AP-A1",           "10.0.1.10", "ubiquiti", "AccessPoint", "UniFi AP"),
        new("AP-A2",           "10.0.1.11", "ubiquiti", "AccessPoint", "UniFi AP"),
        new("AP-A3",           "10.0.1.12", "ubiquiti", "AccessPoint", "UniFi AP"),
        new("TowerB-Backhaul", "10.0.2.1",  "generic",  "Backhaul",  "Tower B Backhaul"),
        new("TowerB-Switch",   "10.0.2.2",  "generic",  "Switch",    "Tower B Switch"),
        new("AP-B1",           "10.0.2.10", "ubiquiti", "AccessPoint", "UniFi AP"),
        new("AP-B2",           "10.0.2.11", "ubiquiti", "AccessPoint", "UniFi AP"),
    };

    /// <summary>Los 50 CPE (10 por cada AP).</summary>
    public static IReadOnlyList<LabNode> Cpes { get; } = BuildCpes();

    private static List<LabNode> BuildCpes()
    {
        var list = new List<LabNode>();
        var apList = new[] { "AP-A1", "AP-A2", "AP-A3", "AP-B1", "AP-B2" };
        var subnet = new[] { 11, 12, 13, 21, 22 }; // 10.0.{octet}.1..10
        for (var a = 0; a < apList.Length; a++)
        {
            for (var i = 1; i <= 10; i++)
            {
                list.Add(new LabNode(
                    $"CPE-{apList[a]}-{i:D2}",
                    $"10.0.{subnet[a]}.{i}",
                    "ubiquiti",
                    "Cpe",
                    "Customer Premises Equipment"));
            }
        }
        return list;
    }

    /// <summary>Los 61 nodos completos.</summary>
    public static IReadOnlyList<LabNode> All { get; } = Backbone.Concat(Cpes).ToList();

    /// <summary>Índice IP → nodo.</summary>
    private static readonly IReadOnlyDictionary<string, LabNode> ByIp =
        All.ToDictionary(n => n.Ip, StringComparer.OrdinalIgnoreCase);

    public static LabNode? Find(string ip) => ByIp.TryGetValue(ip, out var n) ? n : null;
    public static bool IsLabIp(string ip) => ip.StartsWith("10.0.", StringComparison.Ordinal);

    /// <summary>
    /// Relaciones bidireccionales confirmadas. Cada entrada conecta dos interfaces de
    /// dos dispositivos usando el protocolo indicado (physical → ethernet/lldp, wireless → AP-CPE).
    /// Forma: (A hostname, A puerto local, B hostname, B puerto local, protocolo).
    /// </summary>
    public static IReadOnlyList<(string A, string APort, string B, string BPort, string Protocol)> Links { get; } =
        BuildLinks();

    private static List<(string, string, string, string, string)> BuildLinks()
    {
        var links = new List<(string, string, string, string, string)>
        {
            ("EdgeRouter-01",   "ether2", "CoreSwitch-01",   "ether1", "lldp"),
            ("CoreSwitch-01",   "ether2", "TowerA-Backhaul", "ether1", "lldp"),
            ("CoreSwitch-01",   "ether3", "TowerB-Backhaul", "ether1", "lldp"),
            ("TowerA-Backhaul", "ether2", "TowerA-Switch",   "ether1", "lldp"),
            ("TowerA-Switch",   "ether2", "AP-A1",           "ether1", "lldp"),
            ("TowerA-Switch",   "ether3", "AP-A2",           "ether1", "lldp"),
            ("TowerA-Switch",   "ether4", "AP-A3",           "ether1", "lldp"),
            ("TowerB-Backhaul", "ether2", "TowerB-Switch",   "ether1", "lldp"),
            ("TowerB-Switch",   "ether2", "AP-B1",           "ether1", "lldp"),
            ("TowerB-Switch",   "ether3", "AP-B2",           "ether1", "lldp"),
        };

        // 50 enlaces wireless AP↔CPE.
        var apSector = new Dictionary<string, string>
        {
            { "AP-A1", "sector-1" }, { "AP-A2", "sector-2" }, { "AP-A3", "sector-3" },
            { "AP-B1", "sector-1" }, { "AP-B2", "sector-2" },
        };
        foreach (var ap in apSector.Keys)
        {
            for (var i = 1; i <= 10; i++)
            {
                var cpe = $"CPE-{ap}-{i:D2}";
                links.Add((ap, apSector[ap], cpe, "wlan1", "wireless"));
            }
        }

        return links;
    }

    /// <summary>
    /// Devuelve las observaciones de vecinos que un dispositivo concreto reporta
    /// (desde su perspectiva). Cada enlace genera una observación en cada extremo.
    /// </summary>
    public static IReadOnlyList<NeighborData> NeighborsFor(string hostname)
    {
        var result = new List<NeighborData>();
        foreach (var (a, aPort, b, bPort, proto) in Links)
        {
            if (a == hostname)
                result.Add(new NeighborData(b, bPort, aPort, proto, Hash($"{a}:{aPort}:{b}:{bPort}")));
            else if (b == hostname)
                result.Add(new NeighborData(a, aPort, bPort, proto, Hash($"{b}:{bPort}:{a}:{aPort}")));
        }
        return result;
    }

    private static string Hash(string input)
    {
        var bytes = System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(input));
        return Convert.ToHexString(bytes)[..16];
    }
}