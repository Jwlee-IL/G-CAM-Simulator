using Gcam.Core;

namespace Gcam.Simulation;

/// <summary>AB-4 collided soil/air generator: a uniform, laterally infinite soil half-space (z &lt; 0) emitting a chain's
/// evaluated lines isotropically, infinite uniform dry air above, fluence and air kerma scored at height h.
/// Internally +Z is the upward vertical; the output zenith cosine is the propagation direction · up, which the
/// spectrum file states as world +y (the turn-5 EnergyZenithTable convention, ground below).
///
/// Physics: photoelectric absorption (ends the history; K fluorescence not emitted, bounded instead), incoherent
/// scattering with XCOM's bound-electron cross section and free-electron Klein–Nishina angles/energies, pair
/// production (nuclear + electron field) followed by two back-to-back 511-keV photons emitted isotropically at the
/// interaction point (positron range and annihilation in flight neglected). Coherent scattering is treated as no
/// interaction: it keeps the photon energy, so its omission moves fluence only between nearby directions; its size is
/// reported as the uncollided-fluence difference with and without the coherent coefficient. Not modelled: β
/// bremsstrahlung and secondary-electron bremsstrahlung, Doppler broadening, atmosphere density gradient.
///
/// Estimator: laterally invariant geometry, so the point fluence equals the plane average; a photon history carries
/// the column-integrated source weight W = S e^(λd)/(π λ) photons/(cm² s) per Bq/kg (S = ρ_soil/1000 × yield, π the
/// line-selection probability ∝ yield·E/μ_soil), and the track length L inside the slab h ± δ scores W L / (2δ).
/// Everything else is analog, so the estimate is unbiased for the slab-averaged fluence.</summary>
public sealed class SoilAirTransport
{
    /// <summary>keV/(g s) → nGy/h: 1.602176634e-16 J/keV × 1e3 g/kg × 1e9 nGy/Gy × 3600 s/h.</summary>
    public const double KermaToNGyPerHour = 1.602176634e-16 * 1e3 * 1e9 * 3600;
    private const double AnnihilationKeV = KleinNishina.ElectronRestEnergyKeV;
    private const double PairThresholdKeV = 2 * KleinNishina.ElectronRestEnergyKeV;

    private readonly TerrestrialChain _chain;
    private readonly SoilAirMaterials _m;
    private readonly SoilAirTransportOptions _o;
    private readonly double[] _cumulative, _sourceWeight, _lambda, _energy;
    private readonly double _lo, _hi, _scale;
    private readonly FluorescenceBound _soilBound, _airBound;

    public SoilAirTransport(TerrestrialChain chain, SoilAirMaterials materials, SoilAirTransportOptions options)
    {
        ArgumentNullException.ThrowIfNull(chain); ArgumentNullException.ThrowIfNull(materials); ArgumentNullException.ThrowIfNull(options);
        options.Validate();
        _chain = chain; _m = materials; _o = options;
        int n = chain.Lines.Length;
        if (n == 0) throw new ArgumentException("The chain has no lines.");
        _energy = chain.Lines.Select(l => l.EnergyKeV).ToArray();
        if (_energy.Any(e => e < options.TransportCutoffKeV || e > materials.Soil.MaxEnergyKeV))
            throw new ArgumentException("A line lies outside the transport data range.");
        double soilDensity = materials.Soil.DensityGPerCm3;
        var importance = new double[n];
        _lambda = new double[n];
        _sourceWeight = new double[n];
        for (int i = 0; i < n; i++)
        {
            double mu = materials.Soil.LinearMuPerCm(_energy[i]);
            _lambda[i] = options.DepthSamplingFactor * mu;
            // Selection ∝ yield × energy / μ_soil: the line's emitted energy that can reach the surface.
            importance[i] = chain.Lines[i].YieldPerChainDecay * _energy[i] / mu;
        }
        double total = importance.Sum();
        _cumulative = new double[n];
        double running = 0;
        for (int i = 0; i < n; i++)
        {
            double pi = importance[i] / total;
            running += pi;
            _cumulative[i] = running;
            // Photons per cm³ s per Bq/kg: density (g/cm³) / 1000 (g/kg) × yield; divided by the selection probability.
            _sourceWeight[i] = soilDensity / 1000 * chain.Lines[i].YieldPerChainDecay / pi;
        }
        _cumulative[^1] = 1;
        _lo = options.HeightCm - options.SlabHalfWidthCm;
        _hi = options.HeightCm + options.SlabHalfWidthCm;
        _scale = 1 / (2 * options.SlabHalfWidthCm);
        _soilBound = new FluorescenceBound(materials, options, inSoil: true);
        _airBound = new FluorescenceBound(materials, options, inSoil: false);
    }

    private struct Photon
    {
        public double Z, Energy;
        public Vector3 Direction;
        public int Kind; // 0 uncollided source line, 1 uncollided annihilation, 2 scattered
    }

    /// <summary>Run <paramref name="histories"/> source histories with one random stream (reproducible per seed).</summary>
    public SoilAirTally Run(long histories, IRandom random, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(random);
        if (histories < 2) throw new ArgumentOutOfRangeException(nameof(histories));
        int zb = _o.ZenithBins, lines = _energy.Length, eb = _o.ContinuumEdgesKeV.Length - 1;
        var uncollided = new double[lines * zb];
        var uncollidedSq = new double[lines * zb];
        var uncollidedCount = new long[lines * zb];
        var annihilation = new double[zb];
        var continuum = new double[eb * zb];
        double below = 0, above = 0, fU = 0, fA = 0, fS = 0, kU = 0, kA = 0, kS = 0, kSq = 0, k1332 = 0, k10 = 0, kEdge = 0,
            fluor = 0, fluorSq = 0, fluorSoil = 0;
        long interactions = 0, pairs = 0, cutoffs = 0;
        var stack = new Stack<Photon>();
        double[] edges = _o.ContinuumEdgesKeV;

        for (long h = 0; h < histories; h++)
        {
            if ((h & 0xFFFF) == 0) cancellationToken.ThrowIfCancellationRequested();
            int line = Array.BinarySearch(_cumulative, random.NextDouble());
            line = line >= 0 ? line : Math.Min(~line, lines - 1);
            double depth = -Math.Log(1 - random.NextDouble()) / _lambda[line];
            double weight = _sourceWeight[line] * Math.Exp(_lambda[line] * depth) / _lambda[line];
            double historyKerma = 0, historyFluor = 0;
            stack.Push(new Photon { Z = -depth, Energy = _energy[line], Direction = random.NextOnUnitSphere(), Kind = 0 });
            while (stack.Count > 0)
            {
                var p = stack.Pop();
                while (true)
                {
                    double w = p.Direction.Z;
                    bool inSoil = p.Z < 0 || p.Z == 0 && w < 0;
                    var material = inSoil ? _m.Soil : _m.Air;
                    var c = material.At(p.Energy);
                    double step = -Math.Log(1 - random.NextDouble()) / (material.DensityGPerCm3 * c.WithoutCoherent);
                    if (inSoil)
                    {
                        if (w > 0 && p.Z + w * step >= 0) { p.Z = 0; continue; } // leaves the soil: resample in air
                        p.Z += w * step;
                    }
                    else
                    {
                        double end = p.Z + w * step;
                        bool toGround = w < 0 && end <= 0;
                        double length = toGround ? p.Z / -w : step;
                        double track = SlabTrack(p.Z, toGround ? 0 : end, w, length);
                        if (track > 0)
                        {
                            double phi = weight * track * _scale;
                            double kerma = phi * p.Energy * _m.AirMuEnCm2PerG(p.Energy);
                            int z = Math.Min(zb - 1, (int)((w + 1) / 2 * zb));
                            historyKerma += kerma;
                            if (p.Energy > 1332) k1332 += kerma;
                            if (p.Energy < 10) k10 += kerma;
                            switch (p.Kind)
                            {
                                case 0:
                                    uncollided[line * zb + z] += phi; uncollidedSq[line * zb + z] += phi * phi; uncollidedCount[line * zb + z]++;
                                    fU += phi; kU += kerma; break;
                                case 1:
                                    annihilation[z] += phi; fA += phi; kA += kerma; break;
                                default:
                                    fS += phi; kS += kerma;
                                    if (p.Energy < edges[0]) { below += phi; kEdge += kerma; }
                                    else if (p.Energy >= edges[^1]) above += phi;
                                    else
                                    {
                                        int e = Array.BinarySearch(edges, p.Energy);
                                        e = e >= 0 ? e : ~e - 1;
                                        continuum[e * zb + z] += phi;
                                    }
                                    break;
                            }
                        }
                        if (toGround) { p.Z = 0; continue; } // enters the soil: resample there
                        p.Z = end;
                    }
                    interactions++;
                    double choice = random.NextDouble() * c.WithoutCoherent;
                    if (choice < c.Incoherent)
                    {
                        var (cosine, ratio) = KleinNishina.Sample(p.Energy, random);
                        p.Energy /= ratio;
                        p.Direction = KleinNishina.Rotate(p.Direction, cosine, 2 * Math.PI * random.NextDouble());
                        p.Kind = 2;
                        if (p.Energy < _o.TransportCutoffKeV) { cutoffs++; break; }
                        continue;
                    }
                    if (choice < c.Incoherent + c.Photoelectric || p.Energy < PairThresholdKeV)
                    {
                        // K fluorescence is not emitted; its largest possible air kerma is tallied as a bound instead.
                        double bound = weight * (inSoil ? _soilBound : _airBound).Value(p.Energy, p.Z);
                        fluor += bound; historyFluor += bound;
                        if (inSoil) fluorSoil += bound;
                        break;
                    }
                    pairs++;
                    var d = random.NextOnUnitSphere();
                    stack.Push(new Photon { Z = p.Z, Energy = AnnihilationKeV, Direction = d, Kind = 1 });
                    stack.Push(new Photon { Z = p.Z, Energy = AnnihilationKeV, Direction = d * -1, Kind = 1 });
                    break;
                }
            }
            kSq += historyKerma * historyKerma;
            fluorSq += historyFluor * historyFluor;
        }
        return new SoilAirTally
        {
            Chain = _chain.Name, Histories = histories, ZenithBins = zb, LineEnergiesKeV = (double[])_energy.Clone(),
            UncollidedSum = uncollided, UncollidedSumSq = uncollidedSq, UncollidedCount = uncollidedCount, AnnihilationSum = annihilation, ContinuumSum = continuum,
            ScatteredFluenceBelowFirstEdge = below, ScatteredFluenceAboveLastEdge = above,
            FluenceUncollided = fU, FluenceAnnihilation = fA, FluenceScattered = fS,
            KermaUncollided = kU, KermaAnnihilation = kA, KermaScattered = kS, KermaSumSq = kSq,
            KermaAbove1332KeV = k1332, KermaBelow10KeV = k10, KermaBelowFirstEdge = kEdge,
            FluorescenceKermaBound = fluor, FluorescenceKermaBoundSumSq = fluorSq, FluorescenceKermaBoundFromSoil = fluorSoil,
            Interactions = interactions, PairEvents = pairs, CutoffTerminations = cutoffs
        };
    }

    /// <summary>Track length inside the slab of a straight air segment from <paramref name="z0"/> to <paramref name="z1"/>.</summary>
    private double SlabTrack(double z0, double z1, double w, double length)
    {
        if (Math.Abs(w) < 1e-12) return z0 >= _lo && z0 <= _hi ? length : 0;
        double a = Math.Max(Math.Min(z0, z1), _lo), b = Math.Min(Math.Max(z0, z1), _hi);
        return b > a ? (b - a) / Math.Abs(w) : 0;
    }

    /// <summary>Analytic uncollided fluence of one line averaged over the scoring slab (Simpson, 64 intervals over the
    /// smooth height dependence), photons/(cm² s) per Bq/kg, in one upward zenith interval. Uses the transport's own
    /// attenuation (coherent excluded) unless <paramref name="includeCoherent"/>.</summary>
    public static double SlabAveragedUncollided(TerrestrialLine line, SoilAirMaterials materials, SoilAirTransportOptions options,
        double lowCosine, double highCosine, bool includeCoherent = false)
    {
        var soil = materials.Soil.At(line.EnergyKeV); var air = materials.Air.At(line.EnergyKeV);
        double muSoil = materials.Soil.DensityGPerCm3 * (includeCoherent ? soil.Total : soil.WithoutCoherent);
        double muAir = materials.Air.DensityGPerCm3 * (includeCoherent ? air.Total : air.WithoutCoherent);
        const int intervals = 64;
        double a = options.HeightCm - options.SlabHalfWidthCm, step = 2 * options.SlabHalfWidthCm / intervals, sum = 0;
        for (int i = 0; i <= intervals; i++)
        {
            double weight = i == 0 || i == intervals ? 1 : i % 2 == 1 ? 4 : 2;
            sum += weight * HalfSpaceUncollided.FluencePerBqKg(line.YieldPerChainDecay, materials.Soil.DensityGPerCm3,
                muSoil, muAir, a + i * step, lowCosine, highCosine);
        }
        return sum * step / 3 / (2 * options.SlabHalfWidthCm);
    }

    /// <summary>Point (not slab) analytic uncollided fluence at the height, used for the spectrum file's lines.</summary>
    public static double PointUncollided(TerrestrialLine line, SoilAirMaterials materials, SoilAirTransportOptions options,
        double lowCosine, double highCosine, bool includeCoherent = false)
    {
        var soil = materials.Soil.At(line.EnergyKeV); var air = materials.Air.At(line.EnergyKeV);
        return HalfSpaceUncollided.FluencePerBqKg(line.YieldPerChainDecay, materials.Soil.DensityGPerCm3,
            materials.Soil.DensityGPerCm3 * (includeCoherent ? soil.Total : soil.WithoutCoherent),
            materials.Air.DensityGPerCm3 * (includeCoherent ? air.Total : air.WithoutCoherent), options.HeightCm, lowCosine, highCosine);
    }

    /// <summary>Upper bound on the air kerma that one photoabsorption's K-fluorescence photon could deliver at the height.
    /// At most one K X-ray per photoabsorption (one K vacancy); its energy is below the largest K edge of the medium's
    /// elements that the absorbed photon exceeds. The bound takes, over a log grid of fluorescence energies from the
    /// cutoff to that edge, the largest uncollided kerma of an isotropic emitter in plane geometry, E₁(τ)/2 per column
    /// weight, with τ the vertical optical depth to the height using photoelectric attenuation only (scattering at
    /// these energies is a few % of μ and is treated as transparent). Tabulated on a distance grid and read at the grid
    /// point at or below the actual distance (the kernel decreases with distance), so the table never underestimates.</summary>
    private sealed class FluorescenceBound
    {
        private readonly double[] _edges;
        private readonly double[][] _table; // per edge, per distance index
        private readonly double[] _distance;
        private readonly bool _inSoil;
        private readonly double _height;
        private readonly Func<double, double, double> _kernelAtEdge;

        public FluorescenceBound(SoilAirMaterials m, SoilAirTransportOptions o, bool inSoil)
        {
            _inSoil = inSoil; _height = o.HeightCm;
            _edges = (inSoil ? m.SoilKEdgesKeV : m.AirKEdgesKeV).Where(e => e > o.TransportCutoffKeV).OrderBy(e => e).ToArray();
            // Distance grid: 0 and log-spaced 1e-4 … 1e6 cm (soil depth below the surface, or |z − h| in air).
            _distance = new[] { 0.0 }.Concat(Enumerable.Range(0, 201).Select(i => 1e-4 * Math.Pow(10, i * 10.0 / 200))).ToArray();
            double Kernel(double edge, double d)
            {
                double best = 0;
                for (int i = 0; i < o.FluorescenceEnergyPoints; i++)
                {
                    double e = o.TransportCutoffKeV * Math.Pow(edge / o.TransportCutoffKeV, (double)i / (o.FluorescenceEnergyPoints - 1));
                    double tau = inSoil
                        ? m.Soil.DensityGPerCm3 * m.Soil.At(e).Photoelectric * d + m.Air.DensityGPerCm3 * m.Air.At(e).Photoelectric * o.HeightCm
                        : m.Air.DensityGPerCm3 * m.Air.At(e).Photoelectric * d;
                    double value = tau > 0 ? e * m.AirMuEnCm2PerG(e) * ExponentialIntegral.E1(tau) / 2 : double.PositiveInfinity;
                    best = Math.Max(best, value);
                }
                return best;
            }
            _kernelAtEdge = Kernel;
            _table = _edges.Select(edge => _distance.Select(d => Kernel(edge, d)).ToArray()).ToArray();
        }

        public double Value(double absorbedKeV, double z)
        {
            int edge = -1;
            for (int i = 0; i < _edges.Length; i++) if (_edges[i] <= absorbedKeV) edge = i;
            if (edge < 0) return 0;
            double d = _inSoil ? -z : Math.Abs(z - _height);
            // E₁ diverges at the emitter; compute exactly there (an exact zero distance has probability zero).
            if (!_inSoil && d < _distance[1]) return _kernelAtEdge(_edges[edge], Math.Max(d, 1e-12));
            int j = Array.BinarySearch(_distance, d);
            j = j >= 0 ? j : ~j - 1;
            return _table[edge][Math.Max(0, j)];
        }
    }
}
