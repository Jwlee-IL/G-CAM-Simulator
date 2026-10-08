using Gcam.Core;

namespace Gcam.Detector;

/// <summary>How a multi-pixel (Compton-scattered) event is turned into flood-map counts.</summary>
public enum ComptonStrategy
{
    /// <summary>Per-crystal LLD/ULD energy window on EACH pixel's deposit; every pixel whose
    /// own deposit lands in the window gets a count (a common pixelated-readout method).</summary>
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
    private readonly CrystalMaterial _material;
    private readonly double _windowCenterKeV, _windowFraction;
    private readonly ComptonStrategy _strategy;
    private readonly IRandom _rng;
    private readonly double[]? _sensitivity;
    private readonly Action<double, double>? _eventSink;
    private readonly Action<int, int, double, double>? _pixelEventSink;
    private readonly Action<IReadOnlyList<(int X, int Y, double DepositKeV)>, double>? _pixelSitesSink;
    private readonly Action<IReadOnlyList<InteractionSite>, double>? _interactionSink;
    private readonly FrontEndModel? _frontEnd;
    private readonly IRandom? _frontEndRng;
    private readonly EntranceAbsorber? _entrance;   // passive window/encapsulation in front (null = none)
    private readonly EntranceAbsorber? _backing;    // scatterer behind the crystal (null = none) -> backscatter
    private readonly double _reflectorGap;          // dead reflector/kerf gap between crystals (mm; 0 = 100% fill)
    private readonly double _crosstalk;             // EFFECTIVE optical light-leak fraction (contact coupling × reflector transmission)
    private const double CrosstalkLambdaMm = 0.04;  // reflector optical attenuation length — crosstalk ~ exp(-gap/λ)

    public double PlaneZ { get; }
    private readonly bool _allFaces;

    public ComptonCrystalDetector(int pixelsX, int pixelsY, double pixelPitchMm,
        double windowCenterKeV, double windowFraction, ComptonStrategy strategy, IRandom rng,
        double? muAt662PerMm = null, double crystalDepthMm = 10.0, double planeZ = 0.0,
        double[]? sensitivity = null, Action<double, double>? eventSink = null,
        FrontEndModel? frontEnd = null, IRandom? frontEndRng = null,
        EntranceAbsorber? entranceAbsorber = null, EntranceAbsorber? backingScatterer = null,
        double reflectorGapMm = 0.0, double opticalCrosstalk = 0.0, CrystalMaterial? material = null,
        Action<int, int, double, double>? pixelEventSink = null,
        Action<IReadOnlyList<(int X, int Y, double DepositKeV)>, double>? pixelSitesSink = null,
        bool entryThroughAllFaces = false, Action<IReadOnlyList<InteractionSite>, double>? interactionSink = null)
    {
        _image = new DetectorImage(pixelsX, pixelsY);
        _pitch = pixelPitchMm;
        _halfWidth = pixelsX * pixelPitchMm / 2.0;
        _halfHeight = pixelsY * pixelPitchMm / 2.0;
        _depth = crystalDepthMm;
        // The material's own μ(662) unless the config pins one (presets / older configs anchor it explicitly).
        _material = material ?? CrystalMaterial.Gagg;
        _muAt662 = muAt662PerMm ?? _material.MuPerMm(CrystalMaterial.ReferenceKeV);
        _windowCenterKeV = windowCenterKeV;
        _windowFraction = windowFraction;
        _strategy = strategy;
        _rng = rng;
        _sensitivity = sensitivity;
        _eventSink = eventSink;
        _pixelEventSink = pixelEventSink;
        _pixelSitesSink = pixelSitesSink;
        _interactionSink = interactionSink;
        _frontEnd = frontEnd;
        _frontEndRng = frontEndRng;
        _entrance = entranceAbsorber;
        _backing = backingScatterer;
        _reflectorGap = reflectorGapMm;
        // Optical crosstalk is COUPLED to the reflector thickness: light leaking to a neighbour must penetrate the
        // reflector, whose transmission falls ~exp(-gap/λ). So opticalCrosstalk is the contact coupling (bare
        // crystals, gap 0) and the EFFECTIVE crosstalk drops as the reflector widens — the isolation vs dead-area
        // trade-off in a single reflector parameter. λ ≈ 40 µm (a decent diffuse/ESR reflector).
        _crosstalk = opticalCrosstalk * Math.Exp(-reflectorGapMm / CrosstalkLambdaMm);
        PlaneZ = planeZ;
        _allFaces = entryThroughAllFaces;
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

    // True if (x,y) on the detector face lands in the reflector / saw-kerf gap between crystals (the dead region).
    // Active crystal footprint is (pitch − gap), centred in each pitch cell, so the outer gap/2 border is dead.
    private bool InReflectorGap(double x, double y)
    {
        double half = _reflectorGap * 0.5;
        double lx = (x + _halfWidth) % _pitch;
        double ly = (y + _halfHeight) % _pitch;
        return lx < half || lx > _pitch - half || ly < half || ly > _pitch - half;
    }

    private int PixelIndex(double coord, double half) => (int)((coord + half) / _pitch);
    private double PixelCenter(int i, double half) => (i + 0.5) * _pitch - half;

    // aggregated deposits for one photon (small: a Compton cascade rarely exceeds ~4 sites)
    private readonly List<(int px, int py, double e)> _sites = new(8);
    // The same history's individual interactions BEFORE any optical processing (XYZ, time, energy), recorded only when an
    // interaction sink is attached. Pure bookkeeping: no random number is drawn for it, so the transport is unchanged.
    private readonly List<InteractionSite> _raw = new(8);
    private const double LightSpeedMmPerNs = 299.792458;

    public bool Score(Photon photon)
    {
        Vector3 entry;
        if (_allFaces)
        {
            if (!BoxEntry(photon.Ray, out entry)) return false;
        }
        else
        {
            double dz = photon.Direction.Z;
            if (dz == 0.0) return false;
            double t0 = (PlaneZ - photon.Position.Z) / dz;
            if (t0 <= 0.0) return false;
            entry = photon.Ray.At(t0);
            if (entry.X < -_halfWidth || entry.X >= _halfWidth ||
                entry.Y < -_halfHeight || entry.Y >= _halfHeight) return false;
        }
        if (_reflectorGap > 0.0 && InReflectorGap(entry.X, entry.Y)) return false;

        // Entrance material (source encapsulation + front housing/window): the photon may pass through, be
        // photo-absorbed (removed here), or Compton-scatter to a lower energy + new direction. Forward small-angle
        // scatters continue into the crystal and deposit just BELOW full energy — the physical low-energy tail that
        // fills the Compton-edge-to-photopeak valley. Back-scatters head away from the crystal and are lost.
        double e = photon.EnergyKeV;
        var dir = photon.Direction;
        if (_entrance is not null)
        {
            var (absorbed, eScat, dirScat) = _entrance.Interact(e, dir, _rng);
            if (absorbed) return false;
            e = eScat; dir = dirScat;
            if (dir.Z >= 0.0) return false;              // scattered back toward the source — misses the crystal
        }

        // ---- transport the Compton cascade through the crystal slab z in [-depth, 0] ----
        _sites.Clear();
        _raw.Clear();
        double flightNs = 0.0;
        var pos = _allFaces ? entry : new Vector3(entry.X, entry.Y, PlaneZ);
        for (int step = 0; step < 32 && e > 1.0; step++)
        {
            double mu = _muAt662 * _material.MuRel(e);
            double s = -Math.Log(1.0 - _rng.NextDouble()) / mu;
            var prev = pos;
            pos += dir * s;
            flightNs += s / LightSpeedMmPerNs;
            if (pos.Z > PlaneZ ||
                pos.X < -_halfWidth || pos.X >= _halfWidth ||
                pos.Y < -_halfHeight || pos.Y >= _halfHeight)
                break;                                        // out the front / sides -> lost
            if (pos.Z < PlaneZ - _depth)                      // exited the BACK face
            {
                // Backing (SiPM / PCB / housing): the through-going photon may Compton back-scatter off it and
                // re-enter — a ~180° scatter of 662 keV returns ~184 keV that re-absorbs here (backscatter peak),
                // and shallower back-scatters add to the sub-photopeak fill. Reuses Klein-Nishina; no hand tail.
                if (_backing is not null && e > 1.0)
                {
                    // Re-enter at the ACTUAL back-face crossing (not the overshot free-flight endpoint).
                    double backZ = PlaneZ - _depth;
                    double tb = (backZ - prev.Z) / dir.Z;
                    double cx = prev.X + dir.X * tb, cy = prev.Y + dir.Y * tb;
                    var (absBack, eBack, dirBack) = _backing.Interact(e, dir, _rng);
                    if (!absBack && dirBack.Z > 0.0 &&
                        cx >= -_halfWidth && cx < _halfWidth && cy >= -_halfHeight && cy < _halfHeight &&
                        !(_reflectorGap > 0.0 && InReflectorGap(cx, cy)) &&        // re-enter a live crystal, not a gap
                        _rng.NextDouble() < _backing.Transmit(eBack))             // scattered photon must escape the backing
                    {
                        // Time to the back face and back (the backing's own thickness is not tracked: sub-ps).
                        flightNs += (tb - s) / LightSpeedMmPerNs;
                        pos = new Vector3(cx, cy, backZ);                          // re-enter at the crossing, heading up
                        dir = dirBack; e = eBack;
                        continue;
                    }
                }
                break;                                        // absorbed in / passed through the backing -> lost
            }

            int px = PixelIndex(pos.X, _halfWidth);
            int py = PixelIndex(pos.Y, _halfHeight);
            if (_rng.NextDouble() < _material.PhotoFraction(e))
            {
                AddSite(px, py, e);                           // photoelectric: full absorption
                if (_interactionSink is not null) _raw.Add(new InteractionSite(pos.X, pos.Y, pos.Z, flightNs, e, px, py));
                e = 0.0; break;
            }
            var (dep, newE, newDir) = ComptonModel.Scatter(e, dir, _rng);
            AddSite(px, py, dep);
            if (_interactionSink is not null) _raw.Add(new InteractionSite(pos.X, pos.Y, pos.Z, flightNs, dep, px, py));
            dir = newDir; e = newE;
        }
        if (_sites.Count == 0) return false;

        // Entrance attenuation is now handled physically above (pass / photo-absorb / scatter), so the surviving
        // photon just carries its importance weight.
        double weight = photon.Weight;

        // The analog scintillation pulse the SiPM/ADC would see is proportional to the TOTAL energy
        // deposited in the crystal (summed over the Compton cascade sites), BEFORE any energy window —
        // this is the physical pulse height the RTL shaper works on, so the event sink (used to build the
        // MC→RTL event stream) records it for every scored event, photopeak and Compton-continuum alike.
        // The photon's importance-sampling weight goes with it, so a directional-biased run can be
        // resampled back to the physical detected-event spectrum (unweighted would over-represent the
        // biased proposal at positions/angles where deposit/escape probability differs).
        double pulseDeposit = 0.0;
        if (_eventSink is not null || _pixelEventSink is not null || _pixelSitesSink is not null || _interactionSink is not null)
        {
            foreach (var (_, _, dep) in _sites) pulseDeposit += dep;
            _eventSink?.Invoke(pulseDeposit, weight);   // TRUE total light before the spread
        }

        // Optical crosstalk: an imperfect reflector lets a fraction of each interaction's scintillation LIGHT leak
        // to the 4 nearest crystals. The main channel keeps (1−leak); each neighbour gets leak/4 (light past the
        // array edge is lost). A per-crystal windowed readout then sees reduced photopeak light in the main channel
        // (events fall out of its LLD/ULD window → efficiency loss) plus low neighbour hits — which is exactly why
        // the reflector isolation matters. The leak VARIES per event (edge interactions share more than centred
        // ones), modelled as uniform [0, 2·fraction] so the photopeak efficiency rolls off gradually rather than
        // cliff-dropping when the mean crosses the window. Total light is conserved → the total-energy sink is intact.
        if (_crosstalk > 0.0) ApplyOpticalCrosstalk(Math.Min(0.95, 2.0 * _crosstalk * _rng.NextDouble()));

        // One list-mode pulse per history, located by largest collected deposit (Argmax).
        // The old energy-only sink and all transport RNG calls remain unchanged.
        if (_pixelEventSink is not null)
        {
            double best = -1.0;
            int bx = 0, by = 0;
            foreach (var (px, py, dep) in _sites)
            {
                if (dep > best) { best = dep; bx = px; by = py; }
            }
            _pixelEventSink(bx, by, pulseDeposit, weight);
        }
        // The same history's interaction sites (after the light spread, as the Argmax above sees them) and its total
        // deposit, for a caller that merges several photons of ONE decay into one pulse (ListModeSource). The list is
        // reused by the next Score: copy it before returning.
        _pixelSitesSink?.Invoke(_sites, pulseDeposit);
        // The pre-optical interactions of this history (independent of the light spread above). Reused buffer: copy it.
        _interactionSink?.Invoke(_raw, weight);
        Deposit(weight);
        return true;
    }

    private bool BoxEntry(Ray ray, out Vector3 entry)
    {
        double near = 0, far = double.PositiveInfinity;
        bool Slab(double p, double d, double lo, double hi)
        {
            if (d == 0) return p >= lo && p <= hi;
            double a = (lo - p) / d, b = (hi - p) / d;
            if (a > b) (a, b) = (b, a);
            near = Math.Max(near, a); far = Math.Min(far, b);
            return far > near;
        }
        bool hit = Slab(ray.Origin.X, ray.Direction.X, -_halfWidth, _halfWidth)
            && Slab(ray.Origin.Y, ray.Direction.Y, -_halfHeight, _halfHeight)
            && Slab(ray.Origin.Z, ray.Direction.Z, PlaneZ - _depth, PlaneZ);
        entry = hit ? ray.At(near) : default;
        return hit;
    }

    private void ApplyOpticalCrosstalk(double c)
    {
        var orig = _sites.ToArray();
        _sites.Clear();
        foreach (var (px, py, e) in orig) AddSite(px, py, (1.0 - c) * e);
        foreach (var (px, py, e) in orig)
        {
            double share = c * 0.25 * e;
            Leak(px - 1, py, share); Leak(px + 1, py, share);
            Leak(px, py - 1, share); Leak(px, py + 1, share);
        }
    }

    private void Leak(int nx, int ny, double share)
    {
        if (nx >= 0 && nx < _image.Width && ny >= 0 && ny < _image.Height) AddSite(nx, ny, share);
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
