namespace Genoray.MonteCarlo.Configuration;

/// <summary>
/// Root scenario description. Every knob that changes between runs lives here so
/// a simulation is fully data-driven (one JSON file == one scenario).
/// </summary>
public sealed class SimulationConfig
{
    public string Name { get; set; } = "unnamed";

    /// <summary>Number of primary photons to emit.</summary>
    public long PhotonCount { get; set; } = 1_000_000;

    /// <summary>RNG seed for reproducibility (null = nondeterministic).</summary>
    public int? Seed { get; set; }

    public SourceConfig Source { get; set; } = new();
    public MaskConfig Mask { get; set; } = new();
    public DetectorConfig Detector { get; set; } = new();
    public GeometryConfig Geometry { get; set; } = new();
    public DecoderConfig Decoder { get; set; } = new();

    /// <summary>
    /// Deep copy (via JSON round-trip). Studies clone the base config and vary one or two
    /// fields; using this avoids the field-drop bugs that hand-written clones are prone to.
    /// </summary>
    public SimulationConfig Clone()
        => System.Text.Json.JsonSerializer.Deserialize<SimulationConfig>(
               System.Text.Json.JsonSerializer.Serialize(this))!;
}

/// <summary>The reconstruction / decoding stage.</summary>
public sealed class DecoderConfig
{
    /// <summary>
    /// True = classical periodic (cyclic) decoding — aliases off-axis sources into
    /// a ghost. False = finite-mask decoding that ignores rays missing the physical
    /// mask, which suppresses the ghost.
    /// </summary>
    public bool Cyclic { get; set; } = true;

    /// <summary>Reconstruction grid half-width in mm (null = auto: one FCFOV period).</summary>
    public double? ReconHalfExtentMm { get; set; }

    /// <summary>Reconstruction grid step in mm (null = auto).</summary>
    public double? ReconStepMm { get; set; }
}

/// <summary>The radioactive source.</summary>
public sealed class SourceConfig
{
    public string Isotope { get; set; } = "Cs-137";

    /// <summary>Primary gamma energy in keV.</summary>
    public double EnergyKeV { get; set; } = 661.7;

    /// <summary>Source position [x, y, z] in mm.</summary>
    public double[] Position { get; set; } = [0.0, 0.0, 0.0];

    /// <summary>
    /// Directional biasing (detector-area importance sampling): emit only toward the
    /// detector and weight each photon so the result is unbiased vs full 4π isotropic,
    /// but ~1000x fewer photons are needed. True by default. Set false for a 4π check.
    /// </summary>
    public bool DirectionalBiasing { get; set; } = true;

    /// <summary>Source activity in becquerel (decays/s). Used by the noise study.</summary>
    public double ActivityBq { get; set; } = 1_000_000.0;

    /// <summary>Acquisition (measurement) time in seconds. Used by the noise study.</summary>
    public double AcquisitionTimeSeconds { get; set; } = 1.0;

    /// <summary>Gamma emission probability per decay (Cs-137 662 keV line = 0.851).</summary>
    public double BranchingRatio { get; set; } = 0.851;
}

/// <summary>The coded-aperture mask.</summary>
public sealed class MaskConfig
{
    public string Type { get; set; } = "MURA";

    /// <summary>MURA rank (must be prime). The old rig used 7.</summary>
    public int Rank { get; set; } = 7;

    /// <summary>Mosaic tiling of the basic pattern.</summary>
    public int MosaicX { get; set; } = 2;
    public int MosaicY { get; set; } = 2;

    public double ThicknessMm { get; set; } = 10.0;
    public string Material { get; set; } = "Tungsten";

    /// <summary>
    /// Linear attenuation coefficient of the mask material (per mm) at the source
    /// energy. Closed cells transmit exp(-mu * pathLength). Default ≈ tungsten at
    /// 662 keV (mu/rho ≈ 0.093 cm²/g × 19.25 g/cm³ ≈ 1.78 /cm). Set very high (e.g.
    /// 100) to model a perfectly opaque mask for comparison.
    /// </summary>
    public double LinearAttenuationPerMm { get; set; } = 0.178;

    /// <summary>Physical size of one mask cell in mm.</summary>
    public double CellPitchMm { get; set; } = 1.0;

    /// <summary>Use the complementary (anti-)mask: open↔closed. For mask/antimask imaging.</summary>
    public bool Invert { get; set; } = false;

    /// <summary>Channels converge toward a source at this distance (mm) for a FOCUSED coded aperture
    /// — removes off-axis open-channel collimation at the focal plane at the cost of depth of field.
    /// 0 = straight (parallel) channels.</summary>
    public double FocalDistanceMm { get; set; } = 0.0;

    /// <summary>LINEAR open fraction of a cell — the fraction of the cell's WIDTH that is drilled
    /// (so the open AREA is HoleFraction²; e.g. 0.5 → central 25% of the cell is open). 1 = full cell
    /// open; &lt;1 leaves a tungsten border that lowers sensitivity.</summary>
    public double HoleFraction { get; set; } = 1.0;

    /// <summary>Bevel angle (deg) of the channel walls — a wide-FOV device for THICK masks. The code
    /// is defined at the slab mid-plane and the walls flare toward both faces (hourglass), so an
    /// off-axis ray within the taper cone is not collimated away. 0 = straight (parallel) walls.
    /// Mutually exclusive with FocalDistanceMm (which is a narrow-FOV concentrator).</summary>
    public double TaperAngleDeg { get; set; } = 0.0;
}

/// <summary>The pixelated scintillator crystal array.</summary>
public sealed class DetectorConfig
{
    public int PixelsX { get; set; } = 12;
    public int PixelsY { get; set; } = 12;
    public double PixelPitchMm { get; set; } = 1.0;

    /// <summary>Mean energy resolution as FWHM fraction at the primary line (0 = ideal).</summary>
    public double EnergyResolutionFwhm { get; set; } = 0.0;

    // --- Crystal non-uniformity (each crystal differs slightly) ---

    /// <summary>Per-pixel random gain spread (fractional σ). 0 = perfectly uniform.</summary>
    public double GainSigma { get; set; } = 0.0;

    /// <summary>Structured gain gradient across the detector width (fractional, e.g. 0.3 = ±15%).</summary>
    public double GainGradient { get; set; } = 0.0;

    /// <summary>Per-pixel spread of the energy-resolution FWHM (fractional σ).</summary>
    public double EnergyResolutionFwhmSigma { get; set; } = 0.0;

    /// <summary>Photopeak energy-window half-width (fraction, e.g. 0.10 = ±10%). 0 = accept all.</summary>
    public double EnergyWindowFraction { get; set; } = 0.0;

    /// <summary>Seed for the (fixed) per-pixel non-uniformity pattern.</summary>
    public int UniformitySeed { get; set; } = 1;

    // --- Crystal material (stopping power / detection efficiency) ---

    /// <summary>Scintillator material name (informational).</summary>
    public string Material { get; set; } = "ideal";

    /// <summary>Crystal depth in mm (stopping-power path). Used with the attenuation below.</summary>
    public double CrystalThicknessMm { get; set; } = 10.0;

    /// <summary>
    /// Crystal linear attenuation (per mm) at the source energy. Detection efficiency =
    /// 1 - exp(-mu * depth). 0 = ideal (every incident photon detected).
    /// </summary>
    public double CrystalAttenuationPerMm { get; set; } = 0.0;

    /// <summary>Scintillation decay time in ns (used by the RTL pile-up study; informational here).</summary>
    public double DecayTimeNs { get; set; } = 0.0;
}

/// <summary>Placement of source / mask / detector planes along the optical axis (+z).</summary>
public sealed class GeometryConfig
{
    /// <summary>Mask-to-detector distance in mm (the old rig was ~50–80).</summary>
    public double MaskDetectorDistanceMm { get; set; } = 60.0;

    /// <summary>Source-to-mask distance in mm (finite = near-field magnification).</summary>
    public double SourceMaskDistanceMm { get; set; } = 100.0;
}
