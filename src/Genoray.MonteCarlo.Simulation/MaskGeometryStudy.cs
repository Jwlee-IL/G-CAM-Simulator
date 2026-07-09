using Genoray.MonteCarlo.Configuration;
using Genoray.MonteCarlo.Core;

namespace Genoray.MonteCarlo.Simulation;

/// <summary>
/// Mask-channel geometry studies: finite hole size (open fraction of a cell) and FOCUSED
/// (converging) channels. Hole size trades sensitivity for shadow sharpness; focusing removes the
/// off-axis open-channel collimation at a focal distance, at the cost of depth of field.
/// </summary>
public sealed class MaskGeometryStudy
{
    private readonly ISimulationFactory _factory;

    public MaskGeometryStudy(ISimulationFactory factory)
    {
        _factory = factory;
    }

    /// <summary>Geometric efficiency (detected weight / emitted) for the given config.</summary>
    public double Efficiency(SimulationConfig cfg)
    {
        var r = new SimulationRunner(_factory).Run(cfg);
        return r.PhotonsEmitted > 0 ? r.DetectedWeight / r.PhotonsEmitted : 0.0;
    }

    /// <summary>Efficiency AND localization RMS at a fixed detected-count budget (so the shadow
    /// sharpness is isolated from the count cost).</summary>
    public (double efficiency, double rmsMm) EfficiencyAndResolution(
        SimulationConfig cfg, double budget, int repeats, double failThrMm)
    {
        var mean = new SimulationRunner(_factory).Run(cfg);
        double eff = mean.PhotonsEmitted > 0 ? mean.DetectedWeight / mean.PhotonsEmitted : 0.0;
        var img = mean.DetectorImage;
        double w = mean.DetectedWeight;
        if (w <= 0.0) return (eff, double.NaN);
        double scale = budget / w;

        var decoder = _factory.CreateDecoder(cfg)!;
        var rng = _factory.CreateRandom(cfg);
        var noisy = new DetectorImage(img.Width, img.Height);
        double tx = cfg.Source.Position[0], ty = cfg.Source.Position[1];
        double sumSq = 0.0;
        for (int rep = 0; rep < repeats; rep++)
        {
            for (int y = 0; y < img.Height; y++)
                for (int x = 0; x < img.Width; x++)
                    noisy[x, y] = Sampling.Poisson(rng, img[x, y] * scale);
            var e = decoder.Decode(noisy).Estimate.Position;
            sumSq += (e.X - tx) * (e.X - tx) + (e.Y - ty) * (e.Y - ty);
        }
        return (eff, Math.Sqrt(sumSq / repeats));
    }
}
