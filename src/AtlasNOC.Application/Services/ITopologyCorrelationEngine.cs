using AtlasNOC.Application.Probes;

namespace AtlasNOC.Application.Services;

/// <summary>Política de correlación topológica: observaciones de vecinos → enlaces.</summary>
public interface ITopologyCorrelationEngine
{
    /// <summary>Correlaciona observaciones de vecinos de distintos dispositivos y determina enlaces.
    /// Devuelve pares de interfaces (A,B) con evidencia y confidence, solo si hay evidencia suficiente.</summary>
    Task<IReadOnlyList<CorrelationResult>> CorrelateAsync(
        IReadOnlyList<NeighborObservationInput> observations, CancellationToken ct = default);
}

public sealed record NeighborObservationInput(
    string LocalDeviceId,
    string LocalInterfaceId,
    string RemoteIdentity,
    string? RemotePortIdentity,
    string Protocol,
    string RawEvidenceHash);

public sealed record CorrelationResult(
    string AInterfaceId,
    string BInterfaceId,
    int LinkType,
    int DiscoverySource,
    double Confidence,
    string Evidence);