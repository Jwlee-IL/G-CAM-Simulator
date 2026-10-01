using System.Text;
using Gcam.Configuration;
using Gcam.Core;
using Gcam.Detector;
using Gcam.Simulation;
using static Gcam.Cli.ConsoleRender;

namespace Gcam.Cli;

/// <summary>Depth (z) estimation and depth-of-interaction parallax.</summary>
internal static class DepthCommands
{
    internal static int RunDepth(string[] args)
    {
        if (args.Length < 2) { Console.Error.WriteLine("Usage: montecarlo depth <base.json>"); return 1; }
        var baseConfig = ConfigLoader.Load(args[1]);

        double[] trueDistances = [40, 60, 100, 150, 200];
        var assumed = new List<double>();
        for (double s = 20; s <= 260; s += 5) assumed.Add(s);

        Console.WriteLine("Source-distance (z) estimation by coded-aperture refocusing");
        Console.WriteLine($"Mask D = {baseConfig.Geometry.MaskDetectorDistanceMm} mm; sweeping assumed S over [{assumed[0]}, {assumed[^1]}] mm");
        Console.WriteLine("On-axis source; focus metric = peak correlation of the reconstruction.");
        Console.WriteLine();

        var study = new DepthStudy(new DefaultSimulationFactory());
        var results = trueDistances.Select(s => study.Run(baseConfig, s, assumed.ToArray())).ToArray();
        File.WriteAllText("samples/depth.csv", DepthStudy.ToCsv(results));

        Console.WriteLine("  true S    estimated S   error    (magnification M = (D+S)/S)");
        Console.WriteLine("  ------   -----------   ------   ---------------------------");
        foreach (var r in results)
        {
            double m = (baseConfig.Geometry.MaskDetectorDistanceMm + r.TrueSmm) / r.TrueSmm;
            Console.WriteLine($"  {r.TrueSmm,4:F0}mm      {r.EstimatedSmm,6:F1}mm   {r.ErrorMm,+5:F1}mm   M = {m:F2}");
        }
        Console.WriteLine();
        Console.WriteLine("Depth resolution degrades with distance (dM/dS shrinks) — the coded-aperture limit.");
        Console.WriteLine("CSV: samples/depth.csv");
        return 0;
    }

    internal static int RunDepthJoint(string[] args)
    {
        if (args.Length < 2) { Console.Error.WriteLine("Usage: montecarlo depth-joint <base.json>"); return 1; }
        var baseConfig = ConfigLoader.Load(args[1]);

        var assumed = new List<double>();
        for (double s = 20; s <= 260; s += 5) assumed.Add(s);
        const double nominalS = 100.0;
        const int repeats = 60;
        double[] counts = [100, 300, 1000, 3000];

        Console.WriteLine("Depth (and joint lateral+depth) estimation under Poisson noise");
        Console.WriteLine($"Assumed S sweep [{assumed[0]},{assumed[^1]}] mm, nominal {nominalS}, {repeats} realizations/point");
        Console.WriteLine();

        var study = new DepthStudy(new DefaultSimulationFactory());
        var all = new List<JointResult>();
        Console.WriteLine("  scenario        true(x,y,S)        counts   depthBias  depthRMS  latRMS");
        Console.WriteLine("  -------------   ---------------   ------   ---------  --------  ------");

        // Part A: depth-only under noise (known on-axis), near vs far.
        // Part B: joint (x,y,z) for an off-axis source.
        var depthOnly = new (string name, double s)[] { ("onaxis_near", 60.0), ("onaxis_far", 150.0) };
        var joint = new (string name, double x, double y, double s)[] { ("offaxis_near", 5.0, 0.0, 60.0), ("offaxis_far", 5.0, 0.0, 150.0) };

        foreach (var sc in depthOnly)
        {
            foreach (double n in counts)
            {
                var r = study.RunNoisyDepth(baseConfig, sc.name, sc.s, assumed.ToArray(), n, repeats);
                all.Add(r);
                Console.WriteLine($"  {sc.name,-13}   (  0,  0,{sc.s,4:F0})mm     {n,5:F0}    {r.DepthBiasMm,6:F1}mm   {r.DepthRmsMm,5:F1}mm  {r.LateralRmsMm,5:F2}mm");
            }
            Console.WriteLine();
        }
        foreach (var sc in joint)
        {
            foreach (double n in counts)
            {
                var r = study.RunNoisyJoint(baseConfig, sc.name, sc.x, sc.y, sc.s, assumed.ToArray(), nominalS, n, repeats);
                all.Add(r);
                Console.WriteLine($"  {sc.name,-13}   ({sc.x,3:F0},{sc.y,3:F0},{sc.s,4:F0})mm     {n,5:F0}    {r.DepthBiasMm,6:F1}mm   {r.DepthRmsMm,5:F1}mm  {r.LateralRmsMm,5:F2}mm");
            }
            Console.WriteLine();
        }
        File.WriteAllText("samples/depth_joint.csv", DepthStudy.JointToCsv(all.ToArray()));
        Console.WriteLine("Depth RMS blows up in the far field (broad focus curve) and at low counts;");
        Console.WriteLine("lateral RMS stays small (the coded aperture localizes x,y well even when z is uncertain).");
        Console.WriteLine("CSV: samples/depth_joint.csv");
        return 0;
    }

    internal static int RunDepth3D(string[] args)
    {
        if (args.Length < 2) { Console.Error.WriteLine("Usage: montecarlo depth3d <base.json>"); return 1; }
        var baseConfig = ConfigLoader.Load(args[1]);

        var assumed = new List<double>();
        for (double s = 20; s <= 260; s += 5) assumed.Add(s);
        const double nominalS = 100.0;      // the alternating iteration's seed (a source of its trap)
        const int repeats = 60;
        double[] counts = [100, 300, 1000, 3000];

        Console.WriteLine("Joint (x,y,S) depth estimation: alternating iteration vs FULL 3D search");
        Console.WriteLine($"Assumed S [{assumed[0]},{assumed[^1]}] mm, {repeats} realizations/point, off-axis sources");
        Console.WriteLine("The 3D search scores each S-slice by its peak prominence (cross-S comparable) and takes the");
        Console.WriteLine("joint argmax — no nominal-S seed, no lateral<->depth alternation trap.");
        Console.WriteLine();

        var study = new DepthStudy(new DefaultSimulationFactory());
        var scenes = new (string name, double x, double y, double s)[]
        {
            ("offaxis_near", 5.0, 0.0, 60.0),
            ("offaxis_far",  5.0, 0.0, 150.0),
        };
        var all = new List<JointResult>();
        var csv = new System.Text.StringBuilder("method,scenario,true_x_mm,true_y_mm,true_s_mm,counts,depth_bias_mm,depth_rms_mm,lateral_rms_mm\n");

        Console.WriteLine("  scenario        counts   method             depthBias  depthRMS  latRMS");
        Console.WriteLine("  -------------   ------   ----------------   ---------  --------  ------");
        foreach (var sc in scenes)
        {
            foreach (double n in counts)
            {
                // Fair comparison: alternating with the FIXED seed (100), alternating with an ORACLE seed
                // (= true S, best case), and the seed-free 3D search. If alternating still stalls even with
                // the oracle seed, the near-field win is the estimator, not just a bad seed.
                var it = study.RunNoisyJoint(baseConfig, sc.name, sc.x, sc.y, sc.s, assumed.ToArray(), nominalS, n, repeats);
                var ito = study.RunNoisyJoint(baseConfig, sc.name, sc.x, sc.y, sc.s, assumed.ToArray(), sc.s, n, repeats);
                var d3 = study.RunNoisyJoint3D(baseConfig, sc.name, sc.x, sc.y, sc.s, assumed.ToArray(), n, repeats);
                all.Add(it); all.Add(d3);
                Console.WriteLine($"  {sc.name,-13}   {n,5:F0}   alt (seed 100)     {it.DepthBiasMm,6:F1}mm   {it.DepthRmsMm,5:F1}mm  {it.LateralRmsMm,5:F2}mm");
                Console.WriteLine($"  {sc.name,-13}   {n,5:F0}   alt (oracle seed)  {ito.DepthBiasMm,6:F1}mm   {ito.DepthRmsMm,5:F1}mm  {ito.LateralRmsMm,5:F2}mm");
                Console.WriteLine($"  {sc.name,-13}   {n,5:F0}   3D search          {d3.DepthBiasMm,6:F1}mm   {d3.DepthRmsMm,5:F1}mm  {d3.LateralRmsMm,5:F2}mm");
                csv.Append($"alternating,{sc.name},{sc.x:F1},{sc.y:F1},{sc.s:F1},{n:F0},{it.DepthBiasMm:F2},{it.DepthRmsMm:F2},{it.LateralRmsMm:F3}\n");
                csv.Append($"alt_oracle,{sc.name},{sc.x:F1},{sc.y:F1},{sc.s:F1},{n:F0},{ito.DepthBiasMm:F2},{ito.DepthRmsMm:F2},{ito.LateralRmsMm:F3}\n");
                csv.Append($"3d,{sc.name},{sc.x:F1},{sc.y:F1},{sc.s:F1},{n:F0},{d3.DepthBiasMm:F2},{d3.DepthRmsMm:F2},{d3.LateralRmsMm:F3}\n");
            }
            Console.WriteLine();
        }
        File.WriteAllText("samples/depth3d.csv", csv.ToString());
        Console.WriteLine("If 'alt (oracle seed)' still stalls near-field but the 3D search converges, the win is the");
        Console.WriteLine("seed-free estimator, not just the seed. CSV: samples/depth3d.csv");
        return 0;
    }

    internal static int RunDepthDesign(string[] args)
    {
        // How far can this coded aperture tell a source's DEPTH — and how big a mask a chosen "safe standoff"
        // distance would need. Depth resolution (FWHM of the sharpness-vs-focal curve) grows as C·(z-D)²/(A·D);
        // we MEASURE it across distance, fit C, then invert for the required aperture.
        var baseConfig = args.Length >= 2 ? ConfigLoader.Load(args[1]) : new SimulationConfig();
        // Default to the app's "Sharp" optics if the caller didn't set them.
        if (args.Length < 2)
        {
            baseConfig.Mask.Rank = 13; baseConfig.Mask.CellPitchMm = 0.7; baseConfig.Mask.MosaicX = baseConfig.Mask.MosaicY = 2;
            baseConfig.Geometry.MaskDetectorDistanceMm = 80;
            baseConfig.Detector.PixelsX = baseConfig.Detector.PixelsY = 30; baseConfig.Detector.PixelPitchMm = 0.6;
        }
        baseConfig.PhotonCount = 2_000_000;

        var factory = new DefaultSimulationFactory();
        double d = baseConfig.Geometry.MaskDetectorDistanceMm;
        double[] distances = [150, 250, 400, 600, 900, 1400];
        var (rows, _, aperture, _) = DepthDesignStudy.Sweep(baseConfig, factory, distances);

        Console.WriteLine("Depth-of-field design study (coded-aperture 3D range vs mask size)");
        Console.WriteLine($"  Optics: rank {baseConfig.Mask.Rank}, cell {baseConfig.Mask.CellPitchMm} mm, " +
                          $"mask aperture A = {aperture:F0} mm, mask-detector D = {d:F0} mm");
        Console.WriteLine($"  Detector {baseConfig.Detector.PixelsX}x{baseConfig.Detector.PixelsY} @ {baseConfig.Detector.PixelPitchMm} mm");
        Console.WriteLine();
        Console.WriteLine("  distance    depth FWHM    FWHM / distance   (FWHM = depth-of-field; the estimate can");
        Console.WriteLine("  --------    ---------    ---------------    localize finer than this with more counts)");
        foreach (var r in rows)
            Console.WriteLine($"  {r.DistanceMm,5:F0} mm    {r.DepthFwhmMm,6:F0} mm      {r.DepthFwhmMm / r.DistanceMm,5:P0}");
        Console.WriteLine();

        // Read the effective 3D range straight off the measured curve (FWHM grows ~z^1.5, not z², so no C-fit).
        double range10 = DepthDesignStudy.EffectiveRangeMm(rows, 0.20);   // ±10% == FWHM 20% of distance
        double range20 = DepthDesignStudy.EffectiveRangeMm(rows, 0.40);   // ±20%
        Console.WriteLine($"  Effective 3D depth range (this mask):  ±10% out to ~{range10 / 1000.0:F2} m," +
                          $"  ±20% out to ~{range20 / 1000.0:F2} m.");
        Console.WriteLine();

        // How to EXTEND that range (design guidance — measured, not a shaky formula):
        Console.WriteLine("  To reach farther in 3D, enlarge the mask APERTURE as MORE CELLS (higher rank) at a fixed");
        Console.WriteLine("  cell pitch — and/or increase the mask–detector gap D. A coarser pitch does NOT help: it");
        Console.WriteLine("  grows the aperture but worsens the lateral resolution in step, and the two cancel");
        Console.WriteLine("  (Δz ∝ z²·σ_lat/(A·D), σ_lat ∝ cell, A ∝ cell). Measured: rank 13→29 (A 18→41 mm, cell");
        Console.WriteLine("  fixed) extends the ±10% range 0.15→0.23 m and the ±20% range 0.20→0.50 m — sub-linear,");
        Console.WriteLine("  so meter-scale 3D needs a much larger mask (a fixed install, not a handheld).");
        Console.WriteLine("  Design your instrument by re-running `depthdesign <config.json>` on each candidate.");
        Console.WriteLine();
        Console.WriteLine("  Safe standoff grows with source strength (dose ∝ activity/z²): a handheld (small mask)");
        Console.WriteLine("  3D-locates weak/near sources; strong/far sources get lateral (2D) + \"far\" only.");

        File.WriteAllText("samples/depthdesign.csv", DepthDesignStudy.ToCsv(rows));
        Console.WriteLine("\n  CSV: samples/depthdesign.csv");
        return 0;
    }

    internal static int RunDoi(string[] args)
    {
        if (args.Length < 2)
        {
            Console.Error.WriteLine("Usage: montecarlo doi <base.json> [out.csv]");
            Console.Error.WriteLine("  Depth-of-interaction (DOI) parallax: the off-axis localization shift the interaction");
            Console.Error.WriteLine("  depth adds (vs the decoder's front-face back-projection) vs off-axis position & thickness.");
            return 1;
        }

        string csvPath = args.Length >= 3 ? args[2] : "samples/doi.csv";
        var cfg = ConfigLoader.Load(args[1]);
        double[] xs = [0, 3, 6, 9];
        double[] th = [5, 10, 20, 30];
        var rows = new DoiParallaxStudy().Run(cfg, xs, th, photonCount: 2_500_000, cfg.Seed);
        File.WriteAllText(csvPath, DoiParallaxStudy.ToCsv(rows));

        Console.WriteLine("Depth-of-interaction parallax: systematic localization shift (mm) the decoder cannot correct.");
        Console.WriteLine("(The decoder back-projects the front-face crossing; the real scintillation centroid is deeper.)");
        Console.WriteLine();
        Console.Write("   thick\\x ");
        foreach (double x in xs) Console.Write($"{x,8:F0}mm");
        Console.WriteLine();
        foreach (double t in th)
        {
            Console.Write($"   {t,5:F0}mm ");
            foreach (double x in xs)
                Console.Write($"{rows.Single(r => r.SourceXMm == x && r.CrystalThicknessMm == t).DoiShiftMm,8:F3} ");
            Console.WriteLine();
        }
        Console.WriteLine();
        Console.WriteLine("Zero on-axis (normal incidence); off-axis it grows with crystal thickness (deeper interactions →");
        Console.WriteLine("bigger parallax). Sub-mm here (far source → small obliquity), but a real floor under the sub-cell");
        Console.WriteLine("interpolation (theme 43) at the FOV edge, uncorrected by the centre-back-projecting decoder.");
        Console.WriteLine($"CSV written: {csvPath}");
        return 0;
    }
}
