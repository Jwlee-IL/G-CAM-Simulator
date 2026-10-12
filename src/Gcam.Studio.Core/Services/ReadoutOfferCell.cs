namespace Gcam.Studio.Core.Services;

/// <summary>One confirmed (preset geometry, calibration line) cell of the shipped record: offered (experimental) iff
/// calibration succeeded on every one of its seeds, its pinned calibration budget (histories per crystal), the source
/// record and phase, literal failures, the descriptive benchmark cost rows and the crystal diagnostics.</summary>
public sealed record ReadoutOfferCell(string Geometry, double EnergyKeV, bool Offered, string Label,
    int CalibrationSeeds, IReadOnlyList<ReadoutCalibrationFailure> Failures, int Budget, string SourceRecord,
    string SourcePhase, IReadOnlyList<ReadoutCostRow> CostRows, IReadOnlyList<ReadoutCrystalDiagnostics> Diagnostics,
    ReadoutOfferOutcome? Superseded);
