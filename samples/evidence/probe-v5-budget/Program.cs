using System.Collections;
using System.Diagnostics;
using System.Text.Json;
using Gcam.Configuration;
using Gcam.Core;
using Gcam.Detector;
using Gcam.Simulation;

int seed = int.Parse(args[0]); string geometry = args[1]; int budget = int.Parse(args[2]);
if (!Enumerable.Range(0, 4).Select(i => 41000001 + 104729 * i).Contains(seed)) throw new Exception("development only");
if (!(geometry == "Wide" && budget is 8000 or 32000 || geometry == "High" && budget == 64000)) throw new Exception("undeclared budget");
int n = geometry == "Wide" ? 34 : 44;
var cfg = ConfigLoader.Load("samples/scenario.json").Clone();
cfg.Detector.PixelsX = cfg.Detector.PixelsY = n; cfg.Detector.PixelPitchMm = geometry == "Wide" ? .75 : .4;
cfg.Detector.Material = "GAGG"; cfg.Detector.ReflectorGapMm = .1; cfg.Detector.EntranceAbsorberMm = .15; cfg.Detector.BackingScatterMm = 2;
cfg.Detector.EnergyResolutionFwhm = 0; cfg.Detector.OpticalCrosstalkFraction = 0; cfg.Detector.Readout = null;
var ro = new ReadoutConfig { Mode = ReadoutMode.FourOutputAnger, Network = new() { ColumnResistanceOhm = 10, SolveMethod = ChargeNetworkSolveMethod.Adjoint } };
ro.Calibration.Method = FloodCalibrationMethod.LatticeGrowth; ro.Calibration.EnergyKeV = 661.7;
ro.Calibration.EventsPerCrystal = budget; ro.Optics.PhotonsPerBin = 100000; ro.Optics.DepthBins = 8; ro.Calibration.BinsPerCrystal = 32;
int Seed(uint p) => unchecked((int)DefaultRandom.Key(seed, p + 100));
IRandom Rng(uint p) => DefaultRandom.FromKey(DefaultRandom.Key(seed, p + 100));
var clock = Stopwatch.StartNew();
var device = new ReadoutDevice(cfg.Detector, ro, unchecked((int)DefaultRandom.Key(seed, 1)));
var proc = new ReadoutPulseProcessor(device, ro.Pulse, ro.Trigger);
double build = clock.Elapsed.TotalSeconds;
Console.Error.WriteLine($"{geometry}/{seed}/{budget}: build {build:F2}s");
clock.Restart();
var flood = new StreamedFlood(() => ReadoutFlood.Stream(cfg, 661.7, n * n * budget, Seed(2)), n * n * budget);
var cal = ReadoutCalibration.Build(device, proc, flood, ro.Calibration, Rng(3), Rng(4));
using var process = Process.GetCurrentProcess(); process.Refresh();
var output = new { Seed = seed, Geometry = geometry, Budget = budget, EnergyKeV = 661.7,
    cal.Succeeded, cal.Failure, Markers = cal.Lut.Peaks.Count, cal.Scored, cal.InWindow,
    BuildSeconds = build, CalibrationSeconds = clock.Elapsed.TotalSeconds,
    PeakWorkingSetBytes = process.PeakWorkingSet64, PeakPagedMemoryBytes = process.PeakPagedMemorySize64,
    PrivateMemoryBytes = process.PrivateMemorySize64, ManagedRetainedBytes = GC.GetTotalMemory(false) };
File.WriteAllText(args[3], JsonSerializer.Serialize(output));
Console.Error.WriteLine(JsonSerializer.Serialize(output));
sealed class StreamedFlood(Func<IEnumerable<InteractionSite[]>> source, int count) : IReadOnlyList<InteractionSite[]> {
    public int Count => count; public InteractionSite[] this[int i] => throw new NotSupportedException();
    public IEnumerator<InteractionSite[]> GetEnumerator() => source().GetEnumerator(); IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}
