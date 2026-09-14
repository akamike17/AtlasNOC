using AtlasNOC.Application.Wisp;
using AtlasNOC.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace AtlasNOC.Infrastructure.Wisp;

public sealed class WispObservationService(AtlasNOCDbContext context) : IWispObservationService
{
    public async Task<int> RecordAsync(IReadOnlyCollection<WispClientEvidence> evidence,
        CancellationToken ct = default)
    {
        var accepted = evidence
            .Where(x => !string.IsNullOrWhiteSpace(x.ExternalId)
                && !string.IsNullOrWhiteSpace(x.Source)
                && x.Confidence is >= 0 and <= 1)
            .GroupBy(x => new { x.ExternalId, x.Source, x.ObservedAtUtc })
            .Select(x => x.First())
            .ToList();
        if (accepted.Count == 0) return 0;

        var externalIds = accepted.Select(x => x.ExternalId).Distinct().ToList();
        var sources = accepted.Select(x => x.Source).Distinct().ToList();
        var timestamps = accepted.Select(x => x.ObservedAtUtc).ToList();
        var existing = await context.WispClientObservations
            .Where(x => externalIds.Contains(x.ExternalId) && sources.Contains(x.Source)
                && timestamps.Contains(x.ObservedAtUtc))
            .Select(x => new { x.ExternalId, x.Source, x.ObservedAtUtc })
            .ToListAsync(ct);
        var keys = existing.Select(x => $"{x.ExternalId}\u001f{x.Source}\u001f{x.ObservedAtUtc:O}")
            .ToHashSet(StringComparer.Ordinal);
        var added = 0;
        foreach (var item in accepted)
        {
            var key = $"{item.ExternalId}\u001f{item.Source}\u001f{item.ObservedAtUtc:O}";
            if (!keys.Add(key)) continue;
            context.WispClientObservations.Add(new Domain.Entities.WispClientObservation(
                item.ExternalId, item.AccountReference, item.CpeAddress,
                item.SessionReference, item.ObservedAtUtc, item.Source, item.Confidence));
            added++;
        }
        if (added > 0) await context.SaveChangesAsync(ct);
        return added;
    }
}
