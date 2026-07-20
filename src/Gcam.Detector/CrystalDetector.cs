using Gcam.Core;

namespace Gcam.Detector;

/// <summary>
/// Pixelated scintillator crystal array (the old rig read out a 12×12 flood map),
/// sitting on the plane z = <see cref="PlaneZ"/> and centered on the optical axis.
/// </summary>
public sealed class CrystalDetector : IDetector
{
    private readonly DetectorImage _image;
    private readonly double _pixelPitchMm;
    private readonly double _halfWidth;
    private readonly double _halfHeight;
    private readonly double[]? _sensitivity;   // per-pixel non-uniformity (null = uniform)
    private readonly double _crystalMuPerMm;   // stopping power (0 = ideal, detect all)
    private readonly double _crystalDepthMm;
    private readonly EntranceAbsorber? _entrance;   // passive window/encapsulation in front (null = none)

    public double PlaneZ { get; }

    public CrystalDetector(int pixelsX, int pixelsY, double pixelPitchMm, double planeZ = 0.0,
                           double[]? sensitivity = null, double crystalMuPerMm = 0.0, double crystalDepthMm = 10.0,
                           EntranceAbsorber? entranceAbsorber = null)
    {
        _image = new DetectorImage(pixelsX, pixelsY);
        _pixelPitchMm = pixelPitchMm;
        PlaneZ = planeZ;
        _halfWidth = pixelsX * pixelPitchMm / 2.0;
        _halfHeight = pixelsY * pixelPitchMm / 2.0;
        _sensitivity = sensitivity;
        _crystalMuPerMm = crystalMuPerMm;
        _crystalDepthMm = crystalDepthMm;
        _entrance = entranceAbsorber;
    }

    public bool Score(Photon photon)
    {
        // Straight-line propagation to the detector plane.
        double dz = photon.Direction.Z;
        if (dz == 0.0) return false;
        double t = (PlaneZ - photon.Position.Z) / dz;
        if (t <= 0.0) return false;

        var hit = photon.Ray.At(t);
        double u = hit.X + _halfWidth;
        double v = hit.Y + _halfHeight;
        if (u < 0.0 || v < 0.0 || u >= 2.0 * _halfWidth || v >= 2.0 * _halfHeight)
            return false;                               // misses the detector

        int px = (int)(u / _pixelPitchMm);
        int py = (int)(v / _pixelPitchMm);

        double s = _sensitivity is null ? 1.0 : _sensitivity[py * _image.Width + px];

        // Stopping power: probability the photon interacts within the crystal depth
        // (slant path = depth / |cos θ|). 0 mu = ideal (detect all).
        double absorb = 1.0;
        if (_crystalMuPerMm > 0.0)
        {
            double absDz = Math.Abs(photon.Direction.Z);
            double path = absDz > 0.0 ? _crystalDepthMm / absDz : _crystalDepthMm;
            absorb = 1.0 - Math.Exp(-_crystalMuPerMm * path);
        }

        double trans = _entrance is null ? 1.0 : _entrance.Transmit(photon.EnergyKeV);
        _image.Add(px, py, photon.Weight * s * absorb * trans);
        // Returns true for any geometric hit; absorption/sensitivity live in the scored
        // weight. So the runner's raw PhotonsDetected over-counts non-ideal crystals —
        // use DetectedWeight (sum of the image) for the physical detected count.
        return true;
    }

    public DetectorImage Readout() => _image;

    public void Reset()
    {
        for (int y = 0; y < _image.Height; y++)
            for (int x = 0; x < _image.Width; x++)
                _image[x, y] = 0.0;
    }
}
