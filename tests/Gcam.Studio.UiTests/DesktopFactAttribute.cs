namespace Gcam.Studio.UiTests;

/// <summary>
/// A test that drives the real app on the interactive desktop. Skipped unless <c>GCAM_UI_TESTS=1</c>: it moves the
/// mouse and takes focus, so it must be an explicit choice by whoever is at the machine, never a side effect of
/// <c>dotnet test Gcam.sln</c>.
/// </summary>
public sealed class DesktopFactAttribute : FactAttribute
{
    public const string OptInVariable = "GCAM_UI_TESTS";

    public const string LongOptInVariable = "GCAM_UI_LONG_TESTS";

    internal static string? ConditionId(bool desktop, bool longOptIn, bool longRunning) =>
        !desktop ? "desktop-opt-in-off" : longRunning && !longOptIn ? "long-desktop-opt-in-off" : null;

    public DesktopFactAttribute(bool longRunning = false, bool probe = false)
    {
        bool desktop = Environment.GetEnvironmentVariable(OptInVariable) == "1";
        bool longOptIn = Environment.GetEnvironmentVariable(LongOptInVariable) == "1";
        Skip = SkipReason(desktop, longOptIn, longRunning);
        if (Skip is not null) Skip = "[" + ConditionId(desktop, longOptIn, longRunning) + "] " + Skip;
        if (Skip is null && probe && Environment.GetEnvironmentVariable("GCAM_UI_PROBE") != "1")
            Skip = "probe-opt-in-off: set GCAM_UI_PROBE=1 for the separately approved observed run";
    }
    internal static string? SkipReason(bool desktop, bool longOptIn, bool longRunning)
    {
        if (longRunning && !(desktop && longOptIn))
            return $"long desktop UI test: set {OptInVariable}=1 AND {LongOptInVariable}=1; one cold 64 k preparation, ≈ 45 min, ≈ 6 GiB (takes over mouse and focus)";
        return desktop ? null : $"desktop UI test: set {OptInVariable}=1 to run (takes over mouse and focus)";
    }
}
