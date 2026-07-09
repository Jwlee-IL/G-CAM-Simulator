using Genoray.MonteCarlo.Configuration;
using Genoray.MonteCarlo.Core;

namespace Genoray.MonteCarlo.Simulation;

/// <summary>One background scenario: single-mask vs mask/antimask localization.</summary>
public sealed record AntimaskRow(
    string Scenario,
    double BgPerPixel,
    double Gradient,
    double SingleErrMm,      // (a) software flip only: balanced decode of one exposure
    double CalibErrMm,       // (b) flip + subtract calibrated background (no moving part)
    double AntimaskErrMm,    // (c) physical two-exposure mask/antimask
    double SingleFail,
    double CalibFail,
    double AntimaskFail);

/// <summary>
/// Empirically tests when the two-exposure mask/antimask technique earns its keep.
/// Single mask uses the full budget; mask/antimask splits it (fair total time) and
/// decodes the DIFFERENCE of the two exposures — which cancels any *additive* background
/// common to both, but adds their Poisson noise. The balanced MURA decoder only rejects a
/// uniform background in the ideal infinite-cyclic limit; this finite near-field
/// candidate-search decoder picks up background artifacts even from a uniform field, so
/// mask/antimask (or an equivalent calibrated background subtraction) wins against any
/// significant additive background, not only a structured one.
/// </summary>
public sealed class MaskAntimaskStudy
{
    private readonly ISimulationFactory _factory;

    public MaskAntimaskStudy(ISimulationFactory factory)
    {
        _factory = factory;
    }

    public AntimaskRow[] Run(SimulationConfig baseConfig, double nSrc, int repeats, double failThrMm,
                             (string name, double bgPerPixel, double gradient)[] scenarios)
    {
        var cfgA = baseConfig.Clone(); cfgA.Mask.Invert = false;
        var cfgB = baseConfig.Clone(); cfgB.Mask.Invert = true;

        var meanA = new SimulationRunner(_factory).Run(cfgA);
        var meanB = new SimulationRunner(_factory).Run(cfgB);
        double wA = meanA.DetectedWeight, wB = meanB.DetectedWeight;
        var imgA = meanA.DetectorImage;
        var imgB = meanB.DetectorImage;
        int W = imgA.Width, H = imgA.Height;

        var decoder = _factory.CreateDecoder(cfgA)!;
        var rng = _factory.CreateRandom(cfgA);
        double tx = cfgA.Source.Position[0], ty = cfgA.Source.Position[1];

        var single = new DetectorImage(W, H);
        var calib = new DetectorImage(W, H);
        var expA = new DetectorImage(W, H);
        var expB = new DetectorImage(W, H);
        var diff = new DetectorImage(W, H);

        var rows = new List<AntimaskRow>();
        foreach (var (name, bg, grad) in scenarios)
        {
            // Additive background counts/pixel: uniform (grad=0) or a 0..2·bg ramp in x.
            double Bg(int x) => grad > 0 ? bg * 2.0 * x / Math.Max(1, W - 1) : bg;

            double sSq = 0, cSq = 0, aSq = 0;
            int sFail = 0, cFail = 0, aFail = 0;
            for (int r = 0; r < repeats; r++)
            {
                for (int y = 0; y < H; y++)
                    for (int x = 0; x < W; x++)
                    {
                        double b = Bg(x);
                        // (a) Single mask: full source budget + full background.
                        single[x, y] = Sampling.Poisson(rng, nSrc / wA * imgA[x, y] + b);
                        // (b) Same exposure, subtract the CALIBRATED (known, mean) background.
                        calib[x, y] = single[x, y] - b;
                        // (c) Mask/antimask: half budget + half background each exposure.
                        expA[x, y] = Sampling.Poisson(rng, 0.5 * nSrc / wA * imgA[x, y] + 0.5 * b);
                        expB[x, y] = Sampling.Poisson(rng, 0.5 * nSrc / wB * imgB[x, y] + 0.5 * b);
                        diff[x, y] = expA[x, y] - expB[x, y];
                    }

                double errS = Dist(decoder.Decode(single).Estimate, tx, ty);
                double errC = Dist(decoder.Decode(calib).Estimate, tx, ty);
                double errA = Dist(decoder.Decode(diff).Estimate, tx, ty);
                sSq += errS * errS; cSq += errC * errC; aSq += errA * errA;
                if (errS > failThrMm) sFail++;
                if (errC > failThrMm) cFail++;
                if (errA > failThrMm) aFail++;
            }
            rows.Add(new AntimaskRow(name, bg, grad,
                Math.Sqrt(sSq / repeats), Math.Sqrt(cSq / repeats), Math.Sqrt(aSq / repeats),
                (double)sFail / repeats, (double)cFail / repeats, (double)aFail / repeats));
        }
        return rows.ToArray();
    }

    private static double Dist(SourceEstimate e, double tx, double ty)
        => Math.Sqrt((e.Position.X - tx) * (e.Position.X - tx) + (e.Position.Y - ty) * (e.Position.Y - ty));

    public static string ToCsv(AntimaskRow[] rows)
    {
        var sb = new System.Text.StringBuilder();
        sb.AppendLine("scenario,bg_per_pixel,gradient,single_rms_mm,calib_rms_mm,antimask_rms_mm,single_fail,calib_fail,antimask_fail");
        foreach (var r in rows)
            sb.AppendLine($"{r.Scenario},{r.BgPerPixel:F2},{r.Gradient:F1},{r.SingleErrMm:F3},{r.CalibErrMm:F3},{r.AntimaskErrMm:F3},{r.SingleFail:F3},{r.CalibFail:F3},{r.AntimaskFail:F3}");
        return sb.ToString();
    }
}
