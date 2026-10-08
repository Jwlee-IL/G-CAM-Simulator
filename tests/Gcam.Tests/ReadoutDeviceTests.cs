using Gcam.Configuration;
using Gcam.Core;
using Gcam.Detector;
using Gcam.Tests.Harness;

namespace Gcam.Tests;

/// <summary>TODO-19, RD-6: light / charge conservation, four-channel covariance against Wᵀ·Cov(pe)·W, the single-site
/// ideal limit (noiseless, correctly calibrated → the original crystal) and the multisite light centroid (not the
/// arg-max). A 6 × 6 array (3.2 mm pitch, 0.2 mm wall, 10 mm GAGG) keeps the optical tables small.</summary>
public class ReadoutDeviceTests
{
    private static readonly CrystalArrayGeometry Crystals = new(6, 6, 3.2, 0.2, 10.0);

    private static ReadoutConfig Config(ReadoutMode mode = ReadoutMode.FourOutputAnger,
        NetworkTopology topology = NetworkTopology.IdealBilinear) => new()
    {
        Mode = mode,
        Network = new ChargeNetworkConfig { Topology = topology },
        Optics = new ReadoutOpticsConfig { PhotonsPerBin = 3000 },
    };

    private static InteractionSite Site(int ix, int iy, double energy, double heightAboveExit = 5.0)
        => new(Crystals.CenterX(ix), Crystals.CenterY(iy), Crystals.PlaneZ - Crystals.DepthMm + heightAboveExit, 0, energy, ix, iy);

    [Fact]
    public void Expected_ConservesLightThroughOpticsAndNetwork()
    {
        foreach (var topology in new[] { NetworkTopology.IdealBilinear, NetworkTopology.Dpc, NetworkTopology.CornerGrid })
        {
            var device = new ReadoutDevice(Crystals, Config(topology: topology), 5);
            InteractionSite[] sites = [Site(1, 2, 300, 1.0), Site(4, 3, 200, 8.0)];
            var mu = new double[device.Sensors.Count];
            device.ExpectedPhotoelectrons(sites, mu);
            double light = sites.Sum(s => device.Pde * device.PhotonsPerKeV * s.EnergyKeV
                * device.Optics.Collection(s.CrystalY * 6 + s.CrystalX, device.Optics.DepthBin(s.ZMm)));
            Assert.Equal(light, mu.Sum(), 1e-9 * light);
            var ch = new double[device.Channels];
            double sum = device.Expected(sites, ch);
            Assert.Equal(device.CodesPerPe * light, sum, 1e-9 * sum);           // every photoelectron leaves an output
        }
    }

    /// <summary>Cov(Q)_kl = δ_kl·ENF·(μ_k + λ_d) + σ_int²·μ_k·μ_l (Poisson thinning, common intrinsic factor, gain sum with
    /// the exact second moment, baseline-subtracted dark counts) and Cov(channels) = CodesPerPe²·Wᵀ·Cov(Q)·W. Tolerance:
    /// k = 4 normal-theory standard errors of a sample covariance, √((Σ_aa·Σ_bb + Σ_ab²)/(N − 1)); means within 4·√(Σ_aa/N).
    /// The Gaussian branch of the Poisson sampler for means ≥ 30 rounds to integers (+1/12 variance per sensor, below
    /// 1e-4 of the ≥ 10³ pe variances here — negligible against the 4σ band).</summary>
    [Theory]
    [InlineData(NetworkTopology.IdealBilinear)]
    [InlineData(NetworkTopology.Dpc)]
    public void ChannelCovariance_MatchesWTransposeCovPeW(NetworkTopology topology)
    {
        var cfg = Config(topology: topology);
        cfg.Sipm.ExcessNoiseFactor = 1.2;
        cfg.Sipm.DarkCountRatePerMm2Hz = 5e6;                                   // λ_d = 5e6 · 9 mm² · 200 ns = 9 pe
        var device = new ReadoutDevice(Crystals, cfg, 9);
        InteractionSite[] sites = [Site(1, 2, 300, 2.0), Site(4, 3, 200, 7.0)];
        int s = device.Sensors.Count, c = device.Channels;
        var mu = new double[s];
        device.ExpectedPhotoelectrons(sites, mu);
        double var2 = device.IntrinsicSigma * device.IntrinsicSigma, ld = device.DarkMeanPe, enf = device.Enf;
        var cov = new double[c, c];
        var mean = new double[c];
        for (int a = 0; a < c; a++)
        {
            for (int k = 0; k < s; k++) mean[a] += device.CodesPerPe * device.Network[k, a] * mu[k];
            for (int b = 0; b < c; b++)
            {
                double v = 0;
                for (int k = 0; k < s; k++)
                {
                    v += device.Network[k, a] * device.Network[k, b] * enf * (mu[k] + ld);
                    for (int l = 0; l < s; l++) v += device.Network[k, a] * device.Network[l, b] * var2 * mu[k] * mu[l];
                }
                cov[a, b] = v * device.CodesPerPe * device.CodesPerPe;
            }
        }
        const int N = 20_000;
        var rng = new DefaultRandom(2027);
        var samples = new double[N, c];
        var ch = new double[c];
        for (int i = 0; i < N; i++)
        {
            device.Respond(sites, rng, ch);
            for (int a = 0; a < c; a++) samples[i, a] = ch[a];
        }
        var m = new double[c];
        for (int a = 0; a < c; a++) { for (int i = 0; i < N; i++) m[a] += samples[i, a]; m[a] /= N; }
        for (int a = 0; a < c; a++)
        {
            Stat.Within(m[a], mean[a], Math.Sqrt(cov[a, a] / N), what: $"mean of channel {a}");
            for (int b = a; b < c; b++)
            {
                double sab = 0;
                for (int i = 0; i < N; i++) sab += (samples[i, a] - m[a]) * (samples[i, b] - m[b]);
                sab /= N - 1;
                double se = Math.Sqrt((cov[a, a] * cov[b, b] + cov[a, b] * cov[a, b]) / (N - 1));
                Stat.Within(sab, cov[a, b], se, what: $"Cov(channel {a}, channel {b})");
            }
        }
    }

    /// <summary>1:1 coupling, infinite photons (expected values), no noise, a look-up table calibrated on that same ideal
    /// response: every single-site deposit, at every depth, is identified as its own crystal — exactly.</summary>
    [Theory]
    [InlineData(ReadoutMode.FourOutputAnger, NetworkTopology.IdealBilinear)]
    [InlineData(ReadoutMode.FourOutputAnger, NetworkTopology.Dpc)]
    [InlineData(ReadoutMode.IndependentSipm, NetworkTopology.IdealBilinear)]
    public void SingleSiteIdealLimit_IsTheOriginalCrystal(ReadoutMode mode, NetworkTopology topology)
    {
        var device = new ReadoutDevice(Crystals, Config(mode, topology), 4);
        var (lut, points) = IdealLut(device);
        Assert.True(lut.Succeeded, lut.Failure);
        foreach (var (crystal, x, y) in points) Assert.Equal(crystal, lut.Lookup(x, y));
    }

    /// <summary>Two sites in crystals (1, 3) and (4, 3) of the ideal divider: the Anger coordinate IS the light centroid of
    /// the sensors, Σ μ_k ξ_k / Σ μ_k (ξ = sensor centre / half span), to round-off; its crystal (energy centroid
    /// (1·350 + 4·250)/600 = 2.25 → crystal 2, margin 0.25 crystal against the few-per-mille collection differences) is
    /// neither site's — not the arg-max crystal 1.</summary>
    [Fact]
    public void Multisite_AngerPositionIsTheLightCentroid_NotTheArgMax()
    {
        var device = new ReadoutDevice(Crystals, Config(), 4);
        InteractionSite[] sites = [Site(1, 3, 350), Site(4, 3, 250)];
        var mu = new double[device.Sensors.Count];
        device.ExpectedPhotoelectrons(sites, mu);
        double sx = 0, sy = 0, sum = mu.Sum();
        for (int k = 0; k < mu.Length; k++)
        {
            sx += mu[k] * device.Sensors.CenterX(k % 6) / device.Sensors.HalfSpanX;
            sy += mu[k] * device.Sensors.CenterY(k / 6) / device.Sensors.HalfSpanY;
        }
        var ch = new double[4];
        device.Expected(sites, ch);
        Assert.True(device.Position(ch, out double x, out double y));
        Assert.Equal(sx / sum, x, 1e-12);
        Assert.Equal(sy / sum, y, 1e-12);
        var (lut, _) = IdealLut(device);
        Assert.Equal(3 * 6 + 2, lut.Lookup(x, y));
        Assert.Equal(3 * 6 + 1, ReadoutDevice.DirectCrystal(sites, 6));
    }

    /// <summary>The no-recovery saturation limit never fires more cells than a sensor has: with ENF = 1, no dark counts and
    /// no intrinsic spread, each sensor's charge is N·(1 − e^(−n/N)) &lt; N.</summary>
    [Fact]
    public void Saturation_NeverExceedsTheMicrocellCount()
    {
        var cfg = Config(ReadoutMode.IndependentSipm);
        cfg.Sipm.MicrocellsPerMm2 = 400;
        cfg.Sipm.ExcessNoiseFactor = 1.0;
        cfg.Sipm.DarkCountRatePerMm2Hz = 0;
        cfg.Scintillation.IntrinsicResolutionFwhm = 0;
        var device = new ReadoutDevice(Crystals, cfg, 4);
        double cells = 400 * device.Sensors.ActiveAreaMm2;
        var ch = new double[device.Channels];
        var rng = new DefaultRandom(5);
        for (int i = 0; i < 200; i++)
        {
            device.Respond([Site(2, 2, 1332.5, 0.5)], rng, ch);
            Assert.All(ch, q => Assert.True(q / device.CodesPerPe < cells));
        }
        var mu = new double[device.Sensors.Count];
        device.ExpectedPhotoelectrons([Site(2, 2, 1332.5, 0.5)], mu);
        Assert.True(mu.Max() > cells, "the deposit must be large enough to saturate");
    }

    /// <summary>Calibrate a LUT on the noiseless response of one site per crystal and depth bin.</summary>
    private static (FloodLut Lut, List<(int Crystal, double X, double Y)> Points) IdealLut(ReadoutDevice device)
    {
        var points = new List<(int, double, double)>();
        var ch = new double[device.Channels];
        for (int iy = 0; iy < 6; iy++)
            for (int ix = 0; ix < 6; ix++)
                for (int b = 0; b < device.Optics.DepthBins; b++)
                {
                    device.Expected([Site(ix, iy, 661.7, (b + 0.5) * Crystals.DepthMm / device.Optics.DepthBins)], ch);
                    Assert.True(device.Position(ch, out double x, out double y));
                    points.Add((iy * 6 + ix, x, y));
                }
        var lut = FloodLut.Calibrate(points.Select(p => (p.Item2, p.Item3)).ToList(), 6, 6, new FloodCalibrationConfig());
        return (lut, points);
    }
}
