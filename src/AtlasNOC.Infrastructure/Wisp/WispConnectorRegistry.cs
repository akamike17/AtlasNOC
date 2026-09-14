using AtlasNOC.Application.Wisp;

namespace AtlasNOC.Infrastructure.Wisp;

/// <summary>Registro seguro: por defecto no habilita conectores ni acciones remotas.</summary>
public sealed class WispConnectorRegistry(IEnumerable<IWispConnector> connectors) : IWispConnectorRegistry
{
    private readonly IReadOnlyDictionary<string, IWispConnector> _connectors =
        connectors
            .GroupBy(c => c.Descriptor.Key, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.Single(), StringComparer.OrdinalIgnoreCase);

    public IReadOnlyList<WispConnectorDescriptor> List()
        => _connectors.Values.Where(c => c.IsConfigured)
            .Select(c => c.Descriptor).OrderBy(d => d.DisplayName).ToList();

    public IWispConnector? Resolve(string key)
        => string.IsNullOrWhiteSpace(key) ? null : _connectors.GetValueOrDefault(key);
}
