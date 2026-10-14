using Gcam.Configuration;
using Gcam.Studio.Core.Services;
using Gcam.Studio.Core.ViewModels;

namespace Gcam.Studio.Core.Reporting;

/// <summary>One export-time capture of published images and displayed text; never runs a simulation or projection.</summary>
public sealed record SimulationReport(
    DateTimeOffset ExportedAt, string Version, string ConfigurationJson, bool PhysicalReadout,
    IReadOnlyList<FactRow> Scene, IReadOnlyList<FactRow> Acquisition,
    ImagingResult? Image, IReadOnlyList<ImagingPeak> Peaks, IReadOnlyList<FactRow> Imaging,
    SpectrumView? Spectrum, bool LogY, IReadOnlyList<FactRow> SpectrumSettings,
    IReadOnlyList<ReportMeasurement> Measurements)
{
    public static SimulationReport Capture(MainViewModel vm, DateTimeOffset exportedAt, string version)
    {
        var snapshot = vm.Snapshot ?? throw new InvalidOperationException("No acquisition to export.");
        var configuration = snapshot.ConfigurationJson ?? throw new InvalidOperationException("The acquisition has no configuration record.");
        var image = vm.Imaging.Result;
        // Strings are captured in the screen's culture. SVG geometry is formatted invariantly by the writer.
        return new(exportedAt, version, configuration,
            snapshot.Detector?.ReadoutMode is { } mode && mode != ReadoutMode.DirectCrystal,
            vm.Sources.Select((s, i) => new FactRow($"Source {i + 1}", s.Label)).Concat(new FactRow[]
            {
                new("Optics", $"rank {snapshot.Optics?.MuraRank} · cell {snapshot.Optics?.CellPitchMm:0.###} mm · mask distance {snapshot.Optics?.MaskDetectorDistanceMm:0.###} mm"),
                new("Detector", $"{snapshot.Imaging.Flood.Width} × {snapshot.Imaging.Flood.Height} · pixel {snapshot.Optics?.PixelPitchMm:0.###} mm · {vm.DetectorSummary}"),
                new("Chain", snapshot.Chain.ToString()), new("Readout", vm.ReadoutModeSummary),
                new("Ambient", $"{vm.AmbientDoseRateMicroSvPerHour:0.###} µSv/h · {vm.AmbientEnvironmentLabel} · {vm.AmbientBsrReadout}"),
                new("Added background", $"{vm.BackgroundToSignalRatio:G4} × signal"),
                new("Acquired decoder", "Cross-correlation; acquired focus recorded in configuration")
            }).ToArray(),
            [new("Run state", vm.State.ToString()), new("Status", vm.Status),
                new("Simulated time", $"{snapshot.LiveTimeS:0.0} s"), new("Counts", $"{snapshot.Counts:N0}"),
                new("Observed rate", $"{snapshot.ObservedRateCps:N0} cps"), new("Seed", (snapshot.Seed ?? vm.AcquisitionSeed)?.ToString() ?? "Unavailable")],
            image, vm.Imaging.Peaks.ToArray(),
            [new("Channel", vm.Imaging.ChannelSummary), new("Displayed decoder", vm.Imaging.DisplayedMethod.ToString()),
                new("Focus", vm.Imaging.FocalSummary), new("Summary", vm.Imaging.Summary),
                new("Estimate", vm.Imaging.PeakText ?? "Unavailable"),
                new("Estimate confidence", image?.Estimate is { } estimate ? $"{estimate.Confidence:G4}" : "Unavailable"),
                new("Notes", string.Join(" ", new[] { vm.Imaging.Error, vm.Imaging.ChannelUnavailable, vm.Imaging.StripNote, vm.Imaging.ReconstructionNote }.OfType<string>()))],
            vm.Spectrum.View, vm.Spectrum.LogY,
            [new("Summary", vm.Spectrum.Summary), new("Chain", vm.Spectrum.Chain), new("Resolution", vm.Spectrum.Resolution),
                new("Resolving time", vm.Spectrum.ResolvingTime), new("Log Y", vm.Spectrum.LogY.ToString()),
                new("Pile-up", vm.Spectrum.PileUp.ToString()), new("Notes", vm.Spectrum.Error ?? "")],
            vm.Imaging.Measurements.Items.Select(m => new ReportMeasurement(m.Name, m.PaneLabel, m.KindLabel, m.Value, m.Detail)).ToArray());
    }
}
