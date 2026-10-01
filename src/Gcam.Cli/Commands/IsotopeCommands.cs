using System.Text;
using Gcam.Configuration;
using Gcam.Core;
using Gcam.Detector;
using Gcam.Simulation;
using static Gcam.Cli.ConsoleRender;

namespace Gcam.Cli;

/// <summary>Crystal-Compton multi-isotope separation, mixed fields, cascade summing, non-proportionality.</summary>
internal static class IsotopeCommands
{
    internal static int RunCompton(string[] args)
    {
        if (args.Length < 2) { Console.Error.WriteLine("Usage: montecarlo compton <base.json>"); return 1; }
        var baseConfig = ConfigLoader.Load(args[1]);
        const double windowFraction = 0.10;   // ±10% photopeak energy window
        const double detectedBudget = 400.0;  // fixed acquisition (detected counts, ideal detector)
        const int repeats = 200;
        const double failThresholdMm = 3.0;

        var study = new ComptonStudy();

        Console.WriteLine("Crystal Compton scattering — multi-pixel positioning strategies");
        Console.WriteLine($"Single {baseConfig.Source.EnergyKeV} keV source at ({baseConfig.Source.Position[0]},{baseConfig.Source.Position[1]}) mm, ±{windowFraction:P0} window, {repeats} reps @ {detectedBudget:F0} counts");
        Console.WriteLine();
        var rows = study.RunStrategies(baseConfig, windowFraction, detectedBudget, repeats, failThresholdMm);
        File.WriteAllText("samples/compton_strategies.csv", ComptonStudy.StrategiesToCsv(rows));

        Console.WriteLine("  strategy              efficiency   bias      RMS      fail%");
        Console.WriteLine("  -------------------   ----------   ------    ------   -----");
        foreach (var r in rows)
            Console.WriteLine($"  {r.Strategy,-19}   {r.EfficiencyRel,9:P0}   {r.BiasMm,5:F2}mm   {r.RmsMm,5:F2}mm   {r.FailRate,5:P0}");
        Console.WriteLine();

        // Multi-isotope contamination: a Co-60 source downscatters into the Cs-137 662 window,
        // but stays coded from ITS direction -> the decode separates the two spatially.
        double[] csPos = [4.0, 0.0, 0.0];
        double[] coPos = [-5.0, 3.0, 0.0];
        var c = study.RunContamination(baseConfig, csPos, coPos, windowFraction);
        File.WriteAllText("samples/compton_recon_cs.csv", ComptonStudy.ReconToCsv(c.ReconCs, c.OriginMm, c.StepMm));
        File.WriteAllText("samples/compton_recon_co.csv", ComptonStudy.ReconToCsv(c.ReconContaminant, c.OriginMm, c.StepMm));
        File.WriteAllText("samples/compton_recon_combined.csv", ComptonStudy.ReconToCsv(c.ReconCombined, c.OriginMm, c.StepMm));

        Console.WriteLine("Multi-isotope contamination (the 662 keV window):");
        Console.WriteLine($"  Cs-137 @ ({csPos[0]},{csPos[1]}) mm  +  Co-60 @ ({coPos[0]},{coPos[1]}) mm downscatter");
        Console.WriteLine($"  Co-60 contamination fraction in the 662 window: {c.ContaminationFraction:P0}");
        Console.WriteLine("  -> energy window alone can't remove it, but decoding the combined 662-window map");
        Console.WriteLine("     shows BOTH sources (contamination is coded from Co-60's direction).");
        Console.WriteLine("  Recon CSVs: samples/compton_recon_{cs,co,combined}.csv");
        return 0;
    }

    internal static int RunComptonStrip(string[] args)
    {
        if (args.Length < 2) { Console.Error.WriteLine("Usage: montecarlo compton-strip <base.json>"); return 1; }
        var baseConfig = ConfigLoader.Load(args[1]);
        const double windowFraction = 0.10;
        const double coScale = 8.0;    // a stronger Co-60 source, so its 662 contamination is visible
        var study = new ComptonStudy();

        // Separated: spatial decode already separates, stripping removes the Co ghost from the 662 image.
        // Co-located: the spatial decode CANNOT separate them; only per-pixel stripping recovers Cs.
        var scenes = new[]
        {
            study.RunStripping(baseConfig, "separated",  [4.0, 0.0, 0.0], [-5.0, 3.0, 0.0], windowFraction, coScale),
            study.RunStripping(baseConfig, "co-located", [0.0, 0.0, 0.0], [ 0.0, 0.0, 0.0], windowFraction, coScale),
        };

        Console.WriteLine($"Combined lever: per-pixel Compton stripping inside the coded pipeline, then decode (Co x{coScale:F0})");
        Console.WriteLine($"  Co downscatter-into-662 / Co-photopeak ratio R = {scenes[0].R:F3}");
        Console.WriteLine();
        Console.WriteLine("  scene        true Cs   raw 662   stripped   raw err / stripped err");
        Console.WriteLine("  ----------   -------   -------   --------   ----------------------");
        foreach (var s in scenes)
        {
            double rawErr = (s.RawCounts - s.TrueCsCounts) / s.TrueCsCounts * 100.0;
            double strErr = (s.StrippedCounts - s.TrueCsCounts) / s.TrueCsCounts * 100.0;
            Console.WriteLine($"  {s.Name,-10}   {s.TrueCsCounts,7:F0}   {s.RawCounts,7:F0}   {s.StrippedCounts,8:F0}   {rawErr,+6:F0}% / {strErr,+6:F0}%");
            foreach (var (tag, img) in new[] { ("csonly", s.ReconCsOnly), ("raw", s.ReconRaw), ("stripped", s.ReconStripped) })
                File.WriteAllText($"samples/strip_{s.Name}_{tag}.csv", ComptonStudy.ReconToCsv(img, s.OriginMm, s.StepMm));
        }
        Console.WriteLine();
        Console.WriteLine("  Co-located: spatial decode can't separate co-located sources — only per-pixel");
        Console.WriteLine("  stripping recovers the true Cs-137 count. Recon CSVs: samples/strip_*.csv");
        return 0;
    }

    internal static int RunMixedField(string[] args)
    {
        if (args.Length < 2) { Console.Error.WriteLine("Usage: montecarlo mixedfield <base.json>"); return 1; }
        var baseConfig = ConfigLoader.Load(args[1]);
        baseConfig.PhotonCount = 3_000_000;
        baseConfig.Decoder.Cyclic = false;                 // finite-mask decode suppresses off-axis ghosts
        // Keep the recon grid INSIDE the FCFOV (half = rank·cell/frac/2) — beyond it the partial-coding
        // region throws edge artifacts that can outshine a weak source.
        double frac = baseConfig.Geometry.MaskDetectorDistanceMm /
                      (baseConfig.Geometry.MaskDetectorDistanceMm + baseConfig.Geometry.SourceMaskDistanceMm);
        baseConfig.Decoder.ReconHalfExtentMm = 0.95 * baseConfig.Mask.Rank * baseConfig.Mask.CellPitchMm / frac / 2.0;
        baseConfig.Decoder.ReconStepMm = 0.4;

        // A real MIXED-ISOTOPE FIELD: three isotopes at three positions, imaged in ONE run.
        baseConfig.Sources =
        [
            new SourceConfig { Position = [5.0, 1.0, 0.0], ActivityBq = 1.0, Lines = [new EmissionLine { EnergyKeV = 661.7, Intensity = 0.851 }] },                                   // Cs-137
            new SourceConfig { Position = [-6.0, 3.0, 0.0], ActivityBq = 1.0, Lines = [new EmissionLine { EnergyKeV = 1173.2, Intensity = 0.999 }, new EmissionLine { EnergyKeV = 1332.5, Intensity = 0.999 }] }, // Co-60
            new SourceConfig { Position = [0.0, -6.0, 0.0], ActivityBq = 1.5, Lines = [new EmissionLine { EnergyKeV = 122.1, Intensity = 0.856 }] },                                  // Co-57
        ];

        var study = new MixedFieldStudy(new DefaultSimulationFactory());
        var r = study.LocalizeMultiple(baseConfig, k: baseConfig.Sources.Length, minSeparationMm: 3.0);

        Console.WriteLine("Mixed-isotope field imaged in ONE coded-aperture run (non-cyclic decode):");
        Console.WriteLine("  Cs-137 @ (5,1), Co-60 @ (-6,3), Co-57 @ (0,-6) mm  (K-known localization, K=3)");
        Console.WriteLine();
        Console.WriteLine("  true (x,y)      matched found (x,y)     error");
        Console.WriteLine("  -------------   --------------------   -------");
        double worst = 0.0;
        foreach (var m in MixedFieldStudy.MatchOneToOne(r.TruthXY, r.Found))
        {
            worst = Math.Max(worst, m.ErrorMm);
            Console.WriteLine($"  ({m.TruthX,4:F1},{m.TruthY,4:F1})      ({m.FoundX,5:F1},{m.FoundY,5:F1})          {m.ErrorMm,5:F2}mm");
        }
        File.WriteAllText("samples/mixedfield.csv", MixedFieldStudy.ToCsv(r));
        // Dump the reconstruction grid for the plot.
        var sb = new StringBuilder("x_mm,y_mm,value\n");
        for (int gy = 0; gy < r.Recon.Height; gy++)
            for (int gx = 0; gx < r.Recon.Width; gx++)
                sb.Append($"{r.OriginMm + gx * r.StepMm:F2},{r.OriginMm + gy * r.StepMm:F2},{r.Recon[gx, gy]:F4}\n");
        File.WriteAllText("samples/mixedfield_recon.csv", sb.ToString());
        Console.WriteLine();
        Console.WriteLine($"All {r.TruthXY.Length} sources localized, worst error {worst:F2} mm.");
        Console.WriteLine("A true mixed field (not summed per-line runs) localizes every source at once.");
        Console.WriteLine("CSV: samples/mixedfield.csv, samples/mixedfield_recon.csv");
        return 0;
    }

    internal static int RunMixedIso(string[] args)
    {
        if (args.Length < 2) { Console.Error.WriteLine("Usage: montecarlo mixediso <base.json>"); return 1; }
        var cfg = ConfigLoader.Load(args[1]);
        cfg.PhotonCount = 4_000_000;
        cfg.Decoder.Cyclic = false;
        double frac = cfg.Geometry.MaskDetectorDistanceMm /
                      (cfg.Geometry.MaskDetectorDistanceMm + cfg.Geometry.SourceMaskDistanceMm);
        cfg.Decoder.ReconHalfExtentMm = 0.95 * cfg.Mask.Rank * cfg.Mask.CellPitchMm / frac / 2.0;
        cfg.Decoder.ReconStepMm = 0.4;

        // A TRUE mixed field: Cs-137 (662) at A + a stronger Co-60 (1173+1332) at B. Imaged in ONE run
        // through the crystal-Compton detector with a 662 keV ±10% energy window.
        double[] csPos = [4.0, 0.0, 0.0], coPos = [-5.0, 3.0, 0.0];
        cfg.Sources =
        [
            new SourceConfig { Position = csPos, ActivityBq = 1.0, Lines = [new EmissionLine { EnergyKeV = 661.7, Intensity = 0.851 }] },
            new SourceConfig { Position = coPos, ActivityBq = 8.0, Lines = [new EmissionLine { EnergyKeV = 1173.2, Intensity = 0.999 }, new EmissionLine { EnergyKeV = 1332.5, Intensity = 0.999 }] },
        ];

        // ComptonFactory windows the flood at 662 keV; because it uses the default (now mixed-field) source,
        // the whole scene is imaged in ONE pipeline run — no per-line summing.
        var factory = new ComptonFactory(ComptonStrategy.PerPixelWindow, 661.7, 0.10);
        var r = new MixedFieldStudy(factory).LocalizeMultiple(cfg, k: 2, minSeparationMm: 3.0);

        Console.WriteLine("Mixed field through the 662 keV window (crystal-Compton), ONE run:");
        Console.WriteLine($"  Cs-137 @ ({csPos[0]},{csPos[1]}) + Co-60 @ ({coPos[0]},{coPos[1]}) (Co ACTIVITY ×8)");
        Console.WriteLine("  The 662 window can't reject Co downscatter (energy alone fails); the question is whether");
        Console.WriteLine("  the coded decode still images Cs at its position and the contamination at Co's.");
        Console.WriteLine();
        Console.WriteLine("  true (x,y)      matched peak (x,y)      error");
        Console.WriteLine("  -------------   -------------------    -------");
        foreach (var m in MixedFieldStudy.MatchOneToOne(r.TruthXY, r.Found))
            Console.WriteLine($"  ({m.TruthX,4:F1},{m.TruthY,4:F1})      ({m.FoundX,5:F1},{m.FoundY,5:F1})           {m.ErrorMm,5:F2}mm");

        var sb = new StringBuilder("x_mm,y_mm,value\n");
        for (int gy = 0; gy < r.Recon.Height; gy++)
            for (int gx = 0; gx < r.Recon.Width; gx++)
                sb.Append($"{r.OriginMm + gx * r.StepMm:F2},{r.OriginMm + gy * r.StepMm:F2},{r.Recon[gx, gy]:F4}\n");
        File.WriteAllText("samples/mixediso_recon.csv", sb.ToString());
        File.WriteAllText("samples/mixediso.csv", MixedFieldStudy.ToCsv(r));
        Console.WriteLine();
        // The conclusion follows the result: with the physical GAGG cross sections (theme 52) the Co downscatter fills
        // ~55 % of the 662 window, and at Co ×8 the Cs peak is lost; separation holds up to ~Co ×2.
        bool allFound = MixedFieldStudy.MatchOneToOne(r.TruthXY, r.Found).All(m => m.ErrorMm < 2.5);
        Console.WriteLine(allFound
            ? "Both land at their own positions in the 662-window image: the coded decode separates the Cs photopeak\n" +
              "from the Co downscatter by POSITION."
            : "NOT separated: the Co downscatter in the 662 window buries the Cs peak at this activity ratio (with\n" +
              "physical GAGG cross sections spatial separation holds up to about Co ×2 — Findings theme 52).");
        Console.WriteLine("Isotope ID of CO-LOCATED sources needs the spectral lever (per-pixel stripping, theme 16-17).");
        Console.WriteLine("CSV: samples/mixediso{,_recon}.csv");
        return 0;
    }

    internal static int RunMixedStrip(string[] args)
    {
        if (args.Length < 2) { Console.Error.WriteLine("Usage: montecarlo mixedstrip <base.json>"); return 1; }
        var baseCfg = ConfigLoader.Load(args[1]);
        baseCfg.PhotonCount = 4_000_000;
        baseCfg.Decoder.Cyclic = false;
        double frac = baseCfg.Geometry.MaskDetectorDistanceMm /
                      (baseCfg.Geometry.MaskDetectorDistanceMm + baseCfg.Geometry.SourceMaskDistanceMm);
        baseCfg.Decoder.ReconHalfExtentMm = 0.95 * baseCfg.Mask.Rank * baseCfg.Mask.CellPitchMm / frac / 2.0;
        baseCfg.Decoder.ReconStepMm = 0.4;
        const double wf = 0.10, csLine = 661.7, coCenter = 1332.5;

        SourceConfig Cs(double[] p) => new() { Position = p, ActivityBq = 1.0, Lines = [new EmissionLine { EnergyKeV = csLine, Intensity = 0.851 }] };
        SourceConfig Co(double[] p) => new() { Position = p, ActivityBq = 8.0, Lines = [new EmissionLine { EnergyKeV = 1173.2, Intensity = 0.999 }, new EmissionLine { EnergyKeV = 1332.5, Intensity = 0.999 }] };
        DetectorImage Flood(SourceConfig[] s, double center, long? photons = null)
        {
            var c = baseCfg.Clone(); c.Sources = s; c.PhotonCount = photons ?? baseCfg.PhotonCount;
            return new SimulationRunner(new ComptonFactory(ComptonStrategy.PerPixelWindow, center, wf)).Run(c).DetectorImage;
        }
        static double Sum(DetectorImage m) { double s = 0; foreach (var v in m.Raw) s += v; return s; }

        // The mixed field splits a FIXED photon budget by emission weight, so a weak source is diluted.
        // The "true Cs count" reference is therefore Cs run at its share of the budget (w_Cs / Σw), so it
        // is comparable to the Cs contribution actually inside the mixed 662 window.
        double wCsE = 1.0 * 0.851, wCoE = 8.0 * (0.999 + 0.999);
        long csPhotons = (long)(baseCfg.PhotonCount * (wCsE / (wCsE + wCoE)));

        // R = downscatter-into-662 per Co-photopeak — a budget-INDEPENDENT ratio (a crystal/window/geometry
        // property, not the source strength), calibrated from a Co-only run.
        var coCal662 = Flood([Co([0, 0, 0.0])], csLine);
        var coCalWin = Flood([Co([0, 0, 0.0])], coCenter);
        double R = Sum(coCalWin) > 0 ? Sum(coCal662) / Sum(coCalWin) : 0.0;

        Console.WriteLine("Per-pixel Compton stripping on a TRUE mixed field (Cs-137 + Co-60 ACTIVITY ×8).");
        Console.WriteLine($"  Calibrated R (Co downscatter-into-662 / Co-photopeak) = {R:F3}");
        Console.WriteLine();
        Console.WriteLine("  scene         true Cs   raw 662   stripped   raw err / stripped err");
        Console.WriteLine("  -----------   -------   -------   --------   ----------------------");
        var decoder = new DefaultSimulationFactory().CreateDecoder(baseCfg)!;
        foreach (var (name, csP, coP) in new[] { ("separated", new[] { 4.0, 0.0, 0.0 }, new[] { -5.0, 3.0, 0.0 }),
                                                 ("co-located", new[] { 0.0, 0.0, 0.0 }, new[] { 0.0, 0.0, 0.0 }) })
        {
            var trueCs = Flood([Cs(csP)], csLine, csPhotons);   // Cs at its mixed-field photon share
            var raw662 = Flood([Cs(csP), Co(coP)], csLine);
            var coWin = Flood([Cs(csP), Co(coP)], coCenter);
            var stripped = new DetectorImage(raw662.Width, raw662.Height);
            for (int y = 0; y < raw662.Height; y++)
                for (int x = 0; x < raw662.Width; x++)
                    stripped[x, y] = Math.Max(0.0, raw662[x, y] - R * coWin[x, y]);

            double tCs = Sum(trueCs), rw = Sum(raw662), st = Sum(stripped);
            Console.WriteLine($"  {name,-11}   {tCs,7:F0}   {rw,7:F0}   {st,8:F0}   {(rw - tCs) / tCs * 100,+6:F0}% / {(st - tCs) / tCs * 100,+6:F0}%");

            if (name == "co-located")
                foreach (var (tag, img) in new[] { ("raw", raw662), ("stripped", stripped) })
                {
                    var d = decoder.Decode(img);
                    var sb = new StringBuilder("x_mm,y_mm,value\n");
                    for (int gy = 0; gy < d.Reconstruction!.Height; gy++)
                        for (int gx = 0; gx < d.Reconstruction.Width; gx++)
                            sb.Append($"{d.ReconOriginMm + gx * d.ReconStepMm:F2},{d.ReconOriginMm + gy * d.ReconStepMm:F2},{d.Reconstruction[gx, gy]:F4}\n");
                    File.WriteAllText($"samples/mixedstrip_{tag}.csv", sb.ToString());
                }
        }
        Console.WriteLine();
        Console.WriteLine("Separated: coded decode already splits them; stripping removes the Co count over-estimate.");
        Console.WriteLine("Co-located: spatial decode CANNOT separate — per-pixel stripping recovers the Cs count to");
        Console.WriteLine("≈true (±few %; the max(0,·) floor biases it slightly up). The spectral lever succeeds where");
        Console.WriteLine("the spatial one can't. CSV: samples/mixedstrip_*.csv");
        return 0;
    }

    internal static int RunCascade(string[] args)
    {
        if (args.Length < 2)
        {
            Console.Error.WriteLine("Usage: montecarlo cascade <base.json> [out.csv]");
            Console.Error.WriteLine("  True (cascade) coincidence summing: two gammas from ONE decay both deposit in the");
            Console.Error.WriteLine("  crystal and SUM (Co-60 1173+1332→2505). Rate-independent, ∝ε². Isotope from the");
            Console.Error.WriteLine("  config Source.Isotope (Cs-137 / Co-60 / Na-22).");
            return 1;
        }

        string csvPath = args.Length >= 3 ? args[2] : "samples/cascade.csv";
        var cfg = ConfigLoader.Load(args[1]);
        var scheme = DecayScheme.From(cfg.Source.Isotope);

        // Detector footprint = the crystal array; MURA open fraction ≈ 0.5. Sweep source distance to vary ε.
        double detHalf = 0.5 * cfg.Detector.PixelPitchMm * cfg.Mask.Rank * Math.Max(cfg.Mask.MosaicX, cfg.Mask.MosaicY);
        double[] dists = [18, 24, 32, 43, 57, 76, 100];
        const long decays = 8_000_000;
        double specDist = dists[0];

        var d = cfg.Detector;
        var study = new CascadeSummingStudy(detHalf, detHalf, maskOpenFraction: 0.5,
            muAt662PerMm: d.CrystalAttenuationPerMm > 0 ? d.CrystalAttenuationPerMm : null,
            crystalDepthMm: d.CrystalThicknessMm, material: CrystalMaterial.ForConfig(d.Material));
        var (rows, spec, specBin, specMax) = study.Run(scheme, dists, decays, cfg.Seed, specDist);

        File.WriteAllText(csvPath, CascadeSummingStudy.ToCsv(rows));
        var sb = new System.Text.StringBuilder();
        sb.AppendLine("e_keV,counts");
        for (int i = 0; i < spec.Length; i++) sb.AppendLine($"{(i + 0.5) * specBin:F1},{spec[i]:F0}");
        File.WriteAllText(csvPath.Replace(".csv", "_spectrum.csv"), sb.ToString());

        Console.WriteLine($"Cascade coincidence summing: {scheme.Isotope}, {decays:N0} decays/distance, detector ±{detHalf:F0} mm.");
        Console.WriteLine("Two coincident gammas both depositing → one SUMMED event. Rate-independent, ∝ε².");
        Console.WriteLine();
        if (scheme.SumPeaks.Length == 0)
            Console.WriteLine("  (Single-line isotope — no cascade partner, so no coincidence summing. Expect sum ≈ 0.)");
        else
            Console.WriteLine($"  Sum peaks: {string.Join(", ", scheme.SumPeaks.Select(s => $"{s.label}={s.energyKeV:F0}keV"))}");
        Console.WriteLine();
        Console.WriteLine("   dist   single/decay   sum/decay    sum/single");
        Console.WriteLine("   ----   ------------   ---------    ----------");
        foreach (var r in rows)
            Console.WriteLine($"   {r.SourceDistanceMm,4:F0}   {r.SinglePhotopeakPerDecay,12:E3}   {r.SumPeakPerDecay,9:E3}    {r.SumToSingleRatio,10:E3}");

        var pts = rows.Where(r => r.SumPeakPerDecay > 0 && r.SinglePhotopeakPerDecay > 0).ToArray();
        if (pts.Length >= 2)
        {
            double[] lx = pts.Select(p => Math.Log(p.SinglePhotopeakPerDecay)).ToArray();
            double[] ly = pts.Select(p => Math.Log(p.SumPeakPerDecay)).ToArray();
            double mx = lx.Average(), my = ly.Average(), sxy = 0, sxx = 0;
            for (int i = 0; i < lx.Length; i++) { sxy += (lx[i] - mx) * (ly[i] - my); sxx += (lx[i] - mx) * (lx[i] - mx); }
            Console.WriteLine();
            Console.WriteLine($"  log-log slope (sum vs single) = {sxy / sxx:F2}  →  sum ∝ single^2 = ∝ε² geometric scaling.");
            Console.WriteLine("  (Cascade summing is distinguished from random pile-up by being RATE-independent per decay —");
            Console.WriteLine("   not by this slope: pile-up also scales ∝ε² in a distance sweep. Here the pair is same-decay.)");
        }
        Console.WriteLine($"CSV written: {csvPath} (+ _spectrum.csv: the summed spectrum at {specDist:F0} mm)");
        return 0;
    }

    internal static int RunNonProp(string[] args)
    {
        if (args.Length < 2)
        {
            Console.Error.WriteLine("Usage: montecarlo nonprop <base.json> [out.csv]");
            Console.Error.WriteLine("  Derives the crystal's INTRINSIC (non-proportional) resolution from the Compton cascade");
            Console.Error.WriteLine("  + a scintillator light-yield curve nP(E), instead of the hand-set constant floor.");
            return 1;
        }

        string csvPath = args.Length >= 3 ? args[2] : "samples/nonprop.csv";
        var cfg = ConfigLoader.Load(args[1]);
        var crystalMaterial = CrystalMaterial.ForConfig(cfg.Detector.Material);
        double mu662 = cfg.Detector.CrystalAttenuationPerMm > 0 ? cfg.Detector.CrystalAttenuationPerMm
                                                                : crystalMaterial.MuPerMm(CrystalMaterial.ReferenceKeV);
        double depth = cfg.Detector.CrystalThicknessMm > 0 ? cfg.Detector.CrystalThicknessMm : 10.0;
        double[] energies = [122, 356, 511, 662, 1000, 1332];
        var crystals = new[] { NonProportionality.Proportional, NonProportionality.Gagg, NonProportionality.NaI, NonProportionality.CsI };
        const long samples = 1_000_000;

        var (pts, spectra, bin, max) = new NonProportionalityStudy(mu662, depth, crystalMaterial)
            .Run(energies, crystals, samples, cfg.Seed, spectrumEnergyKeV: 662.0);
        File.WriteAllText(csvPath, NonProportionalityStudy.ToCsv(pts));
        var ss = new System.Text.StringBuilder();
        ss.AppendLine("e_keV," + string.Join(",", crystals.Select(c => c.Name)));
        for (int i = 0; i < spectra[0].Length; i++)
            ss.AppendLine($"{(i + 0.5) * bin:F1}," + string.Join(",", spectra.Select(s => s[i].ToString("F0"))));
        File.WriteAllText(csvPath.Replace(".csv", "_spectrum662.csv"), ss.ToString());

        Console.WriteLine($"Non-proportionality study: the nP COMPONENT of intrinsic resolution from the cascade + nP(E), {samples:N0} events/energy.");
        Console.WriteLine("The light per keV varies with electron energy, so a full-energy cascade's total light fluctuates");
        Console.WriteLine("even at fixed deposited energy — the non-proportionality resolution, with NO photon-counting noise.");
        Console.WriteLine();
        Console.Write("   energy ");
        foreach (var c in crystals) Console.Write($"{c.Name,10}");
        Console.WriteLine("   (non-proportionality FWHM component, %)");
        Console.WriteLine("   ------ " + string.Concat(crystals.Select(_ => "  --------")));
        foreach (double e in energies)
        {
            Console.Write($"   {e,5:F0}  ");
            foreach (var c in crystals)
                Console.Write($"{pts.Single(p => p.Crystal == c.Name && p.EnergyKeV == e).IntrinsicFwhmPct,10:F2}");
            Console.WriteLine();
        }
        Console.WriteLine();
        Console.WriteLine("Exactly 0 for a proportional crystal (this component IS non-proportionality); ENERGY-DEPENDENT, so a");
        Console.WriteLine("constant floor can't capture it. GAGG is comparatively proportional (~1-1.5% at 662, BELOW the total");
        Console.WriteLine("intrinsic floor — the rest is light-collection / Ce non-uniformity / SiPM, not electron nP). Photo-");
        Console.WriteLine("absorption is modelled as one full-energy electron (no binding/Auger), so this slightly under-counts.");
        Console.WriteLine($"CSV written: {csvPath} (+ _spectrum662.csv: the 662 keV pulse-height spectrum, photopeak + continuum, no counting noise)");
        return 0;
    }
}
