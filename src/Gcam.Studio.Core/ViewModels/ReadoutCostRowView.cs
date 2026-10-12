namespace Gcam.Studio.Core.ViewModels;

/// <summary>One formatted benchmark row: estimate [lower 95, upper 95] for both readouts and F. Formatting only — the
/// values are the shipped record's, verbatim. F carries no colour or badge (colour-neutral by design).</summary>
public sealed record ReadoutCostRowView(string Exposure, string Rate, string DirectRms, string DirectBias,
    string DirectRemote, string DirectAccepted, string AngerRms, string AngerBias, string AngerRemote,
    string AngerAccepted, string F)
{
    /// <summary>Automation-friendly row text.</summary>
    public override string ToString() =>
        $"{Exposure} · {Rate} · Direct RMS {DirectRms}, bias {DirectBias}, remote {DirectRemote}, accepted {DirectAccepted} · Anger RMS {AngerRms}, bias {AngerBias}, remote {AngerRemote}, accepted {AngerAccepted} · F {F}";
}
