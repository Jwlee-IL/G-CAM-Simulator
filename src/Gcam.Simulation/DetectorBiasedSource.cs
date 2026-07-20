using Gcam.Core;

namespace Gcam.Simulation;

/// <summary>
/// Variance-reduced source: instead of emitting isotropically over 4π (where &gt;99%
/// of photons miss the small detector), it samples a uniform point on the detector
/// rectangle and aims the photon there, weighting by <c>A·cosθ/(4π·r²)</c> so the
/// scored image is an unbiased estimate of the full-4π result. Every photon lands on
/// the detector, so ~1000x fewer are needed for the same per-pixel statistics.
/// </summary>
public sealed class DetectorBiasedSource : ISource
{
    private readonly Vector3 _position;
    private readonly double _energyKeV;
    private readonly double _detHalfW;
    private readonly double _detHalfH;
    private readonly double _detPlaneZ;

    public DetectorBiasedSource(Vector3 position, double energyKeV,
                                double detHalfWidthMm, double detHalfHeightMm, double detPlaneZ)
    {
        _position = position;
        _energyKeV = energyKeV;
        _detHalfW = detHalfWidthMm;
        _detHalfH = detHalfHeightMm;
        _detPlaneZ = detPlaneZ;
    }

    public IEnumerable<Photon> Emit(IRandom rng, long count)
    {
        double area = (2.0 * _detHalfW) * (2.0 * _detHalfH);
        double norm = area / (4.0 * Math.PI);
        for (long n = 0; n < count; n++)
        {
            // Uniform point on the detector rectangle.
            double tx = (rng.NextDouble() * 2.0 - 1.0) * _detHalfW;
            double ty = (rng.NextDouble() * 2.0 - 1.0) * _detHalfH;
            var delta = new Vector3(tx, ty, _detPlaneZ) - _position;
            double r = delta.Length;
            var dir = delta * (1.0 / r);

            // pdf(area)=1/A; isotropic dΩ/4π maps to weight = A·cosθ/(4π r²).
            double cosTheta = Math.Abs(dir.Z);
            double weight = norm * cosTheta / (r * r);

            yield return new Photon
            {
                Ray = new Ray(_position, dir),
                EnergyKeV = _energyKeV,
                Weight = weight,
            };
        }
    }
}
