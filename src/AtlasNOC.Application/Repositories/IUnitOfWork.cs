namespace AtlasNOC.Application.Repositories;

/// <summary>Unit of work wrapping persistence across repositories.</summary>
public interface IUnitOfWork
{
    Task<int> SaveChangesAsync(CancellationToken ct = default);
}