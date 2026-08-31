using Xunit;
using Xunit.Sdk;

namespace AtlasNOC.Tests.Shared;

/// <summary>
/// <c>Fact</c> que omite el test (marcándolo como "Skipped") cuando se lanza
/// <see cref="SkipTestException"/>. Igual que <seealso cref="SkippableFactAttribute"/>
/// del ecosistema xunit v2, sin depender de un paquete externo.
/// </summary>
[XunitTestCaseDiscoverer("AtlasNOC.Tests.Shared.SkippableFactDiscoverer", "AtlasNOC.Tests.Shared")]
public sealed class SkippableFactAttribute : FactAttribute
{
}

/// <summary>
/// <c>Theory</c> que omite el test cuando se lanza <see cref="SkipTestException"/>.
/// </summary>
[XunitTestCaseDiscoverer("AtlasNOC.Tests.Shared.SkippableTheoryDiscoverer", "AtlasNOC.Tests.Shared")]
public sealed class SkippableTheoryAttribute : TheoryAttribute
{
}