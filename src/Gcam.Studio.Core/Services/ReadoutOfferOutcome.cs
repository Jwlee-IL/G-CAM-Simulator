namespace Gcam.Studio.Core.Services;

/// <summary>A superseded published outcome (the v4 verdict a declared repair replaced), kept as history.</summary>
public sealed record ReadoutOfferOutcome(bool Offered, string Label, int CalibrationSeeds,
    IReadOnlyList<ReadoutCalibrationFailure> Failures, int Budget, string SourceRecord, string SourcePhase);
