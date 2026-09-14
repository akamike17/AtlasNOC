namespace AtlasNOC.Domain.Entities;

/// <summary>Observación temporal de un cliente WISP; no es autorización de corte.</summary>
public sealed class WispClientObservation
{
    public Guid Id { get; private set; }
    public string ExternalId { get; private set; } = string.Empty;
    public string? AccountReference { get; private set; }
    public string? CpeAddress { get; private set; }
    public string? SessionReference { get; private set; }
    public DateTime ObservedAtUtc { get; private set; }
    public string Source { get; private set; } = string.Empty;
    public double Confidence { get; private set; }

    private WispClientObservation() { }

    public WispClientObservation(string externalId, string? accountReference, string? cpeAddress,
        string? sessionReference, DateTime observedAtUtc, string source, double confidence)
    {
        if (string.IsNullOrWhiteSpace(externalId)) throw new ArgumentException("ExternalId es obligatorio.", nameof(externalId));
        if (string.IsNullOrWhiteSpace(source)) throw new ArgumentException("Source es obligatorio.", nameof(source));
        if (externalId.Trim().Length > 200) throw new ArgumentException("ExternalId excede 200 caracteres.", nameof(externalId));
        if (accountReference?.Trim().Length > 200) throw new ArgumentException("AccountReference excede 200 caracteres.", nameof(accountReference));
        if (cpeAddress?.Trim().Length > 200) throw new ArgumentException("CpeAddress excede 200 caracteres.", nameof(cpeAddress));
        if (sessionReference?.Trim().Length > 200) throw new ArgumentException("SessionReference excede 200 caracteres.", nameof(sessionReference));
        if (source.Trim().Length > 100) throw new ArgumentException("Source excede 100 caracteres.", nameof(source));
        if (confidence is < 0 or > 1) throw new ArgumentOutOfRangeException(nameof(confidence));
        Id = Guid.NewGuid();
        ExternalId = externalId.Trim();
        AccountReference = accountReference?.Trim();
        CpeAddress = cpeAddress?.Trim();
        SessionReference = sessionReference?.Trim();
        ObservedAtUtc = observedAtUtc;
        Source = source.Trim();
        Confidence = confidence;
    }
}
