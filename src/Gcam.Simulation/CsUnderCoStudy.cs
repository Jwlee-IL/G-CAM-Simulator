using System.Diagnostics;
using System.Globalization;
using System.Text.Json;
using Gcam.Configuration;
using Gcam.Core;

namespace Gcam.Simulation;

/// <summary>TODO-35 / D-42: Cs-137 under Co-60's Compton continuum at use distance (the <c>csco</c> evidence family; plan
/// PLAN.Physics.CsUnderCo60, decisions DA-1 … DA-12). One outer seed per run.
///
/// Response maps (<see cref="GateResponse"/>, per Bq or per µSv/h, every declared window and its gain-shifted copies):
/// Cs-137 and Co-60 per scene ("truth"), Co-60 at extra directions (R only), independent Co-60 calibration maps of declared
/// precision (on axis and at each scene's Co-60 direction), and the transported ambient field per bound (truth and the
/// instrument's model). Acquisitions are exact Poisson draws from those means (<see cref="StrippingStatistics.Poisson"/>).
///
/// Count mode (Q1): per reference window, the stripped count Y = n₆₆₂ − R·n_ref; exact and normal Currie limits from the
/// true means and the scene's true R; Cs-137 at multiples of L_D; three decisions — (A) the a-priori exact rule Y &gt; Y_C
/// with the true R, (P) the plug-in rule (Y − model) / √(n₆₆₂ + R²n_ref) &gt; z with the true R, (B) the same with the
/// calibrated R; window counting (no stripping) reported as its Co-60 bias. Gain errors: acquisitions in the shifted
/// windows judged with the nominal R and threshold.
///
/// Imaging (Q2), product reference window only: the stripped statistic Z_s (<see cref="StrippedSearch"/>, per-pixel Rᵢ
/// from the on-axis calibration map) on the scenario's own decoder grid; thresholds selected per configuration (scene,
/// live time, environment, Co-60 activity) on Cs-free nulls of the selection seeds; the raw 662 keV window through the
/// PR-SENS-02 gate as the counter-case where its threshold exists. Residual study (Q3): Cs-free acquisitions with δR
/// injected, with gain errors, with a global R and with Rᵢ calibrated at Co-60's own direction.</summary>
public static class CsUnderCoStudy
{
    public static Dictionary<string, object?> Run(AmbientEvidenceRequest q)
    {
        var clock = Stopwatch.StartNew();
        var spec = q.CsUnderCo ?? throw new ArgumentException("The csco family needs its CsUnderCo section.");
        if (spec.Phase is not ("selection" or "validation")) throw new ArgumentException($"Unknown phase '{spec.Phase}'.");
        bool validation = spec.Phase == "validation";
        var spectrum = AmbientEvidence.LoadSpectrum(q);
        var zThresholds = validation ? LoadThresholds(q, spec.Thresholds ?? throw new ArgumentException("Validation needs Thresholds.")) : null;
        var rawThresholds = spec.RawGateThresholds is null ? null : LoadThresholds(q, spec.RawGateThresholds);

        var config = ConfigLoader.Load(AmbientEvidence.Repo(q, spec.Scenario));
        config.Seed = q.Seed;
        double d = config.Geometry.MaskDetectorDistanceMm;
        config.Geometry.SourceMaskDistanceMm = spec.SourceDetectorMm - d;
        if (!(config.Geometry.SourceMaskDistanceMm > 0)) throw new ArgumentException("Source inside the head.");
        double elemDeg = Math.Atan(config.Mask.CellPitchMm / d) * 180 / Math.PI;
        (double X, double Y) Point(double[] el) =>
            (spec.SourceDetectorMm * Math.Tan(el[0] * elemDeg * Math.PI / 180), spec.SourceDetectorMm * Math.Tan(el[1] * elemDeg * Math.PI / 180));
        SimulationConfig At((double X, double Y) p) { var c = config.Clone(); c.Source.Position = [p.X, p.Y, 0]; return c; }

        // Windows: the declared ones, then every one again through each gain error.
        var baseWindows = AmbientEvidence.Windows(q, config);
        var windows = new List<CountingWindow>(baseWindows);
        foreach (double g in spec.GainShifts)
            foreach (var w in baseWindows) windows.Add(StrippingStatistics.ForGain(w, g, GainName(w.Name, g)));
        var win = windows.ToArray();
        int W(string name, double g = 0) => Array.FindIndex(win, w => w.Name == (g == 0 ? name : GainName(name, g))) is var i and >= 0 ? i
            : throw new ArgumentException($"Window {name} (gain {g}) not declared.");
        int cs = W(spec.CsWindow);
        var refs = spec.ReferenceWindows;
        if (refs.Length == 0) throw new ArgumentException("At least one reference window.");
        string product = refs[0];

        // Maps.
        var search = new CorrelationSearch(config);
        var strip = new StrippedSearch(search);
        int pixels = search.Pixels;
        var scenes = spec.Scenes;
        var csPos = scenes.Select(s => Point(s.CsElements)).ToArray();
        var coPos = scenes.Select(s => Point(s.CoElements)).ToArray();
        var csMap = new GateMaps[scenes.Length]; var coMap = new GateMaps[scenes.Length]; var coCal = new GateMaps[scenes.Length];
        for (int s = 0; s < scenes.Length; s++)
        {
            coMap[s] = AmbientEvidence.SourceLines(At(coPos[s]), spec.CoLines, q.SourcePhotons, q.Seed, 300008u + 32u * (uint)s, win);
            if (!validation) continue;
            csMap[s] = AmbientEvidence.SourceLines(At(csPos[s]), spec.CsLines, q.SourcePhotons, q.Seed, 300000u + 32u * (uint)s, win);
            coCal[s] = AmbientEvidence.SourceLines(At(coPos[s]), spec.CoLines, spec.CalibrationPhotons, q.Seed, 300016u + 32u * (uint)s, win);
        }
        var axisCal = AmbientEvidence.SourceLines(At((0, 0)), spec.CoLines, spec.CalibrationPhotons, q.Seed, 300500u, win);
        var dirMaps = validation
            ? spec.DirectionsElements.Select((el, i) => AmbientEvidence.SourceLines(At(Point(el)), spec.CoLines, q.SourcePhotons, q.Seed, 300600u + 8u * (uint)i, win)).ToArray()
            : [];
        var truth = new GateMaps[q.Bounds.Length]; var model = new GateMaps[q.Bounds.Length];
        for (int b = 0; b < q.Bounds.Length; b++)
        {
            truth[b] = GateResponse.Ambient(config, q.Bounds[b], spectrum, q.AmbientHistories, GateResponse.StreamSeed(q.Seed, 300800u + 4u * (uint)b), win);
            model[b] = GateResponse.Ambient(config, q.Bounds[b], spectrum, q.CalibrationHistories, GateResponse.StreamSeed(q.Seed, 300801u + 4u * (uint)b), win);
        }
        double mapSeconds = clock.Elapsed.TotalSeconds;

        // Co-60 activity grid, with the activities giving the declared dose rates at the head.
        double dosePerBq = DosePerBq(spec.CoLines, spec.SourceDetectorMm);
        var coLevels = spec.CoActivitiesBq.Concat(spec.CoDoseRatesMicroSvPerHour.Select(h => h / dosePerBq)).Distinct().OrderBy(a => a).ToArray();
        var environments = new List<(string Name, double Field, int Bound)> { ("ideal", 0, -1) };
        foreach (double f in q.FieldsMicroSvPerHour)
            for (int b = 0; b < q.Bounds.Length; b++) environments.Add(($"F={Fmt(f)}|{q.Bounds[b]}", f, b));
        bool EnvApplies((string Name, double Field, int Bound) e, double co) =>
            e.Bound < 0 || q.Bounds[e.Bound] != AmbientGeometry.FrontOnlyThroughMask || co <= spec.FrontOnlyMaxCoBq;
        string Key(int s, double t, string env, double co) => $"{scenes[s].Name}|t={Fmt(t)}|{env}|Co={Fmt(co)}";

        // Per-pixel ratios Rᵢ of a Co map for one reference window (fall back to the map's global R where a pixel has none).
        double[] PixelRatios(GateMaps m, int refIndex)
        {
            double global = m.TotalRate(cs) / m.TotalRate(refIndex);
            return Enumerable.Range(0, pixels).Select(i => m.RatePerPixel[refIndex][i] > 0 ? m.RatePerPixel[cs][i] / m.RatePerPixel[refIndex][i] : global).ToArray();
        }
        int productRef = W(product);
        var rAxisPix = PixelRatios(axisCal, productRef);

        var output = new Dictionary<string, object?>
        {
            ["SchemaVersion"] = 1, ["Kind"] = "ambient-evidence", ["Family"] = "csco", ["Phase"] = spec.Phase, ["Seed"] = q.Seed,
            ["Spectrum"] = new { spectrum.Id, spectrum.ContentHash, FileSha256 = q.Spectrum.Sha256, spectrum.IsValidated },
            ["ElementDeg"] = elemDeg, ["SourceDetectorMm"] = spec.SourceDetectorMm,
            ["Grid"] = new { search.GridSize, search.StepMm, search.OriginMm, strip.FullSupport },
            ["CoDoseMicroSvPerHourPerBq"] = dosePerBq, ["CoLevelsBq"] = coLevels, ["ProductReference"] = product
        };
        output["Ratios"] = RatioSummary(spec, win, cs, refs, W, scenes, csMap, coMap, coCal, axisCal, dirMaps, pixels, config.Detector.PixelsX, validation);
        output["Ambient"] = AmbientSummary(q, truth, model, win, cs, refs, W, axisCal);

        // ------------------------------------------------------------------ imaging nulls (both phases)
        var nulls = new Dictionary<string, double?[]>();
        var nullSummary = new Dictionary<string, object>();
        uint streamIndex = 0;
        var n1 = new int[pixels]; var n2 = new int[pixels];
        var sW = new double[pixels]; var vW = new double[pixels];
        var recon = new double[strip.PaddedPoints]; var variance = new double[strip.PaddedPoints];
        var rawRecon = new double[search.GridPoints];
        var l1 = new double[pixels]; var l2 = new double[pixels];
        var gateModels = model.Select(m => search.ModelFor(m.RatePerPixel[cs])).ToArray();
        bool Within(int index, (double X, double Y) p) => index >= 0 && Angle(strip, index, p) <= elemDeg;
        double Angle(StrippedSearch st, int index, (double X, double Y) p)
        { var (x, y) = st.PointOf(index); return search.AngleBetweenDeg(x, y, p.X, p.Y); }

        // E₀ (stripped background model reconstruction) per configuration.
        double[] E0((string Name, double Field, int Bound) env, double t, double[] rPix, int refIndex)
        {
            var e0 = new double[strip.PaddedPoints];
            if (env.Bound < 0) return e0;
            var m = model[env.Bound];
            var w = new double[pixels];
            for (int i = 0; i < pixels; i++) w[i] = env.Field * t * (m.RatePerPixel[cs][i] - rPix[i] * m.RatePerPixel[refIndex][i]);
            strip.Reconstruct(w, e0);
            return e0;
        }

        // One stripped acquisition already drawn into n1 / n2: Z_s and its location.
        (double Z, int Index) Stripped(double[] rPix, double[] e0)
        {
            for (int i = 0; i < pixels; i++) { sW[i] = n1[i] - rPix[i] * n2[i]; vW[i] = n1[i] + rPix[i] * rPix[i] * n2[i]; }
            strip.Reconstruct(sW, recon);
            strip.Variance(vW, variance);
            return strip.Max(recon, variance, e0);
        }

        int Draw(IRandom rng, double[] lambda, int[] counts)
        {
            int total = 0;
            for (int i = 0; i < pixels; i++) total += counts[i] = StrippingStatistics.Poisson(rng, lambda[i]);
            return total;
        }

        // Pixel means: Co at activity a, Cs at activity aCs, the field; windows (w1, w2) possibly gain-shifted.
        void Means(int s, double t, (string Name, double Field, int Bound) env, double aCo, double aCs, int w1, int w2)
        {
            for (int i = 0; i < pixels; i++)
            {
                double f = env.Bound < 0 ? 0 : env.Field * t;
                l1[i] = t * aCo * coMap[s].RatePerPixel[w1][i] + (aCs > 0 ? t * aCs * csMap[s].RatePerPixel[w1][i] : 0) + (env.Bound < 0 ? 0 : f * truth[env.Bound].RatePerPixel[w1][i]);
                l2[i] = t * aCo * coMap[s].RatePerPixel[w2][i] + (aCs > 0 ? t * aCs * csMap[s].RatePerPixel[w2][i] : 0) + (env.Bound < 0 ? 0 : f * truth[env.Bound].RatePerPixel[w2][i]);
            }
        }

        double? RawThreshold(double t, (string Name, double Field, int Bound) env)
        {
            if (rawThresholds is null || env.Bound < 0) return null;
            string k = AmbientGateStudy.NullKey(spec.RawGateCase, t, env.Field, q.Bounds[env.Bound], spec.CsWindow);
            return rawThresholds.PerConfiguration.TryGetValue(k, out double v) ? v : null;
        }

        for (int s = 0; s < scenes.Length; s++)
            foreach (double t in q.ExposuresS)
                foreach (var env in environments)
                    foreach (double co in coLevels)
                    {
                        if (!EnvApplies(env, co)) continue;
                        string key = Key(s, t, env.Name, co);
                        var e0 = E0(env, t, rAxisPix, productRef);
                        Means(s, t, env, co, 0, cs, productRef);
                        double? rawT = validation ? RawThreshold(t, env) : null;
                        double? zT = validation ? zThresholds!.PerConfiguration[key] : null;
                        var rng = AmbientEvidence.Stream(q.Seed, 340000u + streamIndex++);
                        var z = new double?[spec.NullRepeats];
                        int trusted = 0, trustedAtCo = 0, rawTrusted = 0, rawAtCo = 0;
                        for (int r = 0; r < spec.NullRepeats; r++)
                        {
                            Draw(rng, l1, n1); Draw(rng, l2, n2);
                            var hit = Stripped(rAxisPix, e0);
                            z[r] = double.IsFinite(hit.Z) ? Math.Round(hit.Z, AmbientGateStudy.Thresholds.RecordedDecimals) : null;
                            if (!validation) continue;
                            if (zThresholds!.Trusts(hit.Z, zT!.Value)) { trusted++; if (Within(hit.Index, coPos[s])) trustedAtCo++; }
                            if (rawT is { } rt)
                            {
                                int total = n1.Sum();
                                search.Reconstruct(n1, rawRecon);
                                var raw = search.Search(gateModels[env.Bound], total, rawRecon);
                                if (rawThresholds!.Trusts(raw.Z, rt))
                                {
                                    rawTrusted++;
                                    if (raw.Index >= 0 && search.AngleBetweenDeg(raw.X, raw.Y, coPos[s].X, coPos[s].Y) <= elemDeg) rawAtCo++;
                                }
                            }
                        }
                        if (!validation) nulls[key] = z;
                        else nullSummary[key] = new
                        {
                            Repeats = spec.NullRepeats, Threshold = zT, Trusted = trusted, TrustedAtCo = trustedAtCo,
                            RawThreshold = rawT, RawTrusted = rawT is null ? (int?)null : rawTrusted, RawTrustedAtCo = rawT is null ? (int?)null : rawAtCo,
                            ExpectedCounts662 = l1.Sum(), ExpectedCountsRef = l2.Sum()
                        };
                    }
        if (!validation)
        {
            output["Nulls"] = nulls;
            output["ComputeSeconds"] = clock.Elapsed.TotalSeconds;
            output["MapSeconds"] = mapSeconds;
            return output;
        }
        output["NullValidation"] = nullSummary;

        // ------------------------------------------------------------------ count mode (Q1) and gain (Q3, counts)
        var counts = new Dictionary<string, object>();
        foreach (var refName in refs)
        {
            int rw = W(refName);
            double rCal = axisCal.TotalRate(cs) / axisCal.TotalRate(rw);
            for (int s = 0; s < scenes.Length; s++)
            {
                double eps1 = csMap[s].TotalRate(cs), epsRef = csMap[s].TotalRate(rw), leak = epsRef / eps1;
                double dCo = coMap[s].TotalRate(cs), pCo = coMap[s].TotalRate(rw), rTrue = dCo / pCo;
                foreach (double t in q.ExposuresS)
                    foreach (var env in environments)
                        foreach (double co in coLevels)
                        {
                            if (!EnvApplies(env, co)) continue;
                            double f = env.Bound < 0 ? 0 : env.Field * t;
                            double bt1 = env.Bound < 0 ? 0 : f * truth[env.Bound].TotalRate(cs), bt2 = env.Bound < 0 ? 0 : f * truth[env.Bound].TotalRate(rw);
                            double m1 = env.Bound < 0 ? 0 : f * model[env.Bound].TotalRate(cs), m2 = env.Bound < 0 ? 0 : f * model[env.Bound].TotalRate(rw);
                            double mu1 = t * co * dCo + bt1, mu2 = t * co * pCo + bt2;
                            var normal = StrippingStatistics.CurrieNormal(mu1, mu2, rTrue, leak);
                            var exact = StrippingStatistics.CurrieExact(mu1, mu2, rTrue, leak);
                            var rng = AmbientEvidence.Stream(q.Seed, 310000u + streamIndex++);
                            var multiples = new List<object>();
                            foreach (double mult in spec.CountMultiples.Prepend(0))
                            {
                                double sCs = mult * exact.DetectionLimit;
                                var (a, p, bb) = Decide(rng, spec.CountRepeats, mu1 + sCs, mu2 + leak * sCs, rTrue, rCal, exact.Threshold, m1, m2);
                                multiples.Add(new { Multiple = mult, CsCounts = sCs, CsBq = sCs / (t * eps1), DetectedExact = a, DetectedPlugIn = p, DetectedCalibrated = bb });
                            }
                            var gains = new List<object>();
                            foreach (double g in spec.GainShifts)
                            {
                                int g1 = W(spec.CsWindow, g), g2 = W(refName, g);
                                double mu1g = t * co * coMap[s].TotalRate(g1) + (env.Bound < 0 ? 0 : f * truth[env.Bound].TotalRate(g1));
                                double mu2g = t * co * coMap[s].TotalRate(g2) + (env.Bound < 0 ? 0 : f * truth[env.Bound].TotalRate(g2));
                                double csScale1 = csMap[s].TotalRate(g1) / eps1, csScale2 = csMap[s].TotalRate(g2) / eps1;
                                var free = Decide(rng, spec.CountRepeats, mu1g, mu2g, rTrue, rCal, exact.Threshold, m1, m2);
                                double sD = exact.DetectionLimit;
                                var atLd = Decide(rng, spec.CountRepeats, mu1g + sD * csScale1, mu2g + sD * csScale2, rTrue, rCal, exact.Threshold, m1, m2);
                                gains.Add(new
                                {
                                    Gain = g, BiasCounts = (mu1g - rTrue * mu2g) - (mu1 - rTrue * mu2), BiasOverSigma0 = normal.Sigma0 > 0 ? ((mu1g - rTrue * mu2g) - (mu1 - rTrue * mu2)) / normal.Sigma0 : (double?)null,
                                    FreeExact = free.A, FreeCalibrated = free.B, AtLdExact = atLd.A, AtLdCalibrated = atLd.B
                                });
                            }
                            counts[$"{refName}|{Key(s, t, env.Name, co)}"] = new
                            {
                                Mu662 = mu1, MuRef = mu2, Model662 = m1, ModelRef = m2, RTrue = rTrue, RCalibrated = rCal, Leak = leak,
                                Sigma0 = normal.Sigma0, LcNormal = normal.CriticalLevel, LdNormal = normal.DetectionLimit,
                                Yc = exact.Threshold, LcExact = exact.CriticalLevel, ActualAlpha = exact.ActualAlpha, LdExact = exact.DetectionLimit,
                                ADBq = exact.DetectionLimit / (t * eps1), KD = co > 0 ? co / (exact.DetectionLimit / (t * eps1)) : 0,
                                WindowCountingCsCounts = t * co * dCo, WindowCountingCsBqPerCoBq = dCo / eps1,
                                CalibratedBiasCounts = (mu1 - rCal * mu2) - (m1 - rCal * m2), Repeats = spec.CountRepeats,
                                Multiples = multiples, Gains = gains
                            };
                        }
            }
        }
        output["Counts"] = counts;

        // ------------------------------------------------------------------ imaging with Cs (Q2)
        var imaging = new Dictionary<string, object>();
        for (int s = 0; s < scenes.Length; s++)
        {
            double eps1 = csMap[s].TotalRate(cs);
            foreach (double t in q.ExposuresS)
                foreach (var env in environments)
                    foreach (double co in coLevels)
                    {
                        if (!EnvApplies(env, co)) continue;
                        string key = Key(s, t, env.Name, co);
                        var e0 = E0(env, t, rAxisPix, productRef);
                        double zT = zThresholds!.PerConfiguration[key];
                        double? rawT = RawThreshold(t, env);
                        foreach (double sCs in spec.ImagingCounts)
                        {
                            double aCs = sCs / (t * eps1);
                            Means(s, t, env, co, aCs, cs, productRef);
                            var rng = AmbientEvidence.Stream(q.Seed, 320000u + streamIndex++);
                            int zTrusted = 0, zCorrect = 0, zAtCo = 0, argmaxOk = 0, rawDecoderOk = 0, rawTrusted = 0, rawCorrect = 0, rawAtCo = 0;
                            for (int r = 0; r < spec.ImagingRepeats; r++)
                            {
                                Draw(rng, l1, n1); Draw(rng, l2, n2);
                                var hit = Stripped(rAxisPix, e0);
                                if (zThresholds.Trusts(hit.Z, zT))
                                {
                                    zTrusted++;
                                    if (Within(hit.Index, csPos[s])) zCorrect++;
                                    if (Within(hit.Index, coPos[s])) zAtCo++;
                                }
                                int am = 0;
                                for (int k = 1; k < search.GridPoints; k++) if (recon[k] - e0[k] > recon[am] - e0[am]) am = k;
                                if (Within(am, csPos[s])) argmaxOk++;
                                int total = n1.Sum();
                                search.Reconstruct(n1, rawRecon);
                                var (ex, ey) = search.DecoderEstimate(rawRecon);
                                if (search.AngleBetweenDeg(ex, ey, csPos[s].X, csPos[s].Y) <= elemDeg) rawDecoderOk++;
                                if (rawT is { } rt)
                                {
                                    var raw = search.Search(gateModels[env.Bound], total, rawRecon);
                                    if (rawThresholds!.Trusts(raw.Z, rt))
                                    {
                                        rawTrusted++;
                                        if (raw.Index >= 0 && search.AngleBetweenDeg(raw.X, raw.Y, csPos[s].X, csPos[s].Y) <= elemDeg) rawCorrect++;
                                        if (raw.Index >= 0 && search.AngleBetweenDeg(raw.X, raw.Y, coPos[s].X, coPos[s].Y) <= elemDeg) rawAtCo++;
                                    }
                                }
                            }
                            imaging[$"{key}|S={Fmt(sCs)}"] = new
                            {
                                CsCounts = sCs, CsBq = aCs, K = co / aCs, Repeats = spec.ImagingRepeats, Threshold = zT,
                                ExpectedCounts662 = l1.Sum(), ExpectedCountsRef = l2.Sum(),
                                ZTrusted = zTrusted, ZCorrect = zCorrect, ZAtCo = zAtCo, StrippedArgmaxCorrect = argmaxOk,
                                RawDecoderCorrect = rawDecoderOk, RawThreshold = rawT,
                                RawTrusted = rawT is null ? (int?)null : rawTrusted, RawCorrect = rawT is null ? (int?)null : rawCorrect,
                                RawAtCo = rawT is null ? (int?)null : rawAtCo
                            };
                        }
                    }
        }
        output["Imaging"] = imaging;

        // ------------------------------------------------------------------ residual of a wrong R (Q3, image), Cs-free, ideal
        var residual = new Dictionary<string, object>();
        var ideal = environments[0];
        double rGlobalCal = axisCal.TotalRate(cs) / axisCal.TotalRate(productRef);
        for (int s = 0; s < scenes.Length; s++)
        {
            var rDirPix = PixelRatios(coCal[s], productRef);
            int coIdx = strip.Nearest(coPos[s].X, coPos[s].Y);
            var variants = new List<(string Name, double[] R, double Gain)>();
            foreach (double dr in spec.DeltaR) variants.Add(($"dR={Fmt(dr)}", rAxisPix.Select(x => x * (1 + dr)).ToArray(), 0));
            foreach (double g in spec.GainShifts) variants.Add(($"gain={Fmt(g)}", rAxisPix, g));
            variants.Add(("R=global", Enumerable.Repeat(rGlobalCal, pixels).ToArray(), 0));
            variants.Add(("Ri=direction", rDirPix, 0));
            foreach (double t in q.ExposuresS)
                foreach (double co in coLevels)
                {
                    if (co <= 0) continue;
                    string key = Key(s, t, ideal.Name, co);
                    double zT = zThresholds!.PerConfiguration[key];
                    var zero = new double[strip.PaddedPoints];
                    foreach (var v in variants)
                    {
                        int w1 = v.Gain == 0 ? cs : W(spec.CsWindow, v.Gain), w2 = v.Gain == 0 ? productRef : W(product, v.Gain);
                        Means(s, t, ideal, co, 0, w1, w2);
                        // Noiseless expected Z at Co-60's grid point.
                        double mean = 0, var0 = 0;
                        {
                            var unitRecon = new double[strip.PaddedPoints];
                            var wts = new double[pixels];
                            for (int i = 0; i < pixels; i++) wts[i] = l1[i] - v.R[i] * l2[i];
                            strip.Reconstruct(wts, unitRecon);
                            mean = unitRecon[coIdx];
                            var vv = new double[pixels];
                            for (int i = 0; i < pixels; i++) vv[i] = l1[i] + v.R[i] * v.R[i] * l2[i];
                            var varRecon = new double[strip.PaddedPoints];
                            strip.Variance(vv, varRecon);
                            var0 = varRecon[coIdx];
                        }
                        var rng = AmbientEvidence.Stream(q.Seed, 330000u + streamIndex++);
                        int trusted = 0, atCo = 0; double sumZ = 0; int finite = 0;
                        for (int r = 0; r < spec.ResidualRepeats; r++)
                        {
                            Draw(rng, l1, n1); Draw(rng, l2, n2);
                            var hit = Stripped(v.R, zero);
                            if (double.IsFinite(hit.Z)) { sumZ += hit.Z; finite++; }
                            if (zThresholds.Trusts(hit.Z, zT)) { trusted++; if (Within(hit.Index, coPos[s])) atCo++; }
                        }
                        residual[$"{key}|{v.Name}"] = new
                        {
                            Variant = v.Name, ExpectedCounts662 = l1.Sum(), ExpectedCountsRef = l2.Sum(), Threshold = zT,
                            ExpectedZAtCo = var0 > 0 ? mean / Math.Sqrt(var0) : (double?)null, ResidualAtCoCounts = mean,
                            Repeats = spec.ResidualRepeats, Trusted = trusted, TrustedAtCo = atCo, MeanMaxZ = finite > 0 ? sumZ / finite : (double?)null
                        };
                    }
                }
        }
        output["Residual"] = residual;
        output["MapSeconds"] = mapSeconds;
        output["ComputeSeconds"] = clock.Elapsed.TotalSeconds;
        return output;
    }

    /// <summary>Repeats of a two-window count acquisition; detections by (A) exact a-priori threshold on Y with the true
    /// R, (P) plug-in studentised net with the true R and the model background, (B) the same with the calibrated R.</summary>
    private static (int A, int P, int B) Decide(IRandom rng, int repeats, double lambda1, double lambda2, double rTrue, double rCal,
        double yc, double m1, double m2)
    {
        int a = 0, p = 0, b = 0;
        for (int r = 0; r < repeats; r++)
        {
            int x1 = StrippingStatistics.Poisson(rng, lambda1), x2 = StrippingStatistics.Poisson(rng, lambda2);
            if (x1 - rTrue * x2 > yc) a++;
            double vT = x1 + rTrue * rTrue * x2, vC = x1 + rCal * rCal * x2;
            if (vT > 0 && (x1 - rTrue * x2 - (m1 - rTrue * m2)) / Math.Sqrt(vT) > StrippingStatistics.Z95) p++;
            if (vC > 0 && (x1 - rCal * x2 - (m1 - rCal * m2)) / Math.Sqrt(vC) > StrippingStatistics.Z95) b++;
        }
        return (a, p, b);
    }

    private static object RatioSummary(AmbientEvidenceRequest.CsUnderCoSpec spec, CountingWindow[] win, int cs, string[] refs,
        Func<string, double, int> w, AmbientEvidenceRequest.CsCoScene[] scenes, GateMaps[] csMap, GateMaps[] coMap, GateMaps[] coCal,
        GateMaps axisCal, GateMaps[] dirMaps, int pixels, int nx, bool validation)
    {
        int ny = pixels / nx;
        int Ring(int i) { int x = i % nx, y = i / nx; return Math.Min(Math.Min(x, y), Math.Min(nx - 1 - x, ny - 1 - y)); }
        int rings = (Math.Min(nx, ny) + 1) / 2;
        double[] RingR(GateMaps m, int rw)
        {
            var num = new double[rings]; var den = new double[rings];
            for (int i = 0; i < pixels; i++) { num[Ring(i)] += m.RatePerPixel[cs][i]; den[Ring(i)] += m.RatePerPixel[rw][i]; }
            return num.Zip(den, (a, b) => b > 0 ? a / b : 0).ToArray();
        }
        double RmsVs(GateMaps m, GateMaps reference, int rw)
        {
            double s = 0; int n = 0;
            for (int i = 0; i < pixels; i++)
            {
                double pm = m.RatePerPixel[rw][i], pr = reference.RatePerPixel[rw][i];
                if (pm <= 0 || pr <= 0 || reference.RatePerPixel[cs][i] <= 0) continue;
                double rel = (m.RatePerPixel[cs][i] / pm) / (reference.RatePerPixel[cs][i] / pr) - 1;
                s += rel * rel; n++;
            }
            return n > 0 ? Math.Sqrt(s / n) : 0;
        }
        var result = new Dictionary<string, object>();
        foreach (var refName in refs)
        {
            int rw = w(refName, 0);
            double rAxis = axisCal.TotalRate(cs) / axisCal.TotalRate(rw);
            var perScene = new Dictionary<string, object>();
            for (int s = 0; s < scenes.Length; s++)
            {
                double rTrue = coMap[s].TotalRate(cs) / coMap[s].TotalRate(rw);
                var gains = spec.GainShifts.Select(g =>
                {
                    int g1 = w(spec.CsWindow, g), g2 = w(refName, g);
                    double rg = coMap[s].TotalRate(g1) / coMap[s].TotalRate(g2);
                    double? apparent = validation
                        ? (coMap[s].TotalRate(g1) - rAxis * coMap[s].TotalRate(g2)) / (csMap[s].TotalRate(g1) - rAxis * csMap[s].TotalRate(g2))
                        : null;
                    return new { Gain = g, RTrue = rg, RelativeChange = rg / rTrue - 1, ApparentCsBqPerCoBqNominalR = apparent };
                }).ToArray();
                perScene[scenes[s].Name] = new
                {
                    RTrue = rTrue, RCalibratedHere = validation ? coCal[s].TotalRate(cs) / coCal[s].TotalRate(rw) : (double?)null,
                    CsRate662PerBq = validation ? csMap[s].TotalRate(cs) : (double?)null,
                    CsRateRefPerBq = validation ? csMap[s].TotalRate(rw) : (double?)null,
                    CsLeakIntoRef = validation ? csMap[s].TotalRate(rw) / csMap[s].TotalRate(cs) : (double?)null,
                    CoRate662PerBq = coMap[s].TotalRate(cs), CoRateRefPerBq = coMap[s].TotalRate(rw),
                    CoRateRefStandardError = coMap[s].TotalRateStandardError[rw],
                    RingR = RingR(coMap[s], rw), RiRmsVsAxisCalibration = RmsVs(coMap[s], axisCal, rw),
                    Gains = gains
                };
            }
            var directions = spec.DirectionsElements.Select((el, i) => validation ? new
            {
                Elements = el, RTrue = dirMaps[i].TotalRate(cs) / dirMaps[i].TotalRate(rw), RingR = RingR(dirMaps[i], rw),
                RiRmsVsAxisCalibration = RmsVs(dirMaps[i], axisCal, rw)
            } : null).ToArray();
            result[refName] = new
            {
                RAxisCalibrated = rAxis, AxisRingR = RingR(axisCal, rw),
                AxisGains = spec.GainShifts.Select(g => new { Gain = g, R = axisCal.TotalRate(w(spec.CsWindow, g)) / axisCal.TotalRate(w(refName, g)) }).ToArray(),
                Scenes = perScene, Directions = directions
            };
        }
        return result;
    }

    private static object AmbientSummary(AmbientEvidenceRequest q, GateMaps[] truth, GateMaps[] model, CountingWindow[] win, int cs,
        string[] refs, Func<string, double, int> w, GateMaps axisCal)
    {
        var result = new Dictionary<string, object>();
        for (int b = 0; b < q.Bounds.Length; b++)
            foreach (var refName in refs)
            {
                int rw = w(refName, 0);
                double r = axisCal.TotalRate(cs) / axisCal.TotalRate(rw);
                result[$"{q.Bounds[b]}|{refName}"] = new
                {
                    Truth662CpsPerMicroSvH = truth[b].TotalRate(cs), TruthRefCpsPerMicroSvH = truth[b].TotalRate(rw),
                    Model662CpsPerMicroSvH = model[b].TotalRate(cs), ModelRefCpsPerMicroSvH = model[b].TotalRate(rw),
                    Truth662Se = truth[b].TotalRateStandardError[cs], TruthRefSe = truth[b].TotalRateStandardError[rw],
                    Model662Se = model[b].TotalRateStandardError[cs], ModelRefSe = model[b].TotalRateStandardError[rw],
                    // Stripped background: model − truth per µSv/h (the model's MC error enters the stripped net as this bias).
                    StrippedModelMinusTruthCpsPerMicroSvH = (model[b].TotalRate(cs) - r * model[b].TotalRate(rw)) - (truth[b].TotalRate(cs) - r * truth[b].TotalRate(rw)),
                    StrippedTruthCpsPerMicroSvH = truth[b].TotalRate(cs) - r * truth[b].TotalRate(rw)
                };
            }
        return result;
    }

    /// <summary>Photon H*(10) rate at distance r (µSv/h per Bq): Σ lines intensity / (4π r²) × H*(10)/Φ (ICRP 74), no air.</summary>
    public static double DosePerBq(IReadOnlyList<AmbientEvidenceRequest.SourceLine> lines, double distanceMm)
    {
        double rCm = distanceMm / 10;
        double pSvPerS = lines.Sum(l => l.Intensity / (4 * Math.PI * rCm * rCm) * AmbientDose.PerFluence(l.EnergyKeV));
        return pSvPerS * 3600 * 1e-6;
    }

    private static AmbientGateStudy.Thresholds LoadThresholds(AmbientEvidenceRequest q, AmbientGateRequest.FileReference f)
    {
        byte[] bytes = File.ReadAllBytes(AmbientEvidence.Repo(q, f.File));
        if (IncidentSpectrumFile.Sha256Hex(bytes) != f.Sha256) throw new InvalidDataException($"{f.File} does not match its pinned SHA-256.");
        return JsonSerializer.Deserialize<AmbientGateStudy.Thresholds>(bytes) ?? throw new InvalidDataException("Empty threshold file.");
    }

    private static string GainName(string name, double g) => $"{name}@g{Fmt(g)}";
    private static string Fmt(double v) => v.ToString("R", CultureInfo.InvariantCulture);
}
