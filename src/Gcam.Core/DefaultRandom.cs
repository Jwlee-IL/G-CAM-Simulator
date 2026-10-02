namespace Gcam.Core;

/// <summary>
/// Default <see cref="IRandom"/>: xoshiro256** (Blackman &amp; Vigna 2018) with its 256-bit state seeded by
/// SplitMix64, 53-bit doubles.
/// </summary>
/// <remarks>
/// Replaced the seeded legacy <see cref="System.Random"/> (TODO-26): that generator's state is affine in its seed, so
/// two streams whose seeds differ by d are the same stream shifted mod 1 (draw-for-draw correlations up to 0.99), and
/// a fresh generator per item (seed + index·k) produced a lattice instead of random numbers. SplitMix64 mixes the
/// seed, so nearby seeds give unrelated streams. One instance per stream; not thread-safe.
/// </remarks>
public sealed class DefaultRandom : IRandom
{
    private const ulong Gamma = 0x9E3779B97F4A7C15UL;
    private const double DoubleUnit = 1.0 / (1UL << 53);

    private ulong _s0, _s1, _s2, _s3;

    /// <summary>A stream for <paramref name="seed"/>; null draws a fresh seed from <see cref="Random.Shared"/>, readable
    /// from <see cref="Seed"/> so an unseeded run can be logged and replayed.</summary>
    public DefaultRandom(int? seed = null)
        : this(seed.HasValue ? unchecked((ulong)(long)seed.Value) : unchecked((ulong)Random.Shared.NextInt64(long.MinValue, long.MaxValue)), 0)
    {
    }

    /// <summary>A stream for a 64-bit key (e.g. a (seed, index) pair, see <see cref="Key"/>). A factory rather than a
    /// constructor overload, so an int literal never has to choose between <c>int?</c> and <c>ulong</c>.</summary>
    public static DefaultRandom FromKey(ulong key) => new(key, 0);

    private DefaultRandom(ulong key, byte _)
    {
        Seed = key;
        ulong sm = key;
        _s0 = SplitMix64(ref sm);
        _s1 = SplitMix64(ref sm);
        _s2 = SplitMix64(ref sm);
        _s3 = SplitMix64(ref sm);
    }

    /// <summary>The 64-bit seed this stream was built from (an int seed is sign-extended).</summary>
    public ulong Seed { get; }

    /// <summary>The 64-bit key of item <paramref name="index"/> of a stream family <paramref name="seed"/>: the seed in
    /// the high word, the index in the low word — distinct for every (seed, index) pair, no arithmetic progression
    /// in the int domain.</summary>
    public static ulong Key(int seed, uint index) => ((ulong)(uint)seed << 32) | index;

    public double NextDouble() => (NextUInt64() >> 11) * DoubleUnit;

    public Vector3 NextOnUnitSphere()
    {
        // Cosine-uniform sampling of the sphere: z uniform in [-1,1], azimuth uniform.
        double z = 2.0 * NextDouble() - 1.0;
        double phi = 2.0 * Math.PI * NextDouble();
        double r = Math.Sqrt(1.0 - z * z);
        return new Vector3(r * Math.Cos(phi), r * Math.Sin(phi), z);
    }

    private ulong NextUInt64()
    {
        ulong result = unchecked(RotateLeft(_s1 * 5, 7) * 9);
        ulong t = _s1 << 17;
        _s2 ^= _s0;
        _s3 ^= _s1;
        _s1 ^= _s2;
        _s0 ^= _s3;
        _s2 ^= t;
        _s3 = RotateLeft(_s3, 45);
        return result;
    }

    private static ulong SplitMix64(ref ulong x)
    {
        ulong z = unchecked(x += Gamma);
        z = unchecked((z ^ (z >> 30)) * 0xBF58476D1CE4E5B9UL);
        z = unchecked((z ^ (z >> 27)) * 0x94D049BB133111EBUL);
        return z ^ (z >> 31);
    }

    private static ulong RotateLeft(ulong x, int k) => (x << k) | (x >> (64 - k));
}
