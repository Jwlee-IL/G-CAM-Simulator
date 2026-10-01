namespace Gcam.Studio.Core.Services;

/// <summary>One resolvable line group, with measured counts selected by bin centre.</summary>
public sealed record SpectrumBand(IReadOnlyList<SpectrumLine> Lines, double LoKeV, double HiKeV,
    long Counts, double Share)
{
    public string Isotope => string.Join(" + ", Lines.Select(l => l.Isotope).Distinct());
    public string Energies => string.Join(" + ", Lines.Select(l => l.EnergyKeV).Distinct().Select(e => $"{e:F1}"));
    public string Label => $"{Energies} keV";
    public string Window => $"{LoKeV:F1}–{HiKeV:F1}";
    public string Description => $"{Isotope}: {Label}, window {Window} keV, {Counts:N0} counts, {Share:P1}";
}
