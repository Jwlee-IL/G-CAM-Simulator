using Gcam.Configuration;
using Gcam.Core;
using Gcam.Detector;

namespace Gcam.Simulation;

/// <summary>Single-consumer, incremental MC producer. Each accepted history is emitted exactly once.
/// Arrival gaps use the running importance-weighted detection efficiency, not a repeated deposit pool.
/// <para>A source whose isotope emits several correlated photons per decay (Co-60, Na-22 —
/// <see cref="DecayScheme.Cascade"/>) is simulated per DECAY: one history is one decay, all its photons are transported,
/// and the detected ones form ONE event — total deposit at the largest-deposit pixel over the merged interaction
/// sites, one arrival time — so true coincidence summing is part of the event itself, as in an array-wide readout.
/// With directional biasing one photon k of the n emitted, chosen uniformly, is aimed at the detector face, the others
/// follow from their conditional directions, and the decay carries the weight n·w_k / (n̄·H), H = the number of its
/// photons whose ray crosses the face; histories are allocated to sources ∝ activity × n̄ (n̄ = Σ line intensities), so
/// the emission-rate normalisation is unchanged. Unbiased by the balance identity Σ_k 1[k crosses] / H = 1 (derivation:
/// PLAN.Physics.CascadeEmission.Review, E-2). Scenes without such a source run the one-photon-per-history path and
/// draw exactly as before.</para></summary>
public sealed class ListModeSource : IDisposable
{
    private readonly IEnumerator<Photon>? _photons;
    private readonly DecayEmitter? _decays;
    private readonly List<(int X, int Y, double Deposit)> _merged = new(16);
    private double _photonDeposit;
    private bool _photonScored;
    private readonly IMask _mask = null!;
    private readonly IRandom _transport = null!, _rejection = null!, _time = null!;
    private readonly ComptonCrystalDetector _detector = null!;
    private readonly AmbientAcquisition? _physical;
    private DetectedEvent? _fixedPending;
    private readonly double _emissionRateCps;
    private readonly double _bsr, _darkRate;
    private readonly ListModeBackground? _background;
    private readonly IRandom? _backgroundTime, _darkTime;
    private DetectedEvent? _pendingSignal;
    private double _signalTime, _nextBackgroundTime = double.NaN, _nextDarkTime = double.NaN;
    private (int X, int Y, double Deposit, double Weight)? _scored;
    private long _historiesEmitted, _historiesDetected, _coincidentHistories, _eventsAccepted;
    private double _coincidentWeight, _detectedWeight;
    public long HistoriesEmitted { get => _physical?.HistoriesEmitted ?? _historiesEmitted; private set => _historiesEmitted = value; }
    public long HistoriesDetected { get => _physical?.HistoriesDetected ?? _historiesDetected; private set => _historiesDetected = value; }
    /// <summary>Detected decays in which two or more photons deposited (true-coincidence summed events).</summary>
    public long CoincidentHistories { get => _physical?.CoincidentHistories ?? _coincidentHistories; private set => _coincidentHistories = value; }
    /// <summary>Σ weight of those decays; <c>CoincidentWeight / DetectedWeight</c> estimates the summed fraction of the
    /// detected events (before the rejection step).</summary>
    public double CoincidentWeight { get => _physical?.CoincidentWeight ?? _coincidentWeight; private set => _coincidentWeight = value; }
    public long EventsAccepted { get => _physical?.EventsAccepted ?? _eventsAccepted; private set => _eventsAccepted = value; }
    public double DetectedWeight { get => _physical?.DetectedWeight ?? _detectedWeight; private set => _detectedWeight = value; }
    public double ArrivalTimeS { get; private set; }
    public double WeightBound { get; }
    public double SourceRateCps => _physical?.SourceRateCps ?? (HistoriesEmitted == 0 ? 0 : _emissionRateCps * DetectedWeight / HistoriesEmitted);
    public double AmbientRateCps => _physical?.AmbientRateCps ?? 0;
    public double RateCps => _physical?.RateCps ?? (SourceRateCps * (1 + _bsr) + _darkRate);
    public double Acceptance => HistoriesDetected == 0 ? 0 : (double)EventsAccepted / HistoriesDetected;

    public ListModeSource(SimulationConfig configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        var config = configuration.Clone();
        if (config.Ambient is not null)
        {
            _physical = new AmbientAcquisition(config);
            return;
        }
        _bsr = config.Background?.BackgroundToSignalRatio ?? 0;
        _darkRate = (config.Background?.DarkCountRateKcps ?? 0) * 1000;
        if (!double.IsFinite(_bsr) || _bsr < 0 || !double.IsFinite(_darkRate) || _darkRate < 0 ||
            _bsr > 0 && (!(config.Background!.EnergyKeV > 0) || !double.IsFinite(config.Background.EnergyKeV)))
            throw new ArgumentOutOfRangeException(nameof(configuration));
        if (_bsr > 0 || _darkRate > 0)
        {
            _background = new ListModeBackground(config);
            _backgroundTime = new DefaultRandom((config.Seed ?? 0) + 909);
            _darkTime = new DefaultRandom((config.Seed ?? 0) + 1717);
        }
        var sources = config.Sources is { Length: > 0 } scene ? scene : [config.Source];
        double area = config.Detector.PixelsX * config.Detector.PixelsY * Math.Pow(config.Detector.PixelPitchMm, 2);
        double bound = 0, rate = 0;
        bool anyCascade = false;
        foreach (var source in sources)
        {
            double z = SourceZ(config, source);
            if (!(z > 0) || !double.IsFinite(z)) throw new ArgumentException("Source distance must be finite and positive.");
            double intensity = source.Lines is { Length: > 0 } lines ? lines.Sum(l => l.Intensity) : source.BranchingRatio;
            if (!(source.ActivityBq > 0) || !(intensity > 0) || !double.IsFinite(source.ActivityBq * intensity))
                throw new ArgumentException("Emission rate must be finite and positive.");
            // r >= z, cos(theta) <= 1, so A/(4*pi*z^2) bounds every detector-area proposal. A decay's weight
            // n·w_k/(n̄·H) <= (n_max/n̄)·A/(4πz²) since H >= 1; its analog weight 1/n̄ stays below 1.
            double scale = 1;
            if (CascadeOf(source) is { } scheme)
            {
                anyCascade = true;
                intensity = scheme.MeanPhotonsPerDecay;
                scale = scheme.MaxPhotonsPerDecay / scheme.MeanPhotonsPerDecay;
            }
            bound = Math.Max(bound, scale * area / (4 * Math.PI * z * z));
            rate += source.ActivityBq * intensity;
        }
        _emissionRateCps = rate;
        WeightBound = config.Source.DirectionalBiasing ? bound : 1;
        var factory = new DefaultSimulationFactory();
        _transport = factory.CreateRandom(config);
        _rejection = new DefaultRandom((config.Seed ?? 0) + 8181);
        _time = new DefaultRandom((config.Seed ?? 0) + 4242);
        if (anyCascade) _decays = new DecayEmitter(config, sources, config.Source.DirectionalBiasing);
        else _photons = factory.CreateSource(config).Emit(_transport, long.MaxValue).GetEnumerator();
        _mask = factory.CreateMask(config);
        var d = config.Detector;
        bool decays = _decays is not null;
        _detector = new ComptonCrystalDetector(d.PixelsX, d.PixelsY, d.PixelPitchMm,
            1, double.MaxValue, ComptonStrategy.Argmax, new DefaultRandom((config.Seed ?? 0) + 777),
            d.CrystalAttenuationPerMm > 0 ? d.CrystalAttenuationPerMm : null, d.CrystalThicknessMm,
            entranceAbsorber: d.EntranceAbsorberMm > 0 ? new EntranceAbsorber(d.EntranceAbsorberMm) : null,
            backingScatterer: d.BackingScatterMm > 0 ? new EntranceAbsorber(d.BackingScatterMm) : null,
            reflectorGapMm: d.ReflectorGapMm, opticalCrosstalk: d.OpticalCrosstalkFraction,
            material: CrystalMaterial.ForConfig(d.Material),
            pixelEventSink: decays ? null : (x, y, deposit, weight) => _scored = (x, y, deposit, weight),
            pixelSitesSink: decays ? MergeSites : null);
    }

    // Merge one photon's interaction sites into its decay's: two photons in one crystal add their deposits.
    private void MergeSites(IReadOnlyList<(int X, int Y, double DepositKeV)> sites, double deposit)
    {
        foreach (var (x, y, e) in sites)
        {
            int i = _merged.FindIndex(m => m.X == x && m.Y == y);
            if (i >= 0) _merged[i] = (x, y, _merged[i].Deposit + e);
            else _merged.Add((x, y, e));
        }
        _photonDeposit = deposit;
        _photonScored = true;
    }

    /// <summary>Transport one history; a miss or rejection returns null. Bounded work enables timely Stop.</summary>
    public DetectedEvent? Advance(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (_physical is not null) throw new InvalidOperationException("Absolute ambient acquisitions require AdvanceUntil with a fixed live-time horizon.");
        // The disabled path consumes precisely the legacy RNG draws and preserves every source record.
        if (_background is null)
        {
            var signal = AdvanceSignal();
            if (signal is { } e) ArrivalTimeS = e.ArrivalTimeS;
            return signal;
        }
        if (_pendingSignal is null)
        {
            _pendingSignal = AdvanceSignal();
            if (_pendingSignal is null) return null;
        }
        if (double.IsNaN(_nextBackgroundTime))
            _nextBackgroundTime = _bsr > 0 ? Gap(_backgroundTime!, _bsr * SourceRateCps) : double.PositiveInfinity;
        if (double.IsNaN(_nextDarkTime))
            _nextDarkTime = _darkRate > 0 ? Gap(_darkTime!, _darkRate) : double.PositiveInfinity;
        if (_nextBackgroundTime < _pendingSignal.Value.ArrivalTimeS && _nextBackgroundTime <= _nextDarkTime)
        {
            var background = _background.Advance(_nextBackgroundTime);
            if (background is null) return null;
            ArrivalTimeS = _nextBackgroundTime;
            _nextBackgroundTime += Gap(_backgroundTime!, _bsr * SourceRateCps);
            return background;
        }
        if (_nextDarkTime < _pendingSignal.Value.ArrivalTimeS)
        {
            // Existing event-stream nuisance model: 3 keV-equivalent single-p.e. pulse.
            var dark = _background.Place(3, _nextDarkTime);
            ArrivalTimeS = _nextDarkTime;
            _nextDarkTime += Gap(_darkTime!, _darkRate);
            return dark;
        }
        var next = _pendingSignal;
        _pendingSignal = null;
        ArrivalTimeS = next.Value.ArrivalTimeS;
        return next;
    }

    private static double Gap(IRandom rng, double rate) => -Math.Log(1 - rng.NextDouble()) / rate;

    private static double SourceZ(SimulationConfig config, SourceConfig source)
        => source.Position.Length > 2 && source.Position[2] > 0 ? source.Position[2]
            : config.Geometry.MaskDetectorDistanceMm + config.Geometry.SourceMaskDistanceMm;

    /// <summary>The decay scheme of a source that takes the per-decay path: a cascade isotope whose configured emission
    /// lines are exactly the scheme's lines (a config that names Co-60 but emits only 1173 keV keeps what it asked for).</summary>
    private static DecayScheme? CascadeOf(SourceConfig source)
    {
        if (DecayScheme.Cascade(source.Isotope) is not { } scheme || source.Lines is not { Length: > 0 } lines) return null;
        var configured = lines.Select(l => l.EnergyKeV).Distinct().OrderBy(e => e).ToArray();
        var expected = scheme.SingleLinesKeV.OrderBy(e => e).ToArray();
        return configured.Length == expected.Length && configured.Zip(expected).All(p => Math.Abs(p.First - p.Second) < 0.5)
            ? scheme : null;
    }

    private DetectedEvent? AdvanceSignal()
    {
        if (_decays is not null) return AdvanceDecay();
        _photons!.MoveNext();
        HistoriesEmitted++;
        var photon = _photons.Current;
        _scored = null;
        if (_mask.Transmit(photon.Ray, photon.EnergyKeV, _transport)) _detector.Score(photon);
        if (_scored is not { } hit) return null;
        HistoriesDetected++;
        DetectedWeight += hit.Weight;
        if (hit.Weight > WeightBound * (1 + 1e-12)) throw new InvalidOperationException("Importance weight exceeds proven bound.");
        if (_rejection.NextDouble() >= hit.Weight / WeightBound) return null;
        EventsAccepted++;
        _signalTime += -Math.Log(1 - _time.NextDouble()) / SourceRateCps;
        return new DetectedEvent(hit.X, hit.Y, hit.Deposit, _signalTime);
    }

    private DetectedEvent? AdvanceDecay()
    {
        HistoriesEmitted++;
        var history = _decays!.Next(_transport);
        _merged.Clear();
        double total = 0;
        int depositing = 0;
        for (int i = 0; i < history.Count; i++)
        {
            var (ray, energy, crosses) = history.Photon(i);
            // A ray that misses the detector face cannot score (Score tests the same face): skip its transport.
            if (!crosses) continue;
            _photonScored = false;
            if (_mask.Transmit(ray, energy, _transport))
                _detector.Score(new Photon { Ray = ray, EnergyKeV = energy, Weight = history.Weight });
            if (!_photonScored) continue;
            total += _photonDeposit;
            depositing++;
        }
        if (depositing == 0) return null;
        HistoriesDetected++;
        double weight = history.Weight;
        DetectedWeight += weight;
        if (depositing > 1) { CoincidentHistories++; CoincidentWeight += weight; }
        if (weight > WeightBound * (1 + 1e-12)) throw new InvalidOperationException("Importance weight exceeds proven bound.");
        if (_rejection.NextDouble() >= weight / WeightBound) return null;
        EventsAccepted++;
        // Largest-deposit site over the merged sites of the whole decay (first maximum wins, as Score's Argmax).
        double best = -1;
        int bx = 0, by = 0;
        foreach (var (x, y, e) in _merged)
            if (e > best) { best = e; bx = x; by = y; }
        _signalTime += -Math.Log(1 - _time.NextDouble()) / SourceRateCps;
        return new DetectedEvent(bx, by, total, _signalTime);
    }

    /// <summary>One bounded history toward a fixed live-time horizon. Repeat until ArrivalTimeS reaches the horizon.
    /// Unlike Advance, empty absolute-field intervals progress. A retained source look-ahead survives interval boundaries.</summary>
    public DetectedEvent? AdvanceUntil(double horizonS, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (_physical is not null)
        {
            var next = _physical.AdvanceUntil(horizonS, cancellationToken);
            ArrivalTimeS = _physical.TimeS;
            return next;
        }
        if (!double.IsFinite(horizonS) || horizonS < ArrivalTimeS) throw new ArgumentOutOfRangeException(nameof(horizonS));
        if (_fixedPending is null) _fixedPending = Advance(cancellationToken);
        if (_fixedPending is not { } pending) return null;
        if (pending.ArrivalTimeS > horizonS) { ArrivalTimeS = horizonS; return null; }
        _fixedPending = null; ArrivalTimeS = pending.ArrivalTimeS; return pending;
    }

    public void Dispose() { _photons?.Dispose(); _physical?.Dispose(); }

    /// <summary>One history of a scene with a cascade source: one decay of a cascade source (all its photons, one
    /// weight) or one photon of a single-photon line, allocated ∝ activity × intensity as in <see cref="MixedFieldSource"/>.</summary>
    private sealed class DecayEmitter
    {
        private readonly (Vector3 Pos, DecayScheme? Scheme, double EnergyKeV)[] _emitters;
        private readonly double[] _cumulative;
        private readonly bool _biased;
        private readonly double _halfW, _halfH, _norm;
        private readonly List<double> _energies = new(3);
        private Vector3[] _dirs = new Vector3[3];
        private readonly History _history = new();

        public DecayEmitter(SimulationConfig config, SourceConfig[] sources, bool biased)
        {
            var emitters = new List<(Vector3, DecayScheme?, double)>();
            var weights = new List<double>();
            foreach (var s in sources)
            {
                var pos = new Vector3(s.Position[0], s.Position[1], SourceZ(config, s));
                if (CascadeOf(s) is { } scheme)
                {
                    emitters.Add((pos, scheme, 0));
                    weights.Add(s.ActivityBq * scheme.MeanPhotonsPerDecay);
                }
                else if (s.Lines is { Length: > 0 } lines)
                    foreach (var l in lines) { emitters.Add((pos, null, l.EnergyKeV)); weights.Add(s.ActivityBq * l.Intensity); }
                else { emitters.Add((pos, null, s.EnergyKeV)); weights.Add(s.ActivityBq * s.BranchingRatio); }
            }
            _emitters = emitters.ToArray();
            _cumulative = new double[weights.Count];
            double acc = 0;
            for (int i = 0; i < weights.Count; i++)
            {
                acc += double.IsFinite(weights[i]) && weights[i] > 0 ? weights[i] : 0;
                _cumulative[i] = acc;
            }
            if (!(acc > 0)) throw new ArgumentException("Emission rate must be finite and positive.");
            _biased = biased;
            _halfW = config.Detector.PixelsX * config.Detector.PixelPitchMm / 2;
            _halfH = config.Detector.PixelsY * config.Detector.PixelPitchMm / 2;
            _norm = 4 * _halfW * _halfH / (4 * Math.PI);
        }

        public History Next(IRandom rng)
        {
            double u = rng.NextDouble() * _cumulative[^1];
            int j = 0;
            while (j < _cumulative.Length - 1 && u > _cumulative[j]) j++;
            var (pos, scheme, lineEnergy) = _emitters[j];
            _history.Clear(pos);
            if (scheme is null)
            {
                var (dir, w) = _biased ? Aim(rng, pos) : (rng.NextOnUnitSphere(), 1.0);
                _history.Add(dir, lineEnergy, _biased || CrossesFace(pos, dir));
                _history.Weight = w;
                return _history;
            }
            scheme.SampleEnergies(rng, _energies);
            int n = _energies.Count;
            if (n == 0) return _history;
            if (_dirs.Length < n) _dirs = new Vector3[n];
            int k = 0;
            Vector3 known;
            double wk = 1;
            if (_biased)
            {
                if (n > 1) k = Math.Min(n - 1, (int)(rng.NextDouble() * n));
                (known, wk) = Aim(rng, pos);
            }
            else known = rng.NextOnUnitSphere();
            scheme.Directions(rng, _energies, k, known, _dirs);
            int crossing = 0;
            for (int i = 0; i < n; i++)
            {
                // The aimed photon crosses the face by construction (no re-test at the rounding edge).
                bool crosses = _biased && i == k || CrossesFace(pos, _dirs[i]);
                if (crosses) crossing++;
                _history.Add(_dirs[i], _energies[i], crosses);
            }
            _history.Weight = _biased ? n * wk / (scheme.MeanPhotonsPerDecay * crossing) : 1 / scheme.MeanPhotonsPerDecay;
            return _history;
        }

        // Uniform point on the detector face; weight A·cosθ/(4π r²), as DetectorBiasedSource / MixedFieldSource.
        private (Vector3 Dir, double Weight) Aim(IRandom rng, Vector3 pos)
        {
            double tx = (rng.NextDouble() * 2.0 - 1.0) * _halfW;
            double ty = (rng.NextDouble() * 2.0 - 1.0) * _halfH;
            var delta = new Vector3(tx, ty, 0.0) - pos;
            double r = delta.Length;
            var dir = delta * (1.0 / r);
            return (dir, _norm * Math.Abs(dir.Z) / (r * r));
        }

        // The face test of ComptonCrystalDetector.Score: plane z = 0, [−half, half).
        private bool CrossesFace(Vector3 pos, Vector3 dir)
        {
            if (dir.Z >= 0) return false;
            double t = -pos.Z / dir.Z;
            double x = pos.X + t * dir.X, y = pos.Y + t * dir.Y;
            return x >= -_halfW && x < _halfW && y >= -_halfH && y < _halfH;
        }
    }

    private sealed class History
    {
        private readonly List<(Vector3 Dir, double EnergyKeV, bool Crosses)> _photons = new(3);
        private Vector3 _origin;
        public double Weight { get; set; }
        public int Count => _photons.Count;
        public void Clear(Vector3 origin) { _photons.Clear(); _origin = origin; Weight = 0; }
        public void Add(Vector3 dir, double energyKeV, bool crosses) => _photons.Add((dir, energyKeV, crosses));
        public (Ray Ray, double EnergyKeV, bool Crosses) Photon(int i)
            => (new Ray(_origin, _photons[i].Dir), _photons[i].EnergyKeV, _photons[i].Crosses);
    }
}
