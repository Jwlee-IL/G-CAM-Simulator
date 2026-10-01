using Gcam.Configuration;
using Gcam.Core;
using Gcam.Detector;
using Gcam.Studio.Core.Services;

namespace Gcam.Studio.Services;

/// <summary>One deterministic measurement response shared by spectrum and future imaging windows.
/// True deposits stay intact; gain precedes the physical chain's smear, exactly once.</summary>
public sealed class MeasurementStage
{
    private readonly double[]? _gain;
    private readonly int _width;
    private readonly FrontEndModel _model;
    private readonly int _seed;

    public MeasurementStage(DetectorSettings? detector, int pixelsX, int pixelsY, int seed = 909)
    {
        _seed = seed;
        _model = new FrontEndModel((detector ?? new DetectorSettings()).Chain.BuildConfig());
        _width = pixelsX;
        if (detector is null) return;
        if (pixelsX <= 0 || pixelsY <= 0 || !double.IsFinite(detector.GainSigma) || detector.GainSigma < 0)
            throw new ArgumentOutOfRangeException(nameof(detector));
        _gain = new CrystalUniformity(new DetectorConfig
        {
            PixelsX = pixelsX, PixelsY = pixelsY, GainSigma = detector.GainSigma, UniformitySeed = detector.GainSeed
        }).Gain;
    }

    /// <summary>Pulse amplitude before readout noise; pile-up sums these amplitudes before a single smear.</summary>
    public double Amplitude(DetectedEvent ev)
    {
        if (_gain is null) return ev.DepositKeV;
        if (ev.PixelX < 0 || ev.PixelX >= _width || ev.PixelY < 0 || ev.PixelY >= _gain.Length / _width)
            throw new ArgumentOutOfRangeException(nameof(ev));
        return ev.DepositKeV * _gain[ev.PixelY * _width + ev.PixelX];
    }

    public double Measure(DetectedEvent ev, int index) => MeasureAmplitude(Amplitude(ev), index);

    /// <summary>Index-addressed randomness makes replay independent of snapshot partitioning.</summary>
    public double MeasureAmplitude(double energyKeV, int index)
        => _model.Measure(energyKeV, new DefaultRandom(unchecked(_seed + index * 104729)));
}
