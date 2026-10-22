namespace Gcam.Studio.UiTests.Harness.Video;

/// <summary>Every failed criterion is retained. Contract-less metadata remains a historical contract.</summary>
public sealed record VideoCheckResult(string Contract, string VideoSha256, string CaptionsSha256, bool Passed,
    string[] FailedCriteria, bool DecodedToEnd, int DecodedFrames, long DecodedEnd100ns,
    double? IntervalErrorSeconds, double LongestUndeclaredSingleColourSeconds, double UndeclaredSingleColourShare,
    double MaximumTimestampGapSeconds, double PrivacyShare, string[] UnmappedStepIds, string? DecodeError);
