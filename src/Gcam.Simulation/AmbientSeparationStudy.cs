using System.Diagnostics;
using Gcam.Configuration;
using Gcam.Core;

namespace Gcam.Simulation;

/// <summary>EV-15 under the absolute ambient field (AB-13, TODO-30 turn 8): Cs-137 with Co-60 at the declared activities
/// and live time, the 662 keV window decoded for position (the spatial lever) and per-pixel Compton stripping of that
/// window with the Co photopeak window (the spectral lever), now with the transported field in both windows.
///
/// Per acquisition the two windows are independent exact Poisson images (disjoint pulse-height windows of one Poisson
/// process). The stripping ratio R = Co downscatter into the 662 window ÷ Co photopeak counts is calibrated, as in the
/// legacy <c>mixedstrip</c>, on a separate noiseless Co-only map at the axis (a laboratory calibration). Three Cs-count
/// estimates are recorded against the true expected Cs count in the 662 window, S: the legacy per-pixel
/// Σ max(0, n662 − R·nCo); the unfloored Σ n662 − R·Σ nCo; and the same after subtracting the instrument's background
/// model (an independent ambient MC map × field × t) from both windows. The Cs position is the nearest of the two
/// strongest peaks of the raw 662-window reconstruction (legacy <c>spatial</c>: non-cyclic, 2 peaks ≥ 3 mm apart).
///
/// Turn 9 (AB-14) adds, without changing any draw: the Cs count read from that peak (the spatial lever) — the
/// reconstruction value at the matched Cs peak divided by the peak value per count of the noiseless Cs-only map
/// (raw, and with the background model's reconstruction removed); the Cs position from the stripped reconstruction
/// recon(n662) − R·recon(nCo) (the decoder is linear, so this is the decode of the stripped image, also co-located); and
/// the Co position from the Co-window reconstruction (the scenario decoder's estimate).</summary>
public static class AmbientSeparationStudy
{
    private const double PeakSeparationMm = 3.0;      // legacy MixedFieldStudy.LocalizeMultiple(k = 2, 3 mm)
    private const double LocatedWithinMm = 1.0;       // "sub-mm" Cs location (legacy quotes 0.47 mm vs 8.7 mm lost)

    public static Dictionary<string, object?> Run(AmbientEvidenceRequest q)
    {
        var clock = Stopwatch.StartNew();
        var spec = q.Separation ?? throw new ArgumentException("The separation family needs its Separation section.");
        var spectrum = AmbientEvidence.LoadSpectrum(q);
        var config = ConfigLoader.Load(AmbientEvidence.Repo(q, spec.Scenario));
        config.Seed = q.Seed;
        config.Decoder.Cyclic = false;
        config.Decoder.ReconHalfExtentMm = spec.ReconHalfExtentMm;
        config.Decoder.ReconStepMm = spec.ReconStepMm;
        var windows = AmbientEvidence.Windows(q, config);
        int cs = Array.FindIndex(windows, w => w.Name == spec.CsWindow), co = Array.FindIndex(windows, w => w.Name == spec.CoWindow);
        if (cs < 0 || co < 0) throw new ArgumentException("CsWindow and CoWindow must name declared windows.");
        SimulationConfig At(double[] p) { var c = config.Clone(); c.Source.Position = [p[0], p[1], 0]; return c; }

        var coCal = AmbientEvidence.SourceLines(At([0, 0]), spec.CoLines, q.SourcePhotons, q.Seed, 100000u, windows);
        double r = coCal.TotalRate(cs) / coCal.TotalRate(co);
        var truth = new GateMaps[q.Bounds.Length];
        var model = new GateMaps[q.Bounds.Length];
        for (int b = 0; b < q.Bounds.Length; b++)
        {
            truth[b] = GateResponse.Ambient(config, q.Bounds[b], spectrum, q.AmbientHistories, GateResponse.StreamSeed(q.Seed, 100100u + 4u * (uint)b), windows);
            model[b] = GateResponse.Ambient(config, q.Bounds[b], spectrum, q.CalibrationHistories, GateResponse.StreamSeed(q.Seed, 100101u + 4u * (uint)b), windows);
        }
        var search = new CorrelationSearch(config);
        int pixels = search.Pixels;
        double t = spec.ExposureS;
        var n662 = new int[pixels]; var nCo = new int[pixels];
        var l662 = new double[pixels]; var lCo = new double[pixels];
        var recon = new double[search.GridPoints];
        var reconCo = new double[search.GridPoints]; var stripped = new double[search.GridPoints];
        var modelRecon = new double[search.GridPoints]; var scratch = new double[pixels];
        var image = new DetectorImage(search.GridSize, search.GridSize);
        var conditions = new Dictionary<string, object>();
        var rates = new Dictionary<string, object> { ["R"] = r };
        uint streamIndex = 0;

        for (int s = 0; s < spec.Scenes.Length; s++)
        {
            var scene = spec.Scenes[s];
            var csMap = AmbientEvidence.SourceLines(At(scene.CsMm), spec.CsLines, q.SourcePhotons, q.Seed, 100200u + 16u * (uint)s, windows);
            var coMap = AmbientEvidence.SourceLines(At(scene.CoMm), spec.CoLines, q.SourcePhotons, q.Seed, 100208u + 16u * (uint)s, windows);
            foreach (var w in new[] { cs, co })
                rates[$"{scene.Name}|{windows[w].Name}"] = new { CsCpsPerBq = csMap.TotalRate(w), CoCpsPerBq = coMap.TotalRate(w) };
            double trueCs = spec.CsActivityBq * t * csMap.TotalRate(cs);
            // Peak reconstruction value per Cs count: the noiseless Cs-only 662-window map, decoded, its maximum ÷ its total.
            search.ReconstructExpected(csMap.RatePerPixel[cs], recon);
            double peakPerCount = recon.Max() / csMap.TotalRate(cs);
            bool separated = Math.Abs(scene.CsMm[0] - scene.CoMm[0]) + Math.Abs(scene.CsMm[1] - scene.CoMm[1]) > 0;
            var environments = new List<(string Name, double Field, int Bound)> { ("ideal", 0, -1) };
            foreach (double field in q.FieldsMicroSvPerHour)
                for (int b = 0; b < q.Bounds.Length; b++) environments.Add(($"F={field}|{q.Bounds[b]}", field, b));
            foreach (var env in environments)
            {
                for (int i = 0; i < pixels; i++)
                {
                    double bg662 = env.Bound < 0 ? 0 : env.Field * t * truth[env.Bound].RatePerPixel[cs][i];
                    double bgCo = env.Bound < 0 ? 0 : env.Field * t * truth[env.Bound].RatePerPixel[co][i];
                    l662[i] = t * (spec.CsActivityBq * csMap.RatePerPixel[cs][i] + spec.CoActivityBq * coMap.RatePerPixel[cs][i]) + bg662;
                    lCo[i] = t * (spec.CsActivityBq * csMap.RatePerPixel[co][i] + spec.CoActivityBq * coMap.RatePerPixel[co][i]) + bgCo;
                }
                double model662 = env.Bound < 0 ? 0 : env.Field * t * model[env.Bound].TotalRate(cs);
                double modelCo = env.Bound < 0 ? 0 : env.Field * t * model[env.Bound].TotalRate(co);
                var rng = AmbientEvidence.Stream(q.Seed, 101000u + streamIndex++);
                var floored = new double[q.Repeats]; var unfloored = new double[q.Repeats]; var subtracted = new double[q.Repeats];
                var csError = new double[q.Repeats];
                var spatial = new double[q.Repeats]; var spatialSubtracted = new double[q.Repeats];
                var csStrippedError = new double[q.Repeats]; var coError = new double[q.Repeats];
                // Background model's reconstruction in the 662 window (zero without a field).
                Array.Clear(modelRecon);
                if (env.Bound >= 0)
                {
                    for (int i = 0; i < pixels; i++) scratch[i] = env.Field * t * model[env.Bound].RatePerPixel[cs][i];
                    search.ReconstructExpected(scratch, modelRecon);
                }
                for (int k = 0; k < q.Repeats; k++)
                {
                    int total662 = AmbientEvidence.Draw(rng, l662, n662);
                    int totalCo = AmbientEvidence.Draw(rng, lCo, nCo);
                    double f = 0;
                    for (int i = 0; i < pixels; i++) f += Math.Max(0, n662[i] - r * nCo[i]);
                    floored[k] = f;
                    unfloored[k] = total662 - r * totalCo;
                    subtracted[k] = total662 - model662 - r * (totalCo - modelCo);
                    search.Reconstruct(n662, recon);
                    for (int g = 0; g < recon.Length; g++) image[g % search.GridSize, g / search.GridSize] = recon[g];
                    var peaks = MixedFieldStudy.TopPeaks(image, search.OriginMm, search.StepMm, 2, PeakSeparationMm);
                    var match = MixedFieldStudy.MatchOneToOne([[scene.CsMm[0], scene.CsMm[1]], [scene.CoMm[0], scene.CoMm[1]]], peaks);
                    csError[k] = match[0].ErrorMm;
                    // Spatial lever: the value at the peak matched to Cs, in Cs counts.
                    int at = PeakIndex(search, match[0].FoundX, match[0].FoundY);
                    spatial[k] = at < 0 ? double.NaN : recon[at] / peakPerCount;
                    spatialSubtracted[k] = at < 0 ? double.NaN : (recon[at] - modelRecon[at]) / peakPerCount;
                    search.Reconstruct(nCo, reconCo);
                    var (ox, oy) = search.DecoderEstimate(reconCo);
                    coError[k] = Math.Sqrt((ox - scene.CoMm[0]) * (ox - scene.CoMm[0]) + (oy - scene.CoMm[1]) * (oy - scene.CoMm[1]));
                    for (int g = 0; g < recon.Length; g++) stripped[g] = recon[g] - r * reconCo[g];
                    var (sx, sy) = search.DecoderEstimate(stripped);
                    csStrippedError[k] = Math.Sqrt((sx - scene.CsMm[0]) * (sx - scene.CsMm[0]) + (sy - scene.CsMm[1]) * (sy - scene.CsMm[1]));
                }
                conditions[$"{scene.Name}|{env.Name}"] = new
                {
                    TrueCsCounts = trueCs, ExpectedWindowCounts = l662.Sum(), ExpectedCoWindowCounts = lCo.Sum(),
                    ExpectedBackground662 = env.Bound < 0 ? 0 : env.Field * t * truth[env.Bound].TotalRate(cs),
                    ExpectedBackgroundCo = env.Bound < 0 ? 0 : env.Field * t * truth[env.Bound].TotalRate(co),
                    Floored = Relative(floored, trueCs), Unfloored = Relative(unfloored, trueCs), Subtracted = Relative(subtracted, trueCs),
                    CsLocatedWithin1Mm = separated ? csError.Count(e => e <= LocatedWithinMm) / (double)q.Repeats : (double?)null,
                    CsErrorMedianMm = separated ? Quantile(csError, 0.5) : (double?)null,
                    PeakPerCount = peakPerCount,
                    Spatial = separated ? Relative(spatial, trueCs) : null,
                    SpatialSubtracted = separated ? Relative(spatialSubtracted, trueCs) : null,
                    CsStrippedWithin1Mm = csStrippedError.Count(e => e <= LocatedWithinMm) / (double)q.Repeats,
                    CsStrippedErrorMedianMm = Quantile(csStrippedError, 0.5),
                    CoWithin1Mm = coError.Count(e => e <= LocatedWithinMm) / (double)q.Repeats,
                    CoErrorMedianMm = Quantile(coError, 0.5)
                };
            }
        }
        return new Dictionary<string, object?>
        {
            ["SchemaVersion"] = 1, ["Kind"] = "ambient-evidence", ["Family"] = "separation", ["Seed"] = q.Seed,
            ["Spectrum"] = new { spectrum.Id, spectrum.ContentHash, FileSha256 = q.Spectrum.Sha256, spectrum.IsValidated },
            ["ExposureS"] = t, ["Rates"] = rates, ["Conditions"] = conditions, ["ComputeSeconds"] = clock.Elapsed.TotalSeconds
        };
    }

    private static int PeakIndex(CorrelationSearch search, double x, double y)
    {
        int gx = (int)Math.Round((x - search.OriginMm) / search.StepMm), gy = (int)Math.Round((y - search.OriginMm) / search.StepMm);
        return gx < 0 || gy < 0 || gx >= search.GridSize || gy >= search.GridSize ? -1 : gy * search.GridSize + gx;
    }

    /// <summary>Relative error of a Cs-count estimate, (estimate − S) / S, over the repeats: median, quartiles, mean, SD.</summary>
    private static object Relative(double[] estimates, double trueCounts)
    {
        var e = estimates.Select(v => (v - trueCounts) / trueCounts).ToArray();
        double mean = e.Average();
        return new
        {
            Median = Quantile(e, 0.5), Q1 = Quantile(e, 0.25), Q3 = Quantile(e, 0.75), Mean = mean,
            Sd = Math.Sqrt(e.Sum(v => (v - mean) * (v - mean)) / Math.Max(1, e.Length - 1)),
            MedianAbs = Quantile(e.Select(Math.Abs).ToArray(), 0.5)
        };
    }

    private static double Quantile(double[] values, double p)
    {
        var s = values.OrderBy(v => v).ToArray();
        double h = (s.Length - 1) * p;
        int lo = (int)Math.Floor(h);
        return lo + 1 < s.Length ? s[lo] + (h - lo) * (s[lo + 1] - s[lo]) : s[lo];
    }
}
