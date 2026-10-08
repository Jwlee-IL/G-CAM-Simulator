namespace Gcam.Configuration;

/// <summary>Crystal identification by a flood-map look-up table: a uniform calibration flood (transported, triggered,
/// photopeak-windowed) is histogrammed in the raw position plane, Gaussian-smoothed (which suppresses shot noise and
/// BROADENS spots — it does not sharpen them), its local maxima are taken as markers, ordered into the crystal grid,
/// and the plane is split by a marker-controlled watershed. A flood whose peaks cannot be found or ordered fails
/// explicitly; it is never forced into a grid.</summary>
public sealed class FloodCalibrationConfig
{
    /// <summary>Calibration line (keV).</summary>
    public double EnergyKeV { get; set; } = 661.7;

    /// <summary>Scored calibration histories per crystal.</summary>
    public int EventsPerCrystal { get; set; } = 2000;

    /// <summary>Photopeak window half-width (fraction of the peak) used to select calibration events and to calibrate
    /// each crystal's energy gain.</summary>
    public double WindowFraction { get; set; } = 0.15;

    /// <summary>Histogram bins per crystal along each axis of the raw position plane.</summary>
    public int BinsPerCrystal { get; set; } = 32;

    /// <summary>Gaussian smoothing σ in histogram bins.</summary>
    public double SmoothingSigmaBins { get; set; } = 1.0;

    /// <summary>A local maximum below this fraction of the highest smoothed bin is not a marker candidate.</summary>
    public double MinPeakFraction { get; set; } = 0.02;

    /// <summary>Non-maximum suppression radius as a fraction of the nominal spot spacing (bins per crystal).</summary>
    public double MinSeparationFraction { get; set; } = 0.5;

    /// <summary>Segmentation between the markers.</summary>
    public FloodSegmentation Segmentation { get; set; } = FloodSegmentation.Watershed;
}
