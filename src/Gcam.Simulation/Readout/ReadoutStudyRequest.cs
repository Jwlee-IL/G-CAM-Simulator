using Gcam.Configuration;

namespace Gcam.Simulation;

/// <summary>
/// The readout study's recipe (TODO-19, RD-7): which geometries, readouts and triggers to compare against the direct
/// assignment, at which lines and rates, with which Monte Carlo budgets. Transport is shared by every readout of a
/// geometry (paired comparison); the light / sensor fluctuations are shared by every trigger of a readout.
/// </summary>
public sealed class ReadoutStudyRequest
{
    public ReadoutStudyGeometry[] Geometries { get; set; } = [];
    public ReadoutStudyVariant[] Readouts { get; set; } = [];
    public ReadoutStudyTrigger[] Triggers { get; set; } = [];

    /// <summary>Lines studied (keV): one flood and one localisation run each.</summary>
    public double[] EnergiesKeV { get; set; } = [122.06, 661.7, 1332.5];

    /// <summary>Scored histories per crystal of each validation flood.</summary>
    public int FloodHistoriesPerCrystal { get; set; } = 500;

    /// <summary>Scored histories per crystal of the calibration flood (independent of the validation floods).</summary>
    public int CalibrationHistoriesPerCrystal { get; set; } = 2000;

    /// <summary>Emitted (detector-biased) histories of each localisation run through the scenario's mask.</summary>
    public long LocalisationHistories { get; set; } = 200_000;

    /// <summary>Hit rates (detected histories per second) of the pile-up axis, run on the line nearest 662 keV.</summary>
    public double[] RatesCps { get; set; } = [1e3, 1e4, 1e5, 3e5, 1e6];

    /// <summary>Hits of each rate point (the first localisation events of that line, re-timed as a Poisson stream).</summary>
    public int RateHits { get; set; } = 20_000;

    /// <summary>Photopeak window half-width (fraction of the line) for imaging and the photopeak metrics.</summary>
    public double WindowFraction { get; set; } = 0.15;

    /// <summary>Everything not varied by a geometry, variant or trigger.</summary>
    public ReadoutConfig Base { get; set; } = new();
}
