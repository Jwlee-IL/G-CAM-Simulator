namespace Gcam.Configuration;

/// <summary>
/// Optical transport of scintillation photons from an interaction site to the sensor plane, traced once per geometry
/// into a response table (crystal × depth bin → sensor probabilities). The crystal pixel is a box of the crystal's active
/// width; its side walls and entrance face are a reflector; its exit face is a refracting interface to a couplant layer,
/// an optional light guide, then the sensor plane. No measured reflector or ceramic optical data exists in this
/// repository (review correction 7): the reflectance, transmittance and absorption values are labelled ASSUMPTIONS to be
/// swept, not material data.
/// </summary>
public sealed class ReadoutOpticsConfig
{
    /// <summary>Refractive index of the scintillator (GAGG ≈ 1.9, published datasheet value).</summary>
    public double CrystalRefractiveIndex { get; set; } = 1.9;

    /// <summary>Refractive index of the optical couplant (silicone optical grease ≈ 1.46, published typical value).</summary>
    public double CouplantRefractiveIndex { get; set; } = 1.46;

    /// <summary>Couplant thickness (mm) — assumption.</summary>
    public double CouplantThicknessMm { get; set; } = 0.05;

    /// <summary>Light-guide thickness (mm); 0 = direct coupling (the conventional preset).</summary>
    public double LightGuideThicknessMm { get; set; }

    /// <summary>Light-guide refractive index (PMMA ≈ 1.49).</summary>
    public double LightGuideRefractiveIndex { get; set; } = 1.49;

    /// <summary>Probability that a photon striking a side wall is reflected back into its crystal — assumption. Default
    /// 0.98 with <see cref="ReflectorSurface.Specular"/>: a specular multilayer film (published reflectance ≈ 98 %). The
    /// review prototype's diffuse 0.96 is a swept alternative: traced with depth-of-interaction bins it makes the
    /// collection fall from ≈ 0.60 at the exit face to ≈ 0.37 at the entrance face of a 10 mm pixel, a ≈ 36 % FWHM
    /// photopeak at 662 keV — far wider than GAGG arrays are reported to resolve, so it is not used as the default.</summary>
    public double WallReflectance { get; set; } = 0.98;

    /// <summary>Probability that a photon striking a side wall crosses it into the neighbouring crystal (optical
    /// crosstalk). 0 = opaque reflector. The rest (1 − R − T) is absorbed in the wall.</summary>
    public double WallTransmittance { get; set; }

    /// <summary>Reflectance of the entrance (top) face; null = <see cref="WallReflectance"/>.</summary>
    public double? TopReflectance { get; set; }

    /// <summary>Angular law of wall / top reflection. A perfect specular box traps the light outside the exit escape
    /// cone (collection ≈ 0.3, nearly independent of depth); a diffuse box collects more but depth-dependently. Real
    /// surfaces lie between; both are bounding models to sweep.</summary>
    public ReflectorSurface Surface { get; set; } = ReflectorSurface.Specular;

    /// <summary>Bulk optical absorption length of the crystal for its own light (mm); 0 = not modelled (no measured
    /// value in the repository).</summary>
    public double AbsorptionLengthMm { get; set; }

    /// <summary>Depth-of-interaction bins of the response table.</summary>
    public int DepthBins { get; set; } = 8;

    /// <summary>Traced photons per (crystal class, depth bin): the table's own Monte Carlo budget. Its noise is not
    /// removed by per-crystal gain calibration — it looks like a depth-dependent collection and widens the photopeak.
    /// Measured (3.2 mm pitch, 662 keV, specular 0.98): FWHM 6.86 % at 5 000, 5.87 % at 20 000, 5.58 % at 100 000,
    /// 5.56 % at 400 000 photons per bin; 100 000 is the converged default.</summary>
    public int PhotonsPerBin { get; set; } = 100_000;

    /// <summary>A photon still bouncing after this many reflections is counted as lost (trapped).</summary>
    public int MaxBounces { get; set; } = 10_000;
}
