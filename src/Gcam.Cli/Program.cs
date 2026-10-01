using Gcam.Configuration;
using Gcam.Simulation;
using Gcam.Cli;
using static Gcam.Cli.ConsoleRender;

var commands = new Dictionary<string, Func<string[], int>>(StringComparer.OrdinalIgnoreCase)
{
    ["sweep"]          = CoreCommands.RunSweep,
    ["fov"]            = CoreCommands.RunFieldOfView,
    ["scan"]           = CoreCommands.RunScan,
    ["noise"]          = CoreCommands.RunNoise,
    ["thickness"]      = CoreCommands.RunThickness,
    ["uniformity"]     = CoreCommands.RunUniformity,
    ["array"]          = CoreCommands.RunArray,
    ["maskgeo"]        = MaskCommands.RunMaskGeo,
    ["masksize"]       = MaskCommands.RunMaskSize,
    ["masktaper"]      = MaskCommands.RunMaskTaper,
    ["maskfab"]        = MaskCommands.RunMaskFab,
    ["masksec"]        = MaskCommands.RunMaskSecondary,
    ["maskscatter"]    = MaskCommands.RunMaskScatter,
    ["align"]          = MaskCommands.RunAlign,
    ["mlem"]           = DecodingCommands.RunMlem,
    ["subcell"]        = DecodingCommands.RunSubCell,
    ["antimask"]       = DecodingCommands.RunAntimask,
    ["antimask-scene"] = DecodingCommands.RunAntimaskScene,
    ["compton"]        = IsotopeCommands.RunCompton,
    ["compton-strip"]  = IsotopeCommands.RunComptonStrip,
    ["mixedfield"]     = IsotopeCommands.RunMixedField,
    ["mixediso"]       = IsotopeCommands.RunMixedIso,
    ["mixedstrip"]     = IsotopeCommands.RunMixedStrip,
    ["cascade"]        = IsotopeCommands.RunCascade,
    ["nonprop"]        = IsotopeCommands.RunNonProp,
    ["depth"]          = DepthCommands.RunDepth,
    ["depth-joint"]    = DepthCommands.RunDepthJoint,
    ["depth3d"]        = DepthCommands.RunDepth3D,
    ["depthdesign"]    = DepthCommands.RunDepthDesign,
    ["doi"]            = DepthCommands.RunDoi,
    ["background"]     = SceneCommands.RunBackground,
    ["shield"]         = SceneCommands.RunShield,
    ["finitesrc"]      = SceneCommands.RunFiniteSource,
    ["dose"]           = DoseCommands.RunDose,
    ["frontend"]       = FrontEndCommands.RunFrontend,
    ["eventstream"]    = FrontEndCommands.RunEventStream,
    ["thermal"]        = FrontEndCommands.RunThermal,
    ["thermalro"]      = FrontEndCommands.RunThermalReadout,
    ["pileup"]         = FrontEndCommands.RunPileUp,
    ["deadtime"]       = FrontEndCommands.RunDeadTime,
    ["defects"]        = FrontEndCommands.RunDefects,
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
    Console.WriteLine("  scene / bg      background  shield  finitesrc  dose");
    Console.WriteLine("  front-end/RTL   frontend  eventstream  thermal  thermalro  pileup  deadtime  defects");
    Console.WriteLine();
    Console.WriteLine($"  ({commands.Count} study commands total.)");
}
