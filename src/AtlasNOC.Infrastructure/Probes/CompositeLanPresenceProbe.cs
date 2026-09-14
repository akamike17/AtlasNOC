using AtlasNOC.Application.Probes;

namespace AtlasNOC.Infrastructure.Probes;

public sealed class CompositeLanPresenceProbe(IEnumerable<ILanPresenceProbe> probes) : ILanPresenceProbe
{
    public async Task<IReadOnlySet<string>> DiscoverAsync(IReadOnlySet<string> allowedTargets, int timeoutMs,
        CancellationToken ct = default)
    {
        var result = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var probe in probes)
        {
            var remaining = Math.Max(1, timeoutMs / 2);
            foreach (var target in await probe.DiscoverAsync(allowedTargets, remaining, ct)) result.Add(target);
        }
        return result;
    }
}
