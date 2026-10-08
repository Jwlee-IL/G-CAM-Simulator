namespace Gcam.Detector;

/// <summary>One recorded (triggered, held, digitised) readout event.</summary>
/// <param name="HoldTimeNs">Instant the channels were sampled (CommonAtSumPeak) or the sum peaked (IndependentPeaks).</param>
/// <param name="Codes">Signed, pedestal-subtracted ADC code per channel.</param>
/// <param name="DominantHit">Index (in the processed hit list) of the hit contributing most to the held sum.</param>
/// <param name="ContributingHits">Hits whose pulse contributes at least the configured fraction of the held sum.</param>
/// <param name="DominantShare">The dominant hit's share of the held (noiseless) sum.</param>
public sealed record ReadoutEvent(double HoldTimeNs, double[] Codes, int DominantHit, int ContributingHits,
    double DominantShare);
