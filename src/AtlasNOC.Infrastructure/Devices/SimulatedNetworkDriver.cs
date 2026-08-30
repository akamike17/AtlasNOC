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

    public bool CanHandle(DeviceFingerprint fingerprint)
        => fingerprint.VendorHint == "simulated" || LabTopology.IsLabIp(fingerprint.ManagementIp);

    public Task<DeviceIdentity> GetIdentityAsync(string managementIp, CancellationToken ct)
    {
        var node = LabTopology.Find(managementIp);
        if (node is null)
            return Task.FromResult(new DeviceIdentity(managementIp, "LAB-Model", "LAB-0001", "1.0.0", "1.3.6.1.4.1.LAB"));

        var identity = new DeviceIdentity(
            node.Value.Hostname,
            node.Value.DeviceType,
            $"LAB-{node.Value.Hostname}",
            "1.0.0",
            node.Value.Vendor == "ubiquiti" ? "1.3.6.1.4.1.41112" : "1.3.6.1.4.1.LAB");
        return Task.FromResult(identity);
    }

    public Task<IReadOnlyList<InterfaceData>> GetInterfacesAsync(string managementIp, CancellationToken ct)
    {
        var node = LabTopology.Find(managementIp);
        if (node is null)
            return Task.FromResult<IReadOnlyList<InterfaceData>>(Array.Empty<InterfaceData>());

        // Genera las interfaces que participan en enlaces LAB-01 (determinista),
        // más un único ether1/ether2 por defecto cuando no hay puertos definidos.
        var ports = LabTopology.Links
            .Where(l => l.A == node.Value.Hostname || l.B == node.Value.Hostname)
            .Select(l => l.A == node.Value.Hostname ? l.APort : l.BPort)
            .Distinct()
            .ToList();

        if (ports.Count == 0)
            ports.Add("ether1");

        var result = new List<InterfaceData>();
        var idx = 0;
        foreach (var port in ports)
        {
            idx++;
            var isWireless = port.StartsWith("sector", StringComparison.OrdinalIgnoreCase)
                || port.StartsWith("wlan", StringComparison.OrdinalIgnoreCase);
            result.Add(new InterfaceData(
                idx,
                port,
                isWireless ? "wireless link" : "ethernet link",
                $"00:00:00:00:00:{idx:D2}",
                null,
                1, 1,
                isWireless ? 300_000_000UL : 1_000_000_000UL,
                isWireless ? "wireless" : "ethernet"));
        }

        return Task.FromResult<IReadOnlyList<InterfaceData>>(result);
    }

    public Task<IReadOnlyList<NeighborData>> GetNeighborsAsync(string managementIp, CancellationToken ct)
    {
        var node = LabTopology.Find(managementIp);
        if (node is null)
            return Task.FromResult<IReadOnlyList<NeighborData>>(Array.Empty<NeighborData>());

        return Task.FromResult(LabTopology.NeighborsFor(node.Value.Hostname));
    }

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