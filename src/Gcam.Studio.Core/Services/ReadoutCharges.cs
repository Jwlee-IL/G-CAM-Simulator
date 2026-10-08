namespace Gcam.Studio.Core.Services;

/// <summary>Four realised output values, stored as scalars rather than mutable channel arrays.</summary>
public readonly record struct ReadoutCharges(double A, double B, double C, double D)
{
    public double Sum => A + B + C + D;
    public double Channel(int channel) => channel switch { 0 => A, 1 => B, 2 => C, 3 => D, _ => Sum };
}

