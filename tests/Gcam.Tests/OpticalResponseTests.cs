using Gcam.Configuration;
using Gcam.Core;
using Gcam.Detector;
using Gcam.Tests.Harness;

namespace Gcam.Tests;

/// <summary>TODO-19, RD-6 "light conservation" and the optical tracer against closed forms. Monte Carlo tolerances are
/// k = 4 binomial standard errors of the traced fraction (Stat.BinomialSigma), the photon budget fixed per test.</summary>
public class OpticalResponseTests
{
    private static CrystalArrayGeometry Array3(double pitch = 3.2, double gap = 0.2, int n = 6) => new(n, n, pitch, gap, 10.0);

    private static OpticalResponse Table(CrystalArrayGeometry crystals, ReadoutOpticsConfig optics, SensorLayout? sensors = null,
        int seed = 1)
        => new(crystals, sensors ?? new SensorLayout(crystals.CountX, crystals.CountY, crystals.PitchMm,
            crystals.ActiveWidthMm, 0, 0), optics, seed);

    [Fact]
    public void EveryTracedPhoton_HasExactlyOneFate()
    {
        var table = Table(Array3(), new ReadoutOpticsConfig { PhotonsPerBin = 2000, Surface = ReflectorSurface.Lambertian, WallReflectance = 0.9, WallTransmittance = 0.05 });
        Assert.Equal(1.0, table.FateFractions.Values.Sum(), 12);
        for (int c = 0; c < table.Crystals.Count; c++)
            for (int b = 0; b < table.DepthBins; b++)
            {
                double sum = 0;
                foreach (var s in table.Response(c, b)) { Assert.InRange(s.Probability, 0.0, 1.0); sum += s.Probability; }
                Assert.Equal(table.Collection(c, b), sum, 12);
                Assert.InRange(sum, 0.0, 1.0);
            }
        Assert.True(table.FateFractions[OpticalFate.ExteriorLost] > 0, "transmitting walls must lose light at the array edge");
    }

    /// <summary>Lossless walls and top, couplant index-matched to the crystal (no Fresnel reflection, no TIR), zero couplant
    /// thickness, sensors covering the whole pitch with no reflector gap: every photon must reach its own crystal's sensor.</summary>
    [Fact]
    public void LosslessIndexMatchedBox_DeliversEveryPhoton()
    {
        var crystals = Array3(gap: 0.0);
        var optics = new ReadoutOpticsConfig
        {
            WallReflectance = 1, TopReflectance = 1, CouplantRefractiveIndex = 1.9, CouplantThicknessMm = 0,
            PhotonsPerBin = 1000, Surface = ReflectorSurface.Lambertian,
        };
        var table = Table(crystals, optics);
        Assert.Equal(1.0, table.FateFractions[OpticalFate.Detected], 12);
        for (int c = 0; c < crystals.Count; c++)
        {
            var r = table.Response(c, 3);
            Assert.Single(r.ToArray());
            Assert.Equal(c, r[0].Sensor);
        }
    }

    /// <summary>Absorbing walls and top (R = 0), index-matched exit, no couplant, sensor = crystal footprint: only photons
    /// flying straight to the exit square are detected, a fraction Ω/4π with Ω = 4·asin(a² / (a² + 4h²)) for a square of
    /// side a seen on axis from height h.</summary>
    [Theory]
    [InlineData(1.0)]
    [InlineData(5.0)]
    public void BlackBox_DetectsTheDirectSolidAngle(double height)
    {
        var crystals = Array3();
        var table = Table(crystals, new ReadoutOpticsConfig
        {
            WallReflectance = 0, TopReflectance = 0, CouplantRefractiveIndex = 1.9, CouplantThicknessMm = 0, PhotonsPerBin = 1,
        });
        const int N = 200_000;
        var rng = new DefaultRandom(7);
        int hit = 0;
        for (int i = 0; i < N; i++) if (table.Trace(2, 2, 0, 0, height, rng).Fate == OpticalFate.Detected) hit++;
        double a = crystals.ActiveWidthMm, p = 4 * Math.Asin(a * a / (a * a + 4 * height * height)) / (4 * Math.PI);
        Stat.Within((double)hit / N, p, Stat.BinomialSigma(p, N), what: "direct solid-angle fraction");
    }

    /// <summary>A photon leaving straight down crosses the exit face with the normal-incidence Fresnel transmission
    /// 1 − ((n1 − n2)/(n1 + n2))²; the reflected part goes back up to an absorbing top.</summary>
    [Fact]
    public void NormalIncidence_TransmitsTheFresnelFraction()
    {
        var table = Table(Array3(), new ReadoutOpticsConfig { WallReflectance = 0, TopReflectance = 0, PhotonsPerBin = 1 });
        const int N = 200_000;
        var rng = new DefaultRandom(11);
        int hit = 0;
        for (int i = 0; i < N; i++) if (table.TraceDirection(2, 2, 0, 0, 5, 0, 0, -1, rng).Fate == OpticalFate.Detected) hit++;
        double r = (1.9 - 1.46) / (1.9 + 1.46), t = 1 - r * r;
        Stat.Within((double)hit / N, t, Stat.BinomialSigma(t, N), what: "normal-incidence Fresnel transmission");
    }

    /// <summary>Beyond the critical angle asin(1.46/1.9) ≈ 50.2° the exit face reflects totally: with absorbing walls and top
    /// the photon is never detected (deterministic).</summary>
    [Fact]
    public void BeyondTheCriticalAngle_NothingLeavesTheExitFace()
    {
        var table = Table(Array3(), new ReadoutOpticsConfig { WallReflectance = 0, TopReflectance = 0, PhotonsPerBin = 1 });
        var rng = new DefaultRandom(3);
        double theta = 55 * Math.PI / 180;
        for (int i = 0; i < 1000; i++)
            Assert.NotEqual(OpticalFate.Detected, table.TraceDirection(2, 2, 0, 0, 0.01, Math.Sin(theta), 0, -Math.Cos(theta), rng).Fate);
    }

    [Fact]
    public void SymmetricLayout_TracesOnlyTheFundamentalDomain_OffsetLayoutTracesAll()
    {
        var crystals = Array3(n: 12);
        var optics = new ReadoutOpticsConfig { PhotonsPerBin = 50, DepthBins = 2 };
        Assert.Equal(21, Table(crystals, optics).TracedClasses);                 // 6×6 quadrant, triangle: 6·7/2
        var shifted = new SensorLayout(12, 12, 3.2, 3.0, 0.3, 0);
        Assert.Equal(144, Table(crystals, optics, shifted).TracedClasses);
    }

    /// <summary>The symmetry mapping must not bias the response: a corner crystal's table, mapped from its class, equals
    /// the mirror of the opposite corner's within the tracing noise (here the same class → identical by construction), and
    /// the most likely sensor of every crystal is its own (1:1 coupling, opaque walls).</summary>
    [Fact]
    public void MappedTables_PointEachCrystalAtItsOwnSensor()
    {
        var table = Table(Array3(n: 12), new ReadoutOpticsConfig { PhotonsPerBin = 400, DepthBins = 2 });
        for (int c = 0; c < 144; c++)
            for (int b = 0; b < 2; b++)
                Assert.Equal(c, table.Response(c, b).ToArray().MaxBy(s => s.Probability).Sensor);
    }
}
