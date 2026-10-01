using System.Text;
using Gcam.Configuration;
using Gcam.Core;
using Gcam.Detector;
using Gcam.Simulation;
using static Gcam.Cli.ConsoleRender;

namespace Gcam.Cli;

/// <summary>Reconstruction: MLEM, sub-cell peak interpolation, mask/antimask.</summary>
internal static class DecodingCommands
{
    internal static int RunMlem(string[] args)
    {
        if (args.Length < 2)
        {
            Console.Error.WriteLine("Usage: montecarlo mlem <base.json> [out.csv]");
            Console.Error.WriteLine("  MLEM (Poisson-likelihood) reconstruction vs cross-correlation: two-source resolving");
            Console.Error.WriteLine("  power, non-negativity, and peak sharpness on the same coded floods.");
            return 1;
        }

        string csvPath = args.Length >= 3 ? args[2] : "samples/mlem.csv";
        var cfg = ConfigLoader.Load(args[1]);
        double[] seps = [1, 1.5, 2, 2.5, 3, 3.5, 4, 5, 6];
        const int iters = 80;

        var (rows, single, cp, mp, pstep, porigin) = new MlemStudy().Run(cfg, seps, photonCount: 1_500_000,
            mlemIterations: iters, seed: cfg.Seed, profileSeparationMm: 3.0);
        File.WriteAllText(csvPath, MlemStudy.ToCsv(rows));
        var sb = new System.Text.StringBuilder();
        sb.AppendLine("x_mm,cross,mlem");
        for (int i = 0; i < cp.Length; i++)
            sb.AppendLine($"{porigin + i * pstep:F2},{cp[i]:F5},{(i < mp.Length ? mp[i] : 0):F5}");
        File.WriteAllText(csvPath.Replace(".csv", "_profile.csv"), sb.ToString());

        double? CrossRes() => rows.Where(r => r.CrossResolved).Select(r => (double?)r.SeparationMm).FirstOrDefault();
        double? MlemRes() => rows.Where(r => r.MlemResolved).Select(r => (double?)r.SeparationMm).FirstOrDefault();

        Console.WriteLine($"MLEM vs cross-correlation ({iters} iterations, 1.5M photons).");
        Console.WriteLine();
        Console.WriteLine($"  Single source: bias  cross {single.CrossBiasMm:F2} / MLEM {single.MlemBiasMm:F2} mm;  " +
                          $"min value  cross {single.CrossMinValue:F1} (negative sidelobes) / MLEM {single.MlemMinValue:F2} (≥0);  " +
                          $"peak FWHM  cross {single.CrossPeakFwhmMm:F2} / MLEM {single.MlemPeakFwhmMm:F2} mm.");
        Console.WriteLine();
        Console.WriteLine("   separation   cross valley   MLEM valley   (resolved if > 0.25)");
        Console.WriteLine("   ----------   ------------   -----------");
        foreach (var r in rows)
            Console.WriteLine($"   {r.SeparationMm,7:F1}      {r.CrossValleyDepth,8:F3}{(r.CrossResolved ? " Y" : "  ")}    " +
                              $"{r.MlemValleyDepth,8:F3}{(r.MlemResolved ? " Y" : "  ")}");
        Console.WriteLine();
        Console.WriteLine($"Minimum resolvable separation:  cross-correlation {CrossRes(),0:F1} mm  vs  MLEM {MlemRes(),0:F1} mm.");
        Console.WriteLine("MLEM deconvolves the physical forward model to a non-negative distribution, so it separates pairs");
        Console.WriteLine("cross-correlation merges (and has no negative sidelobes). Cost: iteration + a resolution/noise trade-off.");
        Console.WriteLine("(Ideal high-count binary-aperture demo; the valley metric is truth-centred so ~1.5 mm is optimistic —");
        Console.WriteLine(" the robust split is 2-3 mm. The ghost-suppression benefit needs the finite forward model, Cyclic=false.)");
        Console.WriteLine($"CSV written: {csvPath} (+ _profile.csv: the 3 mm-separation reconstruction profiles)");
        return 0;
    }

    internal static int RunSubCell(string[] args)
    {
        if (args.Length < 2)
        {
            Console.Error.WriteLine("Usage: montecarlo subcell <base.json> [out.csv]");
            Console.Error.WriteLine("  Sweeps a point source across the recon grid in sub-cell steps and compares the");
            Console.Error.WriteLine("  localization error with NO interpolation (argmax) vs parabolic / tent / Gaussian");
            Console.Error.WriteLine("  peak interpolation, across a range of recon-grid steps.");
            return 1;
        }

        string csvPath = args.Length >= 3 ? args[2] : "samples/subcell.csv";
        double[] steps = [0.6, 0.9, 1.2, 1.8, 2.4];
        const int samples = 41;
        const int photons = 800_000;

        var baseConfig = ConfigLoader.Load(args[1]);
        var study = new SubCellStudy(new DefaultSimulationFactory());

        var sb = new System.Text.StringBuilder();
        sb.AppendLine("step_mm,floor_mm,rms_none,rms_parab,rms_tent,rms_gauss,mae_none,mae_parab,mae_tent,mae_gauss");
        var summaries = new List<SubCellSummary>();
        SubCellRow[]? detailRows = null;
        foreach (double step in steps)
        {
            var (rows, s) = study.Run(baseConfig, step, sweepHalfWidthMm: 1.25 * step, samples: samples,
                                      sourceYMm: 0.0, photonCount: photons);
            summaries.Add(s);
            if (Math.Abs(step - 1.2) < 1e-9) detailRows = rows;   // keep one full sawtooth trace for plotting
            sb.AppendLine($"{step:F2},{s.QuantFloorMm:F4},{s.RmsNoneMm:F4},{s.RmsParabolicMm:F4},{s.RmsTentMm:F4}," +
                          $"{s.RmsGaussianMm:F4},{s.MaeNoneMm:F4},{s.MaeParabolicMm:F4},{s.MaeTentMm:F4},{s.MaeGaussianMm:F4}");
        }
        File.WriteAllText(csvPath, sb.ToString());
        if (detailRows is not null)
            File.WriteAllText(csvPath.Replace(".csv", "_trace.csv"), SubCellStudy.ToCsv(detailRows));

        Console.WriteLine("Sub-cell peak interpolation: localization RMS vs recon-grid step.");
        Console.WriteLine("The bare argmax quantizes the estimate to the step (RMS ≈ step/√12); interpolating the");
        Console.WriteLine("correlation-peak shape recovers a fractional offset and beats that floor.");
        Console.WriteLine();
        Console.WriteLine("   step   floor    none   parab    tent   gauss   (RMS mm; best in **)");
        Console.WriteLine("   ----   -----   -----   -----   -----   -----");
        foreach (var s in summaries)
        {
            double best = Math.Min(Math.Min(s.RmsParabolicMm, s.RmsTentMm), s.RmsGaussianMm);
            string Mark(double v) => Math.Abs(v - best) < 1e-9 ? $"*{v:F3}*" : $" {v:F3} ";
            Console.WriteLine($"   {s.ReconStepMm,4:F1}   {s.QuantFloorMm,5:F3}   {s.RmsNoneMm,5:F3}   " +
                              $"{Mark(s.RmsParabolicMm)}  {Mark(s.RmsTentMm)}  {Mark(s.RmsGaussianMm)}");
        }
        Console.WriteLine();
        Console.WriteLine("Interpolation beats the argmax at every step; the gain grows as the grid coarsens (where the");
        Console.WriteLine("quantization floor is largest). Tent is the matched model for the MURA autocorrelation core and");
        Console.WriteLine("the most robust across step sizes; parabolic can edge it at intermediate steps but degrades when");
        Console.WriteLine("the grid nears the projected cell size. Default: tent.");
        Console.WriteLine($"CSV written: {csvPath} (+ _trace.csv: the per-position sawtooth at step 1.2 mm)");
        return 0;
    }

    internal static int RunAntimask(string[] args)
    {
        if (args.Length < 2)
        {
            Console.Error.WriteLine("Usage: montecarlo antimask <base.json> [out.csv]");
            return 1;
        }

        const double nSrc = 400.0;          // detected source counts
        const int repeats = 200;
        const double failThresholdMm = 3.0;
        string csvPath = args.Length >= 3 ? args[2] : "samples/antimask.csv";

        var baseConfig = ConfigLoader.Load(args[1]);
        var scenarios = new (string, double, double)[]
        {
            ("bg=0 (ideal)",  0.0,  0.0),
            ("bg=0.5/px",     0.5,  0.0),
            ("bg=1/px",       1.0,  0.0),
            ("bg=2/px",       2.0,  0.0),
            ("bg=4/px",       4.0,  0.0),
            ("bg=8/px",       8.0,  0.0),
            ("gradient ~4",   4.0,  1.0),
        };

        Console.WriteLine($"Mask vs mask/antimask: {nSrc:F0} source counts, {repeats} reps, additive background scenarios");
        Console.WriteLine("Single mask = full budget on A. Mask/antimask = half each on A and inverted-A, decode the difference.");
        Console.WriteLine();

        var rows = new MaskAntimaskStudy(new DefaultSimulationFactory())
            .Run(baseConfig, nSrc, repeats, failThresholdMm, scenarios);
        File.WriteAllText(csvPath, MaskAntimaskStudy.ToCsv(rows));

        Console.WriteLine("  scenario     (a) flip-only   (b) flip+calib   (c) 2-exposure   | fail: a / b / c");
        Console.WriteLine("  -----------  -------------   --------------   --------------   ------------------");
        foreach (var r in rows)
            Console.WriteLine($"  {r.Scenario,-11}  {r.SingleErrMm,9:F2}mm   {r.CalibErrMm,10:F2}mm   {r.AntimaskErrMm,10:F2}mm   {r.SingleFail,4:P0} / {r.CalibFail,3:P0} / {r.AntimaskFail,3:P0}");

        Console.WriteLine();
        Console.WriteLine($"CSV written: {csvPath}");
        return 0;
    }

    internal static int RunAntimaskScene(string[] args)
    {
        if (args.Length < 2) { Console.Error.WriteLine("Usage: montecarlo antimask-scene <base.json>"); return 1; }
        var baseConfig = ConfigLoader.Load(args[1]);
        var factory = new DefaultSimulationFactory();
        double[] primary = [0.0, 0.0, 0.0];
        double[] bgSource = [6.0, 4.0, 0.0];   // a second directional source in the field

        DetectorImage Mean(double[] pos, bool invert)
        {
            var cfg = baseConfig.Clone();
            cfg.Mask.Invert = invert;
            cfg.Source.Position = pos;
            return new SimulationRunner(factory).Run(cfg).DetectorImage;
        }
        DetectorImage Combine(DetectorImage a, DetectorImage b, double sign)
        {
            var r = new DetectorImage(a.Width, a.Height);
            for (int y = 0; y < a.Height; y++)
                for (int x = 0; x < a.Width; x++)
                    r[x, y] = a[x, y] + sign * b[x, y];
            return r;
        }

        var sA = Mean(primary, false);
        var sB = Mean(primary, true);
        var bA = Mean(bgSource, false);
        var bB = Mean(bgSource, true);
        var decoder = factory.CreateDecoder(baseConfig.Clone())!;

        // Diffuse background cancels in the difference -> antimask of source alone: one peak.
        var diffuse = Combine(sA, sB, -1.0);
        // A directional background source is coded like the source -> survives: two peaks.
        var directional = Combine(Combine(sA, bA, 1.0), Combine(sB, bB, 1.0), -1.0);

        void Dump(string path, DecodeResult d)
        {
            var sb = new StringBuilder("x_mm,y_mm,value\n");
            var img = d.Reconstruction;
            for (int gy = 0; gy < img.Height; gy++)
                for (int gx = 0; gx < img.Width; gx++)
                    sb.Append($"{d.ReconOriginMm + gx * d.ReconStepMm:F2},{d.ReconOriginMm + gy * d.ReconStepMm:F2},{img[gx, gy]:F4}\n");
            File.WriteAllText(path, sb.ToString());
        }

        var rDiffuse = decoder.Decode(diffuse);
        var rDirect = decoder.Decode(directional);
        Dump("samples/antimask_diffuse.csv", rDiffuse);
        Dump("samples/antimask_directional.csv", rDirect);

        Console.WriteLine("Antimask reconstruction scenes:");
        Console.WriteLine($"  diffuse background   -> peak at {rDiffuse.Estimate.Position} (background cancels: one peak)");
        Console.WriteLine($"  directional bg source-> peak at {rDirect.Estimate.Position} (both source + bg imaged: two peaks)");
        Console.WriteLine("  true: primary (0,0), background source (6,4)");
        Console.WriteLine("CSVs: samples/antimask_diffuse.csv, samples/antimask_directional.csv");
        return 0;
    }
}
