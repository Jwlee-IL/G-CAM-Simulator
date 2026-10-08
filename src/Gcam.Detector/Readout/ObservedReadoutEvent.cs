namespace Gcam.Detector;

/// <summary>Immutable observed conversion; input indices identify retained realised hits, never truth pixels.</summary>
public sealed record ObservedReadoutEvent(double HoldTimeNs, double TriggerTimeNs,
    IReadOnlyList<double> Codes, IReadOnlyList<double> AnalogAtHold, int DominantHit,
    int ContributingHits, double DominantShare);
