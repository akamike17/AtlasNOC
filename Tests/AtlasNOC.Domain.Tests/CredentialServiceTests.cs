using System;
using System.Threading.Tasks;
using AtlasNOC.Domain.Entities;
using AtlasNOC.Domain.Enums;
using AtlasNOC.Domain.Services;
using AtlasNOC.Domain.Services.Interfaces;
using AtlasNOC.Domain.ValueObjects;
using Xunit;

namespace AtlasNOC.Domain.Tests;

public class CredentialServiceTests
{
    private readonly InMemoryRepository<Credential> _repo;
    private readonly TestLogger<AuditService> _auditLogger;
    private readonly InMemoryRepository<AuditEvent> _auditRepo;
    private readonly AuditService _auditService;
    private readonly ICredentialProtector _credentialProtector;
    private readonly CredentialService _credentialService;

    public CredentialServiceTests()
    {
        _repo = new InMemoryRepository<Credential>(c => c.Id.Value);
        _auditRepo = new InMemoryRepository<AuditEvent>(e => e.EventId);
        _auditLogger = new TestLogger<AuditService>();
        _auditService = new AuditService(_auditRepo, _auditLogger);
        _credentialProtector = new TestCredentialProtector();
        _credentialService = new CredentialService(_repo, _auditService, _credentialProtector);
    }

    private sealed class TestCredentialProtector : ICredentialProtector
    {
        public string Protect(string plaintext) => $"protected:{plaintext}";
        public string Unprotect(string ciphertext) => ciphertext.Replace("protected:", "");
        public byte[] ProtectBytes(byte[] plaintext) => plaintext;
        public byte[] UnprotectBytes(byte[] ciphertext) => ciphertext;
    }

    [Fact]
    public async Task CreateV2cAsync_ShouldCreateCredential_WithCommunityStored()
    {
        var credential = await _credentialService.CreateV2cAsync(
            "Public-Community", "public-string", "admin",
            expiresAt: DateTime.UtcNow.AddYears(1));

        Assert.NotNull(credential);
        Assert.Equal(SnmpVersion.V2c, credential.Version);
        Assert.Equal("Public-Community", credential.Name);
        Assert.Equal("protected:public-string", credential.Community);
        Assert.True(credential.IsActive);
        Assert.False(credential.IsExpired);
    }

    [Fact]
    public async Task CreateV3Async_ShouldCreateCredential_WithProtectedPasswords()
    {
        var credential = await _credentialService.CreateV3Async(
            "V3-Credential", "snmp-user", "SHA256", "auth-pass-123", "AES", "priv-pass-456", "admin");

        Assert.NotNull(credential);
        Assert.Equal(SnmpVersion.V3, credential.Version);
        Assert.Equal("snmp-user", credential.UserName);
        Assert.Equal("SHA256", credential.AuthProtocol);
        Assert.Equal("AES128", credential.PrivProtocol);
        Assert.Equal("protected:auth-pass-123", credential.ProtectedAuthPassword);
        Assert.Equal("protected:priv-pass-456", credential.ProtectedPrivPassword);
        Assert.True(credential.IsActive);
    }

    [Fact]
    public async Task GetExpiredAsync_ShouldReturnExpiredCredentials()
    {
        await _credentialService.CreateV2cAsync(
            "Expired-Cred", "community", "admin",
            expiresAt: DateTime.UtcNow.AddDays(-1));
        await _credentialService.CreateV2cAsync(
            "Active-Cred", "community", "admin",
            expiresAt: DateTime.UtcNow.AddYears(1));

        var expired = await _credentialService.GetExpiredAsync();
        Assert.Single(expired);
        Assert.Equal("Expired-Cred", expired.First().Name);
    }

    [Fact]
    public async Task RotateV2cAsync_ShouldUpdateCommunity()
    {
        var credential = await _credentialService.CreateV2cAsync(
            "Rotatable", "old-community", "admin");
        await _credentialService.RotateV2cAsync(credential.Id, "new-community", "admin");
        var found = await _credentialService.GetByIdAsync(credential.Id);
        Assert.NotNull(found);
        Assert.Equal("protected:new-community", found.Community);
        Assert.NotNull(found.LastRotatedAt);
    }

    [Fact]
    public async Task DeactivateAsync_ShouldSetIsActiveToFalse()
    {
        var credential = await _credentialService.CreateV2cAsync(
            "Deactivatable", "community", "admin");
        await _credentialService.DeactivateAsync(credential.Id, "admin");
        var found = await _credentialService.GetByIdAsync(credential.Id);
        Assert.NotNull(found);
        Assert.False(found.IsActive);
    }

    [Fact]
    public async Task RotateV3AuthAsync_ShouldUpdateProtectedAuthPassword()
    {
        var credential = await _credentialService.CreateV3Async(
            "V3-Cred", "user", "SHA256", "old-auth-password", "AES",
            "privacy-password", "admin");

        await _credentialService.RotateV3AuthAsync(credential.Id, "new-auth-password", "admin");
        var found = await _credentialService.GetByIdAsync(credential.Id);
        Assert.NotNull(found);
        Assert.Equal("protected:new-auth-password", found.ProtectedAuthPassword);
    }
}
