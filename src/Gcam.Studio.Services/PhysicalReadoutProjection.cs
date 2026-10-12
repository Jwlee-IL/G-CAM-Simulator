using System.Diagnostics;
using System.Globalization;
using Gcam.Configuration;
using Gcam.Core;
using Gcam.Studio.Core.Services;

namespace Gcam.Studio.Services;

/// <summary>Project stored measured facts. No truth-pixel assignment, gain, smear or energy-only pile-up occurs here.
/// Every captured line projects the same held records through its own calibration (TODO-41, SD-A1…A5); a window is read
/// only through the calibration of the one line it contains, never through another line's LUT.</summary>
public static class PhysicalReadoutProjection
{
    public static void ValidateWindow(double low, double high)
    { if (!double.IsFinite(low) || !double.IsFinite(high) || low < 0 || high <= low) throw new ArgumentException("Energy window needs finite 0 ≤ low < high keV."); }

    private static string Kev(double value) => value.ToString("0.##", CultureInfo.InvariantCulture);

    /// <summary>The captured view of <paramref name="lineKeV"/>; a hand-built single-preparation snapshot (no line views)
    /// serves its reference line from the records' own assignments.</summary>
    public static ReadoutLineView? LineView(ReadoutSnapshot readout, double lineKeV)
    {
        if (readout.Line(lineKeV) is { } view) return view;
        if (readout.Lines.Count > 0 || readout.Preparation.LineKeV != lineKeV) return null;
        var assignments = readout.Records.Select(r => new ReadoutLineAssignment(r.Crystal, r.EnergyKeV)).ToArray();
        return new ReadoutLineView(readout.Preparation, assignments, assignments.Count(a => a.Crystal >= 0), assignments.Count(a => a.Crystal < 0));
    }

    public static SpectrumView Spectrum(ReadoutSnapshot readout, SpectrumSettings settings, CancellationToken token)
    {
        ValidateWindow(settings.WindowLowKeV, settings.WindowHighKeV);
        var watch = Stopwatch.StartNew(); var counts = new double[SpectrumService.BinCount]; long total = 0, overflow = 0;
        foreach (var e in readout.Records)
        {
            token.ThrowIfCancellationRequested(); if (e.Crystal < 0) continue;
            int b = (int)(e.EnergyKeV / SpectrumService.BinWidthKeV); total++;
            if (b >= 0 && b < counts.Length) counts[b]++; else overflow++;
        }
        // The window band is read through the calibration of the line it contains when that line is captured, so it
        // agrees with the Imaging window channel; otherwise through the labelled reference calibration.
        double? line = ReadoutPolicy.LineForWindow(settings.WindowLowKeV, settings.WindowHighKeV, out _);
        var view = line is { } l ? LineView(readout, l) : null;
        long window = 0, assigned = 0;
        if (view is not null)
            for (int i = 0; i < view.Assignments.Count; i++)
            {
                var a = view.Assignments[i]; if (a.Crystal < 0) continue;
                assigned++;
                if (a.EnergyKeV >= settings.WindowLowKeV && a.EnergyKeV <= settings.WindowHighKeV) window++;
            }
        else
        {
            assigned = total;
            foreach (var e in readout.Records)
                if (e.Crystal >= 0 && e.EnergyKeV >= settings.WindowLowKeV && e.EnergyKeV <= settings.WindowHighKeV) window++;
        }
        string calibration = view is not null
            ? $"Measured energy window · {Kev(view.LineKeV)} keV calibration"
            : $"Measured energy window · reference {Kev(readout.Preparation.LineKeV)} keV calibration";
        SpectrumBand band = new([new SpectrumLine(calibration, (settings.WindowLowKeV + settings.WindowHighKeV) / 2)],
            settings.WindowLowKeV, settings.WindowHighKeV, window, assigned > 0 ? (double)window / assigned : 0);
        return new(Enumerable.Range(0, counts.Length).Select(i => (i + .5) * SpectrumService.BinWidthKeV).ToArray(), counts,
            [band], total, overflow, band.Share, double.NaN, double.NaN,
            $"Experimental four outputs · measured held energy · reference {Kev(readout.Preparation.LineKeV)} keV calibration", watch.Elapsed)
        { BinEdgesKeV = Enumerable.Range(0, counts.Length + 1).Select(i => i * SpectrumService.BinWidthKeV).ToArray() };
    }

    public static ImagingView Imaging(AcquisitionSnapshot snapshot, SimulationConfig projection, ImagingSettings settings, CancellationToken token, MlemDecoderCache? cache = null)
    {
        ValidateWindow(settings.WindowLowKeV, settings.WindowHighKeV);
        if (settings.Strip) throw new NotSupportedException("Physical readout stripping needs independent readout-aware calibration.");
        var watch = Stopwatch.StartNew();
        var readout = snapshot.Readout!;
        int nx = snapshot.Imaging.Flood.Width, ny = snapshot.Imaging.Flood.Height;
        cache ??= new MlemDecoderCache();
        if (settings.Method == DecoderMethod.Mlem) projection.Decoder.MlemIterations = Gcam.Studio.Core.Imaging.StudioMlem.Iterations;
        MlemProjection? Mlem(double line) => settings.Method == DecoderMethod.Mlem ? new(cache, line) : null;
        var all = ImagingProjection.Project(snapshot.Imaging.Flood, snapshot.Imaging, projection, "All", 0, double.NaN, double.NaN,
            Mlem(readout.Preparation.LineKeV)) with { CalibrationLineKeV = readout.Preparation.LineKeV };
        return new(Array.AsReadOnly(new[] { all, Window(snapshot, readout, nx, ny, projection, settings, Mlem, token) }), [],
            watch.Elapsed, TimeSpan.Zero, TimeSpan.Zero) { Method = settings.Method };
    }

    private static ImagingChannel Unavailable(AcquisitionSnapshot snapshot, int nx, int ny, ImagingSettings settings, string reason,
        double? line = null, long? raw = null)
    {
        var empty = new DetectorImage(nx, ny).ReadOnlyCopy();
        var image = new ImagingResult(empty, snapshot.Imaging.FloodOriginMm, snapshot.Imaging.FloodStepMm, null, 0, 0, null, 0, TimeSpan.Zero);
        return new ImagingChannel("Energy window", settings.WindowLowKeV, settings.WindowHighKeV, image, [])
        { Unavailable = reason, CalibrationLineKeV = line, RawCounts = raw };
    }

    private static ImagingChannel Window(AcquisitionSnapshot snapshot, ReadoutSnapshot readout, int nx, int ny,
        SimulationConfig projection, ImagingSettings settings, Func<double, MlemProjection?> mlem, CancellationToken token)
    {
        if (ReadoutPolicy.LineForWindow(settings.WindowLowKeV, settings.WindowHighKeV, out string? reason) is not { } line)
            return Unavailable(snapshot, nx, ny, settings, reason!);
        if (LineView(readout, line) is not { } view)
            return Unavailable(snapshot, nx, ny, settings,
                $"Unavailable: no {Kev(line)} keV calibration was captured by this acquisition (no other line's LUT is used).", line);
        var flood = new DetectorImage(nx, ny);
        long raw = 0;
        for (int i = 0; i < view.Assignments.Count; i++)
        {
            if ((i & 4095) == 0) token.ThrowIfCancellationRequested();
            var a = view.Assignments[i];
            if (a.Crystal >= 0 && a.EnergyKeV >= settings.WindowLowKeV && a.EnergyKeV <= settings.WindowHighKeV)
            { flood.Add(a.Crystal % nx, a.Crystal / nx, 1); raw++; }
        }
        if (!settings.ApplyFloodCorrection)
            return ImagingProjection.Project(flood.ReadOnlyCopy(), snapshot.Imaging, projection, "Energy window", 0,
                settings.WindowLowKeV, settings.WindowHighKeV, mlem(line)) with { CalibrationLineKeV = line, RawCounts = raw };
        // SD-A4 / A5: weight = mean assigned count / bin count from the line calibration's own flood (no clipping). An
        // empty bin makes the corrected image unavailable; the raw image stays available with the correction off.
        if (view.Preparation.CorrectionHistogram is not { } histogram || histogram.Count != nx * ny)
            return Unavailable(snapshot, nx, ny, settings, $"Corrected image unavailable: the {Kev(line)} keV calibration carries no correction histogram.", line, raw);
        int empty = histogram.Count(h => h <= 0);
        if (empty > 0)
            return Unavailable(snapshot, nx, ny, settings,
                $"Corrected image unavailable: {empty} empty correction bin(s) in the {Kev(line)} keV calibration flood (never capped or divided silently); clear flood correction for the raw image.", line, raw);
        double mean = histogram.Average();
        var corrected = new DetectorImage(nx, ny);
        for (int y = 0; y < ny; y++) for (int x = 0; x < nx; x++)
            corrected[x, y] = flood[x, y] * (mean / histogram[y * nx + x]);
        var channel = ImagingProjection.Project(corrected.ReadOnlyCopy(), snapshot.Imaging, projection, "Energy window", 0,
            settings.WindowLowKeV, settings.WindowHighKeV, mlem(line));
        return channel with { Image = channel.Image with { EffectiveCounts = raw }, CalibrationLineKeV = line, RawCounts = raw, FloodCorrected = true };
    }
}
