using Gcam.Configuration;

namespace Gcam.Tests;

public sealed class FrontEndPartsTests
{
    [Fact]
    public void Presets_BuildTheLegacyConfig_DefaultMatchesStartupSelection()
    {
        Assert.Same(FrontEndParts.Scintillators[0], FrontEndParts.Default.Scintillator);
        Assert.Same(FrontEndParts.Sensors[0], FrontEndParts.Default.Sensor);
        Assert.Same(FrontEndParts.Preamps[1], FrontEndParts.Default.Preamp);
        foreach (var sc in FrontEndParts.Scintillators)
        foreach (var se in FrontEndParts.Sensors)
        foreach (var pa in FrontEndParts.Preamps)
        {
            var config = FrontEndParts.BuildConfig(sc, se, pa);
            Assert.Equal(sc.LightYieldPhPerKeV, config.LightYieldPhPerKeV);
            Assert.Equal(0.50, config.CollectionEfficiency);
            Assert.Equal(se.Pde, config.SipmPde);
            Assert.Equal(se.Enf, config.ExcessNoiseFactor);
            Assert.Equal(sc.NonPropFwhm, config.IntrinsicResolutionFwhm);
            Assert.Equal(se.DcrHz, config.DarkCountRateHz);
            Assert.Equal(pa.IntegrationNs, config.IntegrationTimeNs);
        }
    }

    [Fact]
    public void PulseSamples_MatchesLegacyConvolutionAndSampleGuards()
    {
        foreach (var sc in FrontEndParts.Scintillators)
        foreach (var pa in FrontEndParts.Preamps)
        foreach (double fs in new[] { 125e6, 1e6, 1e9 })
        {
            double nsPerSample = 1e9 / fs;
            double rise = Math.Max(0.5, Math.Min(sc.DecayNs, pa.PulseTailNs) / nsPerSample);
            double tail = Math.Max(rise + 0.5, Math.Max(sc.DecayNs, pa.PulseTailNs) / nsPerSample);
            Assert.Equal((rise, tail), FrontEndParts.PulseSamples(sc, pa, fs));
        }
        Assert.Equal((11.25, 40.0), FrontEndParts.Default.PulseSamples);
    }
}
