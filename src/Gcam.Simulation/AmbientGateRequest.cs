using Gcam.Configuration;

namespace Gcam.Simulation;

/// <summary>One seed of the AB-7 gate study / AB-9 count-gate re-measurement (<see cref="AmbientGateStudy"/>), as a
/// declarative request. The seed driver clones it, replaces <see cref="Seed"/> and <see cref="RepoRoot"/>; every other
/// number is declared in the committed request, including the activities (from <see cref="PilotRatePerBq"/>).</summary>
public sealed class AmbientGateRequest
{
    public int Seed { get; set; }
    /// <summary>Absolute repository root (the driver sets it); every path below is relative to it.</summary>
    public string RepoRoot { get; set; } = ".";
    /// <summary><c>pilot</c> (rates and timing only), <c>selection</c> (null acquisitions only, for the threshold) or
    /// <c>validation</c> (null and source acquisitions, judged with the thresholds selected on other seeds).</summary>
    public string Phase { get; set; } = "selection";
    public FileReference Spectrum { get; set; } = new();
    public FileReference? Thresholds { get; set; }
    public long AmbientHistories { get; set; }
    public long CalibrationHistories { get; set; }
    public long SourcePhotons { get; set; }
    public double[] FieldsMicroSvPerHour { get; set; } = [];
    public AmbientGeometry[] Bounds { get; set; } = [];
    public WindowSpec[] Windows { get; set; } = [];
    public int NullRepeats { get; set; }
    public int SourceRepeats { get; set; }
    /// <summary>Gross-failure radius at the source plane (mm), EV-07's 3 mm.</summary>
    public double FailThresholdMm { get; set; } = 3.0;
    /// <summary>Expected net source counts per acquisition; the activity is level / (exposure × pilot rate per Bq).</summary>
    public double[] SourceLevels { get; set; } = [];
    public double DefaultActivityBq { get; set; }
    public CaseSpec[] Cases { get; set; } = [];
    /// <summary>Source rate per Bq (counts/s) from the pilot seed, key <c>case|position|window</c>.</summary>
    public Dictionary<string, double> PilotRatePerBq { get; set; } = [];

    public sealed class FileReference
    {
        public string File { get; set; } = "";
        public string Sha256 { get; set; } = "";
    }

    public sealed class WindowSpec
    {
        public string Name { get; set; } = "";
        public double? LowKeV { get; set; }
        public double? HighKeV { get; set; }
    }

    public sealed class CaseSpec
    {
        public string Name { get; set; } = "";
        public string Scenario { get; set; } = "";
        /// <summary>Source-plane distance from the detector, D + S (mm).</summary>
        public double SourceDetectorMm { get; set; }
        public PositionSpec[] Positions { get; set; } = [];
        public double[] ExposuresS { get; set; } = [];
    }

    public sealed class PositionSpec
    {
        public string Name { get; set; } = "";
        public double XMm { get; set; }
        public double YMm { get; set; }
    }
}
