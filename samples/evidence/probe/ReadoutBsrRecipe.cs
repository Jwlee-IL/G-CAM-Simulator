using System.Diagnostics;
using System.Text.Json;
using Gcam.Configuration;
using Gcam.Simulation;
using Gcam.Studio.Core.Optics;
using Gcam.Studio.Core.Services;
using Gcam.Studio.Services;

namespace Gcam.EvidenceProbe;

/// <summary>Stage-B cells, separate from the retained v1 family; one worker, fixed source-line banks.</summary>
internal static class ReadoutBsrRecipe
{
    internal static async Task RunAsync(int seed, bool pilot, string samples)
    {
        using var document = JsonDocument.Parse(File.ReadAllText(Path.Combine(samples, "evidence", "readout_bsr_recipe.json")));
        var recipe = document.RootElement;
        using var deadline = new CancellationTokenSource(pilot
            ? TimeSpan.FromSeconds(recipe.GetProperty("pilotDeadlineS").GetDouble()) : TimeSpan.FromHours(12));
        var preparations = new ReadoutPreparationService();
        var cold = new List<object>(); var rows = new List<object>();
        int seeds = pilot ? 1 : recipe.GetProperty("outerSeeds").GetInt32();
        double horizon = recipe.GetProperty(pilot ? "pilotHorizonS" : "familyHorizonS").GetDouble();
        foreach (string name in new[] { "Baseline", "Sharp" })
        {
            var head = OpticsPreset.All.Single(p => p.Name == name).Settings!;
            var detector = new DetectorSettings { ReadoutMode = ReadoutMode.FourOutputAnger };
            var watch = Stopwatch.StartNew();
            var ready = await preparations.PrepareAsync(head, detector,
                ReadoutPolicy.Plan(head, detector, 661.7, preparations.Offers), cancellationToken: deadline.Token);
            if (!ready.Succeeded) throw new InvalidOperationException("Source-line calibration failed.");
            cold.Add(new { module = name, wallS = watch.Elapsed.TotalSeconds });
            var bank = preparations.Capture([ready.Id]);
            try
            {
                for (int index = 0; index < seeds; index++)
                foreach (double bsr in recipe.GetProperty("bsr").EnumerateArray().Select(e => e.GetDouble()))
                foreach (var bound in new AmbientGeometry?[] { null, AmbientGeometry.FrontOnlyThroughMask, AmbientGeometry.BareCrystalAllFaces })
                {
                    int outerSeed = unchecked(seed + index);
                    var config = SimulationService.BuildConfig([new() { ActivityUCi = 500 }], head, detector, bsr,
                        ambient: bound is { } geometry ? AmbientPreset.TerrestrialUnscear2000V1.Field(
                            recipe.GetProperty("ambientDoseMicroSvPerHour").GetDouble(), geometry) : null);
                    config.Seed = outerSeed;
                    using var cell = CancellationTokenSource.CreateLinkedTokenSource(deadline.Token);
                    if (pilot) cell.CancelAfter(TimeSpan.FromSeconds(recipe.GetProperty("pilotCellDeadlineS").GetDouble()));
                    // Conditional retries change transport histories, never the pre-trigger schedule.
                    var schedule = config.Clone(); schedule.Ambient = null; schedule.Detector.Readout = null;
                    using var legacy = new ListModeSource(schedule);
                    using var paired = new ListModeSource(schedule, true, true);
                    var expected = new List<double>(); var actual = new List<double>();
                    while (legacy.ArrivalTimeS < horizon)
                        if (legacy.AdvanceUntil(horizon, cell.Token) is { } e) expected.Add(e.ArrivalTimeS);
                    while (paired.ArrivalTimeS < horizon)
                        if (paired.AdvanceUntil(horizon, cell.Token) is { } e) actual.Add(e.ArrivalTimeS);
                    if (!expected.SequenceEqual(actual)) throw new InvalidOperationException("Pre-trigger schedule identity failed.");
                    long incident = 0, scored = 0; double incidentRate = 0, ambientTransportS = 0;
                    object? reference = null;
                    if (config.Ambient is not null)
                    {
                        watch.Restart();
                        var transport = new AmbientPhotonProcess(config, true, true);
                        while (transport.TimeS < horizon) transport.AdvanceUntil(horizon, cell.Token);
                        ambientTransportS = watch.Elapsed.TotalSeconds;
                        incident = transport.Histories; scored = transport.Detected; incidentRate = transport.IncidentRateCps;
                        if (!pilot)
                        {
                            var independent = config.Clone(); independent.Seed = unchecked(outerSeed + 900000);
                            var other = new AmbientPhotonProcess(independent, physicalModule: true);
                            int histories = recipe.GetProperty("independentReferenceHistoriesPerSeed").GetInt32();
                            while (other.Histories < histories) other.AdvanceUntil(1e100, cell.Token);
                            reference = new { histories, scored = other.Detected };
                        }
                    }
                    config.Detector.Readout = bank.Reference.Policy;
                    var measured = Measure(config, bank, horizon, cell.Token);
                    if (measured.ambientInputs != scored) throw new InvalidOperationException("Ambient envelope/input count mismatch.");
                    if (measured.sourceInputs + measured.backgroundInputs != expected.Count)
                        throw new InvalidOperationException("Merged source/BSR pre-trigger count mismatch.");
                    rows.Add(new { module = name, seed = outerSeed, bsr, bound = bound?.ToString(), horizon,
                        incident, scoredAmbient = scored, incidentRateCps = incidentRate, ambientTransportS, reference,
                        scheduleInputs = expected.Count, scheduleIdentity = true, measured });
                }
                // A 1 keV conditional BSR search is effectively absorbed in the entrance proxy.
                var low = SimulationService.BuildConfig([new() { ActivityUCi = 500 }], head, detector, 1);
                low.Seed = seed; low.Background!.EnergyKeV = 1; low.Detector.Readout = bank.Reference.Policy;
                using var producer = new PhysicalListModeSource(low, bank.Reference.Device, bank.Reference.Calibration, seed);
                using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(deadline.Token);
                cancellation.CancelAfter(TimeSpan.FromMilliseconds(20)); watch.Restart();
                bool cancelled = false;
                try { while (true) producer.AdvanceUntil(1e6, cancellation.Token); }
                catch (OperationCanceledException) { cancelled = true; }
                rows.Add(new { module = name, cancellation = new { cancelled, wallMs = watch.Elapsed.TotalMilliseconds,
                    producer.BackgroundAttempts, producer.HitsObserved, producer.ObservedTimeS } });
            }
            finally { preparations.Release(bank); }
        }
        Console.WriteLine(JsonSerializer.Serialize(new { version = 2, family = "readout-field-v2",
            phase = pilot ? "timing-pilot" : "acquisition-family", outerSeeds = seeds, seed, recipe, cold, rows,
            peakWorkingBytes = Process.GetCurrentProcess().PeakWorkingSet64 }));
    }

    private static BsrFacts Measure(SimulationConfig config, ReadoutBank bank, double horizon, CancellationToken token)
    {
        double transport = 0, response = 0, pulse = 0, projection = 0;
        long source = 0, background = 0, ambient = 0, assigned = 0, unknown = 0, window = 0;
        var facts = new List<(double Trigger, double Hold, double[] Codes, int Crystal, double Energy)>();
        using var producer = new PhysicalListModeSource(config, bank.Reference.Device,
            bank.Entries.Select(e => e.Calibration).ToArray(), config.Seed!.Value, t =>
            { transport += t.Transport.TotalSeconds; response += t.Response.TotalSeconds;
                pulse += t.Pulse.TotalSeconds; projection += t.Projection.TotalSeconds; });
        var watch = Stopwatch.StartNew();
        foreach (double target in new[] { .25, 1.0, horizon }.Distinct())
            while (producer.ObservedTimeS < target)
            {
                foreach (var e in producer.AdvanceUntil(target, token))
                {
                    facts.Add((e.Conversion.TriggerTimeNs, e.Conversion.HoldTimeNs, e.Conversion.Codes.ToArray(), e.Crystal, e.EnergyKeV));
                    if (e.Crystal < 0) unknown++;
                    else { assigned++; if (e.EnergyKeV >= 600 && e.EnergyKeV <= 720) window++; }
                }
                if (producer.LastInputOrigin == TransportOrigin.Signal) source++;
                if (producer.LastInputOrigin == TransportOrigin.Background) background++;
                if (producer.LastInputOrigin == TransportOrigin.Ambient) ambient++;
            }
        double wall = watch.Elapsed.TotalSeconds;
        if (producer.HitsObserved != source + background + ambient || facts.Count != assigned + unknown
            || window != facts.Count(e => e.Crystal >= 0 && e.Energy >= 600 && e.Energy <= 720)
            || producer.TruthSamples.Count > 256) throw new InvalidOperationException("Measured count identity failed.");
        // Independently partitioned pulse horizon: compare all held facts, not merely counts.
        using var whole = new PhysicalListModeSource(config, bank.Reference.Device, bank.Reference.Calibration, config.Seed.Value);
        int position = 0;
        while (whole.ObservedTimeS < horizon)
            foreach (var e in whole.AdvanceUntil(horizon, token))
            {
                var expected = facts[position++];
                if (expected.Trigger != e.Conversion.TriggerTimeNs || expected.Hold != e.Conversion.HoldTimeNs
                    || !expected.Codes.SequenceEqual(e.Conversion.Codes) || expected.Crystal != e.Crystal || !expected.Energy.Equals(e.EnergyKeV))
                    throw new InvalidOperationException("Partitioned held facts differ.");
            }
        if (position != facts.Count) throw new InvalidOperationException("Partitioned conversion count differs.");
        return new(wall, transport, response, pulse, projection, source, background, ambient, facts.Count, assigned, unknown, window,
            producer.BackgroundAttempts, producer.HitsObserved, producer.ObservedTimeS, producer.HasPendingHold, producer.TruthSamples.Count);
    }

    private sealed record BsrFacts(double wallS, double transportS, double responseS, double pulseS, double projectionS,
        long sourceInputs, long backgroundInputs, long ambientInputs, long conversions, long assigned, long unknown, long window,
        long backgroundAttempts, long hitsObserved, double observedTimeS, bool hasPendingHold, int truthHistories);
}
