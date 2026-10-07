using Gcam.Configuration;
using Gcam.Core;
using Gcam.Decoding;
using Gcam.Masks;

namespace Gcam.Simulation;

/// <summary>The claimed MLEM (D-46, EV-11) as one construction shared by the factory, GCAM Studio and the angres study:
/// the pixel-area forward model with <see cref="PixelSubSamples"/>² samples per pixel, closed cells and the frame
/// transmitting <see cref="ClosedCellTransmission"/> at the imaged line, the physical mosaic (inverted when the mask is)
/// and the cross-correlation decoder's reconstruction grid.</summary>
public static class MlemReconstruction
{
    /// <summary>Samples per pixel side of the pixel-area model. The angres evidence requests pin the same value
    /// (<c>PixelSubSamples</c> / <c>AreaPixelSubSamples</c> = 8, checked by a test); at Studio's default optics it samples a
    /// 0.76 mm projected cell about ten times per edge.</summary>
    public const int PixelSubSamples = 8;

    /// <summary>exp(−μ·t) of the mask slab at <paramref name="lineEnergyKeV"/> (the config's 662 keV μ scaled by tungsten's
    /// μ(E) / μ(662)). Thin-mask model: no oblique channel clipping.</summary>
    public static double ClosedCellTransmission(SimulationConfig config, double lineEnergyKeV)
        => Math.Exp(-config.Mask.LinearAttenuationPerMm * CodedApertureMask.TungstenMuRel(lineEnergyKeV) * config.Mask.ThicknessMm);

    /// <summary>The open/closed pattern the photons actually see: the mosaic, complemented when <c>Mask.Invert</c>.
    /// (Fabrication errors stay unknown to the decoder, as for cross-correlation.)</summary>
    public static MaskPattern ForwardPattern(SimulationConfig config)
    {
        var m = config.Mask;
        var pattern = MuraGenerator.Mosaic(m.Rank, m.MosaicX, m.MosaicY);
        if (!m.Invert) return pattern;
        var inverted = new MaskPattern(pattern.Width, pattern.Height);
        for (int x = 0; x < pattern.Width; x++)
            for (int y = 0; y < pattern.Height; y++)
                inverted[x, y] = !pattern[x, y];
        return inverted;
    }

    /// <summary>The pixel-area MLEM on <paramref name="geometry"/> for the line at <paramref name="lineEnergyKeV"/>.</summary>
    public static MlemDecoder Create(SimulationConfig config, CodedApertureGeometry geometry, double lineEnergyKeV, int iterations,
        SubCellMethod subCell)
    {
        ArgumentNullException.ThrowIfNull(config);
        if (iterations < 1) throw new ArgumentOutOfRangeException(nameof(iterations), "MLEM needs at least one iteration.");
        if (!(lineEnergyKeV > 0) || !double.IsFinite(lineEnergyKeV)) throw new ArgumentOutOfRangeException(nameof(lineEnergyKeV));
        return new MlemDecoder(ForwardPattern(config), geometry, iterations,
            new MlemSystemModel(PixelSubSamples, ClosedCellTransmission(config, lineEnergyKeV)), subCell);
    }
}
