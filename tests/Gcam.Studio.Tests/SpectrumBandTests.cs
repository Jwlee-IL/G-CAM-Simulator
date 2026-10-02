using Gcam.Configuration;
using Gcam.Studio.Core.Services;

namespace Gcam.Studio.Tests;

public sealed class SpectrumBandTests
{
    private static SpectrumLine[] CsLines => Isotopes.Get("Cs-137").Lines
        .Select(l => new SpectrumLine("Cs-137", l.EnergyKeV, l.Kind, l.XRayOrigin)).ToArray();

    [Fact]
    public void BandOfXRayLines_IsNamedByEmitter_GammaBandByIsotope()
    {
        // Line kinds come from the engine's isotope table; the Studio keeps no isotope data of its own.
        var lines = CsLines;
        var xrays = new SpectrumBand(lines.Where(l => l.EnergyKeV < 100).ToArray(), 25, 44, 10, 0.1);
        var gamma = new SpectrumBand(lines.Where(l => l.EnergyKeV > 600).ToArray(), 616, 707, 20, 0.2);
        Assert.Equal("Ba K X-rays (Cs-137)", xrays.Name);
        Assert.Equal("Cs-137", gamma.Name);
        Assert.StartsWith("Ba K X-rays (Cs-137): 32.1 + 36.4 keV", xrays.Description);
    }
}
