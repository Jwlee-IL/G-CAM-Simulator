using System.Collections;
using System.Diagnostics;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;
using Gcam.Configuration;
using Gcam.Core;
using Gcam.Detector;
using Gcam.Simulation;

namespace Gcam.EvidenceProbe;

/// <summary>Frozen experimental offer plus descriptive imaging cost. No truth labels enter the blind LUT.</summary>
internal static class StudioReadoutRecipeV5
{
    private sealed class StreamedFlood(Func<IEnumerable<InteractionSite[]>> source, int count) : IReadOnlyList<InteractionSite[]>
    {
        public int Count => count;
        public InteractionSite[] this[int index] => throw new NotSupportedException();
        public IEnumerator<InteractionSite[]> GetEnumerator() => source().GetEnumerator();
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }

    internal static void Run(int seed, string samples, string recipeFile, string geometry, string phase, string familyHash)
    {
        var recipe = JsonNode.Parse(File.ReadAllText(recipeFile))!;
        if (recipe["Version"]!.GetValue<int>() != 5 || geometry is not ("Wide" or "High") || phase is not ("dev" or "repair")) throw new ArgumentException("v5 repair scope");
        var g = recipe["Geometries"]!.AsArray().Single(v => v!["Id"]!.GetValue<string>() == geometry)!;
        int n = g["Count"]!.GetValue<int>(), bins = n * n;
        double pitch = g["PitchMm"]!.GetValue<double>(), distance = g["DistanceMm"]!.GetValue<double>();
        double focal = recipe["FocalDistanceMm"]!.GetValue<double>(), cell = g["CellMm"]!.GetValue<double>();
        var cfg = ConfigLoader.Load(Path.Combine(samples, "scenario.json")).Clone();
        cfg.Seed = seed; cfg.Sources = null; cfg.Background = null; cfg.Ambient = null;
        cfg.Source.Isotope = "line"; cfg.Source.Lines = null; cfg.Source.BranchingRatio = 1;
        cfg.Detector.PixelsX = cfg.Detector.PixelsY = n; cfg.Detector.PixelPitchMm = pitch;
        cfg.Detector.Material = "GAGG"; cfg.Detector.ReflectorGapMm = .1;
        cfg.Detector.EntranceAbsorberMm = .15; cfg.Detector.BackingScatterMm = 2;
        cfg.Detector.EnergyResolutionFwhm = 0; cfg.Detector.OpticalCrosstalkFraction = 0; cfg.Detector.Readout = null;
        cfg.Mask.Rank = g["Rank"]!.GetValue<int>(); cfg.Mask.CellPitchMm = cell;
        cfg.Geometry.MaskDetectorDistanceMm = distance; cfg.Geometry.SourceMaskDistanceMm = focal - distance;
        double half = cfg.Mask.Rank * cell * focal / distance / 2;
        cfg.Decoder.Cyclic = false; cfg.Decoder.ReconHalfExtentMm = .95 * half;
        cfg.Decoder.ReconStepMm = Math.Max(.2, cell * focal / distance / 4); cfg.Decoder.SubCellInterpolation = SubCellMethod.Tent;
        var ro = new ReadoutConfig { Mode = ReadoutMode.FourOutputAnger, Network = new() { ColumnResistanceOhm = 10 } };
        ro.Network.SolveMethod = n == 12 ? ChargeNetworkSolveMethod.Legacy : ChargeNetworkSolveMethod.Adjoint;
        ro.Calibration.Method = n == 12 ? FloodCalibrationMethod.Legacy : FloodCalibrationMethod.LatticeGrowth;
        ro.Optics.PhotonsPerBin = recipe["OpticalPhotons"]!.GetValue<int>(); ro.Optics.DepthBins = recipe["DepthBins"]!.GetValue<int>();
        ro.Calibration.BinsPerCrystal = recipe["HistogramBinsPerCrystal"]!.GetValue<int>();
        int Seed(uint purpose, uint line = 0) => unchecked((int)DefaultRandom.Key(seed, purpose + 100 * line));
        IRandom Rng(uint purpose, uint line = 0) => DefaultRandom.FromKey(DefaultRandom.Key(seed, purpose + 100 * line));
        var clock = Stopwatch.StartNew(); var dev = new ReadoutDevice(cfg.Detector, ro, Seed(1));
        var proc = new ReadoutPulseProcessor(dev, ro.Pulse, ro.Trigger);
        var output = new JsonObject { ["SchemaVersion"] = 5, ["Seed"] = seed, ["Geometry"] = geometry, ["Phase"] = phase,
            ["FamilySha256"] = familyHash, ["RecipeSha256"] = Hash(recipeFile), ["RuleSha256"] = recipe["Rule"]!["Sha256"]!.GetValue<string>(),
            ["BuildSeconds"] = clock.Elapsed.TotalSeconds, ["Cells"] = new JsonArray() };
        bool cost = phase == "dev" || recipe["CostSeeds"]!.AsArray().Any(v => v!.GetValue<int>() == seed);
        output["CostScheduled"] = cost;
        FastDecoder? decoder = null;
        var channels = new double[dev.Channels];
        int Assign(ReadoutEvent? ev, ReadoutCalibration cal, double energy, bool global = false)
        {
            if (ev is null || !dev.Position(ev.Codes, out double x, out double y)) return -1;
            double sum = dev.Sum(ev.Codes);
            if (global && Math.Abs(sum - cal.GlobalPeakSum) > .15 * cal.GlobalPeakSum) return -1;
            int a = cal.Lut.Lookup(x, y);
            return a >= 0 && (global || Math.Abs(cal.Energy(a, sum) - energy) <= .15 * energy) ? a : -1;
        }
        var energies = recipe["EnergiesKeV"]!.AsArray().Select(v => v!.GetValue<double>()).ToArray();
        if (energies.Length != 1 || energies[0] != 661.7) throw new ArgumentException("v5 repair line");
        for (int li = 0; li < energies.Length; li++)
        {
            uint line = 1; // Preserve v4 662-keV purpose streams despite the single-line recipe.
            double energy = energies[li]; cfg.Source.EnergyKeV = energy; ro.Calibration.EnergyKeV = energy;
            int budget = g["CalibrationHistoriesPerCrystal"]!.GetValue<int>();
            ro.Calibration.EventsPerCrystal = budget;
            var flood = new StreamedFlood(() => ReadoutFlood.Stream(cfg, energy, bins * budget, Seed(2, line)), bins * budget);
            clock.Restart(); var cal = ReadoutCalibration.Build(dev, proc, flood, ro.Calibration, Rng(3, line), Rng(4, line));
            var row = new JsonObject { ["EnergyKeV"] = energy, ["CalibrationSucceeded"] = cal.Succeeded, ["Failure"] = cal.Failure,
                ["CalibrationBudget"] = budget, ["CalibrationScored"] = cal.Scored, ["Markers"] = cal.Lut.Peaks.Count,
                ["CalibrationWindowed"] = cal.InWindow, ["GlobalGainCrystals"] = cal.CrystalsOnGlobalGain,
                ["CalibrationSeconds"] = clock.Elapsed.TotalSeconds, ["Imaging"] = new JsonArray(), ["Diagnostics"] = null,
                ["CostFailure"] = null };
            output["Cells"]!.AsArray().Add(row);
            Console.Error.WriteLine($"{geometry}/{energy}: calibration {cal.Succeeded} {clock.Elapsed.TotalSeconds:F2}s {cal.Failure}");
            clock.Restart();
            if (cal.Succeeded) row["Diagnostics"] = new JsonObject { ["0"] = Diagnostics(0), ["100000"] = Diagnostics(100000) };
            row["DiagnosticSeconds"] = clock.Elapsed.TotalSeconds;
            if (!cost) continue;
            clock.Restart();
            var fd = new double[bins]; var fa = new double[bins]; var response = Rng(3, line); var noise = Rng(4, line);
            foreach (var sites in flood)
            {
                int d = ReadoutDevice.DirectCrystal(sites, n);
                if (Math.Abs(sites.Sum(s => s.EnergyKeV) - energy) <= .15 * energy) fd[d]++;
                if (!cal.Succeeded) continue;
                double sum = dev.Respond(sites, response, channels);
                int a = Assign(proc.ProcessIsolated(new(0, channels, sum), noise), cal, energy); if (a >= 0) fa[a]++;
            }
            // Empty flood bins prevent a cost estimate, never change the calibration offer decision.
            bool directFlood = fd.All(v => v > 0), angerFlood = cal.Succeeded && fa.All(v => v > 0);
            row["FloodDirect"] = JsonSerializer.SerializeToNode(fd); row["FloodAnger"] = JsonSerializer.SerializeToNode(fa);
            row["CostFailure"] = !directFlood ? "empty direct correction bin" : cal.Succeeded && !angerFlood ? "empty Anger correction bin" : null;
            if (!directFlood) continue;
            var wd = fd.Select(v => fd.Average() / v).ToArray(); var wa = angerFlood ? fa.Select(v => fa.Average() / v).ToArray() : null;
            decoder ??= new FastDecoder(cfg);
            var xy = new (double X, double Y)[] { (0,0), (1,0), (-1,0), (0,1), (0,-1), (1,1), (1,-1), (-1,1), (-1,-1) };
            int trials = recipe["TrialsPerPosition"]!.GetValue<int>(), pilot = recipe["ExposurePilotHistories"]!.GetValue<int>();
            double offset = recipe["PositionHalfFraction"]!.GetValue<double>() * half;
            for (int pi = 0; pi < xy.Length; pi++)
            {
                double x = xy[pi].X * offset, y = xy[pi].Y * offset;
                var c = cfg.Clone(); c.Source.Position = [x, y, 0]; c.Seed = Seed(200 + (uint)pi, line);
                int directPilot = 0;
                using (var src = new ListModeSource(c, true)) while (src.HistoriesEmitted < pilot)
                    if (src.Advance() is { } ev && Math.Abs(ev.DepositKeV - energy) <= .15 * energy) directPilot++;
                if (directPilot == 0) throw new InvalidOperationException("zero exposure pilot acceptance");
                foreach (int target in recipe["DirectReferenceCounts"]!.AsArray().Select(v => v!.GetValue<int>()))
                {
                    int exposure = (int)Math.Round((double)target * pilot / directPilot), ei = target == 1000 ? 0 : 1;
                    foreach (double rate in recipe["RatesCps"]!.AsArray().Select(v => v!.GetValue<double>()))
                    {
                        var estimates = new JsonArray();
                        for (int b = 0; b < trials; b++)
                        {
                            // Exposures use independent fresh photons. Rate and isolated paths replay those SAME photons and response.
                            uint key = (uint)(pi * trials + b + ei * 4096);
                            var fresh = c.Clone(); fresh.Seed = Seed(6000 + key, line);
                            var rr = Rng(12000 + key, line); var nn = Rng(rate == 0 ? 24000 + key : 40000 + key, line);
                            var arrival = Rng(50000 + key, line); int h = ExactCount.Sample(Rng(30000 + key, line), exposure);
                            var dc = new double[bins]; var ac = new double[bins]; var hits = new List<ReadoutHit>(); double time = 0;
                            using (var src = new ListModeSource(fresh, true)) while (src.HistoriesEmitted < h)
                            {
                                if (src.Advance() is not { } ev) continue;
                                if (Math.Abs(ev.DepositKeV - energy) <= .15 * energy) dc[ev.PixelY * n + ev.PixelX]++;
                                if (!angerFlood) continue;
                                double sum = dev.Respond(src.LastInteractions, rr, channels);
                                if (rate == 0) { int a = Assign(proc.ProcessIsolated(new(0, channels, sum), nn), cal, energy); if (a >= 0) ac[a]++; }
                                else { time += -Math.Log(Math.Max(1e-300, arrival.NextDouble())) * 1e9 / rate; hits.Add(new(time, (double[])channels.Clone(), sum)); }
                            }
                            if (rate != 0 && angerFlood) foreach (var ev in proc.Process(hits, nn)) { int a = Assign(ev, cal, energy); if (a >= 0) ac[a]++; }
                            JsonObject Estimate(double[] counts, double[] weights)
                            {
                                var image = new DetectorImage(n, n); for (int k = 0; k < bins; k++) image[k % n, k / n] = counts[k] * weights[k];
                                var p = decoder.Decode(image);
                                if (b == 0) { var oracle = new DefaultSimulationFactory().CreateDecoder(c)!.Decode(image).Estimate.Position;
                                    if (Math.Abs(p.X - oracle.X) > 1e-9 || Math.Abs(p.Y - oracle.Y) > 1e-9) throw new InvalidOperationException("decoder oracle"); }
                                double ex = (Math.Atan(p.X / focal) - Math.Atan(x / focal)) * 180 / Math.PI;
                                double ey = (Math.Atan(p.Y / focal) - Math.Atan(y / focal)) * 180 / Math.PI;
                                return new JsonObject { ["ExDeg"] = ex, ["EyDeg"] = ey, ["Count"] = counts.Sum(),
                                    ["Remote"] = double.Hypot(ex, ey) > cell / distance * 180 / Math.PI };
                            }
                            estimates.Add(new JsonObject { ["Trial"] = b, ["Direct"] = Estimate(dc, wd), ["Anger"] = angerFlood ? Estimate(ac, wa!) : null });
                        }
                        row["Imaging"]!.AsArray().Add(new JsonObject { ["Position"] = pi, ["Xmm"] = x, ["Ymm"] = y,
                            ["TargetDirectCounts"] = target, ["RateCps"] = rate, ["MeanEmittedHistories"] = exposure,
                            ["ExposurePilotDirect"] = directPilot, ["Trials"] = estimates });
                    }
                }
            }
            row["CostSeconds"] = clock.Elapsed.TotalSeconds;
            Console.Error.WriteLine($"{geometry}/{energy}: cost {clock.Elapsed.TotalSeconds:F2}s");

            JsonObject Diagnostics(double rate)
            {
                int count = rate == 0 ? recipe["ValidationHistoriesPerCrystal"]!.GetValue<int>() * bins : recipe["RateDiagnosticHits"]!.GetValue<int>();
                var full = new int[bins]; var assigned = new int[bins]; int accepted = 0, single = 0, mis = 0, multi = 0;
                double gx = 0, gy = 0; var rr = Rng(20, line); var nn = Rng(rate == 0 ? 21u : 22u, line);
                var arrival = Rng(23, line); double time = 0; var hits = new List<ReadoutHit>();
                var truth = new List<(int F, int D, bool One)>(); var retained = new HashSet<int>();
                void Score(ReadoutEvent? ev, int f, int d, bool one)
                {
                    int a = Assign(ev, cal, energy, true); if (a < 0) return;
                    accepted++; assigned[a]++;
                    if (one) { single++; if (a != d) mis++; } else multi++;
                    double ax = Math.Atan((a % n - f % n) * pitch / distance), ay = Math.Atan((a / n - f / n) * pitch / distance);
                    double dx = Math.Atan((d % n - f % n) * pitch / distance), dy = Math.Atan((d / n - f / n) * pitch / distance);
                    gx += ax * ax - dx * dx; gy += ay * ay - dy * dy;
                }
                foreach (var s in ReadoutFlood.Stream(cfg, energy, count, Seed(10, line)))
                {
                    int f = ReadoutTruth.FirstInteraction(s), fi = s[f].CrystalY * n + s[f].CrystalX;
                    if (ReadoutTruth.IsFullEnergy(s, energy)) full[fi]++;
                    double sum = dev.Respond(s, rr, channels);
                    int d = ReadoutDevice.DirectCrystal(s, n);
                    bool one = s.All(v => v.CrystalX == s[0].CrystalX && v.CrystalY == s[0].CrystalY);
                    if (rate == 0) Score(proc.ProcessIsolated(new(0, channels, sum), nn), fi, d, one);
                    else
                    {
                        time += -Math.Log(Math.Max(1e-300, arrival.NextDouble())) * 1e9 / rate;
                        hits.Add(new(time, (double[])channels.Clone(), sum)); truth.Add((fi, d, one));
                    }
                }
                if (rate != 0) foreach (var ev in proc.Process(hits, nn))
                {
                    if (!retained.Add(ev.DominantHit)) continue;
                    var t = truth[ev.DominantHit]; Score(ev, t.F, t.D, t.One);
                }
                return new JsonObject { ["RateCps"] = rate, ["Inputs"] = count, ["Accepted"] = accepted, ["GSumXRad2"] = gx, ["GSumYRad2"] = gy,
                    ["Single"] = single, ["SingleMis"] = mis, ["Multi"] = multi,
                    ["AssignedToCrystal"] = JsonSerializer.SerializeToNode(assigned), ["FullEnergyByFirst"] = JsonSerializer.SerializeToNode(full) };
            }
        }
        using var process = Process.GetCurrentProcess(); process.Refresh();
        output["PeakWorkingSetBytes"] = process.PeakWorkingSet64;
        output["PeakPagedMemoryBytes"] = process.PeakPagedMemorySize64;
        output["PrivateMemoryBytes"] = process.PrivateMemorySize64;
        Console.WriteLine(output.ToJsonString());
    }
    private static string Hash(string file) => Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(file))).ToLowerInvariant();
}
