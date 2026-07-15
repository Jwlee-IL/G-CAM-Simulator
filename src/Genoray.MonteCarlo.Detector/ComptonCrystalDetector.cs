using Genoray.MonteCarlo.Core;

namespace Genoray.MonteCarlo.Detector;

/// <summary>How a multi-pixel (Compton-scattered) event is turned into flood-map counts.</summary>
public enum ComptonStrategy
{
    /// <summary>Per-crystal LLD/ULD energy window on EACH pixel's deposit; every pixel whose
    /// own deposit lands in the window gets a count (the old rig's method).</summary>
    PerPixelWindow,
    /// <summary>Accept only single-pixel events whose deposit is in the window (reject any
    /// multi-pixel event outright).</summary>
    AntiCoincidence,
    /// <summary>If the TOTAL deposited energy is in the window, put one count at the
    /// largest-deposit pixel.</summary>
    Argmax,
    /// <summary>If the TOTAL deposited energy is in the window, put one count at the
    /// energy-weighted centroid pixel (Anger logic).</summary>
    Centroid,
}

/// <summary>
/// Pixelated scintillator that models Compton scattering INSIDE the crystal: a photon may
/// deposit its energy across several pixels (Compton electron at A, the scattered photon
/// re-absorbed at B, ...), or the scattered photon may escape. How those multi-pixel deposits
/// become a flood-map count is set by <see cref="ComptonStrategy"/>. The energy window is
/// centred on <see cref="_windowCenterKeV"/> (the analysis photopeak — not necessarily the
/// photon's line energy, so a higher isotope's downscatter into a lower window is captured).
/// </summary>
public sealed class ComptonCrystalDetector : IDetector
{
    private readonly DetectorImage _image;
    private readonly double _pitch, _halfWidth, _halfHeight, _depth, _muAt662;
    private readonly double _windowCenterKeV, _windowFraction;
    private readonly ComptonStrategy _strategy;
    private readonly IRandom _rng;
    private readonly double[]? _sensitivity;
    private readonly Action<double, double>? _eventSink;
    private readonly FrontEndModel? _frontEnd;
    private readonly IRandom? _frontEndRng;
    private readonly EntranceAbsorber? _entrance;   // passive window/encapsulation in front (null = none)

    public double PlaneZ { get; }

    public ComptonCrystalDetector(int pixelsX, int pixelsY, double pixelPitchMm,
        double windowCenterKeV, double windowFraction, ComptonStrategy strategy, IRandom rng,
        double muAt662PerMm = 0.09, double crystalDepthMm = 10.0, double planeZ = 0.0,
        double[]? sensitivity = null, Action<double, double>? eventSink = null,
        FrontEndModel? frontEnd = null, IRandom? frontEndRng = null,
        EntranceAbsorber? entranceAbsorber = null)
    {
        _image = new DetectorImage(pixelsX, pixelsY);
        _pitch = pixelPitchMm;
        _halfWidth = pixelsX * pixelPitchMm / 2.0;
        _halfHeight = pixelsY * pixelPitchMm / 2.0;
        _depth = crystalDepthMm;
        _muAt662 = muAt662PerMm;
        _windowCenterKeV = windowCenterKeV;
        _windowFraction = windowFraction;
        _strategy = strategy;
        _rng = rng;
        _sensitivity = sensitivity;
        _eventSink = eventSink;
        _frontEnd = frontEnd;
        _frontEndRng = frontEndRng;
        _entrance = entranceAbsorber;
        PlaneZ = planeZ;
    }

    private bool InWindow(double e)
        => e >= _windowCenterKeV * (1.0 - _windowFraction)
        && e <= _windowCenterKeV * (1.0 + _windowFraction);

    // Measured energy for the WINDOW check: smear the true deposit by the physical front-end resolution
    // (energy-dependent 1/√E FWHM) when a model is present, else the true energy. Makes the energy
    // discrimination realistic — a 662 photopeak event can fall out of a tight window, and continuum events
    // near the edge fluctuate in/out — instead of every deposit being a perfect line. Uses a SEPARATE RNG
    // (not the cascade _rng) so enabling the front-end doesn't perturb the Compton cascade stream — the
    // strategy comparison (ComptonStudy) still replays identical cascades across strategies.
    private double Measured(double e) => _frontEnd is null ? e : _frontEnd.Measure(e, _frontEndRng ?? _rng);

    private int PixelIndex(double coord, double half) => (int)((coord + half) / _pitch);
    private double PixelCenter(int i, double half) => (i + 0.5) * _pitch - half;

    // aggregated deposits for one photon (small: a Compton cascade rarely exceeds ~4 sites)
    private readonly List<(int px, int py, double e)> _sites = new(8);

    public bool Score(Photon photon)
    {
        double dz = photon.Direction.Z;
        if (dz == 0.0) return false;
        double t0 = (PlaneZ - photon.Position.Z) / dz;
        if (t0 <= 0.0) return false;
        var entry = photon.Ray.At(t0);
        if (entry.X < -_halfWidth || entry.X >= _halfWidth ||
            entry.Y < -_halfHeight || entry.Y >= _halfHeight) return false;

        // ---- transport the Compton cascade through the crystal slab z in [-depth, 0] ----
        _sites.Clear();
        var pos = new Vector3(entry.X, entry.Y, PlaneZ);
        var dir = photon.Direction;
        double e = photon.EnergyKeV;
        for (int step = 0; step < 32 && e > 1.0; step++)
        {
            double mu = _muAt662 * ComptonModel.MuRel(e);
            double s = -Math.Log(1.0 - _rng.NextDouble()) / mu;
            pos += dir * s;
            if (pos.Z > PlaneZ || pos.Z < PlaneZ - _depth ||
                pos.X < -_halfWidth || pos.X >= _halfWidth ||
                pos.Y < -_halfHeight || pos.Y >= _halfHeight)
                break;                                        // escaped -> remaining energy lost

            int px = PixelIndex(pos.X, _halfWidth);
            int py = PixelIndex(pos.Y, _halfHeight);
            if (_rng.NextDouble() < ComptonModel.PhotoFraction(e))
            {
                AddSite(px, py, e); e = 0.0; break;           // photoelectric: full absorption
            }
            var (dep, newE, newDir) = ComptonModel.Scatter(e, dir, _rng);
            AddSite(px, py, dep);
            dir = newDir; e = newE;
        }
        if (_sites.Count == 0) return false;

        // Passive entrance window / source encapsulation: attenuates the INCOMING photon by its (pre-interaction)
        // energy, applied as a survival weight so soft X-ray lines are suppressed the way an encapsulated source's
        // are. Deposit energies are unchanged — only how often the event counts.
        double weight = photon.Weight * (_entrance is null ? 1.0 : _entrance.Transmit(photon.EnergyKeV));

        // The analog scintillation pulse the SiPM/ADC would see is proportional to the TOTAL energy
        // deposited in the crystal (summed over the Compton cascade sites), BEFORE any energy window —
        // this is the physical pulse height the RTL shaper works on, so the event sink (used to build the
        // MC→RTL event stream) records it for every scored event, photopeak and Compton-continuum alike.
        // The photon's importance-sampling weight goes with it, so a directional-biased run can be
        // resampled back to the physical detected-event spectrum (unweighted would over-represent the
        // biased proposal at positions/angles where deposit/escape probability differs).
        if (_eventSink is not null)
        {
            double total = 0.0;
            foreach (var (_, _, dep) in _sites) total += dep;
            _eventSink(total, weight);
        }

        Deposit(weight);
        return true;
    }

    private void AddSite(int px, int py, double dep)
    {
        for (int i = 0; i < _sites.Count; i++)
            if (_sites[i].px == px && _sites[i].py == py)
            {
                _sites[i] = (px, py, _sites[i].e + dep);
                return;
            }
        _sites.Add((px, py, dep));
    }

    private double Gain(int px, int py)
        => _sensitivity is null ? 1.0 : _sensitivity[py * _image.Width + px];

    private void Deposit(double weight)
    {
        switch (_strategy)
        {
            case ComptonStrategy.PerPixelWindow:
                foreach (var (px, py, e) in _sites)
                    if (InWindow(Measured(e) * Gain(px, py))) _image.Add(px, py, weight);
                break;

            case ComptonStrategy.AntiCoincidence:
                if (_sites.Count == 1 && InWindow(Measured(_sites[0].e) * Gain(_sites[0].px, _sites[0].py)))
                    _image.Add(_sites[0].px, _sites[0].py, weight);
                break;

            case ComptonStrategy.Argmax:
            {
                double total = 0.0, best = -1.0; int bx = 0, by = 0;
                foreach (var (px, py, e) in _sites)
                {
                    total += e;
                    if (e > best) { best = e; bx = px; by = py; }
                }
                if (InWindow(Measured(total))) _image.Add(bx, by, weight);
                break;
            }

            case ComptonStrategy.Centroid:
            {
                double total = 0.0, cx = 0.0, cy = 0.0;
                foreach (var (px, py, e) in _sites)
                {
                    total += e;
                    cx += e * PixelCenter(px, _halfWidth);
                    cy += e * PixelCenter(py, _halfHeight);
                }
                if (total > 0 && InWindow(Measured(total)))
                {
                    int px = PixelIndex(cx / total, _halfWidth);
                    int py = PixelIndex(cy / total, _halfHeight);
                    if (px >= 0 && px < _image.Width && py >= 0 && py < _image.Height)
                        _image.Add(px, py, weight);
                }
                break;
            }
        }
    }

    public DetectorImage Readout() => _image;

    public void Reset()
    {
        for (int y = 0; y < _image.Height; y++)
            for (int x = 0; x < _image.Width; x++)
                _image[x, y] = 0.0;
    }
}
