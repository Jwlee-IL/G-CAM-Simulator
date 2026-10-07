using System.Text.Json.Serialization;

namespace Gcam.Configuration;

/// <summary>Explicit background knowledge for single-source localization.</summary>
[JsonConverter(typeof(JsonStringEnumConverter<BackgroundCorrectionMode>))]
public enum BackgroundCorrectionMode
{
    JointMlem,
    KnownScaleMlem,
    KnownScaleCorrelation
}
