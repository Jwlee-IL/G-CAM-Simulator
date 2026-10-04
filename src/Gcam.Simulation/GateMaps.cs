namespace Gcam.Simulation;

/// <summary>Expected rate maps, one per counting window (row-major pixels), with the Monte Carlo standard error of each
/// window's total rate, the number of MC trials and the number of scored events behind them.</summary>
public sealed record GateMaps(double[][] RatePerPixel, double[] TotalRateStandardError, long Trials, long Events)
{
    public double TotalRate(int window) => RatePerPixel[window].Sum();
}
