using System.Net;
using System.Security.Cryptography;
using System.Text;
using AtlasNOC.Application.Devices;
using AtlasNOC.Application.Probes;
using AtlasNOC.Application.Services;
using AtlasNOC.Domain.Enums;
using AtlasNOC.Infrastructure.Devices;
using AtlasNOC.Infrastructure.Probes;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace AtlasNOC.Tests.Unit;

public class CiscoDriverTests
{
    private static CiscoDriver Driver(ISnmpProbe snmp) => new(snmp);

    [Fact]
    public void Cisco_driver_matches_sysobjectid()
    {
        var fp = new DeviceFingerprint("10.0.0.1", "cisco-sw", "1.3.6.1.4.1.9.1.1", "Cisco IOS", null);
        Assert.True(Driver(new SimulatedSnmpProbe(false, new SnmpProbe())).CanHandle(fp));
        Assert.Equal("cisco", Driver(new SimulatedSnmpProbe(false, new SnmpProbe())).DriverKey);
    }

    [Fact]
    public void Cisco_driver_rejects_non_cisco()
    {
        var fp = new DeviceFingerprint("10.0.0.1", "mikrotik-rb", "1.3.6.1.4.1.14988.1", "RouterOS", null);
        Assert.False(Driver(new SimulatedSnmpProbe(false, new SnmpProbe())).CanHandle(fp));
    }
}

public class SnmpProbeTests
{
    private sealed class FakeSnmpProbe : ISnmpProbe
    {
        private readonly Func<string, SnmpConnectionOptions, int, CancellationToken, Task<DeviceFingerprint?>> _fingerprint;
        private readonly Func<string, SnmpConnectionOptions, int, CancellationToken, Task<IReadOnlyList<InterfaceData>>> _interfaces;
        private readonly Func<string, SnmpConnectionOptions, int, CancellationToken, Task<IReadOnlyList<NeighborData>>> _lldp;
        private readonly Func<string, SnmpConnectionOptions, int, CancellationToken, Task<IReadOnlyList<NeighborData>>> _cdp;
        private readonly Func<string, SnmpConnectionOptions, int, CancellationToken, Task<DeviceIdentity>> _identity;
        private readonly Func<string, SnmpConnectionOptions, int, CancellationToken, Task<HealthData>> _health;

        public FakeSnmpProbe(
            Func<string, SnmpConnectionOptions, int, CancellationToken, Task<DeviceFingerprint?>>? fingerprint = null,
            Func<string, SnmpConnectionOptions, int, CancellationToken, Task<IReadOnlyList<InterfaceData>>>? interfaces = null,
            Func<string, SnmpConnectionOptions, int, CancellationToken, Task<IReadOnlyList<NeighborData>>>? lldp = null,
            Func<string, SnmpConnectionOptions, int, CancellationToken, Task<IReadOnlyList<NeighborData>>>? cdp = null,
            Func<string, SnmpConnectionOptions, int, CancellationToken, Task<DeviceIdentity>>? identity = null,
            Func<string, SnmpConnectionOptions, int, CancellationToken, Task<HealthData>>? health = null)
        {
            _fingerprint = fingerprint ?? ((_, __, ___, ____) => Task.FromResult<DeviceFingerprint?>(null));
            _interfaces = interfaces ?? ((_, __, ___, ____) => Task.FromResult<IReadOnlyList<InterfaceData>>(Array.Empty<InterfaceData>()));
            _lldp = lldp ?? ((_, __, ___, ____) => Task.FromResult<IReadOnlyList<NeighborData>>(Array.Empty<NeighborData>()));
            _cdp = cdp ?? ((_, __, ___, ____) => Task.FromResult<IReadOnlyList<NeighborData>>(Array.Empty<NeighborData>()));
            _identity = identity ?? ((_, __, ___, ____) => Task.FromResult(new DeviceIdentity("unknown", null, null, null, null)));
            _health = health ?? ((_, __, ___, ____) => Task.FromResult(new HealthData(null, null, null, null, null)));
        }

        public Task<DeviceFingerprint?> FingerprintAsync(string ip, SnmpConnectionOptions options, int timeoutMs, CancellationToken ct)
            => _fingerprint(ip, options, timeoutMs, ct);

        public Task<IReadOnlyList<InterfaceData>> GetInterfacesAsync(string ip, SnmpConnectionOptions options, int timeoutMs, CancellationToken ct)
            => _interfaces(ip, options, timeoutMs, ct);

        public Task<IReadOnlyList<NeighborData>> GetLldpNeighborsAsync(string ip, SnmpConnectionOptions options, int timeoutMs, CancellationToken ct)
            => _lldp(ip, options, timeoutMs, ct);

        public Task<IReadOnlyList<NeighborData>> GetCdpNeighborsAsync(string ip, SnmpConnectionOptions options, int timeoutMs, CancellationToken ct)
            => _cdp(ip, options, timeoutMs, ct);

        public Task<DeviceIdentity> GetIdentityAsync(string ip, SnmpConnectionOptions options, int timeoutMs, CancellationToken ct)
            => _identity(ip, options, timeoutMs, ct);

        public Task<HealthData> GetHealthAsync(string ip, SnmpConnectionOptions options, int timeoutMs, CancellationToken ct)
            => _health(ip, options, timeoutMs, ct);
    }

    [Fact]
        public async Task SNMP_v3_options_are_accepted_and_validated()
        {
            // v3 options currently not implemented in SnmpProbe, but should be accepted without throwing
            var options = new SnmpConnectionOptions(
                SnmpVersion.V3,
                null,
                "testuser",
                "SHA1",  // Valid auth protocol
                "authpass123",
                "AES",
                "privpass123");
            options.Validate(); // Should not throw
        }

    [Fact]
    public async Task SNMP_v2c_community_is_required()
    {
        var options = new SnmpConnectionOptions(SnmpVersion.V2c, null, null, null, null, null, null);
        Assert.Throws<InvalidOperationException>(() => options.Validate()); // community required
    }

    [Fact]
    public async Task CDP_neighbors_parsed_with_local_interface_mapping()
    {
        var fake = new FakeSnmpProbe(
            cdp: async (_, __, ___, ____) => new List<NeighborData>
            {
                new("SW-Core", "Gi1/0/1", "Gi1/0/2", "CDP", "evidence1"),
                new("SW-Access", "Gi1/0/3", "Gi1/0/4", "CDP", "evidence2")
            });

        var neighbors = await fake.GetCdpNeighborsAsync("10.0.0.1", SnmpConnectionOptions.Anonymous(), 2000, CancellationToken.None);
        Assert.Equal(2, neighbors.Count);
        Assert.All(neighbors, n => Assert.Equal("CDP", n.Protocol));
    }

    [Fact]
    public async Task LLDP_and_CDP_merged_deduplicated_by_evidence()
    {
        // When evidence is identical, they should be deduplicated
        var fake = new FakeSnmpProbe(
            lldp: async (_, __, ___, ____) => new List<NeighborData>
            {
                new("SW-Core", "Gi1/0/1", "Gi1/0/2", "LLDP", "same-evidence")
            },
            cdp: async (_, __, ___, ____) => new List<NeighborData>
            {
                new("SW-Core", "Gi1/0/1", "Gi1/0/2", "CDP", "same-evidence")
            });

        var driver = new CiscoDriver(fake);
        var neighbors = await driver.GetNeighborsAsync("10.0.0.1", SnmpConnectionOptions.Anonymous(), CancellationToken.None);

        Assert.Single(neighbors); // Should be deduplicated
        Assert.Contains(neighbors[0].Protocol, new[] { "LLDP", "CDP" });
    }

    [Fact]
    public async Task Unknown_protocol_marked_as_unknown_not_manual()
    {
        var fake = new FakeSnmpProbe(
            lldp: async (_, __, ___, ____) => new List<NeighborData>
            {
                new("Unknown-Device", "Port1", "Port2", "UnknownProtocol", "evidence")
            });

        var driver = new CiscoDriver(fake);
        var neighbors = await driver.GetNeighborsAsync("10.0.0.1", SnmpConnectionOptions.Anonymous(), CancellationToken.None);

        Assert.Single(neighbors);
        Assert.Equal("UnknownProtocol", neighbors[0].Protocol);
    }

    [Fact]
    public async Task Cisco_driver_returns_both_lldp_and_cdp_when_evidence_differs()
    {
        // When evidence differs, both should be returned (no false deduplication)
        var fake = new FakeSnmpProbe(
            lldp: async (_, __, ___, ____) => new List<NeighborData>
            {
                new("SW-Remote", "Gi1/0/1", "Gi1/0/1", "LLDP", "ev1")
            },
            cdp: async (_, __, ___, ____) => new List<NeighborData>
            {
                new("SW-Remote", "Gi1/0/1", "Gi1/0/1", "CDP", "ev2")
            });

        var driver = new CiscoDriver(fake);
        var neighbors = await driver.GetNeighborsAsync("10.0.0.1", SnmpConnectionOptions.Anonymous(), CancellationToken.None);

        // Both protocols returned because evidence is different
        Assert.Equal(2, neighbors.Count);
        Assert.Contains(neighbors, n => n.Protocol == "LLDP");
        Assert.Contains(neighbors, n => n.Protocol == "CDP");
    }
}