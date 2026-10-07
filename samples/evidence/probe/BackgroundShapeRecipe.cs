using System.Diagnostics;
using System.Globalization;
using System.Security.Cryptography;
using System.Text.Json;
using Gcam.Configuration;
using Gcam.Core;
using Gcam.Decoding;
using Gcam.Simulation;

namespace Gcam.EvidenceProbe;

/// <summary>TODO-33 stage 1. Calibration transport and observation streams are independent of truth.
/// Ideal/source+field pairs share their source Poisson component; ambient noise is added independently.</summary>
internal static class BackgroundShapeRecipe
{
    public static void Run(string phase, int seed, string samplesDir, int version = 1)
    {
        string root = Path.Combine(samplesDir, "evidence", "background-shape");
        BackgroundRequest.Validate(root, version);
        string requestPath = BackgroundRequest.PathFor(root, version);
        using var document = JsonDocument.Parse(File.ReadAllBytes(requestPath));
        var request = document.RootElement;
        using var seedDocument = JsonDocument.Parse(File.ReadAllBytes(Path.Combine(samplesDir, "evidence", "seeds.json")));
        string seedList = phase == "validation" ? "BG_VALIDATION32" : "BG_SELECTION16";
        if (!seedDocument.RootElement.GetProperty(seedList).EnumerateArray().Any(s => s.GetInt32() == seed))
            throw new ArgumentException("Seed is outside the pinned phase list.");
        int repeats = phase == "selection" ? request.GetProperty("SelectionRepeats").GetInt32()
            : phase == "pilot" ? request.GetProperty("PilotRepeats").GetInt32() : request.GetProperty("ValidationRepeats").GetInt32();
        string? pinSha256 = null;
        int[] iterations = [60, 120, 240, 400, 800];
        if (phase != "selection")
        {
            byte[] bytes = File.ReadAllBytes(Path.Combine(root, "pinned-v1.json"));
            using var pin = JsonDocument.Parse(bytes);
            string Hash(byte[] b) => Convert.ToHexString(SHA256.HashData(b)).ToLowerInvariant();
            if (pin.RootElement.GetProperty("RequestSha256").GetString() != Hash(File.ReadAllBytes(BackgroundRequest.PathFor(root, 1)))
                || pin.RootElement.GetProperty("SeedListsSha256").GetString() != Hash(File.ReadAllBytes(Path.Combine(samplesDir, "evidence", "seeds.json"))))
                throw new InvalidDataException("Request or seeds changed after iteration selection.");
            iterations = [pin.RootElement.GetProperty("Iterations").GetInt32()];
            pinSha256 = Hash(bytes);
        }
        var spectrum = IncidentSpectrumFile.Load(Path.Combine(samplesDir, "ambient", "terrestrial-unscear2000-v1.json"),
            "4d48bc4272ce4c25ff6ae9b13ed7583e3c8f467afffdb0225956a70cc435b3f4");
        var thresholds = JsonSerializer.Deserialize<AmbientGateStudy.Thresholds>(File.ReadAllBytes(Path.Combine(samplesDir, "evidence", "ambient", "gate-thresholds-v2.json")))!;
        long histories = request.GetProperty("AmbientHistories").GetInt64();
        long photons = request.GetProperty("SourcePhotons").GetInt64();
        var totalWatch = Stopwatch.StartNew();
        var heads = new Dictionary<string, (SimulationConfig Config, GateMaps Truth, GateMaps Calibration)>();
        foreach (string scenario in new[] { "scenario.json", "scenario_handheld.json" })
        {
            int index = heads.Count;
            var config = ConfigLoader.Load(Path.Combine(samplesDir, scenario));
            var windows = new[] { CountingWindow.Open() };
            var truth = GateResponse.Ambient(config, AmbientGeometry.BareCrystalAllFaces, spectrum, histories,
                GateResponse.StreamSeed(seed, 330000u + (uint)index), windows);
            var calibration = GateResponse.Ambient(config, AmbientGeometry.BareCrystalAllFaces, spectrum, histories,
                GateResponse.StreamSeed(seed, 331000u + (uint)index), windows);
            heads.Add(scenario, (config, truth, calibration));
        }
        double mapSeconds = totalWatch.Elapsed.TotalSeconds;
        var cells = new List<object>();
        var timings = new Dictionary<string, double>();
        int caseIndex = 0;
        foreach (var spec in request.GetProperty("Cases").EnumerateArray())
        {
            string name = spec.GetProperty("Name").GetString()!;
            var (head, truth, calibration) = heads[spec.GetProperty("Scenario").GetString()!];
            double z = spec.GetProperty("ZMm").GetDouble(), x = spec.GetProperty("XMm").GetDouble();
            var config = head.Clone();
            config.Geometry.SourceMaskDistanceMm = z - config.Geometry.MaskDetectorDistanceMm;
            config.Source.Position = [x, 0, 0];
            var source = GateResponse.Source(config, photons, GateResponse.StreamSeed(seed, 332000u + (uint)caseIndex), [CountingWindow.Open()]);
            double pilot = spec.GetProperty("PilotRatePerBq").GetDouble();
            foreach (bool cyclic in new[] { true, false })
            {
                config.Decoder.Cyclic = cyclic;
                var watch = Stopwatch.StartNew();
                var search = new CorrelationSearch(config);
                var gateModel = search.ModelFor(calibration.RatePerPixel[0]);
                var factory = new DefaultSimulationFactory();
                var geo = DefaultSimulationFactory.ReconstructionGeometry(config);
                var mlem = factory.CreateMlemDecoder(config, config.Source.EnergyKeV);
                var meta = new BackgroundCalibrationMetadata(config.Detector.PixelsX, config.Detector.PixelsY,
                    config.Detector.PixelPitchMm, DefaultSimulationFactory.BackgroundHeadResponseId(config), "open", "BareCrystalAllFaces", spectrum.ContentHash);
                var normal = new BackgroundCalibration(calibration.RatePerPixel[0], meta, calibration.TotalRate(0), calibration.TotalRateStandardError[0]);
                var joint = new BackgroundAwareDecoder(mlem, factory.CreateDecoder(config)!, geo, normal, meta,
                    BackgroundEstimator.JointMlem, iterations[^1], config.Decoder.SubCellInterpolation, 1);
                foreach (double time in new[] { 10.0, 60.0 })
                    foreach (int level in new[] { 25, 50, 100, 250, 500, 1000, -1 })
                    {
                        string label = level < 0 ? "default" : "S=" + level;
                        bool known = level is 100 or 250 or 1000 or -1;
                        double activity = level < 0 ? 1e6 : level / (time * pilot);
                        var sourceMean = source.RatePerPixel[0].Select(v => v * time * activity).ToArray();
                        var sourceDraws = new int[repeats][];
                        var idealAnswers = new Dictionary<string, (double X, double Y)[]>();
                        var idealBeta = iterations.ToDictionary(it => it, _ => new double[repeats]);
                        idealAnswers["E0"] = new (double, double)[repeats];
                        foreach (int it in iterations) idealAnswers["E4@" + it] = new (double, double)[repeats];
                        if (phase != "selection" && known) idealAnswers["E5"] = new (double, double)[repeats];
                        uint cellKey = (uint)(caseIndex * 100 + (time == 60 ? 50 : 0) + (level < 0 ? 7 : Array.IndexOf(new[] { 25, 50, 100, 250, 500, 1000 }, level)));
                        var rng = DefaultRandom.FromKey(DefaultRandom.Key(seed, 340000u + cellKey));
                        var recon = new double[search.GridPoints];
                        for (int r = 0; r < repeats; r++)
                        {
                            var counts = new int[search.Pixels]; Draw(rng, sourceMean, counts); sourceDraws[r] = counts;
                            search.Reconstruct(counts, recon); idealAnswers["E0"][r] = search.DecoderEstimate(recon);
                            var snapshots = joint.JointModel.Snapshots(counts.Select(v => (double)v).ToArray(), normal.Shape, iterations);
                            foreach (var snapshot in snapshots)
                            {
                                var position = joint.Reconstruct(snapshot.Source).Estimate.Position;
                                idealAnswers["E4@" + snapshot.Iterations][r] = (position.X, position.Y);
                                idealBeta[snapshot.Iterations][r] = snapshot.BackgroundCounts;
                            }
                            if (phase != "selection" && known)
                            {
                                var lam = mlem.Snapshots(counts.Select(v => (double)v).ToArray(), config.Detector.PixelsX, config.Detector.PixelsY, iterations)[0];
                                var position = joint.Reconstruct(lam).Estimate.Position; idealAnswers["E5"][r] = (position.X, position.Y);
                            }
                        }
                        foreach (double field in new[] { 0.0, 0.05, 0.1, 0.2 })
                        {
                            string key = $"{name}|cyclic={cyclic}|t={time}|{label}|F={field}";
                            var aggregates = new Dictionary<string, Moments>();
                            aggregates["E0"] = new(); foreach (int it in iterations) aggregates["E4@" + it] = new();
                            if (phase != "selection" && known) { aggregates["E5"] = new(); aggregates["E6"] = new(); }
                            double expectedB = field * time * truth.TotalRate(0), calibrationB = field * time * calibration.TotalRate(0);
                            var bgMean = truth.RatePerPixel[0].Select(v => v * time * field).ToArray();
                            var backgroundRng = DefaultRandom.FromKey(DefaultRandom.Key(seed, 350000u + cellKey * 4 + (uint)Array.IndexOf(new[] { 0.0, 0.05, 0.1, 0.2 }, field)));
                            double threshold = field == 0 ? 0 : thresholds.PerConfiguration[AmbientGateStudy.NullKey(name, time, field, AmbientGeometry.BareCrystalAllFaces, "open")];
                            double[]? backgroundRecon = null;
                            if (phase != "selection" && known)
                            {
                                backgroundRecon = new double[search.GridPoints];
                                search.ReconstructExpected(normal.Shape.Select(v => v * calibrationB).ToArray(), backgroundRecon);
                            }
                            for (int r = 0; r < repeats; r++)
                            {
                                var bg = new int[search.Pixels]; Draw(backgroundRng, bgMean, bg);
                                var counts = sourceDraws[r].Zip(bg, (a, b) => a + b).ToArray(); int n = counts.Sum();
                                search.Reconstruct(counts, recon);
                                var gate = search.Search(gateModel, n, recon);
                                bool trusted = field > 0 && thresholds.Trusts(gate.Z, threshold);
                                bool gateAssociated = gate.Index >= 0 && Associated(search, gate.X, gate.Y, x, config);
                                foreach (var aggregate in aggregates.Values) aggregate.AddZ(gate.Z);
                                var answer = field == 0 ? idealAnswers["E0"][r] : search.DecoderEstimate(recon);
                                aggregates["E0"].Add(answer, idealAnswers["E0"][r], x, Associated(search, answer.X, answer.Y, x, config), trusted, gateAssociated, 0, n);
                                if (field == 0)
                                {
                                    foreach (int it in iterations)
                                    {
                                        answer = idealAnswers["E4@" + it][r];
                                        aggregates["E4@" + it].Add(answer, answer, x, Associated(search, answer.X, answer.Y, x, config), trusted, gateAssociated, idealBeta[it][r], n);
                                    }
                                }
                                else
                                {
                                    var snapshots = joint.JointModel.Snapshots(counts.Select(v => (double)v).ToArray(), normal.Shape, iterations);
                                    foreach (var snapshot in snapshots)
                                    {
                                        var position = joint.Reconstruct(snapshot.Source).Estimate.Position; answer = (position.X, position.Y);
                                        aggregates["E4@" + snapshot.Iterations].Add(answer, idealAnswers["E4@" + snapshot.Iterations][r], x,
                                            Associated(search, answer.X, answer.Y, x, config), trusted, gateAssociated, snapshot.BackgroundCounts, n);
                                    }
                                }
                                if (phase != "selection" && known)
                                {
                                    var lam = field == 0 ? null : mlem.Snapshots(counts.Select(v => (double)v).ToArray(), config.Detector.PixelsX, config.Detector.PixelsY, iterations,
                                        normal.Shape.Select(v => v * calibrationB).ToArray())[0];
                                    var position = lam is null ? default : joint.Reconstruct(lam).Estimate.Position;
                                    answer = field == 0 ? idealAnswers["E5"][r] : (position.X, position.Y);
                                    aggregates["E5"].Add(answer, idealAnswers["E5"][r], x, Associated(search, answer.X, answer.Y, x, config), trusted, gateAssociated, calibrationB, n);
                                    var signed = recon.Zip(backgroundRecon!, (a, b) => a - b).ToArray(); answer = search.DecoderEstimate(signed);
                                    aggregates["E6"].Add(answer, idealAnswers["E0"][r], x, Associated(search, answer.X, answer.Y, x, config), trusted, gateAssociated, calibrationB, n);
                                }
                            }
                            cells.Add(new { Key = key, Case = name, Cyclic = cyclic, TimeS = time, Level = label, Field = field,
                                SourceCounts = sourceMean.Sum(), ExpectedBackgroundCounts = expectedB, CalibrationCounts = calibrationB,
                                CalibrationStandardUncertaintyCounts = field * time * calibration.TotalRateStandardError[0],
                                GridStepMm = search.StepMm, TargetMm = Math.Sqrt(2) * search.StepMm,
                                Estimators = aggregates });
                        }
                    }
                timings[$"{name}|cyclic={cyclic}"] = watch.Elapsed.TotalSeconds;
                Console.WriteLine($"{phase} seed={seed} {name} cyclic={cyclic} {watch.Elapsed.TotalSeconds:F1}s");
            }
            caseIndex++;
        }
        var output = new { SchemaVersion = 1, Phase = phase, Seed = seed, Repeats = repeats, Iterations = iterations,
            PinnedSha256 = pinSha256,
            RequestSha256 = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(requestPath))).ToLowerInvariant(),
            SpectrumSha256 = spectrum.ContentHash, MapSeconds = mapSeconds, TimingsSeconds = timings, TotalSeconds = totalWatch.Elapsed.TotalSeconds, Cells = cells };
        File.WriteAllText("background-shape.json", JsonSerializer.Serialize(output, new JsonSerializerOptions { WriteIndented = true, NewLine = "\n" }) + "\n");
    }

    private static void Draw(IRandom rng, double[] mean, int[] counts)
    { for (int i = 0; i < mean.Length; i++) counts[i] = Sampling.PoissonExact(rng, mean[i]); }

    private static bool Associated(CorrelationSearch search, double ex, double ey, double x, SimulationConfig config)
        => search.AngleBetweenDeg(ex, ey, x, 0) <= Math.Atan(config.Mask.CellPitchMm / config.Geometry.MaskDetectorDistanceMm) * 180 / Math.PI;

    public sealed class Moments
    {
        public int N { get; private set; }
        public int Associated { get; private set; }
        public int Trusted { get; private set; }
        public int TrustedAssociated { get; private set; }
        public int GateAssociated { get; private set; }
        public double SumDx { get; private set; }
        public double SumDy { get; private set; }
        public double SumExcessX { get; private set; }
        public double SumExcessY { get; private set; }
        public double SumSquaredError { get; private set; }
        public double SumIdealSquaredError { get; private set; }
        public double SumIdealDx { get; private set; }
        public double SumIdealDy { get; private set; }
        public int ZN { get; private set; }
        public double SumZ { get; private set; }
        public double SumZSquared { get; private set; }
        public double SumBeta { get; private set; }
        public double SumBetaSquared { get; private set; }
        public double SumCounts { get; private set; }

        public void Add((double X, double Y) answer, (double X, double Y) ideal, double truthX,
            bool associated, bool trusted, bool gateAssociated, double beta, int counts)
        {
            N++; if (associated) Associated++; if (trusted) Trusted++; if (trusted && associated) TrustedAssociated++;
            if (gateAssociated) GateAssociated++;
            double dx = answer.X - truthX, dy = answer.Y;
            SumDx += dx; SumDy += dy; SumExcessX += answer.X - ideal.X; SumExcessY += answer.Y - ideal.Y;
            SumSquaredError += dx * dx + dy * dy;
            SumIdealSquaredError += (ideal.X - truthX) * (ideal.X - truthX) + ideal.Y * ideal.Y;
            SumIdealDx += ideal.X - truthX; SumIdealDy += ideal.Y;
            SumBeta += beta; SumBetaSquared += beta * beta; SumCounts += counts;
        }

        public void AddZ(double z)
        {
            if (double.IsFinite(z)) { ZN++; SumZ += z; SumZSquared += z * z; }
        }
    }
}
