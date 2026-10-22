namespace Gcam.Studio.UiTests.Harness.Video;

/// <summary>Pixels are streamed; only inexpensive per-sample facts are retained.</summary>
/// <remarks>MediaTypeChanges counts the decoder's current-type changes it followed (padded coded heights).</remarks>
public sealed record DecodedVideo(bool EndOfStream, DecodedVideo.Sample[] Samples, string? Error, int MediaTypeChanges = 0)
{
    public sealed record Sample(long Time100ns, long Duration100ns, int WidthPx, int HeightPx, bool SingleColour, bool Black);
    public long End100ns => Samples.Length == 0 ? 0 : Samples.Max(s => s.Time100ns + s.Duration100ns);
}
