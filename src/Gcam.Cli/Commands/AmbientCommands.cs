using System.Text.Json;
using Gcam.Configuration;
using Gcam.Simulation;

namespace Gcam.Cli;

/// <summary>Explicit physical live-time route, separate from weighted photon-budget studies.</summary>
internal static class AmbientCommands
{
    internal static int RunUncollided(string[] args)
    {
        if (args.Length is < 2 or > 3)
        { Console.Error.WriteLine("Usage: montecarlo ambient-uncollided <kernel-request.json> [output.json]"); return 1; }
        using var input = JsonDocument.Parse(File.ReadAllText(args[1]));
        var request = input.RootElement;
        double yield = request.GetProperty("PhotonYieldPerDecay").GetDouble();
        double density = request.GetProperty("SoilDensityGPerCm3").GetDouble();
        double soilMu = request.GetProperty("SoilMuPerCm").GetDouble();
        double airMu = request.GetProperty("AirMuPerCm").GetDouble();
        double height = request.GetProperty("HeightCm").GetDouble();
        int histories = request.GetProperty("Histories").GetInt32();
        int bins = request.GetProperty("ZenithBins").GetInt32();
        int seed = request.GetProperty("Seed").GetInt32();
        var watch = System.Diagnostics.Stopwatch.StartNew();
        var mc = HalfSpaceUncollided.Measure(yield, density, soilMu, airMu, height, histories, bins, new Gcam.Core.DefaultRandom(seed));
        double analytic = HalfSpaceUncollided.FluencePerBqKg(yield, density, soilMu, airMu, height);
        var angular = Enumerable.Range(0, bins).Select(i => new
        {
            LowCosine = (double)i / bins, HighCosine = (double)(i + 1) / bins,
            Accepted = mc.ZenithCounts[i],
            McFluence = mc.FluenceNormalizationPerBqKg * mc.ZenithCounts[i] / histories,
            AnalyticFluence = HalfSpaceUncollided.FluencePerBqKg(yield, density, soilMu, airMu, height, (double)i / bins, (double)(i + 1) / bins)
        }).ToArray();
        double totalProbability = mc.FluenceNormalizationPerBqKg == 0 ? 0 : analytic / mc.FluenceNormalizationPerBqKg;
        double acceptanceSigma = mc.FluenceNormalizationPerBqKg * Math.Sqrt(totalProbability * (1 - totalProbability) / histories);
        bool passes = Math.Abs(mc.FluencePerBqKg - analytic) <= 6 * acceptanceSigma
            && angular.All(b =>
            {
                double p = mc.FluenceNormalizationPerBqKg == 0 ? 0 : b.AnalyticFluence / mc.FluenceNormalizationPerBqKg;
                return Math.Abs(b.McFluence - b.AnalyticFluence) <= 6 * mc.FluenceNormalizationPerBqKg * Math.Sqrt(p * (1 - p) / histories);
            });
        string json = JsonSerializer.Serialize(new
        {
            SchemaVersion = 1, Kind = "uncollided-kernel-check-NOT-VALIDATED", IsValidatedSpectrum = false,
            Note = "Not a terrestrial spectrum, not a collided transport calculation, and not UNSCEAR validation.",
            Request = request, Mc = mc, AnalyticFluence = analytic,
            Ratio = analytic > 0 ? (double?)(mc.FluencePerBqKg / analytic) : null,
            RatioStandardError = analytic > 0 ? (double?)(mc.StandardErrorPerBqKg / analytic) : null,
            Angular = angular, AcceptanceSigmaMultiplier = 6, PassesUncollidedKernelCheck = passes,
            ComputeSeconds = watch.Elapsed.TotalSeconds
        }, new JsonSerializerOptions { WriteIndented = true });
        if (args.Length == 3) File.WriteAllText(args[2], json);
        Console.WriteLine(json);
        return passes ? 0 : 2;
    }

    internal static int WritePlaceholder(string[] args)
    {
        if (args.Length != 2) { Console.Error.WriteLine("Usage: montecarlo ambient-placeholder <spectrum.json>"); return 1; }
        string json = JsonSerializer.Serialize(IncidentSpectrum.Placeholder(), new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(args[1], json);
        string fileHash = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(File.ReadAllBytes(args[1]))).ToLowerInvariant();
        File.WriteAllText(args[1] + ".sha256", fileHash + "\n");
        Console.WriteLine("NOT VALIDATED: synthetic isotropic development fixture, not an AB-4 terrestrial spectrum.");
        return 0;
    }

    internal static int RunFixedTime(string[] args)
    {
        if (args.Length is < 2 or > 3)
        { Console.Error.WriteLine("Usage: montecarlo ambient-fixed <scenario.json> [output.json]"); return 1; }
        var config = ConfigLoader.Load(args[1]);
        if (config.Ambient is not { } field) { Console.Error.WriteLine("An Ambient config is required."); return 1; }
        var runner = new SimulationRunner(new DefaultSimulationFactory());
        var watch = System.Diagnostics.Stopwatch.StartNew();
        var result = runner.RunFixedTime(config, config.Source.AcquisitionTimeSeconds);
        string json = JsonSerializer.Serialize(new
        {
            SchemaVersion = 1, Spectrum = field.Spectrum.Id, field.Spectrum.IsValidated,
            field.Spectrum.ContentHash, Geometry = field.Geometry.ToString(), field.DoseRateMicroSvPerHour,
            DurationS = config.Source.AcquisitionTimeSeconds, config.Seed,
            Counts = result.DetectedWeight, Flood = result.DetectorImage.Raw.ToArray(),
            PixelsX = result.DetectorImage.Width, PixelsY = result.DetectorImage.Height,
            ComputeSeconds = watch.Elapsed.TotalSeconds
        }, new JsonSerializerOptions { WriteIndented = true });
        if (args.Length == 3) File.WriteAllText(args[2], json);
        Console.WriteLine(json);
        if (!field.Spectrum.IsValidated) Console.Error.WriteLine("NOT VALIDATED: development spectrum; exclude from ambient evidence.");
        return 0;
    }
}
