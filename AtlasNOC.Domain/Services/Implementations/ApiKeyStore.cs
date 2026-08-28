using System.Security.Cryptography;
using System.Text;
using AtlasNOC.Domain.Entities;
using AtlasNOC.Domain.Services.Interfaces;

namespace AtlasNOC.Domain.Services;

public sealed record ApiKeyInfo(Guid Id, string Owner, string Role, string? Description, DateTime CreatedAt);

public sealed class ApiKeyStore
{
    private readonly IRepository<ApiKey> _repository;

    public ApiKeyStore(IRepository<ApiKey> repository)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
    }

    public async Task<IReadOnlyList<ApiKeyInfo>> ListActiveKeysAsync(
        CancellationToken cancellationToken = default)
    {
        var keys = await _repository.GetAllAsync(cancellationToken);
        return keys.Where(key => key.IsActive)
            .OrderByDescending(key => key.CreatedAt)
            .Select(key => new ApiKeyInfo(key.Id, key.Owner, key.Role, key.Description, key.CreatedAt))
            .ToList()
            .AsReadOnly();
    }

    public async Task<(Guid KeyId, string PlaintextKey)> CreateKeyAsync(
        string owner, string description, string role = "ReadOnly",
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(owner);

        if (role is not ("Administrator" or "NocOperator" or "ReadOnly"))
            throw new ArgumentException("Unsupported API key role.", nameof(role));
        var plaintextKey = $"atlasnoc_{Convert.ToHexString(RandomNumberGenerator.GetBytes(32)).ToLowerInvariant()}";
        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(plaintextKey)));
        var apiKey = new ApiKey(Guid.NewGuid(), owner.Trim(), description?.Trim(), hash,
            DateTime.UtcNow, true, role);
        await _repository.AddAsync(apiKey, cancellationToken);
        return (apiKey.Id, plaintextKey);
    }

    public async Task<bool> BootstrapAdministratorAsync(
        string plaintextKey, string owner,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(plaintextKey);
        ArgumentException.ThrowIfNullOrWhiteSpace(owner);
        if (plaintextKey.Length < 32)
            throw new ArgumentException("Bootstrap API key must contain at least 32 characters.", nameof(plaintextKey));

        var existing = await _repository.GetAllAsync(cancellationToken);
        if (existing.Any(key => key.IsActive)) return false;

        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(plaintextKey)));
        var apiKey = new ApiKey(Guid.NewGuid(), owner.Trim(), "Initial administrator bootstrap key",
            hash, DateTime.UtcNow, true, "Administrator");
        await _repository.AddAsync(apiKey, cancellationToken);
        return true;
    }

    public async Task<ApiKeyInfo?> ValidateAsync(string presentedKey,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(presentedKey)) return null;
        var presentedHash = SHA256.HashData(Encoding.UTF8.GetBytes(presentedKey));
        var keys = await _repository.GetAllAsync(cancellationToken);
        foreach (var key in keys.Where(candidate => candidate.IsActive))
        {
            byte[] storedHash;
            try
            {
                storedHash = Convert.FromHexString(key.KeyHash);
            }
            catch (FormatException)
            {
                continue;
            }
            if (storedHash.Length != 32) continue;
            if (CryptographicOperations.FixedTimeEquals(presentedHash, storedHash))
                return new ApiKeyInfo(key.Id, key.Owner, key.Role, key.Description, key.CreatedAt);
        }
        return null;
    }

    public async Task RevokeKeyAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var key = await _repository.GetByIdAsync(id, cancellationToken)
            ?? throw new KeyNotFoundException($"API key {id} was not found.");
        key.Revoke();
        await _repository.UpdateAsync(key, cancellationToken);
    }
}
