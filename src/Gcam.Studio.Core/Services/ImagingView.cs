namespace Gcam.Studio.Core.Services;

/// <summary>Immutable channels and separate worker costs for this request.</summary>
public sealed record ImagingView(IReadOnlyList<ImagingChannel> Channels, IReadOnlyList<StripRatio> Ratios,
    TimeSpan ChannelTime, TimeSpan CalibrationTime, TimeSpan DecodeTime)
{
    /// <summary>Diagnostic count: focus-only requests measure no retained event again.</summary>
    public int NewlyMeasuredEvents { get; init; }
}
