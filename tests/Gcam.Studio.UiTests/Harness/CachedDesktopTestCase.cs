using Xunit.Abstractions;
using Xunit.Sdk;

namespace Gcam.Studio.UiTests.Harness;

/// <summary>
/// A desktop case that consults the result cache (<see cref="ResultCache"/>): with <c>GCAM_UI_CACHE=1</c> a matching
/// recent pass makes it a skip with the fixed reuse reason, decided before Studio could start; otherwise it runs as
/// before, and its final xUnit outcome — after every cleanup assertion and, for a manual chapter, its finalised record —
/// records or removes the case's cache entry.
/// </summary>
public sealed class CachedDesktopTestCase : XunitTestCase
{
    [Obsolete("Called by the de-serializer; should only be called by deriving classes for de-serialization purposes")]
    public CachedDesktopTestCase() { }

    public CachedDesktopTestCase(IMessageSink diagnosticMessageSink, TestMethodDisplay defaultMethodDisplay,
        TestMethodDisplayOptions defaultMethodDisplayOptions, ITestMethod testMethod, object[]? testMethodArguments = null)
        : base(diagnosticMessageSink, defaultMethodDisplay, defaultMethodDisplayOptions, testMethod, testMethodArguments) { }

    private Type RuntimeClass => ((IReflectionTypeInfo)TestMethod.TestClass.Class).Type;

    /// <summary>The opt-in skip comes first, so with <c>GCAM_UI_TESTS</c> unset the cache is never read.</summary>
    protected override string? GetSkipReason(IAttributeInfo factAttribute) =>
        base.GetSkipReason(factAttribute) ?? ResultCache.SkipReason(RuntimeClass, TestMethod.Method.Name, TestMethodArguments);

    public override async Task<RunSummary> RunAsync(IMessageSink diagnosticMessageSink, IMessageBus messageBus, object[] constructorArguments,
        ExceptionAggregator aggregator, CancellationTokenSource cancellationTokenSource)
    {
        if (SkipReason is not null) return await base.RunAsync(diagnosticMessageSink, messageBus, constructorArguments, aggregator, cancellationTokenSource);
        var attempt = ResultCache.Begin(RuntimeClass, TestMethod.Method.Name, TestMethodArguments);
        RunSummary summary;
        try { summary = await base.RunAsync(diagnosticMessageSink, messageBus, constructorArguments, aggregator, cancellationTokenSource); }
        catch (Exception)
        {
            ResultCache.End(attempt, passed: false);
            throw;
        }
        string outcome = ResultCache.End(attempt, summary.Total > 0 && summary.Failed == 0 && summary.Skipped == 0);
        diagnosticMessageSink.OnMessage(new DiagnosticMessage($"{DisplayName}: {outcome}"));
        return summary;
    }
}
