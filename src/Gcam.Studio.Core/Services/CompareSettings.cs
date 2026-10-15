namespace Gcam.Studio.Core.Services;

/// <summary>Captured Imaging choices; Window selects its explicit physical energy window.</summary>
public sealed record CompareSettings(ImagingSettings Imaging, bool Window);
