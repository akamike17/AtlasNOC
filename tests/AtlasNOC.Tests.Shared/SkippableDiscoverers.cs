using Xunit.Abstractions;
using Xunit.Sdk;

namespace AtlasNOC.Tests.Shared;

/// <summary>
/// Discoverer de casos para <see cref="SkippableFactAttribute"/>. Reutiliza la
/// lógica estándar de <see cref="FactDiscoverer"/>; la omisión real ocurre en el
/// runner cuando detecta <see cref="SkipTestException"/>.
/// </summary>
public sealed class SkippableFactDiscoverer : IXunitTestCaseDiscoverer
{
    private readonly IMessageSink _diagnosticMessageSink;

    public SkippableFactDiscoverer(IMessageSink diagnosticMessageSink)
    {
        _diagnosticMessageSink = diagnosticMessageSink;
    }

    public IEnumerable<IXunitTestCase> Discover(
        ITestFrameworkDiscoveryOptions discoveryOptions,
        ITestMethod testMethod,
        IAttributeInfo factAttribute)
    {
        yield return new SkippableTestCase(
            _diagnosticMessageSink,
            discoveryOptions.MethodDisplayOrDefault(),
            discoveryOptions.MethodDisplayOptionsOrDefault(),
            testMethod);
    }
}

/// <summary>Discoverer para <see cref="SkippableTheoryAttribute"/>.</summary>
public sealed class SkippableTheoryDiscoverer : IXunitTestCaseDiscoverer
{
    private readonly IMessageSink _diagnosticMessageSink;

    public SkippableTheoryDiscoverer(IMessageSink diagnosticMessageSink)
    {
        _diagnosticMessageSink = diagnosticMessageSink;
    }

    public IEnumerable<IXunitTestCase> Discover(
        ITestFrameworkDiscoveryOptions discoveryOptions,
        ITestMethod testMethod,
        IAttributeInfo factAttribute)
    {
        // Delegamos en el descubrimiento estándar de Theory para respetar InlineData/MemberData.
        var theoryDiscoverer = new TheoryDiscoverer(_diagnosticMessageSink);
        foreach (var tc in theoryDiscoverer.Discover(discoveryOptions, testMethod, factAttribute))
        {
            yield return tc;
        }
    }
}

/// <summary>XunitTestCase que convierte <see cref="SkipTestException"/> en un skip.</summary>
public sealed class SkippableTestCase : XunitTestCase
{
    [Obsolete("Called by the de-serializer; should only be called by deriving classes for de-serialization purposes")]
    public SkippableTestCase()
    {
    }

    public SkippableTestCase(
        IMessageSink diagnosticMessageSink,
        TestMethodDisplay defaultMethodDisplay,
        TestMethodDisplayOptions defaultMethodDisplayOptions,
        ITestMethod testMethod,
        object[]? testMethodArguments = null)
        : base(diagnosticMessageSink, defaultMethodDisplay, defaultMethodDisplayOptions, testMethod, testMethodArguments)
    {
    }

    public override async Task<RunSummary> RunAsync(
        IMessageSink diagnosticMessageSink,
        IMessageBus messageBus,
        object[] constructorArguments,
        ExceptionAggregator aggregator,
        CancellationTokenSource cancellationTokenSource)
    {
        var skipMessageBus = new SkippableMessageBus(messageBus);
        var result = await base.RunAsync(
            diagnosticMessageSink,
            skipMessageBus,
            constructorArguments,
            aggregator,
            cancellationTokenSource);

        if (skipMessageBus.DynamicallySkippedTestCount > 0)
        {
            result.Failed -= skipMessageBus.DynamicallySkippedTestCount;
            result.Skipped += skipMessageBus.DynamicallySkippedTestCount;
        }

        return result;
    }
}

/// <summary>
/// MessageBus que intercepta <see cref="SkipTestException"/> (llegado como fallo)
/// y lo recalifica como skip dinámico a nivel de corredor.
/// </summary>
public sealed class SkippableMessageBus : IMessageBus
{
    private readonly IMessageBus _inner;

    public SkippableMessageBus(IMessageBus inner) => _inner = inner;

    public int DynamicallySkippedTestCount { get; private set; }

    public void Dispose() { }

    public bool QueueMessage(IMessageSinkMessage message)
    {
        if (message is ITestFailed failed
            && failed.ExceptionTypes.Length == 1
            && failed.ExceptionTypes[0] == typeof(SkipTestException).FullName)
        {
            DynamicallySkippedTestCount++;
            return _inner.QueueMessage(new TestSkipped(failed.Test, failed.Messages[0]));
        }

        return _inner.QueueMessage(message);
    }
}