using Gcam.Configuration;
using Gcam.Core;

namespace Gcam.Simulation;

/// <summary>One shield thickness point: leaked background, localization RMS, and mass.</summary>
public sealed record ShieldRow(
    double ThicknessMm,
    double Transmission,     // fraction of the side/rear background that penetrates the wall
    double BgPerPixel,       // leaked uncoded background counts/pixel
    double RmsRawMm,         // single mask, no background handling
    double RmsCalibMm,       // + calibrated background subtraction (software lever)
    double FailRaw,
    double ShieldMassKg);

/// <summary>
/// Optimal side/top/bottom/rear (5-sided) tungsten shield thickness. The mask is the
/// FRONT (signal); everything entering the crystal from the other five faces is UNCODED
/// noise. A wall of thickness t transmits exp(-mu·t) of an incident background of energy
/// set by mu; the leaked flux lands as an additive uniform background on the flood map
/// (reusing the antimask background-injection). Thicker walls cut the noise but add mass
/// (barrel walls + back plate). We sweep t, measure localization RMS at a fixed source
/// budget, and report the mass — the knee is the useful thickness; past it, weight buys
/// nothing. High-energy background (low mu, e.g. Co-60) never reaches the knee in a
/// hand-carriable mass — that is the "you cannot shield Co-60" result, quantified.
/// </summary>
public sealed class ShieldStudy
{
    private readonly ISimulationFactory _factory;
    private const double RhoWGperMm3 = 19.25e-3;   // tungsten density in g/mm^3

    public ShieldStudy(ISimulationFactory factory) => _factory = factory;

    /// <param name="sideLeakFraction">how much of the leaked background enters through the 4 SIDE walls
    /// (edge-weighted spatial profile) vs the rear wall (uniform). 0 = the uniform pedestal (unchanged);
    /// higher = more directional/structured leak, which the coded decode rejects less well.</param>
    public ShieldRow[] Run(SimulationConfig baseConfig, double nSrc, double bg0PerPixel,
                           double muBgPerMm, double[] thicknesses, int repeats, double failThrMm,
                           double sideLeakFraction = 0.0)
    {
        var mean = new SimulationRunner(_factory).Run(baseConfig);
        double w = mean.DetectedWeight;
        var img = mean.DetectorImage;
        int W = img.Width, H = img.Height;

        var decoder = _factory.CreateDecoder(baseConfig)!;
        var rng = RealizationRandom.For(baseConfig);   // own stream: not the mean map's transport stream
        double tx = baseConfig.Source.Position[0], ty = baseConfig.Source.Position[1];

        // fixed barrel geometry the walls must enclose (mask plane -> behind the crystal).
        // Use the larger detector dimension so a rectangular array is still fully enclosed.
        double det = Math.Max(baseConfig.Detector.PixelsX, baseConfig.Detector.PixelsY)
                     * baseConfig.Detector.PixelPitchMm;
        double barrel = baseConfig.Geometry.MaskDetectorDistanceMm + baseConfig.Detector.CrystalThicknessMm;

        var raw = new DetectorImage(W, H);
        var calib = new DetectorImage(W, H);

        // Spatial profile of the leaked background across the array (mean 1): uniform for the rear-wall
        // component, edge-weighted for the side walls. The coded decode rejects a flat pedestal into DC but
        // not this structure, so a directional leak degrades localization more per count than a uniform one.
        var profile = Background.SideLeakProfile(W, H, sideLeakFraction);

        var rows = new List<ShieldRow>();
        foreach (double t in thicknesses)
        {
            double trans = Math.Exp(-muBgPerMm * t);
            double bg = bg0PerPixel * trans;

            double rSq = 0, cSq = 0; int fail = 0;
            for (int r = 0; r < repeats; r++)
            {
                // Source (scaled to the fixed budget) + the leaked background pedestal (mean bg, shaped by
                // the wall profile). calib subtracts the KNOWN mean profile (a calibrated shield map).
                for (int y = 0; y < H; y++)
                    for (int x = 0; x < W; x++)
                    {
                        double ped = bg * profile[y * W + x];
                        raw[x, y] = Background.RealizePixel(rng, nSrc / w * img[x, y], ped);
                        calib[x, y] = raw[x, y] - ped;
                    }
                double eR = Dist(decoder.Decode(raw).Estimate, tx, ty);
                double eC = Dist(decoder.Decode(calib).Estimate, tx, ty);
                rSq += eR * eR; cSq += eC * eC;
                if (eR > failThrMm) fail++;
            }

            // shield mass: 4 barrel side walls + back plate, tungsten
            double wall = 4.0 * (det + t) * barrel * t;
            double back = (det + 2.0 * t) * (det + 2.0 * t) * t;
            double massKg = (wall + back) * RhoWGperMm3 / 1000.0;

            rows.Add(new ShieldRow(t, trans, bg,
                Math.Sqrt(rSq / repeats), Math.Sqrt(cSq / repeats),
                (double)fail / repeats, massKg));
        }
        return rows.ToArray();
    }

    private static double Dist(SourceEstimate e, double tx, double ty)
        => Math.Sqrt((e.Position.X - tx) * (e.Position.X - tx) + (e.Position.Y - ty) * (e.Position.Y - ty));

    public static string ToCsv(string bgLabel, ShieldRow[] rows)
    {
        var sb = new System.Text.StringBuilder();
        sb.AppendLine("bg,thickness_mm,transmission,bg_per_px,rms_raw_mm,rms_calib_mm,fail_raw,shield_kg");
        foreach (var r in rows)
            sb.AppendLine($"{bgLabel},{r.ThicknessMm:F1},{r.Transmission:F4},{r.BgPerPixel:F3}," +
                          $"{r.RmsRawMm:F3},{r.RmsCalibMm:F3},{r.FailRaw:F3},{r.ShieldMassKg:F3}");
        return sb.ToString();
    }
}
