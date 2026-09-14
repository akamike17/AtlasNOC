namespace AtlasNOC.Application.Probes;

/// <summary>Descubrimiento L2/LAN de sólo lectura, acotado al alcance solicitado.</summary>
public interface ILanPresenceProbe
{
    Task<IReadOnlySet<string>> DiscoverAsync(IReadOnlySet<string> allowedTargets, int timeoutMs,
        CancellationToken ct = default);
}
