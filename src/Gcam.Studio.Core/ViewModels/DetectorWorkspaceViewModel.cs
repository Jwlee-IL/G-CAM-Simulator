using CommunityToolkit.Mvvm.ComponentModel;
using Gcam.Studio.Core.Detector;
using Gcam.Studio.Core.Services;

namespace Gcam.Studio.Core.ViewModels;

/// <summary>Static response pattern: pending detector and optics in Empty, the acquired (locked) ones once data exist.</summary>
public sealed partial class DetectorWorkspaceViewModel(MainViewModel shared, IDetectorFaceService? service)
    : WorkspaceViewModel("Detector", "Workspace.Detector")
{
    private Gcam.Configuration.OpticsSettings? _faceOptics;
    private DetectorSettings? _faceDetector;
    public MainViewModel Shared { get; } = shared;
    [ObservableProperty] private DetectorFace? _face;
    [ObservableProperty] private ReadoutDetectorTab _readoutTab;
    public ReadoutPreparation? Preparation => Shared.Snapshot?.Readout?.Preparation ?? Shared.MatchingPreparation;
    public ReadoutSnapshot? PhysicalSnapshot => Shared.Snapshot?.Readout;
    public IReadOnlyList<string> Diagnostics => Preparation?.Diagnostics ?? [];
    public string CalibrationState => Preparation is { } p
        ? $"{(p.Succeeded ? "Ready" : "Failed: " + p.Failure)} · {p.Peaks.Count} / 144 peaks\nCalibration {p.Id}\nSeeds: optical 402; transport 403; response 404; ADC 405; independent validation 406/407/408\nScored {p.Scored:N0} · triggered {p.Triggered:N0} · windowed {p.InWindow:N0}\nGlobal energy gain fallback {p.GainFallback} / 144 crystals\nCost {p.Cost.TotalSeconds:0.00} s; {p.Density.Width} × {p.Density.Height} LUT bins"
        : Shared.ReadoutState;
    public string PhysicalCounts => PhysicalSnapshot is { } p
        ? $"Assigned {Shared.Snapshot!.Counts:N0} · unknown {p.Unknown:N0} · triggered {p.Records.Count:N0} · realised hits {p.Hits.Count:N0} · diagnostic truth {p.TruthSamples.Count} / 256 first hits{(p.PendingHold ? " · hold pending observed horizon" : "")}"
        : "No measured conversions";
    public double MinimumGain => Face?.Crystals.Min(c => c.Gain) ?? 0;
    public double MaximumGain => Face?.Crystals.Max(c => c.Gain) ?? 0;
    public string GainLegend => Face is { } f ? $"Relative gain {f.Crystals.Min(c => c.Gain):0.000}–{f.Crystals.Max(c => c.Gain):0.000} · face {f.SizeMm:0.###} mm square" : "Enter valid detector settings";
    /// <summary>Whose settings the face shows: the next acquisition's (Empty) or the acquired, locked ones.</summary>
    public string Identity => Shared.Snapshot is null ? "Settings for the next acquisition" : "Acquired settings · locked until Reset";
    public string Readout
    {
        get
        {
            var detector = Shared.Snapshot?.Detector ?? Shared.Detector;
            var optics = Shared.Snapshot?.Optics ?? Shared.Optics;
            if (Shared.IsPhysicalReadout) return $"Experimental GAGG · {optics.DetectorPixels} × {optics.DetectorPixels} matched SiPMs\nPitch {optics.PixelPitchMm:0.###} mm · gap {detector.ReflectorGapMm * 1000:0.###} µm\nDPC row 1000 Ω / column 10 Ω; A–D virtual grounds\nSum trigger 50 keV-equivalent; common hold; specular 0.98\n{PhysicalCounts}";
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
                Face = service?.Build(optics, Shared.IsPhysicalReadout ? detector with { GainSigma = 0 } : detector);
                _faceOptics = optics; _faceDetector = detector;
            }
        }
        else { Face = null; _faceOptics = null; _faceDetector = null; }
        foreach (string name in new[] { nameof(Identity), nameof(Readout), nameof(Counts), nameof(GainLegend), nameof(MinimumGain), nameof(MaximumGain) }) OnPropertyChanged(name);
        foreach (string name in new[] { nameof(Preparation), nameof(PhysicalSnapshot), nameof(Diagnostics), nameof(CalibrationState), nameof(PhysicalCounts) }) OnPropertyChanged(name);
    }
}
