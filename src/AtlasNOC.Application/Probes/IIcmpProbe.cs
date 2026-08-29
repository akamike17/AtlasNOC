namespace AtlasNOC.Application.Probes;

/// <summary>Probe ICMP: disponibilidad y RTT.</summary>
public interface IIcmpProbe
{
    Task<PingResult> PingAsync(string ipAddress, int timeoutMs, CancellationToken ct);
}

public sealed record PingResult(bool Success, double? RoundTripMs, string? ErrorMessage);