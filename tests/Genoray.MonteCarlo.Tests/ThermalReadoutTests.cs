using System.Linq;
using Genoray.MonteCarlo.Configuration;
using Genoray.MonteCarlo.Detector;
using Genoray.MonteCarlo.Simulation;
using Xunit;

namespace Genoray.MonteCarlo.Tests;

/// <summary>
/// Thermal readout beyond the gain-centroid drift (theme 36): SiPM dark-count rate doubles every ~8 °C and — unlike
/// the gain — is NOT nulled by bias compensation, so it climbs with temperature. Its parallel-noise term is ∝1/E, so
/// it degrades LOW-energy resolution more (relatively) than the photopeak; PDE also droops mildly.
/// </summary>
public class ThermalReadoutTests
{
    [Fact]
    public void Dcr_DoublesEveryDoublingInterval_AndBiasCompDoesNotNullIt()
    {
        var t = new ThermalDrift(12, 12, biasCompFraction: 1.0, dcrDoublingC: 8.0);   // perfect gain compensation
        Assert.Equal(2.0, t.DcrFactor(8.0), 3);
        Assert.Equal(4.0, t.DcrFactor(16.0), 3);
        // Perfect bias comp nulls the GAIN drift...
        Assert.Equal(0.0, t.CentroidShift(6, 6, 1.0), 9);
        // ...but the thermal dark generation still climbs.
        Assert.True(t.DcrFactor(16.0) > 3.9, "bias comp must NOT null DCR growth");
    }

    [Fact]
    public void DcrHitsLowEnergyResolutionMoreThanThePhotopeak_AndPdeDroops()
    {
        var fe = new FrontEndConfig { DarkCountRateHz = 1.0e6, IntegrationTimeNs = 300, IntrinsicResolutionFwhm = 0.03 };
        var thermal = new ThermalDrift(12, 12);
        var rows = new ThermalReadoutStudy().Run(fe, thermal, [0.0, 30.0], lowLineKeV: 60.0, photopeakKeV: 661.7);
        var cold = rows[0];
        var hot = rows[1];

        // DCR climbs sharply with temperature.
        Assert.True(hot.DcrHz > 10.0 * cold.DcrHz, $"DCR should climb ~13x over 30C, got {hot.DcrHz / cold.DcrHz:F1}x");
        // The 1/E dark-noise term degrades the low line's resolution MORE (relatively) than the photopeak's.
        double lowRel = (hot.ResLowEPct - cold.ResLowEPct) / cold.ResLowEPct;
        double peakRel = (hot.ResPhotopeakPct - cold.ResPhotopeakPct) / cold.ResPhotopeakPct;
        Assert.True(lowRel > peakRel, $"low-E resolution should degrade more (rel): low {lowRel:P2} vs peak {peakRel:P2}");
        Assert.True(hot.ResLowEPct >= cold.ResLowEPct && hot.ResPhotopeakPct >= cold.ResPhotopeakPct);
        // PDE droops with temperature (follows the over-voltage).
        Assert.True(hot.PdeFactor < 1.0 && hot.PdeFactor > 0.9);
    }
}
