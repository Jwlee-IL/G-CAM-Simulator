using System.Collections;
using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Nodes;
using Gcam.Configuration;
using Gcam.Core;
using Gcam.Detector;
using Gcam.Simulation;

namespace Gcam.EvidenceProbe;

/// <summary>TODO-41 recipe v2: LUT v2 (LatticeGrowth) on the fine heads, legacy LUT on the 12 × 12 Baseline. The isolated
/// held-out row carries the per-crystal acceptance; rate rows carry the spatial statistic and the global throughput.
/// Development adds the three-level numerical refinement of rule v2: calibration histories 2k / 8k / 32k per crystal
/// (nested floods, the same held-out events and streams, compared on the added angular RMS), DOI bins 8 / 16 / 32 and
/// optical photons 100k / 400k (expected-position proxy), plus one independent replicate of the recipe level for each
/// (purpose keys 51 – 54: the statistical floor of a difference, recorded for the rule). Purpose keys 1 – 23 are those of
/// v1.</summary>
internal static class StudioReadoutRecipeV2
{
    private sealed class StreamedFlood(Func<IEnumerable<InteractionSite[]>> source, int count) : IReadOnlyList<InteractionSite[]>
    {
        public int Count => count;
        public InteractionSite[] this[int index] => throw new NotSupportedException("streamed flood");
        public IEnumerator<InteractionSite[]> GetEnumerator() => source().GetEnumerator();
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }

    internal static void Run(int seed, string samples, string recipeFile, JsonNode recipe, string geometry, string phase, string familyHash)
    {
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
        bool baseline = n == recipe["BaselineCount"]!.GetValue<int>();
        var ro = new ReadoutConfig { Mode = ReadoutMode.FourOutputAnger, Network = new() { ColumnResistanceOhm = 10 } };
        ro.Calibration.Method = Enum.Parse<FloodCalibrationMethod>(recipe[baseline ? "BaselineMethod" : "Method"]!.GetValue<string>());
        ro.Network.SolveMethod = Enum.Parse<ChargeNetworkSolveMethod>(recipe[baseline ? "BaselineSolve" : "Solve"]!.GetValue<string>());
        ro.Optics.PhotonsPerBin = recipe["OpticalPhotons"]!.GetValue<int>();
        ro.Optics.DepthBins = recipe["DepthBins"]!.GetValue<int>();
        ro.Calibration.EventsPerCrystal = recipe["CalibrationHistoriesPerCrystal"]!.GetValue<int>();
        ro.Calibration.BinsPerCrystal = recipe["HistogramBinsPerCrystal"]!.GetValue<int>();
        int validation = recipe["ValidationHistoriesPerCrystal"]!.GetValue<int>();
        int rateHits = recipe["RateHits"]!.GetValue<int>();
        var refinement = recipe["Refinement"]!;
        int[] Levels(string key) => refinement[key]!.AsArray().Select(v => v!.GetValue<int>()).ToArray();
        int[] calibrationLevels = Levels("CalibrationHistoriesPerCrystal"), doiLevels = Levels("DepthBins"),
            photonLevels = Levels("PhotonsPerBin");
        if (calibrationLevels[0] != ro.Calibration.EventsPerCrystal || doiLevels[0] != ro.Optics.DepthBins
            || photonLevels[0] != ro.Optics.PhotonsPerBin)
            throw new InvalidOperationException("refinement levels must start at the recipe level");
        var clock = Stopwatch.StartNew(); var times = new Dictionary<string, double>();
        int Seed(uint purpose, uint line = 0) => unchecked((int)DefaultRandom.Key(seed, purpose + 100 * line));
        IRandom Random(uint purpose, uint line = 0) => DefaultRandom.FromKey(DefaultRandom.Key(seed, purpose + 100 * line));
        void Stage(string label) { times[label] = clock.Elapsed.TotalSeconds; Console.Error.WriteLine($"{geometry}: {label} {clock.Elapsed.TotalSeconds:F3}s"); clock.Restart(); }
        var device = new ReadoutDevice(cfg.Detector, ro, Seed(1)); Stage("Build");
        var processor = new ReadoutPulseProcessor(device, ro.Pulse, ro.Trigger);
        var output = new JsonObject { ["SchemaVersion"] = 2, ["Seed"] = seed, ["Geometry"] = geometry,
            ["Phase"] = phase, ["FamilySha256"] = familyHash, ["RecipeSha256"] = Hash(recipeFile), ["RuleSha256"] = recipe["Rule"]!["Sha256"]!.GetValue<string>(),
            ["Method"] = ro.Calibration.Method.ToString(), ["AllocationRad"] = pitch / distance / Math.Sqrt(12),
            ["Cells"] = new JsonArray(), ["Numerical"] = new JsonObject() };
        var cells = output["Cells"]!.AsArray();
        var numerical = output["Numerical"]!.AsObject();
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
            // Single-crystal histories (every deposit in one crystal): the readout's identification alone, without the
            // light-centroid placement of multi-crystal Compton histories (theme 68: by design, not a LUT error).
            var single = new bool[sites.Count];
            var sScored = new int[n * n]; var sWindowed = new int[n * n]; var sAssigned = new int[n * n]; var sCorrect = new int[n * n];
            double sSquaredX = 0, sSquaredY = 0; int sCount = 0;
            void Conversion(ReadoutEvent ev)
            {
                conversions++;
                if (ev.DominantHit < 0 || ev.DominantHit >= truth.Length) throw new InvalidOperationException("invalid dominant-hit reference");
                int h = ev.DominantHit, t = truth[h];
                if (retained[h]) return;
                retained[h] = true; triggered[t]++;
                if (!device.Position(ev.Codes, out double x, out double y)) return;
                if (Math.Abs(device.Sum(ev.Codes) - calibration.GlobalPeakSum) > ro.Calibration.WindowFraction * calibration.GlobalPeakSum) return;
                windowed[t]++;
                if (single[h]) sWindowed[t]++;
                int c = calibration.Lut.Lookup(x, y);
                if (c < 0) return;
                assigned[t]++; count++;
                double dx = (c % n - t % n) * pitch, dy = (c / n - t / n) * pitch;
                double ax = Math.Atan(dx / distance), ay = Math.Atan(dy / distance);
                if (single[h])
                {
                    sAssigned[t]++; sCount++; if (c == t) sCorrect[t]++;
                    sSquaredX += ax * ax; sSquaredY += ay * ay;
                }
                sumX += dx; sumY += dy; squaredX += ax * ax; squaredY += ay * ay;
                string key = $"{c % n - t % n},{c / n - t / n}";
                displacement[key] = displacement.GetValueOrDefault(key) + 1;
            }
            double time = 0;
            for (int h = 0; h < sites.Count; h++)
            {
                truth[h] = ReadoutDevice.DirectCrystal(sites[h], n); scored[truth[h]]++;
                var first = sites[h][0];
                single[h] = sites[h].All(s => s.CrystalX == first.CrystalX && s.CrystalY == first.CrystalY);
                if (single[h]) sScored[truth[h]]++;
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
                ["Single"] = new JsonObject { ["Scored"] = JsonSerializer.SerializeToNode(sScored),
                    ["Windowed"] = JsonSerializer.SerializeToNode(sWindowed), ["Assigned"] = JsonSerializer.SerializeToNode(sAssigned),
                    ["Correct"] = JsonSerializer.SerializeToNode(sCorrect), ["AssignedTotal"] = sCount,
                    ["SumSquaredAngleX"] = sSquaredX, ["SumSquaredAngleY"] = sSquaredY },
                ["WeakestAcceptance"] = finite.Length == n * n ? finite[0] : null,
                ["MedianAcceptance"] = finite.Length == n * n ? (finite[(finite.Length - 1) / 2] + finite[finite.Length / 2]) / 2 : null };
        }
        foreach (var (line, index) in recipe["EnergiesKeV"]!.AsArray().Select((e, i) => (e!.GetValue<double>(), (uint)i)))
        {
            ro.Calibration.EnergyKeV = line;
            var flood = ReadoutFlood.Transport(cfg, line, n * n * ro.Calibration.EventsPerCrystal, Seed(2, index)); Stage($"TransportCal{index}");
            var cal = ReadoutCalibration.Build(device, processor, flood, ro.Calibration, Random(3, index), Random(4, index)); Stage($"Calibration{index}");
            flood = null;
            var heldOut = ReadoutFlood.Transport(cfg, line, n * n * validation, Seed(10, index)); Stage($"TransportHeldOut{index}");
            var isolated = Measure(heldOut, cal, line, 0, index);
            cells.Add(isolated); Stage($"HeldOut{index}");
            var rateSites = heldOut.Take(rateHits).ToArray();
            foreach (var rate in recipe["RatesCps"]!.AsArray().Select(v => v!.GetValue<double>()))
            { cells.Add(Measure(rateSites, cal, line, rate, index)); Stage($"Rate{index}-{rate}"); }
            if (phase == "dev")
            {
                // Nested floods: the same transport seed and response / noise streams, so a smaller level is a prefix of
                // a larger one; the same held-out events and streams measure every level.
                var levels = new JsonArray();
                foreach (int histories in calibrationLevels)
                {
                    ReadoutCalibration level;
                    if (histories == ro.Calibration.EventsPerCrystal) level = cal;
                    else
                    {
                        var levelConfig = JsonSerializer.Deserialize<FloodCalibrationConfig>(JsonSerializer.Serialize(ro.Calibration))!;
                        levelConfig.EventsPerCrystal = histories;
                        int total = n * n * histories;
                        var streamed = new StreamedFlood(() => ReadoutFlood.Stream(cfg, line, total, Seed(2, index)), total);
                        level = ReadoutCalibration.Build(device, processor, streamed, levelConfig, Random(3, index), Random(4, index));
                    }
                    var row = histories == ro.Calibration.EventsPerCrystal ? isolated : Measure(heldOut, level, line, 0, index);
                    long assignedTotal = row["AssignedTotal"]!.GetValue<int>();
                    levels.Add(new JsonObject { ["HistoriesPerCrystal"] = histories, ["Succeeded"] = level.Succeeded,
                        ["Failure"] = level.Failure, ["AssignedTotal"] = assignedTotal,
                        ["RmsX"] = assignedTotal > 0 ? Math.Sqrt(row["SumSquaredAngleX"]!.GetValue<double>() / assignedTotal) : null,
                        ["RmsY"] = assignedTotal > 0 ? Math.Sqrt(row["SumSquaredAngleY"]!.GetValue<double>() / assignedTotal) : null,
                        ["SingleRmsX"] = SingleRms(row, "X"), ["SingleRmsY"] = SingleRms(row, "Y") });
                    Stage($"CalibrationLevel{index}-{histories}");
                }
                // An independent replicate of the recipe level (own flood and streams, the same held-out events): the
                // statistical floor of a level difference, recorded for the rule (not used by rule v2).
                var replicate = ReadoutCalibration.Build(device, processor,
                    ReadoutFlood.Transport(cfg, line, n * n * ro.Calibration.EventsPerCrystal, Seed(52, index)),
                    ro.Calibration, Random(53, index), Random(54, index));
                var replicateRow = Measure(heldOut, replicate, line, 0, index);
                long replicateTotal = replicateRow["AssignedTotal"]!.GetValue<int>();
                Stage($"CalibrationReplicate{index}");
                numerical[$"Calibration-{index}"] = new JsonObject { ["Levels"] = levels, ["Replicate"] = new JsonObject {
                    ["Succeeded"] = replicate.Succeeded, ["Failure"] = replicate.Failure, ["AssignedTotal"] = replicateTotal,
                    ["RmsX"] = replicateTotal > 0 ? Math.Sqrt(replicateRow["SumSquaredAngleX"]!.GetValue<double>() / replicateTotal) : null,
                    ["RmsY"] = replicateTotal > 0 ? Math.Sqrt(replicateRow["SumSquaredAngleY"]!.GetValue<double>() / replicateTotal) : null,
                    ["SingleRmsX"] = SingleRms(replicateRow, "X"), ["SingleRmsY"] = SingleRms(replicateRow, "Y") } };
            }
            heldOut = null; rateSites = null;
        }
        if (phase == "dev")
        {
            var photonConfig = JsonSerializer.Deserialize<ReadoutConfig>(JsonSerializer.Serialize(ro))!;
            photonConfig.Optics.PhotonsPerBin = photonLevels[1];
            var photons = new ReadoutDevice(cfg.Detector, photonConfig, Seed(1)); Stage("PhotonLevel1");
            numerical["Photons"] = OpticalDelta(device, photons, pitch, distance, ro.Optics.DepthBins);
            photons = null;
            var doi = new JsonArray();
            ReadoutDevice previous = device;
            foreach (int bins in doiLevels.Skip(1))
            {
                var doiConfig = JsonSerializer.Deserialize<ReadoutConfig>(JsonSerializer.Serialize(ro))!;
                doiConfig.Optics.DepthBins = bins;
                var next = new ReadoutDevice(cfg.Detector, doiConfig, Seed(1)); Stage($"DoiLevel{bins}");
                // Every DOI difference is sampled at the finest level's bin centres.
                doi.Add(OpticalDelta(previous, next, pitch, distance, doiLevels[^1]));
                previous = next;
            }
            numerical["Doi"] = new JsonObject { ["Differences"] = doi };
            // An independent optical table at the recipe level (own optics seed): the statistical floor of a table
            // difference, at the DOI comparisons' depth samples and at the photon comparison's (not used by rule v2).
            var replicateOptics = new ReadoutDevice(cfg.Detector, ro, Seed(51)); Stage("OpticsReplicate");
            numerical["OpticsReplicate"] = new JsonObject {
                ["AtDoiSamples"] = OpticalDelta(device, replicateOptics, pitch, distance, doiLevels[^1]),
                ["AtPhotonSamples"] = OpticalDelta(device, replicateOptics, pitch, distance, ro.Optics.DepthBins) };
        }
        output["TimingsSeconds"] = JsonSerializer.SerializeToNode(times);
        output["PeakWorkingSetBytes"] = Process.GetCurrentProcess().PeakWorkingSet64;
        Console.WriteLine(output.ToJsonString());
    }

    private static double? SingleRms(JsonObject row, string axis)
    {
        var s = row["Single"]!; int total = s["AssignedTotal"]!.GetValue<int>();
        return total > 0 ? Math.Sqrt(s["SumSquaredAngle" + axis]!.GetValue<double>() / total) : null;
    }

    /// <summary>Largest change of a crystal's expected raw position between two optical tables, in local spacings of the
    /// second, converted to an angle (pitch / D per spacing), over every crystal and <paramref name="depthSamples"/> depths.</summary>
    private static JsonObject OpticalDelta(ReadoutDevice a, ReadoutDevice b, double pitch, double distance, int depthSamples)
    {
        var ac = new double[4]; var bc = new double[4]; double sx = 0, sy = 0;
        int nx = a.Crystals.CountX;
        for (int c = 0; c < a.Crystals.Count; c++)
            for (int depth = 0; depth < depthSamples; depth++)
            {
                InteractionSite[] sites = [new(0, 0, -(depth + .5) * a.Crystals.DepthMm / depthSamples, 0, 661.7, c % nx, c / nx)];
                a.Expected(sites, ac); b.Expected(sites, bc);
                a.Position(ac, out double ax, out double ay); b.Position(bc, out double bx, out double by);
                (double X, double Y) Reference(int neighbour)
                {
                    var s = sites[0] with { CrystalX = neighbour % nx, CrystalY = neighbour / nx };
                    b.Expected([s], bc); b.Position(bc, out double x, out double y); return (x, y);
                }
                double spacingX = double.PositiveInfinity, spacingY = double.PositiveInfinity;
                int col = c % nx, row = c / nx;
                if (col > 0) spacingX = Math.Min(spacingX, bx - Reference(c - 1).X);
                if (col + 1 < nx) spacingX = Math.Min(spacingX, Reference(c + 1).X - bx);
                if (row > 0) spacingY = Math.Min(spacingY, by - Reference(c - nx).Y);
                if (row + 1 < a.Crystals.CountY) spacingY = Math.Min(spacingY, Reference(c + nx).Y - by);
                sx = Math.Max(sx, Math.Abs(ax - bx) / spacingX * pitch / distance);
                sy = Math.Max(sy, Math.Abs(ay - by) / spacingY * pitch / distance);
            }
        return new() { ["Supported"] = true, ["MaxAngleX"] = sx, ["MaxAngleY"] = sy };
    }

    private static string Hash(string path) => Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(File.ReadAllBytes(path))).ToLowerInvariant();
}
