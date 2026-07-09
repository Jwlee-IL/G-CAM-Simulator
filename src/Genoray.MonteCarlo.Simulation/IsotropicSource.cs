using Genoray.MonteCarlo.Core;

namespace Genoray.MonteCarlo.Simulation;

/// <summary>Mono-energetic isotropic point source (e.g. Cs-137 at 661.7 keV).</summary>
public sealed class IsotropicSource : ISource
{
    private readonly Vector3 _position;
    private readonly double _energyKeV;

    public IsotropicSource(Vector3 position, double energyKeV)
    {
        _position = position;
        _energyKeV = energyKeV;
    }

    public IEnumerable<Photon> Emit(IRandom rng, long count)
    {
        for (long n = 0; n < count; n++)
        {
            yield return new Photon
            {
                Ray = new Ray(_position, rng.NextOnUnitSphere()),
                EnergyKeV = _energyKeV,
            };
        }
    }
}
