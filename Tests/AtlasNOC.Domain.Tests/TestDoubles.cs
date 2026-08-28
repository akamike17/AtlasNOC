using AtlasNOC.Domain.Services.Interfaces;
using Microsoft.Extensions.Logging;

namespace AtlasNOC.Domain.Tests;

internal sealed class InMemoryRepository<T> : IRepository<T> where T : class
{
    private readonly Func<T, Guid> _keySelector;
    private readonly Dictionary<Guid, T> _items = new();

    public InMemoryRepository(Func<T, Guid> keySelector) =>
        _keySelector = keySelector ?? throw new ArgumentNullException(nameof(keySelector));

    public Task<T?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(_items.GetValueOrDefault(id));
    }

    public Task<IReadOnlyList<T>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult<IReadOnlyList<T>>(_items.Values.ToList().AsReadOnly());
    }

    public Task AddAsync(T entity, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!_items.TryAdd(_keySelector(entity), entity))
            throw new InvalidOperationException("Duplicate entity key.");
        return Task.CompletedTask;
    }

    public Task UpdateAsync(T entity, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        _items[_keySelector(entity)] = entity;
        return Task.CompletedTask;
    }

    public Task DeleteAsync(T entity, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        _items.Remove(_keySelector(entity));
        return Task.CompletedTask;
    }

    public Task<int> CountAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(_items.Count);
    }
}

internal sealed class TestLogger<T> : ILogger<T>
{
    public List<(LogLevel Level, string Message, Exception? Exception)> Logs { get; } = new();

    public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
    public bool IsEnabled(LogLevel logLevel) => true;

    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state,
        Exception? exception, Func<TState, Exception?, string> formatter) =>
        Logs.Add((logLevel, formatter(state, exception), exception));
}

internal sealed class PassthroughCredentialProtector : ICredentialProtector
{
    public int UnprotectCallCount { get; private set; }

    public string Protect(string plaintext) => plaintext;
    public string Unprotect(string ciphertext)
    {
        UnprotectCallCount++;
        return ciphertext;
    }
    public byte[] ProtectBytes(byte[] plaintext) => plaintext.ToArray();
    public byte[] UnprotectBytes(byte[] ciphertext) => ciphertext.ToArray();
}
