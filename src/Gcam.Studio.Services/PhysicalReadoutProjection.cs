using System.Diagnostics;
using Gcam.Configuration;
using Gcam.Core;
using Gcam.Studio.Core.Services;

namespace Gcam.Studio.Services;

/// <summary>Project stored measured facts. No truth-pixel assignment, gain, smear or energy-only pile-up occurs here.</summary>
public static class PhysicalReadoutProjection
{
    public static void ValidateWindow(double low, double high)
    { if (!double.IsFinite(low) || !double.IsFinite(high) || low < 0 || high <= low) throw new ArgumentException("Energy window needs finite 0 ≤ low < high keV."); }

    public static SpectrumView Spectrum(ReadoutSnapshot readout, SpectrumSettings settings, CancellationToken token)
    {
        ValidateWindow(settings.WindowLowKeV, settings.WindowHighKeV);
        var watch = Stopwatch.StartNew(); var counts = new double[SpectrumService.BinCount]; long total = 0, overflow = 0, window = 0;
        foreach (var e in readout.Records)
        {
            token.ThrowIfCancellationRequested(); if (e.Crystal < 0) continue;
            int b = (int)(e.EnergyKeV / SpectrumService.BinWidthKeV); total++;
            if (b >= 0 && b < counts.Length) counts[b]++; else overflow++;
            if (e.EnergyKeV >= settings.WindowLowKeV && e.EnergyKeV <= settings.WindowHighKeV) window++;
        }
        SpectrumBand band = new([new SpectrumLine("Measured energy window", (settings.WindowLowKeV + settings.WindowHighKeV) / 2)],
            settings.WindowLowKeV, settings.WindowHighKeV, window, total > 0 ? (double)window / total : 0);
        return new(Enumerable.Range(0, counts.Length).Select(i => (i + .5) * SpectrumService.BinWidthKeV).ToArray(), counts,
            [band], total, overflow, band.Share, double.NaN, double.NaN, "Experimental four outputs · measured held energy", watch.Elapsed)
        { BinEdgesKeV = Enumerable.Range(0, counts.Length + 1).Select(i => i * SpectrumService.BinWidthKeV).ToArray() };
    }

    public static ImagingView Imaging(AcquisitionSnapshot snapshot, SimulationConfig projection, ImagingSettings settings, CancellationToken token, MlemDecoderCache? cache = null)
    {
        ValidateWindow(settings.WindowLowKeV, settings.WindowHighKeV);
        if (settings.Strip) throw new NotSupportedException("Physical readout stripping needs independent readout-aware calibration.");
        var watch = Stopwatch.StartNew(); var flood = new DetectorImage(12,12);
        foreach (var e in snapshot.Readout!.Records)
        { token.ThrowIfCancellationRequested(); if (e.Crystal >= 0 && e.EnergyKeV >= settings.WindowLowKeV && e.EnergyKeV <= settings.WindowHighKeV) flood.Add(e.Crystal % 12,e.Crystal / 12,1); }
        MlemProjection? mlem = settings.Method == DecoderMethod.Mlem ? new(cache ?? new MlemDecoderCache(), 661.7) : null;
        if (mlem is not null) projection.Decoder.MlemIterations = Gcam.Studio.Core.Imaging.StudioMlem.Iterations;
        var channels = new[] {
            ImagingProjection.Project(snapshot.Imaging.Flood, snapshot.Imaging, projection, "All", 0, double.NaN, double.NaN, mlem),
            ImagingProjection.Project(flood.ReadOnlyCopy(), snapshot.Imaging, projection, "Energy window", 0, settings.WindowLowKeV, settings.WindowHighKeV, mlem) };
        return new(Array.AsReadOnly(channels), [], watch.Elapsed, TimeSpan.Zero, TimeSpan.Zero) { Method = settings.Method };
    }
}
