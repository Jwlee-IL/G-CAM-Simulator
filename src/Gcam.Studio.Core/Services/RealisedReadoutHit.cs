namespace Gcam.Studio.Core.Services;

/// <summary>One consumed physical input, before triggering. Index in the retained list is its global history id.
/// The direct facts are ideal deposits, never a second measurement of the four charges.</summary>
public readonly record struct RealisedReadoutHit(double ArrivalTimeS, ReadoutCharges Charges,
    int PixelX = -1, int PixelY = -1, double DepositKeV = double.NaN);
