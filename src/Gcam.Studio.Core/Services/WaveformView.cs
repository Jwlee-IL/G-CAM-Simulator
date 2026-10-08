using Gcam.Studio.Core.Plotting;

namespace Gcam.Studio.Core.Services;

/// <summary>Worker-prepared immutable scope output, with event identity and honest measurement limits.</summary>
public sealed record WaveformView(PlotSeries Adc, PlotSeries Shaped, IReadOnlyList<WaveformEvent> Events,
    string ChainReadout, string PulseReadout, string Note, double WindowUs, TimeSpan ProcessingTime)
{
    public IReadOnlyList<PlotSeries> PhysicalLanes { get; init; } = [];
    public IReadOnlyList<PlotMarker> PhysicalMarkers { get; init; } = [];
    public PlotSeries? PhysicalThreshold { get; init; }
}
