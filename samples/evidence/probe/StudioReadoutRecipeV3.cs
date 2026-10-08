using System.Collections;
using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Nodes;
using Gcam.Configuration;
using Gcam.Core;
using Gcam.Detector;
using Gcam.Simulation;

namespace Gcam.EvidenceProbe;

/// <summary>TODO-41 recipe v3 (rule v3, the paired first-interaction metric). Truth is the history's first interaction
/// (<see cref="ReadoutTruth.FirstInteraction"/>, crystal f and its XY). On the same accepted events (Anger photopeak window
/// and LUT-assigned crystal c) the direct largest-deposit crystal d of the same history is scored too; per axis the gate
/// statistic is RMS_Anger² − RMS_direct² with angles atan((c − f) · pitch / D) and atan((d − f) · pitch / D). Piled events
/// (rate rows) are scored by their dominant hit: f and d are those of the dominant hit's own history — the direct reference
/// has no pile-up, it places each history by its own largest deposit. The full-energy acceptance per first-interaction
/// crystal (accepted full-energy histories / full-energy histories) is kept as a diagnostic. Development measures the gate statistic at the refinement
/// levels (calibration histories 2k / 8k / 32k; DOI bins 8 / 16 / 32; photons 100k / 400k; each optics level recalibrated
/// on the same calibration flood and measured on the same held-out events) and at one independent replicate of the recipe
/// level per component (purpose keys 51 – 54). Purpose keys 1 – 23 are those of v1 and v2. Acceptance is the per-crystal
/// sensitivity ratio: windowed Anger events assigned to crystal k (LUT crystal c = k) / full-energy histories with f = k.</summary>
internal static class StudioReadoutRecipeV3
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
            || photonLevels[0] != ro.Optics.PhotonsPerBin || calibrationLevels.Length != 3 || doiLevels.Length != 3
            || photonLevels.Length != 2)
            throw new InvalidOperationException("refinement levels must start at the recipe level (3 / 3 / 2 levels)");
        var energies = recipe["EnergiesKeV"]!.AsArray().Select(e => e!.GetValue<double>()).ToArray();
        var clock = Stopwatch.StartNew(); var times = new Dictionary<string, double>();
        int Seed(uint purpose, uint line = 0) => unchecked((int)DefaultRandom.Key(seed, purpose + 100 * line));
        IRandom Random(uint purpose, uint line = 0) => DefaultRandom.FromKey(DefaultRandom.Key(seed, purpose + 100 * line));
        void Stage(string label) { times[label] = clock.Elapsed.TotalSeconds; Console.Error.WriteLine($"{geometry}: {label} {clock.Elapsed.TotalSeconds:F3}s"); clock.Restart(); }
        var device = new ReadoutDevice(cfg.Detector, ro, Seed(1)); Stage("Build");
        var output = new JsonObject { ["SchemaVersion"] = 3, ["Seed"] = seed, ["Geometry"] = geometry,
            ["Phase"] = phase, ["FamilySha256"] = familyHash, ["RecipeSha256"] = Hash(recipeFile), ["RuleSha256"] = recipe["Rule"]!["Sha256"]!.GetValue<string>(),
            ["Method"] = ro.Calibration.Method.ToString(), ["AllocationRad"] = pitch / distance / Math.Sqrt(12),
            ["FullEnergyToleranceKeV"] = ReadoutTruth.FullEnergyToleranceKeV,
            ["Cells"] = new JsonArray(), ["Numerical"] = new JsonObject() };
        var cells = output["Cells"]!.AsArray();
        var numerical = output["Numerical"]!.AsObject();

        JsonObject Measure(ReadoutDevice dev, IReadOnlyList<InteractionSite[]> sites, ReadoutCalibration calibration, double energy,
            double rate, uint line)
        {
            var processor = new ReadoutPulseProcessor(dev, ro.Pulse, ro.Trigger);
            var channels = new double[dev.Channels];
            int cells2 = n * n;
            // Per first-interaction crystal f.
            var scored = new int[cells2]; var full = new int[cells2]; var triggered = new int[cells2];
            var windowed = new int[cells2]; var accepted = new int[cells2]; var acceptedFull = new int[cells2];
            var assignedTo = new int[cells2];   // by LUT crystal c: the per-crystal sensitivity numerator
            var retained = new bool[sites.Count];
            var first = new int[sites.Count]; var direct = new int[sites.Count]; var isFull = new bool[sites.Count];
            var firstX = new double[sites.Count]; var firstY = new double[sites.Count]; var single = new bool[sites.Count];
            var response = Random(20, line); var noise = Random(rate == 0 ? 21u : 22u, line);
            var arrival = Random(23, line); var hits = new List<ReadoutHit>();
            int count = 0, conversions = 0, misDirect = 0;
            double sqAx = 0, sqAy = 0, sqDx = 0, sqDy = 0, sumAx = 0, sumAy = 0, sumDx = 0, sumDy = 0;
            double contAx = 0, contAy = 0, contDx = 0, contDy = 0, oldX = 0, oldY = 0;
            var dispAnger = new Dictionary<string, int>(); var dispDirect = new Dictionary<string, int>();
            var sScored = new int[cells2]; var sWindowed = new int[cells2]; var sAssigned = new int[cells2]; var sCorrect = new int[cells2];
            double sSquaredX = 0, sSquaredY = 0; int sCount = 0;
            double half = n * pitch / 2;
            double Angle(int delta) => Math.Atan(delta * pitch / distance);
            double Continuous(int crystalIndex, double position) => Math.Atan(((crystalIndex + 0.5) * pitch - half - position) / distance);
            void Conversion(ReadoutEvent ev)
            {
                conversions++;
                if (ev.DominantHit < 0 || ev.DominantHit >= first.Length) throw new InvalidOperationException("invalid dominant-hit reference");
                int h = ev.DominantHit, f = first[h];
                if (retained[h]) return;               // one acceptance per original scored history
                retained[h] = true; triggered[f]++;
                if (!dev.Position(ev.Codes, out double x, out double y)) return;
                if (Math.Abs(dev.Sum(ev.Codes) - calibration.GlobalPeakSum) > ro.Calibration.WindowFraction * calibration.GlobalPeakSum) return;
                windowed[f]++;
                int d = direct[h];
                if (single[h]) sWindowed[d]++;
                int c = calibration.Lut.Lookup(x, y);
                if (c < 0) return;
                accepted[f]++; assignedTo[c]++; count++; if (isFull[h]) acceptedFull[f]++;
                int cx = c % n - f % n, cy = c / n - f / n, dx = d % n - f % n, dy = d / n - f / n;
                double ax = Angle(cx), ay = Angle(cy), bx = Angle(dx), by = Angle(dy);
                sqAx += ax * ax; sqAy += ay * ay; sqDx += bx * bx; sqDy += by * by;
                sumAx += ax; sumAy += ay; sumDx += bx; sumDy += by;
                double cax = Continuous(c % n, firstX[h]), cay = Continuous(c / n, firstY[h]);
                double cdx = Continuous(d % n, firstX[h]), cdy = Continuous(d / n, firstY[h]);
                contAx += cax * cax; contAy += cay * cay; contDx += cdx * cdx; contDy += cdy * cdy;
                string ka = $"{cx},{cy}", kd = $"{dx},{dy}";
                dispAnger[ka] = dispAnger.GetValueOrDefault(ka) + 1; dispDirect[kd] = dispDirect.GetValueOrDefault(kd) + 1;
                // Diagnostics: the stage-1 / v2 largest-deposit numbers (c against d) and the single-crystal subset.
                if (c != d) misDirect++;
                double ox = Angle(c % n - d % n), oy = Angle(c / n - d / n); oldX += ox * ox; oldY += oy * oy;
                if (single[h]) { sAssigned[d]++; sCount++; if (c == d) sCorrect[d]++; sSquaredX += ox * ox; sSquaredY += oy * oy; }
            }
            double time = 0;
            for (int h = 0; h < sites.Count; h++)
            {
                var s = sites[h];
                var fs = s[ReadoutTruth.FirstInteraction(s)];
                first[h] = fs.CrystalY * n + fs.CrystalX; firstX[h] = fs.XMm; firstY[h] = fs.YMm;
                direct[h] = ReadoutDevice.DirectCrystal(s, n);
                isFull[h] = ReadoutTruth.IsFullEnergy(s, energy);
                single[h] = s.All(q => q.CrystalX == s[0].CrystalX && q.CrystalY == s[0].CrystalY);
                scored[first[h]]++; if (isFull[h]) full[first[h]]++; if (single[h]) sScored[direct[h]]++;
                double sum = dev.Respond(s, response, channels);
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
            JsonNode Node<T>(T value) => JsonSerializer.SerializeToNode(value)!;
            return new JsonObject { ["EnergyKeV"] = energy, ["RateCps"] = rate, ["Inputs"] = sites.Count,
                ["CalibrationSucceeded"] = calibration.Succeeded, ["Failure"] = calibration.Failure, ["Markers"] = calibration.Lut.Peaks.Count,
                ["CalibrationScored"] = calibration.Scored, ["CalibrationTriggered"] = calibration.Triggered,
                ["CalibrationWindowed"] = calibration.InWindow, ["GlobalGainCrystals"] = calibration.CrystalsOnGlobalGain,
                ["Scored"] = Node(scored), ["FullEnergy"] = Node(full), ["Triggered"] = Node(triggered),
                ["Windowed"] = Node(windowed), ["Accepted"] = Node(accepted), ["AcceptedFullEnergy"] = Node(acceptedFull),
                ["AssignedToCrystal"] = Node(assignedTo),
                ["Conversions"] = conversions, ["AcceptedTotal"] = count,
                ["SumSquaredAngerX"] = sqAx, ["SumSquaredAngerY"] = sqAy, ["SumSquaredDirectX"] = sqDx, ["SumSquaredDirectY"] = sqDy,
                ["SumAngerX"] = sumAx, ["SumAngerY"] = sumAy, ["SumDirectX"] = sumDx, ["SumDirectY"] = sumDy,
                ["DisplacementAnger"] = Node(dispAnger), ["DisplacementDirect"] = Node(dispDirect),
                ["Continuous"] = new JsonObject { ["SumSquaredAngerX"] = contAx, ["SumSquaredAngerY"] = contAy,
                    ["SumSquaredDirectX"] = contDx, ["SumSquaredDirectY"] = contDy },
                ["LargestDeposit"] = new JsonObject { ["MisIdentified"] = misDirect, ["SumSquaredAngleX"] = oldX, ["SumSquaredAngleY"] = oldY },
                ["Single"] = new JsonObject { ["Scored"] = Node(sScored), ["Windowed"] = Node(sWindowed), ["Assigned"] = Node(sAssigned),
                    ["Correct"] = Node(sCorrect), ["AssignedTotal"] = sCount, ["SumSquaredAngleX"] = sSquaredX, ["SumSquaredAngleY"] = sSquaredY } };
        }
        JsonObject Gate(JsonObject row, ReadoutCalibration calibration) => new()
        {
            ["Succeeded"] = calibration.Succeeded, ["Failure"] = calibration.Failure, ["AcceptedTotal"] = row["AcceptedTotal"]!.GetValue<int>(),
            ["SumSquaredAngerX"] = row["SumSquaredAngerX"]!.GetValue<double>(), ["SumSquaredAngerY"] = row["SumSquaredAngerY"]!.GetValue<double>(),
            ["SumSquaredDirectX"] = row["SumSquaredDirectX"]!.GetValue<double>(), ["SumSquaredDirectY"] = row["SumSquaredDirectY"]!.GetValue<double>(),
        };

        var calibrationNumerical = new JsonObject[energies.Length];
        foreach (var (line, index) in energies.Select((e, i) => (e, (uint)i)))
        {
            ro.Calibration.EnergyKeV = line;
            var processor = new ReadoutPulseProcessor(device, ro.Pulse, ro.Trigger);
            var flood = ReadoutFlood.Transport(cfg, line, n * n * ro.Calibration.EventsPerCrystal, Seed(2, index)); Stage($"TransportCal{index}");
            var cal = ReadoutCalibration.Build(device, processor, flood, ro.Calibration, Random(3, index), Random(4, index)); Stage($"Calibration{index}");
            var heldOut = ReadoutFlood.Transport(cfg, line, n * n * validation, Seed(10, index)); Stage($"TransportHeldOut{index}");
            var isolated = Measure(device, heldOut, cal, line, 0, index);
            cells.Add(isolated); Stage($"HeldOut{index}");
            var rateSites = heldOut.Take(rateHits).ToArray();
            foreach (var rate in recipe["RatesCps"]!.AsArray().Select(v => v!.GetValue<double>()))
            { cells.Add(Measure(device, rateSites, cal, line, rate, index)); Stage($"Rate{index}-{rate}"); }
            if (phase == "dev")
            {
                // Nested floods: the same transport seed and response / noise streams, so a smaller level is a prefix of a
                // larger one; the same held-out events and streams measure every level.
                var levels = new JsonArray { Gate(isolated, cal) };
                foreach (int histories in calibrationLevels.Skip(1))
                {
                    var levelConfig = JsonSerializer.Deserialize<FloodCalibrationConfig>(JsonSerializer.Serialize(ro.Calibration))!;
                    levelConfig.EventsPerCrystal = histories;
                    int total = n * n * histories;
                    var streamed = new StreamedFlood(() => ReadoutFlood.Stream(cfg, line, total, Seed(2, index)), total);
                    var level = ReadoutCalibration.Build(device, processor, streamed, levelConfig, Random(3, index), Random(4, index));
                    var entry = Gate(Measure(device, heldOut, level, line, 0, index), level);
                    entry["HistoriesPerCrystal"] = histories;
                    levels.Add(entry);
                    Stage($"CalibrationLevel{index}-{histories}");
                }
                levels[0]!["HistoriesPerCrystal"] = calibrationLevels[0];
                var replicate = ReadoutCalibration.Build(device, processor,
                    ReadoutFlood.Transport(cfg, line, n * n * ro.Calibration.EventsPerCrystal, Seed(52, index)),
                    ro.Calibration, Random(53, index), Random(54, index));
                var replicateGate = Gate(Measure(device, heldOut, replicate, line, 0, index), replicate);
                Stage($"CalibrationReplicate{index}");
                calibrationNumerical[index] = new JsonObject { ["Levels"] = levels, ["Replicate"] = replicateGate };
            }
        }
        if (phase == "dev")
        {
            for (int i = 0; i < energies.Length; i++) numerical[$"Calibration-{i}"] = calibrationNumerical[i];
            // Each optics level is recalibrated on the recipe's calibration flood and streams and measured on the same
            // held-out events: the gate statistic itself, not a position proxy.
            JsonArray OpticsLevel(ReadoutConfig config, int opticsSeed, string label)
            {
                var dev = new ReadoutDevice(cfg.Detector, config, opticsSeed); Stage(label);
                var perLine = new JsonArray();
                foreach (var (line, index) in energies.Select((e, i) => (e, (uint)i)))
                {
                    var lineConfig = JsonSerializer.Deserialize<FloodCalibrationConfig>(JsonSerializer.Serialize(ro.Calibration))!;
                    lineConfig.EnergyKeV = line;
                    var processor = new ReadoutPulseProcessor(dev, config.Pulse, config.Trigger);
                    var cal = ReadoutCalibration.Build(dev, processor,
                        ReadoutFlood.Transport(cfg, line, n * n * ro.Calibration.EventsPerCrystal, Seed(2, index)),
                        lineConfig, Random(3, index), Random(4, index));
                    var heldOut = ReadoutFlood.Transport(cfg, line, n * n * validation, Seed(10, index));
                    perLine.Add(Gate(Measure(dev, heldOut, cal, line, 0, index), cal));
                    Stage($"{label}-line{index}");
                }
                return perLine;
            }
            ReadoutConfig With(Action<ReadoutConfig> change)
            {
                var c = JsonSerializer.Deserialize<ReadoutConfig>(JsonSerializer.Serialize(ro))!; change(c); return c;
            }
            numerical["Photons"] = new JsonObject { ["PhotonsPerBin"] = photonLevels[1],
                ["Lines"] = OpticsLevel(With(c => c.Optics.PhotonsPerBin = photonLevels[1]), Seed(1), "PhotonLevel1") };
            var doi = new JsonArray();
            foreach (int bins in doiLevels.Skip(1))
                doi.Add(new JsonObject { ["DepthBins"] = bins, ["Lines"] = OpticsLevel(With(c => c.Optics.DepthBins = bins), Seed(1), $"DoiLevel{bins}") });
            numerical["Doi"] = new JsonObject { ["Levels"] = doi };
            numerical["OpticsReplicate"] = new JsonObject { ["Lines"] = OpticsLevel(With(_ => { }), Seed(51), "OpticsReplicate") };
        }
        output["TimingsSeconds"] = JsonSerializer.SerializeToNode(times);
        output["PeakWorkingSetBytes"] = Process.GetCurrentProcess().PeakWorkingSet64;
        Console.WriteLine(output.ToJsonString());
    }

    private static string Hash(string path) => Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(File.ReadAllBytes(path))).ToLowerInvariant();
}
