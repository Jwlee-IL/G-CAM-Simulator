using Gcam.Configuration;
using Gcam.Core;

namespace Gcam.Simulation;

/// <summary>Merge the independent incident field with an optional legacy source stream in fixed live time.</summary>
internal sealed class AmbientAcquisition : IDisposable
{
    private readonly ListModeSource? _source;
    private readonly AmbientPhotonProcess _ambient;
    private DetectedEvent? _pending;
    private readonly ListModeBackground? _darkPlacement;
    private readonly IRandom? _darkTime;
    private readonly double _darkRate;
    private double _nextDark = double.PositiveInfinity;
    public double TimeS { get; private set; }
    public double SourceRateCps => _source?.SourceRateCps ?? 0;
    public double AmbientRateCps => _ambient.DetectedRateCps;
    public double RateCps => (_source?.RateCps ?? _darkRate) + AmbientRateCps;
    public long HistoriesEmitted => _source?.HistoriesEmitted ?? 0;
    public long HistoriesDetected => _source?.HistoriesDetected ?? 0;
    public long CoincidentHistories => _source?.CoincidentHistories ?? 0;
    public double CoincidentWeight => _source?.CoincidentWeight ?? 0;
    public long EventsAccepted => _source?.EventsAccepted ?? 0;
    public double DetectedWeight => _source?.DetectedWeight ?? 0;

    public AmbientAcquisition(SimulationConfig configuration)
    {
        _ambient = new AmbientPhotonProcess(configuration);
        bool empty = configuration.Sources is { Length: 0 } || configuration.Sources is null && configuration.Source.ActivityBq == 0;
        if (!empty)
        {
            var legacy = configuration.Clone(); legacy.Ambient = null;
            _source = new ListModeSource(legacy);
        }
        else
        {
            // BSR * zero source rate is zero. Electronic dark arrivals remain source independent.
            double bsr = configuration.Background?.BackgroundToSignalRatio ?? 0;
            _darkRate = (configuration.Background?.DarkCountRateKcps ?? 0) * 1000;
            if (!double.IsFinite(bsr) || bsr < 0 || !double.IsFinite(_darkRate) || _darkRate < 0)
                throw new ArgumentOutOfRangeException(nameof(configuration));
            if (_darkRate > 0)
            {
                _darkPlacement = new ListModeBackground(configuration);
                _darkTime = new DefaultRandom(unchecked((configuration.Seed ?? 0) + 1717));
                _nextDark = DarkGap();
            }
        }
    }

    public DetectedEvent? AdvanceUntil(double horizonS, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!double.IsFinite(horizonS) || horizonS < TimeS) throw new ArgumentOutOfRangeException(nameof(horizonS));
        if (_source is not null && _pending is null)
        {
            _pending = _source.Advance(cancellationToken);
            if (_pending is null) return null;
        }
        double cutoff = Math.Min(Math.Min(horizonS, _pending?.ArrivalTimeS ?? horizonS), _nextDark);
        var ambient = _ambient.AdvanceUntil(cutoff, cancellationToken);
        TimeS = _ambient.TimeS;
        if (ambient is not null) return ambient;
        if (TimeS < cutoff) return null;
        if (_nextDark <= horizonS && _nextDark <= (_pending?.ArrivalTimeS ?? double.PositiveInfinity))
        {
            var dark = _darkPlacement!.Place(3, _nextDark);
            _nextDark += DarkGap(); return dark;
        }
        if (_pending is { } signal && signal.ArrivalTimeS <= horizonS)
        {
            _pending = null; TimeS = signal.ArrivalTimeS; return signal;
        }
        TimeS = horizonS; return null;
    }

    public void Dispose() => _source?.Dispose();
    private double DarkGap() => -Math.Log(1 - _darkTime!.NextDouble()) / _darkRate;
}
