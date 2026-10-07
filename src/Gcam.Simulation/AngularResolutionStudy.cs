using System.Diagnostics;
using System.Globalization;
using Gcam.Configuration;
using Gcam.Core;
using Gcam.Decoding;
using Gcam.Masks;

namespace Gcam.Simulation;

/// <summary>TODO-34 / D-41: angular resolution at use distance (the <c>angres</c> evidence family; plan
/// PLAN.Physics.AngularResolution, decisions DR-1 … DR-11). One outer seed per run.
///
/// Transported single-source maps (<see cref="GateResponse.Source"/>, the scenario's mask slab and crystal, one counting
/// window) at <see cref="AmbientEvidenceRequest.AngularResolutionSpec.SourceDetectorMm"/>. Positions are in angular
/// elements atan(cell / D). The seed draws one pair position (uniform ±jitter in x and y) and the pair axis (x for an even
/// seed, y for an odd one), so the seed ensemble averages over the sampling phase (DR-3).
///
/// Q2: per placement, separation, counts, ratio and environment, <c>Repeats</c> exact-Poisson acquisitions of the pair and
/// as many of the single-source null (one source of the same total counts at the pair's intensity centroid), each judged
/// by <see cref="ResolvedPair.Test"/> against the hypothesised pair for every valley v: non-cyclic cross-correlation
/// (median baseline) and every MLEM variant at every recorded iteration count (baseline 0). Q1 / DR-9: at the seed's
/// jittered point, the noiseless cross-correlation FWHM, half-maximum diameter, argmax and sub-cell errors, MLEM's noiseless
/// FWHM, and single-source Poisson localisation. The ladder (Q3, DR-8) is <see cref="RunLadder"/>.</summary>
public static class AngularResolutionStudy
{
    public static Dictionary<string, object?> Run(AmbientEvidenceRequest q)
    {
        var spec = q.AngularResolution ?? throw new ArgumentException("The angres family needs its AngularResolution section.");
        return spec.Ladder is not null ? RunLadder(q, spec.Ladder) : RunMain(q, spec);
    }

    // ------------------------------------------------------------------------------------------------ main (Q1, Q2)
    private static Dictionary<string, object?> RunMain(AmbientEvidenceRequest q, AmbientEvidenceRequest.AngularResolutionSpec spec)
    {
        var clock = Stopwatch.StartNew();
        var config = ConfigLoader.Load(AmbientEvidence.Repo(q, spec.Scenario));
        config.Seed = q.Seed;
        double d = config.Geometry.MaskDetectorDistanceMm;
        if (spec.SourceDetectorMm > 0) config.Geometry.SourceMaskDistanceMm = spec.SourceDetectorMm - d;
        if (!(config.Geometry.SourceMaskDistanceMm > 0)) throw new ArgumentException("Source inside the head.");
        double z = d + config.Geometry.SourceMaskDistanceMm;
        double elemDeg = ElementDeg(config);
        config.Decoder = new DecoderConfig
        {
            Cyclic = false, SubCellInterpolation = SubCellMethod.Tent,
            ReconHalfExtentMm = spec.GridHalfDeg > 0 ? z * Math.Tan(spec.GridHalfDeg * Math.PI / 180) : null
        };
        var windows = Windows(q, config, spec.Window);
        var search = new CorrelationSearch(config);
        var frame = new ResolvedPair.GridFrame(search.GridSize, search.OriginMm, search.StepMm, z);
        double gridHalfDeg = Math.Atan(-search.OriginMm / z) * 180 / Math.PI;
        int pixels = search.Pixels, w = config.Detector.PixelsX, h = config.Detector.PixelsY;
        double transmission = ClosedCellTransmission(config);
        (double X, double Y) Point(double ex, double ey) => (z * Math.Tan(ex * elemDeg * Math.PI / 180), z * Math.Tan(ey * elemDeg * Math.PI / 180));
        SimulationConfig At((double X, double Y) p) { var c = config.Clone(); c.Source.Position = [p.X, p.Y, 0]; return c; }
        double[] Map((double X, double Y) p, long photons, uint purpose)
            => GateResponse.Source(At(p), photons, GateResponse.StreamSeed(q.Seed, purpose), windows).RatePerPixel[0];

        // The seed's sampling phase (DR-3).
        var phase = AmbientEvidence.Stream(q.Seed, 400001u);
        double jx = (2 * phase.NextDouble() - 1) * spec.JitterElements, jy = (2 * phase.NextDouble() - 1) * spec.JitterElements;
        bool alongY = (q.Seed & 1) == 1;

        // MLEM variants (DR-6), on the cross-correlation grid.
        var variants = spec.Mlem.Select(v => (Spec: v, Decoder: BuildMlem(config, v, search, z, transmission, At, q.Seed, windows))).ToArray();
        double mapSeconds0 = clock.Elapsed.TotalSeconds;

        var output = new Dictionary<string, object?>
        {
            ["SchemaVersion"] = 1, ["Kind"] = "ambient-evidence", ["Family"] = "angres", ["Seed"] = q.Seed,
            ["ElementDeg"] = elemDeg, ["SourceDetectorMm"] = z, ["Window"] = windows[0].Name,
            ["Grid"] = new { search.GridSize, search.StepMm, search.OriginMm, HalfDeg = gridHalfDeg, StepDeg = Math.Atan(search.StepMm / z) * 180 / Math.PI },
            ["Phase"] = new { JitterXElements = jx, JitterYElements = jy, Axis = alongY ? "y" : "x" },
            ["ClosedCellTransmission"] = transmission, ["Valleys"] = spec.Valleys,
            ["ShadowCellPerPixel"] = config.Mask.CellPitchMm * z / config.Geometry.SourceMaskDistanceMm / config.Detector.PixelPitchMm
        };

        // ---------------------------------------------------------------- Q1 / DR-9: the single source at the seed's point
        if (spec.PointPhotons > 0)
        {
            var p0 = Point(jx, jy);
            var map = Map(p0, spec.PointPhotons, 400100u);
            double total = map.Sum();
            var unit = map.Select(v => v / total).ToArray();
            var recon = new double[search.GridPoints];
            search.ReconstructExpected(unit, recon);
            double med = ResolvedPair.Median(recon, search.GridPoints);
            var fw = ResolvedPair.Fwhm(recon, frame, med);
            int am = Argmax(recon);
            var amp = frame.Point(am);
            var (tx, ty) = search.DecoderEstimate(recon);
            double peakPerCount = recon[am], halfMaxDiameter = ResolvedPair.HalfMaxDiameterDeg(recon, frame, med);
            var mlemPoint = new Dictionary<string, object>();
            foreach (var (vs, dec) in variants)
            {
                var snaps = dec.Snapshots(unit.Select(v => v * 1e4).ToArray(), w, h, vs.Iterations);
                for (int k = 0; k < snaps.Length; k++)
                {
                    var f = ResolvedPair.Fwhm(snaps[k], frame, 0);
                    mlemPoint[$"{vs.Name}@{vs.Iterations[k]}"] = new
                    {
                        FwhmXDeg = Finite(f.X), FwhmYDeg = Finite(f.Y), HalfMaxDiameterDeg = ResolvedPair.HalfMaxDiameterDeg(snaps[k], frame, 0)
                    };
                }
            }
            // Poisson single-source localisation (the scenario decoder's tent sub-cell estimate).
            var poisson = new List<object>();
            var counts = new int[pixels];
            var lambda = new double[pixels];
            var rng = AmbientEvidence.Stream(q.Seed, 400200u);
            foreach (double c in spec.PointCounts)
            {
                for (int i = 0; i < pixels; i++) lambda[i] = c * unit[i];
                double sumSq = 0, sumDx = 0, sumDy = 0; int within = 0;
                for (int r = 0; r < spec.PointRepeats; r++)
                {
                    AmbientEvidence.Draw(rng, lambda, counts);
                    search.Reconstruct(counts, recon);
                    var (ex, ey) = search.DecoderEstimate(recon);
                    double dx = AngleAlong(z, ex, p0.X), dy = AngleAlong(z, ey, p0.Y);
                    sumSq += dx * dx + dy * dy; sumDx += dx; sumDy += dy;
                    if (ResolvedPair.AngleDeg(z, ex, ey, p0.X, p0.Y) <= elemDeg) within++;
                }
                poisson.Add(new
                {
                    Counts = c, Repeats = spec.PointRepeats, RmsErrorDeg = Math.Sqrt(sumSq / spec.PointRepeats),
                    MeanDxDeg = sumDx / spec.PointRepeats, MeanDyDeg = sumDy / spec.PointRepeats, WithinOneElement = within
                });
            }
            output["Point"] = new
            {
                PositionElements = new[] { jx, jy }, TotalRatePerBq = total,
                CcFwhmXDeg = Finite(fw.X), CcFwhmYDeg = Finite(fw.Y), CcHalfMaxDiameterDeg = halfMaxDiameter,
                CcPeakPerCount = peakPerCount, CcArgmaxErrorDeg = ResolvedPair.AngleDeg(z, amp.X, amp.Y, p0.X, p0.Y),
                CcArgmaxDxDeg = AngleAlong(z, amp.X, p0.X), CcArgmaxDyDeg = AngleAlong(z, amp.Y, p0.Y),
                CcTentErrorDeg = ResolvedPair.AngleDeg(z, tx, ty, p0.X, p0.Y), CcTentDxDeg = AngleAlong(z, tx, p0.X), CcTentDyDeg = AngleAlong(z, ty, p0.Y),
                Mlem = mlemPoint, Poisson = poisson
            };
        }

        // ---------------------------------------------------------------- environments (DR-7)
        var environments = new List<(string Name, double Field, int Bound)> { ("ideal", 0, -1) };
        GateMaps[] truth = [], model = [];
        if (q.FieldsMicroSvPerHour.Length > 0)
        {
            var spectrum = AmbientEvidence.LoadSpectrum(q);
            truth = new GateMaps[q.Bounds.Length]; model = new GateMaps[q.Bounds.Length];
            for (int b = 0; b < q.Bounds.Length; b++)
            {
                truth[b] = GateResponse.Ambient(config, q.Bounds[b], spectrum, q.AmbientHistories, GateResponse.StreamSeed(q.Seed, 400800u + 4u * (uint)b), windows);
                model[b] = GateResponse.Ambient(config, q.Bounds[b], spectrum, q.CalibrationHistories, GateResponse.StreamSeed(q.Seed, 400801u + 4u * (uint)b), windows);
            }
            foreach (double f in q.FieldsMicroSvPerHour)
                for (int b = 0; b < q.Bounds.Length; b++) environments.Add(($"F={Fmt(f)}|{q.Bounds[b]}", f, b));
            output["Ambient"] = q.Bounds.Select((bd, b) => new
            {
                Bound = bd.ToString(), TruthCpsPerMicroSvH = truth[b].TotalRate(0), ModelCpsPerMicroSvH = model[b].TotalRate(0),
                CountsInExposure = q.FieldsMicroSvPerHour.Select(f => f * spec.ExposureS * truth[b].TotalRate(0)).ToArray()
            }).ToArray();
        }

        // ---------------------------------------------------------------- Q2: pairs and nulls
        var conditions = new Dictionary<string, object>();
        var valleys = spec.Valleys;
        int nv = valleys.Length;
        uint mapIndex = 0, streamIndex = 0;
        var cnt = new int[pixels];
        var y = new double[pixels];
        var reconPair = new double[search.GridPoints];
        var lam = new double[pixels];
        var lam0 = new double[pixels];
        var res = new bool[nv];
        var prom = new double[nv];
        for (int pl = 0; pl < spec.Placements.Length; pl++)
        {
            string placement = spec.Placements[pl];
            foreach (double delta in spec.SeparationsElements)
            {
                // Sources in elements: s1 the weaker (axis: the lower side; edge: the inner one), s2 the stronger.
                double a1, a2, perp;
                if (placement == "axis") { double c = alongY ? jy : jx; a1 = c - delta / 2; a2 = c + delta / 2; perp = alongY ? jx : jy; }
                else if (placement == "edge") { a2 = spec.EdgeOuterElements + (alongY ? jy : jx); a1 = a2 - delta; perp = alongY ? jx : jy; }
                else throw new ArgumentException($"Unknown placement '{placement}'.");
                (double X, double Y) E(double along) => alongY ? Point(perp, along) : Point(along, perp);
                var s1 = E(a1); var s2 = E(a2);
                foreach (var s in new[] { s1, s2 })
                    if (Math.Abs(s.X) > -search.OriginMm || Math.Abs(s.Y) > -search.OriginMm) throw new ArgumentException("A source lies outside the search grid.");
                var m1 = Map(s1, spec.MapPhotons, 401000u + mapIndex++);
                var m2 = Map(s2, spec.MapPhotons, 401000u + mapIndex++);
                double t1 = m1.Sum(), t2 = m2.Sum();
                double radius = Math.Min(delta / 2, 1.0) * elemDeg;
                foreach (double ratio in spec.Ratios)
                {
                    double fw2 = 1 / (1 + ratio);                       // the stronger source's share of the counts
                    var m0 = Map(E(a1 + fw2 * (a2 - a1)), spec.MapPhotons, 401000u + mapIndex++);
                    double t0 = m0.Sum();
                    foreach (double c1 in spec.CountsPerSource)
                    {
                        double c2 = c1 / ratio;
                        var active = variants.Where(v => Applies(v.Spec, placement, delta, c1, ratio)).ToArray();
                        foreach (var env in environments)
                        {
                            double ft = env.Bound < 0 ? 0 : env.Field * spec.ExposureS;
                            double[]? bTruth = env.Bound < 0 ? null : truth[env.Bound].RatePerPixel[0];
                            double[]? bModel = env.Bound < 0 ? null : model[env.Bound].RatePerPixel[0].Select(v => v * ft).ToArray();
                            for (int i = 0; i < pixels; i++)
                            {
                                double bg = bTruth is null ? 0 : ft * bTruth[i];
                                lam[i] = c1 * m1[i] / t1 + c2 * m2[i] / t2 + bg;
                                lam0[i] = (c1 + c2) * m0[i] / t0 + bg;
                            }
                            // Decoders: CC, then each active MLEM variant (and its +b copy under a field) at each snapshot.
                            var names = new List<string> { "cc" };
                            foreach (var (vs, _) in active)
                            {
                                foreach (int it in vs.Iterations) names.Add($"{vs.Name}@{it}");
                                if (vs.BackgroundTerm && bModel is not null) foreach (int it in vs.Iterations) names.Add($"{vs.Name}+b@{it}");
                            }
                            var pass = new int[2, names.Count, nv];
                            // Turn 3: per acquisition, the second peak's absolute prominence, negated when it is not assigned
                            // to the other true source (0 = no second peak) — enough to apply any significance floor later.
                            var stat = spec.RecordStatistics ? new float[2, names.Count, nv, spec.Repeats] : null;
                            var rng = AmbientEvidence.Stream(q.Seed, 420000u + streamIndex++);
                            for (int kind = 0; kind < 2; kind++)
                                for (int r = 0; r < spec.Repeats; r++)
                                {
                                    AmbientEvidence.Draw(rng, kind == 0 ? lam : lam0, cnt);
                                    int col = 0;
                                    search.Reconstruct(cnt, reconPair);
                                    ResolvedPair.Test(reconPair, frame, true, s1, s2, radius, valleys, res, prom);
                                    for (int v = 0; v < nv; v++) { if (res[v]) pass[kind, col, v]++; if (stat is not null) stat[kind, col, v, r] = Stat(res[v], prom[v]); }
                                    col++;
                                    for (int i = 0; i < pixels; i++) y[i] = cnt[i];
                                    foreach (var (vs, dec) in active)
                                    {
                                        foreach (var bg in (vs.BackgroundTerm && bModel is not null) ? new[] { (double[]?)null, bModel } : [null])
                                        {
                                            var snaps = dec.Snapshots(y, w, h, vs.Iterations, bg);
                                            foreach (var lamda in snaps)
                                            {
                                                ResolvedPair.Test(lamda, frame, false, s1, s2, radius, valleys, res, prom);
                                                for (int v = 0; v < nv; v++) { if (res[v]) pass[kind, col, v]++; if (stat is not null) stat[kind, col, v, r] = Stat(res[v], prom[v]); }
                                                col++;
                                            }
                                        }
                                    }
                                }
                            var decoders = new Dictionary<string, object>();
                            for (int k = 0; k < names.Count; k++)
                                decoders[names[k]] = new
                                {
                                    Pass = Enumerable.Range(0, nv).Select(v => pass[0, k, v]).ToArray(),
                                    Null = Enumerable.Range(0, nv).Select(v => pass[1, k, v]).ToArray(),
                                    PairStat = stat is null ? null : Enumerable.Range(0, nv).Select(v => Enumerable.Range(0, spec.Repeats).Select(r => stat[0, k, v, r]).ToArray()).ToArray(),
                                    NullStat = stat is null ? null : Enumerable.Range(0, nv).Select(v => Enumerable.Range(0, spec.Repeats).Select(r => stat[1, k, v, r]).ToArray()).ToArray()
                                };
                            conditions[Key(placement, delta, c1, ratio, env.Name)] = new
                            {
                                Placement = placement, SeparationElements = delta, CountsPerSource = c1, Ratio = ratio, Environment = env.Name,
                                Repeats = spec.Repeats, BackgroundCounts = bTruth is null ? 0 : ft * bTruth.Sum(), Decoders = decoders
                            };
                        }
                    }
                }
            }
        }
        output["Conditions"] = conditions;
        output["SetupSeconds"] = mapSeconds0;
        output["ComputeSeconds"] = clock.Elapsed.TotalSeconds;
        return output;
    }

    /// <summary>Recorded statistic: the absolute prominence as a float, negative when the pair test was not passed
    /// (unassigned or no second peak; the shape result is the sign, so stat &gt; 0 ⇔ shape-resolved).</summary>
    private static float Stat(bool resolved, double prominence) => resolved ? (float)prominence : -(float)prominence;

    private static bool Applies(AmbientEvidenceRequest.AngularMlemVariant v, string placement, double delta, double counts, double ratio)
        => (v.Counts.Length == 0 || v.Counts.Contains(counts)) && (v.Ratios.Length == 0 || v.Ratios.Contains(ratio))
           && (v.Placements.Length == 0 || v.Placements.Contains(placement)) && delta >= v.MinSeparationElements - 1e-12;

    /// <summary>The MLEM variant's decoder on the cross-correlation search grid (same half-width, step and source plane).</summary>
    private static MlemDecoder BuildMlem(SimulationConfig config, AmbientEvidenceRequest.AngularMlemVariant v, CorrelationSearch search,
        double z, double transmission, Func<(double X, double Y), SimulationConfig> at, int seed, CountingWindow[] windows)
    {
        if (v.Iterations.Length == 0) throw new ArgumentException($"MLEM variant {v.Name} records no iteration count.");
        var geo = Geometry(config, search, z);
        int maxIt = v.Iterations.Max();
        switch (v.Kind)
        {
            // The engine's binary pixel-centre matrix, run through the vectorised loop (one sample per pixel, opaque closed
            // cells: the same matrix; equal to the default loop to rounding, MlemOptionTests).
            case "binary": return new MlemDecoder(MosaicOf(config), geo, maxIt, new MlemSystemModel(1, 0));
            case "area": return new MlemDecoder(MosaicOf(config), geo, maxIt, new MlemSystemModel(v.PixelSubSamples, transmission));
            case "matched":
                {
                    if (v.ColumnPhotons < 1) throw new ArgumentException("A matched variant needs ColumnPhotons.");
                    int n = search.GridSize;
                    var columns = new double[n * n][];
                    Parallel.For(0, n * n, j =>
                    {
                        var p = (search.OriginMm + j % n * search.StepMm, search.OriginMm + j / n * search.StepMm);
                        columns[j] = GateResponse.Source(at(p), v.ColumnPhotons, GateResponse.StreamSeed(seed, 500000u + (uint)j), windows).RatePerPixel[0];
                    });
                    return MlemDecoder.FromColumns(geo, config.Detector.PixelsX, config.Detector.PixelsY, columns, maxIt);
                }
            default: throw new ArgumentException($"Unknown MLEM kind '{v.Kind}'.");
        }
    }

    // ------------------------------------------------------------------------------------------------ ladder (Q3)
    /// <summary>DR-8: EV-11's recipe (<see cref="MlemStudy"/> unchanged, truth-centred valley, cyclic, self-consistent
    /// floods), then one change per step, each with the blind test and its single-source null over the seed's jittered
    /// pair position: L1 the blind test on self-consistent floods; L2 transported floods (every deposit); L3 non-cyclic
    /// decoding (L2's maps); L4 the hand-held head at its own distance; L5 the hand-held head at HeadSourceDetectorMm.</summary>
    public static Dictionary<string, object?> RunLadder(AmbientEvidenceRequest q, AmbientEvidenceRequest.AngularLadderSpec spec)
    {
        var clock = Stopwatch.StartNew();
        var output = new Dictionary<string, object?>
        {
            ["SchemaVersion"] = 1, ["Kind"] = "ambient-evidence", ["Family"] = "angres-ladder", ["Seed"] = q.Seed, ["Valleys"] = spec.Valleys
        };
        var lab = ConfigLoader.Load(AmbientEvidence.Repo(q, spec.LabScenario));
        lab.Seed = q.Seed;
        double labZ = lab.Geometry.MaskDetectorDistanceMm + lab.Geometry.SourceMaskDistanceMm, labElem = ElementDeg(lab);

        // L0: EV-11 as is.
        var (rows, _, _, _, _, _) = new MlemStudy().Run(lab, spec.Ev11SeparationsMm, spec.Ev11Photons, spec.Ev11Iterations, q.Seed);
        output["L0"] = rows.Select(r => new
        {
            r.SeparationMm, SeparationElements = ResolvedPair.AngleDeg(labZ, -r.SeparationMm / 2, 0, r.SeparationMm / 2, 0) / labElem,
            r.CrossValleyDepth, r.MlemValleyDepth, r.CrossResolved, r.MlemResolved
        }).ToArray();

        // EV-11's count level: MlemStudy aims Ev11Photons at the detector rectangle; the landed share of a centred source.
        double landed = SelfConsistentFlood(lab, (0, 0), 2_000_000, AmbientEvidence.Stream(q.Seed, 430000u), out _).Hits;
        double ev11PerSource = 0.5 * spec.Ev11Photons * landed;
        output["Ev11CountsPerSource"] = ev11PerSource;
        var levels = spec.CountsPerSource.Select(c => c > 0 ? c : ev11PerSource).ToArray();

        var phase = AmbientEvidence.Stream(q.Seed, 400001u);
        double jx = (2 * phase.NextDouble() - 1) * spec.JitterElements, jy = (2 * phase.NextDouble() - 1) * spec.JitterElements;
        bool alongY = (q.Seed & 1) == 1;
        output["Phase"] = new { JitterXElements = jx, JitterYElements = jy, Axis = alongY ? "y" : "x" };

        var head = ConfigLoader.Load(AmbientEvidence.Repo(q, spec.HeadScenario));
        head.Seed = q.Seed;
        var headFar = head.Clone();
        headFar.Geometry.SourceMaskDistanceMm = spec.HeadSourceDetectorMm - head.Geometry.MaskDetectorDistanceMm;
        // L2 and L3 share their maps (group "lab-transported"), so L3 changes only the decoding.
        var steps = new (string Name, SimulationConfig Config, bool Transported, bool Cyclic, string Group)[]
        {
            ("L1-blind", lab, false, true, "lab-flood"), ("L2-transported", lab, true, true, "lab-transported"),
            ("L3-noncyclic", lab, true, false, "lab-transported"), ("L4-head-own-distance", head, true, false, "head-own"),
            ("L5-head-far", headFar, true, false, "head-far")
        };
        var mapCache = new Dictionary<string, double[]>();
        var stepOut = new Dictionary<string, object>();
        uint streamIndex = 0, mapIndex = 0;
        foreach (var step in steps)
        {
            var config = step.Config.Clone();
            config.Seed = q.Seed;
            config.Decoder = new DecoderConfig { Cyclic = step.Cyclic, SubCellInterpolation = SubCellMethod.None };
            double z = config.Geometry.MaskDetectorDistanceMm + config.Geometry.SourceMaskDistanceMm, elemDeg = ElementDeg(config);
            var search = new CorrelationSearch(config);
            var frame = new ResolvedPair.GridFrame(search.GridSize, search.OriginMm, search.StepMm, z);
            var geo = Geometry(config, search, z);
            var binary = new MlemDecoder(MosaicOf(config), geo, spec.BinaryIterations, new MlemSystemModel(1, 0));
            var area = new MlemDecoder(MosaicOf(config), geo, spec.AreaIterations, new MlemSystemModel(spec.AreaPixelSubSamples, ClosedCellTransmission(config)));
            int pixels = search.Pixels, w = config.Detector.PixelsX, h = config.Detector.PixelsY;
            (double X, double Y) Point(double ex, double ey) => (z * Math.Tan(ex * elemDeg * Math.PI / 180), z * Math.Tan(ey * elemDeg * Math.PI / 180));
            double[] Map((double X, double Y) p)
            {
                string key = FormattableString.Invariant($"{step.Group}|{p.X:R}|{p.Y:R}");
                if (mapCache.TryGetValue(key, out var cached)) return cached;
                var made = MakeMap(p);
                mapCache[key] = made;
                return made;
            }
            double[] MakeMap((double X, double Y) p)
            {
                uint purpose = 431000u + mapIndex++;
                if (!step.Transported) return SelfConsistentFlood(config, p, spec.MapPhotons, AmbientEvidence.Stream(q.Seed, purpose), out _).Map;
                var c = config.Clone(); c.Source.Position = [p.X, p.Y, 0];
                return GateResponse.Source(c, spec.MapPhotons, GateResponse.StreamSeed(q.Seed, purpose), [CountingWindow.Open()]).RatePerPixel[0];
            }
            var rows2 = new List<object>();
            var cnt = new int[pixels]; var yv = new double[pixels]; var recon = new double[search.GridPoints];
            var lam = new double[pixels]; var lam0 = new double[pixels]; var res = new bool[spec.Valleys.Length];
            foreach (double delta in spec.SeparationsElements)
            {
                double c0 = alongY ? jy : jx, perp = alongY ? jx : jy;
                (double X, double Y) E(double along) => alongY ? Point(perp, along) : Point(along, perp);
                var s1 = E(c0 - delta / 2); var s2 = E(c0 + delta / 2);
                var m1 = Map(s1); var m2 = Map(s2); var m0 = Map(E(c0));
                double t1 = m1.Sum(), t2 = m2.Sum(), t0 = m0.Sum();
                double radius = Math.Min(delta / 2, 1.0) * elemDeg;
                foreach (double c in levels)
                {
                    for (int i = 0; i < pixels; i++) { lam[i] = c * (m1[i] / t1 + m2[i] / t2); lam0[i] = 2 * c * m0[i] / t0; }
                    var pass = new int[2, 3, spec.Valleys.Length];
                    var rng = AmbientEvidence.Stream(q.Seed, 440000u + streamIndex++);
                    for (int kind = 0; kind < 2; kind++)
                        for (int r = 0; r < spec.Repeats; r++)
                        {
                            AmbientEvidence.Draw(rng, kind == 0 ? lam : lam0, cnt);
                            search.Reconstruct(cnt, recon);
                            ResolvedPair.Test(recon, frame, true, s1, s2, radius, spec.Valleys, res);
                            for (int v = 0; v < res.Length; v++) if (res[v]) pass[kind, 0, v]++;
                            for (int i = 0; i < pixels; i++) yv[i] = cnt[i];
                            int col = 1;
                            foreach (var dec in new[] { binary, area })
                            {
                                var l = dec.Snapshots(yv, w, h, [dec == binary ? spec.BinaryIterations : spec.AreaIterations])[0];
                                ResolvedPair.Test(l, frame, false, s1, s2, radius, spec.Valleys, res);
                                for (int v = 0; v < res.Length; v++) if (res[v]) pass[kind, col, v]++;
                                col++;
                            }
                        }
                    string[] names = ["cc", $"binary@{spec.BinaryIterations}", $"area@{spec.AreaIterations}"];
                    rows2.Add(new
                    {
                        SeparationElements = delta, CountsPerSource = c, Repeats = spec.Repeats,
                        Decoders = names.Select((nm, k) => new
                        {
                            Name = nm, Pass = Enumerable.Range(0, res.Length).Select(v => pass[0, k, v]).ToArray(),
                            Null = Enumerable.Range(0, res.Length).Select(v => pass[1, k, v]).ToArray()
                        }).ToArray()
                    });
                }
            }
            stepOut[step.Name] = new
            {
                SourceDetectorMm = z, ElementDeg = elemDeg, step.Transported, step.Cyclic,
                ShadowCellPerPixel = config.Mask.CellPitchMm * z / config.Geometry.SourceMaskDistanceMm / config.Detector.PixelPitchMm,
                Rows = rows2
            };
        }
        output["Steps"] = stepOut;
        output["CountLevels"] = levels;
        output["ComputeSeconds"] = clock.Elapsed.TotalSeconds;
        return output;
    }

    /// <summary>MlemStudy's flood model for one source at (x, y) on the source plane: photons aimed uniformly over the detector
    /// rectangle, weight A·cosθ/(4π r²), thin binary mask (open cells of the physical mosaic pass, everything else stops),
    /// scored at the aimed pixel. Returns the expected weight per pixel per emitted photon and the landed share of the aimed
    /// photons.</summary>
    public static (double[] Map, double Hits) SelfConsistentFlood(SimulationConfig config, (double X, double Y) source, long photons, IRandom rng, out long landed)
    {
        var m = config.Mask;
        var pattern = MosaicOf(config);
        double pitch = m.CellPitchMm, maskHalfW = pattern.Width * pitch / 2.0, maskHalfH = pattern.Height * pitch / 2.0;
        double maskZ = config.Geometry.MaskDetectorDistanceMm, sourceZ = maskZ + config.Geometry.SourceMaskDistanceMm;
        int w = config.Detector.PixelsX, h = config.Detector.PixelsY;
        double detPitch = config.Detector.PixelPitchMm, detHalfW = w * detPitch / 2.0, detHalfH = h * detPitch / 2.0;
        double norm = (2.0 * detHalfW) * (2.0 * detHalfH) / (4.0 * Math.PI);
        var map = new double[w * h];
        landed = 0;
        for (long k = 0; k < photons; k++)
        {
            double tx = (rng.NextDouble() * 2.0 - 1.0) * detHalfW, ty = (rng.NextDouble() * 2.0 - 1.0) * detHalfH;
            double dx = tx - source.X, dy = ty - source.Y, dz = -sourceZ;
            double r = Math.Sqrt(dx * dx + dy * dy + dz * dz);
            double weight = norm * Math.Abs(dz / r) / (r * r);
            double s = (maskZ - sourceZ) / dz;
            double u = source.X + dx * s + maskHalfW, v = source.Y + dy * s + maskHalfH;
            if (u < 0.0 || v < 0.0 || u >= 2.0 * maskHalfW || v >= 2.0 * maskHalfH) continue;
            int cx = (int)(u / pitch), cy = (int)(v / pitch);
            if (cx >= pattern.Width || cy >= pattern.Height || !pattern[cx, cy]) continue;
            int ix = (int)((tx + detHalfW) / detPitch), iy = (int)((ty + detHalfH) / detPitch);
            if (ix < 0 || iy < 0 || ix >= w || iy >= h) continue;
            map[iy * w + ix] += weight / photons;
            landed++;
        }
        return (map, landed / (double)photons);
    }

    // ------------------------------------------------------------------------------------------------ helpers
    public static double ElementDeg(SimulationConfig config) => Math.Atan(config.Mask.CellPitchMm / config.Geometry.MaskDetectorDistanceMm) * 180 / Math.PI;

    /// <summary>exp(−μ·t) of the mask slab at the source line (the slab's 662 keV μ scaled by tungsten's μ(E) / μ(662)).</summary>
    public static double ClosedCellTransmission(SimulationConfig config)
        => Math.Exp(-config.Mask.LinearAttenuationPerMm * CodedApertureMask.TungstenMuRel(config.Source.EnergyKeV) * config.Mask.ThicknessMm);

    private static MaskPattern MosaicOf(SimulationConfig config) => MuraGenerator.Mosaic(config.Mask.Rank, config.Mask.MosaicX, config.Mask.MosaicY);

    private static CodedApertureGeometry Geometry(SimulationConfig config, CorrelationSearch search, double z)
    {
        var pattern = MosaicOf(config);
        return new CodedApertureGeometry(config.Mask.Rank, config.Geometry.MaskDetectorDistanceMm, config.Mask.CellPitchMm, pattern.Width, pattern.Height,
            0.0, config.Detector.PixelPitchMm, z, -search.OriginMm, search.StepMm, config.Decoder.Cyclic);
    }

    private static CountingWindow[] Windows(AmbientEvidenceRequest q, SimulationConfig config, string name)
    {
        return string.IsNullOrEmpty(name) ? [CountingWindow.Open()]
            : [AmbientEvidence.Windows(q, config).FirstOrDefault(x => x.Name == name) ?? throw new ArgumentException($"Window {name} not declared.")];
    }

    private static int Argmax(double[] a) { int b = 0; for (int k = 1; k < a.Length; k++) if (a[k] > a[b]) b = k; return b; }

    /// <summary>Signed angle (degrees) of coordinate e against truth t along one axis, seen from the detector at distance z.</summary>
    private static double AngleAlong(double z, double e, double t) => (Math.Atan(e / z) - Math.Atan(t / z)) * 180 / Math.PI;

    private static double? Finite(double v) => double.IsFinite(v) ? v : null;

    private static string Key(string placement, double delta, double counts, double ratio, string env)
        => $"{placement}|D={Fmt(delta)}|C={Fmt(counts)}|R={Fmt(ratio)}|{env}";

    private static string Fmt(double v) => v.ToString("R", CultureInfo.InvariantCulture);
}
