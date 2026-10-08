namespace Gcam.Configuration;

/// <summary>The SiPM array behind the crystal array. A null size means "match the crystal array" (1:1 coupling, the
/// conventional design). A matched sensor whose active width equals the crystal's active width is a VIRTUAL sensor — a
/// labelled assumption, not a catalogue part (the engine's S13360-3050 preset is a 3 × 3 mm device).</summary>
public sealed class ReadoutSensorConfig
{
    /// <summary>Sensors along x; null = <see cref="DetectorConfig.PixelsX"/>.</summary>
    public int? CountX { get; set; }

    /// <summary>Sensors along y; null = <see cref="DetectorConfig.PixelsY"/>.</summary>
    public int? CountY { get; set; }

    /// <summary>Sensor centre-to-centre pitch (mm); null = the crystal pitch.</summary>
    public double? PitchMm { get; set; }

    /// <summary>Photosensitive width of one sensor (mm, square); null = the crystal's active width (pitch − reflector
    /// gap). The package dead border is the rest of the pitch and is applied geometrically (photons landing there are
    /// lost); the microcell fill is NOT applied here — it is inside the PDE.</summary>
    public double? ActiveWidthMm { get; set; }

    /// <summary>Lateral offset of the sensor grid centre relative to the crystal array centre (mm).</summary>
    public double OffsetXMm { get; set; }

    /// <summary>Lateral offset of the sensor grid centre along y (mm).</summary>
    public double OffsetYMm { get; set; }
}
