using Gcam.Configuration;
using Gcam.Core;
using Gcam.Decoding;
using Gcam.Simulation;
using Gcam.Studio.Core.Optics;
using Gcam.Studio.Core.Services;

namespace Gcam.Studio.Services;

/// <summary>One projection path for broadband, primary-window and stripped floods, by cross-correlation or MLEM.</summary>
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
        => Project(flood, original, config, isotope, peakCount, loKeV, hiKeV, null);

    /// <summary>As the cross-correlation projection, or with <paramref name="mlem"/> the pixel-area MLEM (TODO-36):
    /// <see cref="MlemProjection.Flood"/> is decoded with the known <see cref="MlemProjection.Background"/> (the strip
    /// path's higher-line contribution) while <paramref name="flood"/> stays the displayed flood.</summary>
    public static ImagingChannel Project(DetectorImage flood, ImagingResult original, SimulationConfig config,
        string isotope, int peakCount, double loKeV, double hiKeV, MlemProjection? mlem)
    {
        double count = flood.Raw.ToArray().Sum();
        DecodeResult? decoded;
        if (mlem is null) decoded = count > 0 ? new DefaultSimulationFactory().CreateDecoder(config)!.Decode(flood) : null;
        else
        {
            var data = mlem.Flood ?? flood;
            decoded = data.Raw.ToArray().Sum() > 0 ? mlem.Cache.For(config, mlem.LineEnergyKeV).Decode(data, mlem.Background) : null;
            if (mlem.EffectiveCounts is { } effective) count = effective;
        }
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

/// <summary>How one channel is decoded by MLEM: the cached decoder source, the imaged line (its closed-cell
/// transmission), and for the strip path the raw window flood with the higher lines' known background b_i and the
/// effective count shown for the channel.</summary>
public sealed record MlemProjection(MlemDecoderCache Cache, double LineEnergyKeV, DetectorImage? Flood = null,
    IReadOnlyList<double>? Background = null, double? EffectiveCounts = null);
