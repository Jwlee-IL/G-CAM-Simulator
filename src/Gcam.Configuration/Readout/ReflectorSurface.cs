using System.Text.Json.Serialization;

namespace Gcam.Configuration;

/// <summary>Angular law of the crystal-wall reflector. Written as a string.</summary>
[JsonConverter(typeof(JsonStringEnumConverter<ReflectorSurface>))]
public enum ReflectorSurface
{
    /// <summary>Diffuse (Lambertian) reflection — a white powder / paint reflector.</summary>
    Lambertian,

    /// <summary>Mirror reflection — a specular film.</summary>
    Specular,
}
