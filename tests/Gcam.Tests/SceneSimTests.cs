using Gcam.Configuration;
using Gcam.Simulation;
using Xunit;

namespace Gcam.Tests;

/// <summary>Exercises the exact simulation path the WPF scene editor drives: a multi-source mixed field with
/// per-source distance (Position[2]), activity (Bq), and isotope lines — run + decode (Imaging) and streamed
/// (Spectrum/Waveform). Guards that the scene → config → sim wiring stays runnable end to end.</summary>
public class SceneSimTests
{
    // Mirror MainWindow.ConfigFromScene: sources carry x,y,distance and activity×line weights.
    private static SimulationConfig SceneConfig(long photons)
    {
        SourceConfig Src(string iso, double x, double y, double dist, double uCi, EmissionLine[] lines) => new()
        {
            Isotope = iso,
            Position = [x, y, dist],
            ActivityBq = uCi * 3.7e4,
            DirectionalBiasing = true,
            EnergyKeV = lines[0].EnergyKeV,
            BranchingRatio = lines[0].Intensity,
            Lines = lines,
        };
        var cs = Src("Cs-137", 5, 0, 160, 10, [new EmissionLine { EnergyKeV = 661.7, Intensity = 0.851 }]);
        var co = Src("Co-60", -5, 0, 220, 5,
            [new EmissionLine { EnergyKeV = 1173.2, Intensity = 0.999 },
             new EmissionLine { EnergyKeV = 1332.5, Intensity = 0.999 }]);
        return new SimulationConfig { PhotonCount = photons, Seed = 12345, Source = cs, Sources = [cs, co] };
    }

    [Fact]
    public void MixedScene_RunsAndDecodes()
    {
        var cfg = SceneConfig(300_000);
        var factory = new DefaultSimulationFactory();
        var res = new SimulationRunner(factory).Run(cfg);
        Assert.True(res.DetectedWeight > 0, "the mixed scene should detect some counts");

        var dec = factory.CreateDecoder(cfg)!.Decode(res.DetectorImage);
        Assert.NotNull(dec.Reconstruction);
        Assert.True(double.IsFinite(dec.Estimate.Position.X) && double.IsFinite(dec.Estimate.Position.Y));
    }

    [Fact]
    public void MixedScene_StreamsDepositSpectrum()
    {
        // The Spectrum/Waveform path: EventStreamStudy taps the per-event deposits for the mixed field.
        var cfg = SceneConfig(200_000);
        var events = new EventStreamStudy().Generate(cfg, countRateCps: 100_000, adcSampleRateHz: 125e6, maxEvents: 2000);
        Assert.Equal(2000, events.Count);
        // A Cs+Co field deposits across a wide energy span (122–1332 keV cascade), not a single line.
        Assert.Contains(events, ev => ev.EnergyKeV > 700);   // Co-60 lines land above the Cs 662
    }
}
