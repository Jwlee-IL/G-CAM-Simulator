namespace Gcam.Detector;

/// <summary>Flood peak-to-valley over adjacent-crystal profiles.</summary>
/// <param name="Pairs">Adjacent pairs examined.</param>
/// <param name="Censored">Pairs whose valley has zero density (P/V infinite at this histogram resolution).</param>
/// <param name="Unresolved">Pairs whose profile maximum sits at the midpoint (no valley: P/V ≤ 1).</param>
/// <param name="Median">Median P/V over resolved pairs, censored pairs counted as +∞ (so +∞ when most are censored).</param>
/// <param name="Min">Smallest P/V (1 when any pair is unresolved).</param>
public sealed record PeakToValleyResult(int Pairs, int Censored, int Unresolved, double Median, double Min);
