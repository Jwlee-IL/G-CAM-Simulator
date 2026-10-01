using System.Text;
using Gcam.Configuration;
using Gcam.Core;
using Gcam.Detector;
using Gcam.Simulation;
using static Gcam.Cli.ConsoleRender;

namespace Gcam.Cli;

/// <summary>Core / configuration studies: FCFOV sweep, field of view, config scan, noise, mask thickness, crystal uniformity, detector array.</summary>
internal static class CoreCommands
{
    internal static int RunSweep(string[] args)
    {
        if (args.Length < 2)
        {
            Console.Error.WriteLine("Usage: montecarlo sweep <base.json> [out.csv]");
            return 1;
        }

        const double halfExtentMm = 18.0;
        const double stepMm = 1.5;
        const double reconStepMm = 0.75;
        const long photonsPerPoint = 300_000;   // biasing makes every photon land on the detector
        const double ghostThresholdMm = 3.0;

        var baseConfig = ConfigLoader.Load(args[1]);

        // Theoretical fully-coded FOV half-width (one cyclic period / 2).
        double frac = baseConfig.Geometry.MaskDetectorDistanceMm /
                      (baseConfig.Geometry.MaskDetectorDistanceMm + baseConfig.Geometry.SourceMaskDistanceMm);
        double fcfovHalf = baseConfig.Mask.Rank * baseConfig.Mask.CellPitchMm / frac / 2.0;

        Console.WriteLine($"Sweep    : source over ±{halfExtentMm} mm, step {stepMm} mm, {photonsPerPoint:N0} photons/point");
        Console.WriteLine($"Mask     : {baseConfig.Mask.Type} rank {baseConfig.Mask.Rank}, cell {baseConfig.Mask.CellPitchMm} mm");
        Console.WriteLine($"Predicted FCFOV half-width: {fcfovHalf:F1} mm (theory)");
        Console.WriteLine($"Both modes search the same wide grid (±{halfExtentMm} mm) for a fair comparison.");
        Console.WriteLine();

        var factory = new DefaultSimulationFactory();
        foreach (var (label, cyclic) in new[] { ("CYCLIC (classical)", true), ("NON-CYCLIC (finite mask)", false) })
        {
            // Same wide reconstruction grid for both modes; only the periodicity toggles.
            var cfg = new SimulationConfig
            {
                Name = baseConfig.Name,
                PhotonCount = baseConfig.PhotonCount,
                Seed = baseConfig.Seed,
                Source = baseConfig.Source,
                Mask = baseConfig.Mask,
                Detector = baseConfig.Detector,
                Geometry = baseConfig.Geometry,
                Decoder = new DecoderConfig
                {
                    Cyclic = cyclic,
                    ReconHalfExtentMm = halfExtentMm,
                    ReconStepMm = reconStepMm,
                },
            };

            var sweep = new SourceSweep(factory).Run(cfg, halfExtentMm, stepMm, photonsPerPoint);
            string csvPath = cyclic ? "samples/sweep_cyclic.csv" : "samples/sweep_noncyclic.csv";
            File.WriteAllText(csvPath, SourceSweep.ToCsv(sweep));

            double maxErr = 0.0;
            int inFov = 0;
            foreach (var p in sweep.Points)
            {
                if (p.ErrorMm > maxErr) maxErr = p.ErrorMm;
                if (p.ErrorMm < ghostThresholdMm) inFov++;
            }

            Console.WriteLine($"===== {label} =====");
            Console.WriteLine($"FCFOV map   '.'=localized (<{ghostThresholdMm} mm)   '#'=ghost   '+'=center:");
            RenderGhostMap(sweep, ghostThresholdMm);
            Console.WriteLine($"{inFov}/{sweep.Points.Length} points localized within {ghostThresholdMm} mm   (max error {maxErr:F1} mm)");
            Console.WriteLine($"CSV: {csvPath}");
            Console.WriteLine();
        }
        return 0;
    }

    internal static int RunFieldOfView(string[] args)
    {
        if (args.Length < 2) { Console.Error.WriteLine("Usage: montecarlo fov <base.json> [out.csv]"); return 1; }
        var baseConfig = ConfigLoader.Load(args[1]);
        string csvPath = args.Length >= 3 ? args[2] : "samples/fov.csv";
        baseConfig.PhotonCount = 1_000_000;

        double[] distances = [1000.0, 5000.0];
        double[] directions = [0.0, 45.0];
        double[] onAxis = [500.0, 5000.0];
        double[] bsrs = [0.0, 1.0];
        double[] angles = Enumerable.Range(0, 41).Select(i => i * 0.5).ToArray();   // 0 … 20°
        const int repeats = 100;
        double fcHalf = FieldOfViewStudy.FullyCodedHalfAngleDeg(baseConfig);
        double resDeg = Math.Atan(baseConfig.Mask.CellPitchMm / baseConfig.Geometry.MaskDetectorDistanceMm) * 180.0 / Math.PI;

        Console.WriteLine("Field of view at field distance: usable field of non-cyclic decoding + out-of-field cue");
        Console.WriteLine($"Fully coded half-angle {fcHalf:F2}° along x, {FieldOfViewStudy.FullyCodedHalfAngleDeg(baseConfig, 45.0):F2}° along the diagonal; resolution element {resDeg:F2}° (success = error within it)");
        Console.WriteLine($"Angles 0–20°, {repeats} Poisson realizations each; N0 = counts the source gives on axis; BSR = uniform pedestal / N0");
        Console.WriteLine();

        var all = new List<FovRow>();
        var study = new FieldOfViewStudy();
        foreach (double s in distances)
            foreach (double dir in directions)
                all.AddRange(study.Run(baseConfig, s, dir, angles, onAxis, bsrs, repeats));
        File.WriteAllText(csvPath, FieldOfViewStudy.ToCsv(all));

        Console.WriteLine("  S(m)  dir   N0     BSR  usable ± (>=90 % localized)  side cue >=95 %          outside flag >=90 %   worst false in-field");
        Console.WriteLine("                          non-cyclic  cyclic         centroid    peak         centroid              spot   unflagged");
        foreach (var g in all.GroupBy(r => (r.DistanceMm, r.DirectionDeg, r.OnAxisCounts, r.Bsr)))
        {
            var rows = g.OrderBy(r => r.AngleDeg).ToArray();
            double nc = FieldOfViewStudy.UsableHalfAngleDeg(rows, r => r.LocalizedNonCyclic, 0.9);
            double cy = FieldOfViewStudy.UsableHalfAngleDeg(rows, r => r.LocalizedCyclic, 0.9);
            double fcDir = FieldOfViewStudy.FullyCodedHalfAngleDeg(baseConfig, g.Key.DirectionDeg);
            static string Range((double From, double To)? r) => r is { } v ? $"{v.From,4:F1}-{v.To,4:F1}" : "   never ";
            var sideC = FieldOfViewStudy.PassingRange(rows.Where(r => r.AngleDeg > 0), r => r.SideByCentroid, 0.95);
            var sideP = FieldOfViewStudy.PassingRange(rows.Where(r => r.AngleDeg > 0), r => r.SideByPeak, 0.95);
            var outC = FieldOfViewStudy.PassingRange(rows.Where(r => r.AngleDeg > fcDir), r => r.OutsideByCentroid, 0.9);
            double fi = rows.Max(r => r.FalseInField), fu = rows.Max(r => r.FalseInFieldUnflagged);
            Console.WriteLine($"  {g.Key.DistanceMm / 1000,4:F0}  {g.Key.DirectionDeg,3:F0}  {g.Key.OnAxisCounts,5:F0}  {g.Key.Bsr,4:F1}    {nc,5:F1}       {cy,5:F1}        {Range(sideC)}  {Range(sideP)}    {Range(outC)}             {fi,5:P0}  {fu,5:P0}");
        }
        Console.WriteLine();
        Console.WriteLine($"CSV written: {csvPath}");
        return 0;
    }

    internal static int RunScan(string[] args)
    {
        if (args.Length < 2)
        {
            Console.Error.WriteLine("Usage: montecarlo scan <base.json> [out.csv]");
            return 1;
        }

        int[] ranks = [7, 11, 13, 17, 19, 23];
        double[] cellPitches = [1.0, 1.5, 2.0];
        double[] distances = [20, 25, 30, 40, 50, 60, 70, 80];
        const int sweepN = 11;
        const long photonsPerPoint = 200_000;   // biasing → every photon useful
        const double efficiencyFloor = 3e-5;    // sensitivity floor (geometric efficiency)
        string csvPath = args.Length >= 3 ? args[2] : "samples/scan.csv";

        var baseConfig = ConfigLoader.Load(args[1]);
        int total = ranks.Length * cellPitches.Length * distances.Length;
        Console.WriteLine($"Scanning {total} configs: rank {{{string.Join(",", ranks)}}} x pitch {{{string.Join(",", cellPitches)}}} x D {{{string.Join(",", distances)}}} mm");
        Console.WriteLine($"Source distance S = {baseConfig.Geometry.SourceMaskDistanceMm} mm (fixed), {sweepN}x{sweepN} sweep, {photonsPerPoint:N0} photons/point");
        Console.WriteLine($"Objective: maximize usable FOV (error < 1 resolution cell), efficiency floor = {efficiencyFloor:E1}");
        Console.WriteLine();

        var rows = new ParameterScan(new DefaultSimulationFactory())
            .Run(baseConfig, ranks, cellPitches, distances, sweepN, photonsPerPoint);
        File.WriteAllText(csvPath, ParameterScan.ToCsv(rows));

        var ranked = rows
            .Where(r => r.MedianEfficiency >= efficiencyFloor)
            .OrderByDescending(r => r.UsableFovAreaMm2)
            .ToArray();

        Console.WriteLine("Top configs by usable FOV (sensitivity floor met):");
        Console.WriteLine("  rank  pitch   D    FCFOV±  resol  medErr  usable%   FOVarea  eff(e-4)");
        Console.WriteLine("  ----  -----  ----  ------  -----  ------  -------  --------  --------");
        foreach (var r in ranked.Take(15))
            Console.WriteLine($"  {r.Rank,4}  {r.CellPitchMm,5:F1}  {r.DistanceMm,4:F0}  {r.FcfovHalfMm,6:F1}  {r.ResolutionMm,5:F1}  {r.MedianErrorMm,6:F2}  {r.UsableFraction,6:P0}  {r.UsableFovAreaMm2,8:F0}  {r.MedianEfficiency * 1e4,8:F2}");

        if (ranked.Length > 0)
        {
            var best = ranked[0];
            Console.WriteLine();
            Console.WriteLine($"BEST: rank {best.Rank}, cell {best.CellPitchMm} mm, D {best.DistanceMm} mm");
            Console.WriteLine($"      usable FOV ±{Math.Sqrt(best.UsableFovAreaMm2) / 2.0:F1} mm, resolution {best.ResolutionMm:F1} mm, median error {best.MedianErrorMm:F2} mm, efficiency {best.MedianEfficiency:E2}");
        }
        Console.WriteLine();
        Console.WriteLine($"CSV written: {csvPath}  ({rows.Length} configs)");
        return 0;
    }

    internal static int RunNoise(string[] args)
    {
        if (args.Length < 2)
        {
            Console.Error.WriteLine("Usage: montecarlo noise <base.json> [out.csv]");
            return 1;
        }

        double[] countLevels = [5, 10, 25, 50, 100, 250, 500, 1000, 2500, 5000];
        const int repeats = 300;
        const double failThresholdMm = 3.0;
        string csvPath = args.Length >= 3 ? args[2] : "samples/noise.csv";

        var baseConfig = ConfigLoader.Load(args[1]);
        var positions = new (string label, double x, double y)[] { ("centered", 0.0, 0.0), ("edge_8mm", 8.0, 0.0) };
        var factory = new DefaultSimulationFactory();

        Console.WriteLine($"Noise study: {repeats} Poisson realizations per count level, failure = error > {failThresholdMm} mm");
        Console.WriteLine($"Source activity {baseConfig.Source.ActivityBq:E1} Bq, branching {baseConfig.Source.BranchingRatio} (Cs-137 662 keV)");
        Console.WriteLine();

        var csv = new StringBuilder("source,detected_counts,rms_error_mm,mean_error_mm,failure_rate,equiv_time_s\n");
        foreach (var pos in positions)
        {
            var cfg = new SimulationConfig
            {
                Name = baseConfig.Name,
                PhotonCount = baseConfig.PhotonCount,
                Seed = baseConfig.Seed,
                Source = new SourceConfig
                {
                    Isotope = baseConfig.Source.Isotope,
                    EnergyKeV = baseConfig.Source.EnergyKeV,
                    Position = [pos.x, pos.y, 0.0],
                    DirectionalBiasing = true,
                    ActivityBq = baseConfig.Source.ActivityBq,
                    AcquisitionTimeSeconds = baseConfig.Source.AcquisitionTimeSeconds,
                    BranchingRatio = baseConfig.Source.BranchingRatio,
                },
                Mask = baseConfig.Mask,
                Detector = baseConfig.Detector,
                Geometry = baseConfig.Geometry,
                Decoder = baseConfig.Decoder,
            };

            var res = new NoiseStudy(factory).Run(cfg, countLevels, repeats, failThresholdMm);

            Console.WriteLine($"=== source {pos.label} at ({pos.x},{pos.y}) mm   (efficiency {res.Efficiency:E2}) ===");
            Console.WriteLine("  counts  equivTime   RMSerr   fail%");
            Console.WriteLine("  ------  ---------  -------  -----");
            foreach (var p in res.Points)
            {
                double t = NoiseStudy.EquivalentTimeSeconds(p.DetectedCounts, cfg.Source.ActivityBq, cfg.Source.BranchingRatio, res.Efficiency);
                Console.WriteLine($"  {p.DetectedCounts,6:F0}  {t,8:F2}s  {p.RmsErrorMm,6:F2}mm  {p.FailureRate,5:P0}");
                csv.Append($"{pos.label},{p.DetectedCounts:F0},{p.RmsErrorMm:F3},{p.MeanErrorMm:F3},{p.FailureRate:F3},{t:F3}\n");
            }
            Console.WriteLine();
        }

        File.WriteAllText(csvPath, csv.ToString());
        Console.WriteLine($"CSV written: {csvPath}");
        return 0;
    }

    internal static int RunThickness(string[] args)
    {
        if (args.Length < 2)
        {
            Console.Error.WriteLine("Usage: montecarlo thickness <base.json> [out.csv]");
            return 1;
        }

        double[] thicknesses = [2, 4, 6, 8, 10, 14, 18, 24, 32];
        const int nRadial = 13;
        const double photonBudget = 400_000.0;  // physical emitted = activity × branching × time
        const int repeats = 60;
        const double failFraction = 0.5;
        string csvPath = args.Length >= 3 ? args[2] : "samples/thickness.csv";

        var baseConfig = ConfigLoader.Load(args[1]);
        double d = baseConfig.Geometry.MaskDetectorDistanceMm;
        double s = baseConfig.Geometry.SourceMaskDistanceMm;
        double resolution = baseConfig.Mask.CellPitchMm * (d + s) / d;
        // Sweep radius adapts to the config's FCFOV so wide-FOV masks reveal collimation.
        double maxRadiusMm = 0.95 * baseConfig.Mask.Rank * baseConfig.Mask.CellPitchMm * (d + s) / d / 2.0;

        Console.WriteLine($"Tungsten thickness optimization (mu={baseConfig.Mask.LinearAttenuationPerMm}/mm)");
        Console.WriteLine($"Fixed budget {photonBudget:E1} emitted photons, radial to {maxRadiusMm} mm, {repeats} Poisson reps/point");
        Console.WriteLine($"Localizable = median error < resolution ({resolution:F1} mm)");
        Console.WriteLine();

        var rows = new ThicknessStudy(new DefaultSimulationFactory())
            .Run(baseConfig, thicknesses, maxRadiusMm, nRadial, photonBudget, repeats, failFraction);
        File.WriteAllText(csvPath, ThicknessStudy.ToCsv(rows, resolution));

        Console.WriteLine("  thick   usableFOV   eff@center   eff@edge   leak%");
        Console.WriteLine("  -----  ---------  ----------  ---------  ------");
        foreach (var r in rows)
        {
            double leak = Math.Exp(-baseConfig.Mask.LinearAttenuationPerMm * r.ThicknessMm) * 100.0;
            Console.WriteLine($"  {r.ThicknessMm,4:F0}mm  ±{r.UsableRadiusMm,6:F1}mm  {r.EffCenter,10:E2}  {r.EffEdge,9:E2}  {leak,5:F1}%");
        }

        var best = rows.OrderByDescending(r => r.UsableRadiusMm).First();
        Console.WriteLine();
        Console.WriteLine($"BEST thickness: {best.ThicknessMm} mm -> usable FOV ±{best.UsableRadiusMm:F1} mm");
        Console.WriteLine($"CSV written: {csvPath}");
        return 0;
    }

    internal static int RunUniformity(string[] args)
    {
        if (args.Length < 2)
        {
            Console.Error.WriteLine("Usage: montecarlo uniformity <base.json> [out.csv]");
            return 1;
        }

        double[] levels = [0.0, 0.25, 0.5, 0.75, 1.0, 1.5, 2.0];
        const double photonBudget = 800_000.0;
        const int repeats = 120;
        const double failThresholdMm = 3.0;
        string csvPath = args.Length >= 3 ? args[2] : "samples/uniformity.csv";

        var baseConfig = ConfigLoader.Load(args[1]);
        // Activate a baseline energy-resolution non-uniformity (8% FWHM ±30%, ±10% window)
        // so both mechanisms (gain + resolution) contribute to the flood distortion.
        baseConfig.Detector.EnergyResolutionFwhm = 0.08;
        baseConfig.Detector.EnergyResolutionFwhmSigma = 0.30;
        baseConfig.Detector.EnergyWindowFraction = 0.10;

        Console.WriteLine($"Crystal uniformity study: non-uniformity sweep (random gain σ=0.4·L + gradient 0.6·L)");
        Console.WriteLine($"{repeats} Poisson reps/point, budget {photonBudget:E1}, centered source");
        Console.WriteLine("Comparing raw decoding vs flood-corrected (÷ calibrated sensitivity).");
        Console.WriteLine();

        var rows = new UniformityStudy(new DefaultSimulationFactory())
            .Run(baseConfig, levels, photonBudget, repeats, failThresholdMm);
        File.WriteAllText(csvPath, UniformityStudy.ToCsv(rows));

        Console.WriteLine("  level  gainσ  gradient   RMS raw    RMS corrected   fail raw   fail corr");
        Console.WriteLine("  -----  -----  --------   ---------  -------------   --------   ---------");
        foreach (var r in rows)
            Console.WriteLine($"  {r.Level,5:F2}  {0.40 * r.Level,5:P0}  {0.60 * r.Level,7:P0}   {r.RmsRawMm,7:F2}mm   {r.RmsCorrectedMm,9:F2}mm      {r.FailRaw,6:P0}     {r.FailCorrected,6:P0}");

        Console.WriteLine();
        Console.WriteLine($"CSV written: {csvPath}");
        return 0;
    }

    internal static int RunArray(string[] args)
    {
        if (args.Length < 2)
        {
            Console.Error.WriteLine("Usage: montecarlo array <base.json> [out.csv]");
            return 1;
        }

        const double physicalSizeMm = 12.0;   // fixed detector size
        double[] pitches = [2.0, 1.5, 1.0, 0.75, 0.6, 0.5, 0.4];
        const double photonBudget = 300_000.0;
        const int repeats = 250;
        const double failThresholdMm = 3.0;
        string csvPath = args.Length >= 3 ? args[2] : "samples/array.csv";

        var baseConfig = ConfigLoader.Load(args[1]);

        Console.WriteLine($"Detector array sweep: fixed {physicalSizeMm} mm size, varying pixel pitch, {repeats} Poisson reps");
        Console.WriteLine($"Budget {photonBudget:E1}, centered source. (mask cell shadow spans 'samples/cell' pixels)");
        Console.WriteLine();

        var rows = new ArrayStudy(new DefaultSimulationFactory())
            .Run(baseConfig, physicalSizeMm, pitches, photonBudget, repeats, failThresholdMm);
        File.WriteAllText(csvPath, ArrayStudy.ToCsv(rows));

        Console.WriteLine("  pitch   array    samples/cell   RMS err   fail%");
        Console.WriteLine("  -----  -------  ------------   -------   -----");
        foreach (var r in rows)
            Console.WriteLine($"  {r.PixelPitchMm,4:F2}mm  {r.Pixels,2}x{r.Pixels,-2}   {r.SamplesPerCellShadow,8:F2}     {r.RmsMm,6:F2}mm   {r.FailRate,5:P0}");

        var best = rows.Where(r => !double.IsNaN(r.RmsMm)).OrderBy(r => r.RmsMm).First();
        Console.WriteLine();
        Console.WriteLine($"BEST: {best.PixelPitchMm} mm pitch ({best.Pixels}x{best.Pixels}), {best.SamplesPerCellShadow:F1} samples/cell, RMS {best.RmsMm:F2} mm");
        Console.WriteLine($"CSV written: {csvPath}");
        return 0;
    }
}
