using System.Text.Json;
using System.Windows;
using Gcam.Studio.Core.ViewModels;

namespace Gcam.Studio.Controls;

/// <summary>Read-only numerical UIA evidence, enabled only in a harness-owned process. No commands or I/O.</summary>
internal static class AutomationEvidence
{
    public static string? Read(FrameworkElement owner)
    {
        if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable("GCAM_UIA_RUN"))) return null;
        object? evidence = owner.DataContext switch
        {
            SpectrumWorkspaceViewModel s => s.View is null ? null : new
            {
                s.View.CentresKeV, s.View.Counts, s.View.Bands, s.WindowFwhm,
                PlotCounts = (owner as PlotView)?.Series?.FirstOrDefault()?.Y,
                PlotBands = (owner as PlotView)?.Bands
            },
            WaveformWorkspaceViewModel w => w.View is null ? null : new
            {
                Times = w.Shared.Snapshot!.Events.Select(e => e.ArrivalTimeS).ToArray(),
                w.TriggerIndex, w.WindowUs, w.View.Events, w.Markers, w.RateStudy,
                PlotMarkers = (owner as PlotView)?.Markers
            },
            ImagingWorkspaceViewModel i => i.Result is null ? null : new
            {
                i.SelectedIsotope, i.Strip, i.IsProcessing, i.FocalDistanceMm,
                i.Result.ReconOriginMm, i.Result.ReconStepMm,
                Width = i.Result.Reconstruction?.Width ?? 0,
                Reconstruction = i.Result.Reconstruction?.Raw.ToArray(),
                Flood = i.Result.Flood.Raw.ToArray(), i.Peaks, i.Ratios,
                DisplayedImage = (owner as HeatmapView)?.Image?.Raw.ToArray(),
                FoundMarkers = MeasurementOverlay.GetFoundPeaks(owner)?.Cast<object>().ToArray(),
                i.SweepResult,
                PlotBands = (owner as PlotView)?.Bands
            },
            _ => null
        };
        return evidence is null ? "{}" : JsonSerializer.Serialize(evidence);
    }
}
