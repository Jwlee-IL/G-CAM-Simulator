using Gcam.Configuration;

namespace Gcam.Tests.Harness;

/// <summary>The documented scenarios, loaded from <c>samples/</c> so a test exercises the same head the Findings
/// describe. Each call returns a fresh config; override the photon budget and seed for test speed.</summary>
public static class Rigs
{
    /// <summary>The reference lab geometry (12×12 GAGG, rank-7 mask at D = 60 mm, source plane 100 mm beyond it).</summary>
    public static SimulationConfig Lab(long photons = 300_000, int seed = 12345)
        => Load("scenario.json", photons, seed);

    /// <summary>The theme-22 hand-held head (16×16 @ 1 mm, 15 mm GAGG:Ce,Mg, D = 55 mm).</summary>
    public static SimulationConfig Handheld(long photons = 300_000, int seed = 12345)
        => Load("scenario_handheld.json", photons, seed);

    private static SimulationConfig Load(string file, long photons, int seed)
    {
        var cfg = ConfigLoader.Load(RepoPaths.Sample(file));
        cfg.PhotonCount = photons;
        cfg.Seed = seed;
        return cfg;
    }
}

/// <summary>Locates the repository from the test binary, so tests can read <c>samples/</c>.</summary>
public static class RepoPaths
{
    public static string Root { get; } = Find();

    public static string Sample(string relative) => Path.Combine(Root, "samples", relative);

    private static string Find()
    {
        for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir is not null; dir = dir.Parent)
            if (File.Exists(Path.Combine(dir.FullName, "Gcam.sln"))) return dir.FullName;
        throw new InvalidOperationException("Gcam.sln not found above " + AppContext.BaseDirectory);
    }
}
