namespace Genoray.MonteCarlo.Core;

/// <summary>A photon trajectory: an origin point plus a (unit) direction.</summary>
public readonly record struct Ray(Vector3 Origin, Vector3 Direction)
{
    /// <summary>Point along the ray at parameter <paramref name="t"/> (mm).</summary>
    public Vector3 At(double t) => Origin + Direction * t;
}
