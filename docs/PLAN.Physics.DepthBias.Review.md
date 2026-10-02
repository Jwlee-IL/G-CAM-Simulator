# PLAN.Physics.DepthBias.Review — sampling and a heuristic score; no validated replacement yet

Scope: TODO-23 review and measurement, 2026-10-02. Audit the reference plan, separate H-1…H-5, measure existing refinements and forward-template alternatives. No production/test edits, desktop work or bias subtraction.

## Result and recommendation

The bias exists without acquisition noise. The score ranks a sampled, pixel-centre, thin-plane MURA reconstruction relative to its reconstruction field; it does not fit the acquired image to its physical forward response. Detector sampling, reconstruction sampling and field normalisation interact. Physical transport changes the competing maxima, but is not necessary to produce the bias.

At 60 s, broadband maxima are **360/580 mm for 300/500 mm on axis in every one of five seeds**, and **560 mm for 700 mm at 30 mrad in every seed**. At 700 mm,15 mrad one seed selects **160 mm**. There is a competing-maximum problem as well as systematic offsets.

Do not implement only an interpolated value, sidelobe normalisation or the inherited engine refinement and call it unbiased. None fixes all nine points. A matched geometric forward likelihood gives -1…+1 mm errors on the noise-free controls, conditional on known direction. A physical likelihood reduces the large offsets, but retains finite-template artefacts and requires the true bearing. **No unbiased, scene-independent replacement for Studio's joint search was demonstrated.** Keep the descriptive “sharpest plane” interpretation pending discussion.

## Code audit of “What exists”

| Plan row | Code check / correction |
|---|---|
| Studio metric | Confirmed. [FocusSweepService](../src/Gcam.Studio.Services/FocusSweepService.cs) lines 35–42 uses population mean/std and the peak grid-node value. [ImagingProjection](../src/Gcam.Studio.Services/ImagingProjection.cs) lines 34–40 applies the sub-cell offset only to position. [MixedFieldStudy.TopPeaks](../src/Gcam.Simulation/MixedFieldStudy.cs) chooses the global maximum then blanks a disk; K=1 matches the probe's argmax. |
| Grid per plane | Formula confirmed, interpretation corrected: **the grid is already fixed in angle**. For all 150–950 mm measurement planes, r=.7z/80, step=r/4, half=.95·13r/2, so half/z and step/z are constant. Decoder rounding gives 50×50 nodes: angular origin -54.03125 mrad, step 2.1875 mrad. There is **no node on axis**: the closest is +.65625 mrad in each dimension. Axis phase is constant in angle, not a phase varying just because physical extent grows. Magnification/discrete mask assignments do vary. |
| Engine methods | Existence confirmed; “calibrated” is misleading. [DepthStudy](../src/Gcam.Simulation/DepthStudy.cs) uses raw PointResponse and a centroid above min+.7(max-min), not a physical template calibration. Joint refinement performs three alternations with no convergence check and uses a different lateral grid (one period, period/48, no sub-cell refinement). Theme 24's ~1% statement is not transferable. |
| Sampling | Shadow pitch=.7z/(z-80): **1.591/1.389/1.317 samples per cell** at z=300/500/700 and detector pitch .6 mm; 1.268 at 1 m. Theme 55 is relevant; its lateral error/period is not a depth tolerance. |
| Decoder assumptions | [CrossCorrelationDecoder](../src/Gcam.Decoding/CrossCorrelationDecoder.cs) lines 49–50 rounds size/fixes origin. Correlate evaluates detector centres, floors mask-cell coordinates and applies ±1 entries. It does not integrate active pixel area, slab transmission or crystal transport. [PeakInterpolation](../src/Gcam.Decoding/PeakInterpolation.cs) fits position from neighbours, not a physical integrated response or peak value. |
| Actual Studio planes | [FocusSweepMath](../src/Gcam.Studio.Core/Imaging/FocusSweepMath.cs) defaults to 81 planes equally spaced in **inverse distance**, 110–3000 mm at D=80. The planner's 150–950 mm uniform grid is a measurement grid. Both were tested. |
| Acquisition | [SimulationService.BuildConfig](../src/Gcam.Studio.Services/SimulationService.cs) applies material, absorber, backing, gap and gain map. [ListModeSource](../src/Gcam.Simulation/ListModeSource.cs) uses Compton argmax positions and separately seeded transport/crystal/rejection/time streams; MeasurementStage applies gain and smear once. Position[2] is used here. Nominal config focus stays 1000 mm until AtFocus clones it. |

## Configuration, seeds and experiment definitions

Retained harness: **%TEMP%\gcam-depthbias-20261002-120904\Program.cs**, SHA256 **E9A2A65E658ADE2245F1F964F3263C5600CD6F4D84369E004F77525908AFC7CA**. Probe.csproj references src/Gcam.Studio.Services/Gcam.Studio.Services.csproj, bringing in the engine/Core projects. All generated code, JSON, CSV and logs are under that temporary tree. This review is the only repository document written by this turn.

Common optics: rank 13, 2×2 mosaic, cell .7 mm, D=80 mm, 10 mm tungsten mask, attenuation .178/mm at 661.7 keV, straight full-cell channels. No taper, inversion, fabrication/pose errors. Footprint 18×18 mm; non-cyclic reconstruction, Tent position interpolation, K=1. Measurement planes **150+10i mm**, i=0…80. Distances here are z **from the detector**; engine S=z-80. Sources: z=300/500/700, a=0/15/30 mrad; position=(z·tan(a/1000),0,z). No background, source dark-event process, pile-up or thermal drift.

Physical acquisition: 30×30 at .6 mm, GAGG, 10 mm crystal, tabulated attenuation (config anchor=0), .15 mm steel-equivalent entrance absorber, 2 mm backing, .1 mm reflector gap, crosstalk=0. Gain σ=.03, fixed gain seed=1, no gradient/resolution scatter. Activity 500 µCi=18.5 MBq. Cs-137 lines (661.7,.851), (32.1,.056), (36.4,.014): emission rate **17,038,500 photons/s**. Default GAGG(Ce)/S13360-3050/CSP+CR-RC chain: light yield 50, collection .50, PDE .40, ENF 1.03, intrinsic FWHM .035, DCR 500,000 Hz, integration 320 ns. Measurement uses index-addressed seed 909. Window=661.7 keV ±1.5 chain FWHM (approximately ±45.3 keV). All=every retained event. Full serialized configs: bin/Release/net9.0/config_<z>_<a>_<pitch>.json under the harness.

Distinct means/counts:

1. **Noise-free geometry:** deterministic midpoint quadrature, single-line control using the nominal 662-keV attenuation anchor directly, ideal scoring at detector z=0; no crystal transport/absorbers/gain/smear. Sum z/(4π·distance³) times **expected exponential transmission** with the engine's 12 slab midpoint samples, integrated over pixel area. No Bernoulli or Poisson draws, no seed. Baseline gap=0; separate gap control integrates the .5 mm active square. q24 per pixel axis; q48 repeats at .6 mm. Ray evaluations/image: 518,400 (30×30,q24), 2,073,600 (30×30,q48 or 60×60,q24), 4,665,600 (90×90,q24). Totals are probabilities, not counted photons. Thin control: t=.001 mm, μ=1780/mm, preserving μt=1.78 while removing collimation. This is a mathematical thin-plane separation control, not a prescription for a different tungsten material.
2. **Physical expected flood:** **3,000,000 biased histories/point, seed 61001**, crystal seed+777. Same transport/pixel sink construction as ListModeSource; accumulate importance weights before acquisition rejection/timing. Apply measurement window once. This has finite transport MC error, **not exact zero noise**. W/3M is efficiency; W/3M×17,038,500×60 gives expected 60 s counts. Started after deterministic controls and completed before list-mode acquisitions.
3. **60 s list mode:** BuildConfig → ListModeSource → MeasurementStage. Exclude first event beyond 60 s. Seeds, in order: **43001,44010,45019,46028,47037**; crystal offset +777, rejection +8181, time +4242. Measurement index advances for each retained broadband event. Integer counts, no renormalisation. No dropped seeds.

H-3 physical controls: .3/.2 mm pitches, 60×60/90×90, same 18 mm footprint. Gap=p/6 (.05/.033333… mm) preserves fill factor (5/6)². Seed 43001,60 s per point. The geometric fine-pitch controls have zero gap. **90×90 is an engine experiment, not a supported Studio preset**: Studio validates at most 64 pixels. Harness builds an allowed config then explicitly overrides detector dimensions.

### Physical mean accounting

Every row: seed 61001,3M histories. W sums importance weights, not event counts. Planes in mm on the uniform 10 mm scan.

| z | a | transport hits | W all | W win | plane all | plane win |
|---:|---:|---:|---:|---:|---:|---:|
|300|0|476802|136.472515709|41.8682861187|360|310|
|300|15|471256|134.841078548|41.3838152176|360|360|
|300|30|452662|129.406545613|40.1390063514|280|280|
|500|0|485967|50.1029921966|15.3398920938|580|580|
|500|15|478862|49.3544919610|15.1105326725|470|460|
|500|30|440189|45.3212169984|14.1851047556|460|460|
|700|0|488432|25.6964262885|7.82967014783|680|680|
|700|15|479989|25.2438558928|7.76036533880|560|580|
|700|30|438837|23.0551147254|7.22130770215|560|560|

### Standard 60 s path: maxima and spread

Slash-separated planes follow the five seeds above. Bias=mean minus true z; s=sample SD (n-1). Zero s means the **10 mm node repeated**, not zero physical uncertainty. No tolerance was borrowed/fitted. Exact histories/counts are in the appendix.

|z|a|all planes|all bias / s mm|win planes|win bias / s mm|
|---:|---:|---|---|---|---|
|300|0|360/360/360/360/360|+60 / 0|360/310/310/310/310|+20 / 22.36|
|300|15|360/360/360/360/360|+60 / 0|310/360/360/360/310|+40 / 27.39|
|300|30|280/280/280/280/280|-20 / 0|270/280/280/280/270|-24 / 5.48|
|500|0|580/580/580/580/580|+80 / 0|580/470/470/580/580|+36 / 60.25|
|500|15|470/470/560/470/560|+6 / 49.30|460/470/460/460/460|-38 / 4.47|
|500|30|460/460/460/470/460|-38 / 4.47|460/460/460/460/460|-40 / 0|
|700|0|680/680/680/680/680|-20 / 0|680/680/680/680/810|+6 / 58.14|
|700|15|160/560/580/560/560|-216 / 181.33|560/560/570/680/570|-112 / 51.67|
|700|30|560/560/560/560/560|-140 / 0|560/560/680/560/560|-116 / 53.67|

## Hypotheses: verdicts and separating tests

| Hypothesis | Verdict / measured separation |
|---|---|
| **H-1: peak-node sampling** | **Confirmed contributor; rejected as sole explanation/sufficient fix.** Global half-step grid moves physical mean axis-300 all 360→310 and 300/30 280→300; axis-500 stays 580,700/30 becomes **920**. Local exact sampling and parabolic vertex values also move maxima, inconsistently. Five list seeds: local dense axis-300 360→310 every time; axis-500 580→430 every time. “Physical grid scaling alone changes axis phase” is rejected: angular phase is fixed. |
| **H-2: normalisation** | **Confirmed contributor; rejected as sole explanation/sufficient fix.** Exclude a radius-four-grid-step disk (one resolution element) when estimating field mean/std: physical mean axis-300 360→310, axis-500 stays 580,700/30 stays 560. All five list axis-500 seeds stay 580. Raw peak instead gives axis means 300/490/690, but 700/15 stays 560. Fixed-angle grid is already present. |
| **H-3: undersampling** | **Confirmed contributor; rejected as sole cause.** Noise-free .6/.3/.2 mm pitches: axis-300 360/290/300, axis-500 580/530/550,700/30 680/650/630. No monotonic all-point disappearance. Physical 60 s controls: axis-300 360/290/270, axis-500 580/530/550,700/30 560/670/680 (seed 43001). Finer sampling changes alias-dependent maxima substantially, but does not repair the heuristic or physical response mismatch. Lateral repeat structure measured below. |
| **H-4: physical content/edges** | **Rejected as necessary/sole origin; confirmed modifier.** Single-line geometric mean already has axis +60/+80 mm bias. Removing slab clipping while preserving closed-cell attenuation leaves 360/580. At 300/30 geometry=350 vs physical=280; at 500/15 geometry=560 vs physical=470 all/460 win. Cyclic physical decode retains 360/580 axis and560 at700/30. Gap-only geometry moves500/30 430→460. Crystal transport, finite interaction depth and spectrum were removed together; their individual contributions are not established. |
| **H-5: metric itself** | **Confirmed: prominence is not a calibrated physical depth likelihood; unbiased replacement not yet validated.** Known-angle response divided by field std still peaks580 for physical500/15 and910 for700/30. Integrated geometric likelihood gives -1…+1 mm on all nine controls. Existing centroid/oracle alternation remain biased. Physical likelihood reduces large offsets, conditional on bearing, but retains model-library bias/spread. |
| **H-6 added: pixel-scale forward mismatch** | **Confirmed in geometric control.** Decoder samples centres/thin-plane ±1; acquisition integrates area/slab transmission. Keeping the noise-free data fixed, using its integrated forward likelihood removes large offsets to a1 mm search grid. Thick-vs-thin test excludes slab thickness alone. Physical .2 mm path retains shifts when ideal geometry gives300. No attribution of separate DOI/Compton/gap/normalisation fractions. |
| **H-7 added: finite-MC template interpolation** | **Confirmed validation hazard.** Initial500k templates at150+20i put broadband mean fits almost exactly300/500/700 (template midpoints). Independent3M templates at140+20i, with truths on template nodes, give299/301/299,506/505/495,708/707/708. Budget and origin both changed; their separate sizes are not identified. Mixing noisy neighbours can favour interior mixtures. First near-zero errors are not validation. |

H-1/H-2/H-4 variants use the **same physical mean** without changing transport seed/configuration. Local dense=PointResponse on9×9 positions centred on the original peak, offsets ±step in step/4 increments; original field mean/std retained. Parabolic value adds separable fitted vertex gains to the original value: diagnostic only, not a calibrated rule for a piecewise-constant response. Global2 halves step throughout the field (100×100 nodes).

|z|a|original all|local dense|parabolic|excluded std|raw peak|global2 all/win|cyclic all/win|
|---:|---:|---:|---:|---:|---:|---:|---|---|
|300|0|360|310|310|310|300|310/310|360/360|
|300|15|360|310|360|310|310|310/310|360/360|
|300|30|280|300|280|280|280|300/300|280/270|
|500|0|580|430|580|580|490|580/470|580/580|
|500|15|470|430|470|460|460|430/430|460/460|
|500|30|460|430|470|460|510|430/460|460/460|
|700|0|680|580|680|680|690|580/600|630/630|
|700|15|560|560|**160**|580|560|600/600|580/570|
|700|30|560|580|560|560|560|920/600|560/560|

Current inverse-distance81-plane physical broadband maxima (angles0/15/30): z300→350.74/350.74/276.44; z500→569.33/455.72/455.72; z700→650.41/569.33/569.33 mm. Bias persists; the planner's uniform plane list is not its origin.

### Noise-free pitch control

q24,no gap,ideal detector,single line,expected thick-mask transmission. q48 repeat at .6 leaves all nine **original-score** maxima unchanged. Total probabilities change by at most about .08%; near-tied alternative metrics can switch maxima (local dense500/30 430→580), so small integral error does not imply stable argmax.

|z|a|plane p=.6|p=.3|p=.2|q48 probability p=.6|
|---:|---:|---:|---:|---:|---:|
|300|0|360|290|300|.000163407963664|
|300|15|350|310|300|.000162132747130|
|300|30|350|310|300|.000157590507581|
|500|0|580|530|550|.0000591534026207|
|500|15|560|530|540|.0000585736560861|
|500|30|430|480|550|.0000548859435613|
|700|0|**160**|670|680|.0000301988745931|
|700|15|**160**|740|630|.0000297762061834|
|700|30|680|650|630|.0000275078009417|

### Fine lateral sweep

z500,x=0…20 mm in **.25 mm increments**,y0; a=1000atan(x/500). All81 .6 mm points use q24 and the full81-depth scan. Selected depths range **430–600 mm**. At integer x=0…20:

~~~text
x:  0   1   2   3   4   5   6   7   8   9  10  11  12  13  14  15  16  17  18  19  20
z:580 580 470 580 570 460 460 560 470 430 560 450 470 560 460 430 460 430 460 470 550
~~~

This is oscillatory, not an exact single-period depth offset. geo.csv retains all81 points. Also decode each point at correct z500 for pitches .6/.3/.2 with Tent interpolation. x-error RMS/max: **.18002/.57122**, **.09337/.22713**, **.09273/.20384 mm**. Demeaned, overlap-normalised .6 mm error autocorrelation has local maxima **3.50 mm (.5058)** and **7.50 mm (.4298)**. At .3 its strongest positive local maximum up to10 mm is3.25 (.3870); .2 residual peaks at2.25 and1.00 mm. These are measured repeat structures, not a universal period or theme55's inherited y-sweep period. Finite-mask/grid structure precludes a pure periodic depth correction.

## Existing estimators and forward likelihood

Evaluate DepthStudy on the **same retained floods**, without regenerating data or re-Poissonising. Known-lateral centroid supplies actual x,y; oracle alternation supplies true S and calls unchanged private EstimateJoint through reflection. Both give information unavailable to Studio.

Physical likelihood: normalised900-pixel probability vector p_i(z,a), maximise sum n_i log p_i. Strength profiled out by normalisation; **true bearing a and y=0 held fixed**. Global depth range, probability-vector linear interpolation,1 mm fit grid. No offset subtraction, retuning or truth-centred search range.

Template A: z=150+20i,i0…40,a0/15/30, **500,000 histories each**,seed=72001+z+1009a. B: z=140+20i,same angles, **3,000,000 histories each**,seed=272001+z+1009a. Each has123 templates. Physical config/chain identical to above. templates.csv/templateshi.csv record **every** template seed,histories,hits and all/window W; per-template JSON retains the flood vectors and accounting. Validation data seeds are separate. Spread below is conditional on one fixed library: **library uncertainty excluded**.

Geometric likelihood: templates150+10i,q24,data at the nine points q48; independent quadrature resolutions, no noisy library. Errors at z300/500/700 for angles0/15/30 are **0/-1/0**, **0/0/+1**, **0/0/0 mm**. The flood contains depth information; this does not establish unbiasedness for unknown bearing or physical/finite-count acquisition.

### Physical mean estimates (mm)

|z|a|known-lateral all/win|oracle alternating all/win|likelihood B all/win|
|---:|---:|---|---|---|
|300|0|301.18/301.50|301.18/301.50|299/296|
|300|15|294.92/294.03|291.17/289.03|301/304|
|300|30|292.90/291.43|304.66/293.36|299/304|
|500|0|525.78/521.88|525.78/521.88|506/491|
|500|15|509.98/510.55|492.09/490.77|505/491|
|500|30|491.13/487.26|516.82/519.88|495/492|
|700|0|722.21/707.75|754.99/707.75|708/691|
|700|15|697.18/695.54|**618.16/615.53**|707/709|
|700|30|680.96/677.74|703.66/739.08|708/690|

Pure geometric centroid: axis500=534.73,axis700=731.45. Geometric oracle alternating: axis700=758.95,700/15=658.25. A good seed is not a guarantee at Studio geometry.

### 60 s broadband: bias / sample s (mm), five seeds

|z|a|known-lateral|oracle alternating|likelihood B|
|---:|---:|---|---|---|
|300|0|+1.18/.18|+1.18/.18|-.40/1.34|
|300|15|-4.98/.47|-9.25/1.24|+1.40/1.34|
|300|30|-6.90/.66|+3.66/2.21|-.80/1.10|
|500|0|+25.54/3.00|+25.54/3.00|+2.00/7.31|
|500|15|+10.90/1.98|-6.28/.94|+3.20/5.26|
|500|30|-8.63/1.02|+19.48/3.24|-3.40/4.98|
|700|0|+22.21/5.50|+42.82/22.98|+8.00/7.97|
|700|15|-1.17/9.12|+.97/76.13|+.60/10.24|
|700|30|-20.33/2.68|+2.30/1.50|-1.00/11.07|

### 60 s window: bias / sample s (mm), same seeds

|z|a|known-lateral|oracle alternating|likelihood B|
|---:|---:|---|---|---|
|300|0|+1.61/.69|+1.61/.69|-.60/5.59|
|300|15|-5.55/1.41|-9.31/3.60|-1.20/5.67|
|300|30|-8.98/.93|-8.13/2.09|+.40/4.10|
|500|0|+19.83/5.32|+20.25/5.65|-1.80/10.85|
|500|15|+9.56/3.20|-4.93/9.06|+5.00/8.46|
|500|30|-12.23/1.98|+30.33/33.57|-1.20/8.64|
|700|0|+18.66/2.17|+39.58/20.26|+9.80/13.14|
|700|15|-1.17/12.59|+24.54/96.27|-8.60/14.74|
|700|30|-24.87/4.52|+56.39/63.11|+19.80/27.12|

These are measurements, not tolerances or precision promises. Five seeds do not establish a general bias bound. Broad intervals/multiple maxima matter more than reporting resolution at700 mm.

## Proposed changes / discussion

No production change recommended from this review alone. Correct the fixed-angle and “calibrated refine” premises. Do not borrow historic tolerances or subtract centimetre offsets. The measured improvements **and failures** above rule out accepting a single metric tweak as an unbiased estimator.

If depth estimation is wanted, investigate **joint x/y/z forward likelihood**, integrating active area/slab/crystal/measurement response and profiling intensity/background. Estimate bearing from data. Independently converge templates; vary library grid origins/budgets and use off-grid truths/held-out seeds. Measure bias, spread, wrong-maximum frequency and interval coverage before proposing a Studio replacement. The present conditional likelihood is a separating control, not a completed implementation design.

## Execution / reproduction

Harness builds succeeded, zero warnings/errors. No installs, deletions, git mutation, process termination, persistent environment changes, uploads or GUI launches. A first partial geometry run stopped at Studio's64-pixel validation; the temporary harness was corrected and the complete run repeated. The first thin diagnostic did not preserve μt and was superseded. Neither preliminary run supplies a reported result. Separate temporary output directories avoided overwriting running executables.

Retained outputs: geo.csv,physical.csv,templates.csv,templateshi.csv,list.csv,fine.csv,geoest.csv,est.csv,est-hi.csv,listfine.csv,lateral.csv,geolik.csv; counts.csv and summary files. The diagnostic list-summary.csv field counts=5 is a PowerShell **collection count**, not photons; photon accounting uses original LIST rows and the appendix.

Replay the retained final harness from repository cwd (data output is the original temporary data directory):

~~~powershell
$taskDir = Join-Path $env:TEMP 'gcam-depthbias-20261002-120904'
$dataDir = Join-Path $taskDir 'bin/Release/net9.0'
$replayDir = Join-Path $taskDir 'replay'
dotnet build (Join-Path $taskDir 'Probe.csproj') -c Release --nologo -o $replayDir
$probe = Join-Path $replayDir 'Probe.dll'
dotnet $probe manifest $dataDir
dotnet $probe geo > (Join-Path $taskDir 'replay-geo.csv')
dotnet $probe physical $dataDir > (Join-Path $taskDir 'replay-physical.csv')
dotnet $probe templates $dataDir > (Join-Path $taskDir 'replay-templates.csv')
dotnet $probe templateshi $dataDir > (Join-Path $taskDir 'replay-templateshi.csv')
dotnet $probe list $dataDir > (Join-Path $taskDir 'replay-list.csv')
dotnet $probe fine $dataDir > (Join-Path $taskDir 'replay-fine.csv')
dotnet $probe geoest > (Join-Path $taskDir 'replay-geoest.csv')
dotnet $probe analyze $dataDir templates > (Join-Path $taskDir 'replay-est.csv')
dotnet $probe analyze $dataDir templateshi > (Join-Path $taskDir 'replay-est-hi.csv')
dotnet $probe listfine > (Join-Path $taskDir 'replay-listfine.csv')
dotnet $probe lateral > (Join-Path $taskDir 'replay-lateral.csv')
dotnet $probe geolik > (Join-Path $taskDir 'replay-geolik.csv')
dotnet $probe anchorcheck > (Join-Path $taskDir 'replay-anchorcheck.csv')
~~~

Original runs used -o analysis/high/fine/listfine/lateral/geolik directories under the same temporary tree. The literal executable/output paths are in the command log; the replay is sequential and reproduces the final experiment definitions.

## Not run / limits

- No desktop,Studio launch,UI tests or automation, as instructed.
- No repository unit suite: no production/test code changed; builds and temporary measurements are the performed verification.
- No joint,bearing-independent physical likelihood; no unbiased general estimator established.
- No exact deterministic Compton mean. Geometric controls are noise-free; physical expected floods contain finite weighted MC error. Library convergence and independently separated grid/budget effects remain incomplete.
- No individual DOI/Compton/spectrum attribution, exact-period proof, signed/y/diagonal source grid, multiple sources or background sweep. Required angle points use x with y=0.
- Fine-detector list controls use one seed/point; no fine-pitch spread claim. Standard-path estimator spreads use five seeds at every required point.
- Raw harness/evidence retained in permitted temp tree. This review preserves findings for the next discussion turn.

## Accounting appendix: standard 60 s path

Each row: z, angle, seed, histories, all and window counts. The terminating event beyond 60 s is excluded from counts but its history is included. All 45 acquisitions are listed.

~~~csv
z,a,seed,histories,all,win
300,0,43001,292500,46663,14471
300,0,44010,292922,46639,14264
300,0,45019,293527,46499,14350
300,0,46028,292509,46626,14196
300,0,47037,294004,46579,14388
300,15,43001,290408,45653,13956
300,15,44010,291508,45780,14037
300,15,45019,295221,45976,14120
300,15,46028,293238,46313,14226
300,15,47037,295245,46398,14348
300,30,43001,292748,44321,13747
300,30,44010,293168,44270,13765
300,30,45019,294331,44450,13842
300,30,46028,292625,44172,13646
300,30,47037,292493,43745,13595
500,0,43001,107435,17663,5398
500,0,44010,104865,16980,5111
500,0,45019,105913,17382,5349
500,0,46028,105801,17199,5197
500,0,47037,106049,17057,5259
500,15,43001,105875,17083,5249
500,15,44010,104588,16616,5027
500,15,45019,105707,16939,5139
500,15,46028,106761,17034,5173
500,15,47037,104099,16729,5121
500,30,43001,106471,15759,4906
500,30,44010,104256,15411,4862
500,30,45019,105462,15488,4816
500,30,46028,106475,15798,4971
500,30,47037,104805,15270,4838
700,0,43001,54068,8940,2654
700,0,44010,53634,8711,2615
700,0,45019,54119,8829,2665
700,0,46028,54080,8791,2695
700,0,47037,53004,8578,2653
700,15,43001,53754,8696,2617
700,15,44010,53603,8507,2606
700,15,45019,53943,8682,2619
700,15,46028,54556,8707,2756
700,15,47037,53179,8552,2663
700,30,43001,53601,7748,2363
700,30,44010,53912,7850,2414
700,30,45019,55120,8057,2431
700,30,46028,54183,8014,2495
700,30,47037,52355,7593,2406
~~~

## Accounting appendix: fine detector, 60 s

All rows seed=43001; gap=p/6; same activity/chain/window. Same order as each following pair of METRIC rows (all then win). The metric rows' columns are label,z,angle,pitch,total,original/local/parabolic/excluded/raw/dense-raw maximum planes, then their maximum scores. Local/parabolic were not evaluated in this mode and repeat the original score; use only original/excluded/raw columns.

~~~csv
LISTFINE,300,0,0.3,gap=0.049999999999999996,seed=43001,histories=291376,all=46335,win=14223
METRIC,listfine_all,300,0,0.3,46335,290,290,290,290,290,290,9.7438609,9.7438609,9.7438609,13.51351,26999,26999
METRIC,listfine_win,300,0,0.3,14223,290,290,290,290,290,290,9.6695317,9.6695317,9.6695317,13.137939,7179,7179
LISTFINE,300,15,0.3,gap=0.049999999999999996,seed=43001,histories=291783,all=45639,win=14097
METRIC,listfine_all,300,15,0.3,45639,290,290,290,290,290,290,9.3601526,9.3601526,9.3601526,12.791965,26633,26633
METRIC,listfine_win,300,15,0.3,14097,290,290,290,300,290,290,9.2942725,9.2942725,9.2942725,12.54228,7307,7307
LISTFINE,300,30,0.3,gap=0.049999999999999996,seed=43001,histories=292083,all=44040,win=13725
METRIC,listfine_all,300,30,0.3,44040,300,300,300,300,300,300,9.5999586,9.5999586,9.5999586,13.146679,23134,23134
METRIC,listfine_win,300,30,0.3,13725,300,300,300,300,300,300,9.656624,9.656624,9.656624,13.310048,6393,6393
LISTFINE,500,0,0.3,gap=0.049999999999999996,seed=43001,histories=106706,all=17278,win=5342
METRIC,listfine_all,500,0,0.3,17278,530,530,530,530,500,500,10.528861,10.528861,10.528861,14.720228,9862,9862
METRIC,listfine_win,500,0,0.3,5342,470,470,470,470,470,470,10.003285,10.003285,10.003285,13.499299,2692,2692
LISTFINE,500,15,0.3,gap=0.049999999999999996,seed=43001,histories=106275,all=17021,win=5235
METRIC,listfine_all,500,15,0.3,17021,470,470,470,470,470,470,9.920096,9.920096,9.920096,13.448955,9261,9261
METRIC,listfine_win,500,15,0.3,5235,470,470,470,470,470,470,9.3504031,9.3504031,9.3504031,12.147648,2491,2491
LISTFINE,500,30,0.3,gap=0.049999999999999996,seed=43001,histories=105248,all=15589,win=4913
METRIC,listfine_all,500,30,0.3,15589,470,470,470,470,470,470,9.5698156,9.5698156,9.5698156,13.009329,8216,8216
METRIC,listfine_win,500,30,0.3,4913,470,470,470,470,470,470,9.039434,9.039434,9.039434,11.80352,2205,2205
LISTFINE,700,0,0.3,gap=0.049999999999999996,seed=43001,histories=54303,all=8714,win=2777
METRIC,listfine_all,700,0,0.3,8714,670,670,670,690,690,690,10.395324,10.395324,10.395324,14.226472,4806,4806
METRIC,listfine_win,700,0,0.3,2777,700,700,700,690,690,690,9.7266741,9.7266741,9.7266741,12.81005,1419,1419
LISTFINE,700,15,0.3,gap=0.049999999999999996,seed=43001,histories=53169,all=8465,win=2640
METRIC,listfine_all,700,15,0.3,8465,670,670,670,670,670,670,9.5641781,9.5641781,9.5641781,12.698041,4429,4429
METRIC,listfine_win,700,15,0.3,2640,670,670,670,690,690,690,8.8367533,8.8367533,8.8367533,11.409117,1217,1217
LISTFINE,700,30,0.3,gap=0.049999999999999996,seed=43001,histories=53366,all=7750,win=2395
METRIC,listfine_all,700,30,0.3,7750,670,670,670,670,670,670,9.1751609,9.1751609,9.1751609,11.886732,3916,3916
METRIC,listfine_win,700,30,0.3,2395,750,750,750,670,690,690,8.4948538,8.4948538,8.4948538,10.543972,1065,1065
LISTFINE,300,0,0.2,gap=0.03333333333333333,seed=43001,histories=292760,all=46092,win=14230
METRIC,listfine_all,300,0,0.2,46092,270,270,270,280,270,270,10.216415,10.216415,10.216415,14.590744,27504,27504
METRIC,listfine_win,300,0,0.2,14230,270,270,270,280,270,270,10.290455,10.290455,10.290455,14.812629,7444,7444
LISTFINE,300,15,0.2,gap=0.03333333333333333,seed=43001,histories=289949,all=45440,win=14143
METRIC,listfine_all,300,15,0.2,45440,280,280,280,280,280,280,9.58374,9.58374,9.58374,13.558697,26368,26368
METRIC,listfine_win,300,15,0.2,14143,280,280,280,280,280,280,9.8880963,9.8880963,9.8880963,14.132451,7337,7337
LISTFINE,300,30,0.2,gap=0.03333333333333333,seed=43001,histories=291665,all=44099,win=13636
METRIC,listfine_all,300,30,0.2,44099,280,280,280,280,280,280,10.396555,10.396555,10.396555,14.452089,24543,24543
METRIC,listfine_win,300,30,0.2,13636,280,280,280,280,280,280,10.761114,10.761114,10.761114,15.1548,6806,6806
LISTFINE,500,0,0.2,gap=0.03333333333333333,seed=43001,histories=105996,all=17021,win=5349
METRIC,listfine_all,500,0,0.2,17021,550,550,550,550,490,490,10.930071,10.930071,10.930071,15.627967,9965,9965
METRIC,listfine_win,500,0,0.2,5349,550,550,550,550,490,490,10.65101,10.65101,10.65101,14.387865,2749,2749
LISTFINE,500,15,0.2,gap=0.03333333333333333,seed=43001,histories=105818,all=16919,win=5219
METRIC,listfine_all,500,15,0.2,16919,480,480,480,550,470,470,10.532415,10.532415,10.532415,14.761464,9639,9639
METRIC,listfine_win,500,15,0.2,5219,470,470,470,470,490,490,9.8354448,9.8354448,9.8354448,13.16765,2579,2579
LISTFINE,500,30,0.2,gap=0.03333333333333333,seed=43001,histories=106692,all=15716,win=4961
METRIC,listfine_all,500,30,0.2,15716,540,540,540,540,470,470,9.8461669,9.8461669,9.8461669,13.499773,8582,8582
METRIC,listfine_win,500,30,0.2,4961,540,540,540,540,490,490,9.3507313,9.3507313,9.3507313,12.100118,2337,2337
LISTFINE,700,0,0.2,gap=0.03333333333333333,seed=43001,histories=54045,all=8890,win=2700
METRIC,listfine_all,700,0,0.2,8890,680,680,680,680,630,630,11.903864,11.903864,11.903864,17.255773,5554,5554
METRIC,listfine_win,700,0,0.2,2700,680,680,680,680,630,630,11.320553,11.320553,11.320553,15.764299,1584,1584
LISTFINE,700,15,0.2,gap=0.03333333333333333,seed=43001,histories=53138,all=8363,win=2571
METRIC,listfine_all,700,15,0.2,8363,630,630,630,630,630,630,11.271761,11.271761,11.271761,15.594428,5121,5121
METRIC,listfine_win,700,15,0.2,2571,630,630,630,630,630,630,10.47587,10.47587,10.47587,13.73575,1389,1389
LISTFINE,700,30,0.2,gap=0.03333333333333333,seed=43001,histories=53820,all=7726,win=2449
METRIC,listfine_all,700,30,0.2,7726,680,680,680,680,630,630,10.550649,10.550649,10.550649,14.495754,4300,4300
METRIC,listfine_win,700,30,0.2,2449,680,680,680,680,630,630,10.23702,10.23702,10.23702,13.702977,1221,1221
~~~

## Accounting appendix: all physical templates

Each TRANSPORT row states set,z,angle,seed,histories,detected hits,all W,window W. These are importance-weighted forward-response calculations, not 60 s event records. Set A has 500k histories per template; set B has 3M, with the shifted grid and independent seeds documented above. All 246 templates follow.

~~~csv
TRANSPORT,templates,150,0,seed=72151,histories=500000,hits=72293,allW=82.5688991157,winW=25.7092555614
TRANSPORT,templates,170,0,seed=72171,histories=500000,hits=73490,allW=65.3860408762,winW=20.1906040514
TRANSPORT,templates,190,0,seed=72191,histories=500000,hits=77795,allW=55.4381585976,winW=16.9136468331
TRANSPORT,templates,210,0,seed=72211,histories=500000,hits=78297,allW=45.6903248684,winW=13.986665392
TRANSPORT,templates,230,0,seed=72231,histories=500000,hits=78623,allW=38.2603950035,winW=11.7145904568
TRANSPORT,templates,250,0,seed=72251,histories=500000,hits=79341,allW=32.6876815285,winW=10.0110902833
TRANSPORT,templates,270,0,seed=72271,histories=500000,hits=79731,allW=28.167635094,winW=8.61410826448
TRANSPORT,templates,290,0,seed=72291,histories=500000,hits=79442,allW=24.3318316372,winW=7.47532289019
TRANSPORT,templates,310,0,seed=72311,histories=500000,hits=79230,allW=21.2393582563,winW=6.52579068029
TRANSPORT,templates,330,0,seed=72331,histories=500000,hits=80064,allW=18.9420850013,winW=5.83003254021
TRANSPORT,templates,350,0,seed=72351,histories=500000,hits=80266,allW=16.8830140904,winW=5.17706677771
TRANSPORT,templates,370,0,seed=72371,histories=500000,hits=80181,allW=15.0922312118,winW=4.6666915364
TRANSPORT,templates,390,0,seed=72391,histories=500000,hits=79385,allW=13.4499405667,winW=4.10195976211
TRANSPORT,templates,410,0,seed=72411,histories=500000,hits=79745,allW=12.2255008053,winW=3.79970500819
TRANSPORT,templates,430,0,seed=72431,histories=500000,hits=79856,allW=11.1306310302,winW=3.4130545182
TRANSPORT,templates,450,0,seed=72451,histories=500000,hits=80603,allW=10.2586631232,winW=3.18829398679
TRANSPORT,templates,470,0,seed=72471,histories=500000,hits=81003,allW=9.45112931149,winW=2.88278800042
TRANSPORT,templates,490,0,seed=72491,histories=500000,hits=81596,allW=8.75925911412,winW=2.67155591555
TRANSPORT,templates,510,0,seed=72511,histories=500000,hits=81471,allW=8.07355841441,winW=2.4882894421
TRANSPORT,templates,530,0,seed=72531,histories=500000,hits=81326,allW=7.46258955046,winW=2.28857563983
TRANSPORT,templates,550,0,seed=72551,histories=500000,hits=81001,allW=6.90217105422,winW=2.10048739331
TRANSPORT,templates,570,0,seed=72571,histories=500000,hits=80713,allW=6.40358212778,winW=1.96649719049
TRANSPORT,templates,590,0,seed=72591,histories=500000,hits=81257,allW=6.01718092372,winW=1.8396825828
TRANSPORT,templates,610,0,seed=72611,histories=500000,hits=81303,allW=5.63235422307,winW=1.7222984633
TRANSPORT,templates,630,0,seed=72631,histories=500000,hits=80969,allW=5.25879403147,winW=1.59398221783
TRANSPORT,templates,650,0,seed=72651,histories=500000,hits=81441,allW=4.96901252926,winW=1.53591649902
TRANSPORT,templates,670,0,seed=72671,histories=500000,hits=81092,allW=4.65678973449,winW=1.43181853588
TRANSPORT,templates,690,0,seed=72691,histories=500000,hits=81558,allW=4.41602134417,winW=1.35430922091
TRANSPORT,templates,710,0,seed=72711,histories=500000,hits=81943,allW=4.19046173323,winW=1.28993608382
TRANSPORT,templates,730,0,seed=72731,histories=500000,hits=81750,allW=3.95468949581,winW=1.21012224692
TRANSPORT,templates,750,0,seed=72751,histories=500000,hits=81792,allW=3.74854031374,winW=1.14416052315
TRANSPORT,templates,770,0,seed=72771,histories=500000,hits=82040,allW=3.56714746698,winW=1.09880807662
TRANSPORT,templates,790,0,seed=72791,histories=500000,hits=81829,allW=3.38012325991,winW=1.03400888998
TRANSPORT,templates,810,0,seed=72811,histories=500000,hits=81695,allW=3.21001979049,winW=0.978514419793
TRANSPORT,templates,830,0,seed=72831,histories=500000,hits=81965,allW=3.06730536198,winW=0.931967840098
TRANSPORT,templates,850,0,seed=72851,histories=500000,hits=82119,allW=2.93017153177,winW=0.901797565043
TRANSPORT,templates,870,0,seed=72871,histories=500000,hits=82196,allW=2.79963461219,winW=0.854514944007
TRANSPORT,templates,890,0,seed=72891,histories=500000,hits=82359,allW=2.68054025561,winW=0.811923195108
TRANSPORT,templates,910,0,seed=72911,histories=500000,hits=82358,allW=2.56398928545,winW=0.791853553734
TRANSPORT,templates,930,0,seed=72931,histories=500000,hits=82373,allW=2.45535253176,winW=0.749490268226
TRANSPORT,templates,950,0,seed=72951,histories=500000,hits=83059,allW=2.37266274937,winW=0.723237527589
TRANSPORT,templates,150,15,seed=87286,histories=500000,hits=72096,allW=82.3220732879,winW=25.7970708771
TRANSPORT,templates,170,15,seed=87306,histories=500000,hits=72632,allW=64.6096865705,winW=20.0785540468
TRANSPORT,templates,190,15,seed=87326,histories=500000,hits=75839,allW=54.0309899622,winW=16.6655031458
TRANSPORT,templates,210,15,seed=87346,histories=500000,hits=76433,allW=44.592377288,winW=13.7666899407
TRANSPORT,templates,230,15,seed=87366,histories=500000,hits=77397,allW=37.6540415259,winW=11.5216632644
TRANSPORT,templates,250,15,seed=87386,histories=500000,hits=77963,allW=32.1110456621,winW=9.79578621884
TRANSPORT,templates,270,15,seed=87406,histories=500000,hits=78196,allW=27.6171885263,winW=8.42688339135
TRANSPORT,templates,290,15,seed=87426,histories=500000,hits=78449,allW=24.0199250464,winW=7.33029367119
TRANSPORT,templates,310,15,seed=87446,histories=500000,hits=78361,allW=20.999366491,winW=6.34751545246
TRANSPORT,templates,330,15,seed=87466,histories=500000,hits=78983,allW=18.6802741493,winW=5.72831737684
TRANSPORT,templates,350,15,seed=87486,histories=500000,hits=79532,allW=16.7232261001,winW=5.12492115307
TRANSPORT,templates,370,15,seed=87506,histories=500000,hits=79203,allW=14.903382775,winW=4.59575747396
TRANSPORT,templates,390,15,seed=87526,histories=500000,hits=79455,allW=13.4574896061,winW=4.15719553071
TRANSPORT,templates,410,15,seed=87546,histories=500000,hits=79177,allW=12.1345468233,winW=3.69978214496
TRANSPORT,templates,430,15,seed=87566,histories=500000,hits=79295,allW=11.0488826651,winW=3.35871298345
TRANSPORT,templates,450,15,seed=87586,histories=500000,hits=79330,allW=10.0933794732,winW=3.05721917728
TRANSPORT,templates,470,15,seed=87606,histories=500000,hits=79896,allW=9.31897324342,winW=2.83520802959
TRANSPORT,templates,490,15,seed=87626,histories=500000,hits=79979,allW=8.5828992821,winW=2.63354425334
TRANSPORT,templates,510,15,seed=87646,histories=500000,hits=79659,allW=7.89144176668,winW=2.43942423212
TRANSPORT,templates,530,15,seed=87666,histories=500000,hits=80099,allW=7.34762977926,winW=2.25287849569
TRANSPORT,templates,550,15,seed=87686,histories=500000,hits=80301,allW=6.84030788894,winW=2.08941143531
TRANSPORT,templates,570,15,seed=87706,histories=500000,hits=80234,allW=6.3634926951,winW=1.97005346167
TRANSPORT,templates,590,15,seed=87726,histories=500000,hits=79678,allW=5.89832585865,winW=1.79584662087
TRANSPORT,templates,610,15,seed=87746,histories=500000,hits=79943,allW=5.53630737871,winW=1.70302104035
TRANSPORT,templates,630,15,seed=87766,histories=500000,hits=79942,allW=5.19037805028,winW=1.5782606392
TRANSPORT,templates,650,15,seed=87786,histories=500000,hits=79851,allW=4.87039434938,winW=1.49436321884
TRANSPORT,templates,670,15,seed=87806,histories=500000,hits=80147,allW=4.60100685123,winW=1.42807420142
TRANSPORT,templates,690,15,seed=87826,histories=500000,hits=79692,allW=4.31356596285,winW=1.32939467928
TRANSPORT,templates,710,15,seed=87846,histories=500000,hits=79955,allW=4.08744058841,winW=1.26123353224
TRANSPORT,templates,730,15,seed=87866,histories=500000,hits=79769,allW=3.85757742774,winW=1.19332575674
TRANSPORT,templates,750,15,seed=87886,histories=500000,hits=80049,allW=3.66744212519,winW=1.13736606735
TRANSPORT,templates,770,15,seed=87906,histories=500000,hits=80189,allW=3.48549322791,winW=1.07388252475
TRANSPORT,templates,790,15,seed=87926,histories=500000,hits=79234,allW=3.2718400613,winW=0.998192319005
TRANSPORT,templates,810,15,seed=87946,histories=500000,hits=79815,allW=3.1351020512,winW=0.968954693262
TRANSPORT,templates,830,15,seed=87966,histories=500000,hits=79703,allW=2.98165559578,winW=0.93150576852
TRANSPORT,templates,850,15,seed=87986,histories=500000,hits=79587,allW=2.83887192721,winW=0.879522526067
TRANSPORT,templates,870,15,seed=88006,histories=500000,hits=78914,allW=2.68694367913,winW=0.82106360768
TRANSPORT,templates,890,15,seed=88026,histories=500000,hits=78940,allW=2.56839235157,winW=0.790011217523
TRANSPORT,templates,910,15,seed=88046,histories=500000,hits=78897,allW=2.45541343495,winW=0.758534468484
TRANSPORT,templates,930,15,seed=88066,histories=500000,hits=79296,allW=2.36283433933,winW=0.729512200503
TRANSPORT,templates,950,15,seed=88086,histories=500000,hits=79098,allW=2.25875239844,winW=0.694639587207
TRANSPORT,templates,150,30,seed=102421,histories=500000,hits=67926,allW=77.5107886634,winW=24.4421691523
TRANSPORT,templates,170,30,seed=102441,histories=500000,hits=70571,allW=62.733754115,winW=19.5736619664
TRANSPORT,templates,190,30,seed=102461,histories=500000,hits=73078,allW=52.0327837976,winW=16.1249096669
TRANSPORT,templates,210,30,seed=102481,histories=500000,hits=72515,allW=42.2799241079,winW=13.2820147296
TRANSPORT,templates,230,30,seed=102501,histories=500000,hits=74012,allW=35.9820353631,winW=11.195319348
TRANSPORT,templates,250,30,seed=102521,histories=500000,hits=74441,allW=30.6362546064,winW=9.58416035795
TRANSPORT,templates,270,30,seed=102541,histories=500000,hits=75012,allW=26.4705928416,winW=8.25791047238
TRANSPORT,templates,290,30,seed=102561,histories=500000,hits=75429,allW=23.0752926707,winW=7.13143089806
TRANSPORT,templates,310,30,seed=102581,histories=500000,hits=75812,allW=20.2981856547,winW=6.2629331924
TRANSPORT,templates,330,30,seed=102601,histories=500000,hits=75671,allW=17.8802776178,winW=5.55025982754
TRANSPORT,templates,350,30,seed=102621,histories=500000,hits=75318,allW=15.8219882974,winW=4.91443504368
TRANSPORT,templates,370,30,seed=102641,histories=500000,hits=74694,allW=14.0408949192,winW=4.41563273999
TRANSPORT,templates,390,30,seed=102661,histories=500000,hits=74472,allW=12.6007396389,winW=3.92715049802
TRANSPORT,templates,410,30,seed=102681,histories=500000,hits=74818,allW=11.454829677,winW=3.553364313
TRANSPORT,templates,430,30,seed=102701,histories=500000,hits=73985,allW=10.2985357877,winW=3.23302919758
TRANSPORT,templates,450,30,seed=102721,histories=500000,hits=73981,allW=9.40310730767,winW=2.93387883232
TRANSPORT,templates,470,30,seed=102741,histories=500000,hits=73693,allW=8.58660524638,winW=2.68597727456
TRANSPORT,templates,490,30,seed=102761,histories=500000,hits=74272,allW=7.96215233598,winW=2.50349519547
TRANSPORT,templates,510,30,seed=102781,histories=500000,hits=73737,allW=7.29712465219,winW=2.29144348837
TRANSPORT,templates,530,30,seed=102801,histories=500000,hits=73122,allW=6.70053230974,winW=2.08908050268
TRANSPORT,templates,550,30,seed=102821,histories=500000,hits=73420,allW=6.24754977587,winW=1.94409330536
TRANSPORT,templates,570,30,seed=102841,histories=500000,hits=73331,allW=5.80990095122,winW=1.83895331519
TRANSPORT,templates,590,30,seed=102861,histories=500000,hits=73400,allW=5.4278652294,winW=1.70864502601
TRANSPORT,templates,610,30,seed=102881,histories=500000,hits=73460,allW=5.08197593089,winW=1.58163493261
TRANSPORT,templates,630,30,seed=102901,histories=500000,hits=73106,allW=4.74152890789,winW=1.47214830866
TRANSPORT,templates,650,30,seed=102921,histories=500000,hits=73625,allW=4.48586665311,winW=1.4032198291
TRANSPORT,templates,670,30,seed=102941,histories=500000,hits=73382,allW=4.2081742847,winW=1.31273691214
TRANSPORT,templates,690,30,seed=102961,histories=500000,hits=72875,allW=3.94037963024,winW=1.2375988458
TRANSPORT,templates,710,30,seed=102981,histories=500000,hits=72824,allW=3.71895471291,winW=1.16866053705
TRANSPORT,templates,730,30,seed=103001,histories=500000,hits=73234,allW=3.5377979946,winW=1.1061328498
TRANSPORT,templates,750,30,seed=103021,histories=500000,hits=72875,allW=3.3352309464,winW=1.0405709503
TRANSPORT,templates,770,30,seed=103041,histories=500000,hits=72865,allW=3.16379936416,winW=0.98548111575
TRANSPORT,templates,790,30,seed=103061,histories=500000,hits=72714,allW=2.99944772246,winW=0.942580115064
TRANSPORT,templates,810,30,seed=103081,histories=500000,hits=72725,allW=2.85358427952,winW=0.892368592892
TRANSPORT,templates,830,30,seed=103101,histories=500000,hits=72942,allW=2.72586124927,winW=0.854336339129
TRANSPORT,templates,850,30,seed=103121,histories=500000,hits=72615,allW=2.58746145912,winW=0.808448723386
TRANSPORT,templates,870,30,seed=103141,histories=500000,hits=72745,allW=2.474296733,winW=0.769530759889
TRANSPORT,templates,890,30,seed=103161,histories=500000,hits=72587,allW=2.35921914487,winW=0.74635375387
TRANSPORT,templates,910,30,seed=103181,histories=500000,hits=72622,allW=2.25774475571,winW=0.708957714688
TRANSPORT,templates,930,30,seed=103201,histories=500000,hits=72646,allW=2.16240872323,winW=0.680706790102
TRANSPORT,templates,950,30,seed=103221,histories=500000,hits=72763,allW=2.07566623864,winW=0.646790933543
TRANSPORT,templateshi,140,0,seed=272141,histories=3000000,hits=433331,allW=567.908875837,winW=178.954952802
TRANSPORT,templateshi,160,0,seed=272161,histories=3000000,hits=437837,allW=439.659745578,winW=136.076405671
TRANSPORT,templateshi,180,0,seed=272181,histories=3000000,hits=451347,allW=358.286674533,winW=110.09046522
TRANSPORT,templateshi,200,0,seed=272201,histories=3000000,hits=470324,allW=302.537368485,winW=92.2277604647
TRANSPORT,templateshi,220,0,seed=272221,histories=3000000,hits=469514,allW=249.686703041,winW=76.202004803
TRANSPORT,templateshi,240,0,seed=272241,histories=3000000,hits=476120,allW=212.818328484,winW=64.9670594039
TRANSPORT,templateshi,260,0,seed=272261,histories=3000000,hits=476475,allW=181.511584852,winW=55.236642702
TRANSPORT,templateshi,280,0,seed=272281,histories=3000000,hits=478396,allW=157.166300033,winW=48.0547924895
TRANSPORT,templateshi,300,0,seed=272301,histories=3000000,hits=475890,allW=136.211564294,winW=41.8313739458
TRANSPORT,templateshi,320,0,seed=272321,histories=3000000,hits=477263,allW=120.075917354,winW=36.8046124658
TRANSPORT,templateshi,340,0,seed=272341,histories=3000000,hits=479720,allW=106.922300738,winW=32.7284044769
TRANSPORT,templateshi,360,0,seed=272361,histories=3000000,hits=481285,allW=95.6903584137,winW=29.3851804612
TRANSPORT,templateshi,380,0,seed=272381,histories=3000000,hits=480318,allW=85.7157588227,winW=26.3962448293
TRANSPORT,templateshi,400,0,seed=272401,histories=3000000,hits=478492,allW=77.0683208868,winW=23.7497850983
TRANSPORT,templateshi,420,0,seed=272421,histories=3000000,hits=480120,allW=70.1441454057,winW=21.5679277124
TRANSPORT,templateshi,440,0,seed=272441,histories=3000000,hits=481991,allW=64.1639562854,winW=19.636661483
TRANSPORT,templateshi,460,0,seed=272461,histories=3000000,hits=483500,allW=58.8915502173,winW=18.0743825712
TRANSPORT,templateshi,480,0,seed=272481,histories=3000000,hits=485812,allW=54.3464163054,winW=16.657307038
TRANSPORT,templateshi,500,0,seed=272501,histories=3000000,hits=487489,allW=50.2598954591,winW=15.3804232376
TRANSPORT,templateshi,520,0,seed=272521,histories=3000000,hits=488232,allW=46.5400534137,winW=14.218572524
TRANSPORT,templateshi,540,0,seed=272541,histories=3000000,hits=488419,allW=43.1739270489,winW=13.2258573706
TRANSPORT,templateshi,560,0,seed=272561,histories=3000000,hits=486429,allW=39.9823572712,winW=12.2245055371
TRANSPORT,templateshi,580,0,seed=272581,histories=3000000,hits=485090,allW=37.1705495419,winW=11.3661997602
TRANSPORT,templateshi,600,0,seed=272601,histories=3000000,hits=487371,allW=34.8977041698,winW=10.6777403856
TRANSPORT,templateshi,620,0,seed=272621,histories=3000000,hits=486756,allW=32.6417430769,winW=9.99599203248
TRANSPORT,templateshi,640,0,seed=272641,histories=3000000,hits=487904,allW=30.7061605871,winW=9.40195463564
TRANSPORT,templateshi,660,0,seed=272661,histories=3000000,hits=487986,allW=28.87856825,winW=8.84702505238
TRANSPORT,templateshi,680,0,seed=272681,histories=3000000,hits=487573,allW=27.1820630028,winW=8.34856405567
TRANSPORT,templateshi,700,0,seed=272701,histories=3000000,hits=487841,allW=25.6653347763,winW=7.8534991843
TRANSPORT,templateshi,720,0,seed=272721,histories=3000000,hits=490749,allW=24.4041052687,winW=7.46036315513
TRANSPORT,templateshi,740,0,seed=272741,histories=3000000,hits=490040,allW=23.0696017629,winW=7.04499499949
TRANSPORT,templateshi,760,0,seed=272761,histories=3000000,hits=491593,allW=21.9408636746,winW=6.71448138936
TRANSPORT,templateshi,780,0,seed=272781,histories=3000000,hits=492183,allW=20.8552535567,winW=6.35621128761
TRANSPORT,templateshi,800,0,seed=272801,histories=3000000,hits=492213,allW=19.8268696673,winW=6.05586579955
TRANSPORT,templateshi,820,0,seed=272821,histories=3000000,hits=492823,allW=18.8949957472,winW=5.77164585274
TRANSPORT,templateshi,840,0,seed=272841,histories=3000000,hits=492213,allW=17.9837604966,winW=5.48505216215
TRANSPORT,templateshi,860,0,seed=272861,histories=3000000,hits=491579,allW=17.1350224073,winW=5.23738854297
TRANSPORT,templateshi,880,0,seed=272881,histories=3000000,hits=492514,allW=16.3962107316,winW=5.01000822898
TRANSPORT,templateshi,900,0,seed=272901,histories=3000000,hits=495045,allW=15.7562138545,winW=4.8034257008
TRANSPORT,templateshi,920,0,seed=272921,histories=3000000,hits=496200,allW=15.113850166,winW=4.60966253946
TRANSPORT,templateshi,940,0,seed=272941,histories=3000000,hits=496056,allW=14.4734039426,winW=4.42328327606
TRANSPORT,templateshi,140,15,seed=287276,histories=3000000,hits=429303,allW=562.450673085,winW=176.097193022
TRANSPORT,templateshi,160,15,seed=287296,histories=3000000,hits=433338,allW=435.045682103,winW=135.124542706
TRANSPORT,templateshi,180,15,seed=287316,histories=3000000,hits=444506,allW=352.777544408,winW=108.522120043
TRANSPORT,templateshi,200,15,seed=287336,histories=3000000,hits=457826,allW=294.432603446,winW=90.3671649894
TRANSPORT,templateshi,220,15,seed=287356,histories=3000000,hits=461205,allW=245.20844042,winW=75.6821754882
TRANSPORT,templateshi,240,15,seed=287376,histories=3000000,hits=464850,allW=207.722996618,winW=63.6640558168
TRANSPORT,templateshi,260,15,seed=287396,histories=3000000,hits=466500,allW=177.65868388,winW=54.4725233729
TRANSPORT,templateshi,280,15,seed=287416,histories=3000000,hits=470242,allW=154.43893219,winW=47.4857854545
TRANSPORT,templateshi,300,15,seed=287436,histories=3000000,hits=470002,allW=134.481893603,winW=41.0282679691
TRANSPORT,templateshi,320,15,seed=287456,histories=3000000,hits=472444,allW=118.824482712,winW=36.4168548362
TRANSPORT,templateshi,340,15,seed=287476,histories=3000000,hits=473211,allW=105.437257238,winW=32.2940845805
TRANSPORT,templateshi,360,15,seed=287496,histories=3000000,hits=474419,allW=94.2952433086,winW=28.9476894387
TRANSPORT,templateshi,380,15,seed=287516,histories=3000000,hits=473480,allW=84.4684537091,winW=25.8073532008
TRANSPORT,templateshi,400,15,seed=287536,histories=3000000,hits=474438,allW=76.3909546213,winW=23.3505405174
TRANSPORT,templateshi,420,15,seed=287556,histories=3000000,hits=473495,allW=69.154000682,winW=21.0357338847
TRANSPORT,templateshi,440,15,seed=287576,histories=3000000,hits=477057,allW=63.4865383226,winW=19.4492564783
TRANSPORT,templateshi,460,15,seed=287596,histories=3000000,hits=477746,allW=58.1718927825,winW=17.6767921646
TRANSPORT,templateshi,480,15,seed=287616,histories=3000000,hits=477269,allW=53.3735162303,winW=16.3639836107
TRANSPORT,templateshi,500,15,seed=287636,histories=3000000,hits=479610,allW=49.4314996501,winW=15.1062658359
TRANSPORT,templateshi,520,15,seed=287656,histories=3000000,hits=479344,allW=45.6780738144,winW=13.9588584708
TRANSPORT,templateshi,540,15,seed=287676,histories=3000000,hits=481461,allW=42.5450530306,winW=13.0219957411
TRANSPORT,templateshi,560,15,seed=287696,histories=3000000,hits=480621,allW=39.4920573227,winW=12.1205120107
TRANSPORT,templateshi,580,15,seed=287716,histories=3000000,hits=478740,allW=36.6719478167,winW=11.255520417
TRANSPORT,templateshi,600,15,seed=287736,histories=3000000,hits=480484,allW=34.3932361797,winW=10.580440593
TRANSPORT,templateshi,620,15,seed=287756,histories=3000000,hits=480836,allW=32.2341494946,winW=9.91038102454
TRANSPORT,templateshi,640,15,seed=287776,histories=3000000,hits=480207,allW=30.2117600698,winW=9.2804976937
TRANSPORT,templateshi,660,15,seed=287796,histories=3000000,hits=481216,allW=28.4684969442,winW=8.75645520093
TRANSPORT,templateshi,680,15,seed=287816,histories=3000000,hits=480344,allW=26.7701631195,winW=8.20238313871
TRANSPORT,templateshi,700,15,seed=287836,histories=3000000,hits=478170,allW=25.148204774,winW=7.69753298374
TRANSPORT,templateshi,720,15,seed=287856,histories=3000000,hits=478367,allW=23.7804612399,winW=7.31381263473
TRANSPORT,templateshi,740,15,seed=287876,histories=3000000,hits=478413,allW=22.5147316219,winW=6.89589580826
TRANSPORT,templateshi,760,15,seed=287896,histories=3000000,hits=478339,allW=21.3421845079,winW=6.57235969966
TRANSPORT,templateshi,780,15,seed=287916,histories=3000000,hits=477150,allW=20.2115004015,winW=6.24432294221
TRANSPORT,templateshi,800,15,seed=287936,histories=3000000,hits=477754,allW=19.2380142638,winW=5.90979578088
TRANSPORT,templateshi,820,15,seed=287956,histories=3000000,hits=476692,allW=18.270395314,winW=5.61083530806
TRANSPORT,templateshi,840,15,seed=287976,histories=3000000,hits=476298,allW=17.3964446951,winW=5.35387626681
TRANSPORT,templateshi,860,15,seed=287996,histories=3000000,hits=475299,allW=16.5619701854,winW=5.09508618174
TRANSPORT,templateshi,880,15,seed=288016,histories=3000000,hits=475157,allW=15.8130579811,winW=4.89093956437
TRANSPORT,templateshi,900,15,seed=288036,histories=3000000,hits=474550,allW=15.0987986841,winW=4.6473617859
TRANSPORT,templateshi,920,15,seed=288056,histories=3000000,hits=474599,allW=14.4510112212,winW=4.44327997549
TRANSPORT,templateshi,940,15,seed=288076,histories=3000000,hits=474581,allW=13.8421470823,winW=4.27028955505
TRANSPORT,templateshi,140,30,seed=302411,histories=3000000,hits=394300,allW=516.286164345,winW=165.287813193
TRANSPORT,templateshi,160,30,seed=302431,histories=3000000,hits=417784,allW=419.143061067,winW=131.340268736
TRANSPORT,templateshi,180,30,seed=302451,histories=3000000,hits=430589,allW=341.511590178,winW=106.530394505
TRANSPORT,templateshi,200,30,seed=302471,histories=3000000,hits=436984,allW=280.855719499,winW=87.9086971746
TRANSPORT,templateshi,220,30,seed=302491,histories=3000000,hits=438928,allW=233.210005044,winW=72.4587477936
TRANSPORT,templateshi,240,30,seed=302511,histories=3000000,hits=446219,allW=199.248176386,winW=61.5667100679
TRANSPORT,templateshi,260,30,seed=302531,histories=3000000,hits=448421,allW=170.635778436,winW=52.7326275935
TRANSPORT,templateshi,280,30,seed=302551,histories=3000000,hits=451583,allW=148.185597422,winW=45.9095778037
TRANSPORT,templateshi,300,30,seed=302571,histories=3000000,hits=452330,allW=129.311934978,winW=39.9740526325
TRANSPORT,templateshi,320,30,seed=302591,histories=3000000,hits=451927,allW=113.560463233,winW=35.2696474696
TRANSPORT,templateshi,340,30,seed=302611,histories=3000000,hits=451750,allW=100.560085952,winW=31.189248994
TRANSPORT,templateshi,360,30,seed=302631,histories=3000000,hits=450334,allW=89.4202537894,winW=27.9760809613
TRANSPORT,templateshi,380,30,seed=302651,histories=3000000,hits=446444,allW=79.5655451434,winW=24.9073587687
TRANSPORT,templateshi,400,30,seed=302671,histories=3000000,hits=446279,allW=71.7846074169,winW=22.4061103401
TRANSPORT,templateshi,420,30,seed=302691,histories=3000000,hits=446505,allW=65.1459362782,winW=20.385499791
TRANSPORT,templateshi,440,30,seed=302711,histories=3000000,hits=444049,allW=59.0334136652,winW=18.4431332233
TRANSPORT,templateshi,460,30,seed=302731,histories=3000000,hits=442952,allW=53.879859331,winW=16.8348739576
TRANSPORT,templateshi,480,30,seed=302751,histories=3000000,hits=443901,allW=49.5904440979,winW=15.4845567667
TRANSPORT,templateshi,500,30,seed=302771,histories=3000000,hits=441438,allW=45.4498451168,winW=14.209637442
TRANSPORT,templateshi,520,30,seed=302791,histories=3000000,hits=440404,allW=41.9231755012,winW=13.1112172508
TRANSPORT,templateshi,540,30,seed=302811,histories=3000000,hits=439297,allW=38.7782391122,winW=12.1550792104
TRANSPORT,templateshi,560,30,seed=302831,histories=3000000,hits=440065,allW=36.121474816,winW=11.3234138096
TRANSPORT,templateshi,580,30,seed=302851,histories=3000000,hits=439522,allW=33.6323402898,winW=10.5350486288
TRANSPORT,templateshi,600,30,seed=302871,histories=3000000,hits=439000,allW=31.3905829292,winW=9.82471876047
TRANSPORT,templateshi,620,30,seed=302891,histories=3000000,hits=439965,allW=29.4630770747,winW=9.21598626459
TRANSPORT,templateshi,640,30,seed=302911,histories=3000000,hits=440591,allW=27.6900379598,winW=8.63147132435
TRANSPORT,templateshi,660,30,seed=302931,histories=3000000,hits=439981,allW=26.00149448,winW=8.11770878637
TRANSPORT,templateshi,680,30,seed=302951,histories=3000000,hits=438257,allW=24.3987763644,winW=7.63688636842
TRANSPORT,templateshi,700,30,seed=302971,histories=3000000,hits=437678,allW=22.9942295249,winW=7.19179917022
TRANSPORT,templateshi,720,30,seed=302991,histories=3000000,hits=437750,allW=21.7382570331,winW=6.79512492857
TRANSPORT,templateshi,740,30,seed=303011,histories=3000000,hits=437195,allW=20.5532104183,winW=6.43719585699
TRANSPORT,templateshi,760,30,seed=303031,histories=3000000,hits=437504,allW=19.4996173911,winW=6.08891268774
TRANSPORT,templateshi,780,30,seed=303051,histories=3000000,hits=435642,allW=18.4337864229,winW=5.78780789839
TRANSPORT,templateshi,800,30,seed=303071,histories=3000000,hits=434917,allW=17.4945967568,winW=5.48586015832
TRANSPORT,templateshi,820,30,seed=303091,histories=3000000,hits=435252,allW=16.6645109398,winW=5.22961658967
TRANSPORT,templateshi,840,30,seed=303111,histories=3000000,hits=434966,allW=15.8700782419,winW=4.98438256442
TRANSPORT,templateshi,860,30,seed=303131,histories=3000000,hits=433940,allW=15.1048897315,winW=4.73587772677
TRANSPORT,templateshi,880,30,seed=303151,histories=3000000,hits=434129,allW=14.4324742564,winW=4.51434298727
TRANSPORT,templateshi,900,30,seed=303171,histories=3000000,hits=434186,allW=13.8000313954,winW=4.32920787849
TRANSPORT,templateshi,920,30,seed=303191,histories=3000000,hits=434812,allW=13.2256701926,winW=4.15073453722
TRANSPORT,templateshi,940,30,seed=303211,histories=3000000,hits=433256,allW=12.623575493,winW=3.96869138194
~~~

No approval requests: no action requiring author approval was needed or executed.


## Attenuation-anchor cross-check

The deterministic control uses μ=.178/mm directly (the nominal 662-keV anchor); it initially omitted the engine's interpolation from 662 to 661.7 keV. The public CodedApertureMask.TungstenMuRel(661.7) returns **1.00047700200012**. An additional q24 run multiplies the geometric μ by this exact factor at all nine standard-pitch points. **All nine original-score maximum planes are unchanged**, verified against geo.csv. Thus the large geometric offsets are also present with the exact engine interpolation at the Cs line. anchorcheck.csv retains the corrected probabilities and scores; no stability claim is made for every near-tied alternative metric. Like the other geometric controls this has no random seed and 518,400 quadrature directions per image.

~~~csv
ANCHORFACTOR,1.0004770020001155
METRIC,geometry_mu661.7,300,0,0.6,0.000163381583237,360,360,360,310,310,310,9.2749127,9.2749127,9.2749127,12.366284,9.011475E-05,9.011475E-05
METRIC,geometry_mu661.7,300,15,0.6,0.000162077477532,350,350,350,310,310,310,9.1301538,9.1301538,9.1301538,11.624489,8.8445451E-05,8.8445451E-05
METRIC,geometry_mu661.7,300,30,0.6,0.000157686657551,350,350,350,310,310,310,8.7815198,8.7815198,8.7815198,11.373158,8.0314806E-05,8.0314806E-05
METRIC,geometry_mu661.7,500,0,0.6,5.912783854E-05,580,580,580,580,490,490,9.3127546,9.3127546,9.3127546,12.000857,3.1810294E-05,3.1810294E-05
METRIC,geometry_mu661.7,500,15,0.6,5.85178361872E-05,560,560,560,560,460,460,8.8210488,8.8210488,8.8210488,11.257129,2.9968809E-05,2.9968809E-05
METRIC,geometry_mu661.7,500,30,0.6,5.49030039148E-05,430,430,430,450,510,510,8.5049571,8.5049571,8.5049571,10.876876,2.9019338E-05,2.9019338E-05
METRIC,geometry_mu661.7,700,0,0.6,3.01987331022E-05,160,160,160,680,690,690,8.5283924,8.5283924,8.5283924,10.807733,1.388721E-05,1.388721E-05
METRIC,geometry_mu661.7,700,15,0.6,2.97764769236E-05,160,160,160,580,560,560,8.6882079,8.6882079,8.6882079,10.491087,1.3542358E-05,1.3542358E-05
METRIC,geometry_mu661.7,700,30,0.6,2.75198594892E-05,680,680,680,680,690,690,8.1761936,8.1761936,8.1761936,10.343498,1.2504707E-05,1.2504707E-05
~~~
