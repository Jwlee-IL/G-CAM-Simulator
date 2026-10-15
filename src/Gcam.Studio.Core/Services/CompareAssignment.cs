using Gcam.Core;

namespace Gcam.Studio.Core.Services;

/// <summary>Integer denominators are retained; a NaN map cell means no eligible conversions.</summary>
public sealed record CompareAssignment(DetectorImage Difference, IReadOnlyList<long> Eligible, IReadOnlyList<long> Different);
