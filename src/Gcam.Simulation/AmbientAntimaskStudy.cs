using System.Diagnostics;
using Gcam.Configuration;
using Gcam.Core;

namespace Gcam.Simulation;

/// <summary>EV-12 under the absolute ambient field (AB-13, TODO-30 turn 8): single mask, calibrated background subtraction
/// and a physical two-exposure mask / antimask at <b>equal total live time</b>, as the legacy
/// <see cref="MaskAntimaskStudy"/> compares them — but with the background a transported, source-independent field
/// instead of a uniform pedestal.
///
/// For each window and live time t the source activity is set so the mask exposure expects
/// <see cref="AmbientEvidenceRequest.AntimaskSpec.SourceCounts"/> net counts in the full time (the legacy 400). Then
/// (a) single: one exposure of t through the mask, decoded raw; (b) calibrated: the same image minus the instrument's
/// background model (an independent MC map × field × t — the "known mean background" of the legacy study); (c) antimask:
/// t/2 through the mask and t/2 through the inverted mask at the same activity, the difference decoded. The background
/// of the antimask exposure is the field through the inverted mask: in the front-only bound it is coded differently by
/// the two masks; in the bare bound the side and rear faces do not see the mask, so one map serves both exposures.</summary>
public static class AmbientAntimaskStudy
{
    public static Dictionary<string, object?> Run(AmbientEvidenceRequest q)
    {
        var clock = Stopwatch.StartNew();
        var spec = q.Antimask ?? throw new ArgumentException("The antimask family needs its Antimask section.");
        var spectrum = AmbientEvidence.LoadSpectrum(q);
        var config = ConfigLoader.Load(AmbientEvidence.Repo(q, spec.Scenario));
        config.Seed = q.Seed;
        var windows = AmbientEvidence.Windows(q, config);
        var maskA = config.Clone(); maskA.Mask.Invert = false;
        var maskB = config.Clone(); maskB.Mask.Invert = true;

        var sourceA = GateResponse.Source(maskA, q.SourcePhotons, GateResponse.StreamSeed(q.Seed, 80001), windows);
        var sourceB = GateResponse.Source(maskB, q.SourcePhotons, GateResponse.StreamSeed(q.Seed, 80002), windows);
        var truthA = new GateMaps[q.Bounds.Length];
        var truthB = new GateMaps[q.Bounds.Length];
        var model = new GateMaps[q.Bounds.Length];
        for (int b = 0; b < q.Bounds.Length; b++)
        {
            uint p = 80100u + 16u * (uint)b;
            truthA[b] = GateResponse.Ambient(maskA, q.Bounds[b], spectrum, q.AmbientHistories, GateResponse.StreamSeed(q.Seed, p), windows);
            // Bare bound: the transport never meets the mask (side / rear faces open, top face unmasked), so the antimask
            // exposure sees the same field — the same expected map, not a second noisy estimate of it.
            truthB[b] = q.Bounds[b] == AmbientGeometry.BareCrystalAllFaces ? truthA[b]
                : GateResponse.Ambient(maskB, q.Bounds[b], spectrum, q.AmbientHistories, GateResponse.StreamSeed(q.Seed, p + 1), windows);
            model[b] = GateResponse.Ambient(maskA, q.Bounds[b], spectrum, q.CalibrationHistories, GateResponse.StreamSeed(q.Seed, p + 2), windows);
        }
        double mapSeconds = clock.Elapsed.TotalSeconds;

        var search = new CorrelationSearch(maskA);
        var decoder = new DefaultSimulationFactory().CreateDecoder(maskA)!;
        double tx = config.Source.Position[0], ty = config.Source.Position[1];
        int pixels = search.Pixels, width = config.Detector.PixelsX, height = config.Detector.PixelsY;
        var countsA = new int[pixels]; var countsHalfA = new int[pixels]; var countsHalfB = new int[pixels]; var diff = new int[pixels];
        var lambdaA = new double[pixels]; var lambdaHalfA = new double[pixels]; var lambdaHalfB = new double[pixels];
        var recon = new double[search.GridPoints];
        var calibrated = new DetectorImage(width, height);

        var rates = new Dictionary<string, object>();
        for (int w = 0; w < windows.Length; w++)
        {
            rates[$"source|mask|{windows[w].Name}"] = new { CpsPerBq = sourceA.TotalRate(w), StandardError = sourceA.TotalRateStandardError[w] };
            rates[$"source|antimask|{windows[w].Name}"] = new { CpsPerBq = sourceB.TotalRate(w), StandardError = sourceB.TotalRateStandardError[w] };
            for (int b = 0; b < q.Bounds.Length; b++)
            {
                rates[$"ambient|mask|{q.Bounds[b]}|{windows[w].Name}"] = new { CpsPerMicroSvH = truthA[b].TotalRate(w), StandardError = truthA[b].TotalRateStandardError[w] };
                rates[$"ambient|antimask|{q.Bounds[b]}|{windows[w].Name}"] = new { CpsPerMicroSvH = truthB[b].TotalRate(w), StandardError = truthB[b].TotalRateStandardError[w] };
                rates[$"model|mask|{q.Bounds[b]}|{windows[w].Name}"] = new { CpsPerMicroSvH = model[b].TotalRate(w) };
            }
        }

        var conditions = new Dictionary<string, object>();
        uint streamIndex = 0;
        for (int w = 0; w < windows.Length; w++)
        {
            // Environments: ideal (no field; the live time only scales the activity, so one ideal per window) and every
            // field × bound × live time.
            var environments = new List<(string Name, double Field, int Bound, double T)> { ("ideal", 0, -1, q.ExposuresS[0]) };
            foreach (double t in q.ExposuresS)
                foreach (double field in q.FieldsMicroSvPerHour)
                    for (int b = 0; b < q.Bounds.Length; b++) environments.Add(($"t={t}|F={field}|{q.Bounds[b]}", field, b, t));
            foreach (var env in environments)
            {
                double activity = spec.SourceCounts / (env.T * sourceA.TotalRate(w));
                double[]? bgA = env.Bound < 0 ? null : truthA[env.Bound].RatePerPixel[w];
                double[]? bgB = env.Bound < 0 ? null : truthB[env.Bound].RatePerPixel[w];
                double[]? bgModel = env.Bound < 0 ? null : model[env.Bound].RatePerPixel[w];
                AmbientEvidence.Mean(lambdaA, activity * env.T, sourceA.RatePerPixel[w], env.Field * env.T, bgA);
                AmbientEvidence.Mean(lambdaHalfA, activity * env.T / 2, sourceA.RatePerPixel[w], env.Field * env.T / 2, bgA);
                AmbientEvidence.Mean(lambdaHalfB, activity * env.T / 2, sourceB.RatePerPixel[w], env.Field * env.T / 2, bgB);
                var rng = AmbientEvidence.Stream(q.Seed, 81000u + streamIndex++);
                var single = new AmbientEvidence.ErrorTally(q.FailThresholdMm, q.FailThresholdMm);
                var calib = new AmbientEvidence.ErrorTally(q.FailThresholdMm, q.FailThresholdMm);
                var anti = new AmbientEvidence.ErrorTally(q.FailThresholdMm, q.FailThresholdMm);
                for (int r = 0; r < q.Repeats; r++)
                {
                    AmbientEvidence.Draw(rng, lambdaA, countsA);
                    AmbientEvidence.Draw(rng, lambdaHalfA, countsHalfA);
                    AmbientEvidence.Draw(rng, lambdaHalfB, countsHalfB);
                    search.Reconstruct(countsA, recon);
                    var (sx, sy) = search.DecoderEstimate(recon);
                    single.Add(sx - tx, sy - ty);
                    for (int i = 0; i < pixels; i++)
                        calibrated[i % width, i / width] = countsA[i] - (bgModel is null ? 0 : env.Field * env.T * bgModel[i]);
                    var c = decoder.Decode(calibrated).Estimate.Position;
                    calib.Add(c.X - tx, c.Y - ty);
                    for (int i = 0; i < pixels; i++) diff[i] = countsHalfA[i] - countsHalfB[i];
                    search.Reconstruct(diff, recon);
                    var (ax, ay) = search.DecoderEstimate(recon);
                    anti.Add(ax - tx, ay - ty);
                }
                double background = bgA is null ? 0 : env.Field * env.T * bgA.Sum();
                conditions[$"{windows[w].Name}|{env.Name}"] = new
                {
                    ActivityBq = activity, ExposureS = env.Bound < 0 ? (double?)null : env.T,
                    ExpectedSourceCounts = spec.SourceCounts, ExpectedBackgroundCounts = background,
                    BackgroundPerPixel = background / pixels,
                    ExpectedAntimaskSourceCounts = activity * env.T / 2 * sourceB.TotalRate(w),
                    ExpectedAntimaskBackgroundCounts = bgB is null ? 0 : env.Field * env.T / 2 * bgB.Sum(),
                    Single = single.Summary(), Calibrated = calib.Summary(), Antimask = anti.Summary()
                };
            }
        }
        return new Dictionary<string, object?>
        {
            ["SchemaVersion"] = 1, ["Kind"] = "ambient-evidence", ["Family"] = "antimask", ["Seed"] = q.Seed,
            ["Spectrum"] = new { spectrum.Id, spectrum.ContentHash, FileSha256 = q.Spectrum.Sha256, spectrum.IsValidated },
            ["Rates"] = rates, ["Conditions"] = conditions,
            ["MapSeconds"] = mapSeconds, ["ComputeSeconds"] = clock.Elapsed.TotalSeconds
        };
    }
}
