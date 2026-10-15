namespace Gcam.Studio.UiTests;

/// <summary>A parameterized desktop scenario, with the same opt-in as DesktopFact.</summary>
public sealed class DesktopTheoryAttribute : TheoryAttribute
{
    public DesktopTheoryAttribute()
    {
        if (Environment.GetEnvironmentVariable(DesktopFactAttribute.OptInVariable) != "1")
            Skip = "desktop UI test: set GCAM_UI_TESTS=1 to run (takes over mouse and focus)";
    }
}
