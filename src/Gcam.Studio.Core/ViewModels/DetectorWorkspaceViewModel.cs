using CommunityToolkit.Mvvm.ComponentModel;
using Gcam.Studio.Core.Detector;
using Gcam.Studio.Core.Services;

namespace Gcam.Studio.Core.ViewModels;

/// <summary>Static response pattern, frozen to acquired detector and optics once counts are retained.</summary>
public sealed partial class DetectorWorkspaceViewModel(MainViewModel shared, IDetectorFaceService? service)
    : WorkspaceViewModel("Detector", "Workspace.Detector")
{
    private Gcam.Configuration.OpticsSettings? _faceOptics;
    private DetectorSettings? _faceDetector;
    public MainViewModel Shared { get; } = shared;
    [ObservableProperty] private DetectorFace? _face;
    public double MinimumGain => Face?.Crystals.Min(c => c.Gain) ?? 0;
    public double MaximumGain => Face?.Crystals.Max(c => c.Gain) ?? 0;
    public string GainLegend => Face is { } f ? $"Relative gain {f.Crystals.Min(c => c.Gain):0.000}–{f.Crystals.Max(c => c.Gain):0.000} · face {f.SizeMm:0.###} mm square" : "Enter valid detector settings";
    public string Identity => Shared.Snapshot is null ? "Pending detector · no acquisition" :
        Shared.IsResultStale ? "Acquired detector · settings outdated" : "Acquired detector";
    public string Pending => $"Next: gap {Shared.Detector.ReflectorGapMm * 1000:0.###} µm · gain σ {Shared.GainSigmaPercent:0.#}% · seed {Shared.GainSeed}";
    public string Readout
    {
        get
        {
            var detector = Shared.Snapshot?.Detector ?? Shared.Detector;
            var optics = Shared.Snapshot?.Optics ?? Shared.Optics;
            return $"{detector.Chain.Scintillator.Name}\n{optics.DetectorPixels} × {optics.DetectorPixels} crystals · pitch {optics.PixelPitchMm:0.###} mm\n"
                + $"Gap {detector.ReflectorGapMm * 1000:0.###} µm · active width {optics.PixelPitchMm - detector.ReflectorGapMm:0.###} mm\n"
                + $"Gain σ {detector.GainSigma * 100:0.#}% · seed {detector.GainSeed}\nGeometric active-area fraction: {Face?.ActiveAreaFraction:P2}";
        }
    }
    public string Counts => Shared.Snapshot is { } s
        ? $"Acquired {s.Counts:N0} counts · {s.ObservedRateCps:0.0} cps (counts / live time)"
        : "No acquired counts";
    internal void Refresh()
    {
        var optics = Shared.Snapshot?.Optics ?? Shared.Optics;
        var detector = Shared.Snapshot?.Detector ?? Shared.Detector;
        if (Shared.Snapshot is not null || Shared.GapError is null)
        {
            if (Face is null || optics != _faceOptics || detector != _faceDetector)
            {
                Face = service?.Build(optics, detector);
                _faceOptics = optics; _faceDetector = detector;
            }
        }
        else { Face = null; _faceOptics = null; _faceDetector = null; }
        foreach (string name in new[] { nameof(Identity), nameof(Pending), nameof(Readout), nameof(Counts), nameof(GainLegend), nameof(MinimumGain), nameof(MaximumGain) }) OnPropertyChanged(name);
    }
}
