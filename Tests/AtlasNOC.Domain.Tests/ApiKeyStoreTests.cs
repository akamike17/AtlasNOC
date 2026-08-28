using AtlasNOC.Domain.Entities;
using AtlasNOC.Domain.Services;
using Xunit;

namespace AtlasNOC.Domain.Tests;

public sealed class ApiKeyStoreTests
{
    [Fact]
    public async Task CreateKeyAsync_GeneratesSecretAndStoresOnlyHash()
    {
        var repository = new InMemoryRepository<ApiKey>(key => key.Id);
        var store = new ApiKeyStore(repository);

        var created = await store.CreateKeyAsync("operator", "automation", "NocOperator");
        var persisted = Assert.Single(await repository.GetAllAsync());

        Assert.StartsWith("atlasnoc_", created.PlaintextKey);
        Assert.Equal(73, created.PlaintextKey.Length);
        Assert.DoesNotContain(created.PlaintextKey, persisted.KeyHash, StringComparison.Ordinal);
        Assert.Equal(64, persisted.KeyHash.Length);
        Assert.Equal("NocOperator", persisted.Role);
        Assert.NotNull(await store.ValidateAsync(created.PlaintextKey));
    }

    [Fact]
    public async Task CreateKeyAsync_GeneratesUniqueSecrets()
    {
        var repository = new InMemoryRepository<ApiKey>(key => key.Id);
        var store = new ApiKeyStore(repository);

        var first = await store.CreateKeyAsync("one", "", "ReadOnly");
        var second = await store.CreateKeyAsync("two", "", "ReadOnly");

        Assert.NotEqual(first.PlaintextKey, second.PlaintextKey);
    }

    [Fact]
    public async Task ValidateAsync_RejectsUnknownKey()
    {
        var repository = new InMemoryRepository<ApiKey>(key => key.Id);
        var store = new ApiKeyStore(repository);

        Assert.Null(await store.ValidateAsync("atlasnoc_invalid"));
    }

    [Fact]
    public async Task BootstrapAdministratorAsync_CreatesOnlyOneInitialAdministrator()
    {
        var repository = new InMemoryRepository<ApiKey>(key => key.Id);
        var store = new ApiKeyStore(repository);
        const string secret = "bootstrap-secret-with-more-than-32-characters";

        Assert.True(await store.BootstrapAdministratorAsync(secret, "initial-admin"));
        Assert.False(await store.BootstrapAdministratorAsync(secret + "-different", "other-admin"));

        var key = Assert.Single(await repository.GetAllAsync());
        Assert.Equal("Administrator", key.Role);
        Assert.NotEqual(secret, key.KeyHash);
        Assert.NotNull(await store.ValidateAsync(secret));
    }
}
