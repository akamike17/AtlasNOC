using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace AtlasNOC.Infrastructure.Persistence;

/// <summary>
/// Fábrica de diseño para `dotnet ef`. Permite generar/ejecutar migraciones sin
/// depender del host Web/Worker. Usa la cadena de conexión por defecto para desarrollo.
/// </summary>
public class AtlasNOCDbContextFactory : IDesignTimeDbContextFactory<AtlasNOCDbContext>
{
    public AtlasNOCDbContext CreateDbContext(string[] args)
    {
        var connectionString =
            Environment.GetEnvironmentVariable("ConnectionStrings__DefaultConnection")
            ?? "Server=127.0.0.1;Port=3306;Database=atlasnoc_rebuild;User=Admin;Password=RenacerGood17;";

        var options = new DbContextOptionsBuilder<AtlasNOCDbContext>()
            .UseMySql(connectionString, ServerVersion.Parse("8.0.36-mysql"))
            .Options;

        return new AtlasNOCDbContext(options);
    }
}