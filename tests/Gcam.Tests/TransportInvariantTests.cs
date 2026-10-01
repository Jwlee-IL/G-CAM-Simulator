using Gcam.Core;
using Gcam.Detector;
using Gcam.Masks;
using Gcam.Simulation;
using Gcam.Tests.Harness;

namespace Gcam.Tests;

/// <summary>Each transport stage against its closed form: the biased source's weight is the detector's solid angle,
/// the crystal stops 1 − e^(−μ·t/cosθ), the mask passes its open fraction plus the leak through tungsten, and the
/// Compton cascade never deposits more than the photon brought. Plus run-level determinism, progress and
/// cancellation.</summary>
public class TransportInvariantTests
{
    private const int N = 200_000;

    // Solid angle of a 2a × 2b rectangle seen from a point on its axis at distance h.
    private static double RectangleSolidAngle(double a, double b, double h)
        => 4.0 * Math.Asin(a * b / Math.Sqrt((a * a + h * h) * (b * b + h * h)));

    [Theory]
    [InlineData(160.0)]
    [InlineData(1055.0)]
    [InlineData(20.0)]      // near field: far from the 1/r² limit
    public void BiasedSource_MeanWeightIsTheDetectorSolidAngle(double h)
    {
        const double half = 8.0;
        var source = new DetectorBiasedSource(new Vector3(0, 0, h), 661.7, half, half, detPlaneZ: 0.0);
        var w = source.Emit(new DefaultRandom(3), N).Select(p => p.Weight).ToArray();
        var (mean, sigma) = Stat.MeanWithError(w);
        Stat.Within(mean, RectangleSolidAngle(half, half, h) / (4 * Math.PI), sigma, what: $"Ω/4π at h = {h} mm");
    }

    [Theory]
    [InlineData(661.7, 0.0)]
    [InlineData(316.5, 0.0)]
    [InlineData(661.7, 30.0)]
    public void Crystal_StopsOneMinusExpOfMuTimesSlantPath(double keV, double angleDeg)
    {
        const double mu662 = 0.0513, depth = 15.0;
        var material = CrystalMaterial.ForConfig("GAGG_Mg");
        var det = new CrystalDetector(16, 16, 1.0, planeZ: 0.0, crystalMuPerMm: mu662, crystalDepthMm: depth, material: material);
        double th = angleDeg * Math.PI / 180.0;
        var dir = new Vector3(Math.Sin(th), 0, -Math.Cos(th));
        const int n = 1000;
        for (int i = 0; i < n; i++)
            det.Score(new Photon { Ray = new Ray(new Vector3(0.5 - 10 * Math.Tan(th), 0.5, 10), dir), EnergyKeV = keV });

        double stopped = det.Readout().Raw.ToArray().Sum() / n;
        double expected = 1 - Math.Exp(-mu662 * material.MuRel(keV) * depth / Math.Cos(th));
        Assert.Equal(expected, stopped, 9);   // the plain detector scores the probability, not a sample
    }

    [Theory]
    [InlineData(100.0)]    // opaque: only open cells pass
    [InlineData(0.178)]    // 10 mm W at 662 keV: closed cells leak e^(−1.78) ≈ 17 %
    public void Mask_PassesItsOpenFractionPlusTheTungstenLeak(double muPerMm)
    {
        var pattern = MuraGenerator.Mosaic(7, 2, 2);
        const double cell = 1.0, thickness = 10.0;
        var mask = new CodedApertureMask(pattern, planeZ: 60.0, cell, thickness, muPerMm);
        var rng = new DefaultRandom(11);
        double half = pattern.Width * cell / 2.0;
        int passed = 0;
        for (int i = 0; i < N; i++)
        {
            // Normal incidence, uniform over the mask: no channel clipping, so only the open area matters.
            var origin = new Vector3((rng.NextDouble() * 2 - 1) * half, (rng.NextDouble() * 2 - 1) * half, 100.0);
            if (mask.Transmit(new Ray(origin, new Vector3(0, 0, -1)), 661.7, rng)) passed++;
        }
        double open = pattern.OpenFraction();
        double expected = open + (1 - open) * Math.Exp(-muPerMm * thickness);
        Stat.Within(passed / (double)N, expected, Stat.BinomialSigma(expected, N), what: $"transmission at μ = {muPerMm}");
    }

    [Theory]
    [InlineData(661.7)]
    [InlineData(1332.5)]
    [InlineData(122.1)]
    public void ComptonCascade_NeverDepositsMoreThanThePhotonEnergy(double keV)
    {
        var deposits = new List<double>();
        var det = new ComptonCrystalDetector(16, 16, 1.0, windowCenterKeV: keV, windowFraction: 1.0,
            ComptonStrategy.Argmax, new DefaultRandom(21), crystalDepthMm: 15.0,
            eventSink: (dep, _) => deposits.Add(dep), material: CrystalMaterial.ForConfig("GAGG_Mg"));
        var rng = new DefaultRandom(22);
        for (int i = 0; i < 50_000; i++)
        {
            var origin = new Vector3((rng.NextDouble() * 2 - 1) * 8, (rng.NextDouble() * 2 - 1) * 8, 5);
            det.Score(new Photon { Ray = new Ray(origin, new Vector3(0, 0, -1)), EnergyKeV = keV });
        }

        Assert.NotEmpty(deposits);
        Assert.All(deposits, d => Assert.InRange(d, 1e-9, keV * (1 + 1e-12)));
        // Full absorption happens, and so does partial (Compton escape) — both ends of the spectrum exist.
        Assert.Contains(deposits, d => Math.Abs(d - keV) < 1e-6);
        Assert.Contains(deposits, d => d < 0.9 * keV);
    }

    [Fact]
    public void Run_IsDeterministicForASeed_AndDiffersAcrossSeeds()
    {
        var runner = new SimulationRunner(new DefaultSimulationFactory());
        var a = runner.Run(Rigs.Lab(photons: 50_000, seed: 1)).DetectorImage.Raw.ToArray();
        var b = runner.Run(Rigs.Lab(photons: 50_000, seed: 1)).DetectorImage.Raw.ToArray();
        var c = runner.Run(Rigs.Lab(photons: 50_000, seed: 2)).DetectorImage.Raw.ToArray();
        Assert.Equal(a, b);
        Assert.NotEqual(a, c);
    }

    [Fact]
    public void Run_ReportsMonotoneProgressEndingAtOne()
    {
        var seen = new List<double>();
        new SimulationRunner(new DefaultSimulationFactory())
            .Run(Rigs.Lab(photons: 40_000), new SyncProgress(seen), CancellationToken.None);
        Assert.NotEmpty(seen);
        for (int i = 1; i < seen.Count; i++) Assert.True(seen[i] >= seen[i - 1], $"progress went back at {i}");
        Assert.Equal(1.0, seen[^1]);
    }

    [Fact]
    public void Run_StopsWhenCancelled()
    {
        using var cts = new CancellationTokenSource();
        var progress = new SyncProgress(new List<double>(), onReport: _ => cts.Cancel());
        Assert.ThrowsAny<OperationCanceledException>(() =>
            new SimulationRunner(new DefaultSimulationFactory()).Run(Rigs.Lab(photons: 1_000_000), progress, cts.Token));
    }

    // IProgress<T> that reports synchronously (Progress<T> would post to a thread-pool context).
    private sealed class SyncProgress(List<double> sink, Action<double>? onReport = null) : IProgress<double>
    {
        public void Report(double value) { sink.Add(value); onReport?.Invoke(value); }
    }
}
