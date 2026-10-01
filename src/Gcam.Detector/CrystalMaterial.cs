namespace Gcam.Detector;

/// <summary>
/// A scintillator's interaction data: mass attenuation μ/ρ(E) and the photoelectric share of it, per material, for
/// the crystal transport (<see cref="CrystalDetector"/>, <see cref="ComptonCrystalDetector"/> and the studies that
/// replay its cascade).
/// </summary>
/// <remarks>
/// <para><b>Source.</b> xraylib 4.3.0 photoelectric + incoherent (Compton) cross sections for the compound, 20–800 keV,
/// with grid points just below and above every K edge in range. Above 800 keV (xraylib's spline limit) Compton is the
/// Klein–Nishina cross section × electrons per gram (binding is negligible there) and the photoelectric part is
/// extrapolated log-log from its 600–800 keV slope. Generated 2026-10-01 by `samples/materials/gen_crystal_tables.py`.</para>
/// <para><b>What is left out, on purpose.</b> Coherent (Rayleigh) scattering deposits no energy and barely deflects, so
/// μ here is photo + Compton; it is therefore 2–7 % below NIST's "total with coherent" at 300–1500 keV. Pair
/// production is omitted: negligible to 1.5 MeV, it makes μ 7 % low at 2 MeV and 17 % low at 3 MeV — no photon this
/// engine emits is above 1.33 MeV.</para>
/// <para><b>Check.</b> GAGG μ/ρ(662 keV) = 0.0773 cm²/g here (photo + Compton); with coherent added, xraylib gives
/// 0.0799 against 0.0801 from the NIST elemental tables mixed by weight.</para>
/// </remarks>
public sealed class CrystalMaterial
{
    public const double ReferenceKeV = 661.7;

    private readonly double[] _e, _mu, _pf;
    private readonly double _mu662;

    private CrystalMaterial(string name, string label, string formula, double densityGPerCm3,
        double[] energyKeV, double[] massAttenuation, double[] photoFraction)
    {
        Name = name;
        Label = label;
        Formula = formula;
        DensityGPerCm3 = densityGPerCm3;
        _e = energyKeV;
        _mu = massAttenuation;
        _pf = photoFraction;
        _mu662 = MassAttenuation(ReferenceKeV);
    }

    /// <summary>Key used in configs and presets (<c>Detector.Material</c>), e.g. "GAGG", "CeBr3".</summary>
    public string Name { get; }
    public string Label { get; }
    public string Formula { get; }
    public double DensityGPerCm3 { get; }

    /// <summary>Photo + Compton mass attenuation, cm²/g (log-log interpolated, clamped at the table ends).</summary>
    public double MassAttenuation(double energyKeV) => LogLog(energyKeV, _e, _mu);

    /// <summary>Linear attenuation, 1/mm.</summary>
    public double MuPerMm(double energyKeV) => MassAttenuation(energyKeV) * DensityGPerCm3 / 10.0;

    /// <summary>μ(E)/μ(661.7 keV): scales a configured 662 keV attenuation to another energy.</summary>
    public double MuRel(double energyKeV) => MassAttenuation(energyKeV) / _mu662;

    /// <summary>Probability that an interaction at this energy is photoelectric (full absorption) rather than Compton.</summary>
    public double PhotoFraction(double energyKeV) => Math.Clamp(LogLog(energyKeV, _e, _pf), 0.0, 1.0);

    public static IReadOnlyList<CrystalMaterial> All { get; } =
    [
        new("GAGG", "GAGG:Ce", "Gd3Al2Ga3O12", densityGPerCm3: 6.63,
            energyKeV: [20, 30, 40, 50, 50.2341, 50.2441, 60, 80, 100, 150, 200, 300, 400, 500, 600, 661.7, 800, 1000, 1250, 1500, 2000, 3000],
            massAttenuation: [30.43, 9.9986, 4.5181, 2.4547, 2.4234, 9.9646, 6.2573, 2.9569, 1.6565, 0.60857, 0.32648, 0.1647, 0.11685, 0.095252, 0.082816, 0.077343, 0.068442, 0.059822, 0.052329, 0.046959, 0.039532, 0.030766],
            photoFraction: [0.9966, 0.9884, 0.9732, 0.9499, 0.9492, 0.9876, 0.9803, 0.9587, 0.9279, 0.8172, 0.6828, 0.4446, 0.2934, 0.2051, 0.1521, 0.1297, 0.09607, 0.06638, 0.04582, 0.03382, 0.02097, 0.01077]),
        new("GAGG_Mg", "GAGG:Ce,Mg", "Gd3Al2Ga3O12", densityGPerCm3: 6.63,
            energyKeV: [20, 30, 40, 50, 50.2341, 50.2441, 60, 80, 100, 150, 200, 300, 400, 500, 600, 661.7, 800, 1000, 1250, 1500, 2000, 3000],
            massAttenuation: [30.43, 9.9986, 4.5181, 2.4547, 2.4234, 9.9646, 6.2573, 2.9569, 1.6565, 0.60857, 0.32648, 0.1647, 0.11685, 0.095252, 0.082816, 0.077343, 0.068442, 0.059822, 0.052329, 0.046959, 0.039532, 0.030766],
            photoFraction: [0.9966, 0.9884, 0.9732, 0.9499, 0.9492, 0.9876, 0.9803, 0.9587, 0.9279, 0.8172, 0.6828, 0.4446, 0.2934, 0.2051, 0.1521, 0.1297, 0.09607, 0.06638, 0.04582, 0.03382, 0.02097, 0.01077]),
        new("CeBr3", "CeBr3", "CeBr3", densityGPerCm3: 5.1,
            energyKeV: [20, 30, 40, 40.439, 40.447, 50, 60, 80, 100, 150, 200, 300, 400, 500, 600, 661.7, 800, 1000, 1250, 1500, 2000, 3000],
            massAttenuation: [44.434, 14.611, 6.5328, 6.3371, 14.18, 8.0352, 4.9182, 2.2618, 1.253, 0.46389, 0.25694, 0.1392, 0.10364, 0.086923, 0.076907, 0.072358, 0.064742, 0.05716, 0.050342, 0.045358, 0.038349, 0.029949],
            photoFraction: [0.9979, 0.9927, 0.9826, 0.982, 0.992, 0.9855, 0.976, 0.9481, 0.908, 0.7673, 0.6075, 0.3589, 0.2221, 0.1493, 0.108, 0.09119, 0.0665, 0.04523, 0.03084, 0.02257, 0.01383, 0.007012]),
        new("LaBr3", "LaBr3:Ce", "LaBr3", densityGPerCm3: 5.08,
            energyKeV: [20, 30, 38.9207, 38.9285, 40, 50, 60, 80, 100, 150, 200, 300, 400, 500, 600, 661.7, 800, 1000, 1250, 1500, 2000, 3000],
            massAttenuation: [43.781, 14.392, 6.9448, 15.178, 14.091, 7.7503, 4.7299, 2.1727, 1.2036, 0.44712, 0.24913, 0.13642, 0.10221, 0.086021, 0.076265, 0.071814, 0.064334, 0.056855, 0.050111, 0.045169, 0.038206, 0.029849],
            photoFraction: [0.9979, 0.9926, 0.9837, 0.9926, 0.9919, 0.985, 0.9751, 0.946, 0.9044, 0.7591, 0.5963, 0.3476, 0.2133, 0.1428, 0.1031, 0.08689, 0.06326, 0.04296, 0.02925, 0.02138, 0.01309, 0.006625]),
        new("LYSO", "LYSO:Ce", "Lu1.8Y0.2SiO5", densityGPerCm3: 7.1,
            energyKeV: [20, 30, 40, 50, 60, 63.3075, 63.3201, 80, 100, 150, 200, 300, 400, 500, 600, 661.7, 800, 1000, 1250, 1500, 2000, 3000],
            massAttenuation: [43.792, 14.707, 6.73, 3.6768, 2.256, 1.9575, 9.2287, 5.0411, 2.8306, 1.011, 0.512, 0.22707, 0.14597, 0.11153, 0.093022, 0.085288, 0.073313, 0.062492, 0.053632, 0.047582, 0.039558, 0.030475],
            photoFraction: [0.9979, 0.9928, 0.9833, 0.9686, 0.9483, 0.9403, 0.9873, 0.9769, 0.9596, 0.8939, 0.8041, 0.6083, 0.4488, 0.3377, 0.2627, 0.2288, 0.1751, 0.1247, 0.08815, 0.06607, 0.04175, 0.02187]),
        new("BGO", "BGO", "Bi4Ge3O12", densityGPerCm3: 7.13,
            energyKeV: [20, 30, 40, 50, 60, 80, 90.5168, 90.535, 100, 150, 200, 300, 400, 500, 600, 661.7, 800, 1000, 1250, 1500, 2000, 3000],
            massAttenuation: [65.848, 22.614, 10.48, 5.7567, 3.5342, 1.6583, 1.2092, 4.9139, 3.815, 1.3851, 0.6958, 0.29389, 0.17878, 0.13059, 0.10527, 0.094958, 0.079435, 0.065976, 0.055469, 0.048579, 0.039793, 0.030269],
            photoFraction: [0.9987, 0.9956, 0.9898, 0.9808, 0.9684, 0.9326, 0.9081, 0.9774, 0.9711, 0.925, 0.86, 0.7054, 0.5614, 0.4482, 0.3642, 0.3239, 0.2565, 0.1891, 0.1377, 0.1053, 0.06834, 0.03686]),
        new("NaI", "NaI:Tl", "NaI", densityGPerCm3: 3.67,
            energyKeV: [20, 30, 33.1661, 33.1727, 40, 50, 60, 80, 100, 150, 200, 300, 400, 500, 600, 661.7, 800, 1000, 1250, 1500, 2000, 3000],
            massAttenuation: [20.712, 6.7142, 5.0819, 29.857, 18.347, 10.178, 6.2272, 2.8628, 1.5761, 0.5663, 0.30196, 0.1534, 0.10996, 0.090352, 0.079009, 0.073972, 0.065708, 0.057656, 0.050559, 0.045439, 0.038316, 0.029862],
            photoFraction: [0.9954, 0.9841, 0.9786, 0.9963, 0.9939, 0.9886, 0.9812, 0.9593, 0.9274, 0.8109, 0.6687, 0.4224, 0.2716, 0.1864, 0.1366, 0.1158, 0.08504, 0.05818, 0.03982, 0.0292, 0.01794, 0.009104]),
    ];

    /// <summary>GAGG:Ce — the product crystal (theme 22) and the default when a config names no known material.</summary>
    public static CrystalMaterial Gagg => All[0];

    public static CrystalMaterial? Find(string? name) =>
        All.FirstOrDefault(m => string.Equals(m.Name, name?.Trim(), StringComparison.OrdinalIgnoreCase));

    /// <summary>The material a config names, or GAGG for "ideal" / empty / unknown names (documented default).</summary>
    public static CrystalMaterial ForConfig(string? name) => Find(name) ?? Gagg;

    private static double LogLog(double x, double[] xs, double[] ys)
    {
        if (x <= xs[0]) return ys[0];
        if (x >= xs[^1]) return ys[^1];
        int i = 1;
        while (x > xs[i]) i++;
        double t = (Math.Log(x) - Math.Log(xs[i - 1])) / (Math.Log(xs[i]) - Math.Log(xs[i - 1]));
        return Math.Exp(Math.Log(ys[i - 1]) + t * (Math.Log(ys[i]) - Math.Log(ys[i - 1])));
    }
}
