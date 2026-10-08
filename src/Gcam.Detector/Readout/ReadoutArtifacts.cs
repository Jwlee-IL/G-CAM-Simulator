using System.Text.Json;
using Gcam.Configuration;

namespace Gcam.Detector;

/// <summary>Exact content reuse. Weak entries do not keep large optical tables alive after their clients release them.
/// Seeds and every optical parameter participate in the key; no translated or statistically equivalent tables match.</summary>
internal static class ReadoutArtifacts
{
    private static readonly Dictionary<string, WeakReference<OpticalResponse>> Optics = [];
    private static readonly Dictionary<string, WeakReference<ChargeDivisionNetwork>> Networks = [];
    internal static string NetworkKey(ReadoutConfig config, SensorLayout sensors) =>
        JsonSerializer.Serialize(new { config.Mode, config.Network, sensors.CountX, sensors.CountY });

    internal static ChargeDivisionNetwork Network(ReadoutConfig config, SensorLayout sensors, CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        config = JsonSerializer.Deserialize<ReadoutConfig>(JsonSerializer.Serialize(config))!;
        string key = NetworkKey(config, sensors);
        lock (Networks)
        {
            if (Networks.TryGetValue(key, out var weak) && weak.TryGetTarget(out var existing)) return existing;
        }
        var artifact = config.Mode == ReadoutMode.FourOutputAnger
            ? ChargeDivisionNetwork.Build(config.Network, sensors.CountX, sensors.CountY, token)
            : ChargeDivisionNetwork.Identity(sensors.Count);
        token.ThrowIfCancellationRequested();
        lock (Networks)
        {
            foreach (string dead in Networks.Where(p => !p.Value.TryGetTarget(out _)).Select(p => p.Key).ToArray()) Networks.Remove(dead);
            Networks[key] = new(artifact);
        }
        return artifact;
    }

    internal static OpticalResponse Optical(CrystalArrayGeometry crystals, ReadoutConfig config, int seed, CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        var sensors = SensorLayout.From(config.Sensors, crystals);
        var optics = JsonSerializer.Deserialize<ReadoutOpticsConfig>(JsonSerializer.Serialize(config.Optics))!;
        string key = JsonSerializer.Serialize(new { crystals, sensors, optics, seed });
        lock (Optics)
        {
            if (Optics.TryGetValue(key, out var weak) && weak.TryGetTarget(out var existing)) return existing;
        }
        var artifact = new OpticalResponse(crystals, sensors, optics, seed, token);
        token.ThrowIfCancellationRequested();
        lock (Optics)
        {
            foreach (string dead in Optics.Where(p => !p.Value.TryGetTarget(out _)).Select(p => p.Key).ToArray()) Optics.Remove(dead);
            Optics[key] = new(artifact);
        }
        return artifact;
    }
}
