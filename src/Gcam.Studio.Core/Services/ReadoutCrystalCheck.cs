namespace Gcam.Studio.Core.Services;

/// <summary>One crystal of a preparation's local check: <see cref="Assigned"/> of <see cref="Total"/> single-crystal
/// histories were assigned by the LUT, <see cref="Wrong"/> of them to another crystal.</summary>
public sealed record ReadoutCrystalCheck(int Crystal, int Assigned, int Total, int Wrong);
