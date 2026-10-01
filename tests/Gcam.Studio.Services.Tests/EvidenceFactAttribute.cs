namespace Gcam.Studio.Services.Tests;

/// <summary>Long-running numerical measurements are separate from the default regression suite.</summary>
public sealed class EvidenceFactAttribute : FactAttribute
{
    public EvidenceFactAttribute()
    {
        if (Environment.GetEnvironmentVariable("GCAM_EVIDENCE_TESTS") != "1")
            Skip = "Set GCAM_EVIDENCE_TESTS=1 to run numerical evidence measurements.";
    }
}
