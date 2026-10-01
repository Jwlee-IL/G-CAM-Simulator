using Gcam.Studio.Core.Imaging;

namespace Gcam.Studio.Core.ViewModels;

/// <summary>A finished gesture from the overlay: what to measure, on which image, at which mm points.</summary>
public sealed record MeasurementDraft(ImagePane Pane, MeasurementKind Kind, IReadOnlyList<Vec2> PointsMm);
