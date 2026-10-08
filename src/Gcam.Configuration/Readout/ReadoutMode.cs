using System.Text.Json.Serialization;

namespace Gcam.Configuration;

/// <summary>How detected interactions become an event position and energy (TODO-19, RD-1). Written as a string.</summary>
[JsonConverter(typeof(JsonStringEnumConverter<ReadoutMode>))]
public enum ReadoutMode
{
    /// <summary>Today's engine: each history is assigned to its largest-deposit crystal, energy = total deposit. The
    /// default and the reference; every existing result is produced by this mode.</summary>
    DirectCrystal,

    /// <summary>Conventional Anger readout from published practice: light → SiPM array → resistive charge-division network
    /// → four outputs → digitised together → four-corner Anger ratio → flood-map look-up table → crystal.</summary>
    FourOutputAnger,

    /// <summary>One channel per SiPM (no charge division): prices the four-ADC constraint. Position = charge-weighted
    /// centroid of the sensor centres, crystal from its own flood-map look-up table.</summary>
    IndependentSipm,
}
