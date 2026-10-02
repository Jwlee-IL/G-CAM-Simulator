using Gcam.Configuration;
using Gcam.Core;

namespace Gcam.Simulation;

/// <summary>Convert a scalar energy/zenith fluence table into incoming surface current.
/// The table's zenith is +y, independent of the camera's +z optical axis. Surface selection uses projected
/// area, so sampling scalar fluence uniformly as surface current would be incorrect for an anisotropic field.</summary>
public sealed class AmbientAngularSampler
{
    private readonly IncidentSpectrum _spectrum;
    private readonly IncidentAngularBin[] _bins;
    private readonly double[] _current, _maximumArea;
    private readonly double _width, _height, _depth, _totalCurrent;
    private readonly bool _frontOnly;
    public double EffectiveAreaMm2 { get; }

    public AmbientAngularSampler(IncidentSpectrum spectrum, AmbientGeometry geometry, double widthMm, double heightMm, double depthMm)
    {
        if (spectrum.AngularModel != "EnergyZenithTable" || spectrum.EnergyZenith is not { Length: > 0 } table)
            throw new ArgumentException("An energy/zenith table is required.");
        if (!Enum.IsDefined(geometry) || !(widthMm > 0 && heightMm > 0 && depthMm > 0)
            || !double.IsFinite(widthMm + heightMm + depthMm)) throw new ArgumentOutOfRangeException(nameof(widthMm));
        _spectrum = spectrum;
        _bins = table;
        _width = widthMm; _height = heightMm; _depth = depthMm;
        _frontOnly = geometry == AmbientGeometry.FrontOnlyThroughMask;
        double front = widthMm * heightMm, sideX = heightMm * depthMm, sideY = widthMm * depthMm;
        var weights = spectrum.Lines.Select(l => l.FluenceWeight).Concat(spectrum.Continuum.Select(b => b.FluenceWeight)).ToArray();
        foreach (var bin in _bins)
            if (bin.EnergyIndex < 0 || bin.EnergyIndex >= weights.Length
                || !double.IsFinite(bin.LowCosine) || !double.IsFinite(bin.HighCosine)
                || bin.LowCosine < -1 || bin.HighCosine > 1 || bin.LowCosine >= bin.HighCosine
                || !double.IsFinite(bin.FluenceWeight) || bin.FluenceWeight < 0)
                throw new ArgumentException("Angular bins require a valid energy index, finite nonnegative weight and -1 <= low < high <= 1.");
        for (int i = 0; i < weights.Length; i++)
        {
            var component = _bins.Where(b => b.EnergyIndex == i).OrderBy(b => b.LowCosine).ToArray();
            for (int j = 1; j < component.Length; j++)
                if (component[j].LowCosine < component[j - 1].HighCosine) throw new ArgumentException("Angular bins overlap within an energy component.");
            double sum = component.Sum(b => b.FluenceWeight);
            // A marginal identity, not a statistical tolerance: 64 ulps for finite summation/serialization.
            if (!double.IsFinite(weights[i]) || weights[i] < 0 || Math.Abs(sum - weights[i]) > 64 * 2.2204460492503131e-16 * Math.Max(sum, weights[i]))
                throw new ArgumentException("Angular fluence weights must reproduce each energy marginal.");
        }
        _current = _bins.Select(b => b.FluenceWeight * MeanProjectedArea(b.LowCosine, b.HighCosine, front, sideX, sideY, _frontOnly)).ToArray();
        _maximumArea = _bins.Select(b =>
        {
            double closest = b.LowCosine > 0 ? b.LowCosine : b.HighCosine < 0 ? b.HighCosine : 0;
            double sineMaximum = Math.Sqrt(Math.Max(0, 1 - closest * closest));
            // Restrict the rejection envelope to this bin so narrow near-zenith front bins cannot stall sampling.
            return _frontOnly ? front * sineMaximum
                : Math.Sqrt(front * front + sideX * sideX) * sineMaximum + sideY * Math.Max(Math.Abs(b.LowCosine), Math.Abs(b.HighCosine));
        }).ToArray();
        _totalCurrent = _current.Sum();
        double totalFluence = weights.Sum();
        if (!(totalFluence > 0) || !double.IsFinite(totalFluence) || !(_totalCurrent > 0) || !double.IsFinite(_totalCurrent)
            || _current.Any(a => a < 0 || !double.IsFinite(a)) || _maximumArea.Any(a => !double.IsFinite(a)))
            throw new ArgumentException("Angular spectrum requires finite positive scalar fluence.");
        EffectiveAreaMm2 = _totalCurrent / totalFluence;
    }

    /// <summary>Exact azimuth and bin integrals for a rectangular box with world +y zenith.
    /// Bare projected area is A_y |mu| + (A_x + A_z) (2/pi) sqrt(1-mu²).
    /// Front-only projected area is A_z sqrt(1-mu²)/pi.</summary>
    public static double MeanProjectedArea(double low, double high, double front, double sideX, double sideY, bool frontOnly)
    {
        double absoluteMean = (high * Math.Abs(high) - low * Math.Abs(low)) / (2 * (high - low));
        double sineMean = (SinePrimitive(high) - SinePrimitive(low)) / (high - low);
        return frontOnly ? front * sineMean / Math.PI : sideY * absoluteMean + (sideX + front) * 2 * sineMean / Math.PI;
    }

    private static double SinePrimitive(double mu) => .5 * (mu * Math.Sqrt(Math.Max(0, 1 - mu * mu)) + Math.Asin(mu));

    /// <summary>Sample the joint energy/ray surface current, not the scalar fluence distribution.
    /// Returned ray starts one mm outside the crystal; front rays start beyond the mask slab.</summary>
    public (double EnergyKeV, Ray Ray) Sample(IRandom angular, IRandom energy, double maskFrontZ)
    {
        if (!(_totalCurrent > 0)) throw new InvalidOperationException("The angular field has zero surface current.");
        double choice = angular.NextDouble() * _totalCurrent;
        int index = 0;
        while (index < _bins.Length - 1 && (choice -= _current[index]) >= 0) index++;
        var bin = _bins[index];
        Vector3 direction;
        double projected;
        // Conditional current in this bin. The conservative area bound affects efficiency only, never weights/rate.
        do
        {
            double mu = bin.LowCosine + angular.NextDouble() * (bin.HighCosine - bin.LowCosine);
            double phi = 2 * Math.PI * angular.NextDouble(), sine = Math.Sqrt(Math.Max(0, 1 - mu * mu));
            direction = new(sine * Math.Cos(phi), mu, sine * Math.Sin(phi));
            projected = _frontOnly ? _width * _height * Math.Max(0, -direction.Z)
                : _height * _depth * Math.Abs(direction.X) + _width * _depth * Math.Abs(direction.Y) + _width * _height * Math.Abs(direction.Z);
        } while (angular.NextDouble() * _maximumArea[index] >= projected);

        int face = 0;
        if (!_frontOnly)
        {
            double pick = angular.NextDouble() * projected;
            if (pick < _width * _height * Math.Abs(direction.Z)) face = direction.Z < 0 ? 0 : 1;
            else if (pick < _width * _height * Math.Abs(direction.Z) + _height * _depth * Math.Abs(direction.X)) face = direction.X > 0 ? 2 : 3;
            else face = direction.Y > 0 ? 4 : 5;
        }
        double x = (angular.NextDouble() - .5) * _width, y = (angular.NextDouble() - .5) * _height;
        double z = -angular.NextDouble() * _depth;
        Vector3 position = face switch
        {
            0 => new(x, y, 0), 1 => new(x, y, -_depth),
            2 => new(-_width / 2, y, z), 3 => new(_width / 2, y, z),
            4 => new(x, -_height / 2, z), _ => new(x, _height / 2, z)
        };
        int energyIndex = bin.EnergyIndex;
        double photonEnergy = energyIndex < _spectrum.Lines.Length ? _spectrum.Lines[energyIndex].EnergyKeV
            : ContinuumEnergy(_spectrum.Continuum[energyIndex - _spectrum.Lines.Length], energy);
        double back = _frontOnly ? (maskFrontZ + 1) / -direction.Z : 1;
        return (photonEnergy, new Ray(position - direction * back, direction));
    }

    private static double ContinuumEnergy(IncidentContinuumBin bin, IRandom random) => bin.LowKeV + random.NextDouble() * (bin.HighKeV - bin.LowKeV);
}
