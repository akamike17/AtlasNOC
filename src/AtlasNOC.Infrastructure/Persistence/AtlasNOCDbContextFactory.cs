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
            Environment.GetEnvironmentVariable("ConnectionStrings__DefaultConnection");

        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException(
                "No hay cadena de conexión configurada. Define la variable de entorno " +
                "'ConnectionStrings__DefaultConnection' (o usa Secret Manager en desarrollo) " +
                "antes de ejecutar 'dotnet ef'.");
        }

        var options = new DbContextOptionsBuilder<AtlasNOCDbContext>()
            .UseMySql(connectionString, ServerVersion.Parse("8.0.36-mysql"))
            .Options;

        return new AtlasNOCDbContext(options);
    }
}