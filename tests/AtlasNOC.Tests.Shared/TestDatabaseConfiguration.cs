namespace AtlasNOC.Tests.Shared;

/// <summary>
/// Resuelve de forma segura la cadena de conexión para bases de datos de test.
///
/// Responsabilidad (especificación §1):
///  - leer únicamente la variable de entorno <c>ATLASNOC_TEST_CONNECTION</c>;
///  - validar que el nombre de la base contenga <c>_test</c> o <c>_e2e</c>;
///  - impedir que fixtures destructivas apunten a la base de producción
///    (<c>atlasnoc_rebuild</c> o cualquier otro nombre no reconocido como de test);
///  - devolver la conexión sólo para bases de prueba.
///
/// Nunca se devuelve una contraseña de respaldo real: si la variable no existe
/// o apunta a una base no de test, <see cref="Resolve"/> lanza <see cref="InvalidOperationException"/>
/// con un mensaje claro, de modo que los tests de Integration/Runtime/E2E queden omitidos
/// o fallen explícitamente en lugar de tocar la base de producción.
/// </summary>
public static class TestDatabaseConfiguration
{
    public const string EnvironmentVariableName = "ATLASNOC_TEST_CONNECTION";

    /// <summary>
    /// Devuelve la cadena de conexión de test validada, o lanza si no está disponible
    /// o no apunta a una base de test.
    /// </summary>
    public static string Resolve()
    {
        var connectionString = Environment.GetEnvironmentVariable(EnvironmentVariableName);

        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException(
                $"La variable de entorno '{EnvironmentVariableName}' no está definida. " +
                "Define una cadena de conexión hacia una base de test dedicada " +
                "(el nombre de la base debe contener '_test' o '_e2e') antes de ejecutar " +
                "los tests de Integration/Runtime/E2E.");
        }

        ValidatePointsToTestDatabase(connectionString, notify: connectionString);
        return connectionString;
    }

    /// <summary>
    /// Devuelve la cadena de conexión de test, o <c>null</c> si la variable no está definida.
    /// Útil para marcar tests como omitidos con un mensaje claro sin lanzar en el constructor.
    /// </summary>
    public static string? TryResolve()
    {
        var connectionString = Environment.GetEnvironmentVariable(EnvironmentVariableName);
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            return null;
        }

        ValidatePointsToTestDatabase(connectionString, notify: connectionString);
        return connectionString;
    }

    /// <summary>
    /// Lanza <see cref="InvalidOperationException"/> si la base de la cadena no es de test.
    /// </summary>
    public static void ValidatePointsToTestDatabase(string connectionString, string? notify = null)
    {
        var database = ExtractDatabaseName(connectionString);

        if (string.IsNullOrWhiteSpace(database))
        {
            throw new InvalidOperationException(
                $"La cadena de conexión de test ('{EnvironmentVariableName}') no declara un nombre de base de datos " +
                "explícito (clave 'Database=').");
        }

        var normalized = database.Trim();
        if (!(normalized.Contains("_test", StringComparison.OrdinalIgnoreCase)
              || normalized.Contains("_e2e", StringComparison.OrdinalIgnoreCase)))
        {
            throw new InvalidOperationException(
                $"La base de datos '{normalized}' no parece ser una base de test. " +
                "Los tests destructivos sólo pueden apuntar a bases cuyo nombre contenga '_test' o '_e2e'. " +
                $"Conexión: (sanitizada)");
        }
    }

    /// <summary>
    /// Extrae el nombre de la base de datos de una cadena tipo ADO.NET/MySqlConnector.
    /// Acepta claves <c>Database=</c> y <c>Initial Catalog=</c>, en cualquier posición.
    /// </summary>
    public static string? ExtractDatabaseName(string connectionString)
    {
        foreach (var part in connectionString.Split(';', StringSplitOptions.RemoveEmptyEntries))
        {
            var idx = part.IndexOf('=');
            if (idx <= 0)
            {
                continue;
            }

            var key = part[..idx].Trim();
            var value = part[(idx + 1)..].Trim();

            if (string.Equals(key, "Database", StringComparison.OrdinalIgnoreCase)
                || string.Equals(key, "Initial Catalog", StringComparison.OrdinalIgnoreCase))
            {
                return value;
            }
        }

        return null;
    }

    /// <summary>
    /// Devuelve una copia de la cadena de conexión apuntando a una base de test
    /// distinta (añade un sufijo al nombre), para aislar múltiples fixtures
    /// (WebApplicationFactory) que de otro modo competirían por la misma base.
    /// </summary>
    public static string WithDatabaseSuffix(string connectionString, string suffix)
    {
        var database = ExtractDatabaseName(connectionString);
        if (string.IsNullOrWhiteSpace(database))
        {
            throw new InvalidOperationException("No se pudo determinar el nombre de base para añadir un sufijo.");
        }

        return ReplaceKeyValue(connectionString, "Database", database + suffix);
    }

    private static string ReplaceKeyValue(string connectionString, string key, string newValue)
    {
        var parts = connectionString.Split(';');
        for (var i = 0; i < parts.Length; i++)
        {
            var idx = parts[i].IndexOf('=');
            if (idx <= 0) continue;
            if (string.Equals(parts[i][..idx].Trim(), key, StringComparison.OrdinalIgnoreCase))
            {
                parts[i] = key + "=" + newValue;
            }
        }
        return string.Join(';', parts);
    }
}