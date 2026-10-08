using Gcam.Core;
using Gcam.Detector;

namespace Gcam.Tests;

/// <summary>Rule v3 reference quantities with hand-computed oracles: the first interaction (earliest time, ties to the
/// lower site index) and the full-energy predicate (1 keV transport cutoff plus round-off).</summary>
public sealed class ReadoutTruthTests
{
    private static InteractionSite Site(double t, double e, int cx, int cy) => new(cx, cy, -1, t, e, cx, cy);

    [Fact]
    public void FirstInteraction_IsTheEarliestSite_NotTheLargestDeposit()
    {
        // Compton at crystal (2, 1) first, photo-absorption later in (3, 1) with the larger deposit.
        InteractionSite[] history = [Site(0.004, 900, 3, 1), Site(0.001, 432.5, 2, 1)];
        Assert.Equal(1, ReadoutTruth.FirstInteraction(history));
        Assert.Equal(1 * 4 + 2, ReadoutTruth.FirstInteractionCrystal(history, 4));
        Assert.Equal(1 * 4 + 3, ReadoutDevice.DirectCrystal(history, 4));
    }

    [Fact]
    public void FirstInteraction_TiesGoToTheLowerSiteIndex()
    {
        InteractionSite[] history = [Site(0.003, 10, 0, 0), Site(0.002, 20, 1, 0), Site(0.002, 30, 2, 0)];
        Assert.Equal(1, ReadoutTruth.FirstInteraction(history));
        InteractionSite[] single = [Site(0, 661.7, 5, 5)];
        Assert.Equal(0, ReadoutTruth.FirstInteraction(single));
        Assert.Throws<ArgumentException>(() => ReadoutTruth.FirstInteraction(Array.Empty<InteractionSite>()));
    }

    [Theory]
    [InlineData(661.7, true)]          // complete absorption
    [InlineData(660.7, true)]          // short by exactly the 1 keV transport cutoff
    [InlineData(660.69, false)]        // short by more than the cutoff: a physical loss
    [InlineData(477.3, false)]         // Compton edge: the scattered photon escaped
    public void FullEnergy_UsesTheTransportCutoff(double deposit, bool full)
    {
        InteractionSite[] history = [Site(0, deposit / 2, 0, 0), Site(0.001, deposit / 2, 1, 0)];
        Assert.Equal(full, ReadoutTruth.IsFullEnergy(history, 661.7));
    }
}
