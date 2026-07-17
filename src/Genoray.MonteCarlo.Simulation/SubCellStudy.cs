using Genoray.MonteCarlo.Configuration;
using Genoray.MonteCarlo.Core;

namespace Genoray.MonteCarlo.Simulation;

/// <summary>One true sub-grid source position and the signed X localization error of each estimator.</summary>
public sealed record SubCellRow(
    double TrueXMm,
    double ErrNoneMm,       // raw argmax  — the quantization sawtooth
    double ErrParabolicMm,
    double ErrTentMm,
    double ErrGaussianMm);

/// <summary>Per-estimator summary over the whole sweep: RMS and mean-|bias| of the X localization error.</summary>
public sealed record SubCellSummary(
    double ReconStepMm,
    double QuantFloorMm,    // step/√12 — the RMS a uniform sub-cell offset gives with NO interpolation
    double RmsNoneMm, double RmsParabolicMm, double RmsTentMm, double RmsGaussianMm,
    double BiasNoneMm, double BiasParabolicMm, double BiasTentMm, double BiasGaussianMm);

/// <summary>
/// Studies SUB-CELL peak interpolation: how far below the recon-grid step the source can be localized. The decoder
/// samples the source plane on a grid of spacing <c>ReconStepMm</c> and (with no refinement) reports the integer
/// argmax cell, so a source lying between grid points is snapped to the nearest cell — a deterministic sawtooth
/// error of amplitude ±step/2, RMS = step/√12, that no amount of counts removes. Interpolating the correlation-peak
/// SHAPE around the argmax recovers a fractional offset and beats that floor.
///
/// The study sweeps a point source across the grid in fine sub-cell steps (Y fixed on-axis) and, from ONE noise-free
/// mean detector image per position, decodes with each estimator (<see cref="SubCellMethod"/>) so the quantization
/// sawtooth (None) and its collapse (interpolated) are isolated from Poisson noise. It runs at a deliberately COARSE
/// recon step — the regime where the grid is kept coarse for speed and interpolation actually earns its keep — and
/// reports RMS/bias per method so the DATA, not a prior, picks the best estimator.
/// </summary>
public sealed class SubCellStudy
{
    private readonly ISimulationFactory _factory;

    public SubCellStudy(ISimulationFactory factory)
    {
        _factory = factory;
    }

    public (SubCellRow[] rows, SubCellSummary summary) Run(
        SimulationConfig baseConfig, double reconStepMm, double sweepHalfWidthMm, int samples,
        double sourceYMm, int photonCount)
    {
        // Geometry (hence the recon grid) is independent of where the source sits laterally, so build the four
        // decoders — identical but for the interpolation method — once and reuse them across the sweep.
        var geoCfg = baseConfig.Clone();
        geoCfg.Decoder.ReconStepMm = reconStepMm;
        SubCellMethod[] methods = [SubCellMethod.None, SubCellMethod.Parabolic, SubCellMethod.Tent, SubCellMethod.Gaussian];
        var decoders = new IDecoder[methods.Length];
        for (int m = 0; m < methods.Length; m++)
        {
            var cfgM = geoCfg.Clone();
            cfgM.Decoder.SubCellInterpolation = methods[m];
            decoders[m] = _factory.CreateDecoder(cfgM)!;
        }

        double sourceZ = baseConfig.Source.Position is { Length: >= 3 } p ? p[2]
            : baseConfig.Geometry.MaskDetectorDistanceMm + baseConfig.Geometry.SourceMaskDistanceMm;

        var rows = new List<SubCellRow>(samples);
        var sumSq = new double[methods.Length];
        var sumAbs = new double[methods.Length];
        for (int i = 0; i < samples; i++)
        {
            // Sweep true X across ~2 recon cells so the sawtooth (period = step) shows fully.
            double tx = samples > 1 ? -sweepHalfWidthMm + 2.0 * sweepHalfWidthMm * i / (samples - 1) : 0.0;

            var cfg = baseConfig.Clone();
            cfg.Decoder.ReconStepMm = reconStepMm;
            cfg.PhotonCount = photonCount;
            cfg.Source.Position = [tx, sourceYMm, sourceZ];

            var img = new SimulationRunner(_factory).Run(cfg).DetectorImage;

            var err = new double[methods.Length];
            for (int m = 0; m < methods.Length; m++)
            {
                double estX = decoders[m].Decode(img).Estimate.Position.X;
                err[m] = estX - tx;
                sumSq[m] += err[m] * err[m];
                sumAbs[m] += Math.Abs(err[m]);
            }
            rows.Add(new SubCellRow(tx, err[0], err[1], err[2], err[3]));
        }

        double Rms(int m) => Math.Sqrt(sumSq[m] / samples);
        double Bias(int m) => sumAbs[m] / samples;
        var summary = new SubCellSummary(
            reconStepMm, reconStepMm / Math.Sqrt(12.0),
            Rms(0), Rms(1), Rms(2), Rms(3),
            Bias(0), Bias(1), Bias(2), Bias(3));
        return (rows.ToArray(), summary);
    }

    public static string ToCsv(SubCellRow[] rows)
    {
        var sb = new System.Text.StringBuilder();
        sb.AppendLine("true_x_mm,err_none_mm,err_parabolic_mm,err_tent_mm,err_gaussian_mm");
        foreach (var r in rows)
            sb.AppendLine($"{r.TrueXMm:F4},{r.ErrNoneMm:F4},{r.ErrParabolicMm:F4},{r.ErrTentMm:F4},{r.ErrGaussianMm:F4}");
        return sb.ToString();
    }
}
