using Gcam.Configuration;
using Gcam.Core;
using Gcam.Detector;
using Gcam.Simulation;
using Xunit;

namespace Gcam.Tests;

/// <summary>Crystal Compton scattering: physics kinematics, positioning-strategy behaviour,
/// and the multi-isotope spatial-separation claim (contamination is coded from its source).</summary>
public class ComptonTests
{
    private static SimulationConfig Baseline(long photons = 120_000)
        => new() { PhotonCount = photons, Seed = 12345, Source = new SourceConfig { Position = [0, 0, 0.0] } };

    [Fact]
    public void Scatter_ConservesEnergy_AndStaysWithinKinematicBounds()
    {
        var rng = new DefaultRandom(1);
        var dir = new Vector3(0, 0, -1);
        double e = 661.7;
        double alpha = e / ComptonModel.MeC2;
        double eMinScattered = e / (1.0 + 2.0 * alpha);
        double comptonEdge = e - eMinScattered;
        for (int i = 0; i < 5000; i++)
        {
            var (dep, newE, nd) = ComptonModel.Scatter(e, dir, rng);
            Assert.True(System.Math.Abs(dep + newE - e) < 1e-6);        // energy conserved
            Assert.InRange(newE, eMinScattered - 1e-6, e + 1e-6);       // scattered photon bounds
            Assert.InRange(dep, -1e-6, comptonEdge + 1e-6);             // deposit up to the Compton edge
            Assert.True(System.Math.Abs(nd.Length - 1.0) < 1e-6);      // direction stays a unit vector
        }
    }

    [Fact]
    public void PhotoFraction_DecreasesWithEnergy()
    {
        Assert.True(ComptonModel.PhotoFraction(122) > ComptonModel.PhotoFraction(662));
        Assert.True(ComptonModel.PhotoFraction(662) > ComptonModel.PhotoFraction(1332));
    }

    [Fact]
    public void Argmax_RecoversMoreCountsThanPerPixelWindow()
    {
        // Argmax windows on TOTAL deposit, so it keeps Compton-split full-energy events that a
        // per-pixel window (which needs one pixel to hold the whole energy) throws away.
        var rows = new ComptonStudy().RunStrategies(Baseline(), windowFraction: 0.10,
                                                    detectedBudget: 400, repeats: 20, failThrMm: 3.0);
        double perPixel = System.Array.Find(rows, r => r.Strategy == "PerPixelWindow")!.EfficiencyRel;
        double argmax = System.Array.Find(rows, r => r.Strategy == "Argmax")!.EfficiencyRel;
        Assert.True(argmax > perPixel, $"argmax {argmax} should exceed per-pixel {perPixel}");
    }

    [Fact]
    public void Contamination_IsImagedAtTheContaminantSource_NotTheTarget()
    {
        // Co-60 downscatter leaks into the Cs-137 662 window, but is coded from Co-60's
        // direction -> decoding the contaminant map peaks at the Co-60 position, not the Cs one.
        double[] coPos = [-5.0, 3.0, 0.0];
        var c = new ComptonStudy().RunContamination(Baseline(), [4.0, 0.0, 0.0], coPos, windowFraction: 0.10);

        var (px, py) = ArgmaxMm(c.ReconContaminant, c.OriginMm, c.StepMm);
        Assert.True(System.Math.Sqrt((px - coPos[0]) * (px - coPos[0]) + (py - coPos[1]) * (py - coPos[1])) < 2.5,
                    $"contaminant peak ({px:F1},{py:F1}) should be near Co-60 ({coPos[0]},{coPos[1]})");
        Assert.True(c.ContaminationFraction > 0.05);   // the contamination is non-trivial
    }

    [Fact]
    public void Stripping_RecoversCsCount_EvenCoLocated()
    {
        // Per-pixel Compton stripping removes a strong Co-60 source's downscatter from the Cs-137
        // 662 window, recovering the true Cs count even when the two sources are CO-LOCATED (which
        // the spatial decode alone cannot separate).
        var s = new ComptonStudy().RunStripping(Baseline(), "colocated",
                    [0.0, 0.0, 0.0], [0.0, 0.0, 0.0], windowFraction: 0.10, coPhotonScale: 8.0);
        Assert.True(s.RawCounts > s.TrueCsCounts * 1.3, $"raw {s.RawCounts} should be heavily contaminated vs true {s.TrueCsCounts}");
        Assert.True(Math.Abs(s.StrippedCounts - s.TrueCsCounts) < 0.15 * s.TrueCsCounts,
                    $"stripped {s.StrippedCounts} should recover true Cs {s.TrueCsCounts}");
    }

    private static (double x, double y) ArgmaxMm(DetectorImage img, double origin, double step)
    {
        double best = double.NegativeInfinity; int bx = 0, by = 0;
        for (int y = 0; y < img.Height; y++)
            for (int x = 0; x < img.Width; x++)
                if (img[x, y] > best) { best = img[x, y]; bx = x; by = y; }
        return (origin + bx * step, origin + by * step);
    }
}
