using AtlasNOC.Application.Repositories;
using AtlasNOC.Infrastructure.Persistence;

namespace AtlasNOC.Infrastructure.Persistence;

public class EfUnitOfWork : IUnitOfWork
{
    private readonly AtlasNOCDbContext _context;

    public EfUnitOfWork(AtlasNOCDbContext context)
    {
        _context = context;
    }

    public Task<int> SaveChangesAsync(CancellationToken ct = default)
        => _context.SaveChangesAsync(ct);
}