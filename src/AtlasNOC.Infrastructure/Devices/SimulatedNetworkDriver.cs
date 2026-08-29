using AtlasNOC.Application.Devices;
using AtlasNOC.Application.Probes;

namespace AtlasNOC.Infrastructure.Devices;

/// <summary>
/// Driver de laboratorio determinista (LAB-01). Genera la topología simulada y métricas sintéticas
/// explícitamente marcadas como laboratorio. Se activa solo cuando se acuerda el modo LAB.
/// </summary>
public class SimulatedNetworkDriver : IDeviceDriver
{
    private readonly Random _rng = new(42); // semilla fija => determinista

    public string DriverKey => "simulated";

    // ── Topología LAB-01 (determinista) ────────────────────────────────────
    public static IReadOnlyList<(string Hostname, string Ip, string Vendor)> LabDevices { get; } =
        BuildLabDevices();

    private static IReadOnlyList<(string, string, string)> BuildLabDevices()
    {
        var list = new List<(string, string, string)>
        {
            ("EdgeRouter-01", "10.0.0.1", "generic"),
            ("CoreSwitch-01", "10.0.0.2", "generic"),
            ("TowerA-Backhaul", "10.0.1.1", "generic"),
            ("TowerA-Switch", "10.0.1.2", "generic"),
            ("AP-A1", "10.0.1.10", "ubiquiti"),
            ("AP-A2", "10.0.1.11", "ubiquiti"),
            ("AP-A3", "10.0.1.12", "ubiquiti"),
            ("TowerB-Backhaul", "10.0.2.1", "generic"),
            ("TowerB-Switch", "10.0.2.2", "generic"),
            ("AP-B1", "10.0.2.10", "ubiquiti"),
            ("AP-B2", "10.0.2.11", "ubiquiti"),
        };
        // 10 CPE per AP => 50 CPE.
        var apCounter = 0;
        foreach (var ap in new[] { "AP-A1", "AP-A2", "AP-A3", "AP-B1", "AP-B2" })
        {
            apCounter++;
            for (var i = 1; i <= 10; i++)
                list.Add(($"CPE-{ap}-{i:D2}", $"10.0.{10 + apCounter}.{i}", "ubiquiti"));
        }
        return list;
    }

    public bool CanHandle(DeviceFingerprint fingerprint)
        => fingerprint.VendorHint == "simulated" || fingerprint.ManagementIp.StartsWith("10.0.");

    public Task<DeviceIdentity> GetIdentityAsync(string managementIp, CancellationToken ct)
    {
        var host = managementIp.Replace('.', '-');
        return Task.FromResult(new DeviceIdentity(host, "LAB-Model", "LAB-0001", "1.0.0", "1.3.6.1.4.1.LAB"));
    }

    public Task<IReadOnlyList<InterfaceData>> GetInterfacesAsync(string managementIp, CancellationToken ct)
    {
        var result = new List<InterfaceData>
        {
            new(1, "ether1", "uplink", "00:00:00:00:00:01", managementIp, 1, 1, 1_000_000_000UL, "ethernet"),
            new(2, "ether2", "downlink", "00:00:00:00:00:02", null, 1, 1, 1_000_000_000UL, "ethernet"),
        };
        return Task.FromResult<IReadOnlyList<InterfaceData>>(result);
    }

    public Task<IReadOnlyList<NeighborData>> GetNeighborsAsync(string managementIp, CancellationToken ct)
        => Task.FromResult<IReadOnlyList<NeighborData>>(Array.Empty<NeighborData>());

    public Task<HealthData> GetHealthAsync(string managementIp, CancellationToken ct)
    {
        // Sintético y determinista (semilla fija). Explícitamente laboratorio.
        var cpu = Math.Round(30 + _rng.NextDouble() * 50, 1);
        var mem = Math.Round(40 + _rng.NextDouble() * 40, 1);
        var latency = Math.Round(1 + _rng.NextDouble() * 5, 1);
        return Task.FromResult(new HealthData(latency, 100.0, cpu, mem, 1_000_000));
    }

    public Task<IReadOnlyList<MetricDatum>> GetMetricsAsync(string managementIp, CancellationToken ct)
    {
        var result = new List<MetricDatum>
        {
            new("cpu_usage", Math.Round(30 + _rng.NextDouble() * 50, 1), "%"),
            new("memory_usage", Math.Round(40 + _rng.NextDouble() * 40, 1), "%"),
            new("rtt", Math.Round(1 + _rng.NextDouble() * 5, 1), "ms"),
        };
        return Task.FromResult<IReadOnlyList<MetricDatum>>(result);
    }

    public Task<IReadOnlyList<WirelessClientData>> GetWirelessAssociationsAsync(string managementIp, CancellationToken ct)
    {
        if (!managementIp.Contains("AP", StringComparison.OrdinalIgnoreCase))
            return Task.FromResult<IReadOnlyList<WirelessClientData>>(Array.Empty<WirelessClientData>());

        var result = new List<WirelessClientData>();
        for (var i = 1; i <= 10; i++)
        {
            result.Add(new WirelessClientData(
                $"00:11:22:33:44:{i:D2}", $"CPE-{i:D2}", -55 - _rng.Next(5), -95,
                -55 - _rng.Next(5) - -95, 300 + _rng.Next(100), 300 + _rng.Next(100), "sector-1"));
        }
        return Task.FromResult<IReadOnlyList<WirelessClientData>>(result);
    }
}