using Gcam.Configuration;

namespace Gcam.Studio.Core.Imaging;

/// <summary>GCAM Studio's MLEM reconstruction (TODO-36, MD-4): the pixel-area MLEM of EV-11 with an iteration count chosen
/// by the DR-5 rule at Studio's default optics (MD-4, settled at 400 by MD-10) and focus — smallest resolved separation of a 1 : 1 pair at 1000 counts
/// per source, single-source false split ≤ 5 %, ties to fewer iterations; selected on seeds disjoint from the check
/// seeds (PLAN.Studio.MlemReconstruction.Turn2). The count is not measured for other optics.</summary>
public static class StudioMlem
{
    /// <summary>Iterations at the default optics (MD-10). The DR-5 rule on the selection seeds gave 360 (the fewest that
    /// resolve 1.0 element = 0.50°, the smallest separation reached anywhere on the 120…8000 grid), but 360 sat on the 95 %
    /// edge on the check seeds (94.9 %); the author chose 400, confirmed on a third, fresh seed set: 1.0 element passes
    /// 96.0 % [94.0, 97.9] with a worst single-source false split of 0.13 % at 2000 counts.</summary>
    public const int Iterations = 400;

    /// <summary>Share of 250-count single-source frames, at <see cref="Iterations"/>, that show a second peak at least a
    /// quarter of the main peak's height (the low-count side effect; third seed set: 50.4 % [46.3, 54.2]).</summary>
    public const double LowCountSecondPeakShare = 0.50;

    /// <summary>The optics and decoder focal plane the count was selected at.</summary>
    public static OpticsSettings MeasuredOptics { get; } = new();

    public static bool IsMeasured(OpticsSettings optics, double focalDistanceMm)
    {
        var m = MeasuredOptics;
        return optics.MuraRank == m.MuraRank && optics.CellPitchMm == m.CellPitchMm
            && optics.MaskDetectorDistanceMm == m.MaskDetectorDistanceMm && optics.DetectorPixels == m.DetectorPixels
            && optics.PixelPitchMm == m.PixelPitchMm && focalDistanceMm == m.FocalDistanceMm;
    }
}
