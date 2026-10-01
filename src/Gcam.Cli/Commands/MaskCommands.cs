using System.Text;
using Gcam.Configuration;
using Gcam.Core;
using Gcam.Detector;
using Gcam.Simulation;
using static Gcam.Cli.ConsoleRender;

namespace Gcam.Cli;

/// <summary>Mask channel geometry, size, taper, fabrication tolerance, tungsten secondaries, mask scatter and alignment.</summary>
internal static class MaskCommands
{
    internal static int RunMaskGeo(string[] args)
    {
        if (args.Length < 2) { Console.Error.WriteLine("Usage: montecarlo maskgeo <base.json>"); return 1; }
        var baseConfig = ConfigLoader.Load(args[1]);
        var study = new MaskGeometryStudy(new DefaultSimulationFactory());
        double baseS = baseConfig.Geometry.SourceMaskDistanceMm;   // nominal source distance (focal target)
        const double budget = 400.0; const int repeats = 200; const double failThr = 3.0;
        var csv = new StringBuilder("experiment,x,series,efficiency,rms_mm\n");

        // (A) hole size (straight channels, centered source): sensitivity vs shadow sharpness.
        Console.WriteLine("(A) Hole size (straight channels), centered source, resolution at fixed counts:");
        Console.WriteLine("  hole   efficiency   RMS@budget");
        foreach (double hf in new[] { 0.3, 0.5, 0.7, 0.85, 1.0 })
        {
            var cfg = baseConfig.Clone();
            cfg.Mask.FocalDistanceMm = 0.0; cfg.Mask.HoleFraction = hf;
            cfg.Source.Position = [0, 0, 0.0];
            var (eff, rms) = study.EfficiencyAndResolution(cfg, budget, repeats, failThr);
            Console.WriteLine($"  {hf,4:F2}   {eff,10:E2}   {rms,6:F2}mm");
            csv.Append($"holesize,{hf:F2},hole,{eff:E4},{rms:F3}\n");
        }
        Console.WriteLine();

        // (B) depth of field: efficiency vs source distance, straight vs focused (focal = nominal S).
        // Uses a THICK mask (collimation is what focusing removes; it is negligible at 10 mm).
        const double thickMm = 25.0;
        Console.WriteLine($"(B) Depth of field: efficiency vs source distance, straight vs focused (focal={baseS:F0}mm, {thickMm:F0}mm mask):");
        Console.WriteLine("  S(mm)   straight     focused    focused/straight");
        foreach (double s in new[] { 40.0, 60.0, 80.0, 100.0, 130.0, 170.0, 220.0 })
        {
            var cs = baseConfig.Clone(); cs.Geometry.SourceMaskDistanceMm = s; cs.Source.Position = [0, 0, 0.0];
            cs.Mask.ThicknessMm = thickMm; cs.Mask.FocalDistanceMm = 0.0;
            var cf = cs.Clone(); cf.Mask.FocalDistanceMm = baseS;
            double es = study.Efficiency(cs), ef = study.Efficiency(cf);
            Console.WriteLine($"  {s,4:F0}    {es,9:E2}   {ef,9:E2}    {(es > 0 ? ef / es : 0),6:F2}");
            csv.Append($"dof,{s:F0},straight,{es:E4},0\n");
            csv.Append($"dof,{s:F0},focused,{ef:E4},0\n");
        }
        Console.WriteLine();

        // (C) off-axis uniformity: center vs edge efficiency, straight vs focused.
        double fcfovHalf = baseConfig.Mask.Rank * baseConfig.Mask.CellPitchMm
                           * (baseConfig.Geometry.MaskDetectorDistanceMm + baseS) / baseConfig.Geometry.MaskDetectorDistanceMm / 2.0;
        double edge = 0.8 * fcfovHalf;
        Console.WriteLine($"(C) Off-axis uniformity: center vs edge (x={edge:F1}mm), source at S={baseS:F0}, {thickMm:F0}mm mask:");
        Console.WriteLine("  geometry   eff@center   eff@edge   edge/center");
        foreach (var (name, focal) in new[] { ("straight", 0.0), ("focused", baseS) })
        {
            var cc = baseConfig.Clone(); cc.Mask.ThicknessMm = thickMm; cc.Mask.FocalDistanceMm = focal; cc.Source.Position = [0, 0, 0.0];
            var ce = baseConfig.Clone(); ce.Mask.ThicknessMm = thickMm; ce.Mask.FocalDistanceMm = focal; ce.Source.Position = [edge, 0, 0.0];
            double effC = study.Efficiency(cc), effE = study.Efficiency(ce);
            Console.WriteLine($"  {name,-8}   {effC,10:E2}   {effE,8:E2}   {(effC > 0 ? effE / effC : 0),6:F2}");
            csv.Append($"offaxis,{name},center,{effC:E4},0\n");
            csv.Append($"offaxis,{name},edge,{effE:E4},0\n");
        }

        File.WriteAllText("samples/maskgeo.csv", csv.ToString());
        Console.WriteLine();
        Console.WriteLine("Smaller holes -> lower efficiency, sharper shadow. Focused channels peak at the focal");
        Console.WriteLine("distance (depth of field) and hold efficiency out to the FOV edge (uniform coding).");
        Console.WriteLine("CSV: samples/maskgeo.csv");
        return 0;
    }

    internal static int RunMaskSize(string[] args)
    {
        if (args.Length < 2) { Console.Error.WriteLine("Usage: montecarlo masksize <base.json>"); return 1; }
        var baseConfig = ConfigLoader.Load(args[1]);
        var study = new MaskGeometryStudy(new DefaultSimulationFactory());
        const double budget = 400.0; const int repeats = 250; const double failThr = 3.0;
        double d = baseConfig.Geometry.MaskDetectorDistanceMm, s = baseConfig.Geometry.SourceMaskDistanceMm;
        double detPitch = baseConfig.Detector.PixelPitchMm;
        var csv = new StringBuilder("cell_pitch_mm,shadow_per_pixel,resolution_mm,efficiency,rms_mm\n");

        Console.WriteLine("Optimal mask FEATURE (cell) size: rank/detector fixed, resolution at fixed counts");
        Console.WriteLine($"  D={d:F0} S={s:F0} mm, detector pitch {detPitch:F1} mm, {(int)budget} counts, centered source");
        Console.WriteLine("  cell    shadow/px   resolution   efficiency   RMS@budget");
        Console.WriteLine("  ----    ---------   ----------   ----------   ----------");
        foreach (double pitch in new[] { 0.4, 0.5, 0.6, 0.8, 1.0, 1.25, 1.5, 2.0, 3.0 })
        {
            var cfg = baseConfig.Clone();
            cfg.Mask.CellPitchMm = pitch;
            cfg.Source.Position = [0, 0, 0.0];
            double shadowPerPx = pitch * (d + s) / s / detPitch;    // mask-cell shadow width in detector pixels
            double resolution = pitch * (d + s) / d;                 // projected cell size at the source plane
            var (eff, rms) = study.EfficiencyAndResolution(cfg, budget, repeats, failThr);
            Console.WriteLine($"  {pitch,4:F2}mm  {shadowPerPx,7:F2}     {resolution,7:F2}mm   {eff,10:E2}   {rms,6:F2}mm");
            csv.Append($"{pitch:F2},{shadowPerPx:F3},{resolution:F3},{eff:E4},{rms:F3}\n");
        }
        File.WriteAllText("samples/masksize.csv", csv.ToString());
        Console.WriteLine();
        Console.WriteLine("Too coarse -> coarse resolution; too fine -> the cell shadow drops below a detector");
        Console.WriteLine("pixel (aliasing) and starves per cell. Optimum ~ shadow matches the detector pixel.");
        Console.WriteLine("CSV: samples/masksize.csv");
        return 0;
    }

    internal static int RunMaskTaper(string[] args)
    {
        if (args.Length < 2) { Console.Error.WriteLine("Usage: montecarlo masktaper <base.json>"); return 1; }
        var baseConfig = ConfigLoader.Load(args[1]);
        var study = new MaskGeometryStudy(new DefaultSimulationFactory());
        const double thickMm = 25.0;      // a THICK mask (where collimation bites); taper is pointless when thin
        const double budget = 400.0; const int repeats = 200; const double failThr = 3.0;
        double baseS = baseConfig.Geometry.SourceMaskDistanceMm, d = baseConfig.Geometry.MaskDetectorDistanceMm;
        double fcfovHalf = baseConfig.Mask.Rank * baseConfig.Mask.CellPitchMm * (d + baseS) / d / 2.0;
        double edge = 0.8 * fcfovHalf;
        // Sweep finely at low angles to catch the knee (a small bevel already de-collimates a 25mm slab).
        double[] tapers = [0.0, 1.0, 2.0, 3.0, 4.0, 5.0, 7.0, 10.0, 15.0, 25.0];

        Console.WriteLine($"Tapered (bevelled, hourglass) channels: wider FOV for a THICK ({thickMm:F0}mm) mask");
        Console.WriteLine($"  edge source at x={edge:F1}mm (0.8·FCFOV), {(int)budget} counts, {repeats} reps");
        Console.WriteLine("  A thick STRAIGHT mask collimates off-axis rays (edge/center < 1); bevelling the walls");
        Console.WriteLine("  should recover the edge efficiency while the mid-plane keeps the code sharp.");
        Console.WriteLine();
        Console.WriteLine("  taper   eff@center   eff@edge   edge/center   RMS@edge");
        Console.WriteLine("  -----   ----------   --------   -----------   --------");
        var rows = study.TaperSweep(baseConfig, thickMm, edge, tapers, budget, repeats, failThr);
        var csv = new System.Text.StringBuilder("taper_deg,eff_center,eff_edge,edge_ratio,rms_edge_mm\n");
        foreach (var r in rows)
        {
            Console.WriteLine($"  {r.TaperDeg,4:F0}°   {r.EffCenter,10:E2}   {r.EffEdge,8:E2}   {r.EdgeRatio,10:F2}    {r.RmsEdgeMm,6:F2}mm");
            csv.Append($"{r.TaperDeg:F1},{r.EffCenter:E4},{r.EffEdge:E4},{r.EdgeRatio:F4},{r.RmsEdgeMm:F3}\n");
        }
        File.WriteAllText("samples/masktaper.csv", csv.ToString());
        Console.WriteLine();
        Console.WriteLine("If edge/center climbs toward 1 with taper while RMS@edge stays low, the bevel is a real");
        Console.WriteLine("wide-FOV fix for thick masks (unlike focusing, theme 20, which NARROWS the FOV).");
        Console.WriteLine("CSV: samples/masktaper.csv");
        return 0;
    }

    internal static int RunMaskFab(string[] args)
    {
        if (args.Length < 2)
        {
            Console.Error.WriteLine("Usage: montecarlo maskfab <base.json> [out.csv]");
            Console.Error.WriteLine("  Sweeps tungsten mask fabrication tolerance (µm) vs an IDEAL decoder to find the");
            Console.Error.WriteLine("  usability threshold: hole placement/size jitter, depth drill wander, blocked cells.");
            return 1;
        }

        double[] sigmasUm = [0, 10, 20, 40, 80, 160];
        const double photonBudget = 800_000.0;
        const int repeats = 100;
        string csvPath = args.Length >= 3 ? args[2] : "samples/maskfab.csv";

        var baseConfig = ConfigLoader.Load(args[1]);
        double pitchUm = baseConfig.Mask.CellPitchMm * 1000.0;

        Console.WriteLine($"Mask fabrication tolerance study: a mis-machined tungsten mask vs the IDEAL MURA decoder.");
        Console.WriteLine($"Cell pitch {pitchUm:F0} µm, {baseConfig.Mask.ThicknessMm:F0} mm thick " +
                          $"(aspect ratio ≈ 1:{baseConfig.Mask.ThicknessMm / (0.71 * baseConfig.Mask.CellPitchMm):F0}); " +
                          $"σ drives placement, size (0.7σ), depth wander (2.5σ), blocked cells.");
        Console.WriteLine($"{repeats} Poisson reps/point, centered source; decoder assumes the perfect pattern.");
        Console.WriteLine();

        var rows = new MaskFabricationStudy(new DefaultSimulationFactory())
            .Run(baseConfig, sigmasUm, photonBudget, repeats);
        File.WriteAllText(csvPath, MaskFabricationStudy.ToCsv(rows));

        Console.WriteLine("   σ(µm)   pos   wander   blocked   eff(rel)   RMS(mm)   PSR");
        Console.WriteLine("   -----   ---   ------   -------   --------   -------   -----");
        double psr0 = rows[0].Psr, rms0 = rows[0].RmsMm;
        foreach (var r in rows)
            Console.WriteLine($"   {r.SigmaUm,5:F0}   {r.PosJitterUm,3:F0}   {r.WanderUm,6:F0}   {r.BlockedPct,6:F1}%   " +
                              $"{r.EfficiencyRel,8:P1}   {r.RmsMm,7:F2}   {r.Psr,5:F1}");

        Console.WriteLine();
        Console.WriteLine($"σ=0 baseline: RMS {rms0:F2} mm, PSR {psr0:F1}. As tolerance loosens the coded shadow blurs,");
        Console.WriteLine("the decode PSR falls and localization scatters — the usability threshold is where RMS / PSR");
        Console.WriteLine("leave the ideal-mask floor (the manufacturing requirement a real tungsten mask must hold).");
        Console.WriteLine($"CSV written: {csvPath}");
        return 0;
    }

    internal static int RunMaskSecondary(string[] args)
    {
        if (args.Length < 2)
        {
            Console.Error.WriteLine("Usage: montecarlo masksec <base.json> [out.csv]");
            Console.Error.WriteLine("  Mask tungsten SECONDARIES a pure-attenuation mask omits: Compton scatter +");
            Console.Error.WriteLine("  W K-fluorescence (59/67 keV), transported out of the slab to the detector.");
            return 1;
        }

        string csvPath = args.Length >= 3 ? args[2] : "samples/masksec.csv";
        const int samples = 4_000_000;
        const int bins = 350;
        const double maxE = 700.0;

        var baseConfig = ConfigLoader.Load(args[1]);
        double primary = baseConfig.Source.EnergyKeV > 0 ? baseConfig.Source.EnergyKeV : 661.7;

        var result = new MaskSecondaryStudy().Run(baseConfig, primary, samples, bins, maxE);
        File.WriteAllText(csvPath, MaskSecondaryStudy.ToCsv(result));

        // A Compton scatter reaching the detector must head DOWN (forward), so its arriving photon energy runs from a
        // 90° scatter (minimum forward-detectable) up to ~the primary (small-angle forward scatter).
        double alpha = primary / 510.999;
        double e90 = primary / (1.0 + alpha);   // scattered-photon energy at 90°

        Console.WriteLine($"Mask tungsten secondary study: {primary:F0} keV primary on a {baseConfig.Mask.ThicknessMm:F0} mm " +
                          $"W mask (open fraction {result.OpenFraction:P0}), {samples:N0} samples.");
        Console.WriteLine($"W photoelectric fraction @{primary:F0} = {MaskSecondary.PhotoFraction(primary):P1} " +
                          $"(rest Compton); K-edge {MaskSecondary.KEdgeKeV:F1}, Kα {MaskSecondary.KaKeV:F0}/Kβ {MaskSecondary.KbKeV:F0} keV.");
        Console.WriteLine();
        Console.WriteLine($"  Escaped secondaries reaching the detector: {result.SecondaryPerPrimaryPct:F2}% of the " +
                          $"open-cell (coded) primary flux.");
        Console.WriteLine($"  Of those, {result.FluorPct:F1}% are K-fluorescence X-rays (heavily self-absorbed, front-weighted");
        Console.WriteLine($"  → almost none reach the detector), the rest FORWARD Compton scatter.");
        Console.WriteLine($"  Arriving scatter energy ≈ {e90:F0}–{primary:F0} keV (backscatter heads away): the small-angle");
        Console.WriteLine($"  forward tail near {primary:F0} keV sits INSIDE the photopeak window, so the energy window can NOT");
        Console.WriteLine($"  reject it — a mildly mis-positioned imaging background; the lower-energy scatter IS rejected.");
        Console.WriteLine();
        Console.WriteLine("A pure-attenuation mask misses this entirely.");
        Console.WriteLine($"CSV written: {csvPath}");
        return 0;
    }

    internal static int RunMaskScatter(string[] args)
    {
        if (args.Length < 2)
        {
            Console.Error.WriteLine("Usage: montecarlo maskscatter <base.json> [out.csv]");
            Console.Error.WriteLine("  Folds mask forward-scatter into the coded image and measures the imaging impact vs");
            Console.Error.WriteLine("  the mask-detector gap: contamination %, decoder contrast, and photopeak-window recovery.");
            return 1;
        }

        string csvPath = args.Length >= 3 ? args[2] : "samples/maskscatter.csv";
        var baseCfg = ConfigLoader.Load(args[1]);
        double primaryE = baseCfg.Source.EnergyKeV > 0 ? baseCfg.Source.EnergyKeV : 661.7;
        double winLo = primaryE * 0.89, winHi = primaryE * 1.10;   // ~±10% photopeak window
        double[] gaps = [12, 18, 25, 35, 48, 65];
        const long photons = 4_000_000;
        var study = new MaskScatterStudy();

        var sb = new System.Text.StringBuilder();
        sb.AppendLine("gap_mm,contam_all_pct,contam_window_pct,conf_primary,conf_scatter,conf_window,bias_primary_mm,bias_scatter_mm");
        double[]? spec = null; double specBin = 0, specMax = 0;
        var summary = new List<(double gap, MaskScatterRow[] rows)>();
        foreach (double gap in gaps)
        {
            var cfg = baseCfg.Clone();
            cfg.Geometry.MaskDetectorDistanceMm = gap;
            // Fixed modest off-axis source (mm) — inside the FCFOV at every gap so the decode stays valid.
            var (rows, sp, bin, max) = study.Run(cfg, 5.0, 0.0, photons, winLo, winHi, cfg.Seed);
            summary.Add((gap, rows));
            if (Math.Abs(gap - 18) < 1e-9) { spec = sp; specBin = bin; specMax = max; }
            sb.AppendLine($"{gap:F0},{rows[1].ContaminationPct:F3},{rows[2].ContaminationPct:F3}," +
                          $"{rows[0].Confidence:F4},{rows[1].Confidence:F4},{rows[2].Confidence:F4}," +
                          $"{rows[0].LocalizationBiasMm:F3},{rows[1].LocalizationBiasMm:F3}");
        }
        File.WriteAllText(csvPath, sb.ToString());
        if (spec != null)
        {
            var ss = new System.Text.StringBuilder();
            ss.AppendLine("e_keV,scatter_weight");
            for (int i = 0; i < spec.Length; i++) ss.AppendLine($"{(i + 0.5) * specBin:F1},{spec[i]:F2}");
            File.WriteAllText(csvPath.Replace(".csv", "_spectrum.csv"), ss.ToString());
        }

        Console.WriteLine($"Mask forward-scatter study: {primaryE:F0} keV primary, {photons:N0} photons/gap, window {winLo:F0}-{winHi:F0} keV.");
        Console.WriteLine("A primary hitting a CLOSED cell can Compton-scatter forward to the detector — a coded-image pedestal.");
        Console.WriteLine();
        Console.WriteLine("   gap   contam   +window   conf(prim→scat)   (scatter as % of coded primary)");
        Console.WriteLine("   ---   ------   -------   ---------------");
        foreach (var (gap, rows) in summary)
            Console.WriteLine($"   {gap,3:F0}   {rows[1].ContaminationPct,5:F2}%   {rows[2].ContaminationPct,5:F2}%   " +
                              $"{rows[0].Confidence,6:F3}→{rows[1].Confidence,-6:F3}");
        Console.WriteLine();
        Console.WriteLine("Contamination rises as the gap SHRINKS (a wide mask-detector gap drifts wide-angle scatter off the");
        Console.WriteLine("small detector). It stays small and the balanced MURA decoder rejects the smooth pedestal (contrast");
        Console.WriteLine("barely moves); the photopeak window removes the down-shifted part, leaving the small-angle forward tail.");
        Console.WriteLine($"CSV written: {csvPath} (+ _spectrum.csv: the arriving-scatter energy spectrum at 18 mm)");
        return 0;
    }

    internal static int RunAlign(string[] args)
    {
        if (args.Length < 2)
        {
            Console.Error.WriteLine("Usage: montecarlo align <base.json> [out.csv]");
            Console.Error.WriteLine("  Sweeps mask-detector misalignment (in-plane offset, spacing, roll) vs an IDEAL");
            Console.Error.WriteLine("  decoder and reports the systematic localization bias — the alignment tolerance.");
            return 1;
        }

        string csvPath = args.Length >= 3 ? args[2] : "samples/align.csv";
        const double photonBudget = 800_000.0;
        const int repeats = 120;
        double[] offsets = [0.0, 0.1, 0.2, 0.5, 1.0];    // in-plane mask offset (mm)
        double[] zOffsets = [0.0, 0.25, 0.5, 1.0, 2.0];  // spacing error (mm)
        double[] rolls = [0.0, 0.25, 0.5, 1.0, 2.0];     // roll about the optical axis (deg)

        var baseConfig = ConfigLoader.Load(args[1]);
        // Near the FCFOV edge so the roll / spacing leverage (which grows with off-axis radius) shows clearly.
        double fcfovHalf = baseConfig.Mask.Rank * baseConfig.Mask.CellPitchMm
            * (baseConfig.Geometry.MaskDetectorDistanceMm + baseConfig.Geometry.SourceMaskDistanceMm)
            / baseConfig.Geometry.MaskDetectorDistanceMm / 2.0;
        double srcX = 0.85 * fcfovHalf;
        var src = (srcX, 0.0, 0.0);

        Console.WriteLine("Mask-detector alignment study: a mis-registered mask vs the IDEAL-geometry decoder.");
        Console.WriteLine($"Off-axis source at x={srcX:F0} mm, D={baseConfig.Geometry.MaskDetectorDistanceMm:F0} / " +
                          $"S={baseConfig.Geometry.SourceMaskDistanceMm:F0} mm, {repeats} Poisson reps/point.");
        Console.WriteLine("Bias = systematic (decoded − true); RMS includes scatter. Decoder assumes perfect alignment.");
        Console.WriteLine();

        var rows = new AlignmentStudy(new DefaultSimulationFactory())
            .Run(baseConfig, src, offsets, zOffsets, rolls, photonBudget, repeats);
        File.WriteAllText(csvPath, AlignmentStudy.ToCsv(rows));

        string lastDof = "";
        foreach (var r in rows)
        {
            if (r.Dof != lastDof)
            {
                lastDof = r.Dof;
                string unit = r.Dof == "roll_deg" ? "deg" : "mm";
                Console.WriteLine();
                Console.WriteLine($"  {r.Dof} ({unit})   biasX    biasY     bias     RMS");
                Console.WriteLine("  ---------------   ------   ------   ------   ------");
            }
            Console.WriteLine($"  {r.Magnitude,13:F2}   {r.BiasXMm,6:F2}   {r.BiasYMm,6:F2}   {r.BiasMm,6:F2}   {r.RmsMm,6:F2}");
        }

        Console.WriteLine();
        Console.WriteLine("In-plane offset biases the position almost 1:1; spacing (magnification) and roll bias grow");
        Console.WriteLine("with off-axis distance. The tolerance is where the bias leaves the decoder floor (~sub-mm).");
        Console.WriteLine($"CSV written: {csvPath}");
        return 0;
    }
}
