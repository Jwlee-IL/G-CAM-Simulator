using System.Text.Json.Serialization;

namespace Gcam.Studio.UiTests.Harness.Video;

/// <summary>Every failed criterion is retained. Contract-less metadata remains a historical contract.</summary>
public sealed record VideoCheckResult(string Contract, string VideoSha256, string CaptionsSha256, bool Passed,
    string[] FailedCriteria, bool DecodedToEnd, int DecodedFrames, long DecodedEnd100ns,
    double? IntervalErrorSeconds, double LongestUndeclaredSingleColourSeconds, double UndeclaredSingleColourShare,
    double MaximumTimestampGapSeconds, double PrivacyShare, string[] UnmappedStepIds, string? DecodeError)
{
    /// <summary>The share of decoded samples inside declared window-transition intervals; null when the evidence predates
    /// transition intervals (omitted then, so an old result keeps its shape).</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public double? TransitionShare { get; init; }
}
