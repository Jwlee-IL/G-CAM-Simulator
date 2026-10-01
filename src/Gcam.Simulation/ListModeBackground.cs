using Gcam.Configuration;
using Gcam.Core;
using Gcam.Detector;

namespace Gcam.Simulation;

/// <summary>Fresh diffuse deposits using EventStreamStudy's cosine-flux crystal response.
/// Pixel placement follows BackgroundStudy's uniform detected pedestal, not a new flux predictor.</summary>
internal sealed class ListModeBackground
{
    private readonly ComptonCrystalDetector _detector;
    private readonly IRandom _entry, _pixel;
    private readonly double _halfW, _halfH, _energy;
    private readonly int _width, _height;
    private double? _deposit;

    public ListModeBackground(SimulationConfig config)
    {
        var d = config.Detector;
        _width = d.PixelsX; _height = d.PixelsY;
        _halfW = _width * d.PixelPitchMm / 2;
        _halfH = _height * d.PixelPitchMm / 2;
        _energy = config.Background!.EnergyKeV;
        _entry = new DefaultRandom((config.Seed ?? 0) + 556);
        _pixel = new DefaultRandom((config.Seed ?? 0) + 557);
        // Same energy response as BackgroundDepositSpectrum: unmasked diffuse crystal transport.
        // BSR specifies detected counts, already including shielding/entrance loss. No recycled pool.
        _detector = new ComptonCrystalDetector(d.PixelsX, d.PixelsY, d.PixelPitchMm,
            661.7, 1, ComptonStrategy.Argmax, new DefaultRandom((config.Seed ?? 0) + 555),
            d.CrystalAttenuationPerMm > 0 ? d.CrystalAttenuationPerMm : null, d.CrystalThicknessMm,
            eventSink: (deposit, _) => _deposit = deposit, material: CrystalMaterial.ForConfig(d.Material));
    }

    /// <summary>One background history per call, so Stop remains bounded even for rare detections.</summary>
    public DetectedEvent? Advance(double arrivalTimeS)
    {
        _deposit = null;
        double x = (2 * _entry.NextDouble() - 1) * _halfW * 0.999;
        double y = (2 * _entry.NextDouble() - 1) * _halfH * 0.999;
        double u = _entry.NextDouble(), phi = 2 * Math.PI * _entry.NextDouble();
        var dir = new Vector3(Math.Sqrt(u) * Math.Cos(phi), Math.Sqrt(u) * Math.Sin(phi), -Math.Sqrt(1 - u));
        double lead = 10 / -dir.Z;
        _detector.Score(new Photon { Ray = new Ray(new Vector3(x - dir.X * lead, y - dir.Y * lead, 10), dir),
            EnergyKeV = _energy, Weight = 1 });
        return _deposit is { } energy ? Place(energy, arrivalTimeS) : null;
    }

    public DetectedEvent Place(double energyKeV, double arrivalTimeS)
        => new((int)(_pixel.NextDouble() * _width), (int)(_pixel.NextDouble() * _height), energyKeV, arrivalTimeS);
}
