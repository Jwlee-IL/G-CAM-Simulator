namespace Gcam.Core;

/// <summary>One fresh physical detector pulse, before front-end smearing or energy-window selection.</summary>
public readonly record struct DetectedEvent(int PixelX, int PixelY, double DepositKeV, double ArrivalTimeS);
