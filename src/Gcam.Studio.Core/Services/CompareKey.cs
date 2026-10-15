namespace Gcam.Studio.Core.Services;

/// <summary>Identity of an atomic pair, including the acquisition and view settings that produced it.</summary>
public sealed record CompareKey(Guid AcquisitionId, int SettingsRevision, int HitCount, int HeldCount, double LiveTimeS);
