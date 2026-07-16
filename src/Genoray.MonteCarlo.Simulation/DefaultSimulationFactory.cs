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
        // Coordinate frame: detector at z=0, mask at z=D, sources on the nominal source plane z=D+S.
        // Lateral (x, y) is the "off-axis angle" knob. A source MAY override its distance-to-detector via
        // Position[2] (z, mm): >0 places it at its own depth (real 1/r² efficiency + depth defocus); 0 (the
        // default) falls back to the shared source plane, so existing single-plane configs are unchanged.
        double sourceZ = config.Geometry.MaskDetectorDistanceMm + config.Geometry.SourceMaskDistanceMm;
        double halfW = config.Detector.PixelsX * config.Detector.PixelPitchMm / 2.0;
        double halfH = config.Detector.PixelsY * config.Detector.PixelPitchMm / 2.0;

        double SourceZOf(double[] pos) => pos.Length > 2 && pos[2] > 0.0 ? pos[2] : sourceZ;

        // Mixed-isotope field: build one emitter per (source, line), weighted by activity × intensity.
        if (config.Sources is { Length: > 0 } scene)
        {
            var emitters = new List<(Vector3, double, double)>();
            foreach (var s in scene)
            {
                var sp = new Vector3(s.Position[0], s.Position[1], SourceZOf(s.Position));
                foreach (var (energy, intensity) in LinesOf(s))
                    emitters.Add((sp, energy, s.ActivityBq * intensity));
            }
            // Scene-global biasing flag lives on the primary Source.
            return new MixedFieldSource(emitters, config.Source.DirectionalBiasing, halfW, halfH, detPlaneZ: 0.0);
        }

        var p = config.Source.Position;
        var pos = new Vector3(p[0], p[1], SourceZOf(p));
        if (config.Source.DirectionalBiasing)
            return new DetectorBiasedSource(pos, config.Source.EnergyKeV, halfW, halfH, detPlaneZ: 0.0);
        return new IsotropicSource(pos, config.Source.EnergyKeV);
    }

    // The emission lines of a source: its explicit multi-line list, else the single (energy, branching).
    private static IEnumerable<(double energy, double intensity)> LinesOf(SourceConfig s)
    {
        if (s.Lines is { Length: > 0 } lines)
            foreach (var l in lines) yield return (l.EnergyKeV, l.Intensity);
        else
            yield return (s.EnergyKeV, s.BranchingRatio);
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
        // Fabrication tolerances (a real tungsten mask ≠ the ideal MURA): stamp a fixed per-cell geometry
        // error on the mask if any tolerance is set. The decoder is unaffected (it decodes the ideal pattern).
        bool fabricated = m.HolePositionJitterMm > 0.0 || m.HoleSizeJitterMm > 0.0
                          || m.BlockedCellProbability > 0.0 || m.HoleWanderMm > 0.0;
        var fab = fabricated
            ? new MaskFabrication(pattern.Width, pattern.Height, m.CellPitchMm, m.HoleFraction,
                                  m.HolePositionJitterMm, m.HoleSizeJitterMm, m.BlockedCellProbability,
                                  m.HoleWanderMm, m.FabricationSeed)
            : null;
        return new CodedApertureMask(pattern, config.Geometry.MaskDetectorDistanceMm,
                                     m.CellPitchMm, m.ThicknessMm, m.LinearAttenuationPerMm,
                                     m.FocalDistanceMm, m.HoleFraction, m.TaperAngleDeg, fab,
                                     m.MaskOffsetXMm, m.MaskOffsetYMm, m.MaskOffsetZMm, m.MaskRollDeg);
    }

    public IDetector CreateDetector(SimulationConfig config)
    {
        var d = config.Detector;
        // Non-uniform if any per-pixel variation OR a finite energy window that trims
        // the (uniform) photopeak acceptance. Note: gradient may be negative.
        bool nonUniform = d.GainSigma != 0.0 || d.EnergyResolutionFwhmSigma != 0.0 || d.GainGradient != 0.0
                          || (d.EnergyWindowFraction > 0.0 && d.EnergyResolutionFwhm > 0.0);
        double[]? sensitivity = nonUniform ? new CrystalUniformity(d).Sensitivity : null;
        var entrance = d.EntranceAbsorberMm > 0.0 ? new EntranceAbsorber(d.EntranceAbsorberMm) : null;
        return new CrystalDetector(d.PixelsX, d.PixelsY, d.PixelPitchMm, planeZ: 0.0, sensitivity,
                                   d.CrystalAttenuationPerMm, d.CrystalThicknessMm, entrance);
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
