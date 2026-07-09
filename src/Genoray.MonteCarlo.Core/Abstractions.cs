namespace Genoray.MonteCarlo.Core;

/// <summary>Emits photons (e.g. an isotropic Cs-137 point source).</summary>
public interface ISource
{
    IEnumerable<Photon> Emit(IRandom rng, long count);
}

/// <summary>
/// A coded-aperture mask. Decides whether an incoming photon reaches the
/// detector side (open cell), is blocked, or leaks through the absorber.
/// </summary>
public interface IMask
{
    /// <summary>Returns true if the photon passes the mask toward the detector.</summary>
    bool Transmit(Ray ray, double energyKeV, IRandom rng);
}

/// <summary>Accumulates the response of the pixelated crystal array.</summary>
public interface IDetector
{
    /// <summary>Records the photon if it lands on the array. Returns true if it did.</summary>
    bool Score(Photon photon);
    DetectorImage Readout();
    void Reset();
}

/// <summary>Reconstructs / localizes the source from a detector image.</summary>
public interface IDecoder
{
    DecodeResult Decode(DetectorImage image);
}
