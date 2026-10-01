namespace Gcam.Configuration;

/// <summary>Physical parts that derive both resolution and pulse duration.</summary>
public sealed record FrontEndChain(ScintPreset Scintillator, SensorPreset Sensor, PreampPreset Preamp)
{
    public FrontEndConfig BuildConfig() => FrontEndParts.BuildConfig(Scintillator, Sensor, Preamp);
    public (double RiseSamples, double TailSamples) PulseSamples => FrontEndParts.PulseSamples(Scintillator, Preamp);
    public override string ToString() => $"{Scintillator.Name} / {Sensor.Name} / {Preamp.Name}";
}
