namespace Gcam.Studio.UiTests.Harness.Video;

/// <summary>Run-local inputs to qualification; media, step identity and privacy are separate evidence.</summary>
public sealed record VideoEvidence(RecordingInterval Interval, VideoTimeline.Step[] Steps, VideoTimeline.Cue[] Cues,
    VideoEvidence.PrivacyInterval[] PrivacyIntervals, VideoEvidence.MaskSample[] MaskSamples)
{
    /// <summary>Steps recorded after the recording stopped; they can map to no cue (also listed in Steps).</summary>
    public string[] LateStepIds { get; init; } = [];
    /// <summary>The encoder's key-frame forcing for black (privacy) samples, as the writer reported it; null when the sink
    /// failed before reporting. Placement is guaranteed only for <c>requested-synchronous-encoder-after-drain-marker</c>.</summary>
    public string? KeyFrameForcing { get; init; }
    public sealed record PrivacyInterval(long Start100ns, long End100ns, string Reason, string[] StepIds);
    public sealed record MaskSample(int Frame, long Start100ns, long End100ns, bool Privacy, string[] Reasons, int[][] RectsPx);
}
