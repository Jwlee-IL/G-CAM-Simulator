using System.Globalization;

namespace Gcam.Studio.Core.Services;

/// <summary>Real preparation progress of one line: the stage and, for the streamed flood stages, histories done of the
/// stage's total (0 / 0 for a stage without a count).</summary>
public sealed record ReadoutPrepareProgress(double LineKeV, string Stage, long Done, long Total)
{
    public double Fraction => Total > 0 ? Math.Clamp((double)Done / Total, 0, 1) : 0;

    public override string ToString() => Total > 0
        ? string.Create(CultureInfo.InvariantCulture, $"{LineKeV:0.##} keV · {Stage} · {Done:N0} / {Total:N0} histories ({Fraction:P0})")
        : string.Create(CultureInfo.InvariantCulture, $"{LineKeV:0.##} keV · {Stage}");
}
