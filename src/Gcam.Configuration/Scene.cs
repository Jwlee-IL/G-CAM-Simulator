

namespace Gcam.Configuration;

/// <summary>A gamma-emitting isotope: its decay half-life (info) and emission lines (energy keV, intensity
/// = photons per decay). Intensity is the emission weight the mixed-field source allocates photons by.</summary>
public sealed record IsotopeInfo(string Name, double HalfLifeYears, IsotopeLine[] Lines);

/// <summary>Where a line's photon comes from: a nuclear transition (gamma) or atomic K-shell fluorescence after
/// internal conversion (X-ray). Descriptive only — transport treats every photon by its energy.</summary>
public enum EmissionKind { Gamma, XRay }

/// <summary>One emission line: energy (keV), photons per decay, its kind (gamma unless stated) and, for an X-ray, the
/// emitting atom and shell (<paramref name="XRayOrigin"/>, e.g. "Ba K" — Cs-137's X-rays come from the daughter
/// Ba-137m after internal conversion, not from caesium). A plain <c>(energy, intensity)</c> pair converts to a gamma line.</summary>
public readonly record struct IsotopeLine(double EnergyKeV, double Intensity, EmissionKind Kind = EmissionKind.Gamma,
    string? XRayOrigin = null)
{
    public static implicit operator IsotopeLine((double EnergyKeV, double Intensity) line) => new(line.EnergyKeV, line.Intensity);
}

public static class Isotopes
{
    /// <summary>The common check-source isotopes. Line data are the principal gamma emissions.</summary>
    public static readonly IReadOnlyList<IsotopeInfo> All =
    [
        // Cs-137 also emits Ba K X-rays: the 662 keV transition internally converts ~10% of the time, and the
        // resulting Ba K-shell vacancy fluoresces. These are a real SOURCE emission (not environmental scatter),
        // so they belong in the line list — they put a genuine low-energy peak in the spectrum. Kα1/Kα2 (32.19/
        // 31.82) merge under the detector resolution, so they're lumped as one 32.1 keV line. Intensities are
        // per-decay (Kα ~5.6%, Kβ ~1.4%). 661.7 stays FIRST so it remains the primary line / photopeak centre.
        new IsotopeInfo("Cs-137", 30.1,  [(661.7, 0.851), new(32.1, 0.056, EmissionKind.XRay, "Ba K"), new(36.4, 0.014, EmissionKind.XRay, "Ba K")]),
        new IsotopeInfo("Co-60",   5.27, [(1173.2, 0.999), (1332.5, 0.999)]),
        new IsotopeInfo("Co-57",   0.744,[(122.1, 0.856), (136.5, 0.107)]),
        // Na-22: ENSDF, M. Shamsuzzoha Basunia, Nucl. Data Sheets 127, 69 (2015), via NNDC NuDat (checked 2026-10-02):
        // β⁺ 89.96 % per decay, 1274.537 keV γ 99.940 %. The 511 line is the annihilation PAIR, 2 × β⁺ (NuDat lists
        // 179.91 %). DecayScheme derives its per-decay branches from these two values (one table, one value).
        new IsotopeInfo("Na-22",   2.60, [(511.0, 2 * 0.8996), (1274.5, 0.9994)]),
        new IsotopeInfo("Am-241", 432.0, [(59.5, 0.359)]),
        // Ir-192 (industrial radiography, URS reference source RS-1). ENSDF, C. M. Baglin, Nucl. Data Sheets 113, 1871
        // (2012), via IAEA LiveChart and NNDC NuDat (same evaluation; values checked line by line on 2026-10-01):
        // ground state, T½ 73.829 d, β⁻ 95.24 % / EC 4.76 %. The nine gammas ≥ 1 % per decay (2.14 γ/decay); the
        // Pt / Os K X-rays (61–78 keV, ~0.19 /decay together) are left out — add them only for a low-energy study.
        // 316.5 stays FIRST: Lines[0] is the primary line / photopeak centre.
        new IsotopeInfo("Ir-192", 73.829 / 365.25, [(316.5, 0.8286), (468.1, 0.4784), (308.5, 0.2970), (296.0, 0.2871),
            (604.4, 0.08216), (612.5, 0.0534), (588.6, 0.04522), (205.8, 0.0331), (484.6, 0.0319)]),
    ];

    public static IsotopeInfo Get(string name)
    {
        foreach (var i in All) if (i.Name == name) return i;
        return All[0];
    }
}

/// <summary>One source placed in the scene: its lateral position (x, y mm — set on the canvas), its
/// distance to the detector (z mm — a slider), its isotope, and its activity in µCi.</summary>
public sealed class SceneSource
{
    public string Isotope { get; set; } = "Cs-137";
    public double X { get; set; }                 // mm, lateral
    public double Y { get; set; }                 // mm, lateral
    public double DistanceMm { get; set; } = 1000; // z from the detector plane (~1 m — a realistic standoff)
    public double ActivityUCi { get; set; } = 500.0;   // µCi — a ~1 m standoff source is dim (1/r²), so it
                                                       // needs real strength to form an image in seconds

    public const double BqPerUCi = 3.7e4;         // 1 µCi = 37 kBq

    /// <summary>Build the simulation SourceConfig: position carries the per-source distance in z, activity is
    /// converted to Bq, and the isotope's lines drive the mixed-field emission weights.</summary>
    public SourceConfig ToConfig()
    {
        var iso = Isotopes.Get(Isotope);
        return new SourceConfig
        {
            Isotope = Isotope,
            Position = [X, Y, DistanceMm],
            ActivityBq = ActivityUCi * BqPerUCi,
            DirectionalBiasing = true,
            EnergyKeV = iso.Lines[0].EnergyKeV,
            BranchingRatio = iso.Lines[0].Intensity,
            Lines = iso.Lines.Select(l => new EmissionLine { EnergyKeV = l.EnergyKeV, Intensity = l.Intensity })
                             .ToArray(),
        };
    }

    /// <summary>Photons emitted into 4π per second across all lines = Σ activity·intensity (the emission rate
    /// that, times the detection efficiency, gives the detected count rate).</summary>
    public double EmissionRatePerSec()
    {
        double a = ActivityUCi * BqPerUCi, sum = 0;
        foreach (var l in Isotopes.Get(Isotope).Lines) sum += a * l.Intensity;
        return sum;
    }

    public override string ToString() => $"{Isotope}   ({X:F0}, {Y:F0}) mm   d={DistanceMm:F0} mm   {ActivityUCi:F0} µCi";
}
