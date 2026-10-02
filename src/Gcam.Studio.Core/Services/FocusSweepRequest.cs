namespace Gcam.Studio.Core.Services;

/// <summary>Selected immutable flood and acquired projection inputs; source truth is absent.</summary>
public sealed record FocusSweepRequest(FocusSweepIdentity Identity, ImagingResult Image);
