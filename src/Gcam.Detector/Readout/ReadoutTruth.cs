using Gcam.Core;

namespace Gcam.Detector;

/// <summary>Reference quantities of a transported history for scoring a readout (TODO-41 rule v3): the first interaction
/// (where the coded shadow is cast) and whether the history deposited the full line energy. Used by evidence recipes only;
/// no engine path depends on them.</summary>
public static class ReadoutTruth
{
    /// <summary>The transport stops following a photon once its energy is ≤ 1 keV and drops the remainder
    /// (<c>ComptonCrystalDetector</c>), so a completely absorbed history can be short of the line by at most 1 keV; every
    /// other loss (escape through a face, scatter in the entrance absorber or the backing) is a physical energy loss. The
    /// tolerance is that cutoff plus summation round-off.</summary>
    public const double FullEnergyToleranceKeV = 1.0 + 1e-6;

    /// <summary>Index of the history's first interaction: the earliest <see cref="InteractionSite.TimeNs"/>, ties broken by
    /// site order (the lower index wins).</summary>
    public static int FirstInteraction(IReadOnlyList<InteractionSite> sites)
    {
        ArgumentNullException.ThrowIfNull(sites);
        if (sites.Count == 0) throw new ArgumentException("A history needs at least one interaction.", nameof(sites));
        int first = 0;
        for (int i = 1; i < sites.Count; i++)
            if (sites[i].TimeNs < sites[first].TimeNs) first = i;
        return first;
    }

    /// <summary>Crystal index (iy · countX + ix) of the first interaction.</summary>
    public static int FirstInteractionCrystal(IReadOnlyList<InteractionSite> sites, int countX)
    {
        var s = sites[FirstInteraction(sites)];
        return s.CrystalY * countX + s.CrystalX;
    }

    /// <summary>True when the summed deposit is within <see cref="FullEnergyToleranceKeV"/> of the line.</summary>
    public static bool IsFullEnergy(IReadOnlyList<InteractionSite> sites, double lineKeV)
    {
        ArgumentNullException.ThrowIfNull(sites);
        double sum = 0;
        foreach (var s in sites) sum += s.EnergyKeV;
        return Math.Abs(sum - lineKeV) <= FullEnergyToleranceKeV;
    }
}
