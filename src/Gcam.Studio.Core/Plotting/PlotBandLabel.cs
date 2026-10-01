namespace Gcam.Studio.Core.Plotting;

/// <summary>Screen-space label position and collision row.</summary>
public readonly record struct PlotBandLabel(int Index, double Left, double Width, int Row);
