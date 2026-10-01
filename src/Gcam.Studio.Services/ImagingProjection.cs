using Gcam.Configuration;
using Gcam.Core;
using Gcam.Decoding;
using Gcam.Simulation;
using Gcam.Studio.Core.Optics;
using Gcam.Studio.Core.Services;

namespace Gcam.Studio.Services;

/// <summary>One projection path for broadband, primary-window and stripped floods.</summary>
public static class ImagingProjection
{
    public static SimulationConfig AtFocus(SimulationConfig acquired, OpticsSettings optics, double focalDistanceMm)
    {
        if (OpticsPolicy.ValidateFocus(optics, focalDistanceMm) is { } error)
            throw new ArgumentOutOfRangeException(nameof(focalDistanceMm), error);
        var config = acquired.Clone();
        config.Geometry.SourceMaskDistanceMm = focalDistanceMm - config.Geometry.MaskDetectorDistanceMm;
        double resolution = OpticsGeometry.Calculate(optics, focalDistanceMm).ResolutionElementMm;
        config.Decoder.ReconHalfExtentMm = 0.95 * config.Mask.Rank * resolution / 2;
        config.Decoder.ReconStepMm = Math.Max(0.2, resolution / 4);
        return config;
    }

    public static ImagingChannel Project(DetectorImage flood, ImagingResult original, SimulationConfig config,
        string isotope, int peakCount, double loKeV = double.NaN, double hiKeV = double.NaN)
    {
        double count = flood.Raw.ToArray().Sum();
        var decoded = count > 0 ? new DefaultSimulationFactory().CreateDecoder(config)!.Decode(flood) : null;
        double separation = config.Mask.CellPitchMm *
            (config.Geometry.MaskDetectorDistanceMm + config.Geometry.SourceMaskDistanceMm) /
            config.Geometry.MaskDetectorDistanceMm;
        var peaks = decoded is null ? [] : MixedFieldStudy.TopPeaks(decoded.Reconstruction,
            decoded.ReconOriginMm, decoded.ReconStepMm, peakCount, separation).Select(p =>
            {
                int x = (int)Math.Round((p.Xmm - decoded.ReconOriginMm) / decoded.ReconStepMm);
                int y = (int)Math.Round((p.Ymm - decoded.ReconOriginMm) / decoded.ReconStepMm);
                var (dx, dy) = PeakInterpolation.Estimate(decoded.Reconstruction, x, y, config.Decoder.SubCellInterpolation);
                return new ImagingPeak(isotope, p.Xmm + dx * decoded.ReconStepMm,
                    p.Ymm + dy * decoded.ReconStepMm, p.Value);
            }).ToArray();
        var image = original with { Flood = flood, Reconstruction = decoded?.Reconstruction.ReadOnlyCopy(),
            ReconOriginMm = decoded?.ReconOriginMm ?? 0, ReconStepMm = decoded?.ReconStepMm ?? 0,
            Estimate = decoded?.Estimate, EffectiveCounts = count };
        return new(isotope, loKeV, hiKeV, image, Array.AsReadOnly(peaks));
    }
}
