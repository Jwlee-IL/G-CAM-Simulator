using System.Diagnostics;
using Gcam.Configuration;

namespace Gcam.Simulation;

/// <summary>EV-02 under the absolute ambient field (AB-13, TODO-30 turn 8): the legacy <see cref="FieldOfViewStudy"/>
/// recipe — hand-held head, source at S = 1 m / 5 m moved 0–20° off axis along x and the diagonal, a source of fixed
/// strength giving N0 counts on axis, cyclic decoding on one period and wide non-cyclic decoding (±20°, 0.3° step), the
/// flood-centroid side cue and "outside the field" flag — with the uniform BSR pedestal replaced by the transported field
/// (field × live time × ambient map, per bound).
///
/// Differences from the legacy study, all declared: (1) source and field go through the ambient bound's homogeneous
/// crystal (<see cref="GateResponse"/>), so N0 counts in a counting window; (2) a live time is needed to turn N0 into an
/// activity (activity × t = N0 / on-axis rate), so each field level is run at each declared live time; (3) the centroid
/// flag's threshold (95th percentile of the centroid offset over the in-field angles) is set on <b>separate</b> draws of
/// the same series (<see cref="AmbientEvidenceRequest.FieldOfViewSpec.CalibrationRepeats"/> per in-field angle), not on
/// the evaluated draws as the legacy study did — it is re-calibrated under each field condition.</summary>
public static class AmbientFieldOfViewStudy
{
    public static Dictionary<string, object?> Run(AmbientEvidenceRequest q)
    {
        var clock = Stopwatch.StartNew();
        var spec = q.FieldOfView ?? throw new ArgumentException("The fov family needs its FieldOfView section.");
        if (spec.AnglesDeg.Length == 0 || spec.AnglesDeg[0] != 0 || spec.AnglesDeg.Any(a => !(a >= 0)))
            throw new ArgumentException("Angles must start at 0 (the on-axis normalisation) and be non-negative.");
        var spectrum = AmbientEvidence.LoadSpectrum(q);
        var head = ConfigLoader.Load(AmbientEvidence.Repo(q, spec.Scenario));
        head.Seed = q.Seed;
        var windows = AmbientEvidence.Windows(q, head);
        var truth = new GateMaps[q.Bounds.Length];
        for (int b = 0; b < q.Bounds.Length; b++)
            truth[b] = GateResponse.Ambient(head, q.Bounds[b], spectrum, q.AmbientHistories, GateResponse.StreamSeed(q.Seed, 90000u + (uint)b), windows);

        double d = head.Geometry.MaskDetectorDistanceMm;
        double resolutionDeg = Math.Atan(head.Mask.CellPitchMm / d) * 180 / Math.PI;
        double tanFc = head.Mask.Rank * head.Mask.CellPitchMm / 2.0 / d;
        int width = head.Detector.PixelsX, height = head.Detector.PixelsY, pixels = width * height;
        double pitch = head.Detector.PixelPitchMm, halfW = width * pitch / 2, halfH = height * pitch / 2;
        var rows = new List<object>();
        var rates = new Dictionary<string, object>();
        var timings = new Dictionary<string, double>();
        uint mapIndex = 0, streamIndex = 0;
        var counts = new int[pixels];
        var lambda = new double[pixels];

        foreach (double s in spec.SourceMaskDistancesMm)
        {
            var watch = Stopwatch.StartNew();
            double planeZ = d + s;
            bool InField(double x, double y) => Math.Max(Math.Abs(x), Math.Abs(y)) / planeZ <= tanFc + 1e-12;
            SimulationConfig At(bool cyclic)
            {
                var c = head.Clone();
                c.Geometry.SourceMaskDistanceMm = s;
                c.Background = null; c.Sources = null; c.Source.DirectionalBiasing = true;
                c.Decoder.Cyclic = cyclic;
                c.Decoder.ReconHalfExtentMm = cyclic ? null : planeZ * Math.Tan(spec.GridHalfAngleDeg * Math.PI / 180);
                c.Decoder.ReconStepMm = cyclic ? null : planeZ * Math.Tan(spec.GridStepDeg * Math.PI / 180);
                return c;
            }
            var wide = new CorrelationSearch(At(cyclic: false));
            var cyclicSearch = new CorrelationSearch(At(cyclic: true));
            var wideRecon = new double[wide.GridPoints];
            var cyclicRecon = new double[cyclicSearch.GridPoints];
            timings[$"decoders|S={s}"] = watch.Elapsed.TotalSeconds;

            foreach (double direction in spec.DirectionsDeg)
            {
                watch.Restart();
                double ux = Math.Cos(direction * Math.PI / 180), uy = Math.Sin(direction * Math.PI / 180);
                var positions = spec.AnglesDeg.Select(a => planeZ * Math.Tan(a * Math.PI / 180)).Select(r => (X: r * ux, Y: r * uy)).ToArray();
                var maps = new GateMaps[positions.Length];
                for (int i = 0; i < positions.Length; i++)
                {
                    var c = At(cyclic: false);
                    c.Source.Position = [positions[i].X, positions[i].Y, 0];
                    maps[i] = GateResponse.Source(c, q.SourcePhotons, GateResponse.StreamSeed(q.Seed, 91000u + mapIndex++), windows);
                }
                timings[$"maps|S={s}|dir={direction}"] = watch.Elapsed.TotalSeconds;
                watch.Restart();
                for (int w = 0; w < windows.Length; w++)
                {
                    double onAxis = maps[0].TotalRate(w);
                    if (!(onAxis > 0)) throw new InvalidOperationException("No on-axis rate in the window: cannot normalise N0.");
                    rates[$"S={s}|dir={direction}|{windows[w].Name}"] = new
                    {
                        OnAxisCpsPerBq = onAxis, RelativeRate = maps.Select(m => m.TotalRate(w) / onAxis).ToArray()
                    };
                    var environments = new List<(string Name, double Field, int Bound, double T)> { ("ideal", 0, -1, 0) };
                    foreach (double t in q.ExposuresS)
                        foreach (double field in q.FieldsMicroSvPerHour)
                            for (int b = 0; b < q.Bounds.Length; b++) environments.Add(($"t={t}|F={field}|{q.Bounds[b]}", field, b, t));
                    foreach (double n0 in spec.OnAxisCounts)
                        foreach (var env in environments)
                        {
                            double sourceScale = n0 / onAxis;                 // activity × live time (Bq·s)
                            double[]? bg = env.Bound < 0 ? null : truth[env.Bound].RatePerPixel[w];
                            double bgScale = env.Field * env.T;
                            Draws Realise(int i, int repeats)
                            {
                                AmbientEvidence.Mean(lambda, sourceScale, maps[i].RatePerPixel[w], bgScale, bg);
                                var rng = AmbientEvidence.Stream(q.Seed, 92000u + streamIndex++);
                                var result = new Draws(repeats);
                                for (int r = 0; r < repeats; r++)
                                {
                                    AmbientEvidence.Draw(rng, lambda, counts);
                                    wide.Reconstruct(counts, wideRecon);
                                    var (ex, ey) = wide.DecoderEstimate(wideRecon);
                                    result.ErrorDeg[r] = wide.AngleBetweenDeg(ex, ey, positions[i].X, positions[i].Y);
                                    result.PeakOutside[r] = !InField(ex, ey);
                                    result.PeakSide[r] = Math.Sign(ex * ux + ey * uy);
                                    cyclicSearch.Reconstruct(counts, cyclicRecon);
                                    var (cx, cy) = cyclicSearch.DecoderEstimate(cyclicRecon);
                                    result.CyclicErrorDeg[r] = cyclicSearch.AngleBetweenDeg(cx, cy, positions[i].X, positions[i].Y);
                                    // Flood centroid (mm from the array centre); the lit area moves AWAY from the source.
                                    double sum = 0, sx = 0, sy = 0;
                                    for (int k = 0; k < pixels; k++)
                                    {
                                        int v = counts[k];
                                        if (v == 0) continue;
                                        sum += v; sx += v * ((k % width + 0.5) * pitch - halfW); sy += v * ((k / width + 0.5) * pitch - halfH);
                                    }
                                    double mx = sum > 0 ? sx / sum : 0, my = sum > 0 ? sy / sum : 0;
                                    result.CentroidSide[r] = -Math.Sign(mx * ux + my * uy);
                                    result.CentroidOffsetMm[r] = Math.Sqrt(mx * mx + my * my);
                                }
                                return result;
                            }

                            // Flag threshold from separate draws of the in-field angles of this series.
                            var calibration = new List<double>();
                            for (int i = 0; i < positions.Length; i++)
                                if (InField(positions[i].X, positions[i].Y))
                                    calibration.AddRange(Realise(i, spec.CalibrationRepeats).CentroidOffsetMm);
                            double threshold = Percentile(calibration, 0.95);
                            double expectedBackground = bg is null ? 0 : bgScale * bg.Sum();
                            for (int i = 0; i < positions.Length; i++)
                            {
                                var p = Realise(i, q.Repeats);
                                bool hasSide = spec.AnglesDeg[i] > 0;
                                int n = q.Repeats;
                                double Frac(Func<int, bool> pass) { int k = 0; for (int r = 0; r < n; r++) if (pass(r)) k++; return (double)k / n; }
                                rows.Add(new
                                {
                                    S = s, Direction = direction, Window = windows[w].Name, N0 = n0, Env = env.Name,
                                    ExposureS = env.Bound < 0 ? (double?)null : env.T, Field = env.Field,
                                    Bound = env.Bound < 0 ? null : q.Bounds[env.Bound].ToString(),
                                    Angle = spec.AnglesDeg[i], ExpectedSourceCounts = sourceScale * maps[i].TotalRate(w),
                                    ExpectedBackgroundCounts = expectedBackground, FlagThresholdMm = threshold,
                                    LocNonCyclic = Frac(r => p.ErrorDeg[r] <= resolutionDeg),
                                    LocCyclic = Frac(r => p.CyclicErrorDeg[r] <= resolutionDeg),
                                    MedianErrorDeg = Percentile(p.ErrorDeg, 0.5),
                                    SideCentroid = hasSide ? Frac(r => p.CentroidSide[r] > 0) : (double?)null,
                                    SidePeak = hasSide ? Frac(r => p.PeakSide[r] > 0) : (double?)null,
                                    OutsidePeak = Frac(r => p.PeakOutside[r]),
                                    OutsideCentroid = double.IsNaN(threshold) ? (double?)null : Frac(r => p.CentroidOffsetMm[r] > threshold),
                                    FalseInField = Frac(r => !p.PeakOutside[r] && p.ErrorDeg[r] > resolutionDeg),
                                    FalseInFieldUnflagged = double.IsNaN(threshold) ? (double?)null
                                        : Frac(r => !p.PeakOutside[r] && p.ErrorDeg[r] > resolutionDeg && !(p.CentroidOffsetMm[r] > threshold))
                                });
                            }
                        }
                }
                timings[$"draws|S={s}|dir={direction}"] = watch.Elapsed.TotalSeconds;
            }
        }
        var ambientRates = new Dictionary<string, object>();
        for (int b = 0; b < q.Bounds.Length; b++)
            for (int w = 0; w < windows.Length; w++)
                ambientRates[$"{q.Bounds[b]}|{windows[w].Name}"] = new { CpsPerMicroSvH = truth[b].TotalRate(w), StandardError = truth[b].TotalRateStandardError[w] };
        return new Dictionary<string, object?>
        {
            ["SchemaVersion"] = 1, ["Kind"] = "ambient-evidence", ["Family"] = "fov", ["Seed"] = q.Seed,
            ["Spectrum"] = new { spectrum.Id, spectrum.ContentHash, FileSha256 = q.Spectrum.Sha256, spectrum.IsValidated },
            ["ResolutionDeg"] = resolutionDeg, ["FullyCodedTan"] = tanFc,
            ["AmbientRates"] = ambientRates, ["SourceRates"] = rates, ["Rows"] = rows, ["Timings"] = timings,
            ["ComputeSeconds"] = clock.Elapsed.TotalSeconds
        };
    }

    private sealed class Draws(int n)
    {
        public double[] ErrorDeg { get; } = new double[n];
        public double[] CyclicErrorDeg { get; } = new double[n];
        public bool[] PeakOutside { get; } = new bool[n];
        public double[] PeakSide { get; } = new double[n];
        public double[] CentroidSide { get; } = new double[n];
        public double[] CentroidOffsetMm { get; } = new double[n];
    }

    /// <summary>The legacy study's percentile (nearest rank on the sorted values); NaN for no values.</summary>
    private static double Percentile(IEnumerable<double> values, double q)
    {
        var s = values.OrderBy(v => v).ToArray();
        return s.Length == 0 ? double.NaN : s[Math.Min(s.Length - 1, (int)Math.Floor(q * (s.Length - 1) + 0.5))];
    }
}
