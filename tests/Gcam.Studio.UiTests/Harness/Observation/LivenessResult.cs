namespace Gcam.Studio.UiTests.Harness.Observation;

/// <summary>Independent product-health evidence; errors never establish responsiveness.</summary>
public sealed record LivenessResult(OwnedProcessIdentity Process, long Hwnd, DateTimeOffset AnchorUtc,
    double LimitSeconds, double SamplePeriodSeconds, double ProbeTimeoutSeconds,
    int SampleCount, double LongestNonResponseSeconds, bool Breached, bool CurrentlyNotResponding, bool EarlyExit, int? ExitCode,
    bool AbnormalExit, DateTimeOffset? IntentionalCloseAtUtc, LivenessResult.Interval[] Intervals, string[] Errors)
{
    public sealed record Interval(double StartSeconds, double EndSeconds);
    /// <summary>Seconds from attachment to the main window, or, while no window was found, the seconds observed so far.</summary>
    public double StartupSeconds { get; init; }
    public bool WindowSeen { get; init; }
    /// <summary>The launch produced no usable main window within the launch wait (its own product issue code).</summary>
    public bool LaunchFailed { get; init; }
    public string Phase { get; init; } = "running";
    public bool Passed => !LaunchFailed && !Breached && !EarlyExit && !AbnormalExit && Errors.Length == 0;
}
