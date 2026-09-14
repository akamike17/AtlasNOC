namespace AtlasNOC.Application.Wisp;

public interface IWispObservationService
{
    Task<int> RecordAsync(IReadOnlyCollection<WispClientEvidence> evidence,
        CancellationToken ct = default);
}
