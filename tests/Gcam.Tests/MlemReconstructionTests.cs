using System.Text.Json;
using Gcam.Configuration;
using Gcam.Core;
using Gcam.Decoding;
using Gcam.Masks;
using Gcam.Simulation;
using Gcam.Tests.Harness;
using Xunit;

namespace Gcam.Tests;

/// <summary>TODO-36 (MD-1, MD-2): MLEM as a selectable reconstruction. The default (cross-correlation) path must stay bit
/// for bit what it was, and the factory's MLEM must be the claimed decoder of EV-11 bit for bit. Every comparison here is
/// exact: the two sides run the same arithmetic in the same order, so any difference is a construction difference.</summary>
public sealed class MlemReconstructionTests
{
    private static SimulationConfig Studio()
        => SceneConfigBuilder.Build([new SceneSource { Isotope = "Cs-137", X = 0, Y = 0, DistanceMm = 1000 }], new OpticsSettings(), 1);

    private static SimulationConfig HandheldAt1m()
    {
        var c = Rigs.Handheld();
        c.Geometry.SourceMaskDistanceMm = 1000 - c.Geometry.MaskDetectorDistanceMm;
        c.Decoder = new DecoderConfig { Cyclic = false, SubCellInterpolation = SubCellMethod.Tent };
        return c;
    }

    // A deterministic structured count image (no Monte Carlo).
    private static DetectorImage Counts(int w, int h)
    {
        var img = new DetectorImage(w, h);
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++) img[x, y] = 2 + (x * 5 + y * 11) % 9 + ((x + 2 * y) % 3 == 0 ? 6 : 0);
        return img;
    }

    // ------------------------------------------------------------------------------------------------ config

    [Theory]
    [InlineData("\"Mlem\"", 40)]
    [InlineData("\"mlem\"", 7)]
    public void Method_LoadsFromAJsonString_AndSurvivesCloneAndSave(string method, int iterations)
    {
        string path = Path.Combine(Path.GetTempPath(), $"gcam-method-{Guid.NewGuid():N}.json");
        string saved = path + ".saved.json";
        try
        {
            File.WriteAllText(path, $"{{ \"decoder\": {{ \"method\": {method}, \"mlemIterations\": {iterations} }} }}");
            var cfg = ConfigLoader.Load(path);
            Assert.Equal(DecoderMethod.Mlem, cfg.Decoder.Method);
            Assert.Equal(iterations, cfg.Decoder.MlemIterations);
            var clone = cfg.Clone();
            Assert.Equal(DecoderMethod.Mlem, clone.Decoder.Method);
            Assert.Equal(iterations, clone.Decoder.MlemIterations);
            ConfigLoader.Save(cfg, saved);
            Assert.Contains("\"Method\": \"Mlem\"", File.ReadAllText(saved));   // written as a string, not a number
            var reloaded = ConfigLoader.Load(saved);
            Assert.Equal(DecoderMethod.Mlem, reloaded.Decoder.Method);
            Assert.Equal(iterations, reloaded.Decoder.MlemIterations);
        }
        finally { File.Delete(path); File.Delete(saved); }
    }

    [Fact]
    public void Method_AbsentMeansCrossCorrelation_AndOtherEnumsStayNumeric()
    {
        var cfg = ConfigLoader.Load(RepoPaths.Sample("scenario.json"));
        Assert.Equal(DecoderMethod.CrossCorrelation, cfg.Decoder.Method);
        Assert.Equal(120, cfg.Decoder.MlemIterations);
        // The converter is the new enum's own: SubCellInterpolation keeps its numeric form (Save output unchanged).
        string json = JsonSerializer.Serialize(cfg.Decoder);
        Assert.Contains($"\"SubCellInterpolation\":{(int)SubCellMethod.Tent}", json);
        Assert.Contains("\"Method\":\"CrossCorrelation\"", json);
    }

    // ------------------------------------------------------------------------------------------------ factory, default path

    public static TheoryData<string> Configs => new() { "lab", "lab-noncyclic", "handheld", "handheld-1m", "studio" };

    private static SimulationConfig Named(string name) => name switch
    {
        "lab" => Rigs.Lab(),
        "lab-noncyclic" => With(Rigs.Lab(), c => c.Decoder.Cyclic = false),
        "handheld" => Rigs.Handheld(),
        "handheld-1m" => HandheldAt1m(),
        "studio" => Studio(),
        _ => throw new ArgumentException(name)
    };

    private static SimulationConfig With(SimulationConfig c, Action<SimulationConfig> change) { change(c); return c; }

    [Theory]
    [MemberData(nameof(Configs))]
    public void DefaultMethod_IsTheUnchangedCrossCorrelationDecoder(string name)
    {
        var cfg = Named(name);
        var decoder = new DefaultSimulationFactory().CreateDecoder(cfg);
        Assert.IsType<CrossCorrelationDecoder>(decoder);
        // Reference: the pre-change CreateDecoder body, verbatim (git 01dbca6).
        var m = cfg.Mask;
        double maskZ = cfg.Geometry.MaskDetectorDistanceMm, sourceZ = maskZ + cfg.Geometry.SourceMaskDistanceMm;
        double frac = maskZ / sourceZ, period = m.Rank * m.CellPitchMm / frac;
        var geo = new CodedApertureGeometry(m.Rank, maskZ, m.CellPitchMm, m.Rank * m.MosaicX, m.Rank * m.MosaicY, 0.0,
            cfg.Detector.PixelPitchMm, sourceZ, cfg.Decoder.ReconHalfExtentMm ?? period / 2.0, cfg.Decoder.ReconStepMm ?? period / 48.0,
            cfg.Decoder.Cyclic);
        var reference = new CrossCorrelationDecoder(MuraGenerator.DecodingArray(m.Rank), geo, cfg.Decoder.SubCellInterpolation);
        var img = Counts(cfg.Detector.PixelsX, cfg.Detector.PixelsY);
        var a = decoder!.Decode(img);
        var b = reference.Decode(img);
        Assert.Equal(b.Reconstruction.Raw.ToArray(), a.Reconstruction.Raw.ToArray());
        Assert.Equal(b.Estimate.Position, a.Estimate.Position);
        Assert.Equal(b.Estimate.Confidence, a.Estimate.Confidence);
        Assert.Equal(b.ReconOriginMm, a.ReconOriginMm);
        Assert.Equal(b.ReconStepMm, a.ReconStepMm);
    }

    // ------------------------------------------------------------------------------------------------ factory MLEM = study

    [Fact]
    public void FactoryMlem_EqualsTheAngresStudysConstruction_BitForBit()
    {
        // AngularResolutionStudy.BuildMlem("area"): MLEM on the CC search grid, PixelSubSamples from the request (8),
        // transmission ClosedCellTransmission(config), 120 iterations from the request.
        var cc = HandheldAt1m();
        var search = new CorrelationSearch(cc);
        double z = cc.Geometry.MaskDetectorDistanceMm + cc.Geometry.SourceMaskDistanceMm;
        var pattern = MuraGenerator.Mosaic(cc.Mask.Rank, cc.Mask.MosaicX, cc.Mask.MosaicY);
        var geo = new CodedApertureGeometry(cc.Mask.Rank, cc.Geometry.MaskDetectorDistanceMm, cc.Mask.CellPitchMm, pattern.Width, pattern.Height,
            0.0, cc.Detector.PixelPitchMm, z, -search.OriginMm, search.StepMm, cc.Decoder.Cyclic);
        var study = new MlemDecoder(pattern, geo, 120, new MlemSystemModel(8, AngularResolutionStudy.ClosedCellTransmission(cc)));

        var ml = cc.Clone();
        ml.Decoder.Method = DecoderMethod.Mlem;
        var factory = Assert.IsType<MlemDecoder>(new DefaultSimulationFactory().CreateDecoder(ml));

        int w = cc.Detector.PixelsX, h = cc.Detector.PixelsY;
        Assert.Equal(study.SystemMatrix(w, h), factory.SystemMatrix(w, h));
        var img = Counts(w, h);
        var a = factory.Decode(img);
        var b = study.Decode(img);
        Assert.Equal(b.Reconstruction.Raw.ToArray(), a.Reconstruction.Raw.ToArray());
        Assert.Equal(b.ReconOriginMm, a.ReconOriginMm);
        Assert.Equal(b.ReconStepMm, a.ReconStepMm);
        // The factory refines the estimate with the configured sub-cell method; the λ image is the study's.
        var (bx, by) = PeakInterpolation.Argmax(b.Reconstruction);
        var (dx, dy) = PeakInterpolation.Estimate(b.Reconstruction, bx, by, SubCellMethod.Tent);
        Assert.Equal(b.ReconOriginMm + (bx + dx) * b.ReconStepMm, a.Estimate.Position.X);
        Assert.Equal(b.ReconOriginMm + (by + dy) * b.ReconStepMm, a.Estimate.Position.Y);
    }

    [Fact]
    public void PixelSubSamples_IsTheValuePinnedInTheAngresRequests()
    {
        var dir = Path.Combine(RepoPaths.Root, "samples", "evidence", "angres");
        int seen = 0;
        foreach (var file in Directory.GetFiles(dir, "angres-request-*.json"))
        {
            using var doc = JsonDocument.Parse(File.ReadAllText(file));
            var spec = doc.RootElement.GetProperty("AngularResolution");
            if (spec.TryGetProperty("Mlem", out var variants))
                foreach (var v in variants.EnumerateArray())
                    if (v.GetProperty("Kind").GetString() == "area") { Assert.Equal(MlemReconstruction.PixelSubSamples, v.GetProperty("PixelSubSamples").GetInt32()); seen++; }
            if (spec.TryGetProperty("Ladder", out var ladder) && ladder.ValueKind == JsonValueKind.Object && ladder.TryGetProperty("AreaPixelSubSamples", out var s))
            { Assert.Equal(MlemReconstruction.PixelSubSamples, s.GetInt32()); seen++; }
        }
        Assert.True(seen > 0, "no pixel-area variant found in the angres requests");
    }

    [Fact]
    public void ClosedCellTransmission_IsShared_AndFollowsTheLineEnergy()
    {
        var c = Rigs.Handheld();
        Assert.Equal(MlemReconstruction.ClosedCellTransmission(c, c.Source.EnergyKeV), AngularResolutionStudy.ClosedCellTransmission(c));
        // At the table's 662 keV anchor μ_rel = 1 exactly: exp(−0.178 · 10).
        Assert.Equal(Math.Exp(-0.178 * 1.0 * 10.0), MlemReconstruction.ClosedCellTransmission(c, 662.0));
        // Higher lines leak more (μ falls with energy): Co-60's lines above Cs-137's.
        Assert.True(MlemReconstruction.ClosedCellTransmission(c, 1173.2) > MlemReconstruction.ClosedCellTransmission(c, 661.7));
        var factory = new DefaultSimulationFactory();
        int w = c.Detector.PixelsX, h = c.Detector.PixelsY;
        var co = factory.CreateMlemDecoder(c, 1173.2).SystemMatrix(w, h);
        var geo = DefaultSimulationFactory.ReconstructionGeometry(c);
        var expected = new MlemDecoder(MuraGenerator.Mosaic(c.Mask.Rank, c.Mask.MosaicX, c.Mask.MosaicY), geo, 120,
            new MlemSystemModel(8, MlemReconstruction.ClosedCellTransmission(c, 1173.2))).SystemMatrix(w, h);
        Assert.Equal(expected, co);
    }

    [Fact]
    public void Mlem_UsesTheInvertedMosaic_WhenTheMaskIsInverted()
    {
        var c = Rigs.Lab();
        c.Mask.Invert = true;
        c.Decoder.Method = DecoderMethod.Mlem;
        var p = MuraGenerator.Mosaic(c.Mask.Rank, c.Mask.MosaicX, c.Mask.MosaicY);
        var inv = new MaskPattern(p.Width, p.Height);
        for (int x = 0; x < p.Width; x++) for (int y = 0; y < p.Height; y++) inv[x, y] = !p[x, y];
        var geo = DefaultSimulationFactory.ReconstructionGeometry(c);
        var model = new MlemSystemModel(8, MlemReconstruction.ClosedCellTransmission(c, c.Source.EnergyKeV));
        int w = c.Detector.PixelsX, h = c.Detector.PixelsY;
        var got = ((MlemDecoder)new DefaultSimulationFactory().CreateDecoder(c)!).SystemMatrix(w, h);
        Assert.Equal(new MlemDecoder(inv, geo, 120, model).SystemMatrix(w, h), got);
        Assert.NotEqual(new MlemDecoder(p, geo, 120, model).SystemMatrix(w, h), got);
    }

    // ------------------------------------------------------------------------------------------------ estimate

    [Theory]
    [InlineData("handheld-1m")]
    [InlineData("studio")]
    public void FactoryMlem_NoiselessModelDataAtAGridNode_PeaksAtThatNode(string name)
    {
        // Data = A·e_k for a grid point k whose column is unique (the model's own image of a point source at k). MLEM's
        // fixed point for consistent data reproduces them; the argmax is k exactly (MlemOptionTests' far-source check,
        // here through the factory at the configured iteration count).
        var cfg = Named(name);
        cfg.Decoder.Method = DecoderMethod.Mlem;
        var dec = Assert.IsType<MlemDecoder>(new DefaultSimulationFactory().CreateDecoder(cfg));
        int w = cfg.Detector.PixelsX, h = cfg.Detector.PixelsY, nDet = w * h;
        var a = dec.SystemMatrix(w, h);
        int nSrc = a.Length / nDet, n = (int)Math.Round(Math.Sqrt(nSrc));
        bool Unique(int k)
        {
            var c = a.AsSpan(k * nDet, nDet);
            for (int j = 0; j < nSrc; j++) if (j != k && a.AsSpan(j * nDet, nDet).SequenceEqual(c)) return false;
            return true;
        }
        int kIndex = Enumerable.Range(3, n / 2 - 6).Select(dx => (n / 2 + 2) * n + n / 2 - dx).First(Unique);
        var img = new DetectorImage(w, h);
        for (int i = 0; i < nDet; i++) img[i % w, i / w] = 1000 * a[kIndex * nDet + i];
        var r = dec.Decode(img);
        Assert.Equal((kIndex % n, kIndex / n), PeakInterpolation.Argmax(r.Reconstruction));
    }

    [Fact]
    public void SubCellArgument_NoneIsTheOriginalDecoder_TentRefinesTheReturnedImage()
    {
        var (cfg, img) = (HandheldAt1m(), Counts(16, 16));
        var geo = DefaultSimulationFactory.ReconstructionGeometry(cfg);
        var p = MuraGenerator.Mosaic(cfg.Mask.Rank, cfg.Mask.MosaicX, cfg.Mask.MosaicY);
        var model = new MlemSystemModel(8, 0.17);
        var original = new MlemDecoder(p, geo, 60, model).Decode(img);
        var none = new MlemDecoder(p, geo, 60, model, SubCellMethod.None).Decode(img);
        var tent = new MlemDecoder(p, geo, 60, model, SubCellMethod.Tent).Decode(img);
        Assert.Equal(original.Reconstruction.Raw.ToArray(), none.Reconstruction.Raw.ToArray());
        Assert.Equal(original.Estimate, none.Estimate);
        Assert.Equal(original.Reconstruction.Raw.ToArray(), tent.Reconstruction.Raw.ToArray());
        var (bx, by) = PeakInterpolation.Argmax(tent.Reconstruction);
        var (dx, dy) = PeakInterpolation.Estimate(tent.Reconstruction, bx, by, SubCellMethod.Tent);
        Assert.Equal(tent.ReconOriginMm + (bx + dx) * tent.ReconStepMm, tent.Estimate.Position.X);
        Assert.Equal(tent.ReconOriginMm + (by + dy) * tent.ReconStepMm, tent.Estimate.Position.Y);
        Assert.Equal(original.Estimate.Confidence, tent.Estimate.Confidence);
    }

    // ------------------------------------------------------------------------------------------------ guards

    [Fact]
    public void CorrelationSearch_RefusesAnMlemConfig()
    {
        var cfg = HandheldAt1m();
        cfg.Decoder.Method = DecoderMethod.Mlem;
        var ex = Assert.Throws<ArgumentException>(() => new CorrelationSearch(cfg));
        Assert.Contains("cross-correlation", ex.Message);
    }

    [Fact]
    public void SingleRun_DecodesWithMlem_WhenTheScenarioSelectsIt()
    {
        var cfg = Rigs.Handheld(photons: 50_000);
        cfg.Decoder.Method = DecoderMethod.Mlem;
        cfg.Decoder.MlemIterations = 20;
        var result = new SimulationRunner(new DefaultSimulationFactory()).Run(cfg);
        Assert.NotNull(result.Reconstruction);
        Assert.All(result.Reconstruction!.Raw.ToArray(), v => Assert.True(v >= 0));   // λ is non-negative; CC is not
        var cc = new SimulationRunner(new DefaultSimulationFactory()).Run(Rigs.Handheld(photons: 50_000));
        Assert.Contains(cc.Reconstruction!.Raw.ToArray(), v => v < 0);
    }
}
