namespace Gcam.Core;

/// <summary>Double-precision 3D vector used for photon geometry (units: mm).</summary>
public readonly record struct Vector3(double X, double Y, double Z)
{
    public static readonly Vector3 Zero = new(0, 0, 0);

    public static Vector3 operator +(Vector3 a, Vector3 b) => new(a.X + b.X, a.Y + b.Y, a.Z + b.Z);
    public static Vector3 operator -(Vector3 a, Vector3 b) => new(a.X - b.X, a.Y - b.Y, a.Z - b.Z);
    public static Vector3 operator *(Vector3 a, double s) => new(a.X * s, a.Y * s, a.Z * s);
    public static Vector3 operator *(double s, Vector3 a) => a * s;

    public double Length => Math.Sqrt(X * X + Y * Y + Z * Z);
    public double Dot(Vector3 o) => X * o.X + Y * o.Y + Z * o.Z;

    public Vector3 Normalized()
    {
        var len = Length;
        return len > 0 ? new Vector3(X / len, Y / len, Z / len) : this;
    }
}
