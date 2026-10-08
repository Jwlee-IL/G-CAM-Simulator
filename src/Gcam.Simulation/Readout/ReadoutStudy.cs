using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Nodes;
using Gcam.Configuration;
using Gcam.Core;
using Gcam.Detector;

namespace Gcam.Simulation;

/// <summary>
/// The TODO-19 readout comparison (RD-7): DirectCrystal against FourOutputAnger / IndependentSipm readouts on the same
/// transported histories, per geometry, trigger and line. Per geometry: one optical table (shared by its readouts), one
/// calibration flood, one validation flood per line (uniform, normal incidence) and one localisation run per line through
/// the scenario's mask and decoder. Per readout: Kirchhoff network checks and edge compression. Per readout × trigger:
/// the flood-map calibration (explicit failure), and per line —
/// <list type="bullet">
/// <item>flood: trigger acceptance overall and by position (per crystal), crystal mis-identification of single-crystal
/// histories (photopeak window and all triggered), disagreement with the direct arg-max and with the noiseless light
/// centroid, flood peak-to-valley, energy resolution of full-energy histories;</item>
/// <item>localisation: decoder error at equal emitted fluence and at equal accepted counts (each readout's photopeak list
/// and the direct reference's, truncated to the smaller count);</item>
/// <item>on the line nearest 662 keV, a count-rate axis: the same hits re-timed as Poisson streams and processed in the
/// time domain (pulses sum in all channels) — throughput, piled fraction, mispositioning of piled events against the
/// hit's isolated crystal, and the legacy energy-only resolving-time merge for comparison;</item>
/// <item>cost per event of the readout chain (wall clock, µs).</item>
/// </list>
/// Every random stream is keyed by (outer seed, geometry, readout, trigger, purpose, line) via
/// <see cref="GateResponse.StreamSeed"/>, so trigger settings of one readout see identical light fluctuations and all
/// readouts of a geometry see identical transport. Numbers that are undefined (no events) are written as null.
/// </summary>
public static class ReadoutStudy
{
    private const int Shared = 15;
    private const double NsPerAdcSample = 8.0;          // AD9648 at 125 MSPS, the legacy resolving-time unit

    private enum Kind
    {
        CalTransport = 1, FloodTransport, LocTransport, Optics, CalResponse, CalNoise, FloodResponse, FloodNoise,
        LocResponse, LocNoise, RateTimes, RateNoise,
    }

    private static int Stream(int seed, int g, int v, int t, Kind kind, int e)
        => GateResponse.StreamSeed(seed, (uint)((((g * 16 + v) * 16 + t) * 64 + (int)kind) * 8 + e));

    private static DefaultRandom Rng(int seed, int g, int v, int t, Kind kind, int e)
        => new(Stream(seed, g, v, t, kind, e));

    public static void Validate(ReadoutStudyRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.Geometries.Length is < 1 or > 16 || request.Readouts.Length is < 1 or > 15
            || request.Triggers.Length is < 1 or > 15 || request.EnergiesKeV.Length is < 1 or > 8
            || request.RatesCps.Length > 8 || request.FloodHistoriesPerCrystal < 1 || request.CalibrationHistoriesPerCrystal < 1
            || request.LocalisationHistories < 1 || request.RateHits < 0 || request.WindowFraction is <= 0 or >= 1
            || request.EnergiesKeV.Any(e => !(e > 0)) || request.RatesCps.Any(r => !(r > 0)))
            throw new ArgumentException("Readout study: 1–16 geometries, 1–15 readouts and triggers, 1–8 lines, ≤ 8 rates, positive budgets.");
        if (request.Readouts.Any(v => v.Mode == ReadoutMode.DirectCrystal))
            throw new ArgumentException("DirectCrystal is the study's built-in reference, not a variant.");
    }

    public static JsonObject Run(SimulationConfig scenario, ReadoutStudyRequest request, int seed)
    {
        ArgumentNullException.ThrowIfNull(scenario);
        Validate(request);
        var total = Stopwatch.StartNew();
        var geometries = new JsonArray();
        for (int g = 0; g < request.Geometries.Length; g++) geometries.Add(RunGeometry(scenario, request, seed, g));
        return new JsonObject
        {
            ["Seed"] = seed,
            ["Scenario"] = scenario.Name,
            ["SourcePositionMm"] = new JsonArray(scenario.Source.Position[0], scenario.Source.Position[1]),
            ["WindowFraction"] = request.WindowFraction,
            ["Geometries"] = geometries,
            ["Seconds"] = total.Elapsed.TotalSeconds,
        };
    }

    private sealed record Localised(int[] Direct, double[] Deposit, InteractionSite[][] Sites);

    private static JsonObject RunGeometry(SimulationConfig scenario, ReadoutStudyRequest request, int seed, int gi)
    {
        var geometry = request.Geometries[gi];
        var cfg = scenario.Clone();
        cfg.Detector.PixelPitchMm = geometry.PitchMm;
        cfg.Detector.ReflectorGapMm = geometry.GapMm;
        cfg.Detector.Readout = null;
        var crystals = CrystalArrayGeometry.From(cfg.Detector);
        int nx = crystals.CountX, n = crystals.Count;
        var baseReadout = Copy(request.Base);
        if (geometry.Optics is not null) baseReadout.Optics = Copy(geometry.Optics);
        var lines = request.EnergiesKeV;
        double w = request.WindowFraction;
        var src = (X: scenario.Source.Position[0], Y: scenario.Source.Position[1]);
        int rateLine = Array.IndexOf(lines, lines.MinBy(e => Math.Abs(e - 661.7)));

        var watch = Stopwatch.StartNew();
        // The optical table depends on geometry and optics only, not on the readout mode.
        var optics = ReadoutDevice.BuildOptics(crystals, WithMode(baseReadout, ReadoutMode.FourOutputAnger), Stream(seed, gi, Shared, Shared, Kind.Optics, 0));
        double opticsSeconds = watch.Elapsed.TotalSeconds;
        watch.Restart();
        var calFlood = ReadoutFlood.Transport(cfg, baseReadout.Calibration.EnergyKeV, request.CalibrationHistoriesPerCrystal * n,
            Stream(seed, gi, Shared, Shared, Kind.CalTransport, 0));
        var floods = lines.Select((e, ei) => ReadoutFlood.Transport(cfg, e, request.FloodHistoriesPerCrystal * n,
            Stream(seed, gi, Shared, Shared, Kind.FloodTransport, ei))).ToArray();
        var localised = lines.Select((e, ei) => Localise(cfg, e, request.LocalisationHistories,
            Stream(seed, gi, Shared, Shared, Kind.LocTransport, ei))).ToArray();
        double transportSeconds = watch.Elapsed.TotalSeconds;

        // Direct reference: arg-max crystal, window on the true total deposit (the engine's ideal energy).
        var photopeakLists = new List<(JsonObject Node, int Line, List<int> Crystals)>();
        var direct = new JsonArray();
        for (int ei = 0; ei < lines.Length; ei++)
        {
            var inWindow = new List<int>();
            for (int i = 0; i < localised[ei].Direct.Length; i++)
                if (Math.Abs(localised[ei].Deposit[i] - lines[ei]) <= w * lines[ei]) inWindow.Add(localised[ei].Direct[i]);
            var node = new JsonObject
            {
                ["EnergyKeV"] = lines[ei],
                ["Events"] = localised[ei].Direct.Length,
                ["InWindow"] = inWindow.Count,
                ["ErrorMm"] = Num(Decode(cfg, inWindow, inWindow.Count, src)),
            };
            photopeakLists.Add((node, ei, inWindow));
            direct.Add(node);
        }

        var readouts = new JsonArray();
        for (int vi = 0; vi < request.Readouts.Length; vi++)
        {
            var variant = request.Readouts[vi];
            var ro = Copy(baseReadout);
            ro.Mode = variant.Mode;
            if (variant.Network is not null) ro.Network = Copy(variant.Network);
            if (variant.Segmentation is { } seg) ro.Calibration.Segmentation = seg;
            if (variant.ZeroSuppressionSigma is { } zs) ro.Digitizer.ZeroSuppressionSigma = zs;
            var device = new ReadoutDevice(optics, ro);
            var triggers = new JsonArray();
            for (int ti = 0; ti < request.Triggers.Length; ti++)
            {
                var trigger = request.Triggers[ti];
                var node = new JsonObject { ["Trigger"] = trigger.Name };
                triggers.Add(node);
                if (trigger.Trigger.Logic == TriggerLogic.And && device.Mode != ReadoutMode.FourOutputAnger)
                {
                    node["NotApplicable"] = "AND is defined for four outputs";
                    continue;
                }
                var processor = new ReadoutPulseProcessor(device, ro.Pulse, trigger.Trigger);
                watch.Restart();
                var cal = ReadoutCalibration.Build(device, processor, calFlood, ro.Calibration,
                    Rng(seed, gi, vi, Shared, Kind.CalResponse, 0), Rng(seed, gi, vi, ti, Kind.CalNoise, 0));
                node["Calibration"] = new JsonObject
                {
                    ["Succeeded"] = cal.Succeeded,
                    ["Failure"] = cal.Failure,
                    ["PeaksFound"] = cal.Lut.PeaksFound,
                    ["PeaksExpected"] = cal.Lut.PeaksExpected,
                    ["TriggeredFraction"] = Num((double)cal.Triggered / cal.Scored),
                    ["InWindowFraction"] = Num((double)cal.InWindow / cal.Scored),
                    ["CrystalsOnGlobalGain"] = cal.CrystalsOnGlobalGain,
                    ["Seconds"] = watch.Elapsed.TotalSeconds,
                };
                if (!cal.Succeeded) continue;
                var energies = new JsonArray();
                for (int ei = 0; ei < lines.Length; ei++)
                {
                    var flood = Flood(device, processor, cal, floods[ei], lines[ei], w,
                        Rng(seed, gi, vi, Shared, Kind.FloodResponse, ei), Rng(seed, gi, vi, ti, Kind.FloodNoise, ei));
                    var (loc, inWindow, hits, isolated) = Localisation(cfg, device, processor, cal, localised[ei], lines[ei], w,
                        Rng(seed, gi, vi, Shared, Kind.LocResponse, ei), Rng(seed, gi, vi, ti, Kind.LocNoise, ei), src,
                        ei == rateLine ? request.RateHits : 0);
                    photopeakLists.Add((loc, ei, inWindow));
                    var entry = new JsonObject { ["EnergyKeV"] = lines[ei], ["Flood"] = flood, ["Localisation"] = loc };
                    if (ei == rateLine && hits.Count > 0)
                        entry["Rate"] = Rates(device, processor, cal, hits, isolated, lines[ei], w, request.RatesCps,
                            r => Rng(seed, gi, Shared, Shared, Kind.RateTimes, r), r => Rng(seed, gi, vi, ti, Kind.RateNoise, r));
                    energies.Add(entry);
                }
                node["Energies"] = energies;
            }
            readouts.Add(new JsonObject
            {
                ["Name"] = variant.Name,
                ["Mode"] = variant.Mode.ToString(),
                ["Topology"] = variant.Mode == ReadoutMode.FourOutputAnger ? ro.Network.Topology.ToString() : null,
                ["Channels"] = device.Channels,
                ["Network"] = NetworkSummary(device),
                ["Triggers"] = triggers,
            });
        }

        // Equal accepted counts, pairwise against the direct reference: each readout's photopeak list and the direct list
        // truncated to the smaller of the two counts (first events in arrival order).
        foreach (var (node, ei, list) in photopeakLists.Skip(lines.Length))
        {
            var directList = photopeakLists[ei].Crystals;
            int equal = Math.Min(list.Count, directList.Count);
            node["EqualCount"] = equal;
            node["EqualCountErrorMm"] = Num(Decode(cfg, list, equal, src));
            node["DirectEqualCountErrorMm"] = Num(Decode(cfg, directList, equal, src));
        }

        int centre = crystals.CountY / 2 * nx + nx / 2;
        var peakDevice = new ReadoutDevice(optics, WithMode(baseReadout, ReadoutMode.IndependentSipm));
        var pe = new double[optics.Sensors.Count];
        peakDevice.ExpectedPhotoelectrons([new InteractionSite(crystals.CenterX(nx / 2), crystals.CenterY(crystals.CountY / 2),
            crystals.PlaneZ - crystals.DepthMm + 1e-6, 0, 661.7, nx / 2, crystals.CountY / 2)], pe);
        return new JsonObject
        {
            ["Name"] = geometry.Name,
            ["PitchMm"] = geometry.PitchMm,
            ["GapMm"] = geometry.GapMm,
            ["ActiveWidthMm"] = crystals.ActiveWidthMm,
            ["Optics"] = new JsonObject
            {
                ["Surface"] = baseReadout.Optics.Surface.ToString(),
                ["WallReflectance"] = baseReadout.Optics.WallReflectance,
                ["WallTransmittance"] = baseReadout.Optics.WallTransmittance,
                ["MeanCollection"] = optics.MeanCollection,
                ["CentreCollectionByDepth"] = new JsonArray(Enumerable.Range(0, optics.DepthBins)
                    .Select(b => (JsonNode?)JsonValue.Create(optics.Collection(centre, b))).ToArray()),
                ["Fates"] = new JsonObject(optics.FateFractions.Select(f =>
                    new KeyValuePair<string, JsonNode?>(f.Key.ToString(), JsonValue.Create(f.Value)))),
                ["PePerKeV"] = peakDevice.PePerKeVReference,
                ["PeakSensorPeAt662ExitFace"] = pe.Max(),
                ["PeakPePerMicrocell50um"] = pe.Max() / (400.0 * optics.Sensors.ActiveAreaMm2),
                ["TracedClasses"] = optics.TracedClasses,
                ["Seconds"] = opticsSeconds,
            },
            ["TransportSeconds"] = transportSeconds,
            ["Direct"] = direct,
            ["Readouts"] = readouts,
        };
    }

    private static ReadoutConfig WithMode(ReadoutConfig source, ReadoutMode mode)
    {
        var copy = Copy(source);
        copy.Mode = mode;
        return copy;
    }

    private static T Copy<T>(T value) => JsonSerializer.Deserialize<T>(JsonSerializer.Serialize(value))!;

    private static double? Num(double? v) => v is { } x && double.IsFinite(x) ? x : null;

    private static Localised Localise(SimulationConfig cfg, double energy, long histories, int seed)
    {
        var c = cfg.Clone();
        c.Seed = seed;
        c.Sources = null;
        c.Background = null;
        c.Ambient = null;
        c.Source.Isotope = "line";
        c.Source.EnergyKeV = energy;
        c.Source.Lines = null;
        c.Source.BranchingRatio = 1.0;
        using var source = new ListModeSource(c, recordInteractions: true);
        var directs = new List<int>();
        var deposits = new List<double>();
        var sites = new List<InteractionSite[]>();
        while (source.HistoriesEmitted < histories)
        {
            if (source.Advance() is not { } e) continue;
            directs.Add(e.PixelY * c.Detector.PixelsX + e.PixelX);
            deposits.Add(e.DepositKeV);
            sites.Add(source.LastInteractions.ToArray());
        }
        return new Localised(directs.ToArray(), deposits.ToArray(), sites.ToArray());
    }

    private static double? Decode(SimulationConfig cfg, IReadOnlyList<int> crystals, int count, (double X, double Y) source)
    {
        if (count <= 0) return null;
        int nx = cfg.Detector.PixelsX;
        var image = new DetectorImage(nx, cfg.Detector.PixelsY);
        for (int i = 0; i < count; i++) image.Add(crystals[i] % nx, crystals[i] / nx, 1.0);
        var est = new DefaultSimulationFactory().CreateDecoder(cfg)!.Decode(image).Estimate;
        double dx = est.Position.X - source.X, dy = est.Position.Y - source.Y;
        return Math.Sqrt(dx * dx + dy * dy);
    }

    private static JsonObject NetworkSummary(ReadoutDevice device)
    {
        var node = new JsonObject
        {
            ["MaxChargeImbalance"] = device.Network.MaxChargeImbalance,
            ["MaxKirchhoffResidual"] = device.Network.MaxKirchhoffResidual,
        };
        var cr = device.Crystals;
        if (cr.CountX < 4) return node;
        int row = cr.CountY / 2, m = cr.CountX / 2;
        var ch = new double[device.Channels];
        double X(int ix)
        {
            device.Expected([new InteractionSite(cr.CenterX(ix), cr.CenterY(row), cr.PlaneZ - cr.DepthMm / 2, 0, 661.7, ix, row)], ch);
            return device.Position(ch, out double x, out _) ? x : double.NaN;
        }
        node["EdgeToCentreSpacing"] = Num((X(1) - X(0)) / (X(m) - X(m - 1)));
        return node;
    }

    private static JsonObject Flood(ReadoutDevice device, ReadoutPulseProcessor processor, ReadoutCalibration cal,
        List<InteractionSite[]> flood, double line, double w, IRandom response, IRandom noise)
    {
        int nx = device.Crystals.CountX, n = device.Crystals.Count;
        var scored = new int[n];
        var triggered = new int[n];
        int invalid = 0, unassigned = 0, singleAll = 0, singleAllMis = 0, singlePk = 0, singlePkMis = 0;
        int photopeak = 0, disagreeDirect = 0, disagreeLight = 0, trig = 0;
        var ch = new double[device.Channels];
        var expected = new double[device.Channels];
        var points = new List<(double, double)>();
        var full = new List<double>();
        foreach (var sites in flood)
        {
            int direct = ReadoutDevice.DirectCrystal(sites, nx);
            scored[direct]++;
            double sum = device.Respond(sites, response, ch);
            var ev = processor.ProcessIsolated(new ReadoutHit(0, ch, sum), noise);
            if (ev is null) continue;
            trig++;
            triggered[direct]++;
            if (!device.Position(ev.Codes, out double x, out double y)) { invalid++; continue; }
            int c = cal.Lut.Lookup(x, y);
            if (c < 0) { unassigned++; continue; }
            double energy = cal.Energy(c, device.Sum(ev.Codes));
            bool inWindow = Math.Abs(energy - line) <= w * line;
            bool single = sites.All(s => s.CrystalX == sites[0].CrystalX && s.CrystalY == sites[0].CrystalY);
            int truth = sites[0].CrystalY * nx + sites[0].CrystalX;
            if (single)
            {
                singleAll++;
                if (c != truth) singleAllMis++;
                if (inWindow) { singlePk++; if (c != truth) singlePkMis++; }
            }
            if (inWindow)
            {
                photopeak++;
                points.Add((x, y));
                if (c != direct) disagreeDirect++;
                device.Expected(sites, expected);
                int light = device.Position(expected, out double lx, out double ly) ? cal.Lut.Lookup(lx, ly) : -1;
                if (c != light) disagreeLight++;
            }
            if (Math.Abs(sites.Sum(s => s.EnergyKeV) - line) < 0.01) full.Add(energy);
        }
        var acceptance = Enumerable.Range(0, n).Where(c => scored[c] > 0).Select(c => (double)triggered[c] / scored[c]).ToArray();
        int ny = device.Crystals.CountY;
        bool Corner(int c) => (c % nx == 0 || c % nx == nx - 1) && (c / nx == 0 || c / nx == ny - 1);
        bool Edge(int c) => !Corner(c) && (c % nx == 0 || c % nx == nx - 1 || c / nx == 0 || c / nx == ny - 1);
        bool Centre(int c) => Math.Abs(c % nx - (nx - 1) / 2.0) < 1 && Math.Abs(c / nx - (ny - 1) / 2.0) < 1;
        double Group(Func<int, bool> f)
        {
            var sel = Enumerable.Range(0, n).Where(c => f(c) && scored[c] > 0).ToArray();
            return sel.Length == 0 ? double.NaN : (double)sel.Sum(c => triggered[c]) / sel.Sum(c => scored[c]);
        }
        var density = FloodLut.Smooth(FloodLut.Histogram(points, cal.Lut.BinsX, cal.Lut.BinsY), cal.Lut.BinsX, cal.Lut.BinsY,
            cal.Lut.SmoothingSigmaBins);
        var pv = cal.Lut.PeakToValley(density);
        var (mean, sd, fwhmRobust) = Resolution(full);
        return new JsonObject
        {
            ["Scored"] = flood.Count,
            ["TriggeredFraction"] = Num((double)trig / flood.Count),
            ["Acceptance"] = new JsonObject
            {
                ["Min"] = Num(acceptance.DefaultIfEmpty(double.NaN).Min()),
                ["Median"] = Num(Median(acceptance)),
                ["Max"] = Num(acceptance.DefaultIfEmpty(double.NaN).Max()),
                ["Centre"] = Num(Group(Centre)),
                ["Edge"] = Num(Group(Edge)),
                ["Corner"] = Num(Group(Corner)),
            },
            ["InvalidFraction"] = Num((double)invalid / flood.Count),
            ["UnassignedFraction"] = Num((double)unassigned / flood.Count),
            ["PhotopeakFraction"] = Num((double)photopeak / flood.Count),
            ["MisIdSinglePhotopeak"] = Fraction(singlePkMis, singlePk),
            ["MisIdSingleAll"] = Fraction(singleAllMis, singleAll),
            ["DisagreeDirectPhotopeak"] = Fraction(disagreeDirect, photopeak),
            ["DisagreeLightCentroidPhotopeak"] = Fraction(disagreeLight, photopeak),
            ["PeakToValley"] = new JsonObject
            {
                ["Pairs"] = pv.Pairs, ["Censored"] = pv.Censored, ["Unresolved"] = pv.Unresolved,
                ["Median"] = Num(pv.Median), ["Min"] = Num(pv.Min),
                ["MedianIsInfinite"] = double.IsPositiveInfinity(pv.Median),
            },
            ["FullEnergy"] = new JsonObject
            {
                ["Events"] = full.Count,
                ["MeanKeV"] = Num(mean),
                ["FwhmFraction"] = Num(2.3548 * sd / mean),
                ["FwhmFractionRobust"] = Num(fwhmRobust),
            },
        };
    }

    private static JsonObject Fraction(int k, int n) => new()
    {
        ["k"] = k, ["n"] = n, ["p"] = n > 0 ? (double)k / n : null,
    };

    private static double Median(double[] v)
    {
        if (v.Length == 0) return double.NaN;
        var s = v.OrderBy(x => x).ToArray();
        return 0.5 * (s[(s.Length - 1) / 2] + s[s.Length / 2]);
    }

    private static (double Mean, double Sd, double FwhmRobust) Resolution(List<double> e)
    {
        var v = e.Where(double.IsFinite).ToArray();
        if (v.Length < 2) return (double.NaN, double.NaN, double.NaN);
        double mean = v.Average(), sd = Math.Sqrt(v.Sum(x => (x - mean) * (x - mean)) / (v.Length - 1));
        double median = Median(v), mad = Median(v.Select(x => Math.Abs(x - median)).ToArray());
        return (mean, sd, 2.3548 * 1.4826 * mad / median);
    }

    private static (JsonObject Node, List<int> InWindow, List<ReadoutHit> Hits, List<int> Isolated) Localisation(
        SimulationConfig cfg, ReadoutDevice device, ReadoutPulseProcessor processor, ReadoutCalibration cal, Localised data,
        double line, double w, IRandom response, IRandom noise, (double X, double Y) source, int keepHits)
    {
        var inWindow = new List<int>();
        var hits = new List<ReadoutHit>();
        var isolated = new List<int>();
        var ch = new double[device.Channels];
        int triggered = 0, disagree = 0;
        var watch = Stopwatch.StartNew();
        for (int i = 0; i < data.Sites.Length; i++)
        {
            double sum = device.Respond(data.Sites[i], response, ch);
            var ev = processor.ProcessIsolated(new ReadoutHit(0, ch, sum), noise);
            int crystal = -1;
            if (ev is not null)
            {
                triggered++;
                if (device.Position(ev.Codes, out double x, out double y)) crystal = cal.Lut.Lookup(x, y);
                if (crystal >= 0 && Math.Abs(cal.Energy(crystal, device.Sum(ev.Codes)) - line) <= w * line)
                {
                    inWindow.Add(crystal);
                    if (crystal != data.Direct[i]) disagree++;
                }
            }
            if (hits.Count < keepHits)
            {
                hits.Add(new ReadoutHit(0, (double[])ch.Clone(), sum));
                isolated.Add(crystal);
            }
        }
        double microseconds = watch.Elapsed.TotalMilliseconds * 1000.0 / Math.Max(1, data.Sites.Length);
        var node = new JsonObject
        {
            ["Events"] = data.Sites.Length,
            ["TriggeredFraction"] = Num((double)triggered / data.Sites.Length),
            ["InWindow"] = inWindow.Count,
            ["DisagreeDirectInWindow"] = Fraction(disagree, inWindow.Count),
            ["ErrorMm"] = Num(Decode(cfg, inWindow, inWindow.Count, source)),
            ["MicrosecondsPerEvent"] = microseconds,
        };
        return (node, inWindow, hits, isolated);
    }

    private static JsonArray Rates(ReadoutDevice device, ReadoutPulseProcessor processor, ReadoutCalibration cal,
        List<ReadoutHit> template, List<int> isolated, double line, double w, double[] rates, Func<int, IRandom> times,
        Func<int, IRandom> noises)
    {
        var outp = new JsonArray();
        double resolving = EventStreamStudy.ResolvingSamples(processor.RiseNs / NsPerAdcSample, processor.TailNs / NsPerAdcSample);
        for (int r = 0; r < rates.Length; r++)
        {
            var clock = times(r);
            double t = 0, gap = 1e9 / rates[r];
            var hits = new List<ReadoutHit>(template.Count);
            foreach (var h in template)
            {
                t += -Math.Log(1 - clock.NextDouble()) * gap;
                hits.Add(h with { TimeNs = t });
            }
            var watch = Stopwatch.StartNew();
            var events = processor.Process(hits, noises(r));
            double seconds = watch.Elapsed.TotalSeconds;
            int piled = 0, piledMis = 0, piledRef = 0, singleMis = 0, singleRef = 0, inWindow = 0, inWindowPiled = 0;
            int pkPiledMis = 0, pkPiledRef = 0, pkSingleMis = 0, pkSingleRef = 0;
            foreach (var ev in events)
            {
                bool isPiled = ev.ContributingHits >= 2;
                if (isPiled) piled++;
                int crystal = device.Position(ev.Codes, out double x, out double y) ? cal.Lut.Lookup(x, y) : -1;
                bool win = crystal >= 0 && Math.Abs(cal.Energy(crystal, device.Sum(ev.Codes)) - line) <= w * line;
                if (win) { inWindow++; if (isPiled) inWindowPiled++; }
                int reference = ev.DominantHit >= 0 ? isolated[ev.DominantHit] : -1;
                if (reference < 0) continue;
                if (isPiled) { piledRef++; if (crystal != reference) piledMis++; }
                else { singleRef++; if (crystal != reference) singleMis++; }
                if (!win) continue;
                if (isPiled) { pkPiledRef++; if (crystal != reference) pkPiledMis++; }
                else { pkSingleRef++; if (crystal != reference) pkSingleMis++; }
            }
            var stream = hits.Select(h => new StreamEvent((long)Math.Round(h.TimeNs / NsPerAdcSample), h.Sum / device.CodesPerKeV)).ToList();
            int merged = EventStreamStudy.ApplyPileUp(stream, resolving).Count;
            outp.Add(new JsonObject
            {
                ["RateCps"] = rates[r],
                ["Hits"] = hits.Count,
                ["Events"] = events.Count,
                ["Throughput"] = Num((double)events.Count / hits.Count),
                ["PiledFraction"] = Num((double)piled / events.Count),
                ["MispositionedPiled"] = Fraction(piledMis, piledRef),
                ["MispositionedSingle"] = Fraction(singleMis, singleRef),
                ["MispositionedPiledPhotopeak"] = Fraction(pkPiledMis, pkPiledRef),
                ["MispositionedSinglePhotopeak"] = Fraction(pkSingleMis, pkSingleRef),
                ["PhotopeakPerHit"] = Num((double)inWindow / hits.Count),
                ["PiledShareOfPhotopeak"] = Num((double)inWindowPiled / inWindow),
                ["LegacyResolvingNs"] = resolving * NsPerAdcSample,
                ["LegacyMergedFraction"] = Num(1.0 - (double)merged / hits.Count),
                ["MicrosecondsPerHit"] = seconds * 1e6 / hits.Count,
            });
        }
        return outp;
    }
}
