using System.Net;
using System.Text;
using AtlasNOC.Domain.Entities;
using AtlasNOC.Domain.Enums;
using AtlasNOC.Domain.Services;
using AtlasNOC.Domain.Services.Interfaces;
using Microsoft.Extensions.Options;
using Xunit;
using NotificationChannelContract = AtlasNOC.Domain.Services.Interfaces.NotificationChannel;

namespace AtlasNOC.Domain.Tests;

public sealed class NotificationServiceTests
{
    [Fact]
    public async Task RegisterChannelAsync_ProtectsConfigurationAndRedactsReads()
    {
        var repository = new InMemoryRepository<AtlasNOC.Domain.Entities.NotificationChannel>(channel => channel.Id);
        var service = CreateService(repository, new RecordingHandler());
        var input = Channel("https://hooks.example.com/secret-token");

        var registered = await service.RegisterChannelAsync(input);
        var persisted = Assert.Single(await repository.GetAllAsync());
        var listed = Assert.Single(await service.GetChannelsAsync());

        Assert.NotNull(registered);
        Assert.True(persisted.Configuration.ContainsKey("__protected"));
        Assert.DoesNotContain("secret-token", persisted.Configuration["__protected"], StringComparison.Ordinal);
        Assert.Equal("true", listed.Configuration["configured"]);
        Assert.False(listed.Configuration.ContainsKey("url"));
    }

    [Theory]
    [InlineData("http://hooks.example.com/test")]
    [InlineData("https://localhost/test")]
    [InlineData("https://127.0.0.1/test")]
    [InlineData("https://192.168.1.10/test")]
    public async Task RegisterChannelAsync_RejectsUnsafeWebhookTargets(string url)
    {
        var service = CreateService(
            new InMemoryRepository<AtlasNOC.Domain.Entities.NotificationChannel>(channel => channel.Id),
            new RecordingHandler());

        await Assert.ThrowsAsync<ArgumentException>(() => service.RegisterChannelAsync(Channel(url)));
    }

    [Fact]
    public async Task SendAsync_UsesPerRequestHeadersAndDoesNotExposeConfiguration()
    {
        var repository = new InMemoryRepository<AtlasNOC.Domain.Entities.NotificationChannel>(channel => channel.Id);
        var handler = new RecordingHandler();
        var service = CreateService(repository, handler);
        var channel = Channel("https://hooks.example.com/events") with
        {
            Configuration = new Dictionary<string, string>
            {
                ["url"] = "https://hooks.example.com/events",
                ["headers"] = "{\"Authorization\":\"Bearer secret\"}"
            }
        };
        var registered = await service.RegisterChannelAsync(channel);

        var result = await service.SendAsync(new NotificationRequest(
            "Device down", "A device is unreachable", AlertSeverity.High,
            Array.Empty<string>(), null, new[] { registered!.Id }));

        Assert.True(result.Success);
        Assert.Equal("Bearer secret", handler.Authorization);
        Assert.Null(handler.ClientDefaultAuthorization);
    }

    private static NotificationService CreateService(
        InMemoryRepository<AtlasNOC.Domain.Entities.NotificationChannel> repository,
        RecordingHandler handler)
    {
        var audit = new AuditService(
            new InMemoryRepository<AuditEvent>(item => item.EventId),
            new TestLogger<AuditService>());
        return new NotificationService(repository, audit, new TestLogger<NotificationService>(),
            Options.Create(new NotificationOptions()), new HttpClient(handler),
            new OpaqueCredentialProtector());
    }

    private static NotificationChannelContract Channel(string url) =>
        new(Guid.Empty, "webhook", NotificationChannelType.Webhook,
            new Dictionary<string, string> { ["url"] = url }, true, DateTime.UtcNow);

    private sealed class RecordingHandler : HttpMessageHandler
    {
        public string? Authorization { get; private set; }
        public string? ClientDefaultAuthorization { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Authorization = request.Headers.TryGetValues("Authorization", out var values)
                ? values.Single() : null;
            ClientDefaultAuthorization = null;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NoContent));
        }
    }

    private sealed class OpaqueCredentialProtector : ICredentialProtector
    {
        public string Protect(string plaintext) =>
            Convert.ToBase64String(Encoding.UTF8.GetBytes("protected:" + plaintext));

        public string Unprotect(string ciphertext)
        {
            var decoded = Encoding.UTF8.GetString(Convert.FromBase64String(ciphertext));
            return decoded["protected:".Length..];
        }

        public byte[] ProtectBytes(byte[] plaintext) => Encoding.UTF8.GetBytes(Protect(Convert.ToBase64String(plaintext)));
        public byte[] UnprotectBytes(byte[] ciphertext) => Convert.FromBase64String(Unprotect(Encoding.UTF8.GetString(ciphertext)));
    }
}
