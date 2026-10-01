using Gcam.Configuration;

namespace Gcam.Studio.Core.Optics;

/// <summary>Analytical geometry values, not performance or usable-field guarantees.</summary>
public sealed record OpticsGeometry(double ResolutionElementMm, double NominalHalfFieldMm,
    double SamplesPerCell, double CoveragePeriods, double MaskWidthMm)
{
    public static OpticsGeometry Calculate(OpticsSettings optics, double focalDistanceMm)
    {
        double resolution = optics.CellPitchMm * (focalDistanceMm / optics.MaskDetectorDistanceMm);
        double shadow = optics.CellPitchMm * (focalDistanceMm / (focalDistanceMm - optics.MaskDetectorDistanceMm));
        return new(resolution, optics.MuraRank * resolution / 2, shadow / optics.PixelPitchMm,
            optics.DetectorPixels * optics.PixelPitchMm / (optics.MuraRank * shadow),
            2 * optics.MuraRank * optics.CellPitchMm);
    }

    public string Description => $"Resolution element {ResolutionElementMm:0.##} mm\n" +
        $"Nominal cyclic field ± {NominalHalfFieldMm:0.#} mm\n" +
        $"{SamplesPerCell:0.##} samples per mask cell\n" +
        $"Coverage {CoveragePeriods:0.##} mask periods\nMask width {MaskWidthMm:0.##} mm";
}
