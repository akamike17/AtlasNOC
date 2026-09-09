using AtlasNOC.Tests.Shared;
using Xunit;

namespace AtlasNOC.Tests.Unit;

/// <summary>
/// Prueba de regresión de seguridad (§1): ninguna cadena de conexión versionada
/// en el repositorio puede contener credenciales reales (<c>Password=</c>,
/// <c>Pwd=</c> o <c>User Id=</c>).
///
/// Se analizan sólo líneas que aparentan ser cadenas de conexión (declaran un
/// servidor/base mediante <c>Server=</c>, <c>Host=</c> o <c>Database=</c>),
/// para no dar falsos positivos en documentación o en el propio código de la
/// prueba. Los archivos Markdown de documentación y el archivo de esta prueba
/// quedan excluidos explícitamente.
/// </summary>
public class VersionedConnectionStringSecretsTests
{
    private const string RepoRootMarker = "AtlasNOC.sln";

    // Claves que indican presencia de un secreto o usuario en una cadena de conexión.
    private static readonly string[] ForbiddenTokens =
    {
        "Password=",
        "Pwd=",
        "User Id=",
    };

    // Claves que indican que una línea es una cadena de conexión ADO.NET/MySql.
    private static readonly string[] ConnectionStringMarkers =
    {
        "Server=",
        "Host=",
        "Database=",
    };

    // Rutas/carpetas que se ignoran (build outputs, VCS, etc.).
    private static readonly string[] ExcludedDirectories =
    {
        ".git",
        "bin",
        "obj",
        "node_modules",
    };

    // Archivos de documentación y el propio archivo de esta prueba (contienen los
    // literales como parte de la especificación, no como cadenas de conexión reales).
    private static readonly string[] ExcludedFilePatterns =
    {
        "rev.md",
        "F11_GAP_AUDIT.md",
        "VersionedConnectionStringSecretsTests.cs",
    };

    [Fact]
    public void No_versioned_connection_string_contains_credentials()
    {
        var repoRoot = FindRepoRoot();
        var violations = new List<string>();

        foreach (var file in EnumerateTrackedTextFiles(repoRoot))
        {
            var relative = Path.GetRelativePath(repoRoot, file).Replace('\\', '/');

            if (ExcludedFilePatterns.Any(p =>
                    relative.EndsWith(p, StringComparison.OrdinalIgnoreCase)))
            {
                continue;
            }

            var lines = File.ReadAllLines(file);
            for (var i = 0; i < lines.Length; i++)
            {
                var line = lines[i];
                if (!LooksLikeConnectionString(line))
                {
                    continue;
                }

                foreach (var forbidden in ForbiddenTokens)
                {
                    if (line.Contains(forbidden, StringComparison.OrdinalIgnoreCase))
                    {
                        violations.Add($"{relative}:{i + 1} contiene '{forbidden}'");
                    }
                }
            }
        }

        Assert.True(
            violations.Count == 0,
            "Se encontraron cadenas de conexión con credenciales en archivos versionados:\n  "
            + string.Join("\n  ", violations));
    }

    [Fact]
    public void Test_database_configuration_rejects_production_database_names()
    {
        // 'atlasnoc_rebuild' (producción) no debe pasar la validación de base de test.
        var prod = "Server=127.0.0.1;Port=3306;Database=atlasnoc_rebuild;User=Admin;Password=x;";
        Assert.Throws<InvalidOperationException>(
            () => TestDatabaseConfiguration.ValidatePointsToTestDatabase(prod));

        // Una base '_test' sí es aceptada.
        var test = "Server=127.0.0.1;Port=3306;Database=atlasnoc_integration_test;User=Admin;Password=x;";
        TestDatabaseConfiguration.ValidatePointsToTestDatabase(test); // no lanza
    }

    [Fact]
    public void Test_database_configuration_sanitizes_error_message_no_password()
    {
        // El mensaje de error no debe contener el password ni user id
        var connWithSecret = "Server=127.0.0.1;Port=3306;Database=production_db;User=Admin;Password=SecretPassword123;";
        var ex = Assert.Throws<InvalidOperationException>(
            () => TestDatabaseConfiguration.ValidatePointsToTestDatabase(connWithSecret));

        Assert.DoesNotContain("SecretPassword123", ex.Message);
        Assert.DoesNotContain("Admin", ex.Message);
        Assert.DoesNotContain("Password=", ex.Message);
        Assert.DoesNotContain("User=", ex.Message);
        Assert.Contains("production_db", ex.Message); // database name is safe to show
        Assert.Contains("(sanitizada)", ex.Message);
    }

    [Fact]
    public void Test_database_configuration_extracts_database_name()
    {
        Assert.Equal("atlasnoc_e2e_test",
            TestDatabaseConfiguration.ExtractDatabaseName(
                "Server=127.0.0.1;Database=atlasnoc_e2e_test;User=u;Password=p;"));

        Assert.Equal("db",
            TestDatabaseConfiguration.ExtractDatabaseName(
                "Host=127.0.0.1;Initial Catalog=db;"));
    }

    private static bool LooksLikeConnectionString(string line)
    {
        return ConnectionStringMarkers.Any(m =>
            line.Contains(m, StringComparison.OrdinalIgnoreCase));
    }

    private static string FindRepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, RepoRootMarker)))
        {
            dir = dir.Parent;
        }

        return dir?.FullName
            ?? throw new InvalidOperationException($"No se encontró '{RepoRootMarker}'.");
    }

    private static IEnumerable<string> EnumerateTrackedTextFiles(string root)
    {
        foreach (var file in Directory.EnumerateFiles(root, "*.*", SearchOption.AllDirectories))
        {
            var relative = Path.GetRelativePath(root, file);
            if (ExcludedDirectories.Any(d =>
                    relative.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                        .Contains(d, StringComparer.OrdinalIgnoreCase)))
            {
                continue;
            }

            yield return file;
        }
    }
}