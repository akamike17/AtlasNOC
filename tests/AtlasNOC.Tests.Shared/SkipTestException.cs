using Xunit.Sdk;

namespace AtlasNOC.Tests.Shared;

/// <summary>
/// Excepción lanzada para omitir un test de forma explícita cuando falta un
/// prerrequisito externo (p. ej. la base de test <c>ATLASNOC_TEST_CONNECTION</c>).
/// Compatible con xunit v2 mediante <see cref="SkippableFactAttribute"/> y
/// <see cref="SkippableTheoryAttribute"/>.
/// </summary>
public class SkipTestException : XunitException
{
    public SkipTestException(string message) : base(message)
    {
    }
}