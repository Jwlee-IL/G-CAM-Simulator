namespace Gcam.Studio.Core.Services;

/// <summary>What the shipped record and the module fingerprint allow for one calibration line (SD-A1…A3, A9, A10).</summary>
public enum ReadoutLineStatus
{
    /// <summary>The module matches a confirmed preset geometry whose calibration of this line succeeded on every seed:
    /// offered (experimental) at the pinned budget.</summary>
    Offered,

    /// <summary>The module matches a confirmed geometry whose calibration of this line failed on at least one seed:
    /// refused with the published, literal reason (no fallback LUT).</summary>
    Refused,

    /// <summary>The module matches no confirmed geometry: it may calibrate locally under the declared Custom policy
    /// (2 000 histories per crystal, LatticeGrowth LUT, adjoint network solve), experimental and unvalidated.</summary>
    Local,

    /// <summary>The shipped record is missing or malformed: nothing is offered (never a fallback).</summary>
    Unavailable,
}
