using Genoray.MonteCarlo.Configuration;
using Genoray.MonteCarlo.Core;
using Genoray.MonteCarlo.Detector;

namespace Genoray.MonteCarlo.Simulation;

/// <summary>One multi-pixel positioning strategy under crystal Compton scattering.</summary>
public sealed record StrategyRow(
    string Strategy,
    double EfficiencyRel,   // accepted counts relative to the ideal (no-Compton) detector
    double BiasMm,          // localization error of the high-statistics decode
    double RmsMm,           // localization RMS at a fixed acquisition (Poisson realizations)
    double FailRate);

/// <summary>Combined-field reconstruction grids for the multi-isotope contamination demo.</summary>
public sealed class ContaminationResult
{
    public required DetectorImage ReconCs { get; init; }        // Cs-137 alone, 662 window
    public required DetectorImage ReconContaminant { get; init; }// Co-60 downscatter into the 662 window
    public required DetectorImage ReconCombined { get; init; }   // the two summed
    public double OriginMm { get; init; }
    public double StepMm { get; init; }
    public double ContaminationFraction { get; init; }           // Co-60 counts / total, in the 662 window
    public required double[] CsPos { get; init; }
    public required double[] CoPos { get; init; }
}

/// <summary>
/// Crystal-internal Compton scattering studies. (A) compares multi-pixel positioning strategies
/// (efficiency vs localization) for a single source; (B) shows that a higher isotope's Compton
/// downscatter into a lower isotope's energy window is still coded from the CONTAMINANT's
/// direction, so the coded-aperture decode separates the two spatially — the thing an energy
/// window alone cannot do.
/// </summary>
public sealed class ComptonStudy
{
    private const double CoLine1 = 1173.2, CoLine2 = 1332.5, CsLine = 661.7;

    public StrategyRow[] RunStrategies(SimulationConfig baseConfig, double windowFraction,
                                       double detectedBudget, int repeats, double failThrMm)
    {
        var cfg = baseConfig.Clone();
        double tx = cfg.Source.Position[0], ty = cfg.Source.Position[1];
        double eKeV = cfg.Source.EnergyKeV;

        var ideal = new SimulationRunner(new DefaultSimulationFactory()).Run(cfg);
        double idealWeight = ideal.DetectedWeight;
        double scale = idealWeight > 0 ? detectedBudget / idealWeight : 0.0;

        var rows = new List<StrategyRow>
        {
            Eval("Ideal (no Compton)", cfg, ideal, idealWeight, scale, repeats, failThrMm, tx, ty)
        };
        foreach (var strat in new[] { ComptonStrategy.PerPixelWindow, ComptonStrategy.AntiCoincidence,
                                      ComptonStrategy.Argmax, ComptonStrategy.Centroid })
        {
            var res = new SimulationRunner(new ComptonFactory(strat, eKeV, windowFraction)).Run(cfg);
            rows.Add(Eval(strat.ToString(), cfg, res, idealWeight, scale, repeats, failThrMm, tx, ty));
        }
        return rows.ToArray();
    }

    private StrategyRow Eval(string name, SimulationConfig cfg, SimulationResult res, double idealWeight,
                             double scale, int repeats, double failThrMm, double tx, double ty)
    {
        var decoder = new DefaultSimulationFactory().CreateDecoder(cfg)!;
        var img = res.DetectorImage;
        double bias = Dist(decoder.Decode(img).Estimate, tx, ty);

        var rng = new DefaultRandom(cfg.Seed + 999);
        var noisy = new DetectorImage(img.Width, img.Height);
        double sumSq = 0.0; int fails = 0;
        for (int rep = 0; rep < repeats; rep++)
        {
            for (int y = 0; y < img.Height; y++)
                for (int x = 0; x < img.Width; x++)
                    noisy[x, y] = Sampling.Poisson(rng, img[x, y] * scale);
            double err = Dist(decoder.Decode(noisy).Estimate, tx, ty);
            sumSq += err * err;
            if (err > failThrMm) fails++;
        }
        double effRel = idealWeight > 0 ? res.DetectedWeight / idealWeight : 0.0;
        return new StrategyRow(name, effRel, bias, Math.Sqrt(sumSq / repeats), (double)fails / repeats);
    }

    public ContaminationResult RunContamination(SimulationConfig baseConfig, double[] csPos,
                                                double[] coPos, double windowFraction)
    {
        // Cs-137 at csPos: real 662 photopeak counts.
        var cfgCs = baseConfig.Clone();
        cfgCs.Source.Position = (double[])csPos.Clone();
        cfgCs.Source.EnergyKeV = CsLine;
        var cs = new SimulationRunner(new ComptonFactory(ComptonStrategy.PerPixelWindow, CsLine, windowFraction)).Run(cfgCs);

        // Co-60 at coPos: its two lines downscatter in the crystal into the Cs 662 window.
        var mapCo = new DetectorImage(cs.DetectorImage.Width, cs.DetectorImage.Height);
        foreach (double line in new[] { CoLine1, CoLine2 })
        {
            var cfgCo = baseConfig.Clone();
            cfgCo.Source.Position = (double[])coPos.Clone();
            cfgCo.Source.EnergyKeV = line;
            var co = new SimulationRunner(new ComptonFactory(ComptonStrategy.PerPixelWindow, CsLine, windowFraction)).Run(cfgCo);
            Add(mapCo, co.DetectorImage);
        }

        var combined = new DetectorImage(mapCo.Width, mapCo.Height);
        Add(combined, cs.DetectorImage);
        Add(combined, mapCo);

        var decoder = new DefaultSimulationFactory().CreateDecoder(cfgCs)!;
        var dCs = decoder.Decode(cs.DetectorImage);
        var dCo = decoder.Decode(mapCo);
        var dAll = decoder.Decode(combined);

        double wCo = Sum(mapCo), wAll = Sum(combined);
        return new ContaminationResult
        {
            ReconCs = dCs.Reconstruction!,
            ReconContaminant = dCo.Reconstruction!,
            ReconCombined = dAll.Reconstruction!,
            OriginMm = dAll.ReconOriginMm,
            StepMm = dAll.ReconStepMm,
            ContaminationFraction = wAll > 0 ? wCo / wAll : 0.0,
            CsPos = csPos,
            CoPos = coPos,
        };
    }

    private static void Add(DetectorImage dst, DetectorImage src)
    {
        for (int y = 0; y < dst.Height; y++)
            for (int x = 0; x < dst.Width; x++)
                dst[x, y] += src[x, y];
    }

    private static double Sum(DetectorImage img)
    {
        double s = 0.0;
        foreach (var v in img.Raw) s += v;
        return s;
    }

    private static double Dist(SourceEstimate e, double tx, double ty)
        => Math.Sqrt((e.Position.X - tx) * (e.Position.X - tx) + (e.Position.Y - ty) * (e.Position.Y - ty));

    public static string StrategiesToCsv(StrategyRow[] rows)
    {
        var sb = new System.Text.StringBuilder();
        sb.AppendLine("strategy,efficiency_rel,bias_mm,rms_mm,fail_rate");
        foreach (var r in rows)
            sb.AppendLine($"{r.Strategy},{r.EfficiencyRel:F3},{r.BiasMm:F3},{r.RmsMm:F3},{r.FailRate:F3}");
        return sb.ToString();
    }

    public static string ReconToCsv(DetectorImage recon, double originMm, double stepMm)
    {
        var sb = new System.Text.StringBuilder();
        sb.AppendLine($"# origin_mm={originMm:F4} step_mm={stepMm:F4} width={recon.Width} height={recon.Height}");
        for (int y = 0; y < recon.Height; y++)
        {
            for (int x = 0; x < recon.Width; x++)
            {
                sb.Append(recon[x, y].ToString("F4"));
                if (x < recon.Width - 1) sb.Append(',');
            }
            sb.AppendLine();
        }
        return sb.ToString();
    }
}
