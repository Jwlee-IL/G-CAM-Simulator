using Genoray.MonteCarlo.Core;

namespace Genoray.MonteCarlo.Simulation;

/// <summary>
/// A mixed-isotope field: several (position, energy line) emitters imaged through the coded
/// aperture in one run. Each photon picks an emitter with probability ∝ its EMISSION weight
/// (source activity × line intensity), then is emitted from that position at that energy.
///
/// Relative line/source intensities come out right WITHOUT any extra per-photon reweighting:
/// allocating the photon COUNT in proportion to (activity × intensity) already makes each
/// emitter's contribution ∝ its weight, and the detector-biasing weight (A·cosθ/4π r², the same
/// variance-reduction as <see cref="DetectorBiasedSource"/>) carries each source's own geometric
/// efficiency (a farther / more off-axis source detects less — physically correct). So the flood
/// map has emitter j at strength ∝ (activity_j × intensity_j) × geometric-efficiency_j.
/// </summary>
public sealed class MixedFieldSource : ISource
{
    private readonly Vector3[] _pos;
    private readonly double[] _energyKeV;
    private readonly double[] _cumWeight;   // cumulative emission weights, for O(n) sampling
    private readonly double _totalWeight;
    private readonly bool _biased;
    private readonly double _halfW, _halfH, _detPlaneZ;

    public MixedFieldSource(IReadOnlyList<(Vector3 pos, double energyKeV, double weight)> emitters,
                            bool biased, double detHalfWidthMm, double detHalfHeightMm, double detPlaneZ)
    {
        int n = emitters.Count;
        _pos = new Vector3[n];
        _energyKeV = new double[n];
        _cumWeight = new double[n];
        double acc = 0.0;
        for (int i = 0; i < n; i++)
        {
            _pos[i] = emitters[i].pos;
            _energyKeV[i] = emitters[i].energyKeV;
            // Sanitize: negative or non-finite (NaN/Inf) weights count as zero, so a bad entry can't
            // corrupt the cumulative table or leak through Pick().
            double w = emitters[i].weight;
            acc += double.IsFinite(w) && w > 0.0 ? w : 0.0;
            _cumWeight[i] = acc;
        }
        _totalWeight = acc;
        if (!(acc > 0.0))
            throw new ArgumentException(
                "MixedFieldSource needs at least one emitter with a positive, finite emission weight " +
                "(activity × intensity).", nameof(emitters));
        _biased = biased;
        _halfW = detHalfWidthMm;
        _halfH = detHalfHeightMm;
        _detPlaneZ = detPlaneZ;
    }

    public IEnumerable<Photon> Emit(IRandom rng, long count)
    {
        double norm = (2.0 * _halfW) * (2.0 * _halfH) / (4.0 * Math.PI);
        for (long n = 0; n < count; n++)
        {
            int j = Pick(rng.NextDouble() * _totalWeight);
            var pos = _pos[j];
            double energy = _energyKeV[j];

            if (_biased)
            {
                // Detector-area importance sampling from THIS emitter's position (same as
                // DetectorBiasedSource): aim at a uniform point on the detector, weight A·cosθ/(4π r²).
                double tx = (rng.NextDouble() * 2.0 - 1.0) * _halfW;
                double ty = (rng.NextDouble() * 2.0 - 1.0) * _halfH;
                var delta = new Vector3(tx, ty, _detPlaneZ) - pos;
                double r = delta.Length;
                var dir = delta * (1.0 / r);
                double weight = norm * Math.Abs(dir.Z) / (r * r);
                yield return new Photon { Ray = new Ray(pos, dir), EnergyKeV = energy, Weight = weight };
            }
            else
            {
                yield return new Photon { Ray = new Ray(pos, rng.NextOnUnitSphere()), EnergyKeV = energy, Weight = 1.0 };
            }
        }
    }

    // Sample an emitter index proportional to its emission weight (linear scan; emitter count is tiny).
    private int Pick(double u)
    {
        for (int i = 0; i < _cumWeight.Length; i++)
            if (u <= _cumWeight[i]) return i;
        return _cumWeight.Length - 1;
    }
}
