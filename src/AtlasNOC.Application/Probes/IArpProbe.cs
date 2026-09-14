namespace AtlasNOC.Application.Probes;

public interface IArpProbe
{
    Task<bool> ResolveAsync(string ipAddress, CancellationToken ct = default);
}
