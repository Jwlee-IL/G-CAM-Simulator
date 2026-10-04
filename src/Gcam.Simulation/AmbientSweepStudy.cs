using System.Diagnostics;
using Gcam.Configuration;
using Gcam.Core;
using Gcam.Decoding;
using Gcam.Masks;

namespace Gcam.Simulation;

/// <summary>EV-01 under the absolute ambient field (AB-13, TODO-30 turn 8) and the AB-12 decoder-bias baseline for TODO-33.
///
/// The legacy sweep (<c>montecarlo sweep</c>: ±18 mm grid, 1.5 mm step, cyclic and non-cyclic decoding on the same
/// ±18 mm / 0.75 mm reconstruction grid, "localized" = error &lt; 3 mm) decodes a noiseless mean map per position. With an
/// absolute field the source needs an activity and a live time: those of the recipe's scenario
/// (<c>Source.ActivityBq</c>, <c>Source.AcquisitionTimeSeconds</c> — the values the scenario clone carries), each position
/// is acquired <see cref="AmbientEvidenceRequest.Repeats"/> times as exact Poisson flood maps of
/// activity × t × source map + field × t × ambient map, and the localized count becomes an expectation:
/// Σ over positions of the fraction of acquisitions within 3 mm. The ideal environment is run at the same activity and
/// time (Poisson, no field), beside the legacy noiseless count.
///
/// Fixed points (EV-01's single-run quotes: centre, off-axis, ghost) are decoded with the scenario's own decoder over
/// <see cref="AmbientEvidenceRequest.SweepSpec.PointRepeats"/> acquisitions, and with MLEM (no background term, as the
/// engine's <see cref="MlemDecoder"/>) over <see cref="AmbientEvidenceRequest.SweepSpec.MlemRepeats"/>: the signed mean
/// error is the decoder's systematic pull under the field (AB-12). The noiseless pull is the decoder's answer for the
/// expected ambient map alone.</summary>
public static class AmbientSweepStudy
{
    public static Dictionary<string, object?> Run(AmbientEvidenceRequest q)
    {
        var clock = Stopwatch.StartNew();
        var spec = q.Sweep ?? throw new ArgumentException("The sweep family needs its Sweep section.");
        var spectrum = AmbientEvidence.LoadSpectrum(q);
        var output = new Dictionary<string, object?>
        {
            ["SchemaVersion"] = 1, ["Kind"] = "ambient-evidence", ["Family"] = "sweep", ["Seed"] = q.Seed,
            ["Spectrum"] = new { spectrum.Id, spectrum.ContentHash, FileSha256 = q.Spectrum.Sha256, spectrum.IsValidated }
        };
        var scenarios = new Dictionary<string, object>();
        var timings = new Dictionary<string, double>();
        for (int sc = 0; sc < spec.Scenarios.Length; sc++)
        {
            var item = spec.Scenarios[sc];
            var watch = Stopwatch.StartNew();
            var config = ConfigLoader.Load(AmbientEvidence.Repo(q, item.Scenario));
            config.Seed = q.Seed;
            double activity = config.Source.ActivityBq, t = config.Source.AcquisitionTimeSeconds;
            var windows = AmbientEvidence.Windows(q, config);
            uint basePurpose = 110000u + 10000u * (uint)sc;
            var truth = new GateMaps[q.Bounds.Length];
            for (int b = 0; b < q.Bounds.Length; b++)
                truth[b] = GateResponse.Ambient(config, q.Bounds[b], spectrum, q.AmbientHistories, GateResponse.StreamSeed(q.Seed, basePurpose + (uint)b), windows);
            var environments = new List<(string Name, double Field, int Bound)> { ("ideal", 0, -1) };
            foreach (double field in q.FieldsMicroSvPerHour)
                for (int b = 0; b < q.Bounds.Length; b++) environments.Add(($"F={field}|{q.Bounds[b]}", field, b));
            int pixels = config.Detector.PixelsX * config.Detector.PixelsY;
            var counts = new int[pixels];
            var lambda = new double[pixels];
            uint streamIndex = 0;
            timings[$"{item.Name}|ambient"] = watch.Elapsed.TotalSeconds;

            // --- the sweep ---
            watch.Restart();
            int n = (int)Math.Round(2 * spec.HalfExtentMm / spec.StepMm) + 1;
            var searches = new[] { true, false }.Select(cyclic =>
            {
                var c = config.Clone();
                c.Decoder = new DecoderConfig { Cyclic = cyclic, ReconHalfExtentMm = spec.HalfExtentMm, ReconStepMm = spec.ReconStepMm };
                return (Name: cyclic ? "cyclic" : "noncyclic", Search: new CorrelationSearch(c));
            }).ToArray();
            var recon = new double[searches[0].Search.GridPoints];
            var sweep = new Dictionary<string, object>();
            var localized = new double[environments.Count * windows.Length * searches.Length][];
            var meanDx = new double[localized.Length][];
            var meanDy = new double[localized.Length][];
            for (int k = 0; k < localized.Length; k++) { localized[k] = new double[n * n]; meanDx[k] = new double[n * n]; meanDy[k] = new double[n * n]; }
            for (int p = 0; p < n * n; p++)
            {
                double sx = -spec.HalfExtentMm + p % n * spec.StepMm, sy = -spec.HalfExtentMm + p / n * spec.StepMm;
                var c = config.Clone();
                c.Source.Position = [sx, sy, 0];
                var map = GateResponse.Source(c, q.SourcePhotons, GateResponse.StreamSeed(q.Seed, basePurpose + 100u + (uint)p), windows);
                for (int w = 0; w < windows.Length; w++)
                    for (int e = 0; e < environments.Count; e++)
                    {
                        var env = environments[e];
                        AmbientEvidence.Mean(lambda, activity * t, map.RatePerPixel[w], env.Field * t, env.Bound < 0 ? null : truth[env.Bound].RatePerPixel[w]);
                        var rng = AmbientEvidence.Stream(q.Seed, basePurpose + 2000u + streamIndex++);
                        for (int r = 0; r < q.Repeats; r++)
                        {
                            AmbientEvidence.Draw(rng, lambda, counts);
                            for (int d = 0; d < searches.Length; d++)
                            {
                                searches[d].Search.Reconstruct(counts, recon);
                                var (ex, ey) = searches[d].Search.DecoderEstimate(recon);
                                int k = (e * windows.Length + w) * searches.Length + d;
                                if ((ex - sx) * (ex - sx) + (ey - sy) * (ey - sy) < spec.LocalizedWithinMm * spec.LocalizedWithinMm) localized[k][p] += 1.0 / q.Repeats;
                                meanDx[k][p] += (ex - sx) / q.Repeats; meanDy[k][p] += (ey - sy) / q.Repeats;
                            }
                        }
                    }
            }
            for (int w = 0; w < windows.Length; w++)
                for (int e = 0; e < environments.Count; e++)
                    for (int d = 0; d < searches.Length; d++)
                    {
                        int k = (e * windows.Length + w) * searches.Length + d;
                        sweep[$"{windows[w].Name}|{environments[e].Name}|{searches[d].Name}"] = new
                        {
                            ExpectedLocalized = localized[k].Sum(), MajorityLocalized = localized[k].Count(f => f >= 0.5),
                            Positions = n * n,
                            Fraction = localized[k].Select(v => Math.Round(v, 4)).ToArray(),
                            MeanDxMm = meanDx[k].Select(v => Math.Round(v, 4)).ToArray(),
                            MeanDyMm = meanDy[k].Select(v => Math.Round(v, 4)).ToArray()
                        };
                    }
            timings[$"{item.Name}|sweep"] = watch.Elapsed.TotalSeconds;

            // --- fixed points: the scenario's own decoder (EV-01 single runs) and MLEM (AB-12) ---
            watch.Restart();
            var own = new CorrelationSearch(config);
            var ownRecon = new double[own.GridPoints];
            var mlem = Mlem(config, spec.MlemIterations);
            double period = config.Mask.Rank * config.Mask.CellPitchMm * (config.Geometry.MaskDetectorDistanceMm + config.Geometry.SourceMaskDistanceMm)
                            / config.Geometry.MaskDetectorDistanceMm;
            var points = new Dictionary<string, object>();
            var image = new DetectorImage(config.Detector.PixelsX, config.Detector.PixelsY);
            for (int i = 0; i < item.PointsMm.Length; i++)
            {
                double px = item.PointsMm[i][0], py = item.PointsMm[i][1];
                var c = config.Clone();
                c.Source.Position = [px, py, 0];
                var map = GateResponse.Source(c, q.SourcePhotons, GateResponse.StreamSeed(q.Seed, basePurpose + 5000u + (uint)i), windows);
                for (int w = 0; w < windows.Length; w++)
                    foreach (var env in environments)
                    {
                        double[]? bg = env.Bound < 0 ? null : truth[env.Bound].RatePerPixel[w];
                        AmbientEvidence.Mean(lambda, activity * t, map.RatePerPixel[w], env.Field * t, bg);
                        var rng = AmbientEvidence.Stream(q.Seed, basePurpose + 6000u + streamIndex++);
                        var cc = new AmbientEvidence.ErrorTally(q.FailThresholdMm, q.FailThresholdMm);
                        int ghost = 0;
                        for (int r = 0; r < spec.PointRepeats; r++)
                        {
                            AmbientEvidence.Draw(rng, lambda, counts);
                            own.Reconstruct(counts, ownRecon);
                            var (ex, ey) = own.DecoderEstimate(ownRecon);
                            cc.Add(ex - px, ey - py);
                            // The cyclic alias one period toward the centre (EV-01's opposite-side ghost).
                            double gx = px - Math.Sign(px) * period;
                            if (px != 0 && (ex - gx) * (ex - gx) + (ey - py) * (ey - py) < spec.LocalizedWithinMm * spec.LocalizedWithinMm) ghost++;
                        }
                        var ml = new AmbientEvidence.ErrorTally(q.FailThresholdMm, q.FailThresholdMm);
                        for (int r = 0; r < spec.MlemRepeats; r++)
                        {
                            AmbientEvidence.Draw(rng, lambda, counts);
                            for (int k = 0; k < pixels; k++) image[k % config.Detector.PixelsX, k / config.Detector.PixelsX] = counts[k];
                            var m = mlem.Decode(image).Estimate.Position;
                            ml.Add(m.X - px, m.Y - py);
                        }
                        points[$"({px},{py})|{windows[w].Name}|{env.Name}"] = new
                        {
                            ExpectedSourceCounts = activity * t * map.TotalRate(w),
                            ExpectedBackgroundCounts = bg is null ? 0 : env.Field * t * bg.Sum(),
                            CrossCorrelation = cc.Summary(), GhostFraction = px != 0 ? ghost / (double)spec.PointRepeats : (double?)null,
                            Mlem = ml.Summary()
                        };
                    }
            }
            timings[$"{item.Name}|points"] = watch.Elapsed.TotalSeconds;

            // --- noiseless pull: what each decoder answers for the expected ambient map alone ---
            var pull = new Dictionary<string, object>();
            var decoder = new DefaultSimulationFactory().CreateDecoder(config)!;
            for (int b = 0; b < q.Bounds.Length; b++)
                for (int w = 0; w < windows.Length; w++)
                {
                    var mean = new DetectorImage(config.Detector.PixelsX, config.Detector.PixelsY);
                    for (int k = 0; k < pixels; k++) mean[k % config.Detector.PixelsX, k / config.Detector.PixelsX] = truth[b].RatePerPixel[w][k];
                    var cc = decoder.Decode(mean).Estimate.Position;
                    var ml = mlem.Decode(mean).Estimate.Position;
                    pull[$"{q.Bounds[b]}|{windows[w].Name}"] = new
                    {
                        CrossCorrelationXMm = cc.X, CrossCorrelationYMm = cc.Y, MlemXMm = ml.X, MlemYMm = ml.Y,
                        CpsPerMicroSvH = truth[b].TotalRate(w),
                        EdgeToCentreRatio = EdgeToCentre(truth[b].RatePerPixel[w], config.Detector.PixelsX, config.Detector.PixelsY)
                    };
                }
            scenarios[item.Name] = new
            {
                item.Scenario, ActivityBq = activity, ExposureS = t, PeriodMm = period, GridPositions = n * n,
                Sweep = sweep, Points = points, BackgroundPull = pull
            };
        }
        output["Scenarios"] = scenarios;
        output["Timings"] = timings;
        output["ComputeSeconds"] = clock.Elapsed.TotalSeconds;
        return output;
    }

    /// <summary>MLEM with the scenario decoder's geometry (grid, cyclic flag) and the finite mosaic as its forward model,
    /// built as <see cref="MlemStudy"/> builds it.</summary>
    private static MlemDecoder Mlem(SimulationConfig config, int iterations)
    {
        var m = config.Mask;
        var pattern = MuraGenerator.Mosaic(m.Rank, m.MosaicX, m.MosaicY);
        double maskZ = config.Geometry.MaskDetectorDistanceMm, sourceZ = maskZ + config.Geometry.SourceMaskDistanceMm;
        double period = m.Rank * m.CellPitchMm / (maskZ / sourceZ);
        var geo = new CodedApertureGeometry(m.Rank, maskZ, m.CellPitchMm, pattern.Width, pattern.Height, 0.0,
            config.Detector.PixelPitchMm, sourceZ, config.Decoder.ReconHalfExtentMm ?? period / 2.0,
            config.Decoder.ReconStepMm ?? period / 48.0, config.Decoder.Cyclic);
        return new MlemDecoder(pattern, geo, iterations);
    }

    /// <summary>Mean rate of the outermost pixel ring over the mean of the rest: how far from flat a background map is.</summary>
    private static double EdgeToCentre(double[] map, int width, int height)
    {
        double edge = 0, inner = 0; int ne = 0, ni = 0;
        for (int k = 0; k < map.Length; k++)
        {
            int x = k % width, y = k / width;
            if (x == 0 || y == 0 || x == width - 1 || y == height - 1) { edge += map[k]; ne++; } else { inner += map[k]; ni++; }
        }
        return ni > 0 && inner > 0 ? edge / ne / (inner / ni) : double.NaN;
    }
}
