using System.Text.Json.Serialization;

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
    /// <summary>Window transitions the harness itself caused (the launch until the owned window's first painted capture, a
    /// window-state change it issued, a close it issued), as the samples the recorder marked while each was open. Null on
    /// records written before the field existed: those excuse nothing and keep their verdicts. Omitted when null, so an old
    /// record keeps its shape when it is written again.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public TransitionInterval[]? TransitionIntervals { get; init; }
    public sealed record PrivacyInterval(long Start100ns, long End100ns, string Reason, string[] StepIds);
    public sealed record MaskSample(int Frame, long Start100ns, long End100ns, bool Privacy, string[] Reasons, int[][] RectsPx);
    /// <summary>Sample-aligned like a privacy interval: it starts at its first marked sample and ends at the end of its last.
    /// Its step IDs name the step that began it (the harness action, or the recorder's launch step).</summary>
    public sealed record TransitionInterval(long Start100ns, long End100ns, string Reason, string[] StepIds);
}
