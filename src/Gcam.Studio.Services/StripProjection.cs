using Gcam.Core;
using Gcam.Studio.Core.Services;

namespace Gcam.Studio.Services;

/// <summary>Signed CC input and count metadata. The projection's flood parameter remains the clipped display.</summary>
public sealed record StripProjection(DetectorImage Difference, StripCountEstimate Counts);
