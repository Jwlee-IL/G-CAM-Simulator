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

    [Theory]
    [InlineData(32767)]     // the largest absolute count sum of the 8/16-bit path
    [InlineData(32768)]     // the smallest of the 32-bit path
    [InlineData(250000)]
    public void MatrixDecode_IsExactOnBothAccumulatorWidths(int total)
    {
        // Signed counts whose absolute sum is exactly `total`, spread unevenly over the pixels; both paths are exact
        // integer arithmetic, so the reconstruction equals the decoder's to the last bit on either side of the switch.
        var config = Rigs.Lab(seed: 3108);
        var search = new CorrelationSearch(config);
        var decoder = new DefaultSimulationFactory().CreateDecoder(config)!;
        var counts = new int[search.Pixels];
        var rng = new DefaultRandom(3109);
        for (int left = total; left > 0;)
        {
            int i = (int)(rng.NextDouble() * counts.Length), step = Math.Min(left, 1 + (int)(rng.NextDouble() * 50));
            counts[i] += i % 5 == 0 ? -step : step;
            left -= step;
        }
        // Pixel 1 only ever receives positive steps (1 % 5 ≠ 0), so adding the deficit left by cancelling steps on the
        // other pixels raises the absolute sum to exactly `total`.
        counts[1] += total - counts.Sum(Math.Abs);
        Assert.Equal(total, counts.Sum(Math.Abs));
        var image = new DetectorImage(config.Detector.PixelsX, config.Detector.PixelsY);
        for (int i = 0; i < counts.Length; i++) image[i % image.Width, i / image.Width] = counts[i];
        var recon = new double[search.GridPoints];
        search.Reconstruct(counts, recon);
        Assert.Equal(decoder.Decode(image).Reconstruction.Raw.ToArray(), recon);
    }

    [Fact]
    public void MatrixDecode_IsExactOnAWideGridAndSignedCounts()
    {
        // EV-02's wide non-cyclic grid (±20°, 0.3° at 1 m: > 8192 points, so the heap accumulator; the lab grid of the test
        // above, 49 × 49, exercises the scalar tail after the SIMD blocks) and a mask − antimask difference image (negative
        // counts): still exact integers.
        var config = Rigs.Handheld(seed: 3106);
        config.Geometry.SourceMaskDistanceMm = 1000;
        double planeZ = config.Geometry.MaskDetectorDistanceMm + 1000;
        config.Decoder.Cyclic = false;
        config.Decoder.ReconHalfExtentMm = planeZ * Math.Tan(20 * Math.PI / 180);
        config.Decoder.ReconStepMm = planeZ * Math.Tan(0.3 * Math.PI / 180);
        var search = new CorrelationSearch(config);
        Assert.True(search.GridPoints > 8192);
        var decoder = new DefaultSimulationFactory().CreateDecoder(config)!;
        var rng = new DefaultRandom(3107);
        var counts = new int[search.Pixels];
        var recon = new double[search.GridPoints];
        for (int trial = 0; trial < 3; trial++)
        {
            var image = new DetectorImage(config.Detector.PixelsX, config.Detector.PixelsY);
            for (int i = 0; i < counts.Length; i++)
            {
                counts[i] = Sampling.PoissonExact(rng, 2.0 + trial) - Sampling.PoissonExact(rng, 2.0 + trial);
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

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void ExpectedMapDecode_ReproducesTheDecoderOnARealImage(bool cyclic)
    {
        // A real-valued map (an expected source map, a model background): the per-grid-point sums run over the pixels in
        // the decoder's order and a weight of 0 adds +0, so the result equals the decoder's to the last bit.
        var config = Rigs.Lab(seed: 3110);
        config.Decoder.Cyclic = cyclic;
        var search = new CorrelationSearch(config);
        var decoder = new DefaultSimulationFactory().CreateDecoder(config)!;
        var rng = new DefaultRandom(3111);
        var map = new double[search.Pixels];
        var image = new DetectorImage(config.Detector.PixelsX, config.Detector.PixelsY);
        for (int i = 0; i < map.Length; i++) image[i % image.Width, i / image.Width] = map[i] = i % 9 == 0 ? 0 : rng.NextDouble() * 3.7;
        var recon = new double[search.GridPoints];
        search.ReconstructExpected(map, recon);
        Assert.Equal(decoder.Decode(image).Reconstruction.Raw.ToArray(), recon);
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
