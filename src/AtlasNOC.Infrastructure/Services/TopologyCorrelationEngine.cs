using AtlasNOC.Application.Services;
using AtlasNOC.Domain.Enums;

namespace AtlasNOC.Infrastructure.Services;

/// <summary>Correlador determinista basado en índices O(n), sin inferencias por subred.</summary>
public sealed class TopologyCorrelationEngine : ITopologyCorrelationEngine
{
    public Task<IReadOnlyList<CorrelationResult>> CorrelateAsync(
        IReadOnlyList<NeighborObservationInput> observations, CancellationToken ct = default)
    {
        var valid = observations.Where(IsValid).ToList();
        var byLocalIdentity = valid.GroupBy(o => Normalize(o.LocalDeviceIdentity))
            .ToDictionary(g => g.Key, g => g.ToList());
        var byRemoteIdentity = valid.GroupBy(o => Normalize(o.RemoteIdentity))
            .ToDictionary(g => g.Key, g => g.ToList());
        var byLocalInterface = valid.GroupBy(o => Normalize(o.LocalInterfaceId))
            .ToDictionary(g => g.Key, g => g.ToList());
        var byDirectedEdge = valid.GroupBy(o => EdgeKey(o.LocalDeviceIdentity, o.RemoteIdentity))
            .ToDictionary(g => g.Key, g => g.ToList());

        var results = new List<CorrelationResult>();
        var usedPairs = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var a in valid)
        {
            ct.ThrowIfCancellationRequested();
            if (!byRemoteIdentity.ContainsKey(Normalize(a.RemoteIdentity))
                || !byLocalIdentity.ContainsKey(Normalize(a.LocalDeviceIdentity))
                || !byLocalInterface.ContainsKey(Normalize(a.LocalInterfaceId))) continue;

            if (byDirectedEdge[EdgeKey(a.LocalDeviceIdentity, a.RemoteIdentity)].Count != 1)
                continue;

            var reverseKey = EdgeKey(a.RemoteIdentity, a.LocalDeviceIdentity);
            if (!byDirectedEdge.TryGetValue(reverseKey, out var reverseCandidates)) continue;
            var candidates = reverseCandidates
                .Where(b => !SameDevice(a, b) && SameProtocol(a, b))
                .ToList();

            // Sin identidad de puerto local adicional, múltiples candidatos son ambiguos.
            if (candidates.Count != 1) continue;
            var b = candidates[0];
            var pairKey = PairKey(a.LocalInterfaceId, b.LocalInterfaceId);
            if (!usedPairs.Add(pairKey)) continue;

            var protocol = Normalize(a.Protocol);
            var source = SourceOf(protocol);
            var confidence = ConfidenceOf(protocol, a, b);
            results.Add(new CorrelationResult(a.LocalInterfaceId, b.LocalInterfaceId,
                (int)LinkTypeOf(protocol), (int)source, confidence,
                $"Bidirectional {protocol} evidence: {a.LocalDeviceIdentity} ↔ {b.LocalDeviceIdentity}"));
        }

        return Task.FromResult<IReadOnlyList<CorrelationResult>>(results);
    }

    private static bool IsValid(NeighborObservationInput o)
        => !string.IsNullOrWhiteSpace(o.LocalDeviceGuid)
            && !string.IsNullOrWhiteSpace(o.LocalDeviceIdentity)
            && !string.IsNullOrWhiteSpace(o.LocalInterfaceId)
            && !string.IsNullOrWhiteSpace(o.RemoteIdentity)
            && !string.IsNullOrWhiteSpace(o.RawEvidenceHash);

    private static bool SameDevice(NeighborObservationInput a, NeighborObservationInput b)
        => Normalize(a.LocalDeviceGuid) == Normalize(b.LocalDeviceGuid);

    private static bool SameProtocol(NeighborObservationInput a, NeighborObservationInput b)
        => Normalize(a.Protocol) == Normalize(b.Protocol);

    private static string EdgeKey(string local, string remote) => $"{Normalize(local)}→{Normalize(remote)}";

    private static string PairKey(string left, string right)
    {
        var a = Normalize(left); var b = Normalize(right);
        return string.CompareOrdinal(a, b) <= 0 ? $"{a}|{b}" : $"{b}|{a}";
    }

    private static string Normalize(string? value)
        => (value ?? string.Empty).Trim().TrimEnd('.').ToLowerInvariant();

    private static DiscoverySource SourceOf(string protocol) => protocol switch
    {
        "lldp" => DiscoverySource.Lldp,
        "cdp" => DiscoverySource.Cdp,
        "mikrotik" => DiscoverySource.MikroTikNeighbor,
        "ubiquiti" => DiscoverySource.Ubiquiti,
        "wireless" => DiscoverySource.WirelessAssociation,
        _ => DiscoverySource.Unknown
    };

    private static LinkType LinkTypeOf(string protocol) => protocol switch
    {
        "wireless" => LinkType.Wireless,
        "lldp" or "cdp" or "mikrotik" or "ubiquiti" => LinkType.Physical,
        _ => LinkType.Unknown
    };

    private static double ConfidenceOf(string protocol, NeighborObservationInput a, NeighborObservationInput b)
    {
        var confidence = protocol switch
        {
            "lldp" or "cdp" => 0.90,
            "wireless" => 0.90,
            "mikrotik" or "ubiquiti" => 0.85,
            _ => 0.20
        };
        if (!string.IsNullOrWhiteSpace(a.RemotePortIdentity) && !string.IsNullOrWhiteSpace(b.RemotePortIdentity))
            confidence += 0.05;
        return Math.Min(1.0, confidence);
    }
}
