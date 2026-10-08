using Gcam.Configuration;

namespace Gcam.Detector;

/// <summary>A rectangular grid of square photosensors on the sensor plane. Index = ky · CountX + kx.</summary>
public sealed record SensorLayout(int CountX, int CountY, double PitchMm, double ActiveWidthMm, double OffsetXMm,
    double OffsetYMm)
{
    public int Count => CountX * CountY;
    public double HalfSpanX => CountX * PitchMm / 2.0;
    public double HalfSpanY => CountY * PitchMm / 2.0;
    public double ActiveAreaMm2 => ActiveWidthMm * ActiveWidthMm;

    public double CenterX(int kx) => OffsetXMm + (kx + 0.5) * PitchMm - HalfSpanX;
    public double CenterY(int ky) => OffsetYMm + (ky + 0.5) * PitchMm - HalfSpanY;

    /// <summary>The sensor whose photosensitive square contains (x, y), or −1 (dead border, outside the grid).</summary>
    public int SensorAt(double x, double y)
    {
        double u = (x - OffsetXMm + HalfSpanX) / PitchMm, v = (y - OffsetYMm + HalfSpanY) / PitchMm;
        if (!(u >= 0) || !(v >= 0) || u >= CountX || v >= CountY) return -1;
        int kx = (int)u, ky = (int)v;
        double half = ActiveWidthMm / 2.0;
        if (Math.Abs(x - CenterX(kx)) > half || Math.Abs(y - CenterY(ky)) > half) return -1;
        return ky * CountX + kx;
    }

    public static SensorLayout From(ReadoutSensorConfig sensors, CrystalArrayGeometry crystals)
    {
        ArgumentNullException.ThrowIfNull(sensors);
        ArgumentNullException.ThrowIfNull(crystals);
        var s = new SensorLayout(sensors.CountX ?? crystals.CountX, sensors.CountY ?? crystals.CountY,
            sensors.PitchMm ?? crystals.PitchMm, sensors.ActiveWidthMm ?? crystals.ActiveWidthMm,
            sensors.OffsetXMm, sensors.OffsetYMm);
        if (s.CountX < 1 || s.CountY < 1 || !(s.PitchMm > 0) || !(s.ActiveWidthMm > 0) || s.ActiveWidthMm > s.PitchMm
            || !double.IsFinite(s.OffsetXMm) || !double.IsFinite(s.OffsetYMm))
            throw new ArgumentException("Sensors need positive counts and pitch, and an active width in (0, pitch] (no overlap).");
        return s;
    }
}
