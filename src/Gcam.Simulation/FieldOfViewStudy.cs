using Gcam.Configuration;
using Gcam.Core;

namespace Gcam.Simulation;

/// <summary>One off-axis angle of a field-of-view sweep, at one distance / direction / count level / background.</summary>
public sealed record FovRow(
    double DistanceMm,        // source–mask distance S
    double DirectionDeg,      // azimuth of the off-axis direction (0 = +x, 45 = diagonal)
    double AngleDeg,          // true off-axis angle seen from the detector centre
    double RelEfficiency,     // detected weight relative to the same source on axis
    double OnAxisCounts,      // detected source counts the same source would give on axis (N0)
    double Bsr,               // uniform ambient pedestal, as a fraction of N0
    double LocalizedNonCyclic,// fraction of realizations within one resolution element (wide non-cyclic decode)
    double LocalizedCyclic,   // same, classical cyclic decode on its one-period grid
    double MedianErrorDeg,    // median angular error of the wide non-cyclic decode
    double SideByCentroid,    // fraction whose flood-centroid side matches the source's side
    double SideByPeak,        // fraction whose decoded-peak side matches the source's side
    double OutsideByPeak,     // fraction whose decoded peak lies outside the (square) fully coded field
    double OutsideByCentroid, // fraction whose centroid offset exceeds the in-field 95th percentile
    double FalseInField,      // fraction decoded to a WRONG spot inside the fully coded field (a ghost-like answer)
    double FalseInFieldUnflagged); // ... of which the centroid flag does not say "outside" (the answer a user would trust)

/// <summary>
/// The usable field of view of the hand-held head at FIELD distance (metres, not the 100 mm lab plane), and
/// what a single acquisition can say about a source OUTSIDE it (TODO-04, LIM-01, D-16).
///
/// For each off-axis angle the biased MC gives the mean flood map; Poisson realizations at a fixed SOURCE
/// strength (a source that gives N0 counts on axis gives N0·ε(θ)/ε(0) here — the shield takes its share) plus
/// an optional uniform ambient pedestal are then
/// (1) decoded non-cyclically over a wide grid (±<c>gridHalfAngleDeg</c>) and cyclically over its one period,
///     counting a success when the angular error is within one resolution element (atan(cell / D));
/// (2) read for a left/right cue in two ways: the side of the decoded peak, and the side of the flood
///     centroid. The aperture's shadow moves away from the source while the 10 mm front plate around the mask
///     only attenuates (about 17 % leak at 662 keV), so the lit part of the array is on the side opposite the
///     source until the aperture's shadow leaves the array: along x at atan((mask half-width + S/(D+S) times the
///     detector half-width) / D), about 15° here. The centroid rule is a measured cue, not a guarantee: the code
///     pattern, the plate leak, background and noise all move the centroid too.
/// The fully coded field is the factory's square one-period field: a source is inside it when
/// max(|x|, |y|) / (D+S) is at most rank·cell / (2D). The "outside" flag by centroid uses a threshold set on the
/// in-field angles of the same series (95th percentile of the pooled centroid offsets), so its false-alarm rate
/// over those pooled samples is about 5 %.
/// </summary>
public sealed class FieldOfViewStudy
{
    public FovRow[] Run(SimulationConfig baseConfig, double distanceMm, double directionDeg, double[] anglesDeg,
                        double[] onAxisCounts, double[] bsrValues, int repeats, double gridHalfAngleDeg = 20.0,
                        double gridStepDeg = 0.3)
    {
        if (anglesDeg.Length == 0 || anglesDeg.Any(a => !(a >= 0.0)))
            throw new ArgumentException("angles must be non-negative (the direction carries the side)", nameof(anglesDeg));
        var factory = new DefaultSimulationFactory();
        double d = baseConfig.Geometry.MaskDetectorDistanceMm;
        double planeZ = d + distanceMm;                         // source plane, from the detector
        double resolutionDeg = Rad2Deg(Math.Atan(baseConfig.Mask.CellPitchMm / d));
        double tanFc = baseConfig.Mask.Rank * baseConfig.Mask.CellPitchMm / 2.0 / d;
        bool InField(double x, double y) => Math.Max(Math.Abs(x), Math.Abs(y)) / planeZ <= tanFc + 1e-12;
        double phi = directionDeg * Math.PI / 180.0;
        double ux = Math.Cos(phi), uy = Math.Sin(phi);

        SimulationConfig At(double angleDeg, bool cyclic)
        {
            var c = baseConfig.Clone();
            c.Geometry.SourceMaskDistanceMm = distanceMm;
            c.Background = null;
            c.Sources = null;                                    // one moving source; a scene would override it
            double r = planeZ * Math.Tan(angleDeg * Math.PI / 180.0);
            c.Source.Position = [r * ux, r * uy, 0.0];
            c.Source.DirectionalBiasing = true;
            c.Decoder.Cyclic = cyclic;
            if (cyclic)
            {
                c.Decoder.ReconHalfExtentMm = null;              // one period, the classical grid
                c.Decoder.ReconStepMm = null;
            }
            else
            {
                c.Decoder.ReconHalfExtentMm = planeZ * Math.Tan(gridHalfAngleDeg * Math.PI / 180.0);
                c.Decoder.ReconStepMm = planeZ * Math.Tan(gridStepDeg * Math.PI / 180.0);
            }
            return c;
        }

        // Mean flood maps (one biased MC per angle) and their efficiencies.
        var maps = new DetectorImage[anglesDeg.Length];
        var eff = new double[anglesDeg.Length];
        Parallel.For(0, anglesDeg.Length, i =>
        {
            var cfg = At(anglesDeg[i], cyclic: false);
            cfg.Seed = (baseConfig.Seed ?? 0) + 7919 * (i + 1);
            var res = new SimulationRunner(factory).Run(cfg);
            maps[i] = res.DetectorImage;
            eff[i] = res.PhotonsEmitted > 0 ? res.DetectedWeight / res.PhotonsEmitted : 0.0;
        });
        double eff0 = EfficiencyOnAxis(anglesDeg, eff, () =>
        {
            var cfg = At(0.0, cyclic: false);
            cfg.Seed = baseConfig.Seed ?? 0;
            var res = new SimulationRunner(factory).Run(cfg);
            return res.PhotonsEmitted > 0 ? res.DetectedWeight / res.PhotonsEmitted : 0.0;
        });
        if (!(eff0 > 0.0)) throw new InvalidOperationException("no detected weight on axis: cannot normalise the source strength");

        var wideDecoder = factory.CreateDecoder(At(0.0, cyclic: false))!;
        var cyclicDecoder = factory.CreateDecoder(At(0.0, cyclic: true))!;
        int w = maps[0].Width, h = maps[0].Height;
        double pitch = baseConfig.Detector.PixelPitchMm;

        var rows = new List<FovRow>();
        for (int ci = 0; ci < onAxisCounts.Length; ci++)
            for (int bi = 0; bi < bsrValues.Length; bi++)
            {
                double n0 = onAxisCounts[ci], bsr = bsrValues[bi];
                double pedestal = Background.PedestalPerPixel(bsr, n0, w * h);
                var per = new Realized[anglesDeg.Length];
                Parallel.For(0, anglesDeg.Length, i =>
                {
                    double sum = 0.0;
                    foreach (double v in maps[i].Raw) sum += v;
                    double expected = n0 * eff[i] / eff0;               // same source, shielded share
                    double scale = sum > 0 ? expected / sum : 0.0;
                    // Deterministic per (angle, count level, background) stream, reproducible across processes.
                    var rng = new DefaultRandom(unchecked((baseConfig.Seed ?? 0) + 1_000_003 * (i + 1)
                                                         + 7_919 * (ci + 1) + 104_729 * (bi + 1)));
                    double tx = planeZ * Math.Tan(anglesDeg[i] * Math.PI / 180.0) * ux;
                    double ty = planeZ * Math.Tan(anglesDeg[i] * Math.PI / 180.0) * uy;
                    per[i] = Realize(maps[i], scale, pedestal, rng, repeats, wideDecoder, cyclicDecoder,
                                     planeZ, tx, ty, ux, uy, pitch, InField);
                });

                // Centroid threshold from the in-field angles of THIS series (false alarms ~5 % in field).
                var inField = new List<double>();
                for (int i = 0; i < anglesDeg.Length; i++)
                {
                    double r = planeZ * Math.Tan(anglesDeg[i] * Math.PI / 180.0);
                    if (InField(r * ux, r * uy)) inField.AddRange(per[i].CentroidOffsetMm);
                }
                double thr = Percentile(inField, 0.95);

                for (int i = 0; i < anglesDeg.Length; i++)
                {
                    var p = per[i];
                    bool hasSide = anglesDeg[i] > 0.0;
                    rows.Add(new FovRow(distanceMm, directionDeg, anglesDeg[i], eff[i] / eff0,
                        n0, bsr,
                        Frac(p.ErrorDeg, e => e <= resolutionDeg),
                        Frac(p.CyclicErrorDeg, e => e <= resolutionDeg),
                        Percentile(p.ErrorDeg, 0.5),
                        hasSide ? Frac(p.CentroidSide, s => s > 0) : double.NaN,
                        hasSide ? Frac(p.PeakSide, s => s > 0) : double.NaN,
                        Frac(p.PeakOutside, o => o > 0),
                        double.IsNaN(thr) ? double.NaN : Frac(p.CentroidOffsetMm, c => c > thr),
                        FracOf(repeats, r => p.PeakOutside[r] == 0.0 && p.ErrorDeg[r] > resolutionDeg),
                        double.IsNaN(thr) ? double.NaN
                            : FracOf(repeats, r => p.PeakOutside[r] == 0.0 && p.ErrorDeg[r] > resolutionDeg
                                                   && !(p.CentroidOffsetMm[r] > thr))));
                }
            }
        return rows.ToArray();
    }

    /// <summary>Half-angle of the (square) fully coded field along azimuth <paramref name="directionDeg"/>: one MURA
    /// period seen from the detector, rank·cell / 2 over D, divided by max(|cos φ|, |sin φ|) off the axes.</summary>
    public static double FullyCodedHalfAngleDeg(SimulationConfig cfg, double directionDeg = 0.0)
    {
        double phi = directionDeg * Math.PI / 180.0;
        double k = Math.Max(Math.Abs(Math.Cos(phi)), Math.Abs(Math.Sin(phi)));
        return Rad2Deg(Math.Atan(cfg.Mask.Rank * cfg.Mask.CellPitchMm / 2.0 / cfg.Geometry.MaskDetectorDistanceMm / k));
    }

    /// <summary>Largest angle up to which every row of a series meets <paramref name="minFraction"/>
    /// (rows sorted by angle; 0 if the first row already fails).</summary>
    public static double UsableHalfAngleDeg(IEnumerable<FovRow> series, Func<FovRow, double> metric, double minFraction)
    {
        double last = 0.0;
        foreach (var r in series.OrderBy(r => r.AngleDeg))
        {
            if (!(metric(r) >= minFraction)) break;
            last = r.AngleDeg;
        }
        return last;
    }

    private sealed record Realized(double[] ErrorDeg, double[] CyclicErrorDeg, double[] PeakOutside,
                                   double[] PeakSide, double[] CentroidSide, double[] CentroidOffsetMm);

    private static Realized Realize(DetectorImage mean, double scale, double pedestal, IRandom rng, int repeats,
                                    IDecoder wide, IDecoder cyclic, double planeZ, double tx, double ty,
                                    double ux, double uy, double pitch, Func<double, double, bool> inField)
    {
        var err = new double[repeats];
        var cerr = new double[repeats];
        var peakOutside = new double[repeats];
        var peakSide = new double[repeats];
        var cenSide = new double[repeats];
        var cenOff = new double[repeats];
        var noisy = new DetectorImage(mean.Width, mean.Height);
        double halfW = mean.Width * pitch / 2.0, halfH = mean.Height * pitch / 2.0;
        for (int r = 0; r < repeats; r++)
        {
            Background.Realize(noisy, mean, scale, pedestal, rng);

            var est = wide.Decode(noisy).Estimate.Position;
            err[r] = AngularDistanceDeg(est.X, est.Y, tx, ty, planeZ);
            peakOutside[r] = inField(est.X, est.Y) ? 0.0 : 1.0;
            peakSide[r] = Math.Sign(est.X * ux + est.Y * uy);

            var cest = cyclic.Decode(noisy).Estimate.Position;
            cerr[r] = AngularDistanceDeg(cest.X, cest.Y, tx, ty, planeZ);

            // Flood centroid (mm from the array centre). The shield edge moves the lit area AWAY from the
            // source, so the source side is the opposite of the centroid's. An empty flood gives side 0 (a miss).
            double s = 0.0, sx = 0.0, sy = 0.0;
            for (int y = 0; y < noisy.Height; y++)
                for (int x = 0; x < noisy.Width; x++)
                {
                    double v = noisy[x, y];
                    s += v;
                    sx += v * ((x + 0.5) * pitch - halfW);
                    sy += v * ((y + 0.5) * pitch - halfH);
                }
            double cx = s > 0 ? sx / s : 0.0, cy = s > 0 ? sy / s : 0.0;
            cenSide[r] = -Math.Sign(cx * ux + cy * uy);
            cenOff[r] = Math.Sqrt(cx * cx + cy * cy);
        }
        return new Realized(err, cerr, peakOutside, peakSide, cenSide, cenOff);
    }

    private static double EfficiencyOnAxis(double[] anglesDeg, double[] eff, Func<double> run)
    {
        int i = Array.IndexOf(anglesDeg, 0.0);
        return i >= 0 ? eff[i] : run();
    }

    // Angle between the directions (x1, y1, Z) and (x2, y2, Z) seen from the detector centre.
    private static double AngularDistanceDeg(double x1, double y1, double x2, double y2, double planeZ)
    {
        double dot = x1 * x2 + y1 * y2 + planeZ * planeZ;
        double n1 = Math.Sqrt(x1 * x1 + y1 * y1 + planeZ * planeZ), n2 = Math.Sqrt(x2 * x2 + y2 * y2 + planeZ * planeZ);
        return Rad2Deg(Math.Acos(Math.Clamp(dot / (n1 * n2), -1.0, 1.0)));
    }

    private static double Frac(IReadOnlyCollection<double> values, Func<double, bool> pass)
        => values.Count > 0 ? (double)values.Count(pass) / values.Count : 0.0;

    private static double FracOf(int n, Func<int, bool> pass)
    {
        int k = 0;
        for (int i = 0; i < n; i++) if (pass(i)) k++;
        return n > 0 ? (double)k / n : 0.0;
    }

    /// <summary>The contiguous angle range, starting at the first row that meets <paramref name="minFraction"/>,
    /// over which every row meets it; null if no row does.</summary>
    public static (double From, double To)? PassingRange(IEnumerable<FovRow> series, Func<FovRow, double> metric,
                                                         double minFraction)
    {
        double? from = null, to = null;
        foreach (var r in series.OrderBy(r => r.AngleDeg))
        {
            bool ok = metric(r) >= minFraction;
            if (from is null) { if (ok) from = to = r.AngleDeg; continue; }
            if (!ok) break;
            to = r.AngleDeg;
        }
        return from is null ? null : (from.Value, to!.Value);
    }

    private static double Percentile(IEnumerable<double> values, double q)
    {
        var s = values.OrderBy(v => v).ToArray();
        if (s.Length == 0) return double.NaN;
        return s[Math.Min(s.Length - 1, (int)Math.Floor(q * (s.Length - 1) + 0.5))];
    }

    private static double Rad2Deg(double rad) => rad * 180.0 / Math.PI;

    public static string ToCsv(IEnumerable<FovRow> rows)
    {
        var sb = new System.Text.StringBuilder();
        sb.AppendLine("distance_mm,direction_deg,angle_deg,rel_efficiency,onaxis_counts,bsr,loc_noncyclic,loc_cyclic,median_err_deg,side_centroid,side_peak,outside_peak,outside_centroid,false_infield,false_infield_unflagged");
        foreach (var r in rows)
            sb.AppendLine(FormattableString.Invariant(
                $"{r.DistanceMm:F0},{r.DirectionDeg:F0},{r.AngleDeg:F2},{r.RelEfficiency:F4},{r.OnAxisCounts:F0},{r.Bsr:F2},{r.LocalizedNonCyclic:F3},{r.LocalizedCyclic:F3},{r.MedianErrorDeg:F3},{r.SideByCentroid:F3},{r.SideByPeak:F3},{r.OutsideByPeak:F3},{r.OutsideByCentroid:F3},{r.FalseInField:F3},{r.FalseInFieldUnflagged:F3}"));
        return sb.ToString();
    }
}
