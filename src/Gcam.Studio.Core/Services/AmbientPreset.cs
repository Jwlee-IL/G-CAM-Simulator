using Gcam.Configuration;

namespace Gcam.Studio.Core.Services;

/// <summary>An ambient spectrum Studio offers, by hash-pinned reference. The ViewModel never loads the file: the field
/// it builds names the file and its SHA-256, the acquisition service resolves it from the application's ambient
/// folder through the engine loader (<see cref="ConfigLoader.ResolveAmbientSpectrum"/>, which checks the bytes against
/// the pin), and the engine refuses it unless it is validated and carries its acceptance record.</summary>
public sealed record AmbientPreset(string Id, string Name, string FileName, string Sha256)
{
    public override string ToString() => Name;

    /// <summary>The validated terrestrial field (AB-10; Studio default by AB-15). The pin equals
    /// <c>samples/ambient/terrestrial-unscear2000-v1.json.sha256</c>; a test keeps them equal.</summary>
    public static AmbientPreset TerrestrialUnscear2000V1 { get; } = new("terrestrial-unscear2000-v1",
        "Terrestrial UNSCEAR 2000 v1 — validated", "terrestrial-unscear2000-v1.json",
        "4d48bc4272ce4c25ff6ae9b13ed7583e3c8f467afffdb0225956a70cc435b3f4");

    /// <summary>The user-facing preset list. The development mono662 placeholder is deliberately not in it (AB-15).</summary>
    public static IReadOnlyList<AmbientPreset> All { get; } = Array.AsReadOnly(new[] { TerrestrialUnscear2000V1 });

    /// <summary>A field by reference to this preset; evidence-grade, so the engine requires the validated spectrum.</summary>
    public AmbientFieldConfig Field(double doseRateMicroSvPerHour, AmbientGeometry geometry) => new()
    {
        DoseRateMicroSvPerHour = doseRateMicroSvPerHour, Geometry = geometry,
        SpectrumFile = FileName, SpectrumFileSha256 = Sha256, RequireValidatedSpectrum = true
    };
}
