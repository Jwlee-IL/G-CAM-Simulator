namespace Gcam.Configuration;

/// <summary>Measured-marker selection policy; legacy remains the engine default.</summary>
public enum FloodCalibrationMethod
{
    Legacy,
    /// <summary>TODO-41 v1: separable marginal slots (kept for the stage-1 pilot record; it cannot follow bowed columns).</summary>
    TopologyAware,
    /// <summary>TODO-41 v2: lattice growth from measured maxima on a density smoothed at one eighth of the nominal spot
    /// spacing, with a counting-statistics valley test. <see cref="FloodCalibrationConfig.SmoothingSigmaBins"/> is not
    /// used by this method: its smoothing is BinsPerCrystal / 8.</summary>
    LatticeGrowth,
}
