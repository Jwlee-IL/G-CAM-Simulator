# PLAN.Physics.CrrcPresets.Review — preset intent, low-energy quantisation and shaping-time candidates (TODO-20)

Scope: review and headless measurement of the three existing CR-RC preamp presets, their history, candidate time/order settings and fractional-state arithmetic, and the changes required to the C# / RTL / cocotb contract. No preset, engine, RTL, task-row or Findings edits; no desktop work.

Status: reviewed 2026-10-02 at detached HEAD `cf1d6b7bf764d1ad8523670e68e6338e2416314c`. All parameter and arithmetic changes below are scratch experiments, awaiting the planner's decisions.

## Disposition

**A common order 4 is historically intentional; a requirement to use one K for every physical preset is not established. The measured low-energy loss is an arithmetic defect for energy measurement, and changing K/order alone does not fix it.** Keep the distinction between a generic shaper demonstration and a validated physical-preset response.

The TODO-20 row correctly identifies the shared constants and reproduces the earlier amplitude result. It does not yet specify a physical shaping-time convention. `IntegrationNs` is explicitly an **effective noise integration window**, not a documented RC pole time or peaking time. Treating it as either without a new decision would introduce a new assumption. Likewise, “CR-RC” in a family name does not establish a one-RC-stage topology. The current evidence cannot establish the rig's actual filter order or coefficients.

Recommended decisions: retain order 4 for all three presets unless the author supplies contrary hardware information; introduce an explicit shaping-time specification separate from the DCR noise window; evaluate the common-order candidate below as a simulation convention, and preserve fractional state if low-energy heights are to be useful. Do not adopt the literal-name order-1 candidate as a correction to the 200 ns preset: it has no hardware evidence and performs worse in this measurement. Keep the existing legacy golden operating point as a separate regression fixture.

## Records and current code

| Record | What it establishes |
|---|---|
| `c198a25`, theme 31, `rtl/shapers.py` | A comparison filter deliberately uses order 4, `tau_s=2.5` samples and `K=1/tau_s`. This is the generic short-pulse comparison point, not three physical presets. The floating comparison results in Findings 31 are not measurements of today's integer preset path. |
| `6a35791`, theme 33 | Ports that CR-RC⁴ comparison to synthesizable integer RTL, including same-sample cascade updates. |
| `d5feed7`, introduction of WPF component presets | Adds the present 100/200/500 ns integration windows and 150/320/800 ns CSP tails. `UpdateFrontEnd` describes every CR-RC selection as CR-RC⁴; `RenderWaveform` matches A to the new tail but continues to pass the shared order and K. Commit text explicitly preserves the golden tau=5 defaults. No per-preset K rationale or shaping-time conversion is supplied. |
| `c884d53` | Corrects A=53667 to 53656 and adds independent expected-math assertions, because reading coefficients back from the DUT had hidden the wrong constant. Preserve that lesson. |
| `45eece8` | Moves the component presets into Configuration without adding order/time metadata. |
| `1c3ae61`, Waveform plan D-9 / D-11 | Deliberately preserves the existing integer filter during TODO-10 and exposes the shared order/K; defers this issue to TODO-20. This is scope preservation, not new hardware evidence. |

Read [AGENTS.Planning](AGENTS.Planning.md), root AGENTS / CLAUDE, TODO-20, [PLAN.Studio.Waveform](PLAN.Studio.Waveform.md), its [review](PLAN.Studio.Waveform.Review.md), documentation conventions, Findings 30–33, RTL README / CR-RC implementation / all `test_*.py`, references and runner. Targeted history searches covered the preset introduction, migration, integer shaper and documentation records; no explicit decision requiring shared K across these presets was found. This is an absence in the inspected records, not proof that no external hardware decision exists.

Current wiring:

- `FrontEndParts.Preamps` and `PreampPreset`: boolean shaper family, integration noise window, pulse tail; no per-preset filter order or shaping time.
- `FrontEndParts.BuildConfig` / `FrontEndModel`: IntegrationNs affects DCR variance. The analytic MCA resolution does not depend on the waveform's K/order or integer arithmetic.
- `WaveformService.Generate`: A=`round(exp(-1/tailSamples)*65536)`; K=`Waveform.CrrcKQ16`=26214; order=4 for every CR-RC selection. The current UI suppresses CR-RC energy readout.
- `Waveform.CrrcInt`: A/K are Q16 **coefficients**, but each accumulator contains whole output codes. Each signed shift floors every update. Merely using `long` states does not give fractional precision.
- `TrapShape` is a different recursive filter, with matched M and its own amplitude calibration. It is not an applicable CR-RC energy estimator; no trapezoidal settings need to change for this proposal.

## Measurement conditions and definitions

Headless .NET SDK 9.0.311 Release console project in `%TEMP%/gcam-crrc-review-29523e72edc54c7e88b3818dfd983ef1`. It includes current source directly, so no repository build outputs or presets are modified. Program, project, full CSV tables and console logs remain there. An exact reproduction program is included below so the evidence does not depend on those temporary files surviving.

GAGG(Ce), S13360-3050, AD9648 14-bit / 125 MSPS; ADC gain **4.0955 codes/keV**, ENOB noise **1.32746148 codes RMS**. One isolated event at sample 100, 1024 total samples, rise 90 ns; tails 150/320/800 ns. Deterministic pulses retain the configured finite rise but remove analog noise, ENOB noise and amplitude smearing. Thus they are **not** Studio's zero-rise ideal mode. Sample-phase jitter, baseline drift, afterglow, pile-up, ADC saturation, gain spread and trigger efficiency are not modelled here.

The amplitude table distinguishes a floating recurrence using an unquantised input, the same floating recurrence using the rounded ADC input, and the actual integer recurrence. Floating recurrences use the **same rounded A/K coefficients** as each experiment, so their comparison isolates sample/state quantisation rather than a different filter design. All maxima include the zero pretrigger region, as did the inherited measurement.

Resolution uses **6000 independent isolated traces** per energy/mode/variant, sample-noise seeds 1…6000, amplitude seed 901. “Electronics” = analog sample noise 3 keV RMS plus ENOB noise, no event-amplitude smear. “Full” adds **one** `FrontEndModel.Measure` smear before rasterisation; no second intrinsic smear. Baseline = mean of 64 pretrigger shaped samples; amplitude = baseline-subtracted sample at the noiseless floating peak time, assumed known. This estimator is a controlled measurement, **not an implemented trigger/energy algorithm**.

Report `R_equiv = 2.3548 * sample SD / (g_float * E)`, where `g_float` is the unquantised-input peak at 662 keV divided by 662. These are **Gaussian-equivalent widths**, not fitted photopeak FWHMs: current integer low-energy distributions are discrete and biased. Mean bias is reported separately. Do not interpret a small SD from a stuck filter as good energy resolution. The CSV also reports widths relative to the measured mean, nonpositive fractions, and a gated maximum over ±16 samples around the calibrated peak. Gated maxima have selection bias; they are not used to claim improved resolution.

## Candidate conventions and coefficients

Current K represents an Euler RC nominal time of **20 ns per stage** at 125 MSPS. Its exact discrete pole decay time is `-8/log(1-K/65536)` ≈ **15.66 ns**; “20 ns” is a coefficient convention, not an exact discrete time constant.

For an explicit comparison convention, define `T_sum = order * tau_stage` and provisionally set T_sum to the preset's IntegrationNs. Then `K_Q16 = round(65536 * order * 8 / T_sum_ns)`. This retains the established Euler coefficient definition. **T_sum is neither the true peaking time nor a derived equivalent DCR noise window.** A remains matched to the pulse tail. A future implementation should expose T_sum separately and should not silently redefine `IntegrationNs` or replace K by `1-exp(-dt/tau)`.

| Preset | Integration / tail ns | A_Q16 | Current order / K | Common-order candidate order / K / nominal stage ns | Literal-name alternative |
|---|---|---|---|---|---|
| Fast CSP + CR-RC^4 | 100 / 150 | 62132 | 4 / 26214 | 4 / 20972 / 25 | same as common-order |
| CSP + CR-RC (200 ns) | 200 / 320 | 63918 | 4 / 26214 | 4 / 10486 / 50 | order 1 / K=2621 / 200 ns |
| Slow shaping (high pileup) | 500 / 800 | 64884 | 4 / 26214 | 4 / 4194 / 125 | order 4 retained; no order in name |

Measured floating pulse peak times after arrival are **80/80/80 ns** currently, **104/200/448 ns** for the common-order candidate, and **128 ns** for the order-1 original alternative. The times include the finite input rise. Historical “order*tau_s” prose is not an exact timing law for this discrete, same-sample recurrence. The slower candidate's longer pulse support is a pile-up cost; it is not validated by this isolated-pulse study.

`currentQ12` retains current coefficients and adds 12 fractional state bits. `timing` changes the literal-name order/time convention but retains whole-code states. `timingQ12` adds 12 fractional bits to that convention. `timing4Q12` is the recommended **comparison candidate**, retaining order 4 and varying time with the explicit convention above. None is a production change.

## Amplitude and quantisation

Each cell below is the peak at **32 / 122 / 662 keV**, in output-code equivalents. Q12 raw output integers must be divided by 4096; retaining those fractions is essential.

| Preset | Current floating | Current integer | Current Q12 | Common-order time candidate Q12 |
|---|---|---|---|---|
| Fast | 2.513 / 9.582 / 51.996 | 0 / 5 / 47 | 2.505 / 9.609 / 51.970 | 2.234 / 8.519 / 46.140 |
| Original | 4.581 / 17.466 / 94.773 | 0 / 14 / 90 | 4.591 / 17.459 / 94.781 | 2.626 / 10.029 / 54.438 |
| Slow | 5.700 / 21.731 / 117.915 | 0 / 17 / 114 | 5.719 / 21.742 / 117.933 | 1.568 / 5.989 / 32.527 |

The rounded ADC-input floating peaks currently are Fast **2.506/9.611/51.972**, Original **4.592/17.460/94.781**, Slow **5.720/21.743/117.934**. ADC quantisation explains the small change from the unquantised input; it cannot explain the much larger whole-state loss.

Reproducing the previous review's exact ENOB-on stimulus (`Waveform.Rasterize`, seed 1, analog noise=0, intrinsic=0, tailPad=2048) gives **32.1 keV: 0/0/1**, **662 keV: 47/90/114**. Removing ENOB noise gives **0/0/0** at 32.1 keV. The one code in the inherited slow trace is noise-dependent, not reliable low-energy sensitivity.

The literal-name time changes **without fractional state** give Fast **0/1/41**, Original **0/2/35**, Slow **0/0/6** at 32/122/662 keV. With Q12, the order-1 Original gives **1.973/7.467/40.586**. Re-parameterisation alone is not a remedy; slower coefficients make whole-code deadbands larger.

An energy calibration based only on the current noiseless **662-keV integer peak** returns, at a true 122 keV, **70.43/102.98/98.72 keV**, and returns zero at 32 keV. A constant gain cannot correct this nonlinear loss. With common-order candidate Q12, the same matched 662 calibration returns **32.054/31.930/31.905 keV** at 32 keV and **122.220/121.956/121.896 keV** at 122 keV. These are measured fixed-phase calibration residuals, not tolerances or guarantees.

Current floating gains imply one whole output code corresponds to **12.73/6.99/5.61 keV**. For the common-order candidate these are **14.35/12.16/20.35 keV**. Q12 state granularity is those values divided by 4096 (**0.00350/0.00297/0.00497 keV** for the candidate); this is arithmetic granularity, **not detector resolution**. ADC input rounding, noise and the filter still limit the measurement.

Energy staircase sweep 27…37 keV in 0.01-keV increments (1001 stimuli): current integer peaks have **1/1/3 distinct levels**, with **100/100/56.54%** zero maxima. Current Q12 has **47/93/122** levels, common-order candidate Q12 **68/303/674**, with no zero maxima. Q12 removes the accumulator deadband but does not create 1001 independent ADC measurements: input rounding leaves plateaus and small nonmonotonic steps. No arbitrary precision target was introduced.

## Resolution and bias

Values are **32 / 122 / 662 keV**, percentages, with the controlled baseline-subtracted estimator above.

| Preset / variant | Electronics R_equiv % | Full R_equiv % | Full mean bias % |
|---|---|---|---|
| Fast current | 85.23 / 22.76 / 4.624 | 86.21 / 23.81 / 6.659 | −9.52 / −5.80 / −1.40 |
| Original current | 47.18 / 12.89 / 2.486 | 49.49 / 15.04 / 5.299 | −7.39 / −3.68 / −0.88 |
| Slow current | 37.55 / 10.26 / 1.887 | 39.79 / 12.98 / 4.934 | −5.10 / −3.22 / −0.66 |
| Fast currentQ12 | 78.93 / 20.71 / 3.817 | 80.24 / 22.15 / 6.013 | +0.39 / +0.10 / +0.015 |
| Original currentQ12 | 42.95 / 11.27 / 2.076 | 45.24 / 13.73 / 5.086 | +0.23 / +0.056 / +0.007 |
| Slow currentQ12 | 34.59 / 9.07 / 1.672 | 37.37 / 11.99 / 4.933 | +0.19 / +0.042 / +0.005 |
| Fast common-order candidate Q12 | 59.07 / 15.50 / 2.855 | 60.78 / 17.38 / 5.456 | +0.29 / +0.073 / +0.010 |
| Original common-order candidate Q12 | 16.18 / 4.24 / 0.782 | 21.32 / 8.85 / 4.693 | +0.034 / +0.005 / −0.002 |
| Slow common-order candidate Q12 | 7.70 / 2.02 / 0.372 | 16.08 / 8.08 / 4.653 | −0.105 / −0.031 / −0.009 |
| Original literal-name order-1 candidate Q12 | 59.98 / 15.73 / 2.898 | 61.33 / 17.43 / 5.420 | +0.021 / +0.001 / −0.003 |

Without fractional state, the literal-name time candidates have full R_equiv Fast **86.43/26.97/5.978%**, Original **144.99/38.83/8.238%**, Slow **78.33/19.07/6.334%**. Slow has **negative** mean signal at 32 and 122 keV and −70.51% mean bias at 662 keV. Those widths do not signify valid recovery; the 122-keV filter does not respond measurably to the added event-amplitude fluctuations.

Subtracting a measured baseline matters: whole-state rounding under stationary noise creates an offset. In a separate zero-baseline raw-output pass, current 32-keV means were **−1.789/+0.178/+1.344 codes**, versus **2.274/4.242/5.409** after baseline subtraction. Noise dithers state transitions, so noisy average response cannot be inferred from a noiseless zero maximum. Conversely, subtracting a baseline does not fix the deterministic deadband, nor make an event-triggered detector sensitive at that energy.

The existing analytic `FrontEndModel` predicts approximately **13.81/7.686/4.569%** at 32/122/662 keV for all three chains (tiny differences from DCR integration). The waveform has additional sample noise, so these predictions must not be claimed as measured shaped-energy resolution. For the common-order Q12 candidate, full 32-keV R_equiv across three disjoint 2000-trial batches was Fast **60.02–62.15%**, Original **21.20–21.38%**, Slow **15.90–16.22%**; at 662 keV **5.418–5.513 / 4.672–4.719 / 4.619–4.694%**. These ranges show observed sampling spread, not confidence intervals or acceptance tolerances.

A ±128-ns gated maximum demonstrates why a peak algorithm needs independent calibration: the order-1 candidate's 32-keV full mean becomes **2.813 codes**, versus the noiseless floating **1.961 codes**, despite a smaller gated width. That is approximately **43% positive selection bias**. Do not advertise its smaller width as better resolution. The common-order Original has much less bias in this stimulus, but an unknown arrival phase / variable rise / trigger threshold still needs evidence before enabling an energy readout.

## RTL and cocotb contract required by a future change

Parameter-only changes can use the present `crrc_shaper.sv`: it already accepts ORDER/A/K. The runner must add independent fixture definitions for each preset and scintillator's tail, calculate A from the configured pulse tail and K from the **agreed** timing convention, and assert ORDER as well. Current `test_crrc._ref` pins A/K to the legacy tau=5 / tau_s=2.5 values. Simply changing runner parameters will fail those expected-math assertions. Do not remove them or replace them with values read from the DUT. Keep the old fixture and its golden C# test; add independently expected preset fixtures and stimuli rather than silently changing the legacy default operating point.

Fractional-state changes are a separate arithmetic contract. The scratch Q12 recurrence is:

```text
X = sign_extend(sample) << F; F = 12
imp = X - ((A_Q16 * prevX) >> 16); prevX = X
for each stage, in the SAME sample:
    acc[i] += ((u - acc[i]) * K_Q16) >> 16
    u = acc[i]
output = u                         # signed code * 4096, not whole codes
display/calibrated measurement = output / 4096.0
```

Coefficient Q remains 16; state F is separate. Input is sign-extended before shifting; arithmetic shifts still floor. **Keep fractional output through calibration and plotting**; a final integer shift back to whole codes would discard much of the benefit. A new API/module parameter or separate precise path should document this output scale and preserve a legacy F=0 fixture. `trap_ref.crrc_int`, C# and RTL must implement the same scale, stage update order, reset state and signed operations.

The current RTL WACC=40 is not automatically safe after input scaling: full-scale signed-16 input shifted 12 and multiplied by a Q16 coefficient needs more than 40 bits even before bounding transients. Use explicitly widened signed products, or a provisionally 48-bit accumulator/product path and then establish bounds. C# `long` and Python unlimited integers do not model finite RTL wrapping. Bit-exactness requires matching widths/overflow or proving no overflow for the tested domain; this review has not proved that bound or synthesized a wider design.

Future contract tests should drive **actual RTL** for both legacy and precise paths with 32/32.1/122/662-keV finite-rise stimuli, near-threshold amplitude sweeps, negative samples/noise, stacked/full-scale inputs, reset and valid gaps; assert exact output samples and scale, not only 400→800 proportionality. Include selected-chain A/K (and supported scintillator combinations), explicit clock-to-output latency and longer pulse support. The current CR-RC tests search latency rather than pinning it; the recurrence sampled after the accepting edge corresponds to zero sequence offset. Do not change clock latency accidentally while widening arithmetic. Test independently computed coefficient expectations so the `c884d53` self-consistency bug cannot recur.

The runner currently describes historical “7 tests”, while its code requests trap ×3 for two tops, BLR ×1 and CR-RC ×3, with MC-dependent skips possible. Do not quote the historical count as a fresh execution result. No RTL/cocotb run occurred in this review.

## Planner decisions and remaining work

| Decision needed | Recommended disposition |
|---|---|
| Is shared order a bug? | No established bug: history shows a common CR-RC⁴ family. Keep it, make it explicit in metadata/name/readout; do not infer hardware topology from a shortened name. |
| Is shared K a physical preset specification? | No evidence of that specification. It is an inherited benchmark constant; adopt an explicit shaping-time convention before calling the presets physical shaper responses. |
| Tie shaping time to IntegrationNs? | Use the measured T_sum convention only as an explicit simulation proposal. Keep DCR effective noise window separate; deriving it from the actual weighting is future physics work. |
| Fix low-energy state quantisation? | Yes if shaped amplitude/energy is intended. Approve a deliberate fractional-state C#/RTL/Python contract; changing time/order alone fails. Q12 is a measured candidate, not yet an approved minimum or a universal accuracy guarantee. |
| Enable CR-RC energy display? | Keep it unavailable until an isolated-pulse estimator, trigger/phase/overlap behaviour, noise calibration and validity criteria are measured. No precision guarantee comes from bit-exactness alone. |

Executed: source/history inspection, scratch Release builds/runs, deterministic and staircase sweeps, 6000-trace resolution distributions with electronics/full noise budgets and baseline comparisons. No repository source changes, git state changes, installs, deletion, process stopping, uploads or persistent environment changes.

Not run: actual RTL/Icarus/cocotb, synthesis/timing/finite-width overflow verification, full solution tests, desktop/UI/Studio, physical hardware validation, other scintillator/sensor combinations, arrival-phase or high-rate sweeps. The allowed read roots include the SDK but not the separately installed Python/Icarus tools; this turn therefore used the permitted .NET headless path and did not inspect/run outside-root toolchains. Full solution/UI tests do not validate these unimplemented scratch candidates. No approval-required command was needed.


## Reproduction appendix

Save the following project and program in a newly created `%TEMP%/gcam-*` directory. Set the project property GcamRepo to the worktree root. No external packages are required; compilation includes the current Core/Configuration source and three Detector source files directly.

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <OutputType>Exe</OutputType><TargetFramework>net9.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings><Nullable>enable</Nullable>
  </PropertyGroup>
  <ItemGroup>
    <Compile Include="$(GcamRepo)/src/Gcam.Core/*.cs" />
    <Compile Include="$(GcamRepo)/src/Gcam.Configuration/*.cs" />
    <Compile Include="$(GcamRepo)/src/Gcam.Detector/Waveform.cs" />
    <Compile Include="$(GcamRepo)/src/Gcam.Detector/WindowRasterizer.cs" />
    <Compile Include="$(GcamRepo)/src/Gcam.Detector/FrontEndModel.cs" />
  </ItemGroup>
</Project>
```

From that scratch directory, run:

```powershell
# Set this to the reviewed worktree; do not point to another session's checkout.
$crrcRepo = '<reviewed-worktree-root>'
dotnet build Measure.csproj -c Release -p:GcamRepo="$crrcRepo"
dotnet ./bin/Release/net9.0/Measure.dll
```

The program emits `deterministic.csv` and `resolution.csv` in its working directory, plus coefficient, analytic-resolution, inherited-stimulus and staircase summaries on stdout. Preset indices 0/1/2 mean Fast/Original/Slow. To reproduce the raw-zero-baseline comparison separately, replace the pretrigger mean subtraction with baseline=0 in a copy of the scratch program. The original integer path always calls the unchanged `Waveform.CrrcInt`; fractional-state and floating paths below are measurement references only.

```csharp
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
 int a=(int)Math.Round(Math.Exp(-1/p.TailSamples)*65536); int order=pi==1?1:4;
 int k=(int)Math.Round(65536*order*8/pa.IntegrationNs);
 var variants=new[]{new V("current",4,26214,0),new V("currentQ12",4,26214,12),new V("timing",order,k,0),new V("timingQ12",order,k,12),new V("timing4Q12",4,(int)Math.Round(65536*4*8/pa.IntegrationNs),12)};
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
static double[] Shape(int[] w,int a,V v){
 if(v.F==0)return Waveform.CrrcInt(w,a,v.K,v.Order).Select(x=>(double)x).ToArray();
 var ys=new double[w.Length];var acc=new long[v.Order];long prev=0;
 for(int n=0;n<w.Length;n++){long x=(long)w[n]<<v.F;long u=x-((a*prev)>>16);prev=x;for(int i=0;i<v.Order;i++){acc[i]+=((u-acc[i])*v.K)>>16;u=acc[i];}ys[n]=u/(double)(1<<v.F);}return ys;
}
static (double mean,double sd) Stats(double[] x){double m=x.Average();return(m,Math.Sqrt(x.Sum(v=>(v-m)*(v-m))/(x.Length-1)));}
record V(string Name,int Order,int K,int F);
```
