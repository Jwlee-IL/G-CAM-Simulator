using System.Text.Json.Serialization;

namespace Gcam.Configuration;

/// <summary>How the channel amplitudes are held for conversion. Written as a string.</summary>
[JsonConverter(typeof(JsonStringEnumConverter<HoldMode>))]
public enum HoldMode
{
    /// <summary>All channels are sampled together at the instant the channel SUM peaks within the hold window.</summary>
    CommonAtSumPeak,

    /// <summary>Each channel holds its own peak within the hold window; the held values are converted together.</summary>
    IndependentPeaks,
}
