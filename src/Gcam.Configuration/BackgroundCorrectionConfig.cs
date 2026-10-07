namespace Gcam.Configuration;

/// <summary>Opt-in decoding knowledge, separate from the generating ambient field. Calibration is supplied explicitly
/// to the calibrated factory overload; the ordinary single-run/Studio entry points do not load it implicitly.</summary>
public sealed class BackgroundCorrectionConfig
{
    public BackgroundCorrectionMode Mode { get; set; } = BackgroundCorrectionMode.JointMlem;
}
