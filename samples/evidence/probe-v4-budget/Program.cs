using System.Collections;
using System.Diagnostics;
using System.Text.Json;
using Gcam.Configuration;
using Gcam.Core;
using Gcam.Detector;
using Gcam.Simulation;

int seed=int.Parse(args[0]);
if(!Enumerable.Range(0,4).Select(i=>41000001+104729*i).Contains(seed))throw new Exception("development only");
var cfg=ConfigLoader.Load("samples/scenario.json");
cfg.Detector.PixelsX=cfg.Detector.PixelsY=44; cfg.Detector.PixelPitchMm=.4;
cfg.Detector.Material="GAGG";cfg.Detector.ReflectorGapMm=.1;cfg.Detector.EntranceAbsorberMm=.15;cfg.Detector.BackingScatterMm=2;
var ro=new ReadoutConfig{Mode=ReadoutMode.FourOutputAnger,Network=new(){ColumnResistanceOhm=10,SolveMethod=ChargeNetworkSolveMethod.Adjoint}};
ro.Calibration.Method=FloodCalibrationMethod.LatticeGrowth;ro.Calibration.EnergyKeV=661.7;
int Seed(uint p)=>unchecked((int)DefaultRandom.Key(seed,p+100));
IRandom Rng(uint p)=>DefaultRandom.FromKey(DefaultRandom.Key(seed,p+100));
var clock=Stopwatch.StartNew();var device=new ReadoutDevice(cfg.Detector,ro,unchecked((int)DefaultRandom.Key(seed,1)));
var proc=new ReadoutPulseProcessor(device,ro.Pulse,ro.Trigger);
double build=clock.Elapsed.TotalSeconds;var rows=new List<object>();
foreach(int budget in new[]{8000,32000}){
clock.Restart();ro.Calibration.EventsPerCrystal=budget;
var flood=new StreamedFlood(()=>ReadoutFlood.Stream(cfg,661.7,44*44*budget,Seed(2)),44*44*budget);
var cal=ReadoutCalibration.Build(device,proc,flood,ro.Calibration,Rng(3),Rng(4));
rows.Add(new{Budget=budget,cal.Succeeded,cal.Failure,Markers=cal.Lut.Peaks.Count,cal.Scored,cal.InWindow,Seconds=clock.Elapsed.TotalSeconds});
Console.Error.WriteLine($"{seed} {budget}: {cal.Succeeded} {clock.Elapsed.TotalSeconds:F2}s {cal.Failure}");
File.WriteAllText(args[1],JsonSerializer.Serialize(new{Seed=seed,BuildSeconds=build,Rows=rows}));
}
sealed class StreamedFlood(Func<IEnumerable<InteractionSite[]>> source,int count):IReadOnlyList<InteractionSite[]>{
public int Count=>count;public InteractionSite[] this[int i]=>throw new NotSupportedException();
public IEnumerator<InteractionSite[]> GetEnumerator()=>source().GetEnumerator();IEnumerator IEnumerable.GetEnumerator()=>GetEnumerator();}
