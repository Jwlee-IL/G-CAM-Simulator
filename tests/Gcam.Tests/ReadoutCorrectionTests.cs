using Gcam.Configuration;
using Gcam.Core;
using Gcam.Detector;
using Gcam.Simulation;

namespace Gcam.Tests;

/// <summary>TODO-41 (SD-A4): the opt-in correction histogram counts the calibration flood's own photopeak assignments
/// (finished LUT, per-crystal gains, ±window of the line) from the events calibration already holds. Oracle: an
/// independent transport / readout replay of the same flood with the same streams. Collecting it leaves every
/// calibration output unchanged; a streamed flood (never materialised) gives the same calibration as a list.</summary>
public sealed class ReadoutCorrectionTests
{
    [Theory]
    [InlineData(4, FloodCalibrationMethod.Legacy, ChargeNetworkSolveMethod.Legacy)]
    [InlineData(8, FloodCalibrationMethod.LatticeGrowth, ChargeNetworkSolveMethod.Adjoint)]
    public void Histogram_EqualsReplay_AndOptInLeavesCalibrationIdentical(int n, FloodCalibrationMethod method, ChargeNetworkSolveMethod solve)
    {
        var config = new SimulationConfig();
        config.Detector.PixelsX = config.Detector.PixelsY = n;
        config.Detector.Material = "GAGG";
        var policy = new ReadoutConfig { Mode = ReadoutMode.FourOutputAnger,
            Network = new() { ColumnResistanceOhm = 10, SolveMethod = solve } };
        policy.Calibration.Method = method;
        var device = new ReadoutDevice(config.Detector, policy, 41000001);
        var processor = new ReadoutPulseProcessor(device, policy.Pulse, policy.Trigger);
        int histories = n * n * 2000;
        var flood = ReadoutFlood.Transport(config, 661.7, histories, 41000002);
        var legacy = ReadoutCalibration.Build(device, processor, flood, policy.Calibration,
            new DefaultRandom(41000003), new DefaultRandom(41000004));
        var corrected = ReadoutCalibration.Build(device, processor, new Streamed(config, histories), policy.Calibration,
            new DefaultRandom(41000003), new DefaultRandom(41000004), collectCorrectionHistogram: true);
        Assert.True(corrected.Succeeded, corrected.Failure);
        Assert.Null(legacy.CorrectionHistogram);
        Assert.Equal(legacy.Lut.Density, corrected.Lut.Density);
        Assert.Equal(legacy.Lut.Peaks, corrected.Lut.Peaks);
        Assert.Equal(legacy.GlobalPeakSum, corrected.GlobalPeakSum);
        Assert.Equal(legacy.InWindow, corrected.InWindow);
        var expected = new int[n * n];
        var response = new DefaultRandom(41000003);
        var noise = new DefaultRandom(41000004);
        var channels = new double[4];
        foreach (var sites in ReadoutFlood.Stream(config, 661.7, histories, 41000002))
        {
            double sum = device.Respond(sites, response, channels);
            var ev = processor.ProcessIsolated(new(0, channels, sum), noise);
            if (ev is null || !device.Position(ev.Codes, out double x, out double y)) continue;
            int c = corrected.Lut.Lookup(x, y);
            if (c < 0) continue;
            double measured = corrected.Energy(c, device.Sum(ev.Codes));
            if (Math.Abs(measured - 661.7) <= .15 * 661.7) expected[c]++;
            Assert.Equal(legacy.Energy(c, device.Sum(ev.Codes)), measured);
        }
        Assert.Equal(expected, corrected.CorrectionHistogram);
    }

    /// <summary>The flood as a stream (as Studio prepares it): enumerated once, no random access.</summary>
    private sealed class Streamed(SimulationConfig config, int count) : IReadOnlyList<InteractionSite[]>
    {
        public int Count => count;
        public InteractionSite[] this[int index] => throw new NotSupportedException();
        public IEnumerator<InteractionSite[]> GetEnumerator() => ReadoutFlood.Stream(config, 661.7, count, 41000002).GetEnumerator();
        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => GetEnumerator();
    }
}
