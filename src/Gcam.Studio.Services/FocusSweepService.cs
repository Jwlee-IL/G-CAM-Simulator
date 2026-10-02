using System.Diagnostics;
using Gcam.Configuration;
using Gcam.Studio.Core.Imaging;
using Gcam.Studio.Core.Optics;
using Gcam.Studio.Core.Services;

namespace Gcam.Studio.Services;

/// <summary>Independent projection worker with no acquisition gate, calibration or transport.</summary>
public sealed class FocusSweepService : IFocusSweepService
{
    public Task<FocusSweepResult> SweepAsync(FocusSweepRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var id = request.Identity;
        if (id.PeakCount is < 1 or > 4) throw new ArgumentOutOfRangeException(nameof(request));
        var optics = id.Optics;
        var planes = FocusSweepMath.Planes(optics.MaskDetectorDistanceMm, id.PlaneCount);
        string? error = OpticsPolicy.Validate(optics, id.Detector.ReflectorGapMm);
        foreach (double plane in planes) error ??= OpticsPolicy.ValidateFocus(optics, plane);
        if (error is not null) throw new ArgumentException(error, nameof(request));
        if (request.Image.Flood.Width != optics.DetectorPixels || request.Image.Flood.Height != optics.DetectorPixels)
            throw new ArgumentException("Flood must match acquired optics.", nameof(request));
        // Build projection geometry without any scene truth. AtFocus clones before changing a plane.
        var config = SceneConfigBuilder.Build([new SceneSource { DistanceMm = planes[0] }], optics, 1);
        config.Decoder.Cyclic = false;
        return Task.Run(() =>
        {
            var watch = Stopwatch.StartNew();
            var tracks = new List<List<FocusSample>>();
            foreach (double plane in planes)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var projection = ImagingProjection.AtFocus(config, optics, plane);
                var channel = ImagingProjection.Project(request.Image.Flood, request.Image, projection, id.Channel, id.PeakCount);
                if (channel.Image.Reconstruction is not { } recon) continue;
                var values = recon.Raw.ToArray();
                double mean = values.Average();
                double std = Math.Sqrt(values.Select(v => (v - mean) * (v - mean)).Average());
                if (std == 0) continue; // An empty/constant field has no defined prominence or depth.
                var candidates = channel.Peaks.Select(p => new FocusSample(plane, p.Xmm, p.Ymm,
                    Math.Max(0, (p.Value - mean) / std))).ToArray();
                if (tracks.Count == 0) foreach (var candidate in candidates) tracks.Add([candidate]);
                else
                {
                    var links = FocusSweepMath.Link(tracks.Select(t => t[^1]).ToArray(), candidates.Take(tracks.Count).ToArray());
                    for (int i = 0; i < links.Length; i++) tracks[links[i]].Add(candidates[i]);
                }
            }
            cancellationToken.ThrowIfCancellationRequested();
            return new FocusSweepResult(id, Array.AsReadOnly(tracks.Where(t => t.Count == planes.Length)
                .Select(t => FocusSweepMath.Describe(t)).ToArray()), watch.Elapsed);
        }, cancellationToken);
    }
}
