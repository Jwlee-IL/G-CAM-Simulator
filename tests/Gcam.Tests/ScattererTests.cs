using Gcam.Core;
using Gcam.Detector;
using Xunit;

namespace Gcam.Tests;

/// <summary>The entrance/backing material is a real Compton SCATTERER, not a hand-added peak tail: a 662 keV
/// photon that interacts almost always scatters (iron is Compton-dominated there) to a lower energy, while a
/// 32 keV X-ray that interacts is mostly photo-absorbed. Those forward scatters fill the photopeak's low-energy
/// tail (the Compton-edge-to-photopeak valley) that a bare crystal leaves empty.</summary>
public class ScattererTests
{
    [Fact]
    public void Interact_662_MostlyScattersToLowerEnergy()
    {
        var abs = new EntranceAbsorber(2.0);          // thick enough to force interactions
        var rng = new DefaultRandom(1);
        int scattered = 0, absorbed = 0, passed = 0; double sumScatE = 0;
        for (int i = 0; i < 20000; i++)
        {
            var (a, e, _) = abs.Interact(661.7, new Vector3(0, 0, -1), rng);
            if (a) absorbed++;
            else if (e < 661.6) { scattered++; sumScatE += e; }
            else passed++;
        }
        Assert.True(scattered > 0, "some 662 keV photons must Compton-scatter in the slab");
        Assert.True(scattered > absorbed * 5, "at 662 keV iron interactions are Compton-dominated, not photoelectric");
        Assert.True(sumScatE / scattered < 661.7, "scattered photons lose energy");
    }

    [Fact]
    public void Interact_32keV_InteractionsMostlyAbsorbed()
    {
        var abs = new EntranceAbsorber(2.0);
        var rng = new DefaultRandom(2);
        int scattered = 0, absorbed = 0;
        for (int i = 0; i < 20000; i++)
        {
            var (a, e, _) = abs.Interact(32.1, new Vector3(0, 0, -1), rng);
            if (a) absorbed++;
            else if (e < 32.0) scattered++;
        }
        Assert.True(absorbed > scattered, "a 32 keV X-ray that interacts is mostly photo-absorbed");
    }

    [Fact]
    public void EntranceScatterer_FillsTheSubPhotopeakValley()
    {
        // Fire 662 keV photons straight into the crystal; count events depositing in the valley (500-640 keV),
        // which a bare crystal leaves nearly empty. With an entrance scatterer, forward small-angle scatters fill it.
        static int ValleyCount(EntranceAbsorber? entrance, int seed)
        {
            var deposits = new List<double>();
            var det = new ComptonCrystalDetector(16, 16, 1.0, 661.7, 1.0, ComptonStrategy.Argmax,
                new DefaultRandom(seed), muAt662PerMm: 0.09, crystalDepthMm: 10.0, planeZ: 0.0,
                sensitivity: null, eventSink: (dep, _) => deposits.Add(dep), entranceAbsorber: entrance);
            var emit = new DefaultRandom(seed + 5);
            for (int i = 0; i < 40000; i++)
            {
                double x = (emit.NextDouble() * 2 - 1) * 6, y = (emit.NextDouble() * 2 - 1) * 6;
                det.Score(new Photon { Ray = new Ray(new Vector3(x, y, 10), new Vector3(0, 0, -1)), EnergyKeV = 661.7, Weight = 1.0 });
            }
            return deposits.Count(e => e >= 500 && e < 640);
        }

        int bare = ValleyCount(null, 7);
        int withScatter = ValleyCount(new EntranceAbsorber(0.5), 7);
        Assert.True(withScatter > bare * 1.5,
            $"entrance scatter should fill the sub-photopeak valley (bare {bare} -> {withScatter})");
    }
}
