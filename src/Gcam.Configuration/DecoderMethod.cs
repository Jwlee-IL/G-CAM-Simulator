using System.Text.Json.Serialization;

namespace Gcam.Configuration;

/// <summary>Reconstruction method of <see cref="DecoderConfig"/>. Written and read as a string ("CrossCorrelation",
/// "Mlem"; case-insensitive) by its own converter, so a scenario can say <c>"Method": "Mlem"</c> without changing how the
/// other enums of the config are stored.</summary>
[JsonConverter(typeof(JsonStringEnumConverter<DecoderMethod>))]
public enum DecoderMethod
{
    /// <summary>±1 cross-correlation back-projection (the pipeline default).</summary>
    CrossCorrelation,

    /// <summary>Poisson maximum-likelihood EM with the pixel-area forward model (D-46, EV-11): the decoder PR-IMG-04 is
    /// claimed with. Honoured by the CLI single run and GCAM Studio's projection; study commands refuse it.</summary>
    Mlem,
}
