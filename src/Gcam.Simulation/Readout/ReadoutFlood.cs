using Gcam.Configuration;
using Gcam.Core;
using Gcam.Detector;

namespace Gcam.Simulation;

/// <summary>Uniform flood of the crystal array with a mono-energetic, normally incident beam (as from a distant
/// uncollimated source), transported through the configured crystal (material, depth, entrance absorber, backing,
/// reflector gaps) with the engine's Compton transport; returns the pre-optical interaction sites of every history that
/// deposited energy. Used for flood-map calibration and validation floods; the legacy optical crosstalk is not applied
/// (the physical readout's optics replace it).</summary>
public static class ReadoutFlood
{
    public static List<InteractionSite[]> Transport(SimulationConfig config, double energyKeV, int histories, int seed, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(config);
        if (!(energyKeV > 0) || histories < 0) throw new ArgumentOutOfRangeException(nameof(energyKeV));
        var d = config.Detector;
        var transport = DefaultRandom.FromKey(DefaultRandom.Key(seed, 1));
        var entry = DefaultRandom.FromKey(DefaultRandom.Key(seed, 2));
        InteractionSite[]? last = null;
        var detector = new ComptonCrystalDetector(d.PixelsX, d.PixelsY, d.PixelPitchMm, 661.7, 1.0, ComptonStrategy.Argmax,
            transport, d.CrystalAttenuationPerMm > 0 ? d.CrystalAttenuationPerMm : null, d.CrystalThicknessMm,
            entranceAbsorber: d.EntranceAbsorberMm > 0 ? new EntranceAbsorber(d.EntranceAbsorberMm) : null,
            backingScatterer: d.BackingScatterMm > 0 ? new EntranceAbsorber(d.BackingScatterMm) : null,
            reflectorGapMm: d.ReflectorGapMm, material: CrystalMaterial.ForConfig(d.Material),
            interactionSink: (sites, _) => last = sites.ToArray());
        double halfW = d.PixelsX * d.PixelPitchMm / 2, halfH = d.PixelsY * d.PixelPitchMm / 2;
        var down = new Vector3(0, 0, -1);
        var outp = new List<InteractionSite[]>(histories);
        long tries = 0, maxTries = 1000L * Math.Max(1, histories);
        while (outp.Count < histories)
        {
            if ((tries & 4095) == 0) cancellationToken.ThrowIfCancellationRequested();
            if (++tries > maxTries) throw new InvalidOperationException("Flood transport scores almost nothing (check the crystal).");
            double x = (entry.NextDouble() * 2 - 1) * halfW, y = (entry.NextDouble() * 2 - 1) * halfH;
            last = null;
            detector.Score(new Photon { Ray = new Ray(new Vector3(x, y, 10), down), EnergyKeV = energyKeV, Weight = 1 });
            if (last is { Length: > 0 }) outp.Add(last);
        }
        return outp;
    }
}
