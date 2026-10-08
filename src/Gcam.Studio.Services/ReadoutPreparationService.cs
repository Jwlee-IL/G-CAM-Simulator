using System.Diagnostics;
using Gcam.Configuration;
using Gcam.Core;
using Gcam.Detector;
using Gcam.Simulation;
using Gcam.Studio.Core.Services;

namespace Gcam.Studio.Services;

/// <summary>Bounded two-entry content cache. Preparation is explicit and independent of live acquisition seeds.</summary>
public sealed class ReadoutPreparationService : IReadoutPreparationService
{
    private readonly SemaphoreSlim _gate = new(1);
    private readonly List<(ReadoutPreparation View, ReadoutDevice Device, ReadoutCalibration Calibration)> _cache = [];
    public static string Key(OpticsSettings optics, DetectorSettings detector)
        => ReadoutPolicy.Key(optics, detector);

    public static string? GeometryError(OpticsSettings optics) => ReadoutPolicy.GeometryError(optics);

    public static ReadoutConfig Preset() => new()
    {
        Mode = ReadoutMode.FourOutputAnger,
        Network = new ChargeNetworkConfig { ColumnResistanceOhm = 10 }
    };

    internal (ReadoutPreparation View, ReadoutDevice Device, ReadoutCalibration Calibration) Get(Guid id)
    {
        lock (_cache) return _cache.FirstOrDefault(c => c.View.Id == id) is var artifact && artifact.View is not null
            ? artifact : throw new InvalidOperationException("Prepare the matching readout before Start.");
    }

    public async Task<ReadoutPreparation> PrepareAsync(OpticsSettings optics, DetectorSettings detector,
        IProgress<string>? progress = null, CancellationToken cancellationToken = default)
    {
        if (GeometryError(optics) is { } error) throw new ArgumentException(error);
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            string key = Key(optics, detector);
            lock (_cache) if (_cache.FirstOrDefault(c => c.View.Key == key) is var cached && cached.View is not null) return cached.View;
            var artifact = await Task.Run(() => Build(optics, detector, key, progress, cancellationToken), cancellationToken).ConfigureAwait(false);
            cancellationToken.ThrowIfCancellationRequested();
            if (artifact.View.Succeeded) lock (_cache)
            {
                if (_cache.Count == 2) _cache.RemoveAt(0);
                _cache.Add(artifact);
            }
            return artifact.View;
        }
        finally { _gate.Release(); }
    }

    private static (ReadoutPreparation View, ReadoutDevice Device, ReadoutCalibration Calibration) Build(
        OpticsSettings optics, DetectorSettings detector, string key, IProgress<string>? progress, CancellationToken token)
    {
        var watch = Stopwatch.StartNew();
        var config = SimulationService.BuildConfig([new SceneSource()], optics,
            detector with { ReadoutMode = ReadoutMode.DirectCrystal });
        var ro = Preset();
        config.Detector.Material = "GAGG";
        config.Detector.GainSigma = 0;
        progress?.Report("Preparing optical table and DC network…");
        var device = new ReadoutDevice(config.Detector, ro, 402, token);
        var processor = new ReadoutPulseProcessor(device, ro.Pulse, ro.Trigger);
        progress?.Report("Transporting independent calibration flood…");
        var flood = ReadoutFlood.Transport(config, ro.Calibration.EnergyKeV, 144 * ro.Calibration.EventsPerCrystal, 403, token);
        progress?.Report("Finding calibration peaks and building LUT…");
        var cal = ReadoutCalibration.Build(device, processor, flood, ro.Calibration, new DefaultRandom(404), new DefaultRandom(405), token);
        var lut = cal.Lut;
        var density = new DetectorImage(lut.BinsX, lut.BinsY);
        var labels = new int[lut.BinsX * lut.BinsY];
        for (int y = 0; y < lut.BinsY; y++) for (int x = 0; x < lut.BinsX; x++)
        { density[x,y] = lut.Density[y * lut.BinsX + x]; labels[y * lut.BinsX + x] = lut.LabelOfBin(x,y); }
        var diagnostics = new List<string>();
        if (cal.Succeeded)
        {
            progress?.Report("Checking independent single-crystal validation flood…");
            var validation = ReadoutFlood.Transport(config, 661.7, 144 * 200, 406, token);
            int[] total = new int[144], accepted = new int[144], wrong = new int[144];
            var response = new DefaultRandom(407); var noise = new DefaultRandom(408); var channels = new double[4];
            foreach (var sites in validation)
            {
                token.ThrowIfCancellationRequested();
                int truth = ReadoutDevice.DirectCrystal(sites, 12);
                if (sites.Any(s => s.CrystalY * 12 + s.CrystalX != truth)) continue;
                total[truth]++;
                double sum = device.Respond(sites, response, channels);
                var ev = processor.ProcessIsolated(new(0, channels, sum), noise);
                if (ev is null || !device.Position(ev.Codes, out double x, out double y)) continue;
                int mapped = cal.Lut.Lookup(x,y); if (mapped < 0) continue;
                accepted[truth]++; if (mapped != truth) wrong[truth]++;
            }
            for (int c = 0; c < 144; c++) diagnostics.Add($"Crystal {c}: assigned {accepted[c]} / {total[c]} single-crystal histories; wrong {wrong[c]} / {accepted[c]} assigned (661.7 keV, all energies; validation seed 406).");
        }
        var view = new ReadoutPreparation(Guid.NewGuid(), key, cal.Succeeded, cal.Failure, density.ReadOnlyCopy(),
            Array.AsReadOnly(labels), Array.AsReadOnly(lut.Peaks.ToArray()), diagnostics.AsReadOnly(), cal.Scored,
            cal.Triggered, cal.InWindow, cal.CrystalsOnGlobalGain, watch.Elapsed, processor.RiseNs, processor.TailNs,
            processor.Unit(processor.PeakGridNs) == 1 ? Math.Exp(-processor.PeakGridNs / processor.TailNs) - Math.Exp(-processor.PeakGridNs / processor.RiseNs) : 1,
            processor.SupportNs, processor.ThresholdCodes)
        {
            Circuit = device.Network.Circuit is { } circuit ? new(Array.AsReadOnly((string[])circuit.NodeNames.Clone()),
                Array.AsReadOnly(((int A, int B, double Ohm)[])circuit.Resistors.Clone()), circuit.InputOhm) : null,
            SensorActiveWidthMm = ro.Sensors.ActiveWidthMm ?? config.Detector.PixelPitchMm - config.Detector.ReflectorGapMm
        };
        return (view, device, cal);
    }
}
