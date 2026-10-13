using Gcam.Studio.Core.Plotting;

namespace Gcam.Studio.Core.Services;

/// <summary>Worker-prepared immutable scope output, with event identity and honest measurement limits.</summary>
public sealed record WaveformView(PlotSeries Adc, PlotSeries Shaped, IReadOnlyList<WaveformEvent> Events,
    string ChainReadout, string PulseReadout, string Note, double WindowUs, TimeSpan ProcessingTime)
{
    public IReadOnlyList<PlotSeries> PhysicalLanes { get; init; } = [];
    public IReadOnlyList<PlotMarker> PhysicalMarkers { get; init; } = [];
    public PlotSeries? PhysicalThreshold { get; init; }
    /// <summary>The facts of <see cref="ChainReadout"/> as (label, value) pairs for the panel's rows, same numbers.</summary>
    public IReadOnlyList<KeyValuePair<string, string>> ChainFacts { get; init; } = [];
    /// <summary>The caveats of <see cref="ChainReadout"/> that qualify the rows (kept visible under them).</summary>
    public string? ChainNote { get; init; }
    /// <summary>The facts of <see cref="PulseReadout"/> as (label, value) pairs on the physical readout; empty otherwise.</summary>
    public IReadOnlyList<KeyValuePair<string, string>> ReadingFacts { get; init; } = [];
}
