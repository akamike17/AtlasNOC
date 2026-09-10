using System.Collections.Concurrent;
using AtlasNOC.Application.Probes;

namespace AtlasNOC.Infrastructure.Probes;

public sealed class LabNetworkControl : ILabNetworkControl
{
    private readonly ConcurrentDictionary<string, bool> _overrides = new(StringComparer.OrdinalIgnoreCase);

    public bool IsReachable(string ipAddress) =>
        !_overrides.TryGetValue(ipAddress, out var reachable) || reachable;

    public void SetReachability(string ipAddress, bool reachable) =>
        _overrides[ipAddress] = reachable;
}
