namespace Gcam.Core;

/// <summary>
/// How the decoder refines the reconstruction peak BELOW one recon-grid cell. The bare argmax quantizes the
/// source estimate to <c>ReconStepMm</c>, a precision floor of ≈ step/√12 independent of counts; interpolating
/// the correlation-peak SHAPE around the argmax recovers a fractional position and beats that floor.
/// </summary>
public enum SubCellMethod
{
    /// <summary>No interpolation — report the integer argmax cell (the original behaviour).</summary>
    None,

    /// <summary>3-point separable parabola through the peak and its two neighbours. Slightly biased toward the
    /// integer cell for a triangular peak (returns u/(2(1−|u|)) for a tent of true offset u).</summary>
    Parabolic,

    /// <summary>3-point separable TENT (triangle) estimator — the matched model for a MURA autocorrelation core,
    /// exact for an ideal tent from the three samples. The default: the coded-aperture point response near the
    /// peak is the autocorrelation of the projected mask cell, i.e. a tent, not a Gaussian bump.</summary>
    Tent,

    /// <summary>3-point log-parabola (Gaussian) estimator. Only defined where all three samples are positive
    /// (falls back to <see cref="Parabolic"/> near the ±1 decoding array's negative sidelobes); not the natural
    /// model for a signed-correlation surface, kept for comparison.</summary>
    Gaussian,
}
