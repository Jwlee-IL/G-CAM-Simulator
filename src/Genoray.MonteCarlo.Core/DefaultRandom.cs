namespace Genoray.MonteCarlo.Core;

/// <summary>Default <see cref="IRandom"/> backed by <see cref="System.Random"/>.</summary>
public sealed class DefaultRandom : IRandom
{
    private readonly Random _rng;

    public DefaultRandom(int? seed = null)
        => _rng = seed.HasValue ? new Random(seed.Value) : new Random();

    public double NextDouble() => _rng.NextDouble();

    public Vector3 NextOnUnitSphere()
    {
        // Cosine-uniform sampling of the sphere: z uniform in [-1,1], azimuth uniform.
        double z = 2.0 * _rng.NextDouble() - 1.0;
        double phi = 2.0 * Math.PI * _rng.NextDouble();
        double r = Math.Sqrt(1.0 - z * z);
        return new Vector3(r * Math.Cos(phi), r * Math.Sin(phi), z);
    }
}
