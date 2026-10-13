using System.Globalization;
using Gcam.Studio.Core.Services;

namespace Gcam.Studio.Core.ViewModels;

/// <summary>One calibration line of the current module as the Readout and Detector panels show it: the confirmed offer
/// or refusal (from the shipped record) kept separate from the local preparation state of this session. Inline the row
/// shows only the line, the status label (experimental / refused / local / unavailable) with a short reason, and the
/// local state; provenance and the full first-failure message are in <see cref="Details"/> (the row's help mark).</summary>
public sealed record ReadoutLineRow(ReadoutLinePlan Plan, bool InScene, string Offer, string Local)
{
    public double LineKeV => Plan.LineKeV;
    public ReadoutLineStatus Status => Plan.Status;

    private string Line => LineKeV.ToString("0.##", CultureInfo.InvariantCulture) + " keV";

    // Table cells of the Readout panel (line · record · local). They carry the same facts as Summary and Local, split
    // into a status word and its detail; non-breaking spaces keep each detail fragment ("144 / 144 peaks") on one line
    // when a narrow column wraps.
    private const char Nbsp = ' ';

    /// <summary>The line energy without its unit (the column header carries "keV").</summary>
    public string EnergyText => LineKeV.ToString("0.##", CultureInfo.InvariantCulture);

    /// <summary>"in scene" when a source of the scene emits this line, else empty.</summary>
    public string SceneText => InScene ? "in" + Nbsp + "scene" : "";

    /// <summary>The record status word: Offered, Refused, Local or Unavailable.</summary>
    public string RecordLabel => Plan.Status switch
    {
        ReadoutLineStatus.Offered => "Offered",
        ReadoutLineStatus.Refused => "Refused",
        ReadoutLineStatus.Local => "Local",
        _ => "Unavailable",
    };

    /// <summary>The record qualifier kept inline (D1): the experimental label or the refusal's short reason.</summary>
    public string RecordDetail => Plan.Status switch
    {
        ReadoutLineStatus.Offered => "experimental",
        ReadoutLineStatus.Refused => $"failed{Nbsp}on {Plan.Cell!.Failures.Count}{Nbsp}/{Nbsp}{Plan.Cell.CalibrationSeeds}{Nbsp}seeds",
        ReadoutLineStatus.Local => "experimental, unvalidated",
        _ => "",
    };

    /// <summary>The local preparation state word (ready, failed, not prepared, not preparable).</summary>
    public string LocalLabel { get; init; } = Local;

    /// <summary>The local state's detail (cost and peaks when ready, the failure message when failed), else empty.</summary>
    public string LocalDetail { get; init; } = "";

    /// <summary>The inline label: the offer fact and a short reason (the refusal stays inline; its literal text is in help).</summary>
    public string Summary => Plan.Status switch
    {
        ReadoutLineStatus.Offered => $"{Line} · Offered (experimental)",
        ReadoutLineStatus.Refused => $"{Line} · Refused · calibration failed on {Plan.Cell!.Failures.Count} / {Plan.Cell.CalibrationSeeds} seeds",
        ReadoutLineStatus.Local => $"{Line} · Local · experimental, unvalidated",
        _ => $"{Line} · Unavailable",
    } + (InScene ? " · in scene" : "");

    /// <summary>Help text: the full record sentence (population, budget, source, phase, first failure) and local state.</summary>
    public string Details => $"{Offer}\nLocal preparation: {Local}.";

    /// <summary>Accessible name of the row's help mark: the visible row text plus "details" (WCAG 2.5.3: the name contains
    /// the visible label); the mark's HelpText is <see cref="Details"/>.</summary>
    public string AccessibleName => $"{Summary} · local: {Local} (details)";

    /// <summary>Unique automation id of this row's help mark.</summary>
    public string HelpId => "Readout.LineHelp." + LineKeV.ToString("0.##", CultureInfo.InvariantCulture);

    /// <summary>Automation-friendly one-line summary: line, offer status, scene presence and local state.</summary>
    public override string ToString() => $"{Summary} · local: {Local}";
}
