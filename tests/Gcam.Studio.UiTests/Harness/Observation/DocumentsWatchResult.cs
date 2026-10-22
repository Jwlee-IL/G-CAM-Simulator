namespace Gcam.Studio.UiTests.Harness.Observation;

/// <summary>Events are retained even when a file no longer exists at the final snapshot. Blind spots are reported, not
/// failed: Passed is channel health (root watched, positive control seen, no overflow or error).</summary>
public sealed record DocumentsWatchResult(string ResolvedRoot, string RelativePattern, bool CoversRoot,
    bool PositiveControlPassed, DocumentsWatchResult.Change[] Events, string[] Errors)
{
    /// <summary>A subtree the watcher does not cover or inventory (link, other reparse point, unreadable folder).</summary>
    public BlindSpot[] BlindSpots { get; init; } = [];
    public sealed record BlindSpot(string Path, string Kind, string Detail);
    public sealed record Change(DateTimeOffset AtUtc, string Operation, string Path, string? OldPath, bool HarnessControl);
    public bool Passed => CoversRoot && PositiveControlPassed && Errors.Length == 0;
}
