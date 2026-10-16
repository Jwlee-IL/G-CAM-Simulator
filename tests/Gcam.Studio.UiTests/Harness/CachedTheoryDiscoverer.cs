using Xunit.Abstractions;
using Xunit.Sdk;

namespace Gcam.Studio.UiTests.Harness;

/// <summary>Discovers each serializable data row of a <see cref="DesktopTheoryAttribute"/> method as its own
/// <see cref="CachedDesktopTestCase"/>, so every row has its own cache entry. A skipped theory and rows that cannot be
/// pre-enumerated stay ordinary xUnit cases (never reused).</summary>
public sealed class CachedTheoryDiscoverer(IMessageSink diagnosticMessageSink) : TheoryDiscoverer(diagnosticMessageSink)
{
    protected override IEnumerable<IXunitTestCase> CreateTestCasesForDataRow(ITestFrameworkDiscoveryOptions discoveryOptions,
        ITestMethod testMethod, IAttributeInfo theoryAttribute, object[] dataRow) =>
        [new CachedDesktopTestCase(DiagnosticMessageSink, discoveryOptions.MethodDisplayOrDefault(), discoveryOptions.MethodDisplayOptionsOrDefault(), testMethod, dataRow)];
}
