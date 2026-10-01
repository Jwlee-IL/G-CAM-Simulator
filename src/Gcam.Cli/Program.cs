using System.Text;
using Gcam.Configuration;
using Gcam.Core;
using Gcam.Detector;
using Gcam.Simulation;

var commands = new Dictionary<string, Func<string[], int>>(StringComparer.OrdinalIgnoreCase)
{
    ["sweep"]          = RunSweep,
    ["fov"]            = RunFieldOfView,
    ["scan"]           = RunScan,
    ["noise"]          = RunNoise,
    ["thickness"]      = RunThickness,
    ["uniformity"]     = RunUniformity,
    ["array"]          = RunArray,
    ["maskgeo"]        = RunMaskGeo,
    ["masksize"]       = RunMaskSize,
    ["masktaper"]      = RunMaskTaper,
    ["maskfab"]        = RunMaskFab,
    ["masksec"]        = RunMaskSecondary,
    ["maskscatter"]    = RunMaskScatter,
    ["align"]          = RunAlign,
    ["mlem"]           = RunMlem,
    ["subcell"]        = RunSubCell,
    ["antimask"]       = RunAntimask,
    ["antimask-scene"] = RunAntimaskScene,
    ["compton"]        = RunCompton,
    ["compton-strip"]  = RunComptonStrip,
    ["mixedfield"]     = RunMixedField,
    ["mixediso"]       = RunMixedIso,
    ["mixedstrip"]     = RunMixedStrip,
    ["cascade"]        = RunCascade,
    ["nonprop"]        = RunNonProp,
    ["depth"]          = RunDepth,
    ["depth-joint"]    = RunDepthJoint,
    ["depth3d"]        = RunDepth3D,
    ["depthdesign"]    = RunDepthDesign,
    ["doi"]            = RunDoi,
    ["background"]     = RunBackground,
    ["shield"]         = RunShield,
    ["finitesrc"]      = RunFiniteSource,
    ["frontend"]       = RunFrontend,
    ["eventstream"]    = RunEventStream,
    ["thermal"]        = RunThermal,
    ["thermalro"]      = RunThermalReadout,
    ["pileup"]         = RunPileUp,
    ["deadtime"]       = RunDeadTime,
    ["defects"]        = RunDefects,
};

if (args.Length < 1 || args[0] is "help" or "--help" or "-h" or "/?")
{
    PrintUsage(commands);
    return args.Length < 1 ? 1 : 0;
}

if (commands.TryGetValue(args[0], out var handler))
    return handler(args);

// Not a known command — the only remaining valid form is a scenario file path.
// Guard against a mistyped sub-command falling through to a cryptic file-load error.
if (!File.Exists(args[0]))
{
    Console.Error.WriteLine($"Unknown command or scenario file: '{args[0]}'");
    Console.Error.WriteLine("Run 'montecarlo help' to list available commands.");
    return 1;
}

var config = ConfigLoader.Load(args[0]);
Console.WriteLine($"Scenario : {config.Name}");
Console.WriteLine($"Isotope  : {config.Source.Isotope} @ {config.Source.EnergyKeV} keV");
Console.WriteLine($"Source   : (x={config.Source.Position[0]}, y={config.Source.Position[1]}) mm off-axis");
Console.WriteLine($"Mask     : {config.Mask.Type} rank {config.Mask.Rank}, {config.Mask.MosaicX}x{config.Mask.MosaicY} mosaic @ z={config.Geometry.MaskDetectorDistanceMm} mm");
Console.WriteLine($"Detector : {config.Detector.PixelsX}x{config.Detector.PixelsY} crystals @ z=0");
Console.WriteLine($"Photons  : {config.PhotonCount:N0}");
Console.WriteLine();

var runner = new SimulationRunner(new DefaultSimulationFactory());
var result = runner.Run(config);

double efficiency = result.PhotonsEmitted > 0 ? result.DetectedWeight / result.PhotonsEmitted : 0.0;
Console.WriteLine($"Emitted  : {result.PhotonsEmitted:N0}  ({(config.Source.DirectionalBiasing ? "detector-biased" : "4π isotropic")})");
Console.WriteLine($"Detected : {result.DetectedWeight:N1} effective counts   (efficiency {efficiency:E2})");
Console.WriteLine();
Console.WriteLine("Flood map (detector, +y up):");
RenderFloodMap(result.DetectorImage);

if (result.Estimate is { } est && result.Reconstruction is { } recon)
{
    double trueX = config.Source.Position[0];
    double trueY = config.Source.Position[1];
    double err = Math.Sqrt((est.Position.X - trueX) * (est.Position.X - trueX) +
                           (est.Position.Y - trueY) * (est.Position.Y - trueY));

    Console.WriteLine();
    Console.WriteLine($"True     : (x={trueX:F1}, y={trueY:F1}) mm");
    Console.WriteLine($"Estimate : (x={est.Position.X:F1}, y={est.Position.Y:F1}) mm   error={err:F1} mm");
    Console.WriteLine($"Ghost margin (primary/secondary peak): {est.Confidence:F2}   (>~1.5 clean, ~1 ambiguous)");
    Console.WriteLine();
    Console.WriteLine("Reconstruction (source plane, +y up)   T=truth  o=estimate:");
    RenderReconstruction(recon, result.ReconOriginMm, result.ReconStepMm, trueX, trueY, est.Position.X, est.Position.Y);
}
return 0;

static void PrintUsage(Dictionary<string, Func<string[], int>> commands)
{
    Console.WriteLine("montecarlo - coded-aperture gamma-source localization Monte Carlo");
    Console.WriteLine();
    Console.WriteLine("Usage:");
    Console.WriteLine("  montecarlo <scenario.json>            run a single scenario (flood map + recon + estimate)");
    Console.WriteLine("  montecarlo <command> <base.json> ...  run a study (writes CSV/PNG outputs)");
    Console.WriteLine("  montecarlo help                       show this list");
    Console.WriteLine();
    Console.WriteLine("Commands (see AGENTS.md for details on each):");
    Console.WriteLine("  core / config   sweep  fov  scan  noise  thickness  uniformity  array");
    Console.WriteLine("  mask            maskgeo  masksize  masktaper  maskfab  masksec  maskscatter  align");
    Console.WriteLine("  decoding        mlem  subcell  antimask  antimask-scene");
    Console.WriteLine("  compton / iso   compton  compton-strip  mixedfield  mixediso  mixedstrip  cascade  nonprop");
    Console.WriteLine("  depth           depth  depth-joint  depth3d  depthdesign  doi");
    Console.WriteLine("  scene / bg      background  shield  finitesrc");
    Console.WriteLine("  front-end/RTL   frontend  eventstream  thermal  thermalro  pileup  deadtime  defects");
    Console.WriteLine();
    Console.WriteLine($"  ({commands.Count} study commands total.)");
}

static int RunFieldOfView(string[] args)
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

static int RunSweep(string[] args)
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

static int RunMixedStrip(string[] args)
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

static int RunMixedIso(string[] args)
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

static int RunMixedField(string[] args)
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

static int RunMaskTaper(string[] args)
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

static int RunShield(string[] args)
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

static int RunMaskSize(string[] args)
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

static int RunMaskGeo(string[] args)
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

static int RunDepth3D(string[] args)
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

static int RunDepthJoint(string[] args)
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

static int RunDepth(string[] args)
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

static int RunComptonStrip(string[] args)
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

static int RunEventStream(string[] args)
{
    if (args.Length < 2) { Console.Error.WriteLine("Usage: montecarlo eventstream <base.json> [rateKcps] [maxEvents] [bgRatio]"); return 1; }
    var baseConfig = ConfigLoader.Load(args[1]);
    double rateKcps = args.Length > 2 ? double.Parse(args[2]) : 500.0;
    int maxEvents = args.Length > 3 ? int.Parse(args[3]) : 1500;
    const double adcSampleRateHz = 100e6;   // 100 MSPS front-end (Ts = 10 ns; trap_ref tau = 5 samples = 50 ns)

    // A background ratio on the command line opts this run into an ambient field (overrides the config's
    // Background); without it the config's Background (if any) is used, else the stream is clean.
    if (args.Length > 4)
    {
        double bgRatio = double.Parse(args[4]);
        baseConfig.Background = bgRatio > 0.0
            ? new BackgroundConfig { BackgroundToSignalRatio = bgRatio, EnergyKeV = baseConfig.Background?.EnergyKeV ?? 200.0 }
            : null;
    }

    var study = new EventStreamStudy();
    var events = study.Generate(baseConfig, rateKcps * 1e3, adcSampleRateHz, maxEvents);
    var (count, minKeV, maxKeV, meanGap) = EventStreamStudy.Summary(events);

    Directory.CreateDirectory("rtl");
    string path = "rtl/event_stream.txt";
    File.WriteAllText(path, EventStreamStudy.ToText(events, rateKcps * 1e3, adcSampleRateHz, baseConfig.Name));

    Console.WriteLine("MC -> RTL event stream (crystal-Compton deposits + Poisson arrival overlay)");
    Console.WriteLine($"  Field    : {baseConfig.Source.Isotope} @ {baseConfig.Source.EnergyKeV} keV" +
        (baseConfig.Sources is { Length: > 0 } ss ? $"  (+{ss.Length - 1} more sources / mixed field)" : ""));
    Console.WriteLine($"  Rate     : {rateKcps:F0} kcps @ {adcSampleRateHz / 1e6:F0} MSPS  ->  mean gap {meanGap:F0} samples");
    if (baseConfig.Background is { BackgroundToSignalRatio: > 0.0 } bgc)
        Console.WriteLine($"  Backgrd  : BSR {bgc.BackgroundToSignalRatio:F2} @ {bgc.EnergyKeV:F0} keV" +
            (bgc.DarkCountRateKcps is > 0.0 ? $" + DCR {bgc.DarkCountRateKcps:F0} kcps" : "") + "  (uncoded ambient events merged in)");
    Console.WriteLine($"  Events   : {count}   deposited energy {minKeV:F0}..{maxKeV:F0} keV (photopeak + Compton continuum" +
        (baseConfig.Background is { BackgroundToSignalRatio: > 0.0 } ? " + background)" : ")"));
    Console.WriteLine($"  Written  : {path}");
    Console.WriteLine();
    Console.WriteLine("  Drive the RTL shaper with it:  cd rtl && python run_cocotb.py   (mc_event_stream_matches_reference)");
    Console.WriteLine("  Recovery / pile-up analysis :  cd rtl && python event_stream_study.py  -> event_stream.png");
    return 0;
}

static int RunDepthDesign(string[] args)
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

static int RunFrontend(string[] args)
{
    // Energy resolution from the photoelectron budget — the C# FrontEndModel (port of rtl/frontend_model.py),
    // now folded into the pipeline (the crystal-Compton detector smears each deposit by this 1/√E FWHM).
    Console.WriteLine("SiPM front-end — energy resolution from the photoelectron budget (collection 0.60, PDE 0.45, ENF 1.20)");
    Console.WriteLine();
    Console.WriteLine("  crystal   LY    N_pe@662   R_stat   R_intr   R_tot@662   R_tot@1332   regime");
    Console.WriteLine("  -------   --   --------   ------   ------   ---------   ----------   ------");
    // name: (light yield ph/keV, intrinsic FWHM fraction)
    (string name, double ly, double intr)[] crystals =
    {
        ("GAGG", 50, 0.050), ("CeBr3", 45, 0.032), ("LaBr3", 63, 0.022),
        ("LYSO", 30, 0.070), ("BGO", 9, 0.080), ("NaI", 38, 0.055),
    };
    foreach (var (name, ly, intr) in crystals)
    {
        var m = new FrontEndModel(new FrontEndConfig
        {
            LightYieldPhPerKeV = ly, CollectionEfficiency = 0.60, SipmPde = 0.45,
            ExcessNoiseFactor = 1.20, IntrinsicResolutionFwhm = intr,
        });
        double npe = m.Photoelectrons(662.0);
        double rStat = System.Math.Sqrt(System.Math.Max(0.0, m.FwhmFraction(662.0) * m.FwhmFraction(662.0) - intr * intr));
        string regime = intr > rStat ? "crystal-limited" : "photon-limited";
        Console.WriteLine($"  {name,-6}   {ly,2:F0}   {npe,8:F0}   {rStat,5:P1}   {intr,5:P1}   {m.FwhmFraction(662.0),8:P1}    {m.FwhmFraction(1332.0),8:P1}    {regime}");
    }
    Console.WriteLine();
    Console.WriteLine("R_stat = 2.355·√(ENF/N_pe) scales 1/√E; past the knee the crystal's intrinsic floor dominates");
    Console.WriteLine("(more PDE wasted). Enable in a scenario via detector.frontEnd — the Compton detector then");
    Console.WriteLine("smears each deposit by R_tot(E), so energy windows / isotope separation reflect real resolution.");
    Console.WriteLine();

    // DCR as background: dark counts add a PARALLEL-noise term that scales 1/E (worse at low energy).
    const double dcrHz = 1.0e6, tauNs = 200.0;   // ~1 Mcps SiPM (S13360-3050CS class), 200 ns integration
    var clean = new FrontEndModel(new FrontEndConfig { LightYieldPhPerKeV = 45, IntrinsicResolutionFwhm = 0.032 });
    var withDcr = new FrontEndModel(new FrontEndConfig { LightYieldPhPerKeV = 45, IntrinsicResolutionFwhm = 0.032,
        DarkCountRateHz = dcrHz, IntegrationTimeNs = tauNs });
    Console.WriteLine($"SiPM dark counts as background (CeBr3, DCR {dcrHz / 1e6:F1} Mcps, {tauNs:F0} ns window):");
    Console.WriteLine("  energy    R_tot(no DCR)   R_tot(+DCR)   R_dcr alone   (DCR is a 1/E parallel-noise term)");
    foreach (double e in new[] { 122.0, 662.0, 1332.0 })
        Console.WriteLine($"  {e,5:F0} keV   {clean.FwhmFraction(e),10:P2}    {withDcr.FwhmFraction(e),9:P2}    {withDcr.DcrFwhmFraction(e),9:P3}");
    Console.WriteLine("  -> negligible at the photopeak, grows toward low energy: DCR matters for weak / low-line sources.");
    return 0;
}

static int RunBackground(string[] args)
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

static int RunCompton(string[] args)
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

static int RunAntimaskScene(string[] args)
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

static int RunAntimask(string[] args)
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

static int RunArray(string[] args)
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

static int RunUniformity(string[] args)
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

static int RunThermal(string[] args)
{
    if (args.Length < 2)
    {
        Console.Error.WriteLine("Usage: montecarlo thermal <base.json> [out_prefix]");
        Console.Error.WriteLine("  Models SiPM gain drift DURING an acquisition (ambient + self-heating) and");
        Console.Error.WriteLine("  its effect on a per-crystal photopeak-window system, bias-comp OFF vs ON.");
        return 1;
    }

    string prefix = args.Length >= 3 ? args[2] : "samples/thermal";
    const double photonBudget = 800_000.0;
    const int repeats = 60;

    var baseConfig = ConfigLoader.Load(args[1]);
    // Activate a per-crystal photopeak window + static non-uniformity so the drift has something to walk in.
    if (baseConfig.Detector.EnergyResolutionFwhm <= 0.0) baseConfig.Detector.EnergyResolutionFwhm = 0.08;
    if (baseConfig.Detector.EnergyWindowFraction <= 0.0) baseConfig.Detector.EnergyWindowFraction = 0.10;
    if (baseConfig.Detector.EnergyResolutionFwhmSigma <= 0.0) baseConfig.Detector.EnergyResolutionFwhmSigma = 0.30;
    if (baseConfig.Detector.GainSigma <= 0.0) baseConfig.Detector.GainSigma = 0.05;
    if (baseConfig.Detector.GainGradient <= 0.0) baseConfig.Detector.GainGradient = 0.10;

    int W = baseConfig.Detector.PixelsX, H = baseConfig.Detector.PixelsY;
    int n = 13;
    var times = new double[n];
    for (int i = 0; i < n; i++) times[i] = (double)i / (n - 1);

    // Physical drift: SiPM gain −0.7%/°C, ambient +3°C linear over the run, self-heating +8°C centre hotspot
    // (τ 0.3, corner 40% of centre). Compare an uncompensated array with a 90%-effective bias-comp loop.
    ThermalDrift Make(double comp) => new ThermalDrift(W, H,
        alphaPerC: -0.007, biasCompFraction: comp,
        ambientRatePerT: 3.0, ambientSwingC: 0.0, ambientPeriod: 0.0,
        selfHeatC: 8.0, selfHeatTau: 0.3, edgeFactor: 0.4);

    var study = new ThermalDriftStudy(new DefaultSimulationFactory());
    var off = study.Run(baseConfig, Make(0.0), times, photonBudget, repeats);
    var on = study.Run(baseConfig, Make(0.9), times, photonBudget, repeats);

    File.WriteAllText(prefix + "_off.csv", ThermalDriftStudy.ToCsv(off));
    File.WriteAllText(prefix + "_on.csv", ThermalDriftStudy.ToCsv(on));

    Console.WriteLine("Thermal-drift study: SiPM gain -0.7%/C, ambient +3C linear + self-heating +8C hotspot (tau 0.3).");
    Console.WriteLine("Per-crystal photopeak window; flood correction uses the t=0 calibration map.");
    Console.WriteLine();
    Console.WriteLine("                  bias-comp OFF                    bias-comp ON (90%)");
    Console.WriteLine("  time  ctrDeg   eff    resid%   bias    |   eff    resid%   bias");
    Console.WriteLine("  ----  ------   ----   ------   -----   |   ----   ------   -----");
    for (int i = 0; i < n; i++)
    {
        var f = off[i]; var o = on[i];
        Console.WriteLine($"  {f.Time,4:F2}  {f.CenterDeltaC,5:F1}   {f.Efficiency,5:P1} {f.ResidualCoV,7:P1} {f.RmsBiasMm,6:F2}mm  | " +
                          $" {o.Efficiency,5:P1} {o.ResidualCoV,7:P1} {o.RmsBiasMm,6:F2}mm");
    }
    Console.WriteLine();
    Console.WriteLine($"End of acquisition: OFF loses {1 - off[^1].Efficiency,4:P1} of photopeak counts, residual {off[^1].ResidualCoV,4:P1};");
    Console.WriteLine($"                    ON  holds {on[^1].Efficiency,4:P1} efficiency, residual {on[^1].ResidualCoV,4:P1}.");
    Console.WriteLine("Drift is an ENERGY-WINDOW (efficiency) problem, not a localization one: the bias tracks the decoder floor.");
    Console.WriteLine($"CSV written: {prefix}_off.csv, {prefix}_on.csv");
    return 0;
}

static int RunDeadTime(string[] args)
{
    if (args.Length < 2)
    {
        Console.Error.WriteLine("Usage: montecarlo deadtime <base.json> [out.csv]");
        Console.Error.WriteLine("  Sweeps the true count rate vs the recorded rate under non-paralyzable and");
        Console.Error.WriteLine("  paralyzable dead time (MC event stream vs analytic), plus the live fraction.");
        return 1;
    }

    string csvPath = args.Length >= 3 ? args[2] : "samples/deadtime.csv";
    const double adcSampleRateHz = 125e6;
    const int maxEvents = 200_000;
    const double deadTimeUs = 1.0;                    // DAQ per-pulse processing dead time
    double[] rates = [1e4, 5e4, 1e5, 3e5, 6e5, 1e6, 2e6, 5e6, 1e7];

    var baseConfig = ConfigLoader.Load(args[1]);
    var rows = new DeadTimeStudy().Run(baseConfig, rates, deadTimeUs, adcSampleRateHz, maxEvents);
    File.WriteAllText(csvPath, DeadTimeStudy.ToCsv(rows));

    Console.WriteLine($"Dead-time study: τ = {deadTimeUs:F1} µs (1/τ = {1e6 / deadTimeUs:N0} cps), {maxEvents:N0} events/rate.");
    Console.WriteLine("Recorded rate under non-paralyzable m=R/(1+Rτ) and paralyzable m=R·exp(−Rτ); MC vs analytic.");
    Console.WriteLine();
    Console.WriteLine("   true(cps)   R·τ    non-para(MC/an)      para(MC/an)        live np/p");
    Console.WriteLine("   ---------   ----   ----------------     ----------------   ---------");
    foreach (var r in rows)
        Console.WriteLine($"   {r.TrueRateCps,9:N0}   {r.Rtau,4:F2}   {r.RecordedNonParaCps,7:N0}/{r.AnalyticNonParaCps,-7:N0}  " +
                          $"{r.RecordedParaCps,7:N0}/{r.AnalyticParaCps,-7:N0}  {r.LiveNonPara,4:P0}/{r.LivePara,-4:P0}");

    Console.WriteLine();
    Console.WriteLine("Non-paralyzable saturates toward 1/τ; paralyzable PEAKS at R=1/τ then collapses (paralysis).");
    Console.WriteLine("Live fraction = recorded/true = the live-time vs real-time correction a real acquisition applies.");
    Console.WriteLine($"CSV written: {csvPath}");
    return 0;
}

static int RunDoi(string[] args)
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

static int RunThermalReadout(string[] args)
{
    if (args.Length < 2)
    {
        Console.Error.WriteLine("Usage: montecarlo thermalro <base.json> [out.csv]");
        Console.Error.WriteLine("  Thermal DCR/PDE readout effects (beyond the gain-centroid drift): dark-count rate,");
        Console.Error.WriteLine("  low-energy vs photopeak resolution, and PDE droop vs temperature deviation.");
        return 1;
    }

    string csvPath = args.Length >= 3 ? args[2] : "samples/thermalro.csv";
    var cfg = ConfigLoader.Load(args[1]);
    var fe = cfg.Detector.FrontEnd ?? new FrontEndConfig();
    if (!(fe.DarkCountRateHz > 0)) fe = new FrontEndConfig
    {
        LightYieldPhPerKeV = fe.LightYieldPhPerKeV, CollectionEfficiency = fe.CollectionEfficiency,
        SipmPde = fe.SipmPde, ExcessNoiseFactor = fe.ExcessNoiseFactor,
        IntrinsicResolutionFwhm = fe.IntrinsicResolutionFwhm, DarkCountRateHz = 1.0e6, IntegrationTimeNs = 300,
    };
    var thermal = new ThermalDrift(cfg.Detector.PixelsX, cfg.Detector.PixelsY);
    double[] dts = [0, 4, 8, 12, 16, 20, 24, 30];
    var rows = new ThermalReadoutStudy().Run(fe, thermal, dts, lowLineKeV: 60.0, photopeakKeV: 661.7);
    File.WriteAllText(csvPath, ThermalReadoutStudy.ToCsv(rows));

    Console.WriteLine($"Thermal readout study: DCR₀ = {fe.DarkCountRateHz / 1e6:F1} Mcps, doubling every {thermal.DcrDoublingC:F0} °C.");
    Console.WriteLine("Bias compensation nulls the GAIN drift (theme 36) but NOT the thermal dark generation.");
    Console.WriteLine();
    Console.WriteLine("   ΔT(°C)   DCR(Mcps)   res@60keV   res@662keV   PDE");
    Console.WriteLine("   ------   ---------   ---------   ----------   ----");
    foreach (var r in rows)
        Console.WriteLine($"   {r.DeltaTC,5:F0}    {r.DcrHz / 1e6,8:F2}    {r.ResLowEPct,7:F2}%    {r.ResPhotopeakPct,7:F2}%   {r.PdeFactor:F3}");
    var c = rows[0]; var h = rows[^1];
    Console.WriteLine();
    Console.WriteLine($"Over {h.DeltaTC:F0} °C: DCR ×{h.DcrHz / c.DcrHz:F0}, PDE {(h.PdeFactor - 1) * 100:F0}%. The DCR term is ∝1/E, so the");
    Console.WriteLine($"low line degrades ~2× more (relatively) than the photopeak — but modestly for this high-light-yield");
    Console.WriteLine("crystal (statistics-dominated). The dominant effect is the raw DCR growth = a dark trigger / pile-up load.");
    Console.WriteLine($"CSV written: {csvPath}");
    return 0;
}

static int RunMlem(string[] args)
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

static int RunFiniteSource(string[] args)
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

static int RunNonProp(string[] args)
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

static int RunMaskScatter(string[] args)
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

static int RunCascade(string[] args)
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

static int RunSubCell(string[] args)
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

static int RunMaskSecondary(string[] args)
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

static int RunDefects(string[] args)
{
    if (args.Length < 2)
    {
        Console.Error.WriteLine("Usage: montecarlo defects <base.json> [out.csv]");
        Console.Error.WriteLine("  Sweeps bad (dead/hot) pixel fraction vs an IDEAL decoder: raw flood vs a known");
        Console.Error.WriteLine("  bad-pixel-map repair (interpolate the flagged pixels).");
        return 1;
    }

    string csvPath = args.Length >= 3 ? args[2] : "samples/defects.csv";
    const double photonBudget = 800_000.0;
    const int repeats = 60;
    const double deadShare = 0.6;    // dead pixels are more common than hot
    const double hotFactor = 5.0;    // a hot pixel adds 5× the mean live-pixel counts
    double[] badPct = [0.0, 1.0, 2.0, 4.0, 8.0];

    var baseConfig = ConfigLoader.Load(args[1]);

    Console.WriteLine("Detector bad-pixel study: dead (holes) + hot (spikes) pixels vs the IDEAL decoder.");
    Console.WriteLine($"dead share {deadShare:P0}, hot pixel = {hotFactor:F0}× mean level; {repeats} Poisson reps, " +
                      $"averaged over defect maps. Repair = interpolate the known bad-pixel map.");
    Console.WriteLine();

    var rows = new DetectorDefectStudy(new DefaultSimulationFactory())
        .Run(baseConfig, badPct, deadShare, hotFactor, photonBudget, repeats, seeds: 8);
    File.WriteAllText(csvPath, DetectorDefectStudy.ToCsv(rows));

    Console.WriteLine("   bad%   dead%   hot%   RMS raw    RMS repaired");
    Console.WriteLine("   ----   -----   ----   --------   ------------");
    foreach (var r in rows)
        Console.WriteLine($"   {r.BadPixelPct,4:F1}   {r.DeadPct,5:F2}   {r.HotPct,4:F2}   {r.RmsRawMm,7:F2}mm   {r.RmsCorrMm,9:F2}mm");

    Console.WriteLine();
    Console.WriteLine("Bad pixels imprint fixed non-coded structure that pulls the correlation peak; a known bad-pixel");
    Console.WriteLine("map repairs most of it by interpolation (the discrete analogue of flood-field correction).");
    Console.WriteLine($"CSV written: {csvPath}");
    return 0;
}

static int RunAlign(string[] args)
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

static int RunMaskFab(string[] args)
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

static int RunPileUp(string[] args)
{
    if (args.Length < 2)
    {
        Console.Error.WriteLine("Usage: montecarlo pileup <base.json> [out.csv]");
        Console.Error.WriteLine("  Random-coincidence pile-up: overlapping pulses SUM into one recorded event,");
        Console.Error.WriteLine("  adding a self-convolution continuum above the photopeak + a throughput loss.");
        return 1;
    }

    string csvPath = args.Length >= 3 ? args[2] : "samples/pileup.csv";
    const double adcSampleRateHz = 125e6;               // AD9648 125 MSPS (matches the WPF chain)
    const int maxEvents = 150_000;
    double[] rates = [50e3, 200e3, 500e3, 1e6, 2e6];    // detected count rates (cps)

    // Pulse-pair resolving time from the (default) shaper pulse: rise + 2·tail. Faster shaper → less pile-up.
    double tauRes = EventStreamStudy.ResolvingSamples(Waveform.TauRiseSamples, Waveform.TauSamples);
    double tauResNs = tauRes / adcSampleRateHz * 1e9;

    var baseConfig = ConfigLoader.Load(args[1]);
    double primary = baseConfig.Source.EnergyKeV > 0 ? baseConfig.Source.EnergyKeV : 661.7;
    const double maxE = 1500.0;
    const int bins = 750;                               // 2 keV/bin, spans up to the 2× sum region
    double bw = maxE / bins;

    static double[] Hist(IEnumerable<double> es, double bw, int bins)
    {
        var h = new double[bins];
        foreach (double e in es) { int b = (int)(e / bw); if (b >= 0 && b < bins) h[b] += 1; }
        return h;
    }

    var stream0 = new EventStreamStudy();
    // Singles reference: rate is irrelevant to the singles ENERGY spectrum (only its time spacing), so build once.
    var singlesStream = stream0.Generate(baseConfig, rates[0], adcSampleRateHz, maxEvents);
    double[] singlesE = singlesStream.Select(e => e.EnergyKeV).ToArray();
    double[] singlesHist = Hist(singlesE, bw, bins);
    double photopeakLo = primary * 0.90, sumLo = primary * 1.30;   // photopeak window / above-line sum region
    double SingPk = singlesE.Count(e => e >= photopeakLo && e <= primary * 1.10);

    Console.WriteLine($"Pile-up study: peak pile-up on the MC event stream, resolving time {tauRes:F0} samples " +
                      $"({tauResNs:F0} ns @ {adcSampleRateHz / 1e6:F0} MSPS, from shaper rise {Waveform.TauRiseSamples:F0}+2·tail {Waveform.TauSamples:F0}).");
    Console.WriteLine($"Primary line {primary:F0} keV; {singlesE.Length} detected events per rate.");
    Console.WriteLine();
    Console.WriteLine("   rate(cps)   R·τ     throughput   photopeak_kept   above-line(sum)%");
    Console.WriteLine("   ---------   -----   ----------   --------------   ----------------");

    double[]? piledShowcaseHist = null;
    double showcaseRate = rates[^2];   // 1 Mcps: clear continuum but not absurd
    foreach (double rate in rates)
    {
        var stream = stream0.Generate(baseConfig, rate, adcSampleRateHz, maxEvents);
        var piled = EventStreamStudy.ApplyPileUp(stream, tauRes);
        double[] piledE = piled.Select(e => e.EnergyKeV).ToArray();

        double throughput = (double)piled.Count / stream.Count;
        double pk = piledE.Count(e => e >= photopeakLo && e <= primary * 1.10);
        double aboveLine = piledE.Count(e => e > sumLo);
        double rtau = rate * tauRes / adcSampleRateHz;

        Console.WriteLine($"   {rate,9:N0}   {rtau,5:F3}   {throughput,10:P1}   {pk / SingPk,14:P1}   {aboveLine / piledE.Length,16:P2}");

        if (rate == showcaseRate) piledShowcaseHist = Hist(piledE, bw, bins);
    }

    // CSV: singles vs the showcase-rate piled spectrum (per 2-keV bin) for plotting / autoconvolution cross-check.
    var sb = new StringBuilder();
    sb.AppendLine($"# pileup spectrum  primary={primary:F1}keV  resolving_samples={tauRes:F1}  showcase_rate_cps={showcaseRate:G3}");
    sb.AppendLine("energy_keV,singles,piled");
    for (int i = 0; i < bins; i++)
        sb.AppendLine($"{(i + 0.5) * bw:F1},{singlesHist[i]:F0},{(piledShowcaseHist?[i] ?? 0):F0}");
    File.WriteAllText(csvPath, sb.ToString());

    Console.WriteLine();
    Console.WriteLine($"Above the {primary:F0} keV line, pile-up builds a self-convolution continuum up to the " +
                      $"{2 * primary:F0} keV sum peak; higher rate migrates more counts out of the photopeak.");
    Console.WriteLine($"CSV written: {csvPath} (singles vs {showcaseRate / 1e6:F1} Mcps piled, 2 keV bins).");
    return 0;
}

static int RunThickness(string[] args)
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

static int RunNoise(string[] args)
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

static int RunScan(string[] args)
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

static void RenderGhostMap(SweepResult sweep, double threshold)
{
    int c = sweep.N / 2;
    for (int iy = sweep.N - 1; iy >= 0; iy--)
    {
        var sb = new StringBuilder();
        for (int ix = 0; ix < sweep.N; ix++)
        {
            char ch = ix == c && iy == c ? '+' : sweep.At(ix, iy).ErrorMm < threshold ? '.' : '#';
            sb.Append(ch).Append(ch);
        }
        Console.WriteLine(sb.ToString());
    }
}

static void RenderReconstruction(DetectorImage recon, double origin, double step,
                                 double trueX, double trueY, double estX, double estY)
{
    const string ramp = " .:-=+*#%@";
    double min = double.PositiveInfinity, max = double.NegativeInfinity;
    foreach (var v in recon.Raw) { if (v < min) min = v; if (v > max) max = v; }
    double span = max - min;
    if (span <= 0) { Console.WriteLine("(flat)"); return; }

    int Idx(double phys) => (int)Math.Round((phys - origin) / step);
    bool InGrid(int gx, int gy) => gx >= 0 && gx < recon.Width && gy >= 0 && gy < recon.Height;
    int tGx = Idx(trueX), tGy = Idx(trueY), eGx = Idx(estX), eGy = Idx(estY);

    for (int y = recon.Height - 1; y >= 0; y--)
    {
        var sb = new StringBuilder();
        for (int x = 0; x < recon.Width; x++)
        {
            if (x == eGx && y == eGy) sb.Append('o');
            else if (x == tGx && y == tGy) sb.Append('T');
            else sb.Append(ramp[(int)((recon[x, y] - min) / span * (ramp.Length - 1))]);
        }
        Console.WriteLine(sb.ToString());
    }

    if (!InGrid(tGx, tGy))
        Console.WriteLine("(T off-grid: true source is outside the FCFOV — expect a ghost)");
}

static void RenderFloodMap(DetectorImage img)
{
    const string ramp = " .:-=+*#%@";
    double max = 0.0;
    foreach (var v in img.Raw)
        if (v > max) max = v;

    if (max <= 0.0)
    {
        Console.WriteLine("(empty — no photons reached the detector)");
        return;
    }

    for (int y = img.Height - 1; y >= 0; y--)
    {
        var sb = new StringBuilder();
        for (int x = 0; x < img.Width; x++)
        {
            int idx = (int)(img[x, y] / max * (ramp.Length - 1));
            sb.Append(ramp[idx]).Append(ramp[idx]); // doubled for aspect ratio
        }
        Console.WriteLine(sb.ToString());
    }
}
