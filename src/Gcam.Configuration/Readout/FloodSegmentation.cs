using System.Text.Json.Serialization;

namespace Gcam.Configuration;

/// <summary>How the flood plane is split between the crystal markers. Written as a string.</summary>
[JsonConverter(typeof(JsonStringEnumConverter<FloodSegmentation>))]
public enum FloodSegmentation
{
    /// <summary>Marker-controlled watershed of the smoothed density (the conventional preset).</summary>
    Watershed,

    /// <summary>Nearest marker (Voronoi cells in the raw position plane) — the plan's alternative segmentation.</summary>
    NearestPeak,
}
