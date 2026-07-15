using Genoray.MonteCarlo.Configuration;

namespace Genoray.MonteCarlo.Wpf;

/// <summary>A gamma-emitting isotope: its decay half-life (info) and emission lines (energy keV, intensity
/// = photons per decay). Intensity is the emission weight the mixed-field source allocates photons by.</summary>
public sealed record IsotopeInfo(string Name, double HalfLifeYears, (double EnergyKeV, double Intensity)[] Lines);

public static class Isotopes
{
    /// <summary>The common check-source isotopes. Line data are the principal gamma emissions.</summary>
    public static readonly IReadOnlyList<IsotopeInfo> All =
    [
        new IsotopeInfo("Cs-137", 30.1,  [(661.7, 0.851)]),
        new IsotopeInfo("Co-60",   5.27, [(1173.2, 0.999), (1332.5, 0.999)]),
        new IsotopeInfo("Co-57",   0.744,[(122.1, 0.856), (136.5, 0.107)]),
        new IsotopeInfo("Na-22",   2.60, [(511.0, 1.798), (1274.5, 0.999)]),   // 511 = β+ annihilation pair
        new IsotopeInfo("Am-241", 432.0, [(59.5, 0.359)]),
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
