namespace Gcam.Studio.Core.ViewModels;

/// <summary>One prepared calibration line of a ready bank, as the Readout panel's Preparation table shows it.</summary>
public sealed record ReadoutReadyRow(string LineKeV, string Time, string Windowed, string Scored);
