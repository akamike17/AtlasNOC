namespace AtlasNOC.Application.Probes;

/// <summary>Probe ICMP: disponibilidad y RTT.</summary>
public interface IIcmpProbe
{
    Task<PingResult> PingAsync(string ipAddress, int timeoutMs, CancellationToken ct);
}

public interface ILabNetworkControl
{
    bool IsReachable(string ipAddress);
    void SetReachability(string ipAddress, bool reachable);
}

public sealed record PingResult(bool Success, double? RoundTripMs, string? ErrorMessage);
