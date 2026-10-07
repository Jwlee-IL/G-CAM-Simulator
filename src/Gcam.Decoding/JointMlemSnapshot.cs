namespace Gcam.Decoding;

/// <summary>A source-grid iterate with a separate nuisance background amplitude. No element of Source represents the
/// background. Likelihood/expected-total diagnostics are evaluated in double precision when requested.</summary>
public sealed record JointMlemSnapshot(int Iterations, double[] Source, double BackgroundCounts,
    double? LogLikelihood, double ExpectedTotalCounts);
