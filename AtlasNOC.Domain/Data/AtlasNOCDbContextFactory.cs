using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace AtlasNOC.Domain.Data;

public sealed class AtlasNOCDbContextFactory : IDesignTimeDbContextFactory<AtlasNOCDbContext>
{
    public AtlasNOCDbContext CreateDbContext(string[] args)
    {
        var connectionString = Environment.GetEnvironmentVariable("ConnectionStrings__DefaultConnection")
            ?? "Server=127.0.0.1;Database=atlasnoc_design;User=design;Password=design;";
        var options = new DbContextOptionsBuilder<AtlasNOCDbContext>()
            .UseMySql(
                connectionString,
                new MySqlServerVersion(new Version(8, 0, 0)),
                mysql => mysql.MigrationsAssembly("AtlasNOC.Domain"))
            .Options;
        return new AtlasNOCDbContext(options);
    }
}
