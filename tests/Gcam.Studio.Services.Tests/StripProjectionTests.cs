using Gcam.Core;
using Gcam.Simulation;
using Gcam.Studio.Core.Services;
using Gcam.Configuration;

namespace Gcam.Studio.Services.Tests;

public sealed class StripProjectionTests
{
    [Theory]
    [InlineData(-5)]
    [InlineData(0)]
    public void SignedZeroOrNegativeNet_DecodesWithRawSupport(double net)
    {
        var config = SimulationService.BuildConfig([new()],new(),new());
        var display = new DetectorImage(config.Detector.PixelsX,config.Detector.PixelsY);
        var signed = new DetectorImage(display.Width,display.Height);
        signed[0,0]=net;
        var original = new ImagingResult(display,0,1,null,0,0,null,0,TimeSpan.Zero);
        var channel = ImagingProjection.Project(display,original,config,"Cs-137",1,600,720,null,
            new(signed,new(net,4,true)));
        var expected = new DefaultSimulationFactory().CreateDecoder(config)!.Decode(signed);
        Assert.Equal(expected.Reconstruction.Raw.ToArray(),channel.Image.Reconstruction!.Raw.ToArray());
        Assert.Single(channel.Peaks);
        Assert.Equal(net,channel.Image.EffectiveCounts);
        Assert.Same(display,channel.Image.Flood);
        Assert.True(channel.Image.HasData);
    }

    [Fact]
    public void EmptyRawWindows_DoNotCreateAReconstruction()
    {
        var config = SimulationService.BuildConfig([new()],new(),new());
        var empty = new DetectorImage(config.Detector.PixelsX,config.Detector.PixelsY);
        var original = new ImagingResult(empty,0,1,null,0,0,null,0,TimeSpan.Zero);
        var channel = ImagingProjection.Project(empty,original,config,"Cs-137",1,600,720,null,
            new(empty,new(0,0,false)));
        Assert.Null(channel.Image.Reconstruction);
        Assert.Empty(channel.Peaks);
        Assert.False(channel.Image.HasData);
    }

    [Fact]
    public void Mlem_HighOnlyRawSupport_KeepsNegativeNetWithoutADetectionGate()
    {
        var config = SimulationService.BuildConfig([new()],new(),new());
        config.Decoder.MlemIterations = 1; // Data-presence regression, independent of convergence/precision.
        var empty = new DetectorImage(config.Detector.PixelsX,config.Detector.PixelsY);
        var difference = new DetectorImage(empty.Width,empty.Height);
        difference[0,0] = -5;
        var background = new double[empty.Width*empty.Height]; background[0]=5;
        var original = new ImagingResult(empty,0,1,null,0,0,null,0,TimeSpan.Zero);
        var mlem = new MlemProjection(new MlemDecoderCache(),661.7,empty,background);
        var channel = ImagingProjection.Project(empty,original,config,"Cs-137",1,600,720,mlem,
            new(difference,new(-5,4,true)));
        Assert.NotNull(channel.Image.Reconstruction);
        Assert.Single(channel.Peaks);
        Assert.Equal(-5,channel.Image.EffectiveCounts);
        Assert.True(channel.Image.HasData);
    }
}
