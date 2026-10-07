using Gcam.Configuration;

namespace Gcam.Studio.Core.Services;

/// <summary>Immutable channels and separate worker costs for this request.</summary>
public sealed record ImagingView(IReadOnlyList<ImagingChannel> Channels, IReadOnlyList<StripRatio> Ratios,
    TimeSpan ChannelTime, TimeSpan CalibrationTime, TimeSpan DecodeTime)
{
    /// <summary>Diagnostic count: focus-only requests measure no retained event again.</summary>
    public int NewlyMeasuredEvents { get; init; }

    /// <summary>The reconstruction method these channels were decoded with (TODO-38). The selector may already name
    /// another method while this view is still displayed; labels describing the image follow this value.</summary>
    public DecoderMethod Method { get; init; } = DecoderMethod.CrossCorrelation;
}
