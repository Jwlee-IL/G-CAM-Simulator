namespace Gcam.Studio.Core.Services;

/// <summary>The resolved module a preparation was built for, captured immutably: crystal grid and pitch, reflector gap,
/// and the (virtual, 1:1) sensor grid with its actual pitch and active width. Views draw from this, never from pending
/// optics controls or the LUT-bin count.</summary>
public sealed record ReadoutModuleGeometry(int CrystalsX, int CrystalsY, double PitchMm, double GapMm,
    int SensorsX, int SensorsY, double SensorPitchMm, double SensorActiveWidthMm)
{
    public int Crystals => CrystalsX * CrystalsY;
    public int Sensors => SensorsX * SensorsY;
}
