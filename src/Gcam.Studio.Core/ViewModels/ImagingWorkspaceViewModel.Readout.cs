using System.Globalization;

namespace Gcam.Studio.Core.ViewModels;

public sealed partial class ImagingWorkspaceViewModel
{
    /// <summary>The current acquisition's window reading, kept apart from the benchmark exposures.</summary>
    public string ReadoutCurrentCounts => SelectedChannel is { } channel && Shared.IsPhysicalReadout
        ? channel.Unavailable ?? string.Create(CultureInfo.InvariantCulture,
            $"This acquisition: {channel.RawCounts ?? (long)channel.Image.EffectiveCounts:N0} raw counts in the window")
            + (channel.CalibrationLineKeV is { } l ? string.Create(CultureInfo.InvariantCulture, $" ({l:0.##} keV calibration)") : "")
            + (channel.FloodCorrected ? " · display flood-corrected (opt-in)" : "")
        : "";

    internal void NotifyPhysicalChannel()
    {
        foreach (string name in new[] { nameof(ReadoutCurrentCounts), nameof(SamplingCaption), nameof(SamplingEvidence) })
            OnPropertyChanged(name);
    }
}
