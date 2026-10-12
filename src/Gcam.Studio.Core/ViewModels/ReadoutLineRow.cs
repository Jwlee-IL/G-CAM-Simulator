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
