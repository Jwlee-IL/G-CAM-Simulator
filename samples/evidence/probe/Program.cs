// Gcam.EvidenceProbe — the headless recipes behind TODO-27's evidence quotes that no CLI command produces.
//
//   dotnet Gcam.EvidenceProbe.dll <mode> <seed> <samplesDir>
//
// Each mode loads the scenario(s) from <samplesDir>, overwrites only Seed (plus the mode's documented recipe
// changes, made on a Clone() of the complete config), runs the engine as it is and prints one JSON or CSV document
// on stdout. run_seeds.py captures it as output.txt; aggregate.py parses it. Modes and the evidence they serve:
//
//   precise     EV-01/09/20: efficiency, estimate and ghost margin of seven single-source scenarios, unrounded
//   scan        EV-03: ParameterScan on the selected rows (11/1/30, 23/1/20, 7/1/60, 13/1.5/20), 11 positions, 200k
//   scanextra   EV-03: rank 23 / 1 mm / D 20 with a 0.5 mm slab at the same μ·t, and the 2 mm-cell collapse rows
//   bias        EV-07: directional biasing vs 4π — same config, DirectionalBiasing off, 10^8 histories
//   fov         EV-02: FieldOfViewStudy on the hand-held head, 1 m / 5 m, x and diagonal, N0 500/5000, BSR 0/1
//   gap         replacement crystal-gap experiment (PAPER §4): per-pixel ±10 % window, contact 0.4, 0–200 µm gaps
//   spatial     EV-15: Cs-137 at (4, 0) beside Co-60 at (−5, 3), activity ratio 1/2/4/8, per-pixel window
//   depthsharp  EV-33: depth-from-focus width on the viewer's sharp optics (rank 13, 0.7 mm, D 80, 30×30 @ 0.6 mm)
//   materials   EV-19: unrounded efficiency of the crystal presets in samples/materials
//   viewer      EV-33: the viewer's list-mode path at its default optics — a manual 81-plane focus scan and the
//               current FocusSweepService, source at 300/500/700 mm and 0/15/30 mrad, 500 µCi, 60 s
using System.Globalization;
using System.Text.Json;
using Gcam.Configuration;
using Gcam.Core;
using Gcam.Detector;
using Gcam.Simulation;
using Gcam.Studio.Core.Services;
using Gcam.Studio.Services;

CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;
if (args.Length < 3)
{
    Console.Error.WriteLine("Usage: Gcam.EvidenceProbe <mode> <seed> <samplesDir>");
    return 2;
}

string mode = args[0];
int seed = int.Parse(args[1], CultureInfo.InvariantCulture);
string samplesDir = args[2];

SimulationConfig Load(string name = "scenario")
{
    var c = ConfigLoader.Load(Path.Combine(samplesDir, name + ".json"));
    c.Seed = seed;
    return c;
}

var cfg = Load();
var factory = new DefaultSimulationFactory();
var runner = new SimulationRunner(factory);

switch (mode)
{
    case "precise":
    {
        var output = new List<object>();
        foreach (var fn in new[] { "scenario", "scenario_offaxis", "scenario_ghost", "scenario_handheld",
                                   "scenario_orig_gagg", "scenario_ir192", "scenario_handheld_ir192" })
        {
            var c = Load(fn);
            var r = runner.Run(c);
            var p = r.Estimate!.Position;
            output.Add(new
            {
                fn,
                eff = r.DetectedWeight / r.PhotonsEmitted,
                x = p.X,
                y = p.Y,
                error = Math.Sqrt(Math.Pow(p.X - c.Source.Position[0], 2) + Math.Pow(p.Y - c.Source.Position[1], 2)),
                margin = r.Estimate.Confidence,
            });
        }
        Console.WriteLine(JsonSerializer.Serialize(output));
        break;
    }
    case "scan":
    {
        const int Positions = 11, Photons = 200_000;
        var rows = new List<ScanRow>();
        foreach (var (r, p, d) in new (int, double, double)[] { (11, 1, 30), (23, 1, 20), (7, 1, 60), (13, 1.5, 20) })
            rows.AddRange(new ParameterScan(factory).Run(cfg, [r], [p], [d], Positions, Photons));
        Console.WriteLine(ParameterScan.ToCsv(rows.ToArray()));
        break;
    }
    case "scanextra":
    {
        const int Positions = 11, Photons = 200_000;
        var thin = cfg.Clone();
        thin.Mask.ThicknessMm = 0.5;
        thin.Mask.LinearAttenuationPerMm = cfg.Mask.LinearAttenuationPerMm * cfg.Mask.ThicknessMm / 0.5;  // same μ·t
        Console.WriteLine(ParameterScan.ToCsv(new ParameterScan(factory).Run(thin, [23], [1.0], [20.0], Positions, Photons)));
        Console.WriteLine("SECOND");
        Console.WriteLine(ParameterScan.ToCsv(
            new ParameterScan(factory).Run(cfg, [13, 17, 19, 23], [2.0], [20.0, 30, 40, 60, 80], Positions, Photons)));
        break;
    }
    case "bias":
    {
        const int IsotropicHistories = 100_000_000;   // 100× the scenario's biased budget
        var b = runner.Run(cfg);
        var c = cfg.Clone();
        c.Source.DirectionalBiasing = false;
        c.PhotonCount = IsotropicHistories;
        var f = runner.Run(c);
        double biased = b.DetectedWeight / b.PhotonsEmitted, isotropic = f.DetectedWeight / f.PhotonsEmitted;
        Console.WriteLine(JsonSerializer.Serialize(new
        {
            biased,
            isotropic,
            ratio = biased / isotropic,
            counts = f.DetectedWeight,
            biased_photons = cfg.PhotonCount,
            isotropic_photons = IsotropicHistories,
        }));
        break;
    }
    case "fov":
    {
        const int Repeats = 100;
        cfg = Load("scenario_handheld");
        cfg.PhotonCount = 1_000_000;
        var all = new List<FovRow>();
        var angles = Enumerable.Range(0, 41).Select(i => i * 0.5).ToArray();
        var study = new FieldOfViewStudy();
        foreach (var s in new[] { 1000.0, 5000.0 })
            foreach (var dir in new[] { 0.0, 45.0 })
                all.AddRange(study.Run(cfg, s, dir, angles, [500, 5000], [0, 1], Repeats));
        Console.WriteLine(FieldOfViewStudy.ToCsv(all));
        break;
    }
    case "gap":
    {
        // A labelled replacement experiment, not the lost original: physical GAGG lab geometry, optical contact
        // fraction 0.4, ±10 % per-pixel window; relativeIdeal is normalised to the basic (no-Compton) detector.
        cfg.PhotonCount = 2_000_000;
        var norm = runner.Run(cfg).DetectedWeight;
        var output = new List<object>();
        foreach (var gap in new[] { 0.0, 0.02, 0.04, 0.1, 0.2 })
        {
            var c = cfg.Clone();
            c.Detector.ReflectorGapMm = gap;
            c.Detector.OpticalCrosstalkFraction = 0.4;
            var r = new SimulationRunner(new ComptonFactory(ComptonStrategy.PerPixelWindow, 661.7, 0.1)).Run(c);
            output.Add(new { gap, eff = r.DetectedWeight / r.PhotonsEmitted, relativeIdeal = r.DetectedWeight / norm });
        }
        Console.WriteLine(JsonSerializer.Serialize(output));
        break;
    }
    case "spatial":
    {
        cfg.PhotonCount = 4_000_000;
        cfg.Decoder.Cyclic = false;
        cfg.Decoder.ReconHalfExtentMm = 0.95 * cfg.Mask.Rank * cfg.Mask.CellPitchMm * (160.0 / 60) / 2;
        cfg.Decoder.ReconStepMm = 0.4;
        var output = new List<object>();
        foreach (var ratio in new[] { 1.0, 2.0, 4.0, 8.0 })
        {
            var c = cfg.Clone();
            c.Sources =
            [
                new SourceConfig { Position = [4, 0, 0], ActivityBq = 1, Lines = [new EmissionLine { EnergyKeV = 661.7, Intensity = 0.851 }] },
                new SourceConfig
                {
                    Position = [-5, 3, 0], ActivityBq = ratio,
                    Lines = [new EmissionLine { EnergyKeV = 1173.2, Intensity = 0.999 }, new EmissionLine { EnergyKeV = 1332.5, Intensity = 0.999 }],
                },
            ];
            var r = new MixedFieldStudy(new ComptonFactory(ComptonStrategy.PerPixelWindow, 661.7, 0.1)).LocalizeMultiple(c, 2, 3);
            output.Add(new { ratio, matches = MixedFieldStudy.MatchOneToOne(r.TruthXY, r.Found).Select(m => new { m.ErrorMm }) });
        }
        Console.WriteLine(JsonSerializer.Serialize(output));
        break;
    }
    case "depthsharp":
    {
        cfg.Mask.Rank = 13;
        cfg.Mask.CellPitchMm = 0.7;
        cfg.Geometry.MaskDetectorDistanceMm = 80;
        cfg.Detector.PixelsX = cfg.Detector.PixelsY = 30;
        cfg.Detector.PixelPitchMm = 0.6;
        cfg.PhotonCount = 2_000_000;
        var (rows, _, _, _) = DepthDesignStudy.Sweep(cfg, factory, [150, 250, 400, 600, 900, 1400]);
        Console.WriteLine(JsonSerializer.Serialize(new
        {
            rows,
            range10 = DepthDesignStudy.EffectiveRangeMm(rows, 0.2),
            range20 = DepthDesignStudy.EffectiveRangeMm(rows, 0.4),
        }));
        break;
    }
    case "materials":
    {
        var rows = new List<object>();
        foreach (var name in new[] { "NaI", "LaBr3", "CeBr3", "GAGG", "LYSO", "BGO" })
        {
            var c = Load(Path.Combine("materials", name));
            var r = new SimulationRunner(new DefaultSimulationFactory()).Run(c);
            rows.Add(new { name, eff = r.DetectedWeight / r.PhotonsEmitted });
        }
        Console.WriteLine(JsonSerializer.Serialize(rows));
        break;
    }
    case "viewer":
    {
        const double AcquisitionS = 60, ActivityUCi = 500;
        var output = new List<object>();
        foreach (double z in new[] { 300.0, 500, 700 })
            foreach (double a in new[] { 0.0, 15, 30 })
            {
                var opt = new OpticsSettings { DetectorPixels = 30, PixelPitchMm = 0.6 };
                var ds = new DetectorSettings();
                var c = SimulationService.BuildConfig(
                    [new SceneSource { X = z * Math.Tan(a / 1000), Y = 0, DistanceMm = z, ActivityUCi = ActivityUCi }], opt, ds);
                c.Seed = seed;
                using var source = new ListModeSource(c);
                var ms = new MeasurementStage(ds, 30, 30, seed: seed);
                var all = new DetectorImage(30, 30);
                var win = new DetectorImage(30, 30);
                int hits = 0;
                var hw = 1.5 * 661.7 * new FrontEndModel(ds.Chain.BuildConfig()).FwhmFraction(661.7);
                while (source.ArrivalTimeS <= AcquisitionS)
                {
                    var e = source.Advance();
                    if (e is not { } ev || ev.ArrivalTimeS > AcquisitionS) continue;
                    all.Add(ev.PixelX, ev.PixelY, 1);
                    if (Math.Abs(ms.Measure(ev, hits) - 661.7) <= hw) win.Add(ev.PixelX, ev.PixelY, 1);
                    hits++;
                }
                foreach (var (name, image) in new[] { ("all", all), ("window", win) })
                {
                    // Manual estimator: 81 planes 150–950 mm, score = (max − mean) / SD of the decode.
                    double best = double.NegativeInfinity, bestPlane = 0;
                    foreach (double plane in Enumerable.Range(0, 81).Select(i => 150.0 + 10 * i))
                    {
                        var dc = ImagingProjection.AtFocus(c, opt, plane);
                        var r = new DefaultSimulationFactory().CreateDecoder(dc)!.Decode(image).Reconstruction;
                        var values = r.Raw.ToArray();
                        var mean = values.Average();
                        var sd = Math.Sqrt(values.Select(v => (v - mean) * (v - mean)).Average());
                        var score = (values.Max() - mean) / sd;
                        if (score > best) { best = score; bestPlane = plane; }
                    }
                    var ir = new ImagingResult(image, -8.7, 0.6, null, 0, 0, null, image.Raw.ToArray().Sum(), TimeSpan.FromSeconds(AcquisitionS));
                    var identity = new FocusSweepIdentity(Guid.Empty, (long)image.Raw.ToArray().Sum(), AcquisitionS,
                        name == "all" ? "All" : "Cs-137", 1.5, false, opt, ds, 1);
                    var focus = await new FocusSweepService().SweepAsync(new FocusSweepRequest(identity, ir));
                    var t = focus.Tracks.Single();
                    output.Add(new
                    {
                        current = new { estimate = t.Sharpest.PlaneMm, bias = t.Sharpest.PlaneMm - z, t.Interval, t.BoundaryMaximum, t.MultipleModes },
                        z,
                        angle_mrad = a,
                        channel = name,
                        counts = image.Raw.ToArray().Sum(),
                        estimate = bestPlane,
                        bias = bestPlane - z,
                        edge = bestPlane == 150 || bestPlane == 950,
                    });
                }
            }
        Console.WriteLine(JsonSerializer.Serialize(output));
        break;
    }
    default:
        Console.Error.WriteLine($"Unknown mode '{mode}'.");
        return 2;
}
return 0;
