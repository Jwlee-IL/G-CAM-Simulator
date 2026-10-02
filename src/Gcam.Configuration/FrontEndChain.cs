namespace Gcam.Configuration;

/// <summary>Physical parts that derive both resolution and pulse duration.</summary>
public sealed record FrontEndChain(ScintPreset Scintillator, SensorPreset Sensor, PreampPreset Preamp)
{
    public FrontEndConfig BuildConfig() => FrontEndParts.BuildConfig(Scintillator, Sensor, Preamp);
    public (double RiseSamples, double TailSamples) PulseSamples => FrontEndParts.PulseSamples(Scintillator, Preamp);
    /// <summary>Configured CR-RC Euler coefficient, independent of the DCR noise integration window.</summary>
    public int CrrcKQ16 => Preamp.CrrcKQ16();
    public override string ToString() => $"{Scintillator.Name} / {Sensor.Name} / {Preamp.Name}";
}
