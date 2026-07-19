namespace Genoray.MonteCarlo.Detector;

/// <summary>
/// A scintillator's NON-PROPORTIONALITY: the relative light yield per unit energy as a function of the depositing
/// ELECTRON's energy, nP(E), normalized to 1.0 at 662 keV. Real scintillators are not perfectly proportional — a
/// low-energy electron produces slightly more or less light per keV than a high-energy one. Because a full-energy
/// gamma event deposits its energy through a CASCADE of electrons whose energies vary event-to-event (a single
/// photoelectron vs a Compton recoil + a photoelectron + ...), the total light Σ Eᵢ·nP(Eᵢ) fluctuates even when the
/// total deposited energy Σ Eᵢ is fixed. That fluctuation is the INTRINSIC (photon-count-independent) resolution —
/// the physical origin of the constant "intrinsic FWHM floor" the front-end otherwise sets by hand.
///
/// The preset curves are REPRESENTATIVE electron-response shapes from the scintillator literature (not
/// spectroscopic-grade): NaI(Tl) has a strong low-energy light DEFICIT (the classic halide non-proportionality),
/// CsI(Tl) an intermediate-energy EXCESS, and GAGG:Ce is comparatively proportional (only a few % across the range).
/// </summary>
public sealed class NonProportionality
{
    public string Name { get; }
    private readonly double[] _e;   // electron energy (keV)
    private readonly double[] _r;   // relative light yield per keV, normalized to 1.0 at 662 keV

    private NonProportionality(string name, double[] eKeV, double[] relative)
    {
        Name = name;
        _e = eKeV;
        _r = relative;
    }

    /// <summary>Relative light yield per keV at electron energy <paramref name="eKeV"/> (1.0 = the 662 keV value).
    /// Linearly interpolated in log-energy; clamped outside the table.</summary>
    public double Relative(double eKeV)
    {
        if (eKeV <= _e[0]) return _r[0];
        if (eKeV >= _e[^1]) return _r[^1];
        int i = 1; while (eKeV > _e[i]) i++;
        double t = (System.Math.Log(eKeV) - System.Math.Log(_e[i - 1])) / (System.Math.Log(_e[i]) - System.Math.Log(_e[i - 1]));
        return _r[i - 1] + t * (_r[i] - _r[i - 1]);
    }

    private static readonly double[] E = { 5, 10, 20, 40, 60, 100, 200, 400, 662, 1000, 1332 };

    /// <summary>Perfectly proportional reference (nP ≡ 1): its intrinsic resolution is exactly 0.</summary>
    public static readonly NonProportionality Proportional =
        new("proportional", E, new[] { 1.0, 1.0, 1.0, 1.0, 1.0, 1.0, 1.0, 1.0, 1.0, 1.0, 1.0 });

    /// <summary>GAGG:Ce — comparatively proportional (a modest few-% hump around 100–200 keV).</summary>
    public static readonly NonProportionality Gagg =
        new("GAGG", E, new[] { 0.930, 0.945, 0.965, 0.985, 0.995, 1.015, 1.020, 1.010, 1.000, 0.996, 0.993 });

    /// <summary>NaI(Tl) — strong low-energy light DEFICIT (the classic non-proportional halide).</summary>
    public static readonly NonProportionality NaI =
        new("NaI", E, new[] { 0.640, 0.720, 0.805, 0.885, 0.920, 0.960, 1.000, 1.005, 1.000, 0.998, 0.996 });

    /// <summary>CsI(Tl) — an intermediate-energy light EXCESS then a gentle high-energy fall.</summary>
    public static readonly NonProportionality CsI =
        new("CsI", E, new[] { 0.830, 0.885, 0.945, 1.010, 1.040, 1.060, 1.040, 1.015, 1.000, 0.990, 0.984 });

    public static NonProportionality For(string? name) => name?.Trim().ToUpperInvariant() switch
    {
        "GAGG" => Gagg,
        "NAI" => NaI,
        "CSI" => CsI,
        "PROPORTIONAL" or "IDEAL" => Proportional,
        _ => Gagg,
    };
}
