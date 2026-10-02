using System.Globalization;
using Gcam.Configuration;
using Gcam.Core;
using Gcam.Detector;
CultureInfo.CurrentCulture=CultureInfo.InvariantCulture;
const int N=1024,N0=100,Trials=6000;
var adc=Waveform.DefaultAdc; var quiet=adc with {AdcNoiseCodes=0};
var deterministic=new List<string>{"preset,variant,E,order,A,K,floatPeak,adcRoundedFloatPeak,integerPeak,tPeakNs,oneOutputCodeKeV,intGain662RecoveredKeV"};
var stochastic=new List<string>{"preset,variant,E,mode,meanCodes,sdCodes,biasPct,R_equivPct,R_aboutMeanPct,nonpositivePct,gateMeanCodes,gateR_equivPct,batchRmin,batchRmax"};
Console.WriteLine($"ADC gain={adc.AdcPerKev:R} noise={adc.AdcNoiseCodes:R} trials={Trials}");
foreach(var pi in Enumerable.Range(0,3)){
 var pa=FrontEndParts.Preamps[pi]; var ch=new FrontEndChain(FrontEndParts.Scintillators[0],FrontEndParts.Sensors[0],pa); var p=ch.PulseSamples;
 int a=(int)Math.Round(Math.Exp(-1/p.TailSamples)*65536); int order=pa.CrrcOrder;
 int k=ch.CrrcKQ16;
 var variants=new[]{new V("current",4,26214,0),new V("currentQ12",4,26214,12),new V("configured",pa.CrrcOrder,ch.CrrcKQ16,Waveform.CrrcFractionalBits)};
 Console.WriteLine($"PARAM {pi} {pa.Name}: integration={pa.IntegrationNs} rise={p.RiseSamples*8} tail={p.TailSamples*8} A={a} new order={order} K={k} model R32/122/662={ch.BuildConfig().IntegrationTimeNs} / {string.Join('/',new[]{32.0,122,662}.Select(e=>(new FrontEndModel(ch.BuildConfig()).FwhmFraction(e)*100).ToString("F5")))}");
 foreach(var v in variants){
  var refw=WindowRasterizer.Rasterize(new[]{((long)N0,662.0)},N,quiet,p.TailSamples,p.RiseSamples,0);
  double intGain=Shape(refw,a,v).Max()/662;
  var df=Float(Waveform.BiexpPulse(N,N0,662,p.TailSamples,p.RiseSamples,adc.AdcPerKev),a,v);
  double gain=df.Max()/662; int idx=Array.IndexOf(df,df.Max());
  foreach(double e in new[]{32.0,32.1,122,662}){
   var w=WindowRasterizer.Rasterize(new[]{((long)N0,e)},N,quiet,p.TailSamples,p.RiseSamples,0);
   var analog=Float(Waveform.BiexpPulse(N,N0,e,p.TailSamples,p.RiseSamples,adc.AdcPerKev),a,v);
   var rounded=Float(w.Select(x=>(double)x).ToArray(),a,v); var shaped=Shape(w,a,v);
   deterministic.Add($"{pi},{v.Name},{e},{v.Order},{a},{v.K},{analog.Max():F7},{rounded.Max():F7},{shaped.Max():F7},{(idx-N0)*8},{1/gain:F5},{shaped.Max()/intGain:F5}");
   if(e==32.1)continue;
   foreach(bool full in new[]{false,true}){
    var values=new double[Trials]; var peaks=new double[Trials]; var ampRng=new DefaultRandom(901); var fem=new FrontEndModel(ch.BuildConfig());
    for(int t=0;t<Trials;t++){
     double amp=full?fem.Measure(e,ampRng):e;
     var noisy=WindowRasterizer.Rasterize(new[]{((long)N0,amp)},N,adc,p.TailSamples,p.RiseSamples,3,t+1);
     var ys=Shape(noisy,a,v);
     // Known arrival and calibrated noiseless peak time; subtract the mean of 64 pretrigger samples.
     double baseline=ys.Skip(N0-64).Take(64).Average(); values[t]=ys[idx]-baseline; peaks[t]=ys.Skip(Math.Max(N0,idx-16)).Take(Math.Min(N-1,idx+16)-Math.Max(N0,idx-16)+1).Max()-baseline;
    }
    var st=Stats(values); var gp=Stats(peaks);
    var batch=Enumerable.Range(0,3).Select(b=>2.3548*Stats(values.Skip(b*2000).Take(2000).ToArray()).sd/(gain*e)*100).ToArray();
    stochastic.Add($"{pi},{v.Name},{e},{(full?"full":"electronics")},{st.mean:F6},{st.sd:F6},{(st.mean/(gain*e)-1)*100:F4},{2.3548*st.sd/(gain*e)*100:F4},{2.3548*st.sd/Math.Abs(st.mean)*100:F4},{values.Count(x=>x<=0)*100.0/Trials:F3},{gp.mean:F6},{2.3548*gp.sd/(gain*e)*100:F4},{batch.Min():F4},{batch.Max():F4}");
   }
  }
 }
 // quantisation staircase near 32 keV, non-ideal deterministic rise, exact integer code levels
 foreach(var v in variants){
  var peaks=new List<double>();
  for(int t=0;t<=1000;t++){
   double e=27+t*.01;
   var w=WindowRasterizer.Rasterize(new[]{((long)N0,e)},N,quiet,p.TailSamples,p.RiseSamples,0);
   peaks.Add(Shape(w,a,v).Max());
  }
  Console.WriteLine($"STAIR {pi} {v.Name} E27..37 step.01 min={peaks.Min():F6} max={peaks.Max():F6} levels={peaks.Distinct().Count()} zeroPct={peaks.Count(x=>x==0)*100.0/peaks.Count:F3}");
 }
 // Reproduce the previous review's noiseKev=0 but ENOB-on vector.
 foreach(double e in new[]{32.1,662.0}){
  var w=Waveform.Rasterize(new[]{(100L,e)},adc,p.TailSamples,p.RiseSamples,0,0,seed:1,tailPad:2048);
  Console.WriteLine($"INHERITED {pi} E={e} peak={Waveform.CrrcInt(w,a,26214,4).Max()}");
 }
}
File.WriteAllLines("deterministic.csv",deterministic); File.WriteAllLines("resolution.csv",stochastic);
Console.WriteLine(string.Join('\n',deterministic)); Console.WriteLine(string.Join('\n',stochastic));
static double[] Float(double[] w,int a,V v){
 var ys=new double[w.Length];var acc=new double[v.Order];double prev=0;
 for(int n=0;n<w.Length;n++){double u=w[n]-a/65536.0*prev;prev=w[n];for(int i=0;i<v.Order;i++){acc[i]+=(u-acc[i])*v.K/65536.0;u=acc[i];}ys[n]=u;}return ys;
}
static double[] Shape(int[] w,int a,V v) => Waveform.CrrcInt(w,a,v.K,v.Order,v.F).Select(x=>x/(double)(1<<v.F)).ToArray();
static (double mean,double sd) Stats(double[] x){double m=x.Average();return(m,Math.Sqrt(x.Sum(v=>(v-m)*(v-m))/(x.Length-1)));}
record V(string Name,int Order,int K,int F);
