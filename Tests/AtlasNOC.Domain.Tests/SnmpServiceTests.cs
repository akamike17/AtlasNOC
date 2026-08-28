using AtlasNOC.Domain.Entities;
using AtlasNOC.Domain.Enums;
using AtlasNOC.Domain.Services;
using System.Net;
using Xunit;

namespace AtlasNOC.Domain.Tests;

public sealed class SnmpServiceTests
{
    private readonly PassthroughCredentialProtector _protector = new();
    private readonly SnmpService _service;
    private static readonly IPAddress Loopback = IPAddress.Loopback;

    public SnmpServiceTests()
    {
        _service = new SnmpService(_protector);
    }

    [Fact]
    public async Task TestConnection_V3BuildsSecureRequestFromProtectedSecrets()
    {
        var credential = Credential.CreateV3("v3", "user", "SHA256",
            "protected-auth", "AES", "protected-privacy", "test");

        var result = await _service.TestConnectionAsync(Loopback, credential, TimeSpan.FromMilliseconds(50));

        Assert.False(result.Success);
        Assert.DoesNotContain("Invalid credential", result.ErrorMessage);
        Assert.DoesNotContain("Unsupported", result.ErrorMessage);
        Assert.Equal(2, _protector.UnprotectCallCount);
    }

    [Fact]
    public async Task GetAsync_InvalidOid_ReturnsFailureWithoutSendingTraffic()
    {
        var credential = Credential.CreateV2c("v2", "public", "test");

        var result = await _service.GetAsync(Loopback, credential, "not-an-oid", TimeSpan.FromMilliseconds(50));

        Assert.False(result.Success);
        Assert.Equal(-3, result.ErrorStatus);
        Assert.NotEmpty(result.ErrorMessage!);
        Assert.Equal(0, _protector.UnprotectCallCount);
    }

    [Fact]
    public async Task GetAsync_PreCanceledToken_PropagatesCancellation()
    {
        var credential = Credential.CreateV2c("v2", "public", "test");
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            _service.GetAsync(Loopback, credential, "1.3.6.1.2.1.1.1.0",
                TimeSpan.FromSeconds(1), cancellation.Token));
    }

    [Fact]
    public async Task SetAsync_IsDeniedByDefault()
    {
        var credential = Credential.CreateV2c("v2", "private", "test");

        var result = await _service.SetAsync(Loopback, credential,
            "1.3.6.1.2.1.1.5.0", "name", TimeSpan.FromMilliseconds(50));

        Assert.False(result.Success);
        Assert.Contains("disabled", result.ErrorMessage, StringComparison.OrdinalIgnoreCase);
    }
}
