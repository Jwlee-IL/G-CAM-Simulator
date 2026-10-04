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
    /// <summary><c>antimask</c> (EV-12), <c>fov</c> (EV-02), <c>separation</c> (EV-15) or <c>sweep</c> (EV-01, AB-12).</summary>
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
}
