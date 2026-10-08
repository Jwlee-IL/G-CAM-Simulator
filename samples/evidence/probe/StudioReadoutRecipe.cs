using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Nodes;
using Gcam.Configuration;
using Gcam.Core;
using Gcam.Detector;
using Gcam.Simulation;

namespace Gcam.EvidenceProbe;

/// <summary>TODO-41 blind calibration and held-out event displacement recipe. Failed calibrations remain full rows.
/// Purpose keys separate optical, calibration, held-out transport, response, timing and digitisation streams.</summary>
internal static class StudioReadoutRecipe
{
    internal static void Run(int seed, string samples, string recipeFile, string geometry, string phase, string familyHash)
    {
        var recipe = JsonNode.Parse(File.ReadAllText(recipeFile))!;
        var geom = recipe["Geometries"]!.AsArray().Single(g => g!["Id"]!.GetValue<string>() == geometry)!;
        int n = geom["Count"]!.GetValue<int>(); double pitch = geom["PitchMm"]!.GetValue<double>();
        double distance = geom["DistanceMm"]!.GetValue<double>();
        var cfg = ConfigLoader.Load(Path.Combine(samples, "scenario.json"));
        cfg = cfg.Clone(); cfg.Detector.PixelsX = cfg.Detector.PixelsY = n;
        cfg.Detector.PixelPitchMm = pitch; cfg.Detector.ReflectorGapMm = .1;
        cfg.Detector.EntranceAbsorberMm = .15; cfg.Detector.BackingScatterMm = 2;
        cfg.Detector.Material = "GAGG"; cfg.Detector.Readout = null;
        cfg.Geometry.MaskDetectorDistanceMm = distance;
        cfg.Mask.Rank = geom["Rank"]!.GetValue<int>(); cfg.Mask.CellPitchMm = geom["CellMm"]!.GetValue<double>();
        var ro = new ReadoutConfig { Mode = ReadoutMode.FourOutputAnger, Network = new() { ColumnResistanceOhm = 10 } };
        ro.Calibration.Method = n == 12 ? FloodCalibrationMethod.Legacy : FloodCalibrationMethod.TopologyAware;
        ro.Network.SolveMethod = n == 12 ? ChargeNetworkSolveMethod.Legacy : ChargeNetworkSolveMethod.Adjoint;
        ro.Optics.PhotonsPerBin = recipe["OpticalPhotons"]!.GetValue<int>();
        ro.Optics.DepthBins = recipe["DepthBins"]!.GetValue<int>();
        ro.Calibration.EventsPerCrystal = recipe["CalibrationHistoriesPerCrystal"]!.GetValue<int>();
        ro.Calibration.BinsPerCrystal = recipe["HistogramBinsPerCrystal"]!.GetValue<int>();
        int validation = recipe["ValidationHistoriesPerCrystal"]!.GetValue<int>();
        int rateHits = recipe["RateHits"]!.GetValue<int>();
        var clock = Stopwatch.StartNew(); var times = new Dictionary<string, double>();
        int Seed(uint purpose, uint line = 0) => unchecked((int)DefaultRandom.Key(seed, purpose + 100 * line));
        IRandom Random(uint purpose, uint line = 0) => DefaultRandom.FromKey(DefaultRandom.Key(seed, purpose + 100 * line));
        void Stage(string label) { times[label] = clock.Elapsed.TotalSeconds; Console.Error.WriteLine($"{geometry}: {label} {clock.Elapsed.TotalSeconds:F3}s"); clock.Restart(); }
        var device = new ReadoutDevice(cfg.Detector, ro, Seed(1)); Stage("Build");
        var processor = new ReadoutPulseProcessor(device, ro.Pulse, ro.Trigger);
        var output = new JsonObject { ["SchemaVersion"] = 1, ["Seed"] = seed, ["Geometry"] = geometry,
            ["Phase"] = phase, ["FamilySha256"] = familyHash, ["RecipeSha256"] = Hash(recipeFile), ["RuleSha256"] = recipe["Rule"]!["Sha256"]!.GetValue<string>(),
            ["AllocationRad"] = pitch / distance / Math.Sqrt(12), ["Cells"] = new JsonArray(), ["Numerical"] = new JsonObject() };
        var cells = output["Cells"]!.AsArray();
        var channels = new double[device.Channels];
        JsonObject Measure(IReadOnlyList<InteractionSite[]> sites, ReadoutCalibration calibration, double energy,
            double rate, uint line)
        {
            var scored = new int[n * n]; var triggered = new int[n * n];
            var windowed = new int[n * n]; var assigned = new int[n * n];
            var retained = new bool[sites.Count]; var truth = new int[sites.Count];
            var response = Random(20, line); var noise = Random(rate == 0 ? 21u : 22u, line);
            var arrival = Random(23, line); var hits = new List<ReadoutHit>();
            double sumX = 0, sumY = 0, squaredX = 0, squaredY = 0; int count = 0, conversions = 0;
            var displacement = new Dictionary<string, int>();
            void Conversion(ReadoutEvent ev)
            {
                conversions++;
                if (ev.DominantHit < 0 || ev.DominantHit >= truth.Length) throw new InvalidOperationException("invalid dominant-hit reference");
                int h = ev.DominantHit, t = truth[h];
                // One acceptance per original scored history; multiple conversions are counted separately.
                if (retained[h]) return;
                retained[h] = true; triggered[t]++;
                if (!device.Position(ev.Codes, out double x, out double y)) return;
                if (Math.Abs(device.Sum(ev.Codes) - calibration.GlobalPeakSum) > ro.Calibration.WindowFraction * calibration.GlobalPeakSum) return;
                windowed[t]++;
                int c = calibration.Lut.Lookup(x, y);
                if (c < 0) return;
                assigned[t]++; count++;
                double dx = (c % n - t % n) * pitch, dy = (c / n - t / n) * pitch;
                double ax = Math.Atan(dx / distance), ay = Math.Atan(dy / distance);
                sumX += dx; sumY += dy; squaredX += ax * ax; squaredY += ay * ay;
                string key = $"{c % n - t % n},{c / n - t / n}";
                displacement[key] = displacement.GetValueOrDefault(key) + 1;
            }
            double time = 0;
            for (int h = 0; h < sites.Count; h++)
            {
                truth[h] = ReadoutDevice.DirectCrystal(sites[h], n); scored[truth[h]]++;
                double sum = device.Respond(sites[h], response, channels);
                if (rate == 0)
                {
                    var ev = processor.ProcessIsolated(new(0, channels, sum), noise);
                    if (ev is not null) Conversion(ev with { DominantHit = h });
                }
                else
                {
                    time += -Math.Log(Math.Max(1e-300, arrival.NextDouble())) * 1e9 / rate;
                    hits.Add(new(time, (double[])channels.Clone(), sum));
                }
            }
            if (rate != 0) foreach (var ev in processor.Process(hits, noise)) Conversion(ev);
            var acceptances = scored.Select((v, c) => v == 0 ? (double?)null : (double)assigned[c] / v).ToArray();
            var finite = acceptances.Where(v => v.HasValue).Select(v => v!.Value).Order().ToArray();
            return new JsonObject { ["EnergyKeV"] = energy, ["RateCps"] = rate, ["CalibrationSucceeded"] = calibration.Succeeded,
                ["Failure"] = calibration.Failure, ["Markers"] = calibration.Lut.Peaks.Count,
                ["CalibrationScored"] = calibration.Scored, ["CalibrationTriggered"] = calibration.Triggered,
                ["CalibrationWindowed"] = calibration.InWindow, ["GlobalGainCrystals"] = calibration.CrystalsOnGlobalGain,
                ["Scored"] = JsonSerializer.SerializeToNode(scored), ["Triggered"] = JsonSerializer.SerializeToNode(triggered),
                ["Windowed"] = JsonSerializer.SerializeToNode(windowed), ["Assigned"] = JsonSerializer.SerializeToNode(assigned),
                ["Conversions"] = conversions, ["Displacement"] = JsonSerializer.SerializeToNode(displacement),
                ["AssignedTotal"] = count, ["SumDxMm"] = sumX, ["SumDyMm"] = sumY,
                ["SumSquaredAngleX"] = squaredX, ["SumSquaredAngleY"] = squaredY,
                ["WeakestAcceptance"] = finite.Length == n * n ? finite[0] : null,
                ["MedianAcceptance"] = finite.Length == n * n ? (finite[(finite.Length - 1) / 2] + finite[finite.Length / 2]) / 2 : null };
        }
        foreach (var (line, index) in recipe["EnergiesKeV"]!.AsArray().Select((e, i) => (e!.GetValue<double>(), (uint)i)))
        {
            ro.Calibration.EnergyKeV = line;
            var flood = ReadoutFlood.Transport(cfg, line, n * n * ro.Calibration.EventsPerCrystal, Seed(2, index)); Stage($"TransportCal{index}");
            var cal = ReadoutCalibration.Build(device, processor, flood, ro.Calibration, Random(3, index), Random(4, index)); Stage($"Calibration{index}");
            var heldOut = ReadoutFlood.Transport(cfg, line, n * n * validation, Seed(10, index)); Stage($"TransportHeldOut{index}");
            cells.Add(Measure(heldOut, cal, line, 0, index)); Stage($"HeldOut{index}");
            var rateSites = heldOut.Take(rateHits).ToArray();
            foreach (var rate in recipe["RatesCps"]!.AsArray().Select(v => v!.GetValue<double>()))
            { cells.Add(Measure(rateSites, cal, line, rate, index)); Stage($"Rate{index}-{rate}"); }
            if (phase == "dev")
            {
                var refConfig = JsonSerializer.Deserialize<FloodCalibrationConfig>(JsonSerializer.Serialize(ro.Calibration))!;
                refConfig.EventsPerCrystal *= 4; refConfig.BinsPerCrystal *= 2;
                // Same seed: nested calibration transport and response. Raw smoothing width is held fixed.
                refConfig.SmoothingSigmaBins *= 2;
                var refFlood = ReadoutFlood.Transport(cfg, line, n * n * refConfig.EventsPerCrystal, Seed(2, index));
                var refined = ReadoutCalibration.Build(device, processor, refFlood, refConfig, Random(3, index), Random(4, index));
                var numerical = output["Numerical"]!.AsObject();
                numerical[$"Calibration-{index}"] = MarkerDelta(cal.Lut, refined.Lut, pitch, distance);
                Stage($"CalibrationRefinement{index}");
            }
        }
        if (phase == "dev")
        {
            var photonConfig = JsonSerializer.Deserialize<ReadoutConfig>(JsonSerializer.Serialize(ro))!;
            photonConfig.Optics.PhotonsPerBin *= 4;
            var photons = new ReadoutDevice(cfg.Detector, photonConfig, Seed(1)); Stage("PhotonRefinement");
            photonConfig.Optics.DepthBins *= 2;
            var doi = new ReadoutDevice(cfg.Detector, photonConfig, Seed(1)); Stage("DoiRefinement");
            output["Numerical"]!["Photons"] = OpticalDelta(device, photons, pitch, distance);
            output["Numerical"]!["Doi"] = OpticalDelta(photons, doi, pitch, distance);
        }
        output["TimingsSeconds"] = JsonSerializer.SerializeToNode(times);
        output["PeakWorkingSetBytes"] = Process.GetCurrentProcess().PeakWorkingSet64;
        Console.WriteLine(output.ToJsonString());
    }

    private static JsonObject MarkerDelta(FloodLut a, FloodLut b, double pitch, double distance)
    {
        if (!a.Succeeded || !b.Succeeded) return new() { ["Supported"] = false, ["Failure"] = "calibration refinement has a failed LUT" };
        double sx = 0, sy = 0;
        for (int c = 0; c < a.Peaks.Count; c++)
        {
            double spacingX = double.PositiveInfinity, spacingY = double.PositiveInfinity;
            int col = c % a.CountX, row = c / a.CountX;
            if (col > 0) spacingX = Math.Min(spacingX, b.Peaks[c].X - b.Peaks[c - 1].X);
            if (col + 1 < a.CountX) spacingX = Math.Min(spacingX, b.Peaks[c + 1].X - b.Peaks[c].X);
            if (row > 0) spacingY = Math.Min(spacingY, b.Peaks[c].Y - b.Peaks[c - a.CountX].Y);
            if (row + 1 < a.CountY) spacingY = Math.Min(spacingY, b.Peaks[c + a.CountX].Y - b.Peaks[c].Y);
            sx = Math.Max(sx, Math.Abs(a.Peaks[c].X - b.Peaks[c].X) / spacingX * pitch / distance);
            sy = Math.Max(sy, Math.Abs(a.Peaks[c].Y - b.Peaks[c].Y) / spacingY * pitch / distance);
        }
        return new() { ["Supported"] = true, ["MaxAngleX"] = sx, ["MaxAngleY"] = sy };
    }

    private static JsonObject OpticalDelta(ReadoutDevice a, ReadoutDevice b, double pitch, double distance)
    {
        var ac = new double[4]; var bc = new double[4]; double sx = 0, sy = 0;
        for (int c = 0; c < a.Crystals.Count; c++)
            for (int depth = 0; depth < b.Optics.DepthBins; depth++)
            {
                InteractionSite[] sites = [new(0, 0, -(depth + .5) * a.Crystals.DepthMm / b.Optics.DepthBins, 0, 661.7,
                    c % a.Crystals.CountX, c / a.Crystals.CountX)];
                a.Expected(sites, ac); b.Expected(sites, bc);
                a.Position(ac, out double ax, out double ay); b.Position(bc, out double bx, out double by);
                double Reference(int neighbour, bool xAxis)
                {
                    var s = sites[0] with { CrystalX = neighbour % a.Crystals.CountX, CrystalY = neighbour / a.Crystals.CountX };
                    b.Expected([s], bc); b.Position(bc, out double x, out double y); return xAxis ? x : y;
                }
                double spacingX = double.PositiveInfinity, spacingY = double.PositiveInfinity;
                int col = c % a.Crystals.CountX, row = c / a.Crystals.CountX;
                if (col > 0) spacingX = Math.Min(spacingX, bx - Reference(c - 1, true));
                if (col + 1 < a.Crystals.CountX) spacingX = Math.Min(spacingX, Reference(c + 1, true) - bx);
                if (row > 0) spacingY = Math.Min(spacingY, by - Reference(c - a.Crystals.CountX, false));
                if (row + 1 < a.Crystals.CountY) spacingY = Math.Min(spacingY, Reference(c + a.Crystals.CountX, false) - by);
                sx = Math.Max(sx, Math.Abs(ax - bx) / spacingX * pitch / distance);
                sy = Math.Max(sy, Math.Abs(ay - by) / spacingY * pitch / distance);
            }
        return new() { ["Supported"] = true, ["MaxAngleX"] = sx, ["MaxAngleY"] = sy };
    }

    private static string Hash(string path) => Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(File.ReadAllBytes(path))).ToLowerInvariant();
}
