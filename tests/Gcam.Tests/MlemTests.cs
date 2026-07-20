using System.Linq;
using Gcam.Configuration;
using Gcam.Simulation;
using Xunit;

namespace Gcam.Tests;

/// <summary>
/// MLEM (Poisson-likelihood) reconstruction vs cross-correlation. Cross-correlation is one linear back-projection with
/// negative sidelobes that merges nearby sources; MLEM iterates the physical forward model to a NON-NEGATIVE source
/// distribution that deconvolves pairs cross-correlation cannot separate.
/// </summary>
public class MlemTests
{
    private static (MlemRow[] rows, MlemSingle single) Run(double[] seps)
    {
        var cfg = new SimulationConfig { Seed = 33 };
        var (rows, single, _, _, _, _) = new MlemStudy().Run(cfg, seps, photonCount: 800_000, mlemIterations: 60,
            seed: 33, profileSeparationMm: 3.0);
        return (rows, single);
    }

    [Fact]
    public void SingleSource_MlemIsNonNegativeAndSharp_CrossHasSidelobes()
    {
        var (_, s) = Run([3.0]);
        // Both localize the single source.
        Assert.True(s.CrossBiasMm < 1.5, $"cross-corr bias {s.CrossBiasMm:F2} mm");
        Assert.True(s.MlemBiasMm < 1.5, $"MLEM bias {s.MlemBiasMm:F2} mm");
        // MLEM is non-negative; cross-correlation dips into negative sidelobes.
        Assert.True(s.MlemMinValue >= -1e-6, $"MLEM should be non-negative, min {s.MlemMinValue:F3}");
        Assert.True(s.CrossMinValue < 0.0, $"cross-corr should have negative sidelobes, min {s.CrossMinValue:F3}");
        // MLEM deconvolves to a peak no wider than cross-correlation's.
        Assert.True(s.MlemPeakFwhmMm <= s.CrossPeakFwhmMm + 0.2, $"MLEM {s.MlemPeakFwhmMm:F2} vs cross {s.CrossPeakFwhmMm:F2}");
    }

    [Fact]
    public void MlemResolvesCloserPairsThanCrossCorrelation()
    {
        var (rows, _) = Run([2.0, 3.0, 5.0]);
        var at2 = rows.Single(r => r.SeparationMm == 2.0);
        var at3 = rows.Single(r => r.SeparationMm == 3.0);
        var at5 = rows.Single(r => r.SeparationMm == 5.0);

        // At 3 mm MLEM cleanly splits the pair while cross-correlation still sees one merged blob.
        Assert.True(at3.MlemResolved, $"MLEM should resolve a 3 mm pair, valley {at3.MlemValleyDepth:F2}");
        Assert.False(at3.CrossResolved, $"cross-corr should NOT resolve a 3 mm pair, valley {at3.CrossValleyDepth:F2}");
        // MLEM already resolves at 2 mm; cross-correlation does not.
        Assert.True(at2.MlemResolved, $"MLEM should resolve a 2 mm pair, valley {at2.MlemValleyDepth:F2}");
        Assert.False(at2.CrossResolved);
        // A wide pair (5 mm) both resolve. MLEM's valley is deeper at every separation.
        Assert.True(at5.CrossResolved && at5.MlemResolved);
        Assert.All(rows, r => Assert.True(r.MlemValleyDepth >= r.CrossValleyDepth - 1e-9,
            $"MLEM valley {r.MlemValleyDepth:F2} should be ≥ cross {r.CrossValleyDepth:F2} at {r.SeparationMm} mm"));
    }
}
