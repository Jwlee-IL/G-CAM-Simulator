namespace Gcam.Studio.Core.Plotting;

/// <summary>An X interval, such as a photopeak energy window.</summary>
public sealed record PlotBand(double Lo, double Hi, string Label);
