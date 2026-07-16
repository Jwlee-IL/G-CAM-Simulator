namespace Genoray.MonteCarlo.Detector;

/// <summary>
/// Time-varying temperature of the pixelated SiPM array during an acquisition, and the photopeak
/// centroid drift it causes through the SiPM gain temperature coefficient. Two physically distinct drivers:
/// <list type="bullet">
/// <item><b>Ambient</b> — the room / HVAC temperature the whole detector shares. Spatially UNIFORM, so it
/// walks every crystal's photopeak together: a global efficiency droop, not a flood non-uniformity. Modelled
/// as a slow linear drift plus an optional HVAC sine. Dominant for a fixture-mounted (거치형) rig.</item>
/// <item><b>Self-heating</b> — the array warms itself after power-on. Has a spatial GRADIENT (centre hotter
/// than the edges) and an exponential warm-up, so the photopeak walk differs across the face → a flood
/// non-uniformity that the calibration flood map cannot remove (it walks away from the cal snapshot).</item>
/// </list>
/// SiPM gain ∝ over-voltage = V_bias − V_breakdown(T); V_breakdown rises ≈ +21.5 mV/°C (Hamamatsu), so at a
/// few volts of over-voltage dGain/dT ≈ −0.7 %/°C. A bias-compensation (temperature-compensation) loop nulls a
/// fraction of that — <see cref="BiasCompFraction"/>. All temperatures are deviations from the calibration point,
/// so at t = 0 the drift is zero and the model reduces exactly to the static <see cref="CrystalUniformity"/>.
/// </summary>
public sealed class ThermalDrift
{
    public int Width { get; }
    public int Height { get; }

    /// <summary>SiPM gain temperature coefficient (relative gain change per °C). Physically ≈ −0.007 (−0.7 %/°C).</summary>
    public double AlphaPerC { get; }

    /// <summary>Fraction of the temperature coefficient removed by the bias-compensation loop (0 = none, 1 = perfect).</summary>
    public double BiasCompFraction { get; }

    /// <summary>Ambient temperature linear drift rate (°C per unit time). Uniform across the array.</summary>
    public double AmbientRatePerT { get; }

    /// <summary>Amplitude of an optional HVAC ambient temperature swing (°C). Uniform across the array.</summary>
    public double AmbientSwingC { get; }

    /// <summary>Period of the HVAC ambient swing (time units). 0 disables the swing.</summary>
    public double AmbientPeriod { get; }

    /// <summary>Asymptotic self-heating rise at the array centre (°C) once warmed up.</summary>
    public double SelfHeatC { get; }

    /// <summary>Self-heating warm-up time constant (time units).</summary>
    public double SelfHeatTau { get; }

    /// <summary>Self-heating shape at the array corner relative to the centre (0..1; 1 = flat, no gradient).</summary>
    public double EdgeFactor { get; }

    private readonly double _cx, _cy, _rMax;

    public ThermalDrift(int width, int height,
                        double alphaPerC = -0.007, double biasCompFraction = 0.0,
                        double ambientRatePerT = 0.0, double ambientSwingC = 0.0, double ambientPeriod = 0.0,
                        double selfHeatC = 0.0, double selfHeatTau = 1.0, double edgeFactor = 1.0)
    {
        Width = width;
        Height = height;
        AlphaPerC = alphaPerC;
        BiasCompFraction = biasCompFraction;
        AmbientRatePerT = ambientRatePerT;
        AmbientSwingC = ambientSwingC;
        AmbientPeriod = ambientPeriod;
        SelfHeatC = selfHeatC;
        SelfHeatTau = selfHeatTau <= 0.0 ? 1.0 : selfHeatTau;
        EdgeFactor = edgeFactor;

        _cx = (width - 1) / 2.0;
        _cy = (height - 1) / 2.0;
        _rMax = Math.Sqrt(_cx * _cx + _cy * _cy);
    }

    /// <summary>Ambient temperature deviation from calibration at time <paramref name="t"/> (uniform across the array).</summary>
    public double AmbientDelta(double t)
    {
        double d = AmbientRatePerT * t;
        if (AmbientSwingC != 0.0 && AmbientPeriod > 0.0)
            d += AmbientSwingC * Math.Sin(2.0 * Math.PI * t / AmbientPeriod);
        return d;
    }

    /// <summary>Self-heating spatial shape at pixel (x,y): 1 at the centre, <see cref="EdgeFactor"/> at the corner.</summary>
    public double SelfHeatShape(int x, int y)
    {
        if (_rMax <= 0.0) return 1.0;
        double dx = x - _cx, dy = y - _cy;
        double f = Math.Sqrt(dx * dx + dy * dy) / _rMax;   // 0 centre .. 1 corner
        return 1.0 - (1.0 - EdgeFactor) * f;
    }

    /// <summary>Total temperature deviation from calibration at pixel (x,y), time t (ambient + self-heating).</summary>
    public double DeltaT(int x, int y, double t)
    {
        double self = SelfHeatC * (1.0 - Math.Exp(-t / SelfHeatTau)) * SelfHeatShape(x, y);
        return AmbientDelta(t) + self;
    }

    /// <summary>Photopeak centroid shift (relative to the line energy) at pixel (x,y), time t, after bias compensation.</summary>
    public double CentroidShift(int x, int y, double t)
        => AlphaPerC * (1.0 - BiasCompFraction) * DeltaT(x, y, t);

    /// <summary>Row-major centroid-shift map at time <paramref name="t"/> for the whole array.</summary>
    public double[] CentroidShiftMap(double t)
    {
        var s = new double[Width * Height];
        for (int y = 0; y < Height; y++)
            for (int x = 0; x < Width; x++)
                s[y * Width + x] = CentroidShift(x, y, t);
        return s;
    }
}
