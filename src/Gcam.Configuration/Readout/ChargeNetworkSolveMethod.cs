namespace Gcam.Configuration;

/// <summary>Reciprocity reduces the solve to one right-hand side per output. Legacy preserves historical rounding.</summary>
public enum ChargeNetworkSolveMethod
{
    Legacy,
    Adjoint,
}
