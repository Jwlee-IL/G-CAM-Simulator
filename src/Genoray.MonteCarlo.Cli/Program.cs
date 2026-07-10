using System.Text;
using Genoray.MonteCarlo.Configuration;
using Genoray.MonteCarlo.Core;
using Genoray.MonteCarlo.Simulation;

if (args.Length < 1)
{
    Console.Error.WriteLine("Usage: montecarlo <scenario.json>");
    Console.Error.WriteLine("       montecarlo sweep <base.json> [out.csv]");
    return 1;
}

if (args[0].Equals("sweep", StringComparison.OrdinalIgnoreCase))
    return RunSweep(args);

if (args[0].Equals("scan", StringComparison.OrdinalIgnoreCase))
    return RunScan(args);

if (args[0].Equals("noise", StringComparison.OrdinalIgnoreCase))
    return RunNoise(args);

if (args[0].Equals("thickness", StringComparison.OrdinalIgnoreCase))
    return RunThickness(args);

if (args[0].Equals("uniformity", StringComparison.OrdinalIgnoreCase))
    return RunUniformity(args);

if (args[0].Equals("array", StringComparison.OrdinalIgnoreCase))
    return RunArray(args);

if (args[0].Equals("antimask", StringComparison.OrdinalIgnoreCase))
    return RunAntimask(args);

if (args[0].Equals("antimask-scene", StringComparison.OrdinalIgnoreCase))
    return RunAntimaskScene(args);

if (args[0].Equals("compton", StringComparison.OrdinalIgnoreCase))
    return RunCompton(args);

if (args[0].Equals("compton-strip", StringComparison.OrdinalIgnoreCase))
    return RunComptonStrip(args);

if (args[0].Equals("depth", StringComparison.OrdinalIgnoreCase))
    return RunDepth(args);

if (args[0].Equals("depth-joint", StringComparison.OrdinalIgnoreCase))
    return RunDepthJoint(args);

if (args[0].Equals("depth3d", StringComparison.OrdinalIgnoreCase))
    return RunDepth3D(args);

if (args[0].Equals("maskgeo", StringComparison.OrdinalIgnoreCase))
    return RunMaskGeo(args);

if (args[0].Equals("masksize", StringComparison.OrdinalIgnoreCase))
    return RunMaskSize(args);

if (args[0].Equals("shield", StringComparison.OrdinalIgnoreCase))
    return RunShield(args);

if (args[0].Equals("masktaper", StringComparison.OrdinalIgnoreCase))
    return RunMaskTaper(args);

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

    var study = new ShieldStudy(new DefaultSimulationFactory());
    var csv = new System.Text.StringBuilder("bg,thickness_mm,transmission,bg_per_px,rms_raw_mm,rms_calib_mm,fail_raw,shield_kg\n");
    foreach (var (label, energy, mu) in backgrounds)
    {
        var rows = study.Run(baseConfig, nSrc, bg0, mu, thick, repeats, failThr);
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
    File.WriteAllText("samples/shield.csv", csv.ToString());
    Console.WriteLine("Low-energy scattered background is killed by a few mm; Co-60 never reaches the knee");
    Console.WriteLine("at a carriable mass -> shield the low-E noise, beat high-E with coded+stripping.");
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
    var decoder = factory.CreateDecoder(baseConfig.Clone());

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
