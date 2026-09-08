using AtlasNOC.Domain.Entities;
using AtlasNOC.Domain.Enums;
using AtlasNOC.Infrastructure.Workers;
using Xunit;

namespace AtlasNOC.Tests.Unit;

public class NotificationDeliveryTests
{
    private static NotificationDelivery CreateDelivery()
    {
        var alert = new Alert(Guid.NewGuid(), "Device", Guid.NewGuid().ToString(),
            "availability", 0, 50, AlertSeverity.Critical);
        return new NotificationDelivery(alert.Id, Guid.NewGuid());
    }

    [Fact]
    public void FailedDelivery_UsesBackoffAndEventuallyDeadLetters()
    {
        var delivery = CreateDelivery();
        var now = new DateTime(2026, 9, 1, 12, 0, 0, DateTimeKind.Utc);

        delivery.BeginAttempt(now);
        delivery.MarkFailed("HTTP 500", now, maxAttempts: 2);
        Assert.Equal(NotificationDeliveryState.Failed, delivery.State);
        Assert.Equal(now.AddMinutes(1), delivery.NextRetryUtc);

        delivery.BeginAttempt(now.AddMinutes(1));
        delivery.MarkFailed("HTTP 500", now.AddMinutes(1), maxAttempts: 2);
        Assert.Equal(NotificationDeliveryState.DeadLetter, delivery.State);
        Assert.Null(delivery.SentAtUtc);
    }

    [Fact]
    public void UnsupportedChannel_IsNeverMarkedSent()
    {
        var delivery = CreateDelivery();
        delivery.BeginAttempt(DateTime.UtcNow);

        delivery.MarkUnsupported(NotificationChannelType.Email);

        Assert.Equal(NotificationDeliveryState.DeadLetter, delivery.State);
        Assert.Contains("Unsupported", delivery.LastError);
        Assert.Null(delivery.SentAtUtc);
    }

    [Fact]
    public void OnlySendingDeliveryCanBecomeSent()
    {
        var delivery = CreateDelivery();
        Assert.Throws<InvalidOperationException>(() => delivery.MarkSent(DateTime.UtcNow));

        delivery.BeginAttempt(DateTime.UtcNow);
        delivery.MarkSent(DateTime.UtcNow);

        Assert.Equal(NotificationDeliveryState.Sent, delivery.State);
        Assert.NotNull(delivery.SentAtUtc);
    }

    [Theory]
    [InlineData("https://hooks.example.test/atlas")]
    [InlineData("{\"url\":\"https://hooks.example.test/atlas\"}")]
    public void WebhookConfiguration_AcceptsRawOrJsonUrl(string configuration)
        => Assert.Equal("https", NotificationWorker.ParseWebhookUri(configuration).Scheme);

    [Theory]
    [InlineData("{}")]
    [InlineData("file:///tmp/hook")]
    [InlineData("not-a-url")]
    public void InvalidWebhookConfiguration_IsRejected(string configuration)
        => Assert.ThrowsAny<Exception>(() => NotificationWorker.ParseWebhookUri(configuration));
}
