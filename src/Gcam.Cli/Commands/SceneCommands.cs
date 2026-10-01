using System.Text;
using Gcam.Configuration;
using Gcam.Core;
using Gcam.Detector;
using Gcam.Simulation;
using static Gcam.Cli.ConsoleRender;

namespace Gcam.Cli;

/// <summary>Scene effects: ambient background, shield leak, finite source size.</summary>
internal static class SceneCommands
{
    internal static int RunBackground(string[] args)
    {
        if (args.Length < 2) { Console.Error.WriteLine("Usage: montecarlo background <base.json>"); return 1; }
        var baseConfig = ConfigLoader.Load(args[1]);
        double[] bsr = [0.0, 0.1, 0.25, 0.5, 1.0, 2.0, 4.0, 8.0];
        const double detectedBudget = 400.0;   // fixed acquisition (detected source counts)
        const int repeats = 200;
        const double failThresholdMm = 3.0;

        var rows = new BackgroundStudy().RunSweep(baseConfig, bsr, detectedBudget, repeats, failThresholdMm);
        File.WriteAllText("samples/background_sweep.csv", BackgroundStudy.ToCsv(rows));

        Console.WriteLine("Ambient background vs coded-aperture localization (uncoded uniform pedestal)");
        Console.WriteLine($"  Source @ ({baseConfig.Source.Position[0]},{baseConfig.Source.Position[1]}) mm, {detectedBudget:F0} detected counts, {repeats} Poisson reps");
        Console.WriteLine("  The MURA decode pushes the flat pedestal into DC; its shot noise is what eventually buries the peak.");
        Console.WriteLine();
        Console.WriteLine("   BSR    bg/pixel   peakSNR   bias     RMS      fail%");
        Console.WriteLine("   ----   --------   -------   ------   ------   -----");
        foreach (var r in rows)
            Console.WriteLine($"   {r.Bsr,4:F2}   {r.BgPerPixel,8:F2}   {r.PeakSnr,7:F1}   {r.BiasMm,5:F2}mm   {r.RmsMm,5:F2}mm   {r.FailRate,5:P0}");
        Console.WriteLine();
        Console.WriteLine("  CSV: samples/background_sweep.csv");

        // Structured (graded) background: same TOTAL level, but stronger on one side (a nearer contaminated wall).
        // A flat pedestal is rejected to DC; a gradient is diffuse yet NOT flat, so a low-frequency residual
        // survives the decode and BIASES the estimate. Same BSR grid, contrast 0 vs 0.6 along +x.
        const double gradContrast = 0.6;
        var graded = new BackgroundStudy().RunSweep(baseConfig, bsr, detectedBudget, repeats, failThresholdMm,
                                                    gradientContrast: gradContrast, gradientAngleDeg: 0.0);
        File.WriteAllText("samples/background_gradient.csv", BackgroundStudy.ToCsv(graded));
        Console.WriteLine();
        Console.WriteLine($"Structured background — a {gradContrast:P0} spatial gradient (same total level, stronger on +x side):");
        Console.WriteLine("  While the source peak wins, flat and graded are IDENTICAL (the gradient's low-frequency");
        Console.WriteLine("  residual doesn't move the argmax). At the knee the gradient drags the estimate to its");
        Console.WriteLine("  strong side -> a systematic bias where a flat pedestal only fails randomly.");
        Console.WriteLine("   BSR    bias(flat)   bias(grad)   RMS(grad)");
        Console.WriteLine("   ----   ----------   ----------   ---------");
        for (int i = 0; i < rows.Length; i++)
            Console.WriteLine($"   {rows[i].Bsr,4:F2}   {rows[i].BiasMm,7:F2}mm   {graded[i].BiasMm,7:F2}mm   {graded[i].RmsMm,6:F2}mm");
        Console.WriteLine();
        Console.WriteLine("  CSV: samples/background_gradient.csv");
        return 0;
    }

    internal static int RunShield(string[] args)
    {
        if (args.Length < 2) { Console.Error.WriteLine("Usage: montecarlo shield <base.json>"); return 1; }
        var baseConfig = ConfigLoader.Load(args[1]);
        baseConfig.Source.Position = [0, 0, 0.0];

        const double nSrc = 400.0;           // fixed detected source counts
        const double bg0 = 20.0;             // UNSHIELDED side/rear background counts/pixel (heavy env.)
        const int repeats = 250;
        const double failThr = 3.0;
        double[] thick = [0, 2, 4, 6, 8, 10, 12, 15, 20, 25, 30];

        // Tungsten linear attenuation (per mm) — from NIST XCOM total-with-coherent (mu/rho * rho).
        // NOTE: the crystal-Compton MuRel power law (661.7/E)^1.56 is only accurate NEAR 662 keV; it
        // underestimates high-energy attenuation badly (~40% low at 1.25 MeV), so shielding must use
        // tabulated coefficients per line, not that scaling. Values are narrow-beam (uncollided) — a
        // first-order model that neglects Compton buildup and oblique path lengths (knees ~ approximate).
        var backgrounds = new (string label, double energy, double muPerMm)[]
        {
            ("scattered_250keV", 250.0,  0.50),    // NIST W ~0.26 cm^2/g
            ("Cs137_662keV",     661.7,  0.178),   // ~0.093 cm^2/g (anchor)
            ("Co60_1250keV",     1250.0, 0.112),   // ~0.058 cm^2/g
        };

        Console.WriteLine("Optimal 5-sided (side/top/bottom/rear) tungsten shield thickness");
        Console.WriteLine($"  {nSrc:F0} source counts, unshielded background {bg0:F0} counts/px, {repeats} Poisson reps");
        Console.WriteLine($"  detector {baseConfig.Detector.PixelsX}x{baseConfig.Detector.PixelsY}, barrel {baseConfig.Geometry.MaskDetectorDistanceMm + baseConfig.Detector.CrystalThicknessMm:F0} mm");
        Console.WriteLine();

        const double sideFrac = 0.8;          // 4 side walls vs 1 rear -> ~80% of the leak is edge-weighted
        var study = new ShieldStudy(new DefaultSimulationFactory());
        var csv = new System.Text.StringBuilder("bg,thickness_mm,transmission,bg_per_px,rms_raw_mm,rms_calib_mm,fail_raw,shield_kg\n");
        ShieldRow[]? scatteredUniform = null;
        foreach (var (label, energy, mu) in backgrounds)
        {
            var rows = study.Run(baseConfig, nSrc, bg0, mu, thick, repeats, failThr);
            if (label == "scattered_250keV") scatteredUniform = rows;
            csv.Append(ShieldStudy.ToCsv(label, rows).Split('\n', 2)[1]);   // drop repeated header

            // A REAL knee: thinnest t whose raw RMS is near the best AND actually localizes well
            // (absolute quality gate — otherwise a background that never localizes returns a bogus
            // "knee" at the first row that merely ties the equally-bad rest).
            double floor = rows.Min(r => r.RmsRawMm);
            var knee = rows.FirstOrDefault(r => r.RmsRawMm <= floor * 1.15 && r.RmsRawMm < failThr && r.FailRaw < 0.10);

            Console.WriteLine($"=== background {label}  (mu_W = {mu:F3}/mm, HVL {0.6931/mu:F1} mm) ===");
            Console.WriteLine("  t(mm)  transmit  bg/px   RMS raw   RMS calib   shield(kg)");
            foreach (var r in rows)
                Console.WriteLine($"  {r.ThicknessMm,4:F0}   {r.Transmission,7:F3}  {r.BgPerPixel,6:F2}  {r.RmsRawMm,6:F2}mm  {r.RmsCalibMm,7:F2}mm   {r.ShieldMassKg,7:F2}");
            Console.WriteLine(knee is null
                ? $"  -> NO useful thickness in [{thick[0]:F0},{thick[^1]:F0}] mm (unshieldable at carriable mass here)"
                : $"  -> useful thickness ~ {knee.ThicknessMm:F0} mm  (RMS {knee.RmsRawMm:F2} mm, shield {knee.ShieldMassKg:F2} kg)");
            Console.WriteLine();
        }
        // Directional-leak comparison (scattered background): the SAME total leak, but re-shaped so 80% enters
        // edge-weighted through the side walls instead of a flat pedestal. The coded decode rejects the flat
        // pedestal into DC but not the edge structure, so localization degrades more per count -> a thicker knee.
        if (scatteredUniform is not null)
        {
            var dir = study.Run(baseConfig, nSrc, bg0, backgrounds[0].muPerMm, thick, repeats, failThr, sideFrac);
            for (int i = 0; i < dir.Length; i++)   // both drop repeated header; tag the directional rows
                csv.Append(ShieldStudy.ToCsv("scattered_250keV_dir", new[] { dir[i] }).Split('\n', 2)[1]);

            double Knee(ShieldRow[] rr)
            {
                double floor = rr.Min(r => r.RmsRawMm);
                var k = rr.FirstOrDefault(r => r.RmsRawMm <= floor * 1.15 && r.RmsRawMm < failThr && r.FailRaw < 0.10);
                return k?.ThicknessMm ?? double.NaN;
            }
            Console.WriteLine($"=== directional leak (scattered_250keV, {sideFrac:P0} side-wall, same total) ===");
            Console.WriteLine("  t(mm)   RMS uniform   RMS directional");
            for (int i = 0; i < dir.Length; i++)
                Console.WriteLine($"  {dir[i].ThicknessMm,4:F0}   {scatteredUniform[i].RmsRawMm,9:F2}mm   {dir[i].RmsRawMm,11:F2}mm");
            Console.WriteLine($"  -> useful thickness: uniform ~{Knee(scatteredUniform):F0} mm  vs  directional ~{Knee(dir):F0} mm" +
                              "  (structured leak needs a bit more shield)");
            Console.WriteLine();
        }

        File.WriteAllText("samples/shield.csv", csv.ToString());
        Console.WriteLine("Low-energy scattered background is killed by a few mm; Co-60 never reaches the knee");
        Console.WriteLine("at a carriable mass -> shield the low-E noise, beat high-E with coded+stripping.");
        Console.WriteLine("A directional (side-wall) leak of the SAME total degrades localization more than a flat");
        Console.WriteLine("pedestal (the decode only rejects the uniform DC part) -> the real knee is a bit thicker.");
        Console.WriteLine("CSV: samples/shield.csv");
        return 0;
    }

    internal static int RunFiniteSource(string[] args)
    {
        if (args.Length < 2)
        {
            Console.Error.WriteLine("Usage: montecarlo finitesrc <base.json> [out.csv]");
            Console.Error.WriteLine("  Finite (extended) source: reconstruction blur / washout vs source diameter, plus");
            Console.Error.WriteLine("  capsule self-attenuation (662 keV vs a low-energy line) vs wall thickness.");
            return 1;
        }

        string csvPath = args.Length >= 3 ? args[2] : "samples/finitesrc.csv";
        var cfg = ConfigLoader.Load(args[1]);
        var study = new FiniteSourceStudy();

        double[] dias = [0, 1, 2, 3, 4, 5, 6, 8, 11, 16];
        var (rows, _, _, _) = study.Run(cfg, dias, 0.0, 0.0, photonCount: 3_000_000, cfg.Seed);
        File.WriteAllText(csvPath, FiniteSourceStudy.ToCsv(rows));

        // Sealed-source capsule (steel-equivalent walls; a small ceramic/salt pellet). μ (per mm): 662 keV vs a 32 keV
        // Ba K X-ray — representative, since the sim has no material database.
        double[] tc = [0, 0.25, 0.5, 1, 2, 4];
        var caps = study.CapsuleSweep(tc, pelletDiameterMm: 3.0,
            muPelletPrimary: 0.030, muCapsulePrimary: 0.057, muPelletLowE: 0.5, muCapsuleLowE: 0.94, samples: 1_000_000);
        File.WriteAllText(csvPath.Replace(".csv", "_capsule.csv"), FiniteSourceStudy.CapsuleCsv(caps));

        Console.WriteLine($"Finite-source study: {dias.Length} diameters, {rows[0].ReconPeakFwhmMm:F2} mm point-source resolution.");
        Console.WriteLine("A finite source blurs the coded reconstruction and, once its size ~ the coded resolution, washes");
        Console.WriteLine("the shadow out (contrast peak/secondary → 1, localization lost).");
        Console.WriteLine();
        Console.WriteLine("   diameter   reconFWHM   contrast   bias(mm)   note");
        Console.WriteLine("   --------   ---------   --------   --------   ----");
        foreach (var r in rows)
        {
            string note = r.ReconConfidence < 1.05 ? "WASHED OUT" : (r.DiameterMm == 0 ? "point" : "");
            Console.WriteLine($"   {r.DiameterMm,6:F1}      {r.ReconPeakFwhmMm,7:F2}    {r.ReconConfidence,7:F3}    {r.LocalizationBiasMm,6:F2}    {note}");
        }
        Console.WriteLine();
        Console.WriteLine("Capsule self-attenuation (steel-equiv. wall; 3 mm pellet) — the low-energy line is killed far more:");
        Console.WriteLine("   wall(mm)   T(662keV)   T(32keV)");
        Console.WriteLine("   --------   ---------   --------");
        foreach (var c in caps)
            Console.WriteLine($"   {c.ThicknessMm,6:F2}      {c.TransmissionPrimary,7:F3}    {c.TransmissionLowE,7:F3}");
        Console.WriteLine($"CSV written: {csvPath} (+ _capsule.csv)");
        return 0;
    }
}
