using Gcam.Configuration;
using Gcam.Core;
using Gcam.Simulation;
using Xunit;

namespace Gcam.Tests;

/// <summary>
/// Integration harness: locks in the key physics findings as fast regression tests.
/// Uses directional biasing so small photon counts give clean, deterministic (seeded)
/// results. Baseline = rank-7 MURA, 10 mm W, 12×12/1 mm, D 60, S 100 (all config defaults).
/// </summary>
public class PipelineTests
{
    private static SimulationConfig Baseline(double sx = 0, double sy = 0, long photons = 200_000)
        => new() { PhotonCount = photons, Seed = 12345, Source = new SourceConfig { Position = [sx, sy, 0.0] } };

    private static SimulationResult Run(SimulationConfig cfg)
        => new SimulationRunner(new DefaultSimulationFactory()).Run(cfg);

    private static double Error(SimulationResult r, double x, double y)
    {
        var p = r.Estimate!.Position;
        return System.Math.Sqrt((p.X - x) * (p.X - x) + (p.Y - y) * (p.Y - y));
    }

    [Fact]
    public void CenteredSource_LocalizesSubMillimeter()
        => Assert.True(Error(Run(Baseline()), 0, 0) < 1.5);

    [Fact]
    public void OffAxisInsideFcfov_Tracks()
        => Assert.True(Error(Run(Baseline(5, 0)), 5, 0) < 1.5);

    [Fact]
    public void OffAxisOutsideFcfov_GhostsToOppositeSide()
    {
        var r = Run(Baseline(12, 0));                 // outside FCFOV (±9.3 mm)
        Assert.True(r.Estimate!.Position.X < 0);       // ghost on the opposite side
        Assert.True(Error(r, 12, 0) > 5.0);
    }

    [Fact]
    public void Biasing_IsUnbiasedVersus4Pi()
    {
        var biased = Run(Baseline());
        var iso = Baseline(photons: 5_000_000);
        iso.Source.DirectionalBiasing = false;
        var isoR = Run(iso);

        double effB = biased.DetectedWeight / biased.PhotonsEmitted;
        double eff4 = isoR.DetectedWeight / isoR.PhotonsEmitted;
        Assert.True(System.Math.Abs(effB - eff4) / eff4 < 0.20);   // within 20% (biased is unbiased)
    }

    [Fact]
    public void DenserCrystal_DetectsMore()
    {
        var lo = Baseline(); lo.Detector.CrystalAttenuationPerMm = 0.031;   // NaI-ish
        var hi = Baseline(); hi.Detector.CrystalAttenuationPerMm = 0.063;   // BGO-ish
        Assert.True(Run(hi).DetectedWeight > Run(lo).DetectedWeight);
    }

    [Fact]
    public void LeakyMask_AddsCountsVersusOpaque()
    {
        var opaque = Baseline(); opaque.Mask.LinearAttenuationPerMm = 100.0;
        var leaky = Baseline(); leaky.Mask.LinearAttenuationPerMm = 0.05;
        Assert.True(Run(leaky).DetectedWeight > Run(opaque).DetectedWeight);
    }

    [Fact]
    public void NonCyclicDecoding_SuppressesGhost()
    {
        SimulationConfig Wide(bool cyclic)
        {
            var c = Baseline(12, 0);
            c.Decoder.Cyclic = cyclic;
            c.Decoder.ReconHalfExtentMm = 18.0;   // wide search covers the true 12 mm
            c.Decoder.ReconStepMm = 0.75;
            return c;
        }
        double cyclicErr = Error(Run(Wide(true)), 12, 0);
        double nonCyclicErr = Error(Run(Wide(false)), 12, 0);
        Assert.True(nonCyclicErr < cyclicErr);   // finite-mask decoding avoids the wraparound ghost
    }
}
