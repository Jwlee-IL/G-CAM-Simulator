namespace Gcam.Studio.UiTests.Harness.Video;

/// <summary>Independent of encoded samples. Media time zero corresponds to this UTC/monotonic anchor.</summary>
public sealed record RecordingInterval(DateTimeOffset AnchorUtc, long MonotonicStart, long MonotonicEnd, long Frequency)
{
    public long Duration100ns => Frequency <= 0 ? 0 : (long)((MonotonicEnd - MonotonicStart) * (double)TimeSpan.TicksPerSecond / Frequency);
}
