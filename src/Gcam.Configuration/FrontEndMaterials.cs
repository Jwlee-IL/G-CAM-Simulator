namespace Gcam.Configuration;

/// <summary>Explicit transport keys for selectable Studio scintillators. Unsupported parts fail closed.</summary>
public static class FrontEndMaterials
{
    public static IReadOnlyList<ScintPreset> Scintillators { get; } = Array.AsReadOnly(
        FrontEndParts.Scintillators.ToArray());

    public static string Material(ScintPreset scintillator) => scintillator.Name switch
    {
        "GAGG(Ce)" => "GAGG",
        "NaI(Tl)" => "NaI",
        "LYSO" => "LYSO",
        "CsI(Tl)" => "CsI",
        "BGO" => "BGO",
        _ => throw new ArgumentException("No transport material for this scintillator.", nameof(scintillator))
    };
}
