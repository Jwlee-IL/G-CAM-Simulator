using Gcam.Configuration;
using Gcam.Core;
using Gcam.Simulation;
using Gcam.Tests.Harness;

namespace Gcam.Tests;

public sealed class CorrelationSearchTests
{
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void MatrixDecode_ReproducesTheDecoderExactly(bool cyclic)
    {
        // The decoder is linear with ±1 / 0 weights; on integer images both sums are exact, so the reconstruction, the
        // argmax and the sub-cell estimate must be identical, not merely close.
        var config = Rigs.Lab(seed: 3101);
        config.Decoder.Cyclic = cyclic;
        var search = new CorrelationSearch(config);
        var decoder = new DefaultSimulationFactory().CreateDecoder(config)!;
        var rng = new DefaultRandom(3102);
        var counts = new int[search.Pixels];
        var recon = new double[search.GridPoints];
        for (int trial = 0; trial < 20; trial++)
        {
            var image = new DetectorImage(config.Detector.PixelsX, config.Detector.PixelsY);
            double mean = 0.05 + trial;   // sparse to dense images
            for (int i = 0; i < counts.Length; i++)
            {
                counts[i] = Sampling.PoissonExact(rng, mean * (1 + (i % 7)));
                image[i % image.Width, i / image.Width] = counts[i];
            }
            var reference = decoder.Decode(image);
            search.Reconstruct(counts, recon);
            Assert.Equal(reference.Reconstruction.Raw.ToArray(), recon);
            var (x, y) = search.DecoderEstimate(recon);
            Assert.Equal(reference.Estimate.Position.X, x);
            Assert.Equal(reference.Estimate.Position.Y, y);
        }
    }

    [Fact]
    public void StudentisedCorrelation_HasZeroMeanAndUnitVarianceUnderItsBackground()
    {
        // Under a background of shape p with N counts (multinomial), E[recon] = N·Σ G p and Var = N·(Σ G² p − (Σ G p)²)
        // exactly, so Z at any fixed grid point has mean 0 and variance 1. Sample M acquisitions; tolerances are k
        // standard errors of the sample mean (1/√M) and of the sample variance (√((m4 − s⁴)/M), from the sample itself).
        var config = Rigs.Lab(seed: 3103);
        var search = new CorrelationSearch(config);
        var shape = Enumerable.Range(0, search.Pixels).Select(i => 1.0 + (i % 12) * 0.25 + (i / 12) * 0.1).ToArray();
        var model = search.ModelFor(shape);
        double total = shape.Sum();
        var cumulative = shape.Select((v, i) => shape.Take(i + 1).Sum() / total).ToArray();
        var rng = new DefaultRandom(3104);
        const int m = 4000, n = 200;
        int[] points = [0, search.GridPoints / 2, search.GridPoints / 3 + 7];
        var z = points.Select(_ => new double[m]).ToArray();
        var counts = new int[search.Pixels];
        var recon = new double[search.GridPoints];
        for (int r = 0; r < m; r++)
        {
            Array.Clear(counts);
            for (int k = 0; k < n; k++)
            {
                double u = rng.NextDouble();
                int i = Array.FindIndex(cumulative, c => u < c);
                counts[i < 0 ? counts.Length - 1 : i]++;
            }
            search.Reconstruct(counts, recon);
            for (int j = 0; j < points.Length; j++)
                z[j][r] = (recon[points[j]] - n * model.Expected[points[j]]) / Math.Sqrt(n * model.Variance[points[j]]);
        }
        foreach (var sample in z)
        {
            var (mean, variance) = Stat.Moments(sample);
            Stat.Within(mean, 0, 1 / Math.Sqrt(m), what: "mean Z");
            double m4 = sample.Average(v => Math.Pow(v - mean, 4));
            Stat.Within(variance, 1, Math.Sqrt((m4 - variance * variance) / m), what: "variance of Z");
        }
    }

    [Fact]
    public void Search_GivesNoCandidateWithoutCounts()
    {
        var search = new CorrelationSearch(Rigs.Lab(seed: 3105));
        var model = search.ModelFor(Enumerable.Repeat(1.0, search.Pixels).ToArray());
        var hit = search.Search(model, 0, new double[search.GridPoints]);
        Assert.Equal(-1, hit.Index);
        Assert.False(double.IsFinite(hit.Z));
    }
}
