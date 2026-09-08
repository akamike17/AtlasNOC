namespace AtlasNOC.Infrastructure.Services;

public sealed class DiscoveryOptions
{
    public int MaxConcurrentPing { get; set; } = 64;
    public int PingTimeoutMs { get; set; } = 1000;
    public int MaxTargetsPerRun { get; set; } = 4096;
    public int LinkStaleAfterHours { get; set; } = 24;
    public int LeaseSeconds { get; set; } = 60;
    public int LeaseRenewalSeconds { get; set; } = 20;
}
