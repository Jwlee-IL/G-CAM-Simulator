using Gcam.Configuration;

namespace Gcam.Simulation;

/// <summary>One outer seed of an AB-13 evidence family under the absolute ambient field (TODO-30 turn 8), as a declarative
/// request: <see cref="Family"/> names the study and its section carries the recipe. The seed driver clones it and
/// replaces <see cref="Seed"/> and <see cref="RepoRoot"/>; every other number is declared in the committed request.
/// Shared by every family: the validated spectrum, the field levels, the two bounds (AB-2), the counting windows and the
/// Monte Carlo budgets of the response maps (<see cref="GateResponse"/>).</summary>
public sealed class AmbientEvidenceRequest
{
    public int Seed { get; set; }
    /// <summary>Absolute repository root (the driver sets it); every path below is relative to it.</summary>
    public string RepoRoot { get; set; } = ".";
    /// <summary><c>antimask</c> (EV-12), <c>fov</c> (EV-02), <c>separation</c> (EV-15), <c>sweep</c> (EV-01, AB-12) or
    /// <c>csco</c> (TODO-35: Cs-137 under Co-60, <see cref="CsUnderCo"/>) or <c>angres</c> (TODO-34: angular resolution,
    /// <see cref="AngularResolution"/>).</summary>
    public string Family { get; set; } = "";
    public AmbientGateRequest.FileReference Spectrum { get; set; } = new();
    /// <summary>Incident photons per ambient "truth" map (the field the acquisitions are drawn from).</summary>
    public long AmbientHistories { get; set; }
    /// <summary>Incident photons per independent ambient map standing for the instrument's background model (0 = none).</summary>
    public long CalibrationHistories { get; set; }
    /// <summary>Photons per source response map.</summary>
    public long SourcePhotons { get; set; }
    public double[] FieldsMicroSvPerHour { get; set; } = [];
    public AmbientGeometry[] Bounds { get; set; } = [];
    public AmbientGateRequest.WindowSpec[] Windows { get; set; } = [];
    public double[] ExposuresS { get; set; } = [];
    public int Repeats { get; set; }
    /// <summary>Gross-failure radius at the source plane (mm), the legacy studies' 3 mm.</summary>
    public double FailThresholdMm { get; set; } = 3.0;
    public AntimaskSpec? Antimask { get; set; }
    public FieldOfViewSpec? FieldOfView { get; set; }
    public SeparationSpec? Separation { get; set; }
    public SweepSpec? Sweep { get; set; }
    public CsUnderCoSpec? CsUnderCo { get; set; }
    public AngularResolutionSpec? AngularResolution { get; set; }

    /// <summary>EV-12: single mask, calibrated subtraction and a two-exposure mask / antimask at equal total time.</summary>
    public sealed class AntimaskSpec
    {
        public string Scenario { get; set; } = "";
        /// <summary>Expected net source counts on the mask in the full exposure, per window (the legacy study's 400).</summary>
        public double SourceCounts { get; set; }
    }

    /// <summary>EV-02: off-axis angle sweep at field distance, cyclic and wide non-cyclic decoding, flood-centroid cue.</summary>
    public sealed class FieldOfViewSpec
    {
        public string Scenario { get; set; } = "";
        /// <summary>Source–mask distances S (mm), as the legacy study.</summary>
        public double[] SourceMaskDistancesMm { get; set; } = [];
        public double[] DirectionsDeg { get; set; } = [];
        public double[] AnglesDeg { get; set; } = [];
        /// <summary>Counts the source would give on axis in the window (N0); activity = N0 / (exposure × on-axis rate).</summary>
        public double[] OnAxisCounts { get; set; } = [];
        public double GridHalfAngleDeg { get; set; } = 20;
        public double GridStepDeg { get; set; } = 0.3;
        /// <summary>Background-and-source draws per in-field angle used only to set the centroid flag's threshold.</summary>
        public int CalibrationRepeats { get; set; }
    }

    /// <summary>EV-15: Cs-137 beside / with Co-60, spatial decoding of the 662 keV window and per-pixel Compton stripping.</summary>
    public sealed class SeparationSpec
    {
        public string Scenario { get; set; } = "";
        public double CsActivityBq { get; set; }
        public double CoActivityBq { get; set; }
        /// <summary>Live time (s) of every acquisition.</summary>
        public double ExposureS { get; set; }
        public SourceLine[] CsLines { get; set; } = [];
        public SourceLine[] CoLines { get; set; } = [];
        public Scene[] Scenes { get; set; } = [];
        /// <summary>Window that receives Cs (the 662 keV one) and the Co photopeak window used for stripping.</summary>
        public string CsWindow { get; set; } = "";
        public string CoWindow { get; set; } = "";
        public double ReconHalfExtentMm { get; set; }
        public double ReconStepMm { get; set; }
    }

    public sealed class SourceLine
    {
        public double EnergyKeV { get; set; }
        public double Intensity { get; set; }
    }

    public sealed class Scene
    {
        public string Name { get; set; } = "";
        public double[] CsMm { get; set; } = [];
        public double[] CoMm { get; set; } = [];
    }

    /// <summary>EV-01: source-position sweep at the recipe's activity and exposure, plus fixed points (AB-12 bias).</summary>
    public sealed class SweepSpec
    {
        public SweepScenario[] Scenarios { get; set; } = [];
        public double HalfExtentMm { get; set; }
        public double StepMm { get; set; }
        public double ReconStepMm { get; set; }
        public double LocalizedWithinMm { get; set; } = 3.0;
        public int PointRepeats { get; set; }
        public int MlemIterations { get; set; }
        public int MlemRepeats { get; set; }
    }

    public sealed class SweepScenario
    {
        public string Name { get; set; } = "";
        public string Scenario { get; set; } = "";
        /// <summary>Fixed points (mm) decoded with the scenario's own decoder (EV-01's single-run quotes) and MLEM.</summary>
        public double[][] PointsMm { get; set; } = [];
    }
    /// <summary>TODO-35 (D-42): Cs-137 under Co-60's Compton continuum at use distance — Currie limits of the stripped
    /// count per reference window, the stripped trust statistic with per-pixel ratios and its calibrated thresholds, and
    /// the systematics of the stripping ratio (gain, direction, pixel). Positions and criteria are in angular resolution
    /// elements, atan(cell pitch / D) (DA-2).</summary>
    public sealed class CsUnderCoSpec
    {
        /// <summary><c>selection</c>: Cs-free null acquisitions only, Z_s recorded per configuration (thresholds are
        /// selected from these by the AB-11 rule); <c>validation</c>: everything, with the pinned thresholds.</summary>
        public string Phase { get; set; } = "";
        public AmbientGateRequest.FileReference? Thresholds { get; set; }
        /// <summary>The PR-SENS-02 gate thresholds for the raw-window counter-case (DA-6), and the gate's case name.</summary>
        public AmbientGateRequest.FileReference? RawGateThresholds { get; set; }
        public string RawGateCase { get; set; } = "";
        public string Scenario { get; set; } = "";
        /// <summary>Source–detector distance (the gate's convention): S = SourceDetectorMm − D.</summary>
        public double SourceDetectorMm { get; set; }
        public SourceLine[] CsLines { get; set; } = [];
        public SourceLine[] CoLines { get; set; } = [];
        public string CsWindow { get; set; } = "";
        /// <summary>Reference windows the stripping is computed with (counts and R); the first is the product reference
        /// (DA-7), the only one imaged.</summary>
        public string[] ReferenceWindows { get; set; } = [];
        /// <summary>Gain errors g (fractions): every declared window is also tallied as [L/(1+g), H/(1+g)].</summary>
        public double[] GainShifts { get; set; } = [];
        public CsCoScene[] Scenes { get; set; } = [];
        /// <summary>Extra Co-60 directions (elements, x and y) at which only R and Rᵢ are measured.</summary>
        public double[][] DirectionsElements { get; set; } = [];
        public double[] CoActivitiesBq { get; set; } = [];
        /// <summary>Co-60 photon H*(10) rates at the head (µSv/h) whose activity is added to the grid (ICRP 74).</summary>
        public double[] CoDoseRatesMicroSvPerHour { get; set; } = [];
        /// <summary>The front-only bound is run only at Co-60 activities up to this (DA-9).</summary>
        public double FrontOnlyMaxCoBq { get; set; }
        /// <summary>Cs-137 counts (window 1) as multiples of each condition's exact L_D (count mode).</summary>
        public double[] CountMultiples { get; set; } = [];
        public int CountRepeats { get; set; }
        /// <summary>Cs-137 counts in window 1 for the imaging acquisitions.</summary>
        public double[] ImagingCounts { get; set; } = [];
        public int ImagingRepeats { get; set; }
        public int NullRepeats { get; set; }
        /// <summary>Relative errors δR injected on Cs-free acquisitions (Rᵢ × (1 + δR)).</summary>
        public double[] DeltaR { get; set; } = [];
        public int ResidualRepeats { get; set; }
        /// <summary>Photons per line of the calibration maps (the declared precision of R, DA-4 b).</summary>
        public long CalibrationPhotons { get; set; }
    }

    public sealed class CsCoScene
    {
        public string Name { get; set; } = "";
        public double[] CsElements { get; set; } = [];
        public double[] CoElements { get; set; } = [];
    }

    /// <summary>TODO-34 (D-41): angular point response and two-source separation (<c>angres</c>). Positions and criteria in
    /// angular elements atan(cell / D); one outer seed draws one sub-element pair position and orientation (DR-3).</summary>
    public sealed class AngularResolutionSpec
    {
        public string Scenario { get; set; } = "";
        /// <summary>Source–detector distance (the gate's convention, S = SourceDetectorMm − D); 0 = the scenario's own S.</summary>
        public double SourceDetectorMm { get; set; }
        /// <summary>A declared window name; empty = every deposit (open window).</summary>
        public string Window { get; set; } = "";
        /// <summary>Half-width (degrees) of the non-cyclic search grid; 0 = one period, the fully coded field (DR-4).</summary>
        public double GridHalfDeg { get; set; }
        /// <summary>Pair-position jitter: uniform in ±JitterElements along x and y, once per seed.</summary>
        public double JitterElements { get; set; } = 0.5;
        public double[] SeparationsElements { get; set; } = [];
        /// <summary>Expected window counts of the weaker source.</summary>
        public double[] CountsPerSource { get; set; } = [];
        /// <summary>Weaker : stronger intensity (1 and 0.25).</summary>
        public double[] Ratios { get; set; } = [];
        /// <summary><c>axis</c>: the pair centred on the jittered axis point; <c>edge</c>: the outer (stronger) source at
        /// EdgeOuterElements (+ jitter) along the pair axis, the inner one Δ closer to the axis.</summary>
        public string[] Placements { get; set; } = [];
        public double EdgeOuterElements { get; set; } = 2.5;
        /// <summary>Valley thresholds v of the resolved-pair test; the first is the claimed one (DR-2).</summary>
        public double[] Valleys { get; set; } = [];
        public int Repeats { get; set; }
        /// <summary>Photons per pair / null source map.</summary>
        public long MapPhotons { get; set; }
        /// <summary>Photons of the single-source (Q1, DR-9) map at the seed's jittered position; 0 = no point section.</summary>
        public long PointPhotons { get; set; }
        /// <summary>Window counts of single-source Poisson acquisitions for the localisation by-product (DR-9).</summary>
        public double[] PointCounts { get; set; } = [];
        public int PointRepeats { get; set; }
        /// <summary>Live time (s) that turns the field (µSv/h) into counts when FieldsMicroSvPerHour is set.</summary>
        public double ExposureS { get; set; }
        public AngularMlemVariant[] Mlem { get; set; } = [];
        public AngularLadderSpec? Ladder { get; set; }
        /// <summary>Turn 3: record, per acquisition, decoder and v, the second peak's absolute prominence (negative when the
        /// shape test failed), so a significance floor can be selected and applied in aggregation.</summary>
        public bool RecordStatistics { get; set; }
    }

    /// <summary>One MLEM forward model (DR-6) and the conditions it runs on (empty subset = all).</summary>
    public sealed class AngularMlemVariant
    {
        public string Name { get; set; } = "";
        /// <summary><c>binary</c> (the engine's pixel-centre matrix), <c>area</c> (analytic pixel-area matrix with the
        /// slab's closed-cell transmission exp(−μt) at the line energy) or <c>matched</c> (columns = transported maps).</summary>
        public string Kind { get; set; } = "";
        public int PixelSubSamples { get; set; } = 8;
        /// <summary>Iteration counts recorded (snapshots of one run).</summary>
        public int[] Iterations { get; set; } = [];
        /// <summary>Photons per transported column (matched only).</summary>
        public long ColumnPhotons { get; set; }
        public double[] Counts { get; set; } = [];
        public double[] Ratios { get; set; } = [];
        public string[] Placements { get; set; } = [];
        public double MinSeparationElements { get; set; }
        /// <summary>Under a field, also decode with the instrument's background model as b_i (DR-7).</summary>
        public bool BackgroundTerm { get; set; }
    }

    /// <summary>Q3 attribution ladder (DR-8): EV-11's recipe, then one change per step.</summary>
    public sealed class AngularLadderSpec
    {
        public string LabScenario { get; set; } = "";
        public string HeadScenario { get; set; } = "";
        public double HeadSourceDetectorMm { get; set; } = 1000;
        public double[] Ev11SeparationsMm { get; set; } = [];
        public long Ev11Photons { get; set; }
        public int Ev11Iterations { get; set; }
        public double[] SeparationsElements { get; set; } = [];
        /// <summary>Counts per source of the later steps; 0 stands for EV-11's own level (half its landed photons).</summary>
        public double[] CountsPerSource { get; set; } = [];
        public long MapPhotons { get; set; }
        public int Repeats { get; set; }
        public double[] Valleys { get; set; } = [];
        public double JitterElements { get; set; } = 0.5;
        public int BinaryIterations { get; set; }
        public int AreaIterations { get; set; }
        public int AreaPixelSubSamples { get; set; } = 8;
    }
}
