using Gcam.Configuration;
using Gcam.Core;
using Gcam.Detector;

namespace Gcam.Simulation;

/// <summary>
/// Calibration of a physical readout from a uniform flood, as a laboratory does it: every flood history goes through the
/// full readout (light, sensors, network, trigger, hold, digitisation); the global photopeak of the code sum is found
/// (mode of the keV-equivalent sum within ±50 % of the line, then the mean within the window, iterated); events in the
/// photopeak window build the flood-map LUT (<see cref="FloodLut"/>, explicit failure); each crystal's energy gain is the
/// iterated mean code sum of its photopeak events (crystals with fewer than <see cref="MinGainEvents"/> keep the global
/// gain and are counted). The calibration flood must be independent of any validation data (train / test split).
/// </summary>
public sealed class ReadoutCalibration
{
    public const int MinGainEvents = 10;
    private readonly double[] _kevPerCode;

    public FloodLut Lut { get; }
    public double EnergyKeV { get; }
    public int Scored { get; }
    public int Triggered { get; }
    public int InWindow { get; }
    public double GlobalPeakSum { get; }
    public int CrystalsOnGlobalGain { get; }
    public bool Succeeded => Lut.Succeeded;
    public string? Failure => Lut.Failure;

    private ReadoutCalibration(FloodLut lut, double energy, int scored, int triggered, int inWindow, double peak,
        double[] gains, int fallback)
    {
        Lut = lut; EnergyKeV = energy; Scored = scored; Triggered = triggered; InWindow = inWindow;
        GlobalPeakSum = peak; _kevPerCode = gains; CrystalsOnGlobalGain = fallback;
    }

    /// <summary>Calibrated energy (keV) of an event with code sum <paramref name="sum"/> assigned to <paramref name="crystal"/>.</summary>
    public double Energy(int crystal, double sum) => crystal >= 0 ? sum * _kevPerCode[crystal] : double.NaN;

    /// <param name="device">The readout to calibrate.</param>
    /// <param name="processor">Its trigger / hold / digitisation (each flood history is an isolated pulse).</param>
    /// <param name="flood">Pre-optical sites of the calibration flood (independent of any validation data).</param>
    /// <param name="config">Calibration line, window and LUT parameters.</param>
    /// <param name="response">Stream of the light / sensor fluctuations.</param>
    /// <param name="noise">Stream of the electronic noise.</param>
    public static ReadoutCalibration Build(ReadoutDevice device, ReadoutPulseProcessor processor,
        IReadOnlyList<InteractionSite[]> flood, FloodCalibrationConfig config, IRandom response, IRandom noise)
    {
        ArgumentNullException.ThrowIfNull(response);
        ArgumentNullException.ThrowIfNull(noise);
        ArgumentNullException.ThrowIfNull(device);
        ArgumentNullException.ThrowIfNull(processor);
        ArgumentNullException.ThrowIfNull(flood);
        ArgumentNullException.ThrowIfNull(config);
        if (!(config.EnergyKeV > 0) || config.WindowFraction is <= 0 or >= 1)
            throw new ArgumentException("Calibration needs a positive line and a window fraction in (0, 1).");
        var channels = new double[device.Channels];
        var events = new List<(double X, double Y, double Sum)>(flood.Count);
        foreach (var sites in flood)
        {
            double sum = device.Respond(sites, response, channels);
            var ev = processor.ProcessIsolated(new ReadoutHit(0, channels, sum), noise);
            if (ev is null) continue;
            double s = device.Sum(ev.Codes);
            if (device.Position(ev.Codes, out double x, out double y)) events.Add((x, y, s));
        }
        double e = config.EnergyKeV, w = config.WindowFraction;
        double peak = GlobalPeak(events.Select(v => v.Sum / device.CodesPerKeV), e) * device.CodesPerKeV;
        for (int it = 0; it < 3 && peak > 0; it++)
            peak = MeanWithin(events.Select(v => v.Sum), peak, w) ?? peak;
        var windowed = events.Where(v => Math.Abs(v.Sum - peak) <= w * peak).ToList();
        var lut = FloodLut.Calibrate(windowed.Select(v => (v.X, v.Y)).ToList(), device.Crystals.CountX,
            device.Crystals.CountY, config);
        var gains = Enumerable.Repeat(peak > 0 ? e / peak : double.NaN, device.Crystals.Count).ToArray();
        int fallback = device.Crystals.Count;
        if (lut.Succeeded && peak > 0)
        {
            fallback = 0;
            var perCrystal = new List<double>[device.Crystals.Count];
            for (int c = 0; c < perCrystal.Length; c++) perCrystal[c] = [];
            foreach (var v in windowed)
            {
                int c = lut.Lookup(v.X, v.Y);
                if (c >= 0) perCrystal[c].Add(v.Sum);
            }
            for (int c = 0; c < perCrystal.Length; c++)
            {
                if (perCrystal[c].Count < MinGainEvents) { fallback++; continue; }
                double m = peak;
                for (int it = 0; it < 3; it++) m = MeanWithin(perCrystal[c], m, w) ?? m;
                gains[c] = e / m;
            }
        }
        return new ReadoutCalibration(lut, e, flood.Count, events.Count, windowed.Count, peak, gains, fallback);
    }

    // Mode of the keV-equivalent sums within ±50 % of the line: 1 %-of-line bins, 5-bin moving average.
    private static double GlobalPeak(IEnumerable<double> kev, double line)
    {
        int bins = 100;
        var h = new double[bins];
        foreach (double v in kev)
        {
            int b = (int)((v / line - 0.5) * bins);
            if (b >= 0 && b < bins) h[b]++;
        }
        int best = -1;
        double top = 0;
        for (int b = 0; b < bins; b++)
        {
            double s = 0;
            for (int i = Math.Max(0, b - 2); i <= Math.Min(bins - 1, b + 2); i++) s += h[i];
            if (s > top) { top = s; best = b; }
        }
        return best < 0 ? 0 : line * (0.5 + (best + 0.5) / bins);
    }

    private static double? MeanWithin(IEnumerable<double> values, double center, double fraction)
    {
        double s = 0;
        int n = 0;
        foreach (double v in values)
            if (Math.Abs(v - center) <= fraction * center) { s += v; n++; }
        return n > 0 ? s / n : null;
    }
}
