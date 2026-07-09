namespace Genoray.MonteCarlo.Core;

/// <summary>
/// Abstraction over the random-number source so simulations are reproducible
/// (seedable) and the generator is swappable (e.g. for a higher-quality PRNG).
/// </summary>
public interface IRandom
{
    /// <summary>Uniform double in [0, 1).</summary>
    double NextDouble();

    /// <summary>A uniformly distributed direction on the unit sphere (isotropic emission).</summary>
    Vector3 NextOnUnitSphere();
}
