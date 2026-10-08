using System.Text.Json.Serialization;

namespace Gcam.Configuration;

/// <summary>Charge-division network turning the SiPM array into four outputs. Written as a string.</summary>
[JsonConverter(typeof(JsonStringEnumConverter<NetworkTopology>))]
public enum NetworkTopology
{
    /// <summary>Ideal separable divider: each sensor's charge splits into the four outputs with bilinear weights of its
    /// normalised position ((k + ½)/S). Not a circuit — the linear reference against which real networks are compared.</summary>
    IdealBilinear,

    /// <summary>Discretised positioning circuit (Siegel et al., IEEE TNS 43 (1996) 1634): every sensor row is a resistor
    /// chain whose two ends feed a left and a right column chain; the column chains' ends are the four outputs. Solved by
    /// Kirchhoff's laws, so its non-separable distortion emerges from the resistor values.</summary>
    Dpc,

    /// <summary>A uniform two-dimensional resistor grid joining neighbouring sensors, drained at the four corner sensors
    /// into the outputs; solved by Kirchhoff's laws (the review's "grid" candidate).</summary>
    CornerGrid,
}
