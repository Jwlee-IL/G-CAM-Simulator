namespace Gcam.Studio.UiTests;

/// <summary>
/// A test that drives the real app on the interactive desktop. Skipped unless <c>GCAM_UI_TESTS=1</c>: it moves the
/// mouse and takes focus, so it must be an explicit choice by whoever is at the machine, never a side effect of
/// <c>dotnet test Gcam.sln</c>.
/// </summary>
public sealed class DesktopFactAttribute : FactAttribute
{
    public const string OptInVariable = "GCAM_UI_TESTS";

    public DesktopFactAttribute()
    {
        if (Environment.GetEnvironmentVariable(OptInVariable) != "1")
            Skip = $"desktop UI test: set {OptInVariable}=1 to run (takes over mouse and focus)";
    }
}
