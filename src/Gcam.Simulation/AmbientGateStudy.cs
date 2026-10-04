using System.Diagnostics;
using System.Text.Json;
using Gcam.Configuration;
using Gcam.Core;

namespace Gcam.Simulation;

/// <summary>AB-7 gate study and the AB-9 count-gate re-measurement for one outer seed (TODO-30 turn 7).
///
/// Acquisitions are fixed live-time Poisson flood maps: every pixel's count is an exact Poisson draw
/// (<see cref="Sampling.PoissonExact"/>) with mean (activity × source map + dose rate × ambient map) × exposure, the
/// maps coming from <see cref="GateResponse"/> — the flood-map form of the transported event process (AB-1). The
/// ambient "truth" map and the background shape the search statistic uses are two independent Monte Carlo estimates
/// (the latter stands for the instrument's transported background model). Decoding is the scenario's own decoder
/// (<see cref="CorrelationSearch"/>), unchanged.
///
/// Per acquisition two answers are recorded: the decoder's estimate on the raw image (what the camera reports — the
/// EV-07 / EV-09 quantity), and the search statistic Z = max studentised correlation with its grid location (the
/// trusted-location gate: trusted when Z exceeds the threshold selected on other seeds). A located source is correct
/// when its direction is within one angular resolution element, atan(cell pitch / D), of the truth.</summary>
public static class AmbientGateStudy
{
    public static Dictionary<string, object?> Run(AmbientGateRequest q)
    {
        var clock = Stopwatch.StartNew();
        if (q.Phase is not ("pilot" or "selection" or "validation")) throw new ArgumentException($"Unknown phase {q.Phase}.");
        string Repo(string relative) => Path.GetFullPath(Path.Combine(q.RepoRoot, relative));
        var spectrum = IncidentSpectrumFile.Load(Repo(q.Spectrum.File), q.Spectrum.Sha256);
        Thresholds? thresholds = null;
        if (q.Phase == "validation")
        {
            var t = q.Thresholds ?? throw new ArgumentException("The validation phase needs the selected thresholds.");
            byte[] bytes = File.ReadAllBytes(Repo(t.File));
            if (IncidentSpectrumFile.Sha256Hex(bytes) != t.Sha256) throw new InvalidDataException("Threshold file does not match its pinned SHA-256.");
            thresholds = JsonSerializer.Deserialize<Thresholds>(bytes) ?? throw new InvalidDataException("Empty threshold file.");
        }

        var output = new Dictionary<string, object?>
        {
            ["SchemaVersion"] = 1, ["Kind"] = "ambient-gate", ["Phase"] = q.Phase, ["Seed"] = q.Seed,
            ["Spectrum"] = new { spectrum.Id, spectrum.ContentHash, FileSha256 = q.Spectrum.Sha256, spectrum.IsValidated }
        };
        var timings = new Dictionary<string, double>();
        var ambientRates = new Dictionary<string, object>();
        var sourceRates = new Dictionary<string, object>();
        var nulls = new Dictionary<string, double?[]>();
        var nullSummary = new Dictionary<string, object>();
        var sources = new Dictionary<string, object>();

        // Ambient maps per scenario head and bound (independent of the source distance): truth and calibration.
        var scenarios = q.Cases.Select(c => c.Scenario).Distinct().ToArray();
        var heads = new Dictionary<string, (SimulationConfig Config, CountingWindow[] Windows, GateMaps[] Truth, GateMaps[] Calibration)>();
        for (int s = 0; s < scenarios.Length; s++)
        {
            var config = ConfigLoader.Load(Repo(scenarios[s]));
            var windows = q.Windows.Select(w => new CountingWindow(w.Name, w.LowKeV, w.HighKeV, config.Detector.EnergyResolutionFwhm)).ToArray();
            var truth = new GateMaps[q.Bounds.Length];
            var calibration = new GateMaps[q.Bounds.Length];
            for (int b = 0; b < q.Bounds.Length; b++)
            {
                var watch = Stopwatch.StartNew();
                truth[b] = GateResponse.Ambient(config, q.Bounds[b], spectrum, q.AmbientHistories,
                    GateResponse.StreamSeed(q.Seed, 50000u + 16u * (uint)s + (uint)b), windows);
                timings[$"ambient|{scenarios[s]}|{q.Bounds[b]}"] = watch.Elapsed.TotalSeconds;
                if (q.Phase != "pilot")
                    calibration[b] = GateResponse.Ambient(config, q.Bounds[b], spectrum, q.CalibrationHistories,
                        GateResponse.StreamSeed(q.Seed, 51000u + 16u * (uint)s + (uint)b), windows);
                for (int w = 0; w < windows.Length; w++)
                    ambientRates[$"{scenarios[s]}|{q.Bounds[b]}|{windows[w].Name}"] = new
                    {
                        CpsPerMicroSvH = truth[b].TotalRate(w), StandardError = truth[b].TotalRateStandardError[w],
                        CalibrationCpsPerMicroSvH = q.Phase != "pilot" ? calibration[b].TotalRate(w) : (double?)null,
                        truth[b].Trials, truth[b].Events
                    };
            }
            heads[scenarios[s]] = (config, windows, truth, calibration);
        }

        uint nullIndex = 0, conditionIndex = 0;
        for (int c = 0; c < q.Cases.Length; c++)
        {
            var spec = q.Cases[c];
            var (head, windows, truth, calibration) = heads[spec.Scenario];
            var config = head.Clone();
            config.Geometry.SourceMaskDistanceMm = spec.SourceDetectorMm - config.Geometry.MaskDetectorDistanceMm;
            if (!(config.Geometry.SourceMaskDistanceMm > 0)) throw new ArgumentException($"Case {spec.Name}: source inside the head.");
            var watch = Stopwatch.StartNew();
            var search = new CorrelationSearch(config);
            double resolutionDeg = Math.Atan(config.Mask.CellPitchMm / config.Geometry.MaskDetectorDistanceMm) * 180 / Math.PI;
            var sourceMaps = new GateMaps[spec.Positions.Length];
            for (int p = 0; p < spec.Positions.Length; p++)
            {
                var pcfg = config.Clone();
                pcfg.Source.Position = [spec.Positions[p].XMm, spec.Positions[p].YMm, 0];
                sourceMaps[p] = GateResponse.Source(pcfg, q.SourcePhotons, GateResponse.StreamSeed(q.Seed, 52000u + 64u * (uint)c + (uint)p), windows);
                for (int w = 0; w < windows.Length; w++)
                    sourceRates[$"{spec.Name}|{spec.Positions[p].Name}|{windows[w].Name}"] = new
                    {
                        CpsPerBq = sourceMaps[p].TotalRate(w), StandardError = sourceMaps[p].TotalRateStandardError[w],
                        AngleDeg = search.AngleBetweenDeg(0, 0, spec.Positions[p].XMm, spec.Positions[p].YMm)
                    };
            }
            timings[$"maps|{spec.Name}"] = watch.Elapsed.TotalSeconds;
            if (q.Phase == "pilot") continue;

            var models = new CorrelationSearch.BackgroundModel[q.Bounds.Length, windows.Length];
            for (int b = 0; b < q.Bounds.Length; b++)
                for (int w = 0; w < windows.Length; w++)
                    models[b, w] = search.ModelFor(calibration[b].RatePerPixel[w]);
            var counts = new int[search.Pixels];
            var recon = new double[search.GridPoints];
            var lambda = new double[search.Pixels];

            // Background-only acquisitions: the false-trusted-location domain.
            watch.Restart();
            foreach (double t in spec.ExposuresS)
                foreach (double field in q.FieldsMicroSvPerHour)
                    for (int b = 0; b < q.Bounds.Length; b++)
                        for (int w = 0; w < windows.Length; w++)
                        {
                            string key = NullKey(spec.Name, t, field, q.Bounds[b], windows[w].Name);
                            for (int i = 0; i < lambda.Length; i++) lambda[i] = field * t * truth[b].RatePerPixel[w][i];
                            var rng = DefaultRandom.FromKey(DefaultRandom.Key(q.Seed, 60000u + nullIndex++));
                            var z = new double?[q.NullRepeats];
                            for (int r = 0; r < q.NullRepeats; r++)
                            {
                                int total = Draw(rng, lambda, counts);
                                search.Reconstruct(counts, recon);
                                var hit = search.Search(models[b, w], total, recon);
                                z[r] = double.IsFinite(hit.Z) ? Math.Round(hit.Z, Thresholds.RecordedDecimals) : null;
                            }
                            nulls[key] = z;
                            nullSummary[key] = new { ExpectedBackgroundCounts = lambda.Sum() };
                        }
            timings[$"nulls|{spec.Name}"] = watch.Elapsed.TotalSeconds;
            if (q.Phase != "validation") continue;

            // Source acquisitions: ideal (no field) and every field × bound, same source map (paired outer seed).
            watch.Restart();
            var environments = new List<(string Name, double Field, int Bound)> { ("ideal", 0, -1) };
            foreach (double field in q.FieldsMicroSvPerHour)
                for (int b = 0; b < q.Bounds.Length; b++) environments.Add(($"F={field}|{q.Bounds[b]}", field, b));
            for (int p = 0; p < spec.Positions.Length; p++)
            {
                var pos = spec.Positions[p];
                for (int w = 0; w < windows.Length; w++)
                {
                    double pilot = q.PilotRatePerBq[$"{spec.Name}|{pos.Name}|{windows[w].Name}"];
                    foreach (double t in spec.ExposuresS)
                    {
                        var activities = q.SourceLevels.Select(s => ($"S={s}", s / (t * pilot)))
                            .Append(("default", q.DefaultActivityBq)).ToArray();
                        foreach (var (label, activity) in activities)
                            foreach (var env in environments)
                            {
                                double sourceCounts = 0, backgroundCounts = 0;
                                for (int i = 0; i < lambda.Length; i++)
                                {
                                    double s = activity * t * sourceMaps[p].RatePerPixel[w][i];
                                    double bg = env.Bound < 0 ? 0 : env.Field * t * truth[env.Bound].RatePerPixel[w][i];
                                    lambda[i] = s + bg; sourceCounts += s; backgroundCounts += bg;
                                }
                                double? threshold = null, universal = null;
                                if (env.Bound >= 0)
                                {
                                    string nk = NullKey(spec.Name, t, env.Field, q.Bounds[env.Bound], windows[w].Name);
                                    threshold = thresholds!.PerConfiguration[nk];
                                    universal = thresholds.Universal;
                                }
                                var rng = DefaultRandom.FromKey(DefaultRandom.Key(q.Seed, 70000u + conditionIndex++));
                                var agg = new SourceAggregate();
                                for (int r = 0; r < q.SourceRepeats; r++)
                                {
                                    int total = Draw(rng, lambda, counts);
                                    search.Reconstruct(counts, recon);
                                    var (ex, ey) = search.DecoderEstimate(recon);
                                    double errMm = Math.Sqrt((ex - pos.XMm) * (ex - pos.XMm) + (ey - pos.YMm) * (ey - pos.YMm));
                                    double errDeg = search.AngleBetweenDeg(ex, ey, pos.XMm, pos.YMm);
                                    agg.AddDecoder(total, ex - pos.XMm, ey - pos.YMm, errDeg, errMm > q.FailThresholdMm, errDeg <= resolutionDeg);
                                    if (env.Bound >= 0)
                                    {
                                        var hit = search.Search(models[env.Bound, w], total, recon);
                                        bool correct = hit.Index >= 0 && search.AngleBetweenDeg(hit.X, hit.Y, pos.XMm, pos.YMm) <= resolutionDeg;
                                        agg.AddGate(thresholds!.Trusts(hit.Z, threshold!.Value), thresholds.Trusts(hit.Z, universal!.Value), correct);
                                    }
                                }
                                sources[$"{spec.Name}|{pos.Name}|{windows[w].Name}|t={t}|{label}|{env.Name}"] =
                                    agg.Summary(activity, sourceCounts, backgroundCounts, resolutionDeg, env.Bound >= 0);
                            }
                    }
                }
            }
            timings[$"sources|{spec.Name}"] = watch.Elapsed.TotalSeconds;
        }

        output["AmbientRates"] = ambientRates;
        output["SourceRates"] = sourceRates;
        output["NullBackground"] = nullSummary;
        output["Nulls"] = nulls;
        output["Sources"] = sources;
        output["Timings"] = timings;
        output["ComputeSeconds"] = clock.Elapsed.TotalSeconds;
        return output;
    }

    public static string NullKey(string caseName, double exposureS, double field, AmbientGeometry bound, string window)
        => $"{caseName}|t={exposureS}|F={field}|{bound}|{window}";

    private static int Draw(IRandom rng, double[] lambda, int[] counts)
    {
        int total = 0;
        for (int i = 0; i < lambda.Length; i++) total += counts[i] = Sampling.PoissonExact(rng, lambda[i]);
        return total;
    }

    /// <summary>Thresholds selected on the selection seeds: one per null configuration, and one universal value, with the
    /// comparison they were selected for. <c>GreaterThan</c> (turn 7, the default when the file names none): trusted when
    /// Z &gt; T. <c>RoundedAtLeast</c> (AB-11): trusted when Z rounded to <see cref="RecordedDecimals"/> decimals — the
    /// precision the null values are recorded and selected at — is ≥ T, so an acquisition tied with the threshold counts
    /// as trusted (conservative for the false-trusted rate).</summary>
    public sealed class Thresholds
    {
        public const int RecordedDecimals = 4;
        public Dictionary<string, double> PerConfiguration { get; set; } = [];
        public double Universal { get; set; }
        public string Comparison { get; set; } = "GreaterThan";

        public bool Trusts(double z, double threshold) => double.IsFinite(z) && Comparison switch
        {
            "GreaterThan" => z > threshold,
            "RoundedAtLeast" => Math.Round(z, RecordedDecimals) >= threshold,
            _ => throw new InvalidDataException($"Unknown threshold comparison '{Comparison}'.")
        };
    }

    private sealed class SourceAggregate
    {
        private int _n, _fail, _decoderWithin, _detected, _correct, _detectedUniversal, _correctUniversal;
        private double _sumErr2, _sumErr, _sumDeg2, _sumCounts, _sumDx, _sumDy;

        /// <summary>One decoder answer; (dx, dy) is the signed error at the source plane (estimate − truth, mm), kept so
        /// a systematic pull of the raw decoder (AB-12) is measured as a mean vector, not only inside the RMS.</summary>
        public void AddDecoder(int total, double dxMm, double dyMm, double errDeg, bool fail, bool within)
        {
            double errMm = Math.Sqrt(dxMm * dxMm + dyMm * dyMm);
            _n++; _sumCounts += total; _sumErr2 += errMm * errMm; _sumErr += errMm; _sumDeg2 += errDeg * errDeg;
            _sumDx += dxMm; _sumDy += dyMm;
            if (fail) _fail++;
            if (within) _decoderWithin++;
        }

        public void AddGate(bool trusted, bool trustedUniversal, bool correct)
        {
            if (trusted) { _detected++; if (correct) _correct++; }
            if (trustedUniversal) { _detectedUniversal++; if (correct) _correctUniversal++; }
        }

        public object Summary(double activity, double sourceCounts, double backgroundCounts, double resolutionDeg, bool gated) => new
        {
            ActivityBq = activity, ExpectedSourceCounts = sourceCounts, ExpectedBackgroundCounts = backgroundCounts,
            Repeats = _n, MeanCounts = _sumCounts / _n, ResolutionDeg = resolutionDeg,
            RmsErrorMm = Math.Sqrt(_sumErr2 / _n), MeanErrorMm = _sumErr / _n, RmsErrorDeg = Math.Sqrt(_sumDeg2 / _n),
            MeanDxMm = _sumDx / _n, MeanDyMm = _sumDy / _n,
            Failures = _fail, DecoderWithinResolution = _decoderWithin,
            Trusted = gated ? _detected : (int?)null, TrustedCorrect = gated ? _correct : (int?)null,
            TrustedUniversal = gated ? _detectedUniversal : (int?)null, TrustedCorrectUniversal = gated ? _correctUniversal : (int?)null
        };
    }
}
