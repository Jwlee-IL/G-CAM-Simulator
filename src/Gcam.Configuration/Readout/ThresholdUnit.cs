using System.Text.Json.Serialization;

namespace Gcam.Configuration;

/// <summary>Unit of a trigger threshold. Written as a string.</summary>
[JsonConverter(typeof(JsonStringEnumConverter<ThresholdUnit>))]
public enum ThresholdUnit
{
    /// <summary>ADC codes of the compared signal (one channel for Or / And, the code sum for Sum).</summary>
    AdcCode,

    /// <summary>keV-equivalent through the digitiser's reference gain (codes per keV = full-scale code / full-scale keV):
    /// for Or / And, a channel threshold of T keV means that channel alone must carry the signal of a T keV deposit
    /// whose whole light reached it — so an AND at T over four outputs needs at least ≈ 4·T keV in total; for Sum it is
    /// the uncalibrated sum energy.</summary>
    KeVEquivalent,
}
