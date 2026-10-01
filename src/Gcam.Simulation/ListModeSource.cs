using Gcam.Configuration;
using Gcam.Core;
using Gcam.Detector;

namespace Gcam.Simulation;

/// <summary>Single-consumer, incremental MC producer. Each accepted history is emitted exactly once.
/// Arrival gaps use the running importance-weighted detection efficiency, not a repeated deposit pool.</summary>
public sealed class ListModeSource : IDisposable
{
    private readonly IEnumerator<Photon> _photons;
    private readonly IMask _mask;
    private readonly IRandom _transport, _rejection, _time;
    private readonly ComptonCrystalDetector _detector;
    private readonly double _emissionRateCps;
    private (int X, int Y, double Deposit, double Weight)? _scored;
    public long HistoriesEmitted { get; private set; }
    public long HistoriesDetected { get; private set; }
    public long EventsAccepted { get; private set; }
    public double DetectedWeight { get; private set; }
    public double ArrivalTimeS { get; private set; }
    public double WeightBound { get; }
    public double RateCps => HistoriesEmitted == 0 ? 0 : _emissionRateCps * DetectedWeight / HistoriesEmitted;
    public double Acceptance => HistoriesDetected == 0 ? 0 : (double)EventsAccepted / HistoriesDetected;

    public ListModeSource(SimulationConfig configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        var config = configuration.Clone();
        // Ambient events need their own transported process; never silently fabricate or ignore them.
        if (config.Background is { BackgroundToSignalRatio: > 0 } or { DarkCountRateKcps: > 0 })
            throw new NotSupportedException("List-mode ambient acquisition is not implemented.");
        var sources = config.Sources is { Length: > 0 } scene ? scene : [config.Source];
        double area = config.Detector.PixelsX * config.Detector.PixelsY * Math.Pow(config.Detector.PixelPitchMm, 2);
        double bound = 0, rate = 0;
        foreach (var source in sources)
        {
            double z = source.Position.Length > 2 && source.Position[2] > 0 ? source.Position[2]
                : config.Geometry.MaskDetectorDistanceMm + config.Geometry.SourceMaskDistanceMm;
            if (!(z > 0) || !double.IsFinite(z)) throw new ArgumentException("Source distance must be finite and positive.");
            // r >= z, cos(theta) <= 1, so A/(4*pi*z^2) bounds every detector-area proposal.
            bound = Math.Max(bound, area / (4 * Math.PI * z * z));
            double intensity = source.Lines is { Length: > 0 } lines ? lines.Sum(l => l.Intensity) : source.BranchingRatio;
            if (!(source.ActivityBq > 0) || !(intensity > 0) || !double.IsFinite(source.ActivityBq * intensity))
                throw new ArgumentException("Emission rate must be finite and positive.");
            rate += source.ActivityBq * intensity;
        }
        _emissionRateCps = rate;
        WeightBound = config.Source.DirectionalBiasing ? bound : 1;
        var factory = new DefaultSimulationFactory();
        _transport = factory.CreateRandom(config);
        _rejection = new DefaultRandom((config.Seed ?? 0) + 8181);
        _time = new DefaultRandom((config.Seed ?? 0) + 4242);
        _photons = factory.CreateSource(config).Emit(_transport, long.MaxValue).GetEnumerator();
        _mask = factory.CreateMask(config);
        var d = config.Detector;
        _detector = new ComptonCrystalDetector(d.PixelsX, d.PixelsY, d.PixelPitchMm,
            1, double.MaxValue, ComptonStrategy.Argmax, new DefaultRandom((config.Seed ?? 0) + 777),
            d.CrystalAttenuationPerMm > 0 ? d.CrystalAttenuationPerMm : null, d.CrystalThicknessMm,
            entranceAbsorber: d.EntranceAbsorberMm > 0 ? new EntranceAbsorber(d.EntranceAbsorberMm) : null,
            backingScatterer: d.BackingScatterMm > 0 ? new EntranceAbsorber(d.BackingScatterMm) : null,
            reflectorGapMm: d.ReflectorGapMm, opticalCrosstalk: d.OpticalCrosstalkFraction,
            material: CrystalMaterial.ForConfig(d.Material),
            pixelEventSink: (x, y, deposit, weight) => _scored = (x, y, deposit, weight));
    }

    /// <summary>Transport one history; a miss or rejection returns null. Bounded work enables timely Stop.</summary>
    public DetectedEvent? Advance(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        _photons.MoveNext();
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
        ArrivalTimeS += -Math.Log(1 - _time.NextDouble()) / RateCps;
        return new DetectedEvent(hit.X, hit.Y, hit.Deposit, ArrivalTimeS);
    }

    public void Dispose() => _photons.Dispose();
}
