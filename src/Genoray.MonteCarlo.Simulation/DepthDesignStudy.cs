using Genoray.MonteCarlo.Configuration;
using Genoray.MonteCarlo.Core;

namespace Genoray.MonteCarlo.Simulation;

/// <summary>One distance point: how finely the coded aperture can tell a source's DEPTH there.</summary>
public sealed record DepthDesignRow(double DistanceMm, double DepthFwhmMm, double C);

/// <summary>
/// Depth-of-field design study. A coded aperture tells a source's DISTANCE by how sharp its reconstruction
/// peak is versus the assumed focal plane; the width (FWHM) of that sharpness-vs-focal curve IS the depth
/// resolution. Physics says it grows as Δz ≈ C·(z−D)² / (A·D) — quadratic in distance, inverse in the mask
/// aperture A (= rank·mosaic·cell) and the mask–detector gap D. This study MEASURES that FWHM across distance
/// with the MC, fits the constant C, and then inverts the relation: for a chosen "safe standoff" distance and
/// a target depth precision, what mask aperture is required? (The productization trade — a bigger 3D range
/// costs a bigger mask, which is why a handheld only ranges depth up close.)
/// </summary>
public static class DepthDesignStudy
{
    /// <summary>Depth resolution (FWHM of the sharpness-vs-focal curve, mm) for an ON-AXIS source at
    /// <paramref name="trueZ"/> with the given optics. On-axis so the peak stays at the origin across planes;
    /// sharpness is the peak SNR (comparable across planes). The flood is built once at the true distance.</summary>
    public static double DepthResolutionMm(SimulationConfig baseCfg, ISimulationFactory factory, double trueZ,
                                           int steps = 30)
    {
        double d = baseCfg.Geometry.MaskDetectorDistanceMm;
        var cfg = baseCfg.Clone();
        var src = new SourceConfig
        {
            Position = [0, 0, trueZ], ActivityBq = 1.0, DirectionalBiasing = true,
            Lines = [new EmissionLine { EnergyKeV = 661.7, Intensity = 0.851 }],
        };
        cfg.Source = src;
        cfg.Sources = [src];
        cfg.Geometry.SourceMaskDistanceMm = Math.Max(1.0, trueZ - d);
        cfg.Decoder.Cyclic = false;
        var flood = new SimulationRunner(factory).Run(cfg).DetectorImage;

        double zMin = Math.Max(d + 15.0, trueZ * 0.5), zMax = trueZ * 2.0;
        var focal = new double[steps];
        var sharp = new double[steps];
        for (int i = 0; i < steps; i++)
        {
            double z = zMin + (zMax - zMin) * i / (steps - 1);
            focal[i] = z;
            var dc = cfg.Clone();
            dc.Geometry.SourceMaskDistanceMm = Math.Max(1.0, z - d);
            double frac = d / z;
            dc.Decoder.ReconHalfExtentMm = 0.95 * dc.Mask.Rank * dc.Mask.CellPitchMm / frac / 2.0;
            dc.Decoder.ReconStepMm = Math.Max(0.2, dc.Mask.CellPitchMm / frac / 4.0);
            var dec = factory.CreateDecoder(dc)!.Decode(flood);
            var recon = dec.Reconstruction;
            double sum = 0, sum2 = 0;
            int n = recon.Width * recon.Height;
            foreach (var v in recon.Raw) { sum += v; sum2 += v * v; }
            double mean = sum / n, std = Math.Sqrt(Math.Max(1e-9, sum2 / n - mean * mean));
            var pk = MixedFieldStudy.TopPeaks(recon, dec.ReconOriginMm, dec.ReconStepMm, 1, 2.5);
            sharp[i] = pk.Length > 0 ? (pk[0].Value - mean) / std : 0.0;
        }
        return Fwhm(focal, sharp);
    }

    public static double ApertureMm(SimulationConfig cfg) =>
        cfg.Mask.Rank * cfg.Mask.MosaicX * cfg.Mask.CellPitchMm;

    /// <summary>Measure depth resolution across distance and fit C in Δz ≈ C·(z−D)²/(A·D) (far regime).</summary>
    public static (DepthDesignRow[] Rows, double CFit, double ApertureMm, double D) Sweep(
        SimulationConfig baseCfg, ISimulationFactory factory, double[] distancesMm, int steps = 30)
    {
        double d = baseCfg.Geometry.MaskDetectorDistanceMm;
        double a = ApertureMm(baseCfg);
        var rows = new List<DepthDesignRow>();
        double cSum = 0; int cN = 0;
        foreach (var z in distancesMm)
        {
            double fwhm = DepthResolutionMm(baseCfg, factory, z, steps);
            double c = fwhm * a * d / ((z - d) * (z - d));
            rows.Add(new DepthDesignRow(z, fwhm, c));
            if (z > 2.0 * d) { cSum += c; cN++; }        // fit only where the far-field formula holds
        }
        return (rows.ToArray(), cN > 0 ? cSum / cN : 0.0, a, d);
    }

    /// <summary>The farthest distance at which the depth FWHM stays within <paramref name="fwhmFrac"/>·z,
    /// read straight off the MEASURED curve by linear interpolation (no parametric model — the measured FWHM
    /// grows sub-quadratically, so a single-C fit would overstate the aperture needed). Returns the last
    /// swept distance if the limit is never reached.</summary>
    public static double EffectiveRangeMm(DepthDesignRow[] rows, double fwhmFrac)
    {
        for (int i = 0; i < rows.Length; i++)
        {
            double f = rows[i].DepthFwhmMm / rows[i].DistanceMm;
            if (f >= fwhmFrac)
            {
                if (i == 0) return rows[0].DistanceMm;
                double f0 = rows[i - 1].DepthFwhmMm / rows[i - 1].DistanceMm;
                double t = f > f0 ? (fwhmFrac - f0) / (f - f0) : 0.0;
                return rows[i - 1].DistanceMm + t * (rows[i].DistanceMm - rows[i - 1].DistanceMm);
            }
        }
        return rows[^1].DistanceMm;
    }

    private static double Fwhm(double[] x, double[] y)
    {
        int im = 0;
        for (int i = 1; i < y.Length; i++) if (y[i] > y[im]) im = i;
        double max = y[im], baseline = y.Min();
        double half = baseline + (max - baseline) / 2.0;
        if (max <= baseline) return x[^1] - x[0];

        double xl = x[0];
        for (int i = im; i > 0; i--)
            if (y[i - 1] <= half) { xl = Interp(y[i - 1], x[i - 1], y[i], x[i], half); break; }
        double xr = x[^1];
        for (int i = im; i < y.Length - 1; i++)
            if (y[i + 1] <= half) { xr = Interp(y[i + 1], x[i + 1], y[i], x[i], half); break; }
        return Math.Max(0.0, xr - xl);
    }

    private static double Interp(double y0, double x0, double y1, double x1, double yt) =>
        y1 == y0 ? x0 : x0 + (x1 - x0) * (yt - y0) / (y1 - y0);

    public static string ToCsv(DepthDesignRow[] rows)
    {
        var sb = new System.Text.StringBuilder("distance_mm,depth_fwhm_mm,C\n");
        foreach (var r in rows)
            sb.Append($"{r.DistanceMm:F0},{r.DepthFwhmMm:F1},{r.C:F3}\n");
        return sb.ToString();
    }
}
