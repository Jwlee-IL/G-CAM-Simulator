namespace Gcam.Studio.Core.Services;

/// <summary>One stable measured conversion; its index is independent of a transport history's pixel.</summary>
public sealed record MeasuredReadoutRecord(int Index, double HoldTimeS, double TriggerTimeS,
    ReadoutCharges Codes, ReadoutCharges Analog, double RawX, double RawY, int Crystal,
    double EnergyKeV, int DominantHit, int Contributors, double DominantShare);
