using System.Diagnostics;
using System.Security.Cryptography;
using System.Text.Json;
using Gcam.Configuration;
using Gcam.Core;
using Gcam.Decoding;
using Gcam.Simulation;

namespace Gcam.EvidenceProbe;

/// <summary>Targeted BD-7 diagnostics. Independent calibration maps/Poisson acquisitions are reused as one seed
/// cluster, never counted as independent calibrations. Wrong-bound fitting bypasses only the public metadata guard
/// after proving that the same input is rejected by that guard; it is labelled diagnostic and cannot qualify.</summary>
internal static class BackgroundSensitivityRecipe
{
    public static void Run(int seed, string samplesDir, bool pilot = false, int version = 1)
    {
        string folder = Path.Combine(samplesDir, "evidence", "background-shape");
        BackgroundRequest.Validate(folder, version);
        byte[] requestBytes = File.ReadAllBytes(BackgroundRequest.PathFor(folder, version));
        byte[] pinBytes = File.ReadAllBytes(Path.Combine(folder, "pinned-v1.json"));
        using var requestDocument = JsonDocument.Parse(requestBytes);
        using var pinDocument = JsonDocument.Parse(pinBytes);
        using var seedDocument = JsonDocument.Parse(File.ReadAllBytes(Path.Combine(samplesDir, "evidence", "seeds.json")));
        if (!seedDocument.RootElement.GetProperty(pilot ? "BG_SELECTION16" : "BG_VALIDATION32").EnumerateArray().Any(v => v.GetInt32() == seed))
            throw new ArgumentException("Sensitivity seed is outside the declared phase list.");
        string Hash(byte[] bytes) => Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
        if (pinDocument.RootElement.GetProperty("RequestSha256").GetString() != Hash(File.ReadAllBytes(BackgroundRequest.PathFor(folder, 1)))
            || pinDocument.RootElement.GetProperty("SeedListsSha256").GetString() != Hash(File.ReadAllBytes(Path.Combine(samplesDir, "evidence", "seeds.json"))))
            throw new InvalidDataException("Sensitivity request or seed lists differ from the pin.");
        int iterations = pinDocument.RootElement.GetProperty("Iterations").GetInt32();
        var request = requestDocument.RootElement;
        var spectrum = IncidentSpectrumFile.Load(Path.Combine(samplesDir, "ambient", "terrestrial-unscear2000-v1.json"),
            "4d48bc4272ce4c25ff6ae9b13ed7583e3c8f467afffdb0225956a70cc435b3f4");
        var thresholds = JsonSerializer.Deserialize<AmbientGateStudy.Thresholds>(File.ReadAllBytes(Path.Combine(samplesDir, "evidence", "ambient", "gate-thresholds-v2.json")))!;
        long histories = request.GetProperty("AmbientHistories").GetInt64(), photons = request.GetProperty("SourcePhotons").GetInt64();
        int repeats = pilot ? request.GetProperty("PilotRepeats").GetInt32() : request.GetProperty("ValidationRepeats").GetInt32();
        const double TimeS = 60, Field = .2;
        var watch = Stopwatch.StartNew();
        var heads = new Dictionary<string, (SimulationConfig Config, GateMaps Truth, GateMaps Calibration, GateMaps Front)>();
        foreach (string scenario in new[] { "scenario.json", "scenario_handheld.json" })
        {
            int index = heads.Count; var c = ConfigLoader.Load(Path.Combine(samplesDir, scenario));
            // Match the main study's truth/calibration purpose keys, and add a separate front-only calibration.
            heads[scenario] = (c,
                GateResponse.Ambient(c, AmbientGeometry.BareCrystalAllFaces, spectrum, histories, GateResponse.StreamSeed(seed, 330000u + (uint)index), [CountingWindow.Open()]),
                GateResponse.Ambient(c, AmbientGeometry.BareCrystalAllFaces, spectrum, histories, GateResponse.StreamSeed(seed, 331000u + (uint)index), [CountingWindow.Open()]),
                GateResponse.Ambient(c, AmbientGeometry.FrontOnlyThroughMask, spectrum, histories, GateResponse.StreamSeed(seed, 333000u + (uint)index), [CountingWindow.Open()]));
        }
        var cells = new List<object>(); int caseIndex = 0;
        foreach (var spec in request.GetProperty("Cases").EnumerateArray())
        {
            string name = spec.GetProperty("Name").GetString()!;
            var (head, truth, calibration, front) = heads[spec.GetProperty("Scenario").GetString()!];
            var c = head.Clone(); double x = spec.GetProperty("XMm").GetDouble();
            c.Geometry.SourceMaskDistanceMm = spec.GetProperty("ZMm").GetDouble() - c.Geometry.MaskDetectorDistanceMm;
            c.Source.Position = [x, 0, 0];
            var source = GateResponse.Source(c, photons, GateResponse.StreamSeed(seed, 332000u + (uint)caseIndex), [CountingWindow.Open()]);
            double pilotRate = spec.GetProperty("PilotRatePerBq").GetDouble();
            foreach (bool cyclic in new[] { true, false })
            {
                c.Decoder.Cyclic = cyclic;
                var search = new CorrelationSearch(c); var factory = new DefaultSimulationFactory();
                var mlem = factory.CreateMlemDecoder(c, c.Source.EnergyKeV); var geo = DefaultSimulationFactory.ReconstructionGeometry(c);
                var meta = new BackgroundCalibrationMetadata(c.Detector.PixelsX, c.Detector.PixelsY, c.Detector.PixelPitchMm,
                    DefaultSimulationFactory.BackgroundHeadResponseId(c), "open", "BareCrystalAllFaces", spectrum.ContentHash);
                double calibrationRate = calibration.TotalRate(0) * Field, trueB = truth.TotalRate(0) * Field * TimeS;
                var normal = new BackgroundCalibration(calibration.RatePerPixel[0], meta, calibrationRate, Field * calibration.TotalRateStandardError[0]);
                var joint = new BackgroundAwareDecoder(mlem, factory.CreateDecoder(c)!, geo, normal, meta, BackgroundEstimator.JointMlem,
                    iterations, c.Decoder.SubCellInterpolation, TimeS);
                var variants = new List<(string Name, BackgroundCalibration Calibration, bool Wrong)> { ("transported", normal, false) };
                foreach (double acquisitionS in new[] { 600.0, 3600.0 })
                {
                    var mean = calibration.RatePerPixel[0].Select(v => v * Field * acquisitionS).ToArray();
                    var rng = DefaultRandom.FromKey(DefaultRandom.Key(seed, 360000u + (uint)caseIndex * 10 + (acquisitionS == 600 ? 0u : 1u)));
                    var measured = mean.Select(v => (double)Sampling.PoissonExact(rng, v)).ToArray(); double n = measured.Sum();
                    variants.Add(($"measured{acquisitionS}s", new(measured, meta, n / acquisitionS, Math.Sqrt(n) / acquisitionS, acquisitionS, n), false));
                }
                var wrong = new BackgroundCalibration(front.RatePerPixel[0], meta with { Bound = "FrontOnlyThroughMask" }, calibrationRate);
                bool refused = false;
                try { wrong.RequireMatch(meta); } catch (ArgumentException) { refused = true; }
                if (!refused) throw new InvalidOperationException("The public surface did not refuse the wrong bound.");
                variants.Add(("wrong-bound-diagnostic", wrong, true));
                foreach (int level in new[] { 250, 1000, -1 })
                {
                    string label = level < 0 ? "default" : "S=" + level;
                    double activity = level < 0 ? 1e6 : level / (TimeS * pilotRate);
                    var sMean = source.RatePerPixel[0].Select(v => v * TimeS * activity).ToArray();
                    var bMean = truth.RatePerPixel[0].Select(v => v * TimeS * Field).ToArray();
                    var sourceRng = DefaultRandom.FromKey(DefaultRandom.Key(seed, 370000u + (uint)caseIndex * 10 + (uint)(level < 0 ? 2 : level == 250 ? 0 : 1)));
                    var backgroundRng = DefaultRandom.FromKey(DefaultRandom.Key(seed, 371000u + (uint)caseIndex * 10 + (uint)(level < 0 ? 2 : level == 250 ? 0 : 1)));
                    var aggregate = new Dictionary<string, BackgroundShapeRecipe.Moments>();
                    foreach (var variant in variants) foreach (string estimator in new[] { "E4", "E5", "E6" }) aggregate[variant.Name + "|" + estimator] = new();
                    foreach (double offset in new[] { -.5, -.25, -.1, .1, .25, .5 })
                        foreach (string estimator in new[] { "E5", "E6" }) aggregate[$"scale={offset}|{estimator}"] = new();
                    var models = variants.ToDictionary(v => v.Name, v => search.ModelFor(v.Calibration.Shape));
                    double threshold = thresholds.PerConfiguration[AmbientGateStudy.NullKey(name, TimeS, Field, AmbientGeometry.BareCrystalAllFaces, "open")];
                    for (int r = 0; r < repeats; r++)
                    {
                        var sourceCounts = sMean.Select(v => Sampling.PoissonExact(sourceRng, v)).ToArray();
                        var observed = sourceCounts.Zip(bMean, (s, b) => s + Sampling.PoissonExact(backgroundRng, b)).ToArray();
                        var raw = observed.Select(v => (double)v).ToArray(); int total = observed.Sum();
                        var rawRecon = new double[search.GridPoints]; search.Reconstruct(observed, rawRecon);
                        var idealCorrelation = new double[search.GridPoints]; search.Reconstruct(sourceCounts, idealCorrelation);
                        var idealE6 = search.DecoderEstimate(idealCorrelation);
                        var plain = mlem.Snapshots(sourceCounts.Select(v => (double)v).ToArray(), c.Detector.PixelsX, c.Detector.PixelsY, [iterations])[0];
                        var idealPosition = joint.Reconstruct(plain).Estimate.Position; var idealE5 = (idealPosition.X, idealPosition.Y);
                        foreach (var variant in variants)
                        {
                            var gate = search.Search(models[variant.Name], total, rawRecon);
                            bool trusted = thresholds.Trusts(gate.Z, threshold);
                            bool gateAssociated = gate.Index >= 0 && Associated(search, gate.X, gate.Y, x, c);
                            double b = variant.Calibration.ExpectedCounts(TimeS);
                            var idealJoint = joint.JointModel.Snapshots(sourceCounts.Select(v => (double)v).ToArray(), variant.Calibration.Shape, [iterations])[0];
                            var ip = joint.Reconstruct(idealJoint.Source).Estimate.Position;
                            var result = joint.JointModel.Snapshots(raw, variant.Calibration.Shape, [iterations])[0];
                            var p = joint.Reconstruct(result.Source).Estimate.Position;
                            Add(aggregate[variant.Name + "|E4"], (p.X, p.Y), (ip.X, ip.Y), result.BackgroundCounts, gate.Z, trusted, gateAssociated);
                            Known(variant.Name, variant.Calibration.Shape, b, gate.Z, trusted, gateAssociated);
                        }
                        var mainGate = search.Search(models["transported"], total, rawRecon);
                        foreach (double offset in new[] { -.5, -.25, -.1, .1, .25, .5 })
                            Known($"scale={offset}", normal.Shape, normal.ExpectedCounts(TimeS) * (1 + offset), mainGate.Z,
                                thresholds.Trusts(mainGate.Z, threshold), mainGate.Index >= 0 && Associated(search, mainGate.X, mainGate.Y, x, c));

                        void Known(string variantName, IReadOnlyList<double> shape, double b, double z, bool trusted, bool gateAssociated)
                        {
                            var background = shape.Select(v => v * b).ToArray();
                            var fixedSource = mlem.Snapshots(raw, c.Detector.PixelsX, c.Detector.PixelsY, [iterations], background)[0];
                            var p = joint.Reconstruct(fixedSource).Estimate.Position;
                            Add(aggregate[variantName + "|E5"], (p.X, p.Y), idealE5, b, z, trusted, gateAssociated);
                            var signedRecon = new double[search.GridPoints]; search.ReconstructExpected(background, signedRecon);
                            for (int k = 0; k < signedRecon.Length; k++) signedRecon[k] = rawRecon[k] - signedRecon[k];
                            Add(aggregate[variantName + "|E6"], search.DecoderEstimate(signedRecon), idealE6, b, z, trusted, gateAssociated);
                        }
                        void Add(BackgroundShapeRecipe.Moments m, (double X, double Y) p, (double X, double Y) ideal,
                            double b, double z, bool trusted, bool gateAssociated)
                        {
                            m.Add(p, ideal, x, Associated(search, p.X, p.Y, x, c), trusted, gateAssociated, b, total); m.AddZ(z);
                        }
                    }
                    cells.Add(new { Key = $"{name}|cyclic={cyclic}|t={TimeS}|{label}|F={Field}", Case = name, Cyclic = cyclic,
                        TimeS, Level = label, Field, SourceCounts = sMean.Sum(), ExpectedBackgroundCounts = trueB,
                        CalibrationCounts = normal.ExpectedCounts(TimeS), GridStepMm = search.StepMm, TargetMm = Math.Sqrt(2) * search.StepMm,
                        WrongBoundRefused = refused, CalibrationAcquisitions = variants.ToDictionary(v => v.Name,
                            v => new { v.Calibration.CalibrationLiveTimeS, v.Calibration.CalibrationCounts, v.Calibration.RateCps, v.Calibration.RateStandardUncertaintyCps }),
                        Estimators = aggregate });
                }
            }
            caseIndex++;
            Console.WriteLine($"sensitivity seed={seed} {name} cumulative={watch.Elapsed.TotalSeconds:F1}s");
        }
        File.WriteAllText("background-shape-sensitivity.json", JsonSerializer.Serialize(new { SchemaVersion = 1, Phase = pilot ? "sensitivity-pilot" : "sensitivity", Seed = seed,
            Repeats = repeats, Iterations = new[] { iterations }, PinnedSha256 = Hash(pinBytes), RequestSha256 = Hash(requestBytes),
            TotalSeconds = watch.Elapsed.TotalSeconds, Cells = cells }, new JsonSerializerOptions { WriteIndented = true, NewLine = "\n" }) + "\n");
    }

    private static bool Associated(CorrelationSearch search, double ex, double ey, double x, SimulationConfig config)
        => search.AngleBetweenDeg(ex, ey, x, 0) <= Math.Atan(config.Mask.CellPitchMm / config.Geometry.MaskDetectorDistanceMm) * 180 / Math.PI;
}
