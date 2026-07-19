using Genoray.MonteCarlo.Configuration;
using Genoray.MonteCarlo.Core;
using Genoray.MonteCarlo.Detector;
using Genoray.MonteCarlo.Masks;

namespace Genoray.MonteCarlo.Simulation;

/// <summary>One source diameter: the reconstruction-peak blur and localization it produces.</summary>
public sealed record FiniteSourceRow(
    double DiameterMm,
    double ReconPeakFwhmMm,      // FWHM of the reconstruction peak (the imaging resolution) — grows with source size
    double LocalizationBiasMm,   // |decoded − true| on the source plane (≈0 until the peak washes out)
    double ReconConfidence);     // peak/secondary — falls as the source blurs the shadow; ≈1 = washed out (no peak)

/// <summary>One capsule thickness: the self-attenuation (transmitted fraction) it imposes, per energy line.</summary>
public sealed record CapsuleRow(double ThicknessMm, double TransmissionPrimary, double TransmissionLowE);

/// <summary>
/// Models a FINITE (extended) radioactive source instead of an ideal mathematical point, plus optional CAPSULE
/// self-attenuation — the audit's finite-source / capsule item. Two physical effects:
/// <list type="bullet">
/// <item><b>Source SIZE → reconstruction blur.</b> Each emission point in the active volume casts a slightly shifted
/// coded shadow; summed over the volume the reconstruction peak is CONVOLVED with the source's projected profile, so
/// its FWHM (the imaging resolution) grows once the source diameter approaches the point-source resolution — a
/// resolution FLOOR set by the source, not the camera.</item>
/// <item><b>CAPSULE self-attenuation.</b> A sealed source's gammas must escape the active pellet and its housing; the
/// transmitted fraction falls with capsule thickness and — because μ is energy-dependent — kills LOW-energy lines
/// (Cs-137's 32 keV Ba X-rays) far more than the 662 keV primary.</item>
/// </list>
/// The size-blur study builds the flood by real transport from emission points sampled in a sphere and decodes it;
/// the capsule study is an attenuation MC over emission point + direction. Self-contained (it does not change the main
/// source/pipeline).
///
/// Scope: the flood uses an IDEAL binary zero-thickness MURA lookup at the mask mid-plane (no tungsten leakage /
/// hole-fraction / taper / fabrication / alignment / detector attenuation) — the ideal coded primary shadow, not the
/// full tungsten-slab response. The quadrature blur is a small-blur heuristic (the projected sphere is non-Gaussian
/// with some z-defocus). "Washout" means THIS point-source argmax decoder no longer has a unique peak, not that an
/// extended source carries no information. The capsule sweep uses the pellet exit path plus a steel-EQUIVALENT normal
/// wall thickness (not exact spherical-shell transport).
/// </summary>
public sealed class FiniteSourceStudy
{
    private readonly ISimulationFactory _factory;

    public FiniteSourceStudy(ISimulationFactory? factory = null)
    {
        _factory = factory ?? new DefaultSimulationFactory();
    }

    /// <summary>Sweep source DIAMETER; for each, build the coded flood from a finite spherical source and report the
    /// reconstruction-peak FWHM (resolution) and localization bias. Also returns the peak profile at the largest
    /// diameter and at the point limit for plotting.</summary>
    public (FiniteSourceRow[] rows, double[] profilePoint, double[] profileExtended, double profileStepMm) Run(
        SimulationConfig config, double[] diametersMm, double sourceXMm, double sourceYMm,
        long photonCount, int? seed = null)
    {
        var m = config.Mask;
        var pattern = MuraGenerator.Mosaic(m.Rank, m.MosaicX, m.MosaicY);
        double pitch = m.CellPitchMm;
        double maskHalfW = pattern.Width * pitch / 2.0, maskHalfH = pattern.Height * pitch / 2.0;
        double maskZ = config.Geometry.MaskDetectorDistanceMm;
        double sourceZ = maskZ + config.Geometry.SourceMaskDistanceMm;
        int W = config.Detector.PixelsX, H = config.Detector.PixelsY;
        double detPitch = config.Detector.PixelPitchMm;
        double detHalfW = W * detPitch / 2.0, detHalfH = H * detPitch / 2.0;
        double norm = (2.0 * detHalfW) * (2.0 * detHalfH) / (4.0 * Math.PI);
        var decoder = _factory.CreateDecoder(config)!;

        var rows = new List<FiniteSourceRow>();
        double[] profilePoint = [], profileExtended = [];
        double profileStep = 0.0;
        double maxDia = diametersMm.Length > 0 ? diametersMm.Max() : 0.0;

        foreach (double dia in diametersMm)
        {
            var rng = new DefaultRandom((seed ?? config.Seed ?? 0) + (int)Math.Round(dia * 100));
            var flood = new DetectorImage(W, H);
            double radius = dia / 2.0;

            for (long n = 0; n < photonCount; n++)
            {
                // Emission point uniformly inside the source sphere (diameter dia) at (sourceX, sourceY, sourceZ).
                var off = radius > 0 ? RandomInSphere(rng, radius) : new Vector3(0, 0, 0);
                var src = new Vector3(sourceXMm + off.X, sourceYMm + off.Y, sourceZ + off.Z);

                double tx = (rng.NextDouble() * 2.0 - 1.0) * detHalfW;
                double ty = (rng.NextDouble() * 2.0 - 1.0) * detHalfH;
                var aim = new Vector3(tx, ty, 0.0);
                var delta = aim - src;
                double r = delta.Length;
                var d = delta * (1.0 / r);
                if (d.Z >= 0.0) continue;
                double weight = norm * Math.Abs(d.Z) / (r * r);

                var maskHit = src + d * ((maskZ - src.Z) / d.Z);
                double u = maskHit.X + maskHalfW, v = maskHit.Y + maskHalfH;
                if (u < 0.0 || v < 0.0 || u >= 2.0 * maskHalfW || v >= 2.0 * maskHalfH) continue;
                int cx = (int)(u / pitch), cy = (int)(v / pitch);
                if (cx < pattern.Width && cy < pattern.Height && pattern[cx, cy])   // open cell → coded primary
                {
                    int ix = (int)((aim.X + detHalfW) / detPitch), iy = (int)((aim.Y + detHalfH) / detPitch);
                    if (ix >= 0 && iy >= 0 && ix < W && iy < H) flood.Add(ix, iy, weight);
                }
            }

            var res = decoder.Decode(flood);
            double fwhm = PeakFwhmMm(res, out double[] profile);
            double bias = Math.Sqrt((res.Estimate.Position.X - sourceXMm) * (res.Estimate.Position.X - sourceXMm) +
                                    (res.Estimate.Position.Y - sourceYMm) * (res.Estimate.Position.Y - sourceYMm));
            rows.Add(new FiniteSourceRow(dia, fwhm, bias, res.Estimate.Confidence));
            profileStep = res.ReconStepMm;
            if (dia <= 0.0) profilePoint = profile;
            if (Math.Abs(dia - maxDia) < 1e-9) profileExtended = profile;
        }
        return (rows.ToArray(), profilePoint, profileExtended, profileStep);
    }

    /// <summary>Capsule self-attenuation: mean transmitted fraction of the primary and a low-energy line through a
    /// spherical active pellet (diameter <paramref name="pelletDiameterMm"/>) inside a shell of thickness t, over
    /// emission point + isotropic-toward-detector direction. μ values are linear attenuation (per mm) at each energy.</summary>
    public CapsuleRow[] CapsuleSweep(double[] thicknessesMm, double pelletDiameterMm,
                                     double muPelletPrimary, double muCapsulePrimary,
                                     double muPelletLowE, double muCapsuleLowE, long samples, int seed = 424242)
    {
        var rng = new DefaultRandom(seed);
        double radius = pelletDiameterMm / 2.0;
        var rows = new List<CapsuleRow>();
        foreach (double tc in thicknessesMm)
        {
            double sumP = 0.0, sumL = 0.0;
            for (long i = 0; i < samples; i++)
            {
                var p = radius > 0 ? RandomInSphere(rng, radius) : new Vector3(0, 0, 0);
                var dir = rng.NextOnUnitSphere();
                double pelletPath = RayExitDistanceFromSphere(p, dir, radius);   // path through remaining pellet
                double capsulePath = tc;   // steel-EQUIVALENT normal wall thickness (not exact spherical-shell path)
                sumP += Math.Exp(-muPelletPrimary * pelletPath - muCapsulePrimary * capsulePath);
                sumL += Math.Exp(-muPelletLowE * pelletPath - muCapsuleLowE * capsulePath);
            }
            rows.Add(new CapsuleRow(tc, sumP / samples, sumL / samples));
        }
        return rows.ToArray();
    }

    // FWHM (mm) of the reconstruction peak, measured along x through the argmax row at half of (peak − baseline).
    private static double PeakFwhmMm(DecodeResult res, out double[] peakRow)
    {
        var recon = res.Reconstruction;
        int bx = 0, by = 0; double peak = double.NegativeInfinity;
        for (int y = 0; y < recon.Height; y++)
            for (int x = 0; x < recon.Width; x++)
                if (recon[x, y] > peak) { peak = recon[x, y]; bx = x; by = y; }

        peakRow = new double[recon.Width];
        var sorted = new List<double>(recon.Width);
        for (int x = 0; x < recon.Width; x++) { peakRow[x] = recon[x, by]; sorted.Add(recon[x, by]); }
        sorted.Sort();
        double baseline = sorted[sorted.Count / 2];                     // median of the peak row ≈ sidelobe floor
        double half = baseline + 0.5 * (peak - baseline);

        // Interpolated half-max crossings either side of the peak column. If a crossing is missing (a broad,
        // edge-clipped, or washed-out peak) the FWHM is meaningless — return NaN rather than a truncated width.
        double left = double.NaN, right = double.NaN;
        for (int x = bx; x > 0; x--)
            if (peakRow[x] >= half && peakRow[x - 1] < half)
            { left = x - 1 + (half - peakRow[x - 1]) / (peakRow[x] - peakRow[x - 1]); break; }
        for (int x = bx; x < recon.Width - 1; x++)
            if (peakRow[x] >= half && peakRow[x + 1] < half)
            { right = x + (peakRow[x] - half) / (peakRow[x] - peakRow[x + 1]); break; }
        return (right - left) * res.ReconStepMm;   // NaN if either crossing was not found
    }

    private static Vector3 RandomInSphere(IRandom rng, double radius)
    {
        var dir = rng.NextOnUnitSphere();
        double rr = radius * Math.Cbrt(rng.NextDouble());     // uniform in volume
        return dir * rr;
    }

    // Distance from an interior point p (relative to sphere centre) to the sphere surface along unit dir.
    private static double RayExitDistanceFromSphere(Vector3 p, Vector3 dir, double radius)
    {
        double b = p.Dot(dir);
        double c = p.Dot(p) - radius * radius;
        double disc = b * b - c;
        return disc <= 0 ? 0.0 : -b + Math.Sqrt(disc);        // positive root = forward exit
    }

    public static string ToCsv(FiniteSourceRow[] rows)
    {
        var sb = new System.Text.StringBuilder();
        sb.AppendLine("diameter_mm,recon_peak_fwhm_mm,localization_bias_mm,confidence");
        foreach (var r in rows)
            sb.AppendLine($"{r.DiameterMm:F2},{r.ReconPeakFwhmMm:F4},{r.LocalizationBiasMm:F4},{r.ReconConfidence:F4}");
        return sb.ToString();
    }

    public static string CapsuleCsv(CapsuleRow[] rows)
    {
        var sb = new System.Text.StringBuilder();
        sb.AppendLine("capsule_mm,transmission_primary,transmission_lowE");
        foreach (var r in rows)
            sb.AppendLine($"{r.ThicknessMm:F2},{r.TransmissionPrimary:F4},{r.TransmissionLowE:F4}");
        return sb.ToString();
    }
}
