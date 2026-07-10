using Genoray.MonteCarlo.Configuration;
using Genoray.MonteCarlo.Core;
using Genoray.MonteCarlo.Decoding;
using Genoray.MonteCarlo.Detector;
using Genoray.MonteCarlo.Masks;

namespace Genoray.MonteCarlo.Simulation;

/// <summary>Wires the standard MURA / crystal / cross-correlation pipeline from config.</summary>
public sealed class DefaultSimulationFactory : ISimulationFactory
{
    public IRandom CreateRandom(SimulationConfig config)
        => new DefaultRandom(config.Seed);

    public ISource CreateSource(SimulationConfig config)
    {
        // Coordinate frame: detector at z=0, mask at z=D, source at z=D+S.
        // Source lateral position (x, y) is the "off-axis angle" knob; Position[2] is unused.
        var p = config.Source.Position;
        double sourceZ = config.Geometry.MaskDetectorDistanceMm + config.Geometry.SourceMaskDistanceMm;
        var pos = new Vector3(p[0], p[1], sourceZ);

        if (config.Source.DirectionalBiasing)
        {
            double halfW = config.Detector.PixelsX * config.Detector.PixelPitchMm / 2.0;
            double halfH = config.Detector.PixelsY * config.Detector.PixelPitchMm / 2.0;
            return new DetectorBiasedSource(pos, config.Source.EnergyKeV, halfW, halfH, detPlaneZ: 0.0);
        }

        return new IsotropicSource(pos, config.Source.EnergyKeV);
    }

    public IMask CreateMask(SimulationConfig config)
    {
        var m = config.Mask;
        var pattern = MuraGenerator.Mosaic(m.Rank, m.MosaicX, m.MosaicY);
        if (m.Invert)
        {
            var inv = new MaskPattern(pattern.Width, pattern.Height);
            for (int x = 0; x < pattern.Width; x++)
                for (int y = 0; y < pattern.Height; y++)
                    inv[x, y] = !pattern[x, y];
            pattern = inv;
        }
        return new CodedApertureMask(pattern, config.Geometry.MaskDetectorDistanceMm,
                                     m.CellPitchMm, m.ThicknessMm, m.LinearAttenuationPerMm,
                                     m.FocalDistanceMm, m.HoleFraction, m.TaperAngleDeg);
    }

    public IDetector CreateDetector(SimulationConfig config)
    {
        var d = config.Detector;
        // Non-uniform if any per-pixel variation OR a finite energy window that trims
        // the (uniform) photopeak acceptance. Note: gradient may be negative.
        bool nonUniform = d.GainSigma != 0.0 || d.EnergyResolutionFwhmSigma != 0.0 || d.GainGradient != 0.0
                          || (d.EnergyWindowFraction > 0.0 && d.EnergyResolutionFwhm > 0.0);
        double[]? sensitivity = nonUniform ? new CrystalUniformity(d).Sensitivity : null;
        return new CrystalDetector(d.PixelsX, d.PixelsY, d.PixelPitchMm, planeZ: 0.0, sensitivity,
                                   d.CrystalAttenuationPerMm, d.CrystalThicknessMm);
    }

    public IDecoder? CreateDecoder(SimulationConfig config)
    {
        var m = config.Mask;
        double maskZ = config.Geometry.MaskDetectorDistanceMm;
        double sourceZ = maskZ + config.Geometry.SourceMaskDistanceMm;

        // The cyclic reconstruction is periodic in source-lateral space with one
        // period == the fully-coded FOV. Size the grid to exactly one period so a
        // source inside the FCFOV yields a single peak (sources outside it alias
        // into a ghost — the artifact we want to study later).
        double frac = maskZ / sourceZ;                 // back-projection scale (dz = 0)
        double fcfovPeriod = m.Rank * m.CellPitchMm / frac;

        double half = config.Decoder.ReconHalfExtentMm ?? fcfovPeriod / 2.0;
        double step = config.Decoder.ReconStepMm ?? fcfovPeriod / 48.0;

        var geo = new CodedApertureGeometry(
            Rank: m.Rank,
            MaskPlaneZ: maskZ,
            MaskCellPitchMm: m.CellPitchMm,
            MaskCellsX: m.Rank * m.MosaicX,
            MaskCellsY: m.Rank * m.MosaicY,
            DetectorPlaneZ: 0.0,
            DetectorPitchMm: config.Detector.PixelPitchMm,
            SourcePlaneZ: sourceZ,
            ReconHalfExtentMm: half,
            ReconStepMm: step,
            Cyclic: config.Decoder.Cyclic);
        return new CrossCorrelationDecoder(MuraGenerator.DecodingArray(m.Rank), geo);
    }
}
