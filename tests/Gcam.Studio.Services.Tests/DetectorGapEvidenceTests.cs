using Gcam.Configuration;
using Gcam.Detector;
using Gcam.Simulation;
using Gcam.Studio.Core.Services;
using Gcam.Studio.Services;
using Xunit.Abstractions;

namespace Gcam.Studio.Services.Tests;

/// <summary>Opt-in Studio-path MC evidence, with independent spread and validation seeds.</summary>
public sealed class DetectorGapEvidenceTests(ITestOutputHelper output)
{
    [EvidenceFact]
    [Trait("Category", "Evidence")]
    public void RateRatio_MatchesGeometricArea_WithinMeasuredSeedSpread()
    {
        const int calibrationSeeds = 8, validationSeeds = 4;
        const double liveS = 60, pitch = .6;
        // This is a per-seed predictive bound, not an estimator precision claim. Measure independent ratios
        // first; 4*s*sqrt(1+1/n) includes uncertainty of the calibration mean. No fixed borrowed tolerance.
        var ratios = new double[2, calibrationSeeds + validationSeeds];
        for (int i = 0; i < calibrationSeeds + validationSeeds; i++)
        {
            int seed = 43001 + i * 1009;
            var baseline = Count(0, seed);
            for (int g = 0; g < 2; g++)
            {
                double gap = (g + 1) * .1;
                var result = Count(gap, seed);
                ratios[g, i] = result.All / (double)baseline.All;
                output.WriteLine($"seed={seed}, gap={gap * 1000:0} um, baseline={baseline.All}, counts={result.All}, cps={result.All / liveS:F6}, ratio={ratios[g, i]:F8}, window={result.Window}");
            }
        }
        for (int g = 0; g < 2; g++)
        {
            double gap = (g + 1) * .1, expected = Math.Pow((pitch - gap) / pitch, 2);
            var measured = Enumerable.Range(0, calibrationSeeds).Select(i => ratios[g, i]).ToArray();
            double mean = measured.Average();
            double spread = Math.Sqrt(measured.Sum(r => Math.Pow(r - mean, 2)) / (calibrationSeeds - 1));
            double tolerance = 4 * spread * Math.Sqrt(1 + 1d / calibrationSeeds);
            output.WriteLine($"gap={gap * 1000:0} um: expected={expected:F8}, calibration mean={mean:F8}, sample s={spread:F8}, tolerance=4*s*sqrt(1+1/8)={tolerance:F8}");
            Assert.True(spread > 0, "A measured seed spread is required; no fallback tolerance is allowed.");
            Assert.InRange(Math.Abs(mean - expected), 0, tolerance);
            for (int i = calibrationSeeds; i < calibrationSeeds + validationSeeds; i++)
                Assert.InRange(Math.Abs(ratios[g, i] - expected), 0, tolerance);
        }

        (int All, int Window) Count(double gap, int seed)
        {
            var detector = new DetectorSettings { ReflectorGapMm = gap };
            var config = SimulationService.BuildConfig([new SceneSource { X = 0, Y = 0, DistanceMm = 1000, ActivityUCi = 500 }],
                new OpticsSettings(), detector); config.Seed = seed;
            using var source = new ListModeSource(config);
            var measurement = new MeasurementStage(detector, 30, 30);
            var model = new FrontEndModel(detector.Chain.BuildConfig());
            double halfWindow = 1.5 * 661.7 * model.FwhmFraction(661.7);
            int count = 0, window = 0;
            while (source.ArrivalTimeS <= liveS)
            {
                var ev = source.Advance();
                if (ev is not { } e || e.ArrivalTimeS > liveS) continue;
                if (Math.Abs(measurement.Measure(e, count) - 661.7) <= halfWindow) window++;
                count++;
            }
            return (count, window);
        }
    }
}
