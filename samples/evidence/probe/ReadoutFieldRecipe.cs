using System.Diagnostics;
using System.Text.Json;
using Gcam.Configuration;
using Gcam.Simulation;
using Gcam.Studio.Core.Optics;
using Gcam.Studio.Core.Services;
using Gcam.Studio.Services;

namespace Gcam.EvidenceProbe;

/// <summary>Bounded one-worker timing pilot, or a declared 16-seed acquisition family conditional on fixed banks.
/// Run with readout-field-pilot / readout-field-family; JSON stdout contains no machine paths.</summary>
internal static class ReadoutFieldRecipe
{
    internal static async Task RunAsync(int seed, bool pilot, string samples)
    {
        using var document = JsonDocument.Parse(File.ReadAllText(Path.Combine(samples, "evidence", "readout_field_recipe.json")));
        var recipe = document.RootElement;
        int outerSeeds = recipe.GetProperty("outerSeeds").GetInt32();
        double familyHorizon = recipe.GetProperty("familyHorizonS").GetDouble();
        using var deadline = new CancellationTokenSource(pilot
            ? TimeSpan.FromSeconds(recipe.GetProperty("pilotDeadlineS").GetDouble()) : TimeSpan.FromHours(12));
        var preparations = new ReadoutPreparationService();
        var rows = new List<object>();
        var cold = new List<object>();
        foreach (string name in new[] { "Baseline", "Sharp" })
        {
            var head = OpticsPreset.All.Single(p => p.Name == name).Settings!;
            var detector = new DetectorSettings { ReadoutMode = ReadoutMode.FourOutputAnger };
            var plan = ReadoutPolicy.Plan(head, detector, 661.7, preparations.Offers);
            var watch = Stopwatch.StartNew();
            var ready = await preparations.PrepareAsync(head, detector, plan, cancellationToken: deadline.Token);
            double preparationS = watch.Elapsed.TotalSeconds;
            watch.Restart();
            var repeat = await preparations.PrepareAsync(head, detector, plan, cancellationToken: deadline.Token);
            if (!ReferenceEquals(ready, repeat) || !ready.Succeeded) throw new InvalidOperationException("Preparation identity or calibration failed.");
            cold.Add(new { module = name, preparationS, warmMs = watch.Elapsed.TotalMilliseconds, budget = plan.Budget });
            var bank = preparations.Capture([ready.Id]);
            try
            {
                // Excluded warm-up, including JIT and the opt-in timers.
                var warm = Configuration(head, detector, seed, .1, AmbientGeometry.FrontOnlyThroughMask);
                Measure(warm, bank, .25, deadline.Token);
                int count = pilot ? 1 : outerSeeds;
                for (int index = 0; index < count; index++)
                foreach (var bound in Enum.GetValues<AmbientGeometry>())
                foreach (double dose in pilot ? new[] { 0.0, .1, 1.0 } : new[] { 0.0, .1 })
                {
                    int outerSeed = unchecked(seed + index);
                    var config = Configuration(head, detector, outerSeed, dose, bound);
                    // Independent fixed-history reference is acquisition-family work, never part of the short pilot.
                    object? reference = null;
                    if (!pilot && dose > 0)
                    {
                        var independent = config.Clone(); independent.Seed = unchecked(outerSeed + 900000);
                        var transport = new AmbientPhotonProcess(independent, physicalModule: true);
                        int histories = recipe.GetProperty("independentReferenceHistoriesPerSeed").GetInt32();
                        while (transport.Histories < histories) transport.AdvanceUntil(1e100, deadline.Token);
                        reference = new { histories, scored = transport.Detected,
                            efficiency = transport.Detected / (double)histories };
                    }
                    int repeats = pilot && dose == .1 ? recipe.GetProperty("pilotRepeatsAtNominalDose").GetInt32() : 1;
                    for (int replicate = 0; replicate < repeats; replicate++)
                    {
                        using var cellDeadline = CancellationTokenSource.CreateLinkedTokenSource(deadline.Token);
                        if (pilot) cellDeadline.CancelAfter(TimeSpan.FromSeconds(recipe.GetProperty("pilotCellDeadlineS").GetDouble()));
                        // Recording-only vs full producer separate incident work from readout work.
                        var ambient = new AmbientPhotonProcess(config, recordInteractions: true, physicalModule: true);
                        var incidentWatch = Stopwatch.StartNew();
                        long recorded = 0; double copyS = 0;
                        double horizon = pilot ? recipe.GetProperty("pilotHorizonS").GetDouble() : familyHorizon;
                        while (ambient.TimeS < horizon)
                        {
                            if (ambient.AdvanceUntil(horizon, cellDeadline.Token) is not null)
                            {
                                recorded++;
                                // Independent copy timing estimates envelope bookkeeping cost at this site population.
                                long before = Stopwatch.GetTimestamp();
                                var envelope = ambient.LastRecordedEvent!;
                                _ = new RecordedTransportEvent(envelope.Event, envelope.Sites, envelope.Origin);
                                copyS += Stopwatch.GetElapsedTime(before).TotalSeconds;
                            }
                        }
                        double incidentS = incidentWatch.Elapsed.TotalSeconds;
                        var measured = Measure(config, bank, horizon, cellDeadline.Token);
                        rows.Add(new { module = name, seed = outerSeed, bound = bound.ToString(), dose, replicate,
                            horizon, incidentRateCps = ambient.IncidentRateCps, incident = ambient.Histories,
                            scoredAmbient = recorded, incidentS, copyS, measured, reference });
                    }
                }
                // Pre-cancelled low-acceptance advance proves no response/noise consumption; an actual low-rate
                // transport loop separately measures cancellation latency between bounded attempts.
                var low = Configuration(head, detector, seed, .000001, AmbientGeometry.FrontOnlyThroughMask);
                low.Detector.Readout = bank.Reference.Policy;
                low.Source.ActivityBq = 1e-9; foreach (var source in low.Sources!) source.ActivityBq = 1e-9;
                using var cancellation = new CancellationTokenSource(TimeSpan.FromMilliseconds(20));
                var lowWatch = Stopwatch.StartNew();
                bool cancelled = false;
                using var producer = new PhysicalListModeSource(low, bank.Reference.Device,
                    bank.Entries.Select(e => e.Calibration).ToArray(), seed);
                try { while (true) producer.AdvanceUntil(1e6, cancellation.Token); }
                catch (OperationCanceledException) { cancelled = true; }
                rows.Add(new { module = name, cancellation = new { cancelled,
                    wallMs = lowWatch.Elapsed.TotalMilliseconds, producer.ObservedTimeS, producer.HitsObserved } });
                if (pilot)
                {
                    var sessionWatch = Stopwatch.StartNew();
                    var captured = detector with { PreparedReadoutLines = new([ready.Id]) };
                    await using var session = new SimulationService(readoutPreparation: preparations).StartAmbient(
                        [new() { ActivityUCi = 500 }], head, 1, 1000,
                        AmbientPreset.TerrestrialUnscear2000V1.Field(.1, AmbientGeometry.FrontOnlyThroughMask),
                        captured, seed: seed);
                    int publications = 0; double decodeS = 0; AcquisitionSnapshot? last = null;
                    await foreach (var snapshot in session.ReadSnapshotsAsync(deadline.Token))
                    { publications++; decodeS += snapshot.DecodeTime.TotalSeconds; last = snapshot; }
                    rows.Add(new { module = name, services = new { wallS = sessionWatch.Elapsed.TotalSeconds,
                        publications, decodeS, observedS = last!.LiveTimeS, counts = last.Counts } });
                }
            }
            finally { preparations.Release(bank); }
            if (!pilot && name == "Baseline")
            {
                var co = await preparations.PrepareAsync(head, detector,
                    ReadoutPolicy.Plan(head, detector, 1332.5, preparations.Offers), cancellationToken: deadline.Token);
                var twoLine = preparations.Capture([ready.Id, co.Id]);
                try
                {
                    for (int index = 0; index < outerSeeds; index++)
                    foreach (var bound in Enum.GetValues<AmbientGeometry>())
                    {
                        int outerSeed = unchecked(seed + index);
                        var config = SimulationService.BuildConfig([new() { ActivityUCi = 500 },
                            new() { Isotope = "Co-60", X = 20, ActivityUCi = 300 }], head, detector,
                            ambient: AmbientPreset.TerrestrialUnscear2000V1.Field(.1, bound));
                        config.Seed = outerSeed;
                        rows.Add(new { module = name, seed = outerSeed, bound = bound.ToString(), twoLine = true,
                            measured = Measure(config, twoLine, familyHorizon, deadline.Token) });
                    }
                }
                finally { preparations.Release(twoLine); }
            }
        }
        Console.WriteLine(JsonSerializer.Serialize(new { version = 1, phase = pilot ? "timing-pilot" : "acquisition-family",
            outerSeeds = pilot ? 1 : outerSeeds, seed,
            familyErrorBudget = recipe.GetProperty("familyErrorBudget").GetDouble(), recipe, cold, rows,
            peakWorkingBytes = Process.GetCurrentProcess().PeakWorkingSet64,
            uncertainty = "Incident Poisson mean = rate*T; fixed-M reference binomial variance p(1-p)/M. Scored comparison variance = rate*T*p + (rate*T)^2*p(1-p)/M. Assignment/window intervals use outer-seed spread. Exact identities use zero tolerance; no source fixed-rate Poisson oracle." }));
    }

    private static SimulationConfig Configuration(OpticsSettings head, DetectorSettings detector, int seed,
        double dose, AmbientGeometry bound)
    {
        var config = SimulationService.BuildConfig([new SceneSource { ActivityUCi = 500 }], head, detector,
            ambient: AmbientPreset.TerrestrialUnscear2000V1.Field(dose, bound));
        config.Seed = seed;
        return config;
    }

    private static object Measure(SimulationConfig config, ReadoutBank bank, double horizon, CancellationToken token)
    {
        config.Detector.Readout = bank.Reference.Policy;
        double transportS = 0, responseS = 0, pulseS = 0, projectionS = 0;
        long sourceInputs = 0, ambientInputs = 0, conversions = 0, assigned = 0, unknown = 0, window = 0;
        var assignedByLine = new long[bank.Entries.Count];
        var unknownByLine = new long[bank.Entries.Count];
        var windowByLine = new long[bank.Entries.Count];
        using var producer = new PhysicalListModeSource(config, bank.Reference.Device,
            bank.Entries.Select(e => e.Calibration).ToArray(), config.Seed ?? 0, t =>
            {
                transportS += t.Transport.TotalSeconds; responseS += t.Response.TotalSeconds;
                pulseS += t.Pulse.TotalSeconds; projectionS += t.Projection.TotalSeconds;
            });
        var watch = Stopwatch.StartNew();
        foreach (double target in new[] { .25, 1.0, horizon }.Distinct())
            while (producer.ObservedTimeS < target)
            {
                foreach (var record in producer.AdvanceUntil(target, token))
                {
                    conversions++; if (record.Crystal < 0) unknown++;
                    else { assigned++; if (record.EnergyKeV >= 600 && record.EnergyKeV <= 720) window++; }
                    for (int line = 0; line < record.Assignments.Count; line++)
                    {
                        var a = record.Assignments[line];
                        if (a.Crystal < 0) unknownByLine[line]++;
                        else
                        {
                            assignedByLine[line]++;
                            var edges = ReadoutPolicy.BenchmarkWindow(bank.Entries[line].View.LineKeV, .15);
                            if (a.EnergyKeV >= edges.Low && a.EnergyKeV <= edges.High) windowByLine[line]++;
                        }
                    }
                }
                if (producer.LastInputOrigin == TransportOrigin.Signal) sourceInputs++;
                if (producer.LastInputOrigin == TransportOrigin.Ambient) ambientInputs++;
            }
        if (conversions != assigned + unknown || producer.HitsObserved != sourceInputs + ambientInputs
            || producer.TruthSamples.Count > 256 || assignedByLine.Where((a, i) => a + unknownByLine[i] != conversions).Any())
            throw new InvalidOperationException("Count conservation failed.");
        return new { wallS = watch.Elapsed.TotalSeconds, transportS, responseS, pulseS, projectionS,
            sourceInputs, ambientInputs, conversions, assigned, unknown, window, producer.HitsObserved,
            assignedByLine, unknownByLine, windowByLine,
            producer.ObservedTimeS, producer.HasPendingHold, truthHistories = producer.TruthSamples.Count,
            producer.SourceRateCps, producer.AmbientRateCps };
    }
}
