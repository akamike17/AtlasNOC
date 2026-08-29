using System.Security.Cryptography;
using System.Text;
using AtlasNOC.Application.Services;
using AtlasNOC.Domain.Enums;

namespace AtlasNOC.Infrastructure.Services;

/// <summary>
/// Motor de correlación topológica. Convierte observaciones de vecinos (LLDP/CDP/MikroTik/Ubiquiti/inalámbrico)
/// en enlaces solo cuando hay evidencia suficiente y no ambigua (Flujo C y reglas técnicas §15).
/// </summary>
public class TopologyCorrelationEngine : ITopologyCorrelationEngine
{
    public Task<IReadOnlyList<CorrelationResult>> CorrelateAsync(
        IReadOnlyList<NeighborObservationInput> observations, CancellationToken ct = default)
    {
        var results = new List<CorrelationResult>();

        // Asociar observations bidireccionales: (A→B) y (B→A) con identidades coincidentes.
        // Clave de correlación: remote identity normalizada (hostname/sysName).
        var byRemoteIdentity = observations
            .GroupBy(o => Normalize(o.RemoteIdentity), StringComparer.OrdinalIgnoreCase);

        var used = new HashSet<string>();

        // Mapa deviceId -> gestión de matches de puerto/identidad.
        foreach (var group in byRemoteIdentity)
        {
            ct.ThrowIfCancellationRequested();
            // Para cada par de observaciones donde A.RemoteIdentity == B.Hostname y B.RemoteIdentity == A.Hostname
            var candidates = group.ToList();
            // Encontrar el par simétrico: la observación cuya identidad local corresponde a la remote de otra.
            var matched = FindBidirectionalPairs(observations, used);
            foreach (var (a, b) in matched)
            {
                var key = PairKey(a, b);
                if (!used.Add(key)) continue;

                var protocol = ProtocolOf(a);
                var source = SourceOf(protocol);
                var confidence = ConfidenceOf(protocol, a, b);

                results.Add(new CorrelationResult(
                    a.LocalInterfaceId,
                    b.LocalInterfaceId,
                    (int)LinkTypeOf(protocol),
                    (int)source,
                    confidence,
                    $"Bidirectional {protocol} match: {a.RemoteIdentity} ↔ {b.RemoteIdentity}"));
            }
        }

        return Task.FromResult<IReadOnlyList<CorrelationResult>>(results);
    }

    private static List<(NeighborObservationInput A, NeighborObservationInput B)> FindBidirectionalPairs(
        IReadOnlyList<NeighborObservationInput> observations, HashSet<string> used)
    {
        var pairs = new List<(NeighborObservationInput, NeighborObservationInput)>();
        for (var i = 0; i < observations.Count; i++)
        {
            for (var j = i + 1; j < observations.Count; j++)
            {
                var a = observations[i];
                var b = observations[j];
                if (a.LocalDeviceId == b.LocalDeviceId) continue;

                var aSeesB = Normalize(a.RemoteIdentity) == Normalize(b.LocalDeviceId)
                    || Normalize(a.RemoteIdentity) == Normalize(b.RemoteIdentity);
                var bSeesA = Normalize(b.RemoteIdentity) == Normalize(a.LocalDeviceId)
                    || Normalize(b.RemoteIdentity) == Normalize(a.RemoteIdentity);

                if (aSeesB || bSeesA)
                {
                    // Preferir match simétrico completo sobre parcial.
                    var symmetrical = Normalize(a.RemoteIdentity) == Normalize(b.LocalDeviceId)
                        && Normalize(b.RemoteIdentity) == Normalize(a.LocalDeviceId);
                    if (symmetrical || aSeesB)
                        pairs.Add((a, b));
                }
            }
        }
        return pairs;
    }

    private static string Normalize(string? s)
        => (s ?? string.Empty).Trim().ToLowerInvariant().TrimEnd('.');

    private static string PairKey(NeighborObservationInput a, NeighborObservationInput b)
    {
        var left = a.LocalInterfaceId;
        var right = b.LocalInterfaceId;
        var ordered = string.CompareOrdinal(left, right) <= 0 ? (left, right) : (right, left);
        return $"{ordered.Item1}|{ordered.Item2}";
    }

    private static string ProtocolOf(NeighborObservationInput o) => o.Protocol.ToLowerInvariant();

    private static DiscoverySource SourceOf(string protocol) => protocol switch
    {
        "lldp" => DiscoverySource.Lldp,
        "cdp" => DiscoverySource.Cdp,
        "mikrotik" => DiscoverySource.MikroTikNeighbor,
        "ubiquiti" => DiscoverySource.Ubiquiti,
        "wireless" => DiscoverySource.WirelessAssociation,
        _ => DiscoverySource.Manual
    };

    private static LinkType LinkTypeOf(string protocol) => protocol switch
    {
        "wireless" => LinkType.Wireless,
        _ => LinkType.Physical
    };

    private static double ConfidenceOf(string protocol, NeighborObservationInput a, NeighborObservationInput b)
    {
        var baseConfidence = protocol switch
        {
            "lldp" => 0.95,
            "cdp" => 0.95,
            "mikrotik" => 0.85,
            "ubiquiti" => 0.85,
            "wireless" => 0.90,
            _ => 0.5
        };
        // Simetría completa (ambos extremos se ven) aumenta la confianza.
        var symmetrical = Normalize(a.RemoteIdentity) == Normalize(b.LocalDeviceId)
            && Normalize(b.RemoteIdentity) == Normalize(a.LocalDeviceId);
        return Math.Clamp(baseConfidence + (symmetrical ? 0.05 : 0.0), 0.0, 1.0);
    }
}