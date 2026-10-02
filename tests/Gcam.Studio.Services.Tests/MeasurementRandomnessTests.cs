using Gcam.Detector;
using Gcam.Studio.Core.Services;
using Xunit.Abstractions;

namespace Gcam.Studio.Services.Tests;

/// <summary>The per-event energy smear of <see cref="MeasurementStage"/> behaves like independent draws (TODO-26). Each
/// event's smear comes from its own stream keyed by (seed, index); under the old generator, reseeded with
/// seed + index·104729, consecutive smears were correlated −0.81 and the window-count variance of 1 000-event blocks
/// was 5 % of the binomial value — spectra fluctuated far less than physically.</summary>
public sealed class MeasurementRandomnessTests(ITestOutputHelper output)
{
    private const int Blocks = 400, PerBlock = 1000;

    [Fact]
    public void Smears_AreUncorrelated_AndWindowCountsAreBinomial()
    {
        var stage = new MeasurementStage(new DetectorSettings(), 30, 30);
        var model = new FrontEndModel(new DetectorSettings().Chain.BuildConfig());
        const double e = 661.7;
        double halfWindow = 0.5 * e * model.FwhmFraction(e);   // ±0.5 FWHM: p ≈ 0.76, far from 0 and 1

        int n = Blocks * PerBlock;
        var x = new double[n];
        var counts = new double[Blocks];
        for (int i = 0; i < n; i++)
        {
            x[i] = stage.MeasureAmplitude(e, i) - e;
            if (Math.Abs(x[i]) <= halfWindow) counts[i / PerBlock]++;
        }

        // Lag-1 correlation of consecutive events' smears: N(0, 1/n) for independent draws.
        double mean = x.Average(), sxy = 0, sxx = 0;
        for (int i = 0; i < n; i++) sxx += (x[i] - mean) * (x[i] - mean);
        for (int i = 1; i < n; i++) sxy += (x[i] - mean) * (x[i - 1] - mean);
        double lag1 = sxy / sxx, sigmaLag = 1 / Math.Sqrt(n);

        // Block window counts: variance / (m·p̂(1−p̂)) ~ χ²(B−1)/(B−1), standard deviation √(2/(B−1)).
        double p = counts.Sum() / n, cm = counts.Average();
        double variance = counts.Sum(c => (c - cm) * (c - cm)) / (Blocks - 1);
        double ratio = variance / (PerBlock * p * (1 - p)), sigmaRatio = Math.Sqrt(2.0 / (Blocks - 1));
        output.WriteLine($"lag-1 corr {lag1:+0.0000;-0.0000} (4σ = {4 * sigmaLag:F4}); in-window p = {p:F4}; " +
                         $"block variance / binomial = {ratio:F3} (4σ band 1 ± {4 * sigmaRatio:F3})");

        Assert.InRange(lag1, -4 * sigmaLag, 4 * sigmaLag);
        Assert.InRange(ratio, 1 - 4 * sigmaRatio, 1 + 4 * sigmaRatio);
    }
}
