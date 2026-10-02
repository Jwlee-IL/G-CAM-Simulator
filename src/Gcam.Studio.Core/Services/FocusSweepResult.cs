namespace Gcam.Studio.Core.Services;

public sealed record FocusSweepResult(FocusSweepIdentity Identity, IReadOnlyList<FocusTrack> Tracks, TimeSpan ProcessingTime);
