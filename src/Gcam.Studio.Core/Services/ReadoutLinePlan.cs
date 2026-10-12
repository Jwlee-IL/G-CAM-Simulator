using Gcam.Configuration;

namespace Gcam.Studio.Core.Services;

/// <summary>The resolved calibration policy of one line for one module: status, the matched confirmed geometry and cell
/// (null for an unmatched module), the numerical budget (histories per crystal), LUT method and network solve, and
/// whether this is the TODO-40 legacy entry (12 × 12 Baseline at 661.7 keV: legacy seeds, LUT and solve, byte for
/// byte). <see cref="Reason"/> is the literal sentence the views show.</summary>
public sealed record ReadoutLinePlan(double LineKeV, ReadoutLineStatus Status, string? GeometryId, ReadoutOfferCell? Cell,
    int Budget, FloodCalibrationMethod Method, ChargeNetworkSolveMethod Solve, bool IsLegacy, string Reason)
{
    /// <summary>A line that may be prepared at all (offered, or locally calibrated for an unmatched Custom module).</summary>
    public bool CanPrepare => Status is ReadoutLineStatus.Offered or ReadoutLineStatus.Local;

    public string SolveLabel => Solve == ChargeNetworkSolveMethod.Adjoint ? "adjoint network solve" : "legacy network solve";

    public string MethodLabel => Method == FloodCalibrationMethod.LatticeGrowth ? "LatticeGrowth LUT (v2)" : "legacy LUT";
}
