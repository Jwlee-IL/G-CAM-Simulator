namespace Gcam.Core;

/// <summary>A single gamma photon travelling through the system.</summary>
public sealed class Photon
{
    /// <summary>Current trajectory (origin + direction).</summary>
    public Ray Ray { get; set; }

    /// <summary>Energy in keV (Cs-137 primary line = 661.7).</summary>
    public double EnergyKeV { get; set; }

    /// <summary>Statistical weight for variance-reduction techniques (1.0 = analog).</summary>
    public double Weight { get; set; } = 1.0;

    /// <summary>False once the photon is absorbed or escapes the geometry.</summary>
    public bool Alive { get; set; } = true;

    public Vector3 Position => Ray.Origin;
    public Vector3 Direction => Ray.Direction;
}
