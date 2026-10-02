namespace Gcam.Studio.Core.Services;

/// <summary>One resolvable line group, with measured counts selected by bin centre.</summary>
public sealed record SpectrumBand(IReadOnlyList<SpectrumLine> Lines, double LoKeV, double HiKeV,
    long Counts, double Share)
{
    public string Isotope => string.Join(" + ", Lines.Select(l => l.Isotope).Distinct());
    /// <summary>Row label: the isotope for gamma lines; for a band of X-ray lines the emitter from the engine table
    /// with the source in brackets — "Ba K X-rays (Cs-137)": the photons come from the daughter barium, not caesium.</summary>
    public string Name
    {
        get
        {
            if (Lines.Count == 0 || !Lines.All(l => l.Kind == Gcam.Configuration.EmissionKind.XRay)) return Isotope;
            string origin = string.Join(" + ", Lines.Select(l => l.XRayOrigin).OfType<string>().Distinct());
            return origin.Length > 0 ? $"{origin} X-rays ({Isotope})" : $"X-rays ({Isotope})";
        }
    }
    public string Energies => string.Join(" + ", Lines.Select(l => l.EnergyKeV).Distinct().Select(e => $"{e:F1}"));
    public string Label => $"{Energies} keV";
    public string Window => $"{LoKeV:F1}–{HiKeV:F1}";
    public string Description => $"{Name}: {Label}, window {Window} keV, {Counts:N0} counts, {Share:P1}";
}
