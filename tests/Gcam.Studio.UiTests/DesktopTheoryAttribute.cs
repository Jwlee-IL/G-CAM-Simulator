using Xunit.Sdk;

namespace Gcam.Studio.UiTests;

/// <summary>A parameterized desktop scenario, with the same opt-in and result cache as DesktopFact (one cache entry per
/// data row).</summary>
[XunitTestCaseDiscoverer("Gcam.Studio.UiTests.Harness.CachedTheoryDiscoverer", "Gcam.Studio.UiTests")]
public sealed class DesktopTheoryAttribute : TheoryAttribute
{
    public DesktopTheoryAttribute()
    {
        if (Environment.GetEnvironmentVariable(DesktopFactAttribute.OptInVariable) != "1")
            Skip = "desktop UI test: set GCAM_UI_TESTS=1 to run (takes over mouse and focus)";
    }
}
