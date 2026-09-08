namespace AtlasNOC.Infrastructure.Services;

public sealed class PollingOptions
{
    public int MaxConcurrency { get; set; } = 32;
    public int DefaultIntervalSeconds { get; set; } = 30;
    public int DefaultTimeoutMs { get; set; } = 5000;
    public int DefaultRetries { get; set; } = 1;
    public int InterfaceRefreshIntervalSeconds { get; set; } = 60;
    public int WirelessRefreshIntervalSeconds { get; set; } = 60;
    public int SchedulerTickSeconds { get; set; } = 1;
}
