# PLAN.Studio.Readout.Review — measured review of TODO-40

Scope: review only of the reference plan against Studio at `aeac875` and a copied snapshot of the uncommitted TODO-19 engine. No product code, tests, reference plan or normative documents were changed. Measurements are short headless pilots, not physics validation.

Status: reviewed 2026-10-08; implementation requires the author's decisions and the merged TODO-19 API. Engine references below are relative to the TODO-19 worktree; Studio references are relative to this repository.

## Recommendation and premise corrections

The intended viewer is useful, but the premise that stage 1 provides a live acquisition readout ready to connect to Studio is wrong. Stage 1 supplies study components. Live integration requires an incremental pulse processor, event association and explicit horizon semantics. Removing a guard or processing each snapshot independently would produce incorrect pile-up, stop/continue and waveform results.

Default Studio is not the stage-1 lab head: `OpticsSettings` and `MainViewModel` start with 30×30 crystals, 0.6 mm pitch, rank 13, 0.7 mm mask cells, D=80 mm; the initial source is Cs-137, 500 µCi, detector-referenced distance 1000 mm. Studio adds 100 µm gap, 0.15 mm entrance and 2 mm backing. Ambient defaults to 0.10 µSv/h. Interaction recording presently refuses that ambient acquisition. The reference plan must explicitly decide supported scenes and geometry rather than imply all existing Start paths work.

RD-14 leaves pitch subject to fresh confirmation and retains DirectCrystal as engine default. Turn 5 was appended to the engine report during this review and was read on the final pass: it pins a fresh 32-seed confirmation rule, increases flood statistics to 3000/crystal, and reports only a two-seed pilot. Its tie preferences are 3.2 mm pitch, 0.2 mm wall and ratio 0.03; these are rule preferences, not a completed confirmation result. Do not label a pitch or a geometry/readout combination confirmed on the strength of that pilot. Recheck after merge. This review does not adopt the historical localisation, FWHM or mis-ID numbers as Studio tolerances.

### Every “What exists” row

| Reference row | Verified correction and code |
|---|---|
| Detector workspace | Correct: `DetectorWorkspaceViewModel.Refresh`, `DetectorFaceService`, `DetectorFace` and `DetectorFaceView` supply exact active crystal/gap rectangles and seeded static gain, pending before acquisition and acquired afterwards. Counts/live-time rate are text; this is not an acquired spatial flood, sensor layout or LUT display. |
| Waveform workspace | Correct with qualification: `WaveformService.Process` synthesizes an array-wide energy ADC trace and an integer trapezoid or Q12 CR-RC trace from raw deposits and the acquired chain. The literal note says four position channels are not modelled. Its measured amplitudes agree with isolated Spectrum/Imaging, but shaped heights are explicitly not MCA energy; rate study re-spaces events locally. |
| Acquisition | Position is direct assignment. `AcquisitionSession.ProduceAsync` constructs `ListModeSource(_config)` without recording, keeps raw `DetectedEvent`s, adds one count at each direct pixel, decodes, copies the entire event list and publishes on a nominal 250 ms cadence. Transport budget is 200 ms; channel capacity is two, DropOldest. Spectrum/Imaging separately reproduce an index-addressed response, rather than consume stored measured events. `MainViewModel.ReadSegmentAsync` awaits Spectrum and Imaging updates; scope is independent and active-only. |
| Engine readout | `ListModeSource` always calls `ReadoutGuard.RequireDirect`, even with `recordInteractions:true`. `DefaultSimulationFactory.CreateDetector`, `ComptonFactory.CreateDetector` and `EventStreamStudy.Generate` also guard. Null or explicit DirectCrystal is accepted; non-direct is refused. Recording additionally refuses any ambient object, positive BSR or positive background dark rate. `LastInteractions` belongs to the last returned signal event and must travel with look-ahead. Study code deliberately clones and sets Readout=null before transport, then applies a separate readout. This is an explicit study adapter, not general factory support. |
| Drawings | `ReadoutExport.Build` resolves a named study geometry/readout/trigger, exports configuration, network weights and optional SPICE circuit; `samples/readout/schematic.py` generates SVG and `netlist_check.py` independently solves it. `HW.Readout` drawings show 3.2 mm / 0.2 mm gap and DPC ratio 0.1, not the selected ratio 0.01. Export constructs a one-photon/one-depth drawing device; it is not a calibration optical table. It does not accept Studio settings directly, nor provide an interactive WPF schematic. |

### SR-1 through SR-8 disposition

| Proposal | Disposition |
|---|---|
| SR-1 selector | Adopt frozen physical selection and DirectCrystal default. A preset must expand to explicit resolved settings; distinguish a readout preset from an optics preset. `ReadoutConfig` defaults are DPC ratio 0.1, not the measured candidate 0.01. IndependentSipm needs explicit zero suppression (the comparison candidate uses 3 sigma); simply changing Mode leaves it unsuppressed. Recommend initial physical support on the 12×12 reference geometry, with other geometries explicitly experimental/unavailable until measured. |
| SR-2 Detector | Adopt, except “package area” overstates the API: `SensorLayout` supplies active width, grid pitch and offsets, not an actual package outline. Draw active and pitch-cell/dead-border footprints and label virtual matched sensors. Separate calibration density (smoothed) from live raw-coordinate histogram and LUT-assigned count image. Per-crystal confusion requires truth-labelled independent validation, not the calibration or live data masquerading as validation. |
| SR-3 Waveform | Adopt measured-record association, but existing `ReadoutEvent` has hold time, codes, dominant hit, contribution count and share only. It has no trigger time, trigger transition trace, held analogue values, contributors' global IDs or clipping flags. `Process` retains no state or waveform. Require those engine outputs before promising markers and exact trace/hold agreement. Do not present the legacy integer-shaper trace as the four-output processor's estimator. |
| SR-4 consistency | Adopt one measured record. Bypass legacy `MeasurementStage`, Spectrum's energy-only merge and Imaging's analytic response for physical data. Use calibrated held sum and LUT ID; reject unknown LUT positions explicitly. Current strip calibration and energy-window widths depend on the legacy chain and cannot be reused unchanged. Disable physical stripping initially, and label any explicit windows without borrowing the legacy FWHM model. CC/MLEM may reconstruct measured LUT floods, but current MLEM forward response omits readout migration and trigger efficiency: label that approximation, no inherited precision claim. |
| SR-5 performance | Replace unconditional 4 Hz performance with nominal latest-prefix publication and honest MC/readout-limited pacing. Preparation is substantial and currently uncancellable: `OpticalResponse`, network solve, `ReadoutFlood.Transport`, `ReadoutCalibration.Build` and pulse `Process` take no token. Build only on an explicit prepare/start action, cache by physical content, and check cancellation inside long loops. Merely wrapping in Task.Run does not make Stop timely. |
| SR-6 layering | Adopt. Core remains plain net9.0; immutable UI-free records/contracts/geometry there, engine adapters in Services, drawing and automation peers in WPF controls, views InitializeComponent-only. Reuse plot/heatmap geometry and theme roles but add raw-coordinate labels and required overlay/threshold support rather than pretend existing controls expose them. |
| SR-7 verification | Adopt exact proposed rows below, deterministic integration tests and detached renders. Existing SR-RUN-16/-21, SR-WAVE-01/-02/-04/-06 and Spectrum/Imaging analytic-response rows need DirectCrystal qualification; adding rows without correcting their universal wording is contradictory. Desktop scenarios are proposals only, not run in this turn. |
| SR-8 scope | Adopt DirectCrystal engine default and no new saturation/recovery physics. Engine already has optional no-recovery saturation, off by default; call this an occupancy bound, not recovery or a validated small-pitch device. Also exclude RC network dynamics, trigger-noise jitter, channel skew, wall gamma transport, electronics redesign and a new “best pitch” selection. |

## Headless measurements

The scratch probe and numerical results are retained under `%TEMP%/gcam-todo40/probe/`. `sources.sha256` records the relative path and SHA-256 of every compiled engine source. The probe copies only source files from the six engine projects, excludes bin/obj, compiles them into one isolated Release net9.0 executable, with no project references back to either worktree. `NuGet.Config` clears package sources; no packages are needed. This avoids writes to TODO-19 even during restore/build. Source-copy timing is not an atomic worktree snapshot; hashes identify the exact pilot subject. Recheck against the merge.

Configuration: Studio scene-builder geometry, Cs-137 500 µCi at (0,0), distance 1000 mm, signal only, GAGG transport, entrance 0.15 mm, backing 2 mm, gap 0.1 mm. Readout: four outputs, DPC row 1000 ohm/column 10 ohm, specular 0.98, eight depth bins, 100,000 optical photons/bin, sum threshold 50 keV-equivalent, common hold, 8 ns grid, unlimited cells. Other readout values are current engine defaults. Physical gain sigma is not injected through Studio's legacy gain stage.

For each geometry: time one full device construction (optics plus network), transport 2000 scored 661.7-keV histories per crystal, build the same calibration three times (fixed response/noise streams), and time 5000 transported flood histories at 100, 1000, 10,000, 100,000 and 1,000,000 input hits/s, three repetitions. Per-hit cost includes Respond, allocated analogue channels, complete batch pulse processing, raw position, LUT lookup and calibrated energy, not MC transport, decoding, plotting or future incremental bookkeeping. Retiming uses exponential gaps; response/noise seeds vary between repetitions. The flood includes the Compton continuum, not just photopeak events. Initial low-rate timings include tiered JIT settling; keep their range rather than discarding it as an outlier. A separate 3000-event ListModeSource pilot compares recording on/off with seed 401.

### Preparation and calibration

| Quantity | Baseline: 12×12, pitch 1 mm | Sharp: 30×30, pitch 0.6 mm |
|---|---:|---:|
| Device build, optics plus network (one build) | 7.654 s | 45.399 s |
| Flood transport (scored histories) | 0.528 s / 288,000 | 2.347 s / 1,800,000 |
| Calibration builds, same seed repeated | 1.002, 0.740, 0.710 s | 33.575, 37.014, 36.856 s |
| Calibration outcome | success, all 144 gains calibrated | failure on all repeats |
| Triggered / photopeak-windowed calibration events | 266,330 / 91,037 | 1,666,682 / 634,207 |
| Peaks retained after nonmaximum suppression, before selecting/ordering markers | 265 (144 expected crystals) | 1988 (900 expected crystals) |
| Failure message | none | “flood peaks cannot be ordered into a 30 × 30 grid (row 0, columns 24/25)” |
| Cold full preparation sum | 9.185 s | 81.320 s, then failure |

The three repetitions reuse identical training data and RNG seeds: they establish timing spread and deterministic failure, not three independent calibration trials. `ReadoutCalibration.Triggered` actually counts trigger outputs with a positive sum that pass `Position`; it is not a separately retained count of every hardware trigger. A future display must label that existing denominator accurately or add the separate counter. Device construction is measured once, so no uncertainty interval is available for it. Do not tune smoothing/separation or reduce optical budget after seeing this failure and claim a validated 30×30 preset; a new fixed policy needs independent validation. Failure with DPC ratio 0.01 at this pitch does not prove all 30×30 networks fail.

### Event cost and snapshot budget

Total cost is microseconds per **input hit**, not per surviving trigger. Ranges are observed min–max across three short batches, not confidence intervals.

| Input hits/s | Baseline total µs/hit | Sharp total µs/hit |
|---:|---:|---:|
| 100 | 12.954–27.426 (initial JIT settling) | 30.426–30.998 |
| 1000 | 7.682–9.725 | 30.377–31.200 |
| 10,000 | 8.047–10.643 | 30.578–30.658 |
| 100,000 | 7.416–7.746 | 30.211–30.429 |
| 1,000,000 | 6.588–8.870 | 28.261–28.426 |

At 1000 input hits/s baseline response alone costs 1.906–2.243 µs and pulse/LUT/energy 5.729–7.482 µs. Sharp response alone costs 19.020–19.850 µs and pulse plus attempted LUT lookup 11.297–11.357 µs; its failed LUT maps zero events, so Sharp timing **does not include a successful energy calibration lookup or a functional imaging path**. Allocations are about 296 bytes/input hit at low rate, 285 at 100 kcps and 189–191 at 1 Mcps, including the realised hit/event objects and arrays. Lower cost at the highest rate partly reflects fewer recorded events, not improved fidelity.

A second scratch run builds only the baseline device and settles the full Respond/Process/Position path for four batches, then records five 5000-hit batches at 100 cps: **8.413, 9.452, 8.195, 9.263, 10.207 µs/hit** (median 9.263; no LUT/energy lookup in this supplementary warm-up check). This explains why a single inherited “15–33 µs/event” cannot be a performance requirement: geometry, JIT, rate, outcome and what “event” includes all matter.

The 3000-accepted-signal pilot estimates the running source rate at **38.307 cps baseline / 71.878 cps Sharp**, with identical rates and history counts recording off/on. Histories emitted: 15,290 / 18,334. Recording retained 4128 / 4256 sites. Transport wall times off/on were 0.0230/0.0184 s and 0.0356/0.0354 s; these are too short and affected by warm-up to conclude that recording is faster or to bound overhead. Allocated-byte increments from recording were **89.920 / 92.096 bytes per accepted history**. These rate estimates are one seeded MC prefix; no seed-spread interval was measured. They exclude Studio's default ambient and are input-history rates, not final accepted physical-readout rates.

At speed ×10 and nominal 250 ms cadence, the measured signal rates request roughly **96 baseline / 180 Sharp input hits per snapshot**. Baseline readout cost from the 100-cps pilot is about **1.24–2.63 ms** (warm path roughly 0.8–1.0 ms); Sharp is **5.47–5.57 ms** but its calibration fails. These are extrapolations from pre-transported flood histories. They omit transport, decoder, snapshot projection and UI consumption. At ×1, 1000 cps requests 250 hits/tick, costing roughly 1.9–2.4 / 7.6–7.8 ms; 100 kcps requests 25,000 hits/tick, costing 185–194 / 755–761 ms. Multiply by requested speed before comparing with the session's 200 ms worker budget. The default signal-rate case has headroom; arbitrary activity, speed and geometries cannot be promised 4 Hz effective processing. Nominal cadence is not a bound on user-visible refresh while the consumer awaits projections.

### Retained interaction memory and existing prefix copies

`Unsafe.SizeOf<InteractionSite>()` is **48 bytes** (five doubles plus two ints); `DetectedEvent` is **24 bytes**. On this x64 runtime, raw per-event arrays use approximately `24 + 48×siteCount` bytes, plus 8 bytes/event in the outer reference array/list backing. The flood container calculation includes its actual List capacity but excludes its small List object, allocator/GC metadata, temporary working storage and optical/LUT caches. A measured cloning-allocation check on 5000 baseline flood records (1.4102 sites/record) allocated **99.726 bytes/record**, agreeing with that layout to small LINQ/container overhead.

| Retained raw flood | Baseline | Sharp |
|---|---:|---:|
| Mean sites/history | 1.41166 | 1.48791 |
| Calculated owned arrays + backing refs | 28,730,856 bytes (27.40 MiB) | 186,155,640 bytes (177.53 MiB) |
| Bytes/history | 99.760 | 103.420 |

The actual list-mode prefixes have 1.376 / 1.41867 sites per accepted history, implying about **98.05 / 100.10 bytes/history** including outer references. At the measured source rates and 60 s, full raw interactions would occupy roughly **0.215 / 0.412 MiB**, before legacy events, realised channels, measured records and immutable snapshot storage. Use the flood's approximately 100–103 bytes/history as a planning scale only: at 1000 cps for 60 s it is 5.7–5.9 MiB; at 100 kcps it is 571–592 MiB; ×speed changes wall acquisition time, not memory for the same live-time horizon. Cascade energy/site distributions can change this scale. Full truth retention is inexpensive at the short default signal run but unbounded under arbitrary activity and Continue.

The supplementary run measures the **current** snapshot idiom, `Array.AsReadOnly(list.ToArray())`, after 20 warm-up copies, averaged over 100 copies. This is raw DetectedEvent storage only, not the proposed physical payload:

| Cumulative events | Mean copy ms/snapshot | Allocated bytes/snapshot |
|---:|---:|---:|
| 2300 (baseline ~60 s) | 0.0040 | 55,249 |
| 4313 (Sharp ~60 s) | 0.0454 | 103,561 |
| 60,000 | 0.1545 | 1,440,133 |
| 600,000 | 2.1332 | 14,400,049 |

At four publications/s those last arrays allocate about 5.5 and 54.9 MiB/s respectively; cumulative copies grow with acquired prefix, even if consumers only process new events. Copying InteractionSite[][] references shallowly would cost roughly 8 bytes/event but still retain the owned inner arrays; deep-copying all interactions each tick is unnecessary. Recommend immutable append-only chunks to avoid repeating an ever-growing copy. A retained four-double channel array alone is 56 bytes/hit plus its owning record/references; independent sensor arrays are `24 + 8×sensorCount` bytes/hit (7224 bytes for 900 sensors), so high-rate independent retention needs its own support limits. A continuous four-channel 125 MSPS double raster for 60 s would be 240 GB of values alone: scope must render bounded local windows.

All times are Stopwatch wall clock on a busy machine, one process, no affinity or background-process intervention. Three repeats describe observed variation only. Memory layout measurements describe this .NET/x64 runtime; they are not whole-process RSS or a peak preparation-memory bound. Preparation may hold multiple tuple lists, histograms, priority queues and arrays simultaneously. No precision or performance tolerance was borrowed or added to product tests.

## Proposed data flow and service design

1. `ReadoutSettings` is a deeply immutable Core physical input record owned by MainViewModel alongside DetectorSettings. A `ReadoutEditorViewModel` owns mode/preset/units/validation, participates in the existing physical lock and reverts programmatic changes. Services expands it into a cloned engine config; no mutable engine config crosses a UI/worker boundary. Resolve material, scintillation, sensor and pulse defaults explicitly: engine ReadoutDevice's null light-yield/intrinsic fields fall back to GAGG and pulse defaults to `FrontEndParts.Default`, irrespective of the selected Studio chain. Initial physical preset is GAGG with its readout pulse facts; do not offer CsI while silently retaining GAGG optics/light constants.

   Existing optical response is averaged over lateral position within each crystal and binned in depth. `ReadoutDevice.ExpectedPhotoelectrons` uses crystal indices, depth and energy; it does not consume site X/Y or TimeNs for a separate pulse. The processor gives each history a common pulse at its list-mode arrival. Preserve these declared stage-1 approximations in captions; displaying sites must not imply that subcrystal timing or lateral optical response has been modelled.
2. `IReadoutPreparationService` returns a frozen `ReadoutCalibrationView` and opaque preparation identity: resolved settings, seed policy, calibration line/budget, scored/triggered/windowed totals, marker counts, gain-fallback count, smoothed density, LUT labels/peaks, schematic geometry and failure reason. Services holds optics/network/calibration in a bounded cache, distinct keys for optical geometry/material/seed and full calibration response/trigger/pulse/ADC/budget/seed. View changes never rebuild. Calibration seed is pinned separately and validation streams are disjoint; do not use source truth or live counts to train the LUT. Only successful complete artifacts are reusable. UI shows Not prepared/Preparing/Ready/Failed, costs and progress; preparation cancellation discards obsolete results. No calibrate-on-every-editor-keystroke.
3. A single session-owned `ReadoutAcquisitionAdapter` transports fresh interactions, applies stochastic sensor response once, advances a stateful engine processor and appends immutable measured events. Preserve source look-ahead AND its interaction payload, response/noise streams, active pulses, Armed/Busy/WaitRearm state, partially searched hold, grid origin/step and unpublished events across Stop/Continue. Keep transport settings and requested readout identity separate: an explicit raw interaction producer may use a DirectCrystal transport clone while recording, but must carry physical identity and never accidentally label its direct pixels as measured.
4. Proposed engine contract: `Append(hitId, arrivalTime, channelAmplitudes)`, `AdvanceTo(observedHorizon, token)` and a frozen diagnostics result. It emits a record only when its entire hold search is observed, with no fabricated future counts. Stop freezes state without finalising an open hold; Continue extends the same horizon. Completed retains a right-censored open hold for future Continue. Either implement this contract in the engine after merge, or explicitly narrow TODO-40 to offline studies; Studio must not own a second divergent pulse algorithm. Batch `Process` remains a regression oracle with the same explicit terminal-horizon policy.
5. Core `MeasuredReadoutEvent` contains acquisition ID, stable measured-event ID, hold and trigger times, fixed held codes (four scalars for Anger; immutable channel block for independent), raw dimensionless X/Y, calibrated energy, LUT crystal or unknown, calibration ID and diagnostic flags. `ReadoutTraceHit` retains stable input ID, arrival and realised analogue channels. Provenance relates multiple input histories to each trigger; dominant ID/share alone is not a complete contributor list. True interactions/deposits live in a separate diagnostic type and never determine runtime LUT assignment. Store outcomes for untriggered/invalid-position histories as counters, not as invented counts. Expose clipping only once the engine explicitly records it; a code equal to a rail alone cannot prove analogue clipping.
6. Extend AcquisitionSnapshot with a discriminated Direct/Physical payload, measurement horizon, calibration identity, input/triggered/assigned/unknown counters, immutable measured-prefix chunks and trace-prefix chunks. The physical Counts/flood/status rate are LUT-assigned triggered events/live time; show triggered and unknown separately. Spectrum total = valid-energy assigned events, accounted as axis bins plus underflow/overflow, so Imaging All and Spectrum conserve the same accepted population. Never overload `DetectedEvent.DepositKeV` with measured energy or its pixel with LUT ID while still calling it truth. Retain chunk owners as immutable; a mutable List behind IReadOnlyList is insufficient. Copy only the open chunk, seal full chunks, keep published prefixes valid even after Reset.
7. Spectrum histograms stored measured energies with no second gain/noise/merge. Imaging All and energy-window floods use stored LUT pixels and the same energy-window policy. Changing a view window re-filters retained measured records; acquisition remains fixed. Initially show explicit measured window bounds, derived from a separately characterised physical resolution table if available, otherwise manual keV bounds; do not show `FrontEndModel.FwhmFraction` as physical readout resolution. Physical stripping is unavailable until H-only calibration traverses the same readout and its rate dependence has been specified. Focus uses retained measured floods without transport or calibration. CC/MLEM selection remains a view operation, with the forward-response approximation visible.
8. `IReadoutWaveformService` renders realised trace hits around a selected measured event using the engine's exact Unit pulse/support/grid. It does not re-draw sensor charge or electronics noise to fabricate the measured held values. Plot analogue A/B/C/D and sum in code-equivalent units; overlay held signed ADC samples and the stored trigger/hold markers. Trigger lines belong on the sum for Sum and on each channel for OR/AND. No continuous sampled ADC-noise trace is claimed: current engine digitizes held values only. Keep shared X navigation; avoid storing continuous 125 MSPS acquisitions. Diagnostic rate/ideal replay, if retained, gets a separate result ID and explicit study label and cannot change the measured record.
9. Extend DetectorWorkspaceViewModel via `IReadoutDetectorService`: Crystal face / Sensor grid / Network / Calibration flood / Live flood tabs within Detector, with an exact coordinate model and overlays. Raw X/Y are dimensionless [-1,1); not detector millimetres. Reuse image geometry with configurable coordinate unit or a dedicated ReadoutFloodView; do not feed Anger coordinates into an mm-labelled heatmap. Copy LUT Density because the engine exposes a mutable double array; flatten labels through LabelOfBin. Confusion/acceptance diagnostics are an optional explicit independent validation study, with denominator and truth definition shown (single-crystal mis-ID versus multisite disagreement); no per-live-crystal acceptance denominator can be inferred from measured data alone.

Retention recommendation: keep compact realised channel hits plus measured records for association and scope replay; discard full interactions after response except a labelled bounded diagnostic sample. Reconstruct a selected local waveform from the realised channels, not truth plus a newly seeded simulation. Full raw retention can be an explicit diagnostic option with an estimated memory display. IndependentSipm costs scale with sensor count and must not allocate/display all channels as full traces by default; selected sensor traces plus sum/centroid diagnostics are enough.

## Screen sketches

Existing fixed left/right panels and flexible centre are retained. Detector uses an internal view selector rather than shrinking five plots into the centre. Figures here are layouts, not numerical evidence.

```text
GCAM    Imaging Spectrum Waveform Detector       Theme | Live time | Speed | Seed | Start
Sources / Detector        Detector: Calibration flood             Readout facts
Readout [Four outputs v]  [Face][Sensors][Network][Calibration][Live] Acquired settings
Preset [DPC r0.01 v]      +--------------------------------------+ 4 outputs; virtual SiPMs
Ambient 0 (required)      | raw Y                                 | Calibration: Ready
Readout assumptions       |    .+. .+. .+.    white LUT contours  | 661.7 keV; train seed
GAGG / unlimited cells    |    .+. .+. .+.    marker IDs           | scored / triggered /
[Prepare readout]         |                                      | windowed; gain fallback
                         |                         raw X         | [Show LUT boundaries]
                         +--------------------------------------+ [Independent validation]
                         Density colour bar; raw X/Y hover         study only; denominator
                         Calibration flood - smoothed counts       shown with each metric
Status: Preparing optics... Cancel / or measured t, counts, cps and limiting state
```

```text
Detector: Network                              Detector: Live flood
+-----------------------------------------+    +-------------------------------+
| active SiPM squares within pitch cells  |    | raw coordinate counts + LUT    |
| row resistor chains -> column chains    |    | selected measured event cross  |
| A(x-,y-) B(x+,y-) C(x-,y+) D(x+,y+)      |    +-------------------------------+
| -> common pulse -> sum trigger -> hold  |    Assigned N; unknown U; triggered T
| -> 14-bit held ADC -> X/Y -> LUT        |    Crystal-count image remains Imaging
+-----------------------------------------+    Calibration and live floods labelled
Illustrative DC circuit; 1000 / 10 ohm          separately, with separate colour bars
Sensor active area / pitch border; virtual     No live truth acceptance claim
```

```text
Waveform: measured event #k                     Selection
+-----------------------------------------+    [Latest] [Next] [Index k]
| A     /\__    B  /\___                 |    Acquired time / hold time
| C    /\__     D /\____                 |    Calibration ID; LUT crystal
| analogue code-equivalent traces         |    raw X / Y; measured keV
+-----------------------------------------+    Dominant hit; contribution count
| sum /\__/\___  threshold ----           |    Trigger: Sum 50 keV-equivalent
|     trigger |  hold |  signed ADC points |    No RC/skew/jitter model
+-----------------------------------------+    Real acquired trace / Study replay
Shared time axis in us relative to stored hold; window and navigation are view settings
Input IDs / times / contributor facts in event rows; all A-D belong to measured event #k
```

Use DynamicResource brushes, existing padding/gap/type roles and neutral white/black LUT overlays. Four-output colours need a fourth trace role in both themes or four stacked labelled lanes; current PlotColourRole supplies only three series hues. Recommend stacked labelled lanes with common X for legibility, plus a sum lane. Add engine-free geometry helpers and automation peers for new surfaces. Use a ListBox template instead of an unstyled DataGrid. Detached bindings use Shared/local context, never Window ancestor lookup.

## Exact proposed requirement wording

These rows are proposed text for the planner; no VV documents were edited. IDs must be rechecked against the merged SRS before adoption.

| ID | Proposed normative text |
|---|---|
| SR-DET-05 | Detector shall offer DirectCrystal (default), FourOutputAnger and IndependentSipm as explicit physical readout selections for supported geometries. The resolved geometry, material, sensor, optics, network, trigger, pulse, digitizer and calibration inputs shall be frozen at Start and locked by the view model while acquiring or while acquired data exist. Unsupported combinations shall block Start with an actionable message; they shall not silently change other physical inputs. |
| SR-DET-06 | Detector shall display the acquired crystal and sensor geometry, active sensor footprints and pitch-cell borders, and an illustrative network generated from the same resolved circuit as the simulation. Virtual matched sensors and omitted RC dynamics shall be labelled. Pending geometry before acquisition shall retain the existing pending-settings caption. |
| SR-DET-07 | An explicit preparation action shall build an independent calibration flood on a worker, show preparation state and progress, and publish density, ordered markers and LUT boundaries only with their calibration identity. Failed calibration shall show the engine reason and shall prevent physical acquisition; no direct-assignment fallback shall be used. Calibration shall be reused only for identical resolved inputs and seed/budget policy. |
| SR-DET-08 | Calibration density, live raw-coordinate counts and LUT-assigned crystal counts shall have distinct names, axes and count denominators. Raw X/Y shall be dimensionless. Independent validation diagnostics shall identify validation seed/budget, energy and truth definition; training data shall not be reported as held-out confusion or acceptance evidence. |
| SR-DET-09 | Detached render fixtures shall cover pending, preparing, ready, failed and acquired readout displays, sensor/network views and flood overlays in both themes at 1280×800 and 1440×900, without an HWND or desktop input. |
| SR-WAVE-09 | For a physical acquisition, Waveform shall select a stable measured-event ID and display realised analogue channel pulses and their sum on a common acquired time axis, with stored trigger and hold instants and signed held ADC samples. Anger channels shall follow the engine order A(x−,y−), B(x+,y−), C(x−,y+), D(x+,y+). Displayed pulses shall sum every contributing retained hit with the acquisition's engine pulse kernel; response fluctuations shall not be re-drawn. |
| SR-WAVE-10 | The selected physical event shall display its raw X/Y, LUT crystal or unknown status, calibrated held-sum energy, calibration identity and contribution diagnostics from the same record used by Spectrum and Imaging. Trigger thresholds shall show their units and apply to the sum or channels according to the acquired trigger logic. Continuous digitizer noise and integer-shaper MCA equivalence shall not be implied when only held conversion is modelled. |
| SR-WAVE-11 | Physical scope generation shall run only while active, retain held-selection output for a covered immutable prefix, bound total trace allocation across channels, cancel or discard obsolete requests, and preserve X navigation without reprocessing. Any diagnostic ideal or rate replay shall be labelled with a separate study identity and shall leave measured acquisition data unchanged. |
| SR-RUN-30 | A physical acquisition shall apply optical/sensor response, channel overlap, trigger, hold, digitization, LUT assignment and calibrated energy exactly once before workspace processing. Immutable snapshots shall carry the resulting measured prefix and acquisition/calibration identities. Valid assigned events shall supply both Spectrum and Imaging; unknown LUT positions and invalid energy shall be accounted separately and shall not be assigned a truth pixel. |
| SR-RUN-31 | Stop and Continue shall preserve transport look-ahead with its interaction payload, readout random streams, active pulses, trigger/re-arm state, hold search, grid phase and unpublished records. No event shall be finalised using unobserved future hits. To the same observed horizon, a stopped/continued acquisition shall equal an uninterrupted acquisition event for event, regardless of snapshot partitioning or workspace visibility. A right-censored hold at Completed shall remain resumable. |
| SR-RUN-32 | Acquisition shall target cumulative latest-prefix publication at 4 Hz with bounded backpressure and cooperative cancellation of transport, readout and preparation. When work cannot meet requested speed, acquired time shall advance only through the proven observed prefix and achieved speed shall be shown. View changes shall not retrigger preparation or measurement. |
| SR-RUN-33 | For physical data, the status count/rate and Imaging All flood shall count valid LUT-assigned recorded events; Spectrum shall histogram their stored calibrated energies with explicit out-of-axis accounting. Input histories, triggers and rejected/unknown outcomes shall be separately labelled. Legacy gain/noise smearing and energy-only pile-up merging shall not be applied to these records. |
| SR-RUN-34 | Physical acquisition shall refuse ambient/background/chain/geometry combinations whose interaction or response support is unavailable, before transport starts. An unsupported selection shall preserve the user's current values and state and explain the required correction. DirectCrystal behaviour shall retain its existing supported inputs and seeded event stream. |

Qualify SR-RUN-16/-21 and SR-WAVE-01/-02/-04/-06 with “For DirectCrystal acquisition” where they prescribe arg-max pixels, true-deposit scope index and analytic gain/smear response. Extend SR-RUN-12's physical input list with resolved readout/preparation inputs. Extend SR-RUN-23 with SR-RUN-31's state. Keep SR-RUN-28's rate definition but specify the accepted population through SR-RUN-33. Spectrum/Imaging requirements must branch their response, windows, stripping and resolution descriptions by readout mode; physical windows must use explicit bounds or a separately characterised resolution contract. Do not alter old numerical assertions to make new paths pass.

## Verification to implement after decisions

| Layer | Tests and oracle |
|---|---|
| Engine prerequisite | Stateful-vs-batch equality on fixed realised hits with an explicit shared horizon; split before threshold, inside hold, at busy end/re-arm and through a pulse tail. Include two overlapping hits in different crystals, subthreshold-only input, no triggers, equal arrival times and late detector-referenced acquisition times. Assert IDs/codes/hold times and RNG progression, no snapshot-dependent reseeding. Cancellation before a draw consumes none; cancellation during preparation publishes no partial artifact. |
| Services | Direct mode preserves existing fixtures; settings freeze and unsupported ambient/BSR/non-GAGG paths fail before work; successful preparation cached by exact content; single changed trigger/noise/sensor/optical seed invalidates appropriate cache; failed cache never promoted. Detector snapshot arrays remain unchanged after new work/reset. |
| Shared measured prefix | Derive Spectrum histogram and Imaging floods independently by iterating stored records. Assert exact integer counts, bins plus under/overflow, unknown accounting and window membership, and verify no MeasurementStage invocation on physical events. Inject a multisite fixture whose LUT pixel differs from truth arg-max and an overlapping pair whose measured sum/position differ from either input; assert stored outcomes, not arg-max correctness. |
| Waveform | Evaluate stored realised channel amplitudes with engine Unit at the stored hold grid and compare held analogue samples before conversion; held code comparison uses the exact recorded noise/digitizer output, not a newly sampled trace. Sum-channel equality from A+B+C+D, trigger logic/units, stored marker time and contributor IDs. Check partial horizon label, active-only work, held prefix reuse and obsolete-result discard. Independent mode displays selected sensor subset without four-output labels. |
| Core/UI-free geometry | Sensor active/pitch footprints, row-zero orientation, corner labels, raw-coordinate bin-centre mapping (-1+(i+0.5)*2/B), LUT boundary transformation under pan/zoom, dimensionless coordinate readout; state/lock tables including preparation cancel/failure and Reset. |
| Offscreen | Synthetic fixtures for Direct before/acquired, physical ready/failed/preparing, Anger and Independent, calibration/live distinction, unknown event, overlapping waveform and empty/partial horizon. Both themes and two sizes; assert bindings, trace count/order, shared X, correct threshold target, marker and LUT overlay alignment, labels and scrolling, no clipped fields. PNGs are layout evidence, not MC physics evidence. Add 125/150% coordinate helper checks where supported by existing detached harness. |
| Numerical evidence | Only if a product claim is requested: pin each Studio geometry/scene/readout before sampling; independent calibration and validation seeds, explicit acceptance/mis-ID definitions and MC uncertainty. Derive tolerances from that experiment's sample sizes/covariance or a proven deterministic limit. Neither stage-1 lab localisation nor the default-chain FWHM is an oracle for Studio physical readout. Timing is reported as repeated measurements, not a flaky CI pass/fail wall-time threshold. |

Suggested new classes: ReadoutPreparationServiceTests, ReadoutAcquisitionTests, ReadoutContinuationTests, MeasuredReadoutProjectionTests, ReadoutWaveformServiceTests, ReadoutEditorViewModelTests and ReadoutGeometryTests, with small deterministic fixtures in ordinary runs. Long precision families remain opt-in and planner-run. Add catalog entries and traceability through the existing record tooling when implementation lands; report actual test counts then.

Desktop scenarios to add, not run here: (1) select supported physical settings and explicitly turn ambient off, prepare, acquire, Stop/Continue/Reset and verify locks/provenance; (2) malformed/unsupported inputs preserve settings and block Start; (3) known failing calibration shows reason, no acquisition fallback; (4) select measured event and compare its ID/energy/LUT pixel across Waveform, Spectrum membership and Imaging count evidence; (5) deterministic overlap fixture or labelled high-rate replay shows all-channel overlap without altering acquisition; (6) calibration versus live flood tabs, LUT overlays, network/sensor labels, keyboard access, themes and navigation; (7) IndependentSipm reports channel count/zero suppression and avoids Anger labels; (8) cancellation and obsolete preparation/scope responses cannot overwrite the current selection. Use unique AutomationIds and manifests with the acquired settings hash/ID, record arrays and derived checks. Each new scenario needs a headless oracle and a break-verdict corruption that fails at the intended assertion, followed by recovery, on the author's later desktop go. Do not reuse the old localisation-side desktop tolerance.

## Order after TODO-19 merge

1. Reconcile RD-14/turn 5 and the exact merged API/source hashes. Have the planner adopt supported geometry, background and chain scope and the measured-event/horizon semantics. Review this prerequisite before UI implementation.
2. Add incremental engine processor/diagnostics/cancellation and explicit raw-interaction producer support, with batch/continuation parity. Keep ordinary factory guards unless a specific production entry point genuinely implements physical response.
3. Add immutable Core physical settings, preparation/measurement/snapshot contracts and editor/lock state tests. Introduce session adapter and compact immutable retention. Confirm default DirectCrystal regression.
4. Preparation cache/progress/failure and small independent validation diagnostics; no long physics selection run inside implementation. Re-measure preparation, count-rate/prefix-copy costs against merged binaries.
5. Spectrum/Imaging measured adapters, explicit window policy and physical stripping restriction; verify exact count/record association. Decide forward-response correction as a later study if needed.
6. Detector tabs/geometry/LUT/network and exact realised physical scope; Core helpers first, views and detached render fixtures next. Add tokens only where needed, keep physical inputs locked.
7. Headless suite and detached renders; planner applies SRS/SDS/VV/design/catalog text with evidence. Desktop scenarios implemented but run last only when the author frees the desktop; final suite, break-verdict, recovery and survey. No requirement is marked verified by an unrun desktop proposal.

## Author questions, options and recommendations

| Question | Options | Recommendation and reason |
|---|---|---|
| Live integration or offline exploration first? | A: live shared measured-record path with engine prerequisite; B: offline readout study screen first. | A matches SR-3/-4, but explicitly budget the engine work. B is a smaller legitimate scope if that prerequisite is deferred; it cannot claim live consistency. |
| First physical geometry? | A: explicitly selected 12×12 reference head; B: all current Studio presets immediately; C: wait for RD-14 pitch confirmation before any physical screen. | A for engineering exploration, clearly experimental; no “best pitch” label. B requires more cost/calibration measurements and support bounds. Reconcile C with the author's pending confirmation before publishing a preset as validated. |
| Ambient/BSR support? | A: block physical Start unless ambient=0 and BSR=0; B: extend ambient/background interaction recording in the engine before shipping. | A for bounded initial scope, without silently zeroing the default ambient field. B is required for physical acquisitions under realistic ambient but expands transport work substantially. |
| Physical detection chain and legacy gain inputs? | A: fixed labelled GAGG physical preset, disable inactive legacy gain/chain controls in physical mode; B: generalise material/chain/gain throughout the engine. | A initially. The readout currently defaults to GAGG response and does not apply Studio's CrystalUniformity stage; applying gain again would invent a second detector response. B needs an explicit site-level gain and material contract and new calibration evidence. |
| Preparation policy? | A: explicit Prepare with bounded content cache; B: auto-build after every physical edit; C: prepare lazily at Start. | A: gives reviewable calibration/failure before counting and avoids heavy rebuilds during editing. Start may use a matching prepared artifact, or explicitly show preparation if none exists. |
| Retention? | A: compact realised hits + measured records, bounded truth sample; B: all interactions; C: aggregate-only with recent scope ring. | A preserves arbitrary selected-event scope and continuation while controlling memory. B is a labelled diagnostics option. C loses old-event replay and must change SR-WAVE selection promises. |
| Physical windows and stripping? | A: explicit keV bounds / characterised physical response table, stripping disabled; B: full readout-aware H-only and rate-dependent calibration now; C: reuse analytic chain widths and strip ratios. | A first; B later if needed. Reject C: no demonstrated oracle for physical energy/position migration. |
| Four-channel layout? | A: four labelled lanes plus sum sharing X; B: four overlaid colours plus sum panel. | A at minimum window size; B can be an optional view once a fourth accessible trace role and collision tests exist. Held samples and analogue traces remain distinguishable. |
| Scope replay studies? | A: measured-only initially; B: keep ideal/rate diagnostics as separately labelled replays. | A for first implementation; B is compatible later only with separate study identity and no overwrite of acquisition facts. |
| Counts and unfinished holds? | A: assigned triggered counts; unknowns separate, hold waits for observed horizon and remains continuable; B: count every input history and flush holds at Stop. | A. B contradicts shared measured-event counts and continuation equivalence. It can be a separate input counter, not the measured status count. |

## Write audit and approval requests

Only the new review file was written in the repository. Write operations, in execution order (full shell scripts are in the command log):

1. Scratch setup shell: `New-Item -ItemType Directory -Force` for the scratch/probe/source directories; `Copy-Item -LiteralPath <engine-source> -Destination <scratch-source>` for the six engine source trees; `[IO.File]::WriteAllText` for `sources.sha256`, `Probe.csproj` and `NuGet.Config`. Each destination is beneath `%TEMP%/gcam-todo40/`; repository source paths are read-only inputs.
2. Probe shell: `[IO.File]::WriteAllText` for scratch `Program.cs`, followed from the scratch project directory by `dotnet run --project Probe.csproj -c Release`. Restore/build created scratch obj/bin and the program wrote `results.json`. Exit 0; no packages required.
3. `apply_patch` added `docs/PLAN.Studio.Readout.Review.md`.
4. Supplementary accounting shell: `[IO.File]::WriteAllText` updated scratch `Program.cs` to add the accounting dispatch and created `Accounting.cs`, then `dotnet run --project Probe.csproj -c Release -- accounting`. Build updated scratch obj/bin and the program wrote `accounting.json`. Exit 0.
5. `apply_patch` completed this review with measurement tables, the newly available turn-5 correction and the write audit.
6. `apply_patch` clarified the existing calibration denominator, suppressed-peak count and optical/time approximations after checking those members.

No desktop, product tests or long evidence suite were run. The isolated probe compiled and both runs ended successfully. No command changed git state or wrote into TODO-19. No cleanup was attempted. Final repository status is expected to contain this review and the already-present untracked reference plan only; the final check verifies LF and no machine-specific absolute paths in this file.

### APPROVAL REQUESTS

None. No irreversible action is required for this review.
