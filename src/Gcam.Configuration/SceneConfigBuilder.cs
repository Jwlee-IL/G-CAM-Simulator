namespace Gcam.Configuration;

/// <summary>Optical/detector settings a front-end edits alongside the scene.</summary>
public sealed record OpticsSettings
{
    /// <summary>MURA rank; snapped to the nearest prime when the config is built.</summary>
    public int MuraRank { get; init; } = 13;
    public double CellPitchMm { get; init; } = 0.7;
    public double MaskDetectorDistanceMm { get; init; } = 80;
    public int DetectorPixels { get; init; } = 30;
    public double PixelPitchMm { get; init; } = 0.6;

    /// <summary>The source plane the decoder focuses on (rangefinder distance from the detector).</summary>
    public double FocalDistanceMm { get; init; } = 1000;
}

/// <summary>
/// Builds a <see cref="SimulationConfig"/> from a scene of sources plus optics settings — the same imaging
/// setup the original viewer used (finite-mask decode, recon grid kept inside the fully-coded FOV), but free of
/// any UI so it can be shared and unit-tested.
/// </summary>
public static class SceneConfigBuilder
{
    public static SimulationConfig Build(IReadOnlyList<SceneSource> scene, OpticsSettings optics, long photons, int seed = 12345)
    {
        ArgumentNullException.ThrowIfNull(scene);
        ArgumentNullException.ThrowIfNull(optics);
        if (photons <= 0) throw new ArgumentOutOfRangeException(nameof(photons), "Photon budget must be positive.");

        var sources = scene.Select(s => s.ToConfig()).ToArray();
        var cfg = new SimulationConfig
        {
            PhotonCount = photons,
            Seed = seed,
            Source = sources.Length > 0 ? sources[0] : new SourceConfig(),
            Sources = sources.Length > 0 ? sources : null,
        };

        double d = Math.Max(1.0, optics.MaskDetectorDistanceMm);
        double pitch = Math.Max(0.05, optics.PixelPitchMm);
        double focal = Math.Max(d + 1.0, optics.FocalDistanceMm);

        cfg.Mask.Rank = NearestPrime(optics.MuraRank);
        cfg.Mask.CellPitchMm = Math.Max(0.05, optics.CellPitchMm);
        cfg.Geometry.MaskDetectorDistanceMm = d;
        cfg.Geometry.SourceMaskDistanceMm = focal - d;
        cfg.Detector.PixelsX = cfg.Detector.PixelsY = Math.Clamp(optics.DetectorPixels, 4, 64);
        cfg.Detector.PixelPitchMm = pitch;

        // Non-cyclic (finite-mask) decode suppresses off-axis ghosts so several sources resolve separately.
        cfg.Decoder.Cyclic = false;
        double frac = d / focal;
        cfg.Decoder.ReconHalfExtentMm = 0.95 * cfg.Mask.Rank * cfg.Mask.CellPitchMm / frac / 2.0;
        cfg.Decoder.ReconStepMm = Math.Max(0.2, cfg.Mask.CellPitchMm / frac / 4.0);
        return cfg;
    }

    /// <summary>Half-width (mm) of the fully-coded field of view at the focal plane.</summary>
    public static double FcfovHalfMm(OpticsSettings optics)
    {
        double d = Math.Max(1.0, optics.MaskDetectorDistanceMm);
        double focal = Math.Max(d + 1.0, optics.FocalDistanceMm);
        return NearestPrime(optics.MuraRank) * optics.CellPitchMm / (d / focal) / 2.0;
    }

    public static int NearestPrime(int n)
    {
        if (n < 2) return 2;
        for (int k = 0; ; k++)
        {
            if (n - k >= 2 && IsPrime(n - k)) return n - k;
            if (IsPrime(n + k)) return n + k;
        }
    }

    private static bool IsPrime(int n)
    {
        if (n < 2) return false;
        for (int i = 2; (long)i * i <= n; i++)
            if (n % i == 0) return false;
        return true;
    }
}
