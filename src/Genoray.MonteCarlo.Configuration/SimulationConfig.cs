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

    /// <summary>
    /// Optional MIXED-ISOTOPE FIELD: several sources (each at its own position, with its own
    /// activity and emission lines) imaged through the coded aperture in ONE run. When non-empty this
    /// replaces the single <see cref="Source"/> for emission (Source still holds scene-global settings
    /// like <see cref="SourceConfig.DirectionalBiasing"/>). Null/empty = the classic single source.
    /// </summary>
    public SourceConfig[]? Sources { get; set; }

    public MaskConfig Mask { get; set; } = new();
    public DetectorConfig Detector { get; set; } = new();
    public GeometryConfig Geometry { get; set; } = new();
    public DecoderConfig Decoder { get; set; } = new();

    /// <summary>
    /// Optional AMBIENT BACKGROUND: a diffuse/isotropic radiation field that is NOT coded by the mask
    /// (every pixel sees the mask's average transmission), so it lands as a uniform pedestal on the flood
    /// map and as extra events in the RTL stream. Null = a clean field (all legacy scenarios/tests). The
    /// controllable knob is <see cref="BackgroundConfig.BackgroundToSignalRatio"/>; nothing turns this on
    /// implicitly — a scenario or study opts in.
    /// </summary>
    public BackgroundConfig? Background { get; set; }

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

    /// <summary>Source position [x, y, z] in mm. Only x, y are used (the "off-axis angle"); z is
    /// ignored — every source sits on the source plane z = MaskDetectorDistance + SourceMaskDistance.</summary>
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

    /// <summary>
    /// Optional MULTI-LINE emission: the source emits each of these lines (energy + per-decay
    /// intensity), instead of the single <see cref="EnergyKeV"/> at <see cref="BranchingRatio"/>.
    /// e.g. Co-60 = [{1173.2, 0.999}, {1332.5, 0.999}]. Null/empty = the single line.
    /// </summary>
    public EmissionLine[]? Lines { get; set; }
}

/// <summary>One gamma emission line: energy and per-decay intensity (branching).</summary>
public sealed class EmissionLine
{
    public double EnergyKeV { get; set; }

    /// <summary>Photons of this line emitted per decay (0..~1). Sets the relative line strength.</summary>
    public double Intensity { get; set; } = 1.0;

    /// <summary>Part of a coincident cascade (e.g. Co-60 1173+1332 both emitted per decay). Informational
    /// for now — used by the detector's cascade/sum modelling, not by the geometric flood.</summary>
    public bool CascadeCoincident { get; set; } = false;
}

/// <summary>
/// A diffuse ambient background field. Because an isotropic background is not directionally coded by the
/// mask, it is modelled as an uncoded UNIFORM addition — a pedestal (+ Poisson noise) on the flood map and
/// a second Poisson event process in the RTL stream — rather than transported photon-by-photon (which would
/// just converge to the same uniform pedestal at huge cost). The master knob is a dimensionless
/// background-to-signal ratio, so a study can sweep "how bad is the background" directly.
/// </summary>
public sealed class BackgroundConfig
{
    /// <summary>Total detected background counts ÷ total detected source counts (dimensionless). The
    /// controllable master knob. 0 = no background; 1 = as many background as source counts.</summary>
    public double BackgroundToSignalRatio { get; set; } = 0.0;

    /// <summary>Representative background gamma energy (keV) — sets the crystal-deposit spectrum of a
    /// background event. A scattered/ambient field is low-energy dominated (~200); a natural line such as
    /// K-40 would be 1461. Used by the RTL event-stream path (the flood-map pedestal is energy-agnostic).</summary>
    public double EnergyKeV { get; set; } = 200.0;

    /// <summary>Optional SiPM dark-count rate (kcps) added to the RTL event stream as sub-keV
    /// single-photoelectron pulses. Null = none. The flood map ignores DCR (calibrated out in imaging).</summary>
    public double? DarkCountRateKcps { get; set; }
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

/// <summary>
/// Physical SiPM front-end model — energy resolution from the photoelectron budget (ported from
/// rtl/frontend_model.py). N_pe = lightYield · E · collection · PDE; the statistical resolution
/// R_stat(E) = 2.355·√(ENF / N_pe) scales as 1/√E, and the total is √(R_stat² + R_intrinsic²) with the
/// crystal's non-proportionality floor. Defaults ≈ CeBr3 on a good MPPC (~4.3 % FWHM @ 662 keV).
/// </summary>
public sealed class FrontEndConfig
{
    /// <summary>Scintillator light yield in photons per keV (GAGG ~50, CeBr3 ~45, LaBr3 ~63, BGO ~9).</summary>
    public double LightYieldPhPerKeV { get; set; } = 45.0;

    /// <summary>Crystal → SiPM light collection efficiency (geometry + coupling).</summary>
    public double CollectionEfficiency { get; set; } = 0.60;

    /// <summary>SiPM photon-detection efficiency at the crystal's emission wavelength.</summary>
    public double SipmPde { get; set; } = 0.45;

    /// <summary>SiPM excess noise factor (afterpulsing/crosstalk gain variance), ≈ 1.1–1.3.</summary>
    public double ExcessNoiseFactor { get; set; } = 1.20;

    /// <summary>Crystal intrinsic (non-proportionality) resolution FWHM fraction — the photon-count-independent
    /// floor (GAGG ~0.05, CeBr3 ~0.032, LaBr3 ~0.022).</summary>
    public double IntrinsicResolutionFwhm { get; set; } = 0.032;

    /// <summary>SiPM dark-count rate per channel (Hz). Dark p.e. accumulated during the integration window add
    /// a PARALLEL-noise variance to the measured charge → a resolution term that scales 1/E (worse at LOW
    /// energy, unlike the 1/√E statistical term). 0 = no DCR. S13360-3050CS ≈ 0.5–1.5 Mcps.</summary>
    public double DarkCountRateHz { get; set; } = 0.0;

    /// <summary>Charge-integration / shaping window (ns) over which dark counts accumulate. Longer window =
    /// more dark p.e. = more DCR noise (and more pile-up). Only used with <see cref="DarkCountRateHz"/>.</summary>
    public double IntegrationTimeNs { get; set; } = 200.0;
}

/// <summary>The pixelated scintillator crystal array.</summary>
public sealed class DetectorConfig
{
    public int PixelsX { get; set; } = 12;
    public int PixelsY { get; set; } = 12;
    public double PixelPitchMm { get; set; } = 1.0;

    /// <summary>Mean energy resolution as FWHM fraction at the primary line (0 = ideal). Hand-set; a
    /// physically-derived, ENERGY-DEPENDENT resolution can instead be supplied via <see cref="FrontEnd"/>.</summary>
    public double EnergyResolutionFwhm { get; set; } = 0.0;

    /// <summary>Optional physical FRONT-END model: derive the energy resolution from the photoelectron budget
    /// (crystal light yield × collection × SiPM PDE, + intrinsic non-proportionality floor) instead of the
    /// hand-set <see cref="EnergyResolutionFwhm"/>. When present the crystal-Compton detector smears each
    /// event's deposited energy by the resulting 1/√E FWHM before the energy window. Null = no smearing.</summary>
    public FrontEndConfig? FrontEnd { get; set; }

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

    /// <summary>Effective stainless-steel-equivalent thickness (mm) of passive material in front of the crystal —
    /// source encapsulation + detector entrance window/reflector/housing lumped together. Preferentially removes
    /// low-energy photons (energy-dependent μ), so it tames soft X-ray lines the way a real encapsulated source
    /// does. In the crystal-Compton detector it also SCATTERS: forward small-angle Compton events fill the
    /// photopeak's low-energy tail (the Compton-edge-to-photopeak valley). 0 = bare geometry, legacy behaviour.</summary>
    public double EntranceAbsorberMm { get; set; } = 0.0;

    /// <summary>Reflector / saw-kerf gap (mm) between adjacent crystals in the pixelated array — the DEAD REGION.
    /// The active crystal footprint is (pitch − gap); a photon whose entry point lands in the gap deposits in the
    /// reflector and is lost. Reduces the fill factor to ((pitch−gap)/pitch)² and stamps a periodic sensitivity
    /// pattern on the flood. 0 = ideal 100% fill.</summary>
    public double ReflectorGapMm { get; set; } = 0.0;

    /// <summary>Optical crosstalk fraction: the share of an interaction's scintillation light that an imperfect
    /// reflector leaks to the 4 nearest crystals (each gets a quarter). The main channel keeps (1−fraction), so a
    /// per-crystal windowed readout loses some photopeak light (efficiency drop) and neighbours see low hits.
    /// Total light is conserved (the total-energy spectrum is unaffected). 0 = perfect optical isolation.</summary>
    public double OpticalCrosstalkFraction { get; set; } = 0.0;

    /// <summary>Effective stainless-steel-equivalent thickness (mm) of the material BEHIND the crystal (SiPM /
    /// PCB / housing). A through-going photon can Compton back-scatter off it and re-enter the crystal — a ~180°
    /// scatter of 662 keV returns ~184 keV, the backscatter peak. Behind the crystal, so it does not attenuate the
    /// incoming beam. 0 = no backing (no backscatter).</summary>
    public double BackingScatterMm { get; set; } = 0.0;

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
