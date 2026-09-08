namespace AtlasNOC.Infrastructure.Services;

public sealed class NotificationOptions
{
    public int BatchSize { get; set; } = 50;
    public int MaxAttempts { get; set; } = 5;
    public int SendingLeaseMinutes { get; set; } = 5;
    public int WorkerIntervalSeconds { get; set; } = 30;
    public int WebhookTimeoutSeconds { get; set; } = 10;
}
