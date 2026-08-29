using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using AtlasNOC.Domain.Data;
using AtlasNOC.Domain.Services.Interfaces;

namespace AtlasNOC.Domain.Services;

/// <summary>
/// Implementación de IRepository&lt;T&gt; basada en EF Core / DbContext.
/// </summary>
public class EfCoreRepository<T> : IRepository<T> where T : class
{
    private readonly AtlasNOCDbContext _dbContext;
    private readonly DbSet<T> _dbSet;

    public EfCoreRepository(AtlasNOCDbContext dbContext)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        _dbSet = dbContext.Set<T>();
    }

    public async Task<T?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
        {
            // Resolve the primary key to the entity's CLR key type. Entities whose key is a
            // value object (DeviceId, AlertId, CredentialId) store a Guid via a value
            // converter; FindAsync requires the model type (e.g. DeviceId), not the Guid.
            object? keyValue = id;
            var entityType = _dbContext.Model.FindEntityType(typeof(T));
            var keyProperty = entityType?.FindPrimaryKey()?.Properties.FirstOrDefault();
            var converter = keyProperty?.GetValueConverter();
            if (converter is not null)
            {
                keyValue = converter.ConvertFromProvider(id);
            }

            return await _dbSet.FindAsync(new[] { keyValue }, cancellationToken);
        }

    public async Task<IReadOnlyList<T>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        return await _dbSet.ToListAsync(cancellationToken);
    }

    public async Task AddAsync(T entity, CancellationToken cancellationToken = default)
    {
        await _dbSet.AddAsync(entity, cancellationToken);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task UpdateAsync(T entity, CancellationToken cancellationToken = default)
    {
        _dbSet.Update(entity);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task DeleteAsync(T entity, CancellationToken cancellationToken = default)
    {
        _dbSet.Remove(entity);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<int> CountAsync(CancellationToken cancellationToken = default)
    {
        return await _dbSet.CountAsync(cancellationToken);
    }
}
