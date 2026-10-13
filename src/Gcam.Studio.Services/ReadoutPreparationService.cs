using System.Diagnostics;
using System.Globalization;
using Gcam.Configuration;
using Gcam.Core;
using Gcam.Detector;
using Gcam.Simulation;
using Gcam.Studio.Core.Services;

namespace Gcam.Studio.Services;

/// <summary>Per-line readout preparation with session-only reuse (TODO-41, SD-A1…A8). Preparation is explicit, runs on
/// one worker, streams its floods and is independent of live acquisition seeds. The 12 × 12 Baseline at 661.7 keV is the
/// TODO-40 legacy entry (seeds 402–408, legacy solve and LUT) byte for byte; every other entry uses its pinned budget,
/// LatticeGrowth and the adjoint solve, with declared line streams. Nothing is persisted: the cache lives in memory,
/// is keyed by module (device) and by line (calibration), never evicts an entry a live acquisition holds, and charges
/// each entry its accounted LUT / view bytes against <see cref="AccountedByteLimit"/>.</summary>
public sealed class ReadoutPreparationService : IReadoutPreparationService
{
    /// <summary>Optical table and network seed of every module (line-independent; TODO-40 value).</summary>
    public const int DeviceSeed = 402;

    /// <summary>Validation histories per crystal of the local single-crystal diagnostic (TODO-40 value).</summary>
    public const int ValidationHistoriesPerCrystal = 200;

    /// <summary>Provisional session limit of accounted calibration bytes (LUT density + labels and their published copies,
    /// gains and correction counts). A High-res entry accounts ≈ 48 MB; devices (optical tables) are not counted and are
    /// dropped once no entry references them. Not a measured optimum — see Turn 8.</summary>
    public const long AccountedByteLimit = 512L * 1024 * 1024;

    private const int ProgressInterval = 4096; // histories between progress reports and cancellation checks

    private readonly SemaphoreSlim _gate = new(1);
    private readonly List<ReadoutArtifact> _entries = [];
    private readonly Dictionary<string, ReadoutDevice> _devices = [];
    private readonly Dictionary<Guid, int> _pins = [];
    private readonly Lazy<ReadoutOfferRecord> _offers;

    private readonly long _limit;

    public ReadoutPreparationService() : this(ReadoutOfferRecordLoader.LoadEmbedded) { }

    /// <summary>Test seam: a record source other than the embedded, hash-pinned one, and another accounted-byte limit.</summary>
    public ReadoutPreparationService(Func<ReadoutOfferRecord> offers, long accountedByteLimit = AccountedByteLimit)
    {
        ArgumentNullException.ThrowIfNull(offers);
        ArgumentOutOfRangeException.ThrowIfNegative(accountedByteLimit);
        _offers = new(offers);
        _limit = accountedByteLimit;
    }

    public ReadoutOfferRecord Offers => _offers.Value;
    public event EventHandler? CacheChanged;
    public ReadoutPreparation? FindCached(string calibrationKey)
    {
        lock (_entries) return _entries.FirstOrDefault(e => e.View.CalibrationKey == calibrationKey)?.View;
    }
    public int CachedDevices { get { lock (_entries) return _devices.Count; } }

    public static string Key(OpticsSettings optics, DetectorSettings detector) => ReadoutPolicy.Key(optics, detector);

    /// <summary>The TODO-40 preset: four outputs, DPC row 1000 Ω / column 10 Ω, every other section at engine defaults.</summary>
    public static ReadoutConfig Preset() => new()
    {
        Mode = ReadoutMode.FourOutputAnger,
        Network = new ChargeNetworkConfig { ColumnResistanceOhm = 10 }
    };

    /// <summary>The resolved readout policy of one line: the preset with the plan's solve, LUT method, line and budget.
    /// The legacy entry resolves to exactly <see cref="Preset"/>.</summary>
    public static ReadoutConfig Policy(ReadoutLinePlan plan)
    {
        ArgumentNullException.ThrowIfNull(plan);
        var ro = Preset();
        ro.Network.SolveMethod = plan.Solve;
        ro.Calibration.Method = plan.Method;
        ro.Calibration.EnergyKeV = plan.LineKeV;
        ro.Calibration.EventsPerCrystal = plan.Budget;
        return ro;
    }

    /// <summary>Declared streams of a non-legacy entry: 402 000 000 + 1 000 · line index (1 = 122.06, 2 = 661.7,
    /// 3 = 1332.5 keV) + purpose (3 flood, 4 response, 5 ADC noise, 6 / 7 / 8 validation flood / response / noise).
    /// The legacy entry keeps 403…408.</summary>
    public static int LineSeed(ReadoutLinePlan plan, int purpose)
    {
        if (plan.IsLegacy) return DeviceSeed + purpose - 2;
        int line = ReadoutPolicy.AnalysisLines.ToList().IndexOf(plan.LineKeV) + 1;
        if (line == 0) throw new ArgumentException("Only analysis lines have declared streams.", nameof(plan));
        return 402_000_000 + 1_000 * line + purpose;
    }

    internal ReadoutArtifact Get(Guid id)
    {
        lock (_entries) return _entries.FirstOrDefault(c => c.View.Id == id)
            ?? throw new InvalidOperationException("Prepare the matching readout before Start.");
    }

    /// <summary>Captures the bank of an acquisition and pins its entries until <see cref="Release"/>.</summary>
    internal ReadoutBank Capture(IReadOnlyList<Guid> ids)
    {
        if (ids.Count == 0) throw new InvalidOperationException("Prepare the matching readout before Start.");
        lock (_entries)
        {
            var entries = ids.Select(Get).ToArray();
            if (entries.Any(e => !ReferenceEquals(e.Device, entries[0].Device)))
                throw new InvalidOperationException("Captured line preparations must share one prepared module.");
            if (entries.Select(e => e.View.LineKeV).Distinct().Count() != entries.Length)
                throw new InvalidOperationException("A bank holds one preparation per calibration line.");
            foreach (var id in ids) _pins[id] = _pins.GetValueOrDefault(id) + 1;
            return new ReadoutBank(Array.AsReadOnly(entries));
        }
    }

    internal void Release(ReadoutBank bank)
    {
        lock (_entries)
            foreach (var e in bank.Entries)
                if (_pins.TryGetValue(e.View.Id, out int n)) { if (n <= 1) _pins.Remove(e.View.Id); else _pins[e.View.Id] = n - 1; }
    }

    /// <summary>Accounted bytes currently held by the session cache.</summary>
    public long CachedBytes { get { lock (_entries) return _entries.Sum(e => e.AccountedBytes); } }

    /// <summary>Same-session cached line preparations.</summary>
    public int CachedEntries { get { lock (_entries) return _entries.Count; } }

    /// <summary>TODO-40 entry point: the module's 661.7 keV line (the legacy Baseline entry on the 12 × 12 head).</summary>
    public Task<ReadoutPreparation> PrepareAsync(OpticsSettings optics, DetectorSettings detector,
        IProgress<string>? progress = null, CancellationToken cancellationToken = default)
    {
        var plan = ReadoutPolicy.Plan(optics, detector, ReadoutPolicy.ReferenceLineKeV, Offers);
        if (!plan.CanPrepare) throw new ArgumentException(plan.Reason);
        return PrepareAsync(optics, detector, plan,
            progress is null ? null : new StageProgress(progress), cancellationToken);
    }

    public async Task<ReadoutPreparation> PrepareAsync(OpticsSettings optics, DetectorSettings detector, ReadoutLinePlan line,
        IProgress<ReadoutPrepareProgress>? progress = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(optics);
        ArgumentNullException.ThrowIfNull(detector);
        ArgumentNullException.ThrowIfNull(line);
        if (!line.CanPrepare) throw new ArgumentException(line.Reason);
        var expected = ReadoutPolicy.Plan(optics, detector, line.LineKeV, Offers);
        if (expected != line) throw new ArgumentException("The line plan does not belong to this module and record.");
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            string module = Key(optics, detector);
            string key = ReadoutPolicy.CalibrationKey(module, line);
            lock (_entries)
                if (_entries.FirstOrDefault(c => c.View.CalibrationKey == key) is { } cached)
                {
                    _entries.Remove(cached); _entries.Add(cached); // most recently used last
                    progress?.Report(new(line.LineKeV, "Session cache hit", 0, 0));
                    return cached.View;
                }
            var artifact = await Task.Run(() => Build(optics, detector, line, module, key, progress, cancellationToken), cancellationToken).ConfigureAwait(false);
            cancellationToken.ThrowIfCancellationRequested();
            if (artifact.View.Succeeded) lock (_entries)
            {
                _entries.Add(artifact);
                Evict(artifact);
            }
            return artifact.View;
        }
        finally
        {
            lock (_entries) PruneDevices();
            _gate.Release();
            CacheChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    // Oldest unpinned entries go first; the entry just added and every pinned entry stay. Devices no entry references
    // are dropped, so a module's optical table lives exactly as long as one of its calibrations.
    private void Evict(ReadoutArtifact keep)
    {
        long total = _entries.Sum(e => e.AccountedBytes);
        for (int i = 0; i < _entries.Count && total > _limit;)
        {
            var e = _entries[i];
            if (ReferenceEquals(e, keep) || _pins.ContainsKey(e.View.Id)) { i++; continue; }
            _entries.RemoveAt(i);
            total -= e.AccountedBytes;
        }
        PruneDevices();
    }

    private void PruneDevices()
    {
        foreach (var unused in _devices.Where(d => _entries.All(e => !ReferenceEquals(e.Device, d.Value))).Select(d => d.Key).ToArray())
            _devices.Remove(unused);
    }

    private ReadoutDevice Device(string key, DetectorConfig detector, ReadoutConfig policy, CancellationToken token)
    {
        lock (_entries) if (_devices.TryGetValue(key, out var cached)) return cached;
        var device = new ReadoutDevice(detector, policy, DeviceSeed, token);
        lock (_entries) _devices[key] = device;
        return device;
    }

    private ReadoutArtifact Build(OpticsSettings optics, DetectorSettings detector, ReadoutLinePlan plan, string module,
        string key, IProgress<ReadoutPrepareProgress>? progress, CancellationToken token)
    {
        var watch = Stopwatch.StartNew();
        var config = SimulationService.BuildConfig([new SceneSource()], optics,
            detector with { ReadoutMode = ReadoutMode.DirectCrystal });
        var ro = Policy(plan);
        config.Detector.Material = "GAGG";
        config.Detector.GainSigma = 0;
        int nx = config.Detector.PixelsX, ny = config.Detector.PixelsY, crystals = nx * ny;
        double line = plan.LineKeV;
        progress?.Report(new(line, "Preparing optical table and DC network…", 0, 0));
        var device = Device($"{module}|{plan.Solve}", config.Detector, ro, token);
        var processor = new ReadoutPulseProcessor(device, ro.Pulse, ro.Trigger);
        int histories = checked(crystals * plan.Budget);
        int floodSeed = LineSeed(plan, 3), responseSeed = LineSeed(plan, 4), noiseSeed = LineSeed(plan, 5);
        progress?.Report(new(line, "Transporting independent calibration flood…", 0, histories));
        var flood = new StreamedFlood(() => ReadoutFlood.Stream(config, line, histories, floodSeed, token), histories);
        var cal = ReadoutCalibration.Build(device, processor, flood, ro.Calibration, new DefaultRandom(responseSeed),
            new DefaultRandom(noiseSeed), token, collectCorrectionHistogram: true,
            progress: progress is null ? null : new CountProgress(progress, line, "Transporting independent calibration flood…", histories),
            stage: stage => progress?.Report(new(line, stage, 0, 0)));
        var lut = cal.Lut;
        var density = new DetectorImage(lut.BinsX, lut.BinsY);
        var labels = new int[lut.BinsX * lut.BinsY];
        for (int y = 0; y < lut.BinsY; y++) for (int x = 0; x < lut.BinsX; x++)
        { density[x, y] = lut.Density[y * lut.BinsX + x]; labels[y * lut.BinsX + x] = lut.LabelOfBin(x, y); }
        var diagnostics = new List<string>();
        var checks = new List<ReadoutCrystalCheck>();
        string checkScope = "";
        int vFlood = LineSeed(plan, 6), vResponse = LineSeed(plan, 7), vNoise = LineSeed(plan, 8);
        if (cal.Succeeded)
        {
            int validationHistories = checked(crystals * ValidationHistoriesPerCrystal);
            progress?.Report(new(line, "Checking independent single-crystal validation flood…", 0, validationHistories));
            int[] total = new int[crystals], accepted = new int[crystals], wrong = new int[crystals];
            var response = new DefaultRandom(vResponse); var noise = new DefaultRandom(vNoise); var channels = new double[device.Channels];
            int done = 0;
            foreach (var sites in ReadoutFlood.Stream(config, line, validationHistories, vFlood, token))
            {
                token.ThrowIfCancellationRequested();
                if ((++done % ProgressInterval) == 0) progress?.Report(new(line, "Checking independent single-crystal validation flood…", done, validationHistories));
                int truth = ReadoutDevice.DirectCrystal(sites, nx);
                if (sites.Any(s => s.CrystalY * nx + s.CrystalX != truth)) continue;
                total[truth]++;
                double sum = device.Respond(sites, response, channels);
                var ev = processor.ProcessIsolated(new(0, channels, sum), noise);
                if (ev is null || !device.Position(ev.Codes, out double x, out double y)) continue;
                int mapped = cal.Lut.Lookup(x, y); if (mapped < 0) continue;
                accepted[truth]++; if (mapped != truth) wrong[truth]++;
            }
            string lineText = line.ToString("0.##", CultureInfo.InvariantCulture);
            string seedText = plan.IsLegacy ? "validation seed 406" : FormattableString.Invariant($"validation seeds {vFlood}/{vResponse}/{vNoise}");
            for (int c = 0; c < crystals; c++)
            {
                diagnostics.Add($"Crystal {c}: assigned {accepted[c]} / {total[c]} single-crystal histories; wrong {wrong[c]} / {accepted[c]} assigned ({lineText} keV, all energies; {seedText}).");
                checks.Add(new(c, accepted[c], total[c], wrong[c]));
            }
            checkScope = $"{lineText} keV, all energies; {seedText}";
        }
        var sensors = device.Sensors;
        var view = new ReadoutPreparation(Guid.NewGuid(), module, cal.Succeeded, cal.Failure, density.ReadOnlyCopy(),
            Array.AsReadOnly(labels), Array.AsReadOnly(lut.Peaks.ToArray()), diagnostics.AsReadOnly(), cal.Scored,
            cal.Triggered, cal.InWindow, cal.CrystalsOnGlobalGain, watch.Elapsed, processor.RiseNs, processor.TailNs,
            processor.Unit(processor.PeakGridNs) == 1 ? Math.Exp(-processor.PeakGridNs / processor.TailNs) - Math.Exp(-processor.PeakGridNs / processor.RiseNs) : 1,
            processor.SupportNs, processor.ThresholdCodes)
        {
            CrystalChecks = checks.AsReadOnly(), CheckScope = checkScope,
            Circuit = device.Network.Circuit is { } circuit ? new(Array.AsReadOnly((string[])circuit.NodeNames.Clone()),
                Array.AsReadOnly(((int A, int B, double Ohm)[])circuit.Resistors.Clone()), circuit.InputOhm) : null,
            SensorActiveWidthMm = ro.Sensors.ActiveWidthMm ?? config.Detector.PixelPitchMm - config.Detector.ReflectorGapMm,
            LineKeV = line,
            Plan = plan,
            CalibrationKey = key,
            Module = new(nx, ny, config.Detector.PixelPitchMm, config.Detector.ReflectorGapMm, sensors.CountX, sensors.CountY,
                sensors.PitchMm, sensors.ActiveWidthMm),
            Seeds = plan.IsLegacy
                ? "optical 402; transport 403; response 404; ADC 405; independent validation 406/407/408"
                : FormattableString.Invariant($"optical {DeviceSeed}; transport {floodSeed}; response {responseSeed}; ADC {noiseSeed}; independent validation {vFlood}/{vResponse}/{vNoise}"),
            CorrectionHistogram = cal.CorrectionHistogram,
            Gains = cal.Gains,
        };
        long bins = (long)lut.BinsX * lut.BinsY;
        long bytes = bins * (sizeof(double) + sizeof(int)) * 2 + (long)crystals * (sizeof(double) + sizeof(int));
        return new ReadoutArtifact(view, device, cal, ro, bytes);
    }

    private sealed class CountProgress(IProgress<ReadoutPrepareProgress> target, double line, string stage, long total) : IProgress<int>
    {
        public void Report(int value) => target.Report(new(line, stage, value, total));
    }

    private sealed class StageProgress(IProgress<string> target) : IProgress<ReadoutPrepareProgress>
    {
        private string? _last;
        public void Report(ReadoutPrepareProgress value)
        {
            if (value.Stage == _last) return; // the TODO-40 contract reports stages, not counts
            _last = value.Stage;
            target.Report(value.Stage);
        }
    }
}
