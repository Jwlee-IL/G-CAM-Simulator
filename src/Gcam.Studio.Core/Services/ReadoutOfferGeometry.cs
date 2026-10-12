namespace Gcam.Studio.Core.Services;

/// <summary>A confirmed preset geometry of the shipped record (crystals per side, crystal pitch, mask-detector distance,
/// MURA rank and cell). Gap 0.1 mm, entrance absorber 0.15 mm, backing 2 mm and GAGG were fixed by the recipe.</summary>
public sealed record ReadoutOfferGeometry(string Id, int Count, double PitchMm, double DistanceMm, int Rank, double CellMm);
