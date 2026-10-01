# PLAN.Studio.Waveform.Review — measured time base, chain consistency and waveform limits (TODO-10)

Scope: review of [PLAN.Studio.Waveform](PLAN.Studio.Waveform.md), especially W-2 / W-3 / W-4 / W-6; current-code inspection, headless MC measurements and current C# versus actual SystemVerilog comparisons. No implementation, desktop testing, Studio launch, index or HEAD operations. Only this review document is written in the repository.

Status: reviewed 2026-10-02; decisions below are proposals for the planner, not an implementation specification.

## Findings and disposition by row

| Row | Disposition | Proposed change and reason |
|---|---|---|
| W-1 | **Confirmed, extended** | One immutable chain setting, captured at Start and carried by snapshots. Include measurement, spectrum bands/resolving time, imaging windows, H-only strip calibration and waveform. Do not read the shell's current editable chain when processing an old acquisition. |
| W-2 | **Confirmed as policy; corrected argument** | Mark chain edits stale consistently with I-2, disable physical edits while acquiring, and preserve the acquired chain on existing displays. Reprocessing true deposits is technically possible for electronics; “a real detector cannot change recorded data” is a provenance/product policy, not a mathematical impossibility. A scintillator change also changes transport, so cannot in general be simulated by smearing the retained GAGG deposits. |
| W-3 | **Confirmed, extended** | Real-time triggered scope is the default; use a short pulse-scale window, initially about 10 µs, with pretrigger and event markers. Offer an explicit acquisition-event selector and channel choice. A long window shows rare arrivals, not ordinarily visible pulse detail or pile-up. Latest / next / index must use stable acquisition indices and respect the acquired prefix. |
| W-4 | **Confirmed, clarified** | Keep a separate “rate study: arrivals re-spaced at … kcps” mode using retained MC deposits. State the simulated channel and rate, and keep it local to Waveform. It must not change acquisition counts/live time, Spectrum, Imaging, stale state or actual arrival times. 50 kcps gives occasional overlap; 1 Mcps gives obvious overlap under this pulse model. |
| W-5 | **Corrected** | Legacy ideal mode still has ADC ENOB noise: noiseKev=0 does not set AdcNoiseCodes=0. Specify whether “ideal” removes ADC noise as well; recommend zero intrinsic smear, zero analog AND ADC noise, instantaneous rise, selected tail/filter retained, explicit ideal-mode label. This is a shaper stimulus, not the acquired measurement. |
| W-6 | **Partly confirmed; flat-top-for-all dropped** | Source N_pe and FWHM from FrontEndModel; time constants from PulseSamples; resolving interval from EventStreamStudy. Label FWHM as single-channel model resolution, excluding pixel gain spread. Trapezoid alone has a flat-top readout; CR-RC needs calibrated peak amplitude and a low-signal/quantization caveat. Show actual filter settings: all Crrc presets currently select order 4 and the SAME K_Q16, regardless of their names or IntegrationNs. |
| W-7 | **Confirmed, extended** | 10 M samples = 80 ms at 125 MSPS is an allocation cap, not a continuous 4 Hz processing promise. Validate/count samples before Rasterize and reserve warm-up/padding work explicitly. Do not rasterize a huge absolute timestamp then clip in PlotView. |
| New N-1 | **Required decision** | Physical pixel channel versus explicitly synthetic array-sum channel. Current Spectrum merges all pixels as one channel; a real pixel scope should not inherit that pooling assumption silently. See measured differences below. |
| New N-2 | **Required decision** | How scintillator selection maps to transport material. Existing chain presets contain CsI, but CrystalMaterial has no CsI entry; unknown names silently fall back to GAGG. Unsupported transport must be visible or unavailable, not silently renamed as a new material. |
| New N-3 | **Required correction** | Share the event response without double smearing. Existing Rasterize's scaled FWHM plus sample noise is not the Studio FrontEndModel response. Define the waveform as a simulated ADC signal with explicit noise limits; do not claim its extracted energies equal the analytic MCA unless that agreement is measured. |
| New N-4 | **Required correction** | Window initialization, history and tail handling: pre-window pulses/filter state, long pulse support, partial acquisition windows, empty windows and overflow-safe sample arithmetic. |

## Code-established wiring and consistency

Read the planning procedure, root AGENTS / CLAUDE, Waveform / Migration / Spectrum / ImagingOptions plans (I-2), Studio guidance, PlotView and plotting helpers, legacy RenderWaveform / UpdateFrontEnd, FrontEndParts / FrontEndChain, FrontEndModel / Waveform / EventStreamStudy, ListModeSource, all Studio service sources, Main / Spectrum / Imaging / source view models, RTL README and all three cocotb test modules.

The default UI is one centered Cs-137 source, **500 µCi at 1000 mm from the detector** (`MainViewModel.AddSource`, `SourceItemViewModel`), Sharp optics **rank 13, cell 0.7 mm, D=80 mm, 30×30 pixels at 0.6 mm**, focus 1000 mm. Studio detector inputs are entrance absorber 0.15 mm, backing 2 mm, gap 0.1 mm, gain σ=3%, gain seed 1. Background is zero. The source includes 661.7 / 32.1 / 36.4 keV lines with intensities 0.851 / 0.056 / 0.014. These are not the CLI scenario.json defaults.

`ListModeSource` transports photons and accepts deposits using importance-weight rejection, then advances time at the running weighted detection rate. Thus its early rate estimate evolves; its arrivals are the existing Studio model, not a new stationary-rate guarantee. `AcquisitionSession` publishes cumulative true deposits and real arrival times, with frozen Detector / Optics, but no chain. It leaves one look-ahead event unacquired. Scope generation must not include that event or invent an observed future tail beyond live time without a label.

`MeasurementStage` applies pixel gain then an index-seeded FrontEndModel smear. Spectrum and Imaging use this same deterministic mapping for isolated events; Spectrum optionally sums amplitudes first and smears the pooled group once. Imaging currently has no corresponding pile-up filter. Identical shared chain does not make their pile-up paths identical.

Hard-coded chain locations include MeasurementStage's constructor, SpectrumService's model / resolving constructor / returned label, ImagingService's model, and SpectrumWorkspaceViewModel's label. Also change all ImagingService calibration MeasurementStage calls. Cache keys must include acquired chain (or a guaranteed new acquisition ID); Spectrum must reset measured bins/group state, Imaging must reset measured energies/windows/ratios when their physical identity changes. A single atomic selector change should cause one stale transition. Readouts should describe the **acquired** settings beside a stale result, with the pending selection clearly separate.

Transport currently calls `CrystalMaterial.ForConfig(config.Detector.Material)`. Studio BuildConfig does not set Material from a selected scintillator; the default string `ideal` resolves to GAGG on the Compton path. Merely wiring FrontEndParts.BuildConfig changes light/noise response, not cross sections. For a selector presented as physical scintillator, map supported materials and recompute transport; do not reuse deposits across a material change. CsI requires a separately validated transport model or an explicit response-only study limitation. The existing chain has no GAGG afterglow term; the RTL material-rate-study headline must not be applied to these bi-exponential traces.

Core may hold chain selection, window math and service result records, but cannot call Detector.FrontEndModel or Simulation.EventStreamStudy under DESIGN.Architecture. Compute engine-derived readouts in Services and return values; avoid duplicating physics formulas in a view model merely to satisfy the plan's “readout maths — Core” phrase.

## W-3: measured time base and pulse counts

Headless Release build of **current source included directly in a scratch project outside the repository**; .NET SDK 9.0.311, Windows, fs=125 MHz. Two centered scenes: default above; **hot near = 5000 µCi at 200 mm**, same physical optics/detector (decoder focus remains 1000 mm, irrelevant to arrivals). This hot scene is a chosen comparison, not a product operating limit. For each scene and seeds 1/2/3, collect 200,000 accepted ListModeSource events. Chain does not affect transport in the current implementation, so count windows are identical for all preamps at fixed GAGG/sensor.

| Scene | MC weighted rate, cps (seeds 1 / 2 / 3) | Count / arrival-span rate, cps | First-to-40th event span, ms | Samples for that span |
|---|---|---|---|---|
| Default | 72.789 / 72.788 / 72.704 | 72.699 / 72.945 / 72.923 | 559.575 / 699.506 / 648.666 | 69.947 M / 87.438 M / 81.083 M |
| Hot near | 17208.259 / 17230.932 / 17212.212 | 17204.781 / 17309.920 / 17238.192 | 1.983 / 1.934 / 2.754 | 0.248 M / 0.242 M / 0.344 M |

The plan's “~100 cps, ~50 M samples for 40 pulses” has the correct order of magnitude, but the actual default is about **72.8 cps**, mean gap **13.7 ms / 1.72 M samples**; expected first-to-40th span at this rate is 39/rate ≈ **0.536 s / 67.0 M samples**. Forty gaps would be a different definition. An 80 ms untriggered window has about 5.82 expected events; a triggered window includes its selected event in addition.

For a forward window [trigger, trigger+length), use each of the first 190,000 events as a trigger; the last 10,000 events supply look-ahead solely for this completed-stream measurement. These overlapping windows are descriptive, not independent trials. Numbers below are seed 1; seed 2/3 default means are within 1.00066–1.00083 (10 µs), 1.00720–1.00761 (100 µs), 1.07185–1.07364 (1 ms), 6.811–6.842 (80 ms).

| Window | Samples | Default pooled: mean / >1 fraction | Hot pooled: mean / >1 fraction | Default own-pixel mean | Hot own-pixel mean |
|---|---:|---:|---:|---:|---:|
| 10 µs | 1,250 | 1.000726 / 0.0726% | 1.171047 / 15.7342% | 1.000000 | 1.000297 |
| 100 µs | 12,500 | 1.007511 / 0.7463% | 2.715632 / 82.0732% | 1.000011 | 1.002670 |
| 1 ms | 125,000 | 1.072700 / 6.9837% | 18.195179 / 100% | 1.000088 | 1.027566 |
| 80 ms | 10,000,000 | 6.811358 / 99.6979% | 1376.522579 / 100% | 1.008434 | 3.221220 |

Own-pixel windows use each event as a trigger but count only that pixel, dropping the last 20 triggers per channel (182,000 usable triggers, seed 1). Their >1 fractions at 80 ms are **0.8407% default / 84.4819% hot**. They describe a randomly selected event's channel, not one predetermined pixel. Counts alone do not imply pulse overlap: most hot pulses in a 100 µs window remain well separated compared with ns pulse widths.

Recommendation: start at 10 µs and a selected acquired event, show trigger index/pixel/deposit/absolute acquisition time, and allow wider time views. Default to the selected event's pixel for a physical channel; offer explicitly synthetic array sum for compatibility/exploration. A “next close pair” search may make rare overlap discoverable, but must identify its selection condition and channel; it is a conditioned view, not a typical-rate illustration.

## W-4: pile-up measurements and the value of rate study

Fixed GAGG(Ce), S13360-3050. Effective resolving interval is the existing heuristic rise+2×tail, not a measured discriminator's ability to recover two pulse heights.

| Preamp | Rise / tail, ns | Effective resolving ns | Pooled close gaps, default (600k events) | Pooled close gaps, hot (600k events) | Own-pixel close gaps, hot |
|---|---:|---:|---:|---:|---:|
| Fast | 90 / 150 | 390 | 16 / 0.00267% | 4120 / 0.68667% | 8 / 0.00133% |
| Original | 90 / 320 | 730 | 28 / 0.00467% | 7640 / 1.27334% | 12 / 0.00200% |
| Slow | 90 / 800 | 1690 | 67 / 0.01117% | 17372 / 2.89535% | 24 / 0.00400% |
| Trapezoid | 90 / 320 | 730 | 28 / 0.00467% | 7640 / 1.27334% | 12 / 0.00200% |

Denominator = three streams ×199,999 gaps. Own-pixel calculation compares to the previous event in that same pixel, not merely the previous pooled event. **Zero own-pixel close pairs** occurred in 600k default events; this is an observation, not proof of zero probability. Rare counts have substantial sampling uncertainty. Poisson predictions 100×(1−exp(−rate×interval)) at seed-1 weighted rates are default **0.00284 / 0.00531 / 0.01230%**, hot **0.66888 / 1.24835 / 2.86631%**, respectively. They support the scale without substituting for MC.

An additional headless pulse-visibility diagnostic used MC deposits/times (seed 1). For each adjacent pair, compute the preceding bi-exponential pulse at the second pulse's own ideal peak time. Count contributions ≥10% of the second isolated ADC peak. This threshold is an explicitly chosen distortion diagnostic, not a validated peak-picker or human visibility criterion; noise, gain scatter, clipping, earlier pulses and integer effects are omitted from this fraction. The formula is the same pulse shape used by Rasterize. More complete overlapping trains can differ.

| Preamp | Default distorted-pair fraction | Hot distorted-pair fraction | Re-spaced 50 kcps | Re-spaced 1 Mcps |
|---|---:|---:|---:|---:|
| Fast | 0.00300% | 0.80750% | 2.31251% | 35.33868% |
| Original | 0.00600% | 1.46401% | 4.21402% | 52.13276% |
| Slow | 0.01400% | 3.32002% | 9.32555% | 74.09137% |
| Trapezoid | 0.00600% | 1.46401% | 4.21402% | 52.13276% |

Rate-study rows re-space the default scene's 200k retained deposits with exponential gaps, RNG 555, in one synthetic counting channel. Their close-gap fractions are respectively **1.93151 / 3.62752 / 8.11754%** at 50 kcps, **32.40866 / 51.82026 / 81.59491%** at 1 Mcps. Do not divide array counts by 900 and call these the same study.

To confirm actual quantized-waveform overlap, Rasterize two actual >400-keV MC deposits at their rounded real sample separation, intrinsicFwhm=0 / noiseKev=0, pad=2048 (ADC ENOB noise remains). A rare default pair at indices 134347/134348, different pixels, E=661.700 / 455.685 keV, gap=19 samples, increased ADC at the second isolated peak from **348→658 Fast, 818→1755 Original/Trap, 1256→2908 Slow**. Hot indices 105008/105009, also different pixels, E=661.700 / 468.475, quantize to the same sample; **359→863, 841→2025, 1293→3116**. These illustrate visible pooled overlap but are **not** real same-pixel pile-up. They also show why sample-rounded coincidence must not be described as a decay cascade. Current ListModeSource emits singles; TODO-14 remains separate.

Thus rate study is useful, especially for a physical pixel scope that is otherwise usually isolated. Name it as simulated detected rate **per displayed channel**, keep the acquired energies/time identity available, freeze its seed, and re-space a bounded retained selection in original order rather than resampling a new MC pool on every repaint. Never use acquisition speed to compress physical time.

## W-6: readout sources and corrections

| Quantity | Source / result | Limit |
|---|---|---|
| N_pe(662) | FrontEndModel.Photoelectrons(662) = **6620** for default scintillator/sensor, all preamps | Light yield×collection×PDE×E, not ADC peak codes. |
| FWHM(662) | FrontEndModel.FwhmFraction; Fast / Original / Slow = **4.569202 / 4.569210 / 4.569231%** | Single-channel analytic response, excludes 3% fixed pixel gain spread and pulse-height extraction errors. |
| FWHM(32.1) | **13.791449 / 13.792453 / 13.795467%** | Rasterize's FWHM(662)×sqrt(662/E) would instead be about **20.75%**; intrinsic floor / DCR have different energy laws. |
| Pulse constants | FrontEndChain.PulseSamples; table above | “Rise” is an exponential time constant, not 10–90% rise time. Guards affect effective samples; report samples×8 ns. |
| Filter | CrrcInt(order=4,K=26214,A matched to tail); TrapShape(rise=10,flat=8,M matched to tail) | K corresponds to 2.5 samples =20 ns low-pass constant; trap ramp/flat are 80/64 ns. IntegrationNs only affects FrontEndModel DCR integration, not these filter settings. |
| Effective resolution time | EventStreamStudy.ResolvingSamples /125e6 | Effective grouping model; not derived from actual shaped peak separation or RTL trigger thresholds. |
| Trapezoid pulse energy | FlatTop / CalibrateFlatPerKev with matched tau/tauRise/M and local baseline | In a single 662-keV example recovered **662.569 keV**, at 32.1 recovered **32.637 keV**; ideal-mode calibration must use tauRise=0. Saturation, close neighbors and partial windows invalidate a simple readout. |
| CR-RC pulse energy | No existing CR-RC calibration helper | Use calibrated local peak amplitude and label its quantization/overlap limit. Never divide a CR-RC FlatTop by trapezoidal gain. |

For the three CR-RC chains an isolated 32.1-keV rasterized pulse produced integer shaped maxima **0 / 0 / 1 codes**; isolated 662 maxima **47 / 90 / 114 codes**. These use the ADC ENOB noise still present with noiseKev=0; no amplitude smear, tailPad=2048. Their quantization loss exists on the path the plan proposes to reuse, so “bit-exact” must not be translated into accurate low-energy recovery. A naïve FlatTop/trap-calibration on the CR-RC 662 traces yielded **0.223 / 0.109 / 0.045 keV**, proving that readout is inapplicable. Current parameters deserve independent calibrated-peak evidence before any precision claim; improvements to filter precision would require a separately agreed engine/RTL scope.

`Waveform.Rasterize` adds scaled intrinsic amplitude smearing plus analog RMS noise (default 3 keV) and ADC ENOB noise. Feeding already-measured Studio energies and a nonzero intrinsicFwhm smears twice. Feeding gain-only energies with chain FWHM at 662 repeats the legacy approximation and disagrees with FrontEndModel at other energies. Recommended minimal migration: measure event amplitude once with index-addressed chain response, rasterize with intrinsicFwhm=0, explicitly report that the ADC simulation adds electronic noise and its shaped energy is not the analytic MCA measurement. A truly unified ADC-derived spectrum/imaging would require calibrated peak extraction and channel attribution and is a separate project, not an unmeasured TODO-10 shortcut. An alternative is a gain-only ADC stimulus with physical chain noise modeled explicitly; planner should select the contract before implementation.

## Processing cost and bounds

One warm-up and three measured runs per size / preamp, median component times below, Release, default ADC, GAGG pulse constants. Synthetic 661.7-keV Poisson train at 50 kcps, RNG 555, fixed Rasterize seed, amplitude FWHM input 0.04572, default 3-keV analog noise. A zero-energy final length sentinel forces exactly N samples; it is not a physical pulse. Actual counts =1/5/53/387/4095 for the five sizes. Conversion to two double arrays and construction of two MinMaxPyramids is included separately. Query asks both pyramids for 1000 columns. This measures service work/plot data preparation, **not WPF redraw or composition**. CPU model query was denied; do not assume these are universal timings.

Each cell = **raster / shape / double conversion + two pyramids**, ms. All three raw timings are retained in the scratch output/harness below.

| N / duration | Fast | Original | Slow | Trapezoid | Allocations, decimal MB |
|---|---|---|---|---|---:|
| 1,250 /10 µs | .057/.011/.069 | .048/.009/.046 | .046/.008/.042 | .033/.025/.034 | .096 |
| 12,500 /100 µs | .322/.040/.152 | .473/.042/.136 | .336/.035/.128 | .311/.167/.120 | .513 |
| 125,000 /1 ms | 4.752/.623/1.333 | 3.065/.324/1.103 | 3.447/.341/.949 | 3.091/1.520/.966 | 4.676 |
| 1 M /8 ms | 25.645/3.077/9.999 | 26.770/3.045/9.771 | 28.512/3.132/10.886 | 27.409/12.782/10.631 | 37.051 |
| 10 M /80 ms | 268.956/28.897/95.632 | 282.461/29.088/96.336 | 308.915/28.361/95.731 | 259.152/142.519/100.321 | 370.053 |

Median two-pyramid query at 10 M = **0.733 /0.701 /0.690 /0.920 ms**, consistent with cheap cached display queries. It does not include generating new data, which exceeds 250 ms at the cap. Allocations measured with GC.GetAllocatedBytesForCurrentThread cover these synchronous calls, not process peak working set. At N=10 M, principal allocated arrays are 80 MB raster scratch, 40 MB ADC int, 80 MB shaped long, 160 MB plot doubles, ~10 MB pyramid storage. Old/new snapshots and retained buffers can increase live memory further. PlotView.Prepare currently builds pyramids synchronously when Series changes; putting only raster/shaper on a worker still leaves this O(N) UI operation.

Generate only while visible and when selected acquisition event/window/response changes, not on every 4 Hz snapshot if the selection is held. Coalesce requests, cancel obsolete work (current Rasterize/shapers have no cancellation checks), reject late responses with acquisition ID/revision, and cache viewport-only navigation. Small windows are cheap. Keep 10 M as an explicit long-window option and revise preparation handoff if needed before promising smooth live replacement at that size. A ring buffer for TrapShape's 28-tap shift could reduce cost later, but no optimization is required to display a short pulse window.

Rasterize currently derives length from `(int)maxArrival + tailPad`; it does not enforce 10 M or handle long timestamps safely. Subtract the window origin in seconds before sample conversion, use checked/clamped bounded long arithmetic, reject invalid times/rates/lengths, include an empty-window noise path, and preserve requested window length independently of the last event. An empty real window is legitimate; it is not “no acquisition.”

Default tailPad=128 truncates pulses: for a 662-keV nominal ADC amplitude the rasterizer's own support formula gives **166/348/864 samples** for GAGG Fast/Original/Slow, **1079** for CsI chains. Warm-up must include preceding pulses within their amplitude-dependent support, then enough shaper history; a fresh filter reset at an arbitrary crop changes baseline. The trapezoid's long-train integer baseline also walks: the 50-kcps 10-M sample run reached **−4,898,637**, with peak 910,744. Blr exists but the legacy display does not call it. Decide whether displaying raw output plus local baseline or an explicitly labelled BLR is intended; do not add BLR and silently change the flat calibration convention. Finite warm-up approximates an acquisition-wide filter state; characterize that error rather than promising exact window equivalence without carrying state.

## RTL-bit-exact status: confirmed with conditions

No Studio waveform service exists yet. Reusing Waveform.CrrcInt / TrapShape, with the legacy matched pole-zero parameters, uses the RTL integer algorithms; reimplementing them as floating filters would not. For GAGG the actual coefficients are:

| Preamp | A_Q16 | M_Q8 | Execution checked |
|---|---:|---:|---|
| Fast | 62132 | 4673 | CrrcInt vs crrc_shaper.sv |
| Original | 63918 | 10113 | CrrcInt vs crrc_shaper.sv |
| Slow | 64884 | 25472 | CrrcInt vs crrc_shaper.sv |
| Trapezoid | 63918 | 10113 | TrapShape vs trapezoidal_shaper.sv |

Generated four 4096-sample ADC vectors in C# (661.7 at100, 1332.5 at200, 32.1 at2000, length sentinel at3968; selected chain pulse, realistic raster noise). Drove the **actual unchanged RTL files** with scratch Icarus testbenches and compared every output sample with C#: **0 mismatches for each chain (16,384 total)**. CR-RC used default WACC=40, trapezoid was checked both WACC=40 and original **WACC=32**, both zero mismatches on this stimulus. `valid=1` throughout, three reset clocks, comparison after NBA settles. This confirms selected coefficients as well as recurrence, beyond existing default-coefficient golden tests. It does not establish all possible ADC sequences/overflow/valid gaps or an analog hardware response.

C# states are signed long while RTL accumulators have finite width; bit-exact claims require no overflow or matching wrapping. The current cocotb defaults use tau=5 (M=1156, A=53656), not the selected GAGG chains. CR-RC cocotb pins default constants; a parameter sweep must deliberately update the expected-math contract rather than remove the check. The trap tests support parameters but the provided runner builds defaults. README's “Next” section is stale: test_trap_shaper already has an MC-stream case. Runner invokes three trap tests for each of two tops, one BLR and three CR-RC tests; do not blindly repeat the historical “7 tests” count. No cocotb suite rerun was performed this turn; the direct Icarus comparisons above are separate evidence.

## Proposed acceptance and evidence changes

1. Agree the three material decisions before implementation: channel identity, physical material mapping (including unsupported CsI), and waveform amplitude/noise contract versus analytic MCA. Keep these distinct from view-only ideal/rate-study controls.
2. Shared-chain tests: all supported chains reach measured response, spectrum bands/resolving interval/labels and Imaging H-only calibration; frozen acquired settings survive later edits; mixed-field ratios/windows reset on chain identity. Assert Spectrum/Imaging equality of the isolated-event response, not equality of their currently different pile-up outputs.
3. Window tests: known timestamps on boundaries, checked sample conversion for late acquisition times, zero events/noise-only windows, triggered prehistory, tail clipping, insufficient acquired future, cap including work buffers, pixel filtering and explicit pooled mode. A pile-up preview must be local and preserve events/time/counts exactly.
4. Meaningful engine integration: retained **MC** deposits with specified scenes/seeds above; real default trace usually isolated; explicitly re-spaced rate study has measured overlap, no invented energy lines. Assert guaranteed association/time preservation; record stochastic counts with derived uncertainty, not copied tolerances.
5. Calibrated isolated-pulse evidence per supported chain and energies, with noise modes and quantization documented. Trapezoid uses matched calibration; CR-RC uses its own peak calibration and reports low-signal limits. Include saturation / overlaps / partial windows and determine when to suppress a recovered-energy value. Do not add a tolerance that forces 32-keV CR-RC “accuracy” to pass.
6. Extend deterministic parameterized C#/RTL or Python-reference regression for selected chain coefficients and signed arithmetic. Use no-overflow stimuli; separate longer-width stress/overflow tests if the product claims broad bit-exactness. Keep cocotb changes outside TODO-10 unless planner explicitly changes the exclusion.
7. Service/request tests: held trigger does not regenerate on unchanged live snapshots, newest selection wins, Stop/new acquisition invalidates old work, errors are visible, max-window allocation/time behavior documented. Headless view snapshots at both sizes/themes verify shared X, event markers, units, ideal/rate-study labels and acquired-versus-pending chain; desktop checks remain last.
8. After implementation agreement, update SRS/SDS/VV/layout/architecture plus actual evidence inventory. Do not describe real default pile-up as a normal visual effect or import GAGG-afterglow rate claims into this model.

## Reproduction and limitations

Scratch root used locally: `$env:TEMP/gcam-todo10-review`. Its csproj directly includes repository source while excluding bin/obj; all compile/build outputs stay in scratch. Run MainProgram, FollowupProgram and ChannelProgram below sequentially as Program.cs in that directory; outputs are results.txt, followup.txt, channels.txt. Each program compiles the same current source. Preserve culture/encoding if comparing output hashes. MainProgram's 600k events/scene and timings are the primary measurements; follow-up adds visibility and selected-coefficient vectors; channel program adds channel windows and explicit ADC overlap.

```powershell
$waveRepo = (Get-Location).Path
$waveScratch = Join-Path $env:TEMP 'gcam-todo10-review'
New-Item -ItemType Directory -Force -Path $waveScratch | Out-Null
$waveIncludes = @('Gcam.Core','Gcam.Configuration','Gcam.Masks','Gcam.Detector',
    'Gcam.Decoding','Gcam.Simulation','Gcam.Studio.Services') | ForEach-Object {
    '<Compile Include="' + $waveRepo + '\src\' + $_ + '\**\*.cs" Exclude="' +
    $waveRepo + '\src\' + $_ + '\obj\**;' + $waveRepo + '\src\' + $_ + '\bin\**" />'
}
$waveIncludes += @('Services','Optics','Plotting','Imaging') | ForEach-Object {
    '<Compile Include="' + $waveRepo + '\src\Gcam.Studio.Core\' + $_ + '\*.cs" />'
}
Set-Content (Join-Path $waveScratch 'Review.csproj') (
    '<Project Sdk="Microsoft.NET.Sdk"><PropertyGroup><OutputType>Exe</OutputType>' +
    '<TargetFramework>net9.0</TargetFramework><ImplicitUsings>enable</ImplicitUsings>' +
    '<Nullable>enable</Nullable></PropertyGroup><ItemGroup>' + ($waveIncludes -join '') +
    '</ItemGroup></Project>')
# Paste each appendix program into scratch Program.cs, then execute with scratch cwd:
Set-Location $waveScratch
# dotnet run --project Review.csproj -c Release > results.txt
# dotnet run --project Review.csproj -c Release > followup.txt
# dotnet run --project Review.csproj -c Release > channels.txt
```

Primary output SHA256: `E14DCFA05FF01E49BA4BF407B7D03DFCFCCCE00675C1939B3E7D2B80B24B571E`.
Follow-up: `15AB437D506ACED8A9120DE951F045D3D73C92C37D61BB70D5AFC834504B06DC`.
Channel check: `9F5059DC32F9F55CDC2C114A3917246AC183628F03490AED8B1650959F25D0D8`.

One read command using the default login shell was rejected with `CreateProcessAsUserW failed: 5`; repeating without login succeeded. The first scratch build failed because NiceTicks required Core.Imaging; adding that source include **in scratch only** fixed it. `Get-CimInstance Win32_Processor` was denied (“client cannot access CIM resource”); CPU hardware identification could not run. Scratch dotnet and Icarus commands themselves succeeded.

Not run: desktop/UI tests, Studio launch, WPF render/redraw gate, full solution build/test, cocotb suite, all scintillator/sensor transport-rate combinations, hardware validation, a channel topology/peak-picker model, analog afterglow, calibrated CR-RC response/noise sweep or a full overflow proof. This review's rates are current GAGG/default-sensor MC conditions; electronics-only preamp changes leave those arrivals unchanged. Other materials need new transport measurements after the mapping decision. No inherited physics claim is promoted to a universal guarantee.

## Appendix A — primary MC and cost program

```csharp
using System.Diagnostics;
using Gcam.Configuration;
using Gcam.Core;
using Gcam.Detector;
using Gcam.Simulation;
using Gcam.Studio.Services;
using Gcam.Studio.Core.Services;
using Gcam.Studio.Core.Plotting;
const double fs=125e6;
foreach(var pa in FrontEndParts.Preamps) {
 var ch=new FrontEndChain(FrontEndParts.Scintillators[0],FrontEndParts.Sensors[0],pa); var p=ch.PulseSamples;var m=new FrontEndModel(ch.BuildConfig());
 Console.WriteLine($"CHAIN {pa.Name} rise_ns={p.RiseSamples*8} tail_ns={p.TailSamples*8} resolve_ns={EventStreamStudy.ResolvingSamples(p.RiseSamples,p.TailSamples)*8} npe={m.Photoelectrons(662)} fwhm662_pct={100*m.FwhmFraction(662):F6} fwhm32_pct={100*m.FwhmFraction(32.1):F6} A={Math.Round(Math.Exp(-1/p.TailSamples)*65536)} M={Math.Round(256/(Math.Exp(1/p.TailSamples)-1))}");
}
foreach(var sceneName in new[]{"default","hot"}) for(int seed=1;seed<=3;seed++) {
 var scene=new[]{new SceneSource{DistanceMm=sceneName=="default"?1000:200,ActivityUCi=sceneName=="default"?500:5000}};
 var cfg=SimulationService.BuildConfig(scene,new OpticsSettings(),new DetectorSettings());cfg.Seed=seed;
 var evs=new List<DetectedEvent>();using var source=new ListModeSource(cfg);var sw=Stopwatch.StartNew();
 while(evs.Count<200_000)if(source.Advance() is {} ev)evs.Add(ev);
 Console.WriteLine($"SCENE {sceneName} seed={seed} events={evs.Count} histories={source.HistoriesEmitted} rate_mc={source.RateCps:F6} rate_time={evs.Count/evs[^1].ArrivalTimeS:F6} span_s={evs[^1].ArrivalTimeS:F3} ms={sw.Elapsed.TotalMilliseconds:F1}");
 var gaps=evs.Zip(evs.Skip(1),(a,b)=>b.ArrivalTimeS-a.ArrivalTimeS).ToArray();
 foreach(double win in new[]{10e-6,100e-6,1e-3,.08}) {
  int end=0;long sum=0;int more=0;int triggers=evs.Count-10000;
  for(int i=0;i<triggers;i++){end=Math.Max(end,i+1);while(end<evs.Count && evs[end].ArrivalTimeS<evs[i].ArrivalTimeS+win)end++;int n=end-i;sum+=n;if(n>1)more++;}
  Console.WriteLine($"WINDOW {sceneName} seed={seed} us={win*1e6} triggered_mean={(double)sum/triggers:F6} multi_pct={100.0*more/triggers:F6}");
 }
 foreach(var pa in FrontEndParts.Preamps){var p=FrontEndParts.PulseSamples(FrontEndParts.Scintillators[0],pa);double resolve=EventStreamStudy.ResolvingSamples(p.RiseSamples,p.TailSamples)/fs;int close=gaps.Count(g=>g<resolve);int same=Enumerable.Range(1,evs.Count-1).Count(i=>gaps[i-1]<resolve&&evs[i].PixelX==evs[i-1].PixelX&&evs[i].PixelY==evs[i-1].PixelY);
  int channelClose=0;var last=new Dictionary<(int,int),double>();foreach(var ev in evs){var key=(ev.PixelX,ev.PixelY);if(last.TryGetValue(key,out var t)&&ev.ArrivalTimeS-t<resolve)channelClose++;last[key]=ev.ArrivalTimeS;}
  Console.WriteLine($"PILE {sceneName} seed={seed} preamp={pa.Name} pooled_close={close} pct={100.0*close/gaps.Length:F6} adjacent_same={same} per_pixel_close={channelClose} per_pixel_pct={100.0*channelClose/gaps.Length:F6} expected_pct={100*(1-Math.Exp(-source.RateCps*resolve)):F6}");
 }
 Console.WriteLine($"SPAN40 {sceneName} seed={seed} ms={(evs[39].ArrivalTimeS-evs[0].ArrivalTimeS)*1e3:F6} samples={(evs[39].ArrivalTimeS-evs[0].ArrivalTimeS)*fs:F0}");
 if(seed==1){
 foreach(var pa in FrontEndParts.Preamps){var p=FrontEndParts.PulseSamples(FrontEndParts.Scintillators[0],pa);double resolve=EventStreamStudy.ResolvingSamples(p.RiseSamples,p.TailSamples)/fs;int ix=Array.FindIndex(gaps,g=>g<resolve);if(ix<0)continue;
 var a=evs[ix];var b=evs[ix+1];var pair=new[]{(100L,a.DepositKeV),(100L+(long)Math.Round((b.ArrivalTimeS-a.ArrivalTimeS)*fs),b.DepositKeV)};var wave=Waveform.Rasterize(pair,Waveform.DefaultAdc,tau:p.TailSamples,tauRise:p.RiseSamples,noiseKev:0,intrinsicFwhm:0,tailPad:2048);
 long[] Shape(int[] w)=>pa.Crrc?Waveform.CrrcInt(w,(int)Math.Round(Math.Exp(-1/p.TailSamples)*65536),Waveform.CrrcKQ16,4):Waveform.TrapShape(w,10,8,(int)Math.Round(256/(Math.Exp(1/p.TailSamples)-1)));
 var sh=Shape(wave);Console.WriteLine($"PAIR {sceneName} {pa.Name} index={ix} gap_ns={(b.ArrivalTimeS-a.ArrivalTimeS)*1e9:F3} pixels={a.PixelX}/{a.PixelY},{b.PixelX}/{b.PixelY} energies={a.DepositKeV:F3},{b.DepositKeV:F3} adc_max={wave.Max()} shaped_max={sh.Max()}");
 }
 }
}
// warm up JIT before timing; a zero-energy final event is a length sentinel, not a physical pulse.
foreach(var pa in FrontEndParts.Preamps) foreach(int n in new[]{1250,12500,125000,1000000,10000000}) {
 var p=FrontEndParts.PulseSamples(FrontEndParts.Scintillators[0],pa);
 var rng=new DefaultRandom(555);var events=new List<(long,double)>();double t=100;
 while(t<n-256){events.Add(((long)Math.Round(t),661.7));t+=-Math.Log(1-rng.NextDouble())*fs/50000;}
 events.Add((n-128,0));
 for(int run=-1;run<3;run++){
 GC.Collect();long alloc=GC.GetAllocatedBytesForCurrentThread();var sw=Stopwatch.StartNew();
 var w=Waveform.Rasterize(events,Waveform.DefaultAdc,tau:p.TailSamples,tauRise:p.RiseSamples,intrinsicFwhm:.04572);double raster=sw.Elapsed.TotalMilliseconds;sw.Restart();
 var sh=pa.Crrc?Waveform.CrrcInt(w,(int)Math.Round(Math.Exp(-1/p.TailSamples)*65536),Waveform.CrrcKQ16,4):Waveform.TrapShape(w,10,8,(int)Math.Round(256/(Math.Exp(1/p.TailSamples)-1)));double shape=sw.Elapsed.TotalMilliseconds;sw.Restart();
 var y1=Array.ConvertAll(w,x=>(double)x);var y2=Array.ConvertAll(sh,x=>(double)x);var py1=new MinMaxPyramid(y1);var py2=new MinMaxPyramid(y2);double plotPrep=sw.Elapsed.TotalMilliseconds;sw.Restart();py1.Query(0,n,1000);py2.Query(0,n,1000);double query=sw.Elapsed.TotalMilliseconds;
 if(run>=0)Console.WriteLine($"COST {pa.Name} n={n} run={run} pulses={events.Count-1} raster_ms={raster:F3} shape_ms={shape:F3} plotprep_ms={plotPrep:F3} query2_ms={query:F3} alloc_mb={(GC.GetAllocatedBytesForCurrentThread()-alloc)/1e6:F3} min={sh.Min()} max={sh.Max()}");
 }
}
foreach(var sc in FrontEndParts.Scintillators) foreach(var pa in FrontEndParts.Preamps){var p=FrontEndParts.PulseSamples(sc,pa);int support=(int)Math.Ceiling(p.TailSamples*Math.Log(2*662*Waveform.DefaultAdc.AdcPerKev+2))+4;Console.WriteLine($"TAIL {sc.Name}/{pa.Name} tau={p.TailSamples} support={support} pad=128");}
```

## Appendix B — visibility, calibration and RTL vectors

```csharp
using Gcam.Configuration;using Gcam.Core;using Gcam.Detector;using Gcam.Simulation;using Gcam.Studio.Services;using Gcam.Studio.Core.Services;
const double fs=125e6;
foreach(var sceneName in new[]{"default","hot","whatif50k","whatif1M"}){
 var cfg=SimulationService.BuildConfig(new[]{new SceneSource{DistanceMm=sceneName=="hot"?200:1000,ActivityUCi=sceneName=="hot"?5000:500}},new OpticsSettings(),new DetectorSettings());cfg.Seed=1;using var source=new ListModeSource(cfg);var evs=new List<DetectedEvent>();while(evs.Count<200000)if(source.Advance() is {} e)evs.Add(e);
 if(sceneName.StartsWith("whatif")){var r=new DefaultRandom(555);double rate=sceneName=="whatif50k"?50000:1e6;double t=0;for(int i=0;i<evs.Count;i++){t+=-Math.Log(1-r.NextDouble())/rate;evs[i]=evs[i] with{ArrivalTimeS=t};}}
 foreach(var pa in FrontEndParts.Preamps){var p=FrontEndParts.PulseSamples(FrontEndParts.Scintillators[0],pa);double rise=p.RiseSamples/fs,tail=p.TailSamples/fs;double peakTime=Math.Log(tail/rise)/(1/rise-1/tail);double peakFactor=Math.Exp(-peakTime/tail)-Math.Exp(-peakTime/rise);int distort=0;int close=0;
 for(int i=1;i<evs.Count;i++){double gap=evs[i].ArrivalTimeS-evs[i-1].ArrivalTimeS;if(gap<EventStreamStudy.ResolvingSamples(p.RiseSamples,p.TailSamples)/fs)close++;double dt=gap+peakTime;double residual=evs[i-1].DepositKeV*(Math.Exp(-dt/tail)-Math.Exp(-dt/rise));if(evs[i].DepositKeV>0 && residual>=.1*evs[i].DepositKeV*peakFactor)distort++;}
 Console.WriteLine($"VISIBLE {sceneName} preamp={pa.Name} close_pct={100.0*close/(evs.Count-1):F6} prior_at_peak_ge10pct={distort} pct={100.0*distort/(evs.Count-1):F6} peak_delay_ns={peakTime*1e9:F3}");
 }
}
for(int k=0;k<FrontEndParts.Preamps.Count;k++){
 var pa=FrontEndParts.Preamps[k];var p=FrontEndParts.PulseSamples(FrontEndParts.Scintillators[0],pa);int a=(int)Math.Round(Math.Exp(-1/p.TailSamples)*65536),m=(int)Math.Round(256/(Math.Exp(1/p.TailSamples)-1));
 var w=Waveform.Rasterize(new[]{(100L,661.7),(200L,1332.5),(2000L,32.1),(3968L,0.0)},Waveform.DefaultAdc,tau:p.TailSamples,tauRise:p.RiseSamples,intrinsicFwhm:.0456921);
 var sh=pa.Crrc?Waveform.CrrcInt(w,a,Waveform.CrrcKQ16,4):Waveform.TrapShape(w,10,8,m);File.WriteAllLines($"input{k}.hex",w.Select(x=>(x&65535).ToString("x4")));File.WriteAllLines($"expected{k}.txt",sh.Select(x=>x.ToString()));Console.WriteLine($"VECTOR k={k} n={w.Length} A={a} M={m} min={sh.Min()} max={sh.Max()}");
 foreach(double e in new[]{32.1,662.0}){var one=Waveform.Rasterize(new[]{(100L,e)},Waveform.DefaultAdc,tau:p.TailSamples,tauRise:p.RiseSamples,noiseKev:0,intrinsicFwhm:0,tailPad:2048);var shaped=pa.Crrc?Waveform.CrrcInt(one,a,Waveform.CrrcKQ16,4):Waveform.TrapShape(one,10,8,m);double flat=Waveform.FlatTop(shaped,100);double cal=Waveform.CalibrateFlatPerKev(Waveform.DefaultAdc,mQ8:m,tau:p.TailSamples,tauRise:p.RiseSamples);Console.WriteLine($"CAL {pa.Name} E={e} max={shaped.Max()} flat={flat} trapcal={cal:F6} recovered_using_flat={flat/cal:F6}");}
}
```

## Appendix C — channel windows and actual overlap

```csharp
using Gcam.Configuration;using Gcam.Detector;using Gcam.Simulation;using Gcam.Studio.Services;using Gcam.Studio.Core.Services;using Gcam.Core;
foreach(var name in new[]{"default","hot"}){var cfg=SimulationService.BuildConfig(new[]{new SceneSource{DistanceMm=name=="hot"?200:1000,ActivityUCi=name=="hot"?5000:500}},new OpticsSettings(),new DetectorSettings());cfg.Seed=1;using var src=new ListModeSource(cfg);var evs=new List<DetectedEvent>();while(evs.Count<200000)if(src.Advance() is {} e)evs.Add(e);
 var channels=evs.GroupBy(e=>(e.PixelX,e.PixelY)).Select(g=>g.ToArray()).ToArray();foreach(double window in new[]{1e-5,1e-4,.001,.08}){long sum=0;int more=0,total=0;foreach(var ch in channels){int end=0;for(int i=0;i<ch.Length-20;i++){end=Math.Max(end,i+1);while(end<ch.Length&&ch[end].ArrivalTimeS<ch[i].ArrivalTimeS+window)end++;sum+=end-i;if(end-i>1)more++;total++;}}Console.WriteLine($"CHANNELWINDOW {name} us={window*1e6} triggers={total} mean={(double)sum/total:F6} multi_pct={100.0*more/total:F6}");}
 foreach(var pa in FrontEndParts.Preamps){var p=FrontEndParts.PulseSamples(FrontEndParts.Scintillators[0],pa);double rt=EventStreamStudy.ResolvingSamples(p.RiseSamples,p.TailSamples)/125e6;int i=Enumerable.Range(1,evs.Count-1).Where(i=>evs[i].ArrivalTimeS-evs[i-1].ArrivalTimeS<rt&&evs[i].DepositKeV>400&&evs[i-1].DepositKeV>400).OrderBy(i=>evs[i].ArrivalTimeS-evs[i-1].ArrivalTimeS).FirstOrDefault();if(i==0){Console.WriteLine($"OVERLAP {name} {pa.Name} no >400-keV close pair");continue;}var e1=evs[i-1];var e2=evs[i];long gap=(long)Math.Round((e2.ArrivalTimeS-e1.ArrivalTimeS)*125e6);int[] Raster((long,double)[] e)=>Waveform.Rasterize(e,Waveform.DefaultAdc,tau:p.TailSamples,tauRise:p.RiseSamples,noiseKev:0,intrinsicFwhm:0,tailPad:2048);var one=Raster(new[]{(100L+gap,e2.DepositKeV)});var both=Raster(new[]{(100L,e1.DepositKeV),(100L+gap,e2.DepositKeV)});int peak=Array.IndexOf(one,one.Max());Console.WriteLine($"OVERLAP {name} {pa.Name} i={i} gap_samples={gap} E={e1.DepositKeV:F3}/{e2.DepositKeV:F3} pixel_same={e1.PixelX==e2.PixelX&&e1.PixelY==e2.PixelY} isolated_adc_at_peak={one[peak]} pair_adc_at_peak={both[peak]} distortion_pct={100.0*(both[peak]-one[peak])/one[peak]:F3}");}
}
```

## Appendix D — direct Icarus comparison

Run in the same scratch directory **after Appendix B** generates input{k}.hex and expected{k}.txt; set `$waveRepo` to the repository root. This uses default RTL accumulator widths (40-bit CR-RC, 32-bit trapezoid). The earlier exploratory 40-bit trapezoid comparison also passed; it is not necessary for reproduction. The scratch testbench drives samples on falling edges and records the output after each rising edge settles. Timescale is explicit here for reproducibility; the original scratch bench omitted it, which changes clock units but not the per-clock integer results.

```powershell
$waveRows = @(
    @{k=0;cr=$true;a=62132;m=4673},
    @{k=1;cr=$true;a=63918;m=10113},
    @{k=2;cr=$true;a=64884;m=25472},
    @{k=3;cr=$false;a=63918;m=10113})
foreach ($row in $waveRows) {
    $k = $row.k
    $instance = if ($row.cr) {
        'crrc_shaper #(.A_Q16(' + $row.a + '),.K_Q16(26214),.ORDER(4)) dut' +
        '(.clk(clk),.rst(rst),.valid(valid),.sample(sample),.shaped(shaped));'
    } else {
        'trapezoidal_shaper #(.M_Q8(' + $row.m + '),.RISE(10),.FLAT(8)) dut' +
        '(.clk(clk),.rst(rst),.valid(valid),.sample(sample),.shaped(trapOut));' +
        ' assign shaped=trapOut;'
    }
    $tb = '`timescale 1ns/1ps' + "`n" +
        'module tb; reg clk=0; always #5 clk=~clk; reg rst=1; reg valid=0;' +
        ' reg signed [15:0] sample=0; wire signed [39:0] shaped;' +
        ' wire signed [31:0] trapOut; reg [15:0] mem[0:4095]; integer i; integer fd; ' +
        $instance + ' initial begin $readmemh("input' + $k + '.hex",mem);' +
        ' fd=$fopen("actual' + $k + '.txt","w"); repeat(3) @(negedge clk); rst=0; valid=1;' +
        ' for(i=0;i<4096;i=i+1) begin sample=mem[i]; @(posedge clk); #1;' +
        ' $fdisplay(fd,"%0d",shaped); @(negedge clk); end $fclose(fd); $finish; end endmodule'
    Set-Content -LiteralPath ('tb' + $k + '.sv') -Value $tb
    $sv = if ($row.cr) { 'crrc_shaper.sv' } else { 'trapezoidal_shaper.sv' }
    & iverilog -g2012 -s tb -o ('sim' + $k + '.vvp') ('tb' + $k + '.sv') (Join-Path $waveRepo ('rtl/' + $sv))
    if ($LASTEXITCODE -ne 0) { throw 'iverilog failed' }
    & vvp ('sim' + $k + '.vvp')
    if ($LASTEXITCODE -ne 0) { throw 'vvp failed' }
    $expected = Get-Content ('expected' + $k + '.txt')
    $actual = Get-Content ('actual' + $k + '.txt')
    if ($actual.Count -ne $expected.Count) { throw 'length mismatch' }
    $bad = 0
    for ($i=0; $i -lt $expected.Count; $i++) {
        if ($expected[$i].Trim() -ne $actual[$i].Trim()) { $bad++ }
    }
    "RTL k=$k samples=$($expected.Count) mismatches=$bad"
    if ($bad -ne 0) { throw 'sample mismatch' }
}
```
