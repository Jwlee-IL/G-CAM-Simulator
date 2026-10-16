using Xunit.Abstractions;
using Xunit.Sdk;

namespace Gcam.Studio.UiTests.Harness;

/// <summary>Discovers a <see cref="DesktopFactAttribute"/> method as a <see cref="CachedDesktopTestCase"/>.</summary>
public sealed class CachedFactDiscoverer(IMessageSink diagnosticMessageSink) : FactDiscoverer(diagnosticMessageSink)
{
    protected override IXunitTestCase CreateTestCase(ITestFrameworkDiscoveryOptions discoveryOptions, ITestMethod testMethod, IAttributeInfo factAttribute) =>
        new CachedDesktopTestCase(DiagnosticMessageSink, discoveryOptions.MethodDisplayOrDefault(), discoveryOptions.MethodDisplayOptionsOrDefault(), testMethod);
}
