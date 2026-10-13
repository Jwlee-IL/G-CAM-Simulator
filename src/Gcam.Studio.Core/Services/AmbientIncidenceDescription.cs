using Gcam.Configuration;

namespace Gcam.Studio.Core.Services;

/// <summary>The captured transport construction, distinct from an ambient efficiency claim.</summary>
public static class AmbientIncidenceDescription
{
    public static string For(AmbientGeometry geometry, bool physical)
        => geometry == AmbientGeometry.FrontOnlyThroughMask ? "Front-only through the mask"
            : physical ? "Unmasked all-face module approximation" : "Bare crystal on all faces";

    public const string PhysicalHelp = "Nominal-envelope sampling with module gaps and lumped steel-equivalent front entrance / rear backing slabs; no side housing, explicit passive volumes or angle-dependent passive path. The unmasked all-face module approximation is not a bare-crystal upper bound. DirectCrystal retains its homogeneous bare-envelope construction.";
}
