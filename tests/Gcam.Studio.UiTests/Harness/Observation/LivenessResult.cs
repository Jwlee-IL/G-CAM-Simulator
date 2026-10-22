namespace Gcam.Studio.UiTests.Harness.Observation;

/// <summary>Independent product-health evidence; errors never establish responsiveness.</summary>
public sealed record LivenessResult(OwnedProcessIdentity Process, long Hwnd, DateTimeOffset AnchorUtc,
    double LimitSeconds, double SamplePeriodSeconds, double ProbeTimeoutSeconds,
    int SampleCount, double LongestNonResponseSeconds, bool Breached, bool CurrentlyNotResponding, bool EarlyExit, int? ExitCode,
    bool AbnormalExit, DateTimeOffset? IntentionalCloseAtUtc, LivenessResult.Interval[] Intervals, string[] Errors)
{
    public sealed record Interval(double StartSeconds, double EndSeconds);
    public bool Passed => !Breached && !EarlyExit && !AbnormalExit && Errors.Length == 0;
}
