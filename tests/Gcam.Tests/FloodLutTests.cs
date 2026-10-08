using Gcam.Configuration;
using Gcam.Core;
using Gcam.Detector;
using Gcam.Simulation;
using Gcam.Tests.Harness;

namespace Gcam.Tests;

/// <summary>TODO-19, RD-6 "calibration train / test split" and explicit failure of the flood-map look-up table, on
/// synthetic floods (Gaussian spots on a known grid, independent streams for calibration and test) and on a transported
/// readout flood.</summary>
public class FloodLutTests
{
    // A 6 × 6 grid of spots at u_i = (2i + 1)/6 − 1 with a barrel distortion (edge spacing compressed), σ in raw units.
    private static List<(int Crystal, double X, double Y)> Spots(int perCrystal, double sigma, int seed, double distortion = 0.15)
    {
        var rng = new DefaultRandom(seed);
        var outp = new List<(int, double, double)>();
        for (int iy = 0; iy < 6; iy++)
            for (int ix = 0; ix < 6; ix++)
            {
                double u = (2 * ix + 1) / 6.0 - 1, v = (2 * iy + 1) / 6.0 - 1;
                double cx = u * (1 - distortion * u * u), cy = v * (1 - distortion * v * v);
                for (int k = 0; k < perCrystal; k++)
                    outp.Add((iy * 6 + ix, cx + sigma * Sampling.Gaussian(rng), cy + sigma * Sampling.Gaussian(rng)));
            }
        return outp;
    }

    private static FloodLut Calibrate(List<(int Crystal, double X, double Y)> flood, FloodSegmentation segmentation = FloodSegmentation.Watershed)
        => FloodLut.Calibrate(flood.Select(p => (p.X, p.Y)).ToList(), 6, 6,
            new FloodCalibrationConfig { BinsPerCrystal = 32, SmoothingSigmaBins = 1.5, Segmentation = segmentation });

    [Theory]
    [InlineData(FloodSegmentation.Watershed)]
    [InlineData(FloodSegmentation.NearestPeak)]
    public void SeparatedSpots_AreOrderedIntoTheCrystalGrid(FloodSegmentation segmentation)
    {
        var lut = Calibrate(Spots(400, 0.02, 1), segmentation);
        Assert.True(lut.Succeeded, lut.Failure);
        for (int iy = 0; iy < 6; iy++)
            for (int ix = 0; ix < 6; ix++)
            {
                double u = (2 * ix + 1) / 6.0 - 1, v = (2 * iy + 1) / 6.0 - 1;
                var p = lut.Peaks[iy * 6 + ix];
                // Marker = a smoothed-histogram maximum: within two bins (2/192 raw units) of the true centre plus the
                // centroid's own spread (σ/√400 = 0.001); 0.02 covers it with margin.
                Assert.InRange(p.X, u * (1 - 0.15 * u * u) - 0.02, u * (1 - 0.15 * u * u) + 0.02);
                Assert.InRange(p.Y, v * (1 - 0.15 * v * v) - 0.02, v * (1 - 0.15 * v * v) + 0.02);
            }
    }

    /// <summary>Train on one flood, test on an independent one: the LUT (built only from the training flood) classifies
    /// the test flood as well as its own training events — no over-fitting beyond counting noise. Spots overlap enough
    /// (σ = 0.06 against a half spacing of ≈ 0.13 at the compressed edge) that mis-identification is well above zero;
    /// tolerance: 4 standard errors of the difference of two binomial fractions with the pooled rate.</summary>
    [Fact]
    public void TrainTestSplit_GivesStatisticallyEqualMisIdentification()
    {
        var train = Spots(1500, 0.06, 101);
        var test = Spots(1500, 0.06, 202);
        var lut = Calibrate(train);
        Assert.True(lut.Succeeded, lut.Failure);
        double Mis(List<(int Crystal, double X, double Y)> set) => set.Count(p => lut.Lookup(p.X, p.Y) != p.Crystal) / (double)set.Count;
        double pTrain = Mis(train), pTest = Mis(test), pooled = 0.5 * (pTrain + pTest);
        Assert.True(pooled > 0.005, $"mis-identification too small to test: {pooled}");
        double se = Math.Sqrt(pooled * (1 - pooled) * (1.0 / train.Count + 1.0 / test.Count));
        Stat.Within(pTest, pTrain, se, what: "test vs train mis-identification");
    }

    [Fact]
    public void UnresolvedFlood_FailsExplicitly()
    {
        var rng = new DefaultRandom(9);
        var uniform = Enumerable.Range(0, 20_000).Select(_ => (rng.NextDouble() * 2 - 1, rng.NextDouble() * 2 - 1)).ToList();
        var lut = FloodLut.Calibrate(uniform, 6, 6, new FloodCalibrationConfig());
        Assert.False(lut.Succeeded);
        Assert.Contains("flood peaks", lut.Failure);
        Assert.Equal(-1, lut.Lookup(0, 0));
        Assert.Empty(lut.Peaks);
        var empty = FloodLut.Calibrate([], 6, 6, new FloodCalibrationConfig());
        Assert.False(empty.Succeeded);
        Assert.Equal("empty calibration flood", empty.Failure);
    }

    /// <summary>Spots that merge (σ comparable to the spacing) must not be forced into a grid.</summary>
    [Fact]
    public void MergedSpots_FailInsteadOfBeingForcedIntoAGrid()
    {
        var lut = Calibrate(Spots(400, 0.2, 5));
        Assert.False(lut.Succeeded);
        Assert.Contains("flood peaks", lut.Failure);
    }

    [Fact]
    public void PointsOutsideThePlane_AreUnassigned()
    {
        var lut = Calibrate(Spots(400, 0.02, 1));
        Assert.Equal(-1, lut.Lookup(1.0, 0));
        Assert.Equal(-1, lut.Lookup(0, -1.2));
        Assert.Equal(-1, lut.Lookup(double.NaN, 0));
    }

    /// <summary>A transported readout flood (6 × 6 array, ideal divider, 662 keV): calibration on one flood, validation on
    /// an independent flood of different transport, light and noise streams — photopeak single-crystal mis-identification
    /// equals the calibration flood's own within the binomial bound (pooled rate, k = 4); at least the photopeak spots must
    /// be found.</summary>
    [Fact]
    public void TransportedFlood_CalibrationGeneralisesToAnIndependentFlood()
    {
        var cfg = Rigs.Lab();
        cfg.Detector.PixelsX = cfg.Detector.PixelsY = 6;
        cfg.Detector.PixelPitchMm = 3.2;
        cfg.Detector.ReflectorGapMm = 0.2;
        var ro = new ReadoutConfig
        {
            Mode = ReadoutMode.FourOutputAnger,
            Network = new ChargeNetworkConfig { Topology = NetworkTopology.IdealBilinear },
            Optics = new ReadoutOpticsConfig { PhotonsPerBin = 3000 },
        };
        var device = new ReadoutDevice(cfg.Detector, ro, 21);
        var processor = new ReadoutPulseProcessor(device, ro.Pulse, ro.Trigger);
        var trainFlood = ReadoutFlood.Transport(cfg, 661.7, 36 * 1500, 31);
        var testFlood = ReadoutFlood.Transport(cfg, 661.7, 36 * 1500, 32);
        var cal = ReadoutCalibration.Build(device, processor, trainFlood, ro.Calibration, new DefaultRandom(41), new DefaultRandom(42));
        Assert.True(cal.Succeeded, cal.Failure);
        (int K, int N) Mis(List<InteractionSite[]> flood, int seed)
        {
            var response = new DefaultRandom(seed);
            var noise = new DefaultRandom(seed + 1);
            var ch = new double[device.Channels];
            int k = 0, n = 0;
            foreach (var sites in flood)
            {
                double sum = device.Respond(sites, response, ch);
                var ev = processor.ProcessIsolated(new ReadoutHit(0, ch, sum), noise);
                if (ev is null || !device.Position(ev.Codes, out double x, out double y)) continue;
                int c = cal.Lut.Lookup(x, y);
                if (c < 0 || sites.Any(s => s.CrystalX != sites[0].CrystalX || s.CrystalY != sites[0].CrystalY)) continue;
                if (Math.Abs(cal.Energy(c, device.Sum(ev.Codes)) - 661.7) > 0.15 * 661.7) continue;
                n++;
                if (c != sites[0].CrystalY * 6 + sites[0].CrystalX) k++;
            }
            return (k, n);
        }
        var (kTrain, nTrain) = Mis(trainFlood, 41);
        var (kTest, nTest) = Mis(testFlood, 51);
        Assert.True(nTrain > 5000 && nTest > 5000);
        double pTrain = (double)kTrain / nTrain, pTest = (double)kTest / nTest, pooled = (double)(kTrain + kTest) / (nTrain + nTest);
        // With zero pooled mis-identifications both rates are 0 — equal; otherwise the binomial bound.
        if (pooled > 0)
            Stat.Within(pTest, pTrain, Math.Sqrt(pooled * (1 - pooled) * (1.0 / nTrain + 1.0 / nTest)), what: "photopeak mis-ID test vs train");
    }
}
