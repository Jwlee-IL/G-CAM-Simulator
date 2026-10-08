# Studio readout implementation — TODO-40, turn 2

Status: implemented against MainRoot 01592a5; headless verification passed. Desktop verification remains for the planner on the author's go. No material disagreement with SD-1 through SD-6. No tolerance was changed. This document proposes wording for the planner; it does not amend the SRS, SDS, traceability, AGENTS or README.

## Result and implementation stages

1. Engine prerequisite: `ReadoutPulseProcessor.CreateStream` owns the pulse grid, active/queued inputs, trigger/hold/busy/re-arm state and ADC RNG. A hold is converted only after its entire search window is observed. Stop and Completed do not flush a censored hold. Expired pulse inputs are released, retaining global input IDs for associations. Original batch processing remains the study oracle. `PhysicalListModeSource` is an explicit signal-only physical entry point; ordinary factory/list-mode guards stay in place. Cancellation hooks were added to optical-table construction, flood transport and calibration without changing their disabled-token arithmetic or random draws.
2. Services: acquisition captures the readout and preparation identity. Explicit Prepare uses a serialized, two-successful-entry cache. The versioned content key includes crystal count/pitch, gap, entrance and backing; the fixed GAGG preset and seed/budget policy belong to that version. It ignores inactive legacy gain/chain and view/source settings. Active sessions keep their artifact even after cache eviction. Preparation failure/cancellation publishes no usable partial calibration. Realised scalar hits and measured conversions are retained in sealed chunks; only the unfinished tail and chunk-reference directory are copied for each prefix. Only the first 256 scored input hits retain diagnostic interaction sites. Spectrum/Imaging use stored energies/LUT IDs; the physical path never invokes the legacy measurement or energy-only pile-up stage. Windows are inclusive explicit keV bounds. Stripping and ideal/rate replay are unavailable.
3. Core/views: unsupported heads cannot select physical readout; nonzero ambient or BSR prevents Start with the required correction and preserves the inputs. The experimental label states the GAGG/DPC/specular assumptions and missing dynamics. Prepare, cancellation, identity and failure are visible. Detector has SiPM/circuit, Anger/LUT, calibration and independent per-crystal diagnostics tabs. SiPM active footprints and pitch-cell borders are distinct; resistor edges come from the resolved engine graph. Calibration density is smoothed density; live density is raw-coordinate counts; Imaging is LUT-assigned crystal counts. Waveform has A–D and sum, shared time navigation, trigger/hold markers, signed held codes, raw position, LUT/unknown, energy and association. Its threshold is prepared on the worker so it remains visible after zooming. Core remains WPF-free; the new views' code-behind contains only InitializeComponent.
4. Tests: five new test classes and their catalog/discovery entries were added. Existing catalog entries/fingerprints were preserved. Detached renders ran; desktop methods were written and skipped. No Studio process or desktop input was launched.
5. TODO-42: estimate only, below. Ambient/BSR physical support was not implemented.

## Files by group

Paths below are repository-relative; names in a group share its directory prefix.

| Group | Files |
|---|---|
| `src/Gcam.Detector/Readout` | `OpticalResponse.cs`, `ReadoutDevice.cs`, `ReadoutPulseProcessor.cs`, new `ObservedReadoutEvent.cs` |
| `src/Gcam.Simulation/Readout` | `ReadoutCalibration.cs`, `ReadoutFlood.cs`, new `PhysicalListModeSource.cs`, `PhysicalMeasuredEvent.cs`, `PhysicalTruthSample.cs` |
| `src/Gcam.Studio.Core/Services` | `AcquisitionSnapshot.cs`, `DetectorSettings.cs`, `ImagingSettings.cs`, `SpectrumSettings.cs`, `WaveformEvent.cs`, `WaveformView.cs`; new `IReadoutPreparationService.cs`, `MeasuredReadoutRecord.cs`, `ReadoutCharges.cs`, `ReadoutCircuit.cs`, `ReadoutPolicy.cs`, `ReadoutPreparation.cs`, `ReadoutSnapshot.cs`, `ReadoutTruthSample.cs`, `RealisedReadoutHit.cs` |
| `src/Gcam.Studio.Core` | new `Detector/ReadoutDetectorTab.cs`; ViewModels `MainViewModel.cs`, new `MainViewModel.Readout.cs`, `DetectorWorkspaceViewModel.cs`, `ImagingWorkspaceViewModel.cs`, `SpectrumWorkspaceViewModel.cs`, `WaveformWorkspaceViewModel.cs` |
| `src/Gcam.Studio.Services` | `AcquisitionSession.cs`, `SimulationService.cs`, `SpectrumService.cs`, `ImagingService.cs`, `WaveformService.cs`; new `ImmutableRecordStore.cs`, `ReadoutPreparationService.cs`, `PhysicalReadoutProjection.cs`, `PhysicalReadoutWaveform.cs` |
| `src/Gcam.Studio` | `App.xaml.cs`; new `Controls/ReadoutMapView.cs`; new `Views/ReadoutPanel`, `ReadoutDetectorView`, `ReadoutWaveformView` XAML and InitializeComponent-only companions; existing `MainWindow`, `ChainPanel`, `DetectorPanel`, `DetectorView`, `ImagingOptionsPanel`, `SpectrumPanel`, `WaveformPanel`, `WaveformView` XAML |
| Tests | new `Gcam.Tests/ReadoutStreamTests.cs`, `Gcam.Studio.Tests/ReadoutViewModelTests.cs`, `Gcam.Studio.Services.Tests/PhysicalReadoutTests.cs`, `Gcam.Studio.RenderTests/ReadoutRenderTests.cs`, `Gcam.Studio.UiTests/ReadoutScenarioTests.cs` |
| Documentation | new this report; only new-class entries and their generated discovery rows in `docs/VV.Tests.md` |

The modified reference plan and untracked review were already present at turn entry and were not written in this turn. Other protected documentation was not edited. No git state command, installation, deletion or process stop was used.

## Verification and counts

Baseline was taken before the implementation, with the same Release solution test command. Final logs are under `%TEMP%/gcam-todo40/turn2/`, with `before`, `verified` and `render-final` TRX prefixes.

| Test project | Baseline passed / skipped | Final normal passed / skipped | Expectation |
|---|---:|---:|---|
| Engine | 534 / 0 | 538 / 0 | Original 534 plus four stream cases |
| Studio Core | 211 / 0 | 214 / 0 | Three new view-model cases |
| Studio Services | 108 / 7 | 113 / 7 | Five new physical cases |
| Studio UI | 15 / 20 | 16 / 23 | One headless count oracle; three new desktop cases skipped |
| Studio renders | 0 / 1 | 0 / 2 | Two opt-in render methods skipped normally; both passed in the separate render run |
| Total normal | 868 / 28 | 881 / 32 | 913 total; zero failures |

`dotnet build Gcam.sln -c Release` passed. Two existing analyzer warnings remain (`ImagingServiceTests` xUnit2000 and `WaveformServiceTests` xUnit2012). Full `dotnet test Gcam.sln -c Release` passed with process-local English CLI and task-scratch TEMP/TMP. Python unittests ran 43 cases: 42 passed, one opt-in git test skipped. Calibration checker reported four records current. Catalog discovery reported 166 units and zero incomplete descriptions. No existing test assertion was loosened. UI/evidence opt-ins remained off; render opt-in was set only for the detached run.

### Exact identity evidence

The retained `ReadoutDefaultPathTests` constants are unchanged and passed:

| Fixture | SHA-256 |
|---|---|
| Lab, 2,000 list-mode events including counters | `0057f59cd68691b61d179c499bff4f3f201832032ff01269d7c356398e57ceb1` |
| Co-60, 1,000 events including counters | `1bd61bb8a5175065b93745f7e721c8a783c5c4da6e823eaaed9bd175f20e8fa9` |
| Realism, 1,000 events including counters | `f66c6bd1e6caf1cc36ce6e864e59549d86cd568547f662ca88e941649dff0e48` |
| Crosstalk runner image | `5dbe7eea09ca409f3595811e4ccc8e7ed3d8e2a35f852e8146cca445e1ba083a` |
| Default configuration JSON | `d833711a64bf423f408d6321a29140e3cc2f25a7df12c3df89a4a0a5d1fbecd1` |

The scratch differential probe separately compiles the engine at 01592a5 and the current engine. It uses `ReadoutStudyTests.Tiny` with seeds 6100001 and 6100004 (the latter with 500 keV noise for explicit failure), removes only `*Seconds*` and `Microseconds*` timing fields exactly as that existing fixture does, and compares the resulting JSON bytes. Both hashes are `d4e6875014b043d0c022e03f7c6c0f857f75b32ae32fcb7eaa4750321daa3a8c`. Expectation: exact equality, tolerance zero. Current engine was recompiled after pulse-buffer retirement was added. Artifacts: `identity/baseline.json` and `identity/current.json` under task scratch.

Code-path proof for other existing studies: their source/entry points are unchanged. The changed original routines add only default-none cancellation checks and an optional null held-value observer; original batch arithmetic, trigger decisions and RNG draws remain unchanged. The persistent path is a new entry point. All original engine tests passed. The fixtures are empirical checks, not an exhaustive enumeration of every configuration/seed.

The realised stream is compared with original batch processing at observation partitions 8, 73 and 500 ns. Codes, hold times, dominant IDs, contributor counts and shares match exactly; expired buffers are empty after support ends. A hold pending at 32 ns survives pre-cancellation and completes identically once its horizon is observed. No statistical tolerance applies to these fixed inputs/RNG streams.

## Measurements, expectations and uncertainty

All timing observations are short busy-machine pilots, not acceptance thresholds or confidence intervals. No FPS, latency or physics tolerance was inferred from their best run.

### Preparation and acquisition

Fixed reference head, independent seeds optical 402, calibration transport 403, response 404, ADC 405, validation 406/407/408. Calibration budget is 144 × 2,000 = 288,000 scored histories at 661.7 keV. Validation budget is 144 × 200 = 28,800 scored histories; the per-crystal rows explicitly filter single-crystal histories and report assigned/total and wrong/assigned at all energies. They are not accuracy gates or live acceptance estimates.

Preparation produced 144 ordered peaks, 266,330 triggered outcomes with valid raw position, 91,037 calibration-window events and zero global-gain fallbacks. These are fixed-seed facts; the success requirement is a successful LUT with all 144 ordered peaks, not a borrowed acceptance band. Isolated service preparation took 8.5389731 s; a concurrent full-suite preparation took 13.5672194 s. The roughly 8.5–13.6 s spread illustrates load sensitivity. Preparation includes optical table, transport, LUT/gains and independent diagnostics. A cancellation requested at the transport-stage progress boundary returned no partial artifact; the earlier full-suite case spent 7.7599939 s building optics before that request, which is not cancellation latency.

The continuation fixture uses seed 4711, 500 µCi Cs-137 at 1 m and 2 s acquired live time. It produces 82 realised inputs and 68 assigned conversions, zero unknowns. Expectation is the uninterrupted reference, exactly: all measured records and scalar hits match after Stop/Continue and after changing inactive legacy gain to 50%/seed 888. Prior snapshot prefixes do not change. Assigned + unknown = triggered, All flood total = assigned, and Spectrum total = assigned exactly. These single-seed numbers are not estimates of physical efficiency.

The boundary fixture stores assigned energies 599, 600, 720 and 721 keV and an unknown record at 650 keV. Spectrum total and Imaging All are four; the inclusive 600–720 keV window is two in both workspaces; unknown is excluded. These counts are definitions, tolerance zero.

### 4 Hz short service probe

One prepared head; three repeats per activity, seed 4711, 20 s acquired time. A copied test virtual clock advances exactly 250 ms per refresh. Each run publishes 81 snapshots (initial state plus 80 ticks); only the final prefix is retained by the probe. Wall time includes transport, physical response, decoding, immutable publication, task scheduling and clock coordination, not only the pulse processor. It deliberately does not re-time a measured acquisition into a rate study.

| Activity at 1 m | Same result on all three repeats | Wall seconds | Mean decode milliseconds/snapshot | End-to-end µs/input hit |
|---|---|---|---|---|
| 500 µCi | 835 inputs; 687 assigned + 1 unknown = 688 conversions | 0.3237299, 0.2544169, 0.2595226 | 2.35783, 2.30459, 2.43520 | 387.700, 304.691, 310.806 |
| 5,000 µCi | 7,798 inputs; 6,547 assigned + 19 unknown = 6,566 conversions | 0.4735120, 0.2720673, 0.2653303 | 1.77960, 1.06945, 1.05092 | 60.722, 34.889, 34.025 |

The acquired input rates are 41.75 and 389.9/s, calculated as inputs/20 s. The higher activity is a pacing pilot, not a product preset. The per-hit cost includes fixed per-snapshot decode/scheduler work, hence its decrease with rate. Timing uncertainty is represented by the three observations; no extrapolation to an arbitrary count rate is justified. Identical counts on repeats are deterministic reproduction checks, tolerance zero.

### Retention and waveform bounds

Managed allocation probe: after preparation, allocate arrays of 100,000 scalar hits and measured records on one thread and subtract the measured 24-byte array headers. This x64 runtime allocates 40 bytes/scalar hit and 152 bytes/measured record including its array reference. Record object itself is 144 bytes; chunk array headers/reference directories add small overhead. At the 500 µCi pilot this payload is 835×40 + 688×152 = 137,976 bytes for 20 s, excluding caches, density, bounded truth and worker/view objects. The 5,000 µCi payload is 1,309,952 bytes. These are measured layout sizes and arithmetic, not cross-runtime promises. The engine releases expired pulse buffers; it no longer retains a second cumulative array history. Diagnostic truth is capped at the first 256 input hits. Live density is a fixed 256×256 double grid: 524,288 data bytes per detached copy. Calibration uses its own 384×384 grid (12×32), and density/labels are detached from mutable engine arrays.

Physical scope shares a cap of 1,000,000 sample slots across five lanes and the threshold trace, with 8 ns sampling. A clipped requested window is labelled. Prefix reuse/navigation and latest-request cancellation remain in the workspace view model. The scope sums retained realised channel charges with the acquired biexponential kernel, without new random draws. Sum samples equal A+B+C+D exactly in the same operation order.

Held analogue replay in the real 2 s fixture is tested at the held sample. Let u=2^-53, N=retained inputs, S=sum of absolute channel amplitudes. The bound is S[8u max(1,live seconds)×10^9(1/rise+1/tail)/normalization + gamma_(8N+20)], gamma_m=mu/(1-mu). The first held event has no older pulse at a support-cutoff discontinuity. The first term bounds absolute-time conversion/subtraction through the pulse derivative; the second covers multiply/add rounding. This derives a case-specific code-unit bound, not a physics-resolution tolerance.

| Lane | Observed absolute error, codes | Derived bound, codes |
|---|---:|---:|
| A | 8.469669410260394e-12 | 0.0017128947612874363 |
| B | 1.3415046851150692e-11 | 0.0013033130629916219 |
| C | 2.5579538487363607e-12 | 0.0018413920464779914 |
| D | 4.007461029686965e-12 | 0.0012524298180648648 |

## Detached renders and desktop handoff

Output is exclusively under `%TEMP%/gcam-todo40/turn2/renders/`. `ReadoutRenderTests` parses the production readout XAML with the usual theme dictionaries injected locally, without Application/Window/HWND construction. Its spots/records are explicitly synthetic drawing fixtures. The existing full-window renderer also passed.

New render list: `detector-Sipm`, `detector-Anger`, `detector-Calibration`, `detector-Diagnostics`, `four-lanes`, `ready`, `pending`, `preparing`, `failed`, each suffixed with dark/light and 1280x800/1440x900: 36 PNGs in `readout-render`. Structural assertions require five nonzero plot surfaces, populated series and a threshold series on the sum. Existing renders add four plot PNGs in `studio-plot` and 88 window PNGs in `studio-render`: 128 files total. No pixel-difference or timing tolerance was borrowed. Sensor footprint borders, LUT overlays and five-lane output were visually inspected. Standalone new readout renders are not a desktop survey of the assembled window; that remains a planner check.

Three desktop methods were added, not run:

| Method | Oracle / planner action |
|---|---|
| `UnsupportedGeometryAndField_ExplainRefusalWithoutZeroing` | Sharp cannot retain FourOutputAnger; Baseline can. Start with nonzero field stays Empty, explains zero-field requirement and preserves field. Legacy gain/chain controls are disabled. |
| `PreparationCancelRetryAndTabs_AreExplicit` | Cancel Prepare, retry successfully, visit four Detector tabs and inspect independent diagnostics. Bounded UI waits are operational deadlines, not physics tolerances. |
| `FixedSeedRepeatAndContinuation_ConserveCountsAndExposeFourLanes` | Seed 12345, Baseline, ambient zero, 4 s: completed uninterrupted and reset/stopped/continued acquisitions have exactly equal assigned/unknown/triggered counts. Stopped counts stay fixed; five scope lanes have nonzero bounds; replay/pile-up controls disabled. |

The same class's headless count oracle accepts 1,234+2=1,236 and rejects nonconservation/missing identity. The planner must obtain the author's free-desktop go before setting `GCAM_UI_TESTS=1`, then run `ReadoutScenarioTests`, verify minimum-size assembled layout and both themes, inspect failure bundles and perform the normal survey. Do not mark desktop requirements verified from the headless/render run. No desktop command is included in the executed-command list below.

## Proposed exact documentation wording — not applied

### SRS new rows

| ID | Proposed wording |
|---|---|
| SR-DET-05 | Detector shall offer DirectCrystal by default and the experimental GAGG FourOutputAnger preset for the 12×12 Baseline reference head only (rank 7, cell pitch 1 mm, mask–detector distance 60 mm, crystal pitch 1 mm). The resolved readout and preparation identity shall be captured at Start and locked while acquiring or while data exist. Unsupported geometry shall refuse the physical selection with an actionable reason without changing other inputs. IndependentSipm is outside this Studio implementation. |
| SR-DET-06 | Detector shall show active SiPM footprints and pitch-cell borders and an illustrative charge-division graph from the same resolved circuit used by the engine. It shall label virtual matched sensors, DPC ratio 0.01, virtual-ground outputs and omitted RC/skew/jitter. Legacy gain and chain controls shall be inactive and disabled in physical mode. |
| SR-DET-07 | Explicit Prepare shall build an independent calibration on a worker, expose state/progress/cancellation and publish density, ordered markers and LUT boundaries with their identity. Failure shall display the engine reason and prevent Start; no direct-assignment fallback is permitted. A bounded cache shall reuse only identical resolved inputs and versioned seed/budget policy. |
| SR-DET-08 | Smoothed calibration density, live raw X/Y counts and LUT-assigned crystal counts shall have distinct labels and denominators. Raw coordinates shall be dimensionless. Independent per-crystal diagnostics shall state their energy, all-energy/window selection, single-crystal truth definition, validation seeds and scored/assigned denominators; training density is not held-out acceptance evidence. |
| SR-DET-09 | Detached readout render fixtures shall cover pending, preparing, ready, failed and acquired displays, sensor/circuit views, flood/LUT overlays and four lanes plus sum in both themes at 1280×800 and 1440×900, without an HWND or desktop input. Desktop verification remains separately gated. |
| SR-WAVE-09 | Physical Waveform shall select a stable measured-event ID and show realised A(x−,y−), B(x+,y−), C(x−,y+), D(x+,y+) analogue pulses and their sum on one acquired time range, with stored trigger/hold instants and signed held ADC codes. The pulse sum shall use retained realised hits and the acquired engine kernel without redrawing fluctuations. |
| SR-WAVE-10 | The selected physical conversion shall show raw X/Y, LUT crystal or unknown, calibrated held energy when available, calibration identity and contributor/dominant-hit diagnostics from the stored record used by Spectrum and Imaging. The sum threshold shall state code-equivalent units and the 50 keV-equivalent preset. Continuous digitizer noise and integer-shaper equivalence shall not be implied. |
| SR-WAVE-11 | Physical scope generation shall run only while active, reuse covered held-selection output, bound the combined lane/threshold allocation, cancel or discard obsolete requests and preserve shared X navigation. Ideal/rate replay shall be unavailable for physical acquisition. |
| SR-RUN-30 | Physical response, overlap, trigger, hold, digitization, LUT assignment and calibrated energy shall be applied once before workspace processing. Immutable snapshots shall carry the measured prefix and calibration identity; workspace operations shall not reacquire or remeasure it. |
| SR-RUN-31 | Stop and Completed shall preserve readout RNG progression, transport look-ahead, pulse grid, active/queued inputs and trigger/hold/busy/re-arm state. A conversion shall be published only after its full hold search is observed. Continue shall be record-identical to uninterrupted acquisition at the same seed and final horizon, including an unfinished hold. |
| SR-RUN-32 | Readout transport/preparation shall run off the UI thread with cancellation. Acquisition snapshots shall use the existing nominal 4 Hz cadence and segment-boundary publications, show the consumed prefix and achieved speed under compute limits, and never invent future counts. View changes shall not repeat preparation or measurement. |
| SR-RUN-33 | Physical status counts/rate and Imaging All shall count valid LUT-assigned conversions; unknowns, all conversions and realised pre-trigger input hits shall be separately labelled. Spectrum shall histogram assigned conversions' stored calibrated energies with out-of-axis accounting. Explicit keV windows shall select the same inclusive record population in Spectrum and Imaging. Legacy gain/smearing, energy-only pile-up and stripping shall not be applied. |
| SR-RUN-34 | Physical Start shall refuse nonzero ambient/BSR and unavailable geometries before transport, preserve input values and explain the required correction. DirectCrystal shall retain its existing supported inputs and seeded stream. Engine defaults and microcell-saturation policy are unchanged. |

Proposed qualifiers to existing rows: prefix analytic-response/true-deposit provisions of SR-RUN-16/-21 and SR-WAVE-01/-02/-04/-06 with “For DirectCrystal acquisition”. Add “resolved readout and preparation identity” to SR-RUN-12's frozen inputs and SR-RUN-31 state to the continuation provisions. Preserve SR-RUN-28's counts/live-time definition and refer its physical population to SR-RUN-33. Spectrum/Imaging window and stripping descriptions shall say: “For DirectCrystal, the existing analytic-response windows and optional stripping apply. For physical readout, stored measured energy is selected by explicit inclusive keV bounds; legacy response and stripping are unavailable.” These branch qualifiers prevent contradictory universal requirements.

### SDS insertion

“Physical readout uses an explicit signal-only PhysicalListModeSource and the engine's persistent observed-horizon pulse stream. AcquisitionSession freezes the prepared experimental reference-head artifact, publishes immutable chunked measured/scalar-hit prefixes and retains at most 256 diagnostic truth samples. ReadoutPreparationService owns a serialized two-entry successful-artifact cache and independent calibration/validation seeds. PhysicalReadoutProjection and PhysicalReadoutWaveform operate on stored measured facts on workers; they do not invoke MeasurementStage. Detector/Spectrum/Imaging/Waveform remain views of one acquisition. The WPF ReadoutMapView draws the detached resolved circuit and density/LUT; readout views contain layout and InitializeComponent-only companions. Default direct factories and studies retain their previous path.”

### Traceability/VV.Studio insertion

“TODO-40 headless verification: SR-DET-05/-07 and SR-RUN-34 → ReadoutViewModelTests plus PhysicalReadoutTests refusals/preparation; SR-DET-08 and SR-RUN-30/-33 → PhysicalReadoutTests immutable/count/window tests; SR-RUN-31 → ReadoutStreamTests and physical continuation; SR-WAVE-09/-10/-11 → held replay/sum/disabled-study checks; SR-DET-06/-09 → ReadoutRenderTests plus the independently tested engine circuit. Desktop ReadoutScenarioTests are implemented but unexecuted pending the author's go. Normal Release suite: 881 passed, 32 skipped, zero failed; separate detached renders: two passed. Python: 42 passed, one opt-in skipped; calibration and catalog checkers passed. This is working-tree verification, not a committed milestone or desktop acceptance.”

New catalog entries currently carry no links to unapplied SRS IDs; the planner can add the above selectors after adopting the rows. No existing catalog source fingerprint was changed.

### AGENTS.Studio insertion

“Readout: DirectCrystal remains the default. Experimental FourOutputAnger requires the 12×12 Baseline head, ambient/BSR zero and explicit successful Prepare. Physical workspaces consume one immutable measured prefix; do not apply legacy MeasurementStage or energy-only pile-up to it. Preparation is independently seeded and cached by resolved detector inputs; view/source changes do not invalidate it. Keep Core WPF-free. New readout renders require GCAM_RENDER_OUTPUT under task scratch; desktop ReadoutScenarioTests remain opt-in on the author's go.”

### README insertion

“GCAM Studio includes an experimental four-output GAGG readout for the 12×12 Baseline reference head. Select Baseline, set ambient and BSR to zero explicitly, select FourOutputAnger and Prepare before Start. Detector shows the resolved SiPM/circuit and independent flood/LUT calibration; Waveform shows four realised lanes and their sum. Spectrum and Imaging use the same held/LUT-assigned records with explicit keV windows. Legacy gain/chain, stripping and ideal/rate replay are inactive for this mode. Other heads and physical readout with background remain separate tasks.”

## TODO-42 appendix — implementation-size estimate only

The work is more than removing the recording guard. Ambient already transports an incident Poisson process with a fixed horizon and pixel event callback; it can record pre-optical sites at the same scoring call. Its merge owns a pending signal record whose sites must remain paired through ambient interleaving. BSR transports a diffuse energy history and then **randomly replaces its pixel** in `ListModeBackground.Place`; simply attaching that history's original XYZ would disagree with the returned pixel. Legacy electronic dark arrivals have no gamma sites and should remain unsupported by this extension.

| Files / work | Approximate added/changed production lines |
|---|---:|
| `AmbientPhotonProcess.cs`: opt-in site callback and accepted-history envelope; clear misses; preserve all-face and masked coordinates | 40–65 |
| `AmbientAcquisition.cs`: merge site envelopes with signal/ambient look-ahead and observed horizon | 45–80 |
| `ListModeSource.cs`: explicit recording routes, origin/site ownership across signal, BSR and ambient | 60–100 |
| `ListModeBackground.cs`: physical BSR spatial contract and true site transport while preserving disabled legacy Place/RNG | 60–120 |
| `PhysicalListModeSource.cs`: horizon-aware producer integration, source-free field, provenance and cancellation | 60–100 |
| One Core/Simulation interaction envelope/producer contract | 20–40 |
| Studio `SimulationService`, `AcquisitionSession`, readout policy/messages: enable only supported field paths and capture field provenance | 40–75 |
| Total | approximately 325–580 production lines in about 8–10 files |

Tests: approximately 270–420 lines across 3–5 new/extended classes, roughly 10–14 cases: recording-off exact hashes for signal/ambient/BSR; immutable paired sites under merge/Stop/Continue; missed and empty intervals; no future conversion; source-free field; front-mask and all-face site bounds; measured energy/deposit conservation with a site-count-derived rounding bound; BSR count-ratio semantics; cancellation; unknown/assigned partitions; independent seeds; dark refusal. These are planning ranges, not measured counts or time promises.

Recommendation: preserve DirectCrystal's Place path and RNG exactly. Define physical BSR as a separately explicit diffuse detected-input transport contract whose sites and location come from the same transported history. Keep BSR as a pre-trigger detected-input ratio, not a post-trigger assigned-count promise. Alternatives are translating sites into the random legacy pixel (multisite/boundary risk) or introducing a new incident-fluence background model (larger scope). The author must settle this contract before implementation; no spatial truth should be fabricated to mimic a pixel label.

Main risks: look-ahead/site association, multisite deposits and ambient energy clamping by a few ulps, all-face coordinates/optical boundary assumptions, BSR spatial semantics and conditional retries, unchanged zero-field RNG progression, field-only sessions, and unsupported electronic dark triggers. A flag alone cannot address them.

Measurement cost: reference calibration remains reusable (the measured 8.5–13.6 s preparation pilot), not rebuilt per field dose. A timing pilot should measure incident transport and accepted readout separately; total work scales as N_incident×transport cost + N_detected×readout cost. Add at most one transient accepted site's envelope and one look-ahead per producer; keep existing compact retention/256 truth cap. Existing calibration uncertainty is shared by the run. New physical count/rate or efficiency acceptance needs its own seed ensemble and derived sampling/calibration bounds; do not reuse direct ambient calibration tolerances. Start with short fixed-horizon pilots for both geometries and BSR; long rate/dose evidence belongs to the planner after timing. No TODO-42 code or long numerical evidence run was performed.

## Executed commands that wrote files

Repeated invocations of the same form are grouped. `$taskDir` resolves to `%TEMP%/gcam-todo40/turn2/`; `$taskRoot` to `%TEMP%/gcam-todo40/`. Environment assignments below were process-local only. Read-only searches/file reads/git queries are not write commands.

1. `New-Item -ItemType Directory -Force <task scratch>` created the task/turn2 directories.
2. `apply_patch` wrote the repository files listed in the file table and the scratch scripts `engine_edits.py`, `studio_edits.py`, `views_edit.py`, `identity_probe.py`, `service_probe.py`, `catalog_add.py`, `final_source_check.py`. It also wrote this report. Existing protected plan/review files were not patched.
3. `python -B "$taskDir/engine_edits.py"`, `python -B "$taskDir/studio_edits.py"`, `python -B "$taskDir/views_edit.py"` edited the corresponding engine, service/model and view files. The view script also split scalar public types into their own files.
4. `python -B "$taskDir/identity_probe.py"` wrote baseline/current source copies, project and program under `identity/`; read-only `git show 01592a5:<engine file>` supplied original changed engine files. Repeated after retention changes.
5. `dotnet run --project "$taskDir/identity/baseline/Identity.csproj" -c Release -- <repo>/samples/scenario.json "$taskDir/identity/baseline.json"` and the equivalent `current` command compiled only task-scratch probe sources and wrote the comparison JSON. Current was rerun after the final engine change.
6. `python -B "$taskDir/service_probe.py"` wrote a task-scratch project/program and copied ManualTimeProvider; `dotnet run --project "$taskDir/service-probe/Probe.csproj" -c Release` wrote scratch build outputs and normal referenced-project build outputs.
7. `dotnet build Gcam.sln -c Release`, also with `--no-restore`, wrote normal bin/obj outputs (and restore cache when applicable). One intermediate compile error was corrected; the final build passed.
8. `dotnet test Gcam.sln -c Release --logger "trx;LogFilePrefix=<before|after|final|verified>" --results-directory "$taskDir"` wrote normal build outputs and scratch TRX files. Final verification is the `verified` prefix.
9. Targeted `dotnet test tests/Gcam.Tests -c Release --filter "FullyQualifiedName~Readout"`; filters `FullyQualifiedName~ReadoutDefaultPathTests|FullyQualifiedName~ReadoutStreamTests` and `FullyQualifiedName~ReadoutStreamTests`; `dotnet test tests/Gcam.Studio.Tests -c Release --filter FullyQualifiedName~ReadoutViewModelTests`; `dotnet test tests/Gcam.Studio.Services.Tests -c Release --filter FullyQualifiedName~PhysicalReadoutTests` wrote normal build outputs. Detailed/normal console logger variants were used to inspect results.
10. `python -B "$taskDir/catalog_add.py"` added only the five new-class entries/fingerprints/method lists and their generated discovery rows in `docs/VV.Tests.md`; on its second invocation it refreshed only those new entries after adding a test. `python -B samples/testing/test_records.py check-catalog --discover` verified them and invoked no-build list-test discovery, without editing the catalog.
11. With process-local `GCAM_RENDER_SNAPSHOTS=1`, `GCAM_RENDER_OUTPUT="$taskDir/renders"`, UI opt-in off: `dotnet test tests/Gcam.Studio.RenderTests -c Release --filter FullyQualifiedName~ReadoutRenderTests --logger "console;verbosity=normal"`, then `dotnet test tests/Gcam.Studio.RenderTests -c Release --no-build --logger "trx;LogFilePrefix=render" --results-directory "$taskDir"`. Final repetition used `dotnet test tests/Gcam.Studio.RenderTests -c Release --logger "trx;LogFilePrefix=render-final" --results-directory "$taskDir"`. These wrote normal build outputs, scratch PNGs/TRX.
12. `python -B -m unittest discover -s samples/evidence/tests -p "test_*.py"` used task-scratch TEMP/TMP for its temporary test fixtures. The opt-in git test was disabled. `python samples/evidence/calibration_record.py --check` checked records; it may write normal Python bytecode within the repository, not calibration/VV text.

13. `python -B "$taskDir/final_source_check.py"` checked changed source and permitted documentation for LF and absence of absolute user paths, plus InitializeComponent-only view companions. Its normalization list was empty: no repository file was rewritten by this check. `git --no-optional-locks diff --check` passed. Final SiPM, Anger and five-lane PNGs were visually inspected; sensor borders, LUT boundaries and the sum threshold are visible.

No cleanup/deletion, installation, upload, git state change or GUI command was issued. No started command remains running at handoff.

## Approval requests

None. Desktop execution requires the author's separate go as already specified; no irreversible command is requested by this implementation.

## Turn 3 — corrected scope fixture timing and shared X geometry

Status: both reported defects fixed; required verification passed. No engine or acquisition arithmetic changed and no tolerance was loosened.

### Causes and fixes

The old render fixture combined a realised hit at 0 s with an invented trigger at 900 ns and hold at 1,000 ns. Its pulse profile was also a drawing-only 20/100 ns profile. `PhysicalReadoutWaveform` correctly plotted these inconsistent facts: it subtracts the selected hold from both hit traces and marker timestamps. The selected-record and ViewModel paths did not introduce an offset. The fixture now obtains the profile, trigger, hold, signed codes and held analogue values from an actual `ReadoutPulseProcessor` observed stream. It uses a single realised hit at 1,000 ns with charges 1,000/500/800/400 codes and a 400-code sum threshold; the drawing density remains explicitly synthetic. Fixture trigger is 1,016 ns, hold 1,160 ns: on the hold-relative axis they are −0.144 µs and 0. The arrival is −0.160 µs. No future-input flush is used.

Each `PlotView` previously measured its own Y tick labels and therefore chose a different left plot edge. The readout XAML now supplies `AxisLeftGutter=80` to all five plots. This fixed DIP gutter accommodates the grouped code labels and does not vary with lane amplitude; other plots retain the automatic gutter by default. The lanes already shared their data range; now their plot widths and X transforms also coincide. `PlotAreaBounds` and `XToScreen` expose the actual rendered geometry/mapping used by ticks, markers and navigation for layout verification.

Changed files: `src/Gcam.Studio/Controls/PlotView.cs`, `src/Gcam.Studio/Views/ReadoutWaveformView.xaml`, `tests/Gcam.Studio.Services.Tests/PhysicalReadoutTests.cs`, `tests/Gcam.Studio.RenderTests/ReadoutRenderTests.cs`, the two previously added readout catalog entries/discovery rows in `docs/VV.Tests.md`, and this appended report. The existing desktop scenario checks surface presence and count conservation, not marker timing, so its oracle needed no change; it remains unrun.

### Regression tests and derived bounds

New headless case `IsolatedScope_TriggerFirstCrossingAndHoldPeakShareThePulseTimeBase` runs an isolated sum-triggered pulse through the actual engine and Services replay. It independently brackets the continuous rising-edge threshold root using the analytic bi-exponential profile, asserts the trigger sample exceeds threshold and its predecessor does not, and constrains the trigger to the root bracket plus one engine period. It checks that hold is inside the configured search window and within one sample period of the analytic peak. It then checks replay's first sampled threshold crossing and maximum against the published markers. The two sample grids can differ in phase: each first sampled crossing lies between the same continuous crossing and one period afterwards, so their separation is bounded by the larger period. The peak's adjacent candidate samples similarly straddle the continuous maximum. No physics or timing tolerance from another scenario applies.

Observed headless values: continuous crossing bracket [5.705037863597647, 5.7050378635976475] ns after arrival; trigger 8 ns; hold 160 ns; analytic peak 158.83967901456091 ns. The engine period and scope period are both 8 ns, so the comparison bound is 8 ns (0.008 µs); the search window is 160 ns and trigger-to-hold is 152 ns. These assertions passed with the derived bounds unchanged.

The render test independently asserts the fixture's first sum crossing/peak-to-marker bounds and hold window before drawing. The previous invented times would fail this assertion. After rendering the production XAML, it checks exactly equal plot left edges, widths, view ranges and root-relative `XToScreen(0)` for every lane. Equality has no pixel tolerance: identical layout inputs and transform calculations must produce identical doubles. Both themes at 1280×800 and 1440×900 passed. Zero-time coordinates were 318.1011475847344 DIP and 350.1267680811315 DIP respectively in every lane; left plot edge was exactly 80 DIP.

### Verification and regenerated files

Release solution build: zero errors, zero warnings. Full English-CLI solution tests, with process-local task-scratch TEMP/TMP and UI/render/evidence opt-ins disabled: 882 passed, 32 skipped, zero failed (previously 881/32). Project counts: engine 538/0, Core 214/0, Services 114/7, UI 16/23, renders 0/2. Separate readout render test: one passed, zero failed/skipped. Python unittest discovery: 42 passed, one opt-in git test skipped. Catalog discovery: 166 units, zero incomplete descriptions. The catalog refresh updated only the entries introduced for readout in turn 2. No desktop test or GUI was run.

Output directory: `%TEMP%/gcam-todo40/turn3/renders/readout-render/`. All 36 files were regenerated from the final test source:

```text
detector-Anger-dark-1280x800.png
detector-Anger-dark-1440x900.png
detector-Anger-light-1280x800.png
detector-Anger-light-1440x900.png
detector-Calibration-dark-1280x800.png
detector-Calibration-dark-1440x900.png
detector-Calibration-light-1280x800.png
detector-Calibration-light-1440x900.png
detector-Diagnostics-dark-1280x800.png
detector-Diagnostics-dark-1440x900.png
detector-Diagnostics-light-1280x800.png
detector-Diagnostics-light-1440x900.png
detector-Sipm-dark-1280x800.png
detector-Sipm-dark-1440x900.png
detector-Sipm-light-1280x800.png
detector-Sipm-light-1440x900.png
failed-dark-1280x800.png
failed-dark-1440x900.png
failed-light-1280x800.png
failed-light-1440x900.png
four-lanes-dark-1280x800.png
four-lanes-dark-1440x900.png
four-lanes-light-1280x800.png
four-lanes-light-1440x900.png
pending-dark-1280x800.png
pending-dark-1440x900.png
pending-light-1280x800.png
pending-light-1440x900.png
preparing-dark-1280x800.png
preparing-dark-1440x900.png
preparing-light-1280x800.png
preparing-light-1440x900.png
ready-dark-1280x800.png
ready-dark-1440x900.png
ready-light-1280x800.png
ready-light-1440x900.png
```

The regenerated dark 1440×900 five-lane image was visually inspected: Trigger is on the rising pulse, Hold at its peak, and all five zero ticks/marker lines align. TRX prefixes are `verified` and `readout-final` under the turn3 scratch directory.

### Every turn 3 command that wrote files

`$taskRoot` below denotes `%TEMP%/gcam-todo40/` before process-local reassignment; `$taskDir` denotes its `turn3` subdirectory. No persistent environment setting was made.

1. `apply_patch`: patched the six repository files listed above and wrote scratch `turn3/edits.py`.
2. `New-Item -ItemType Directory -Force "$taskRoot/turn3"`: created scratch only.
3. `python -B "$taskDir/edits.py"`: wrote the readout XAML gutter and render fixture/layout assertions, with LF endings.
4. `dotnet test tests/Gcam.Studio.Services.Tests -c Release --filter FullyQualifiedName~IsolatedScope --logger "console;verbosity=normal"`: compiled and ran the new headless case; normal bin/obj/restore outputs only.
5. `dotnet test tests/Gcam.Studio.RenderTests -c Release --filter FullyQualifiedName~ReadoutRenderTests --logger "trx;LogFilePrefix=readout" --results-directory "$taskDir"`, repeated with `readout-final` after the final render-fixture assertions: normal build outputs and scratch PNG/TRX files. Process-local `GCAM_RENDER_SNAPSHOTS=1`, `GCAM_RENDER_OUTPUT="$taskDir/renders"`, `GCAM_UI_TESTS=0`, English CLI and task-scratch TEMP/TMP.
6. `dotnet build Gcam.sln -c Release`: normal build/restore outputs; process-local English CLI and task-scratch TEMP/TMP.
7. `dotnet test Gcam.sln -c Release --logger "trx;LogFilePrefix=verified" --results-directory "$taskDir"`: normal build outputs and scratch TRX; process-local English CLI and task-scratch TEMP/TMP, UI/render/evidence flags all `0`.
8. `python -B "$taskRoot/turn2/catalog_add.py"`: refreshed the turn2 readout entries' source fingerprints/method lists and generated discovery rows; no class entry added and all other fingerprints preserved. English CLI and UI/render flags `0`, task-scratch TEMP/TMP.
9. `python -B -m unittest discover -s samples/evidence/tests -p "test_*.py"`: temporary fixtures under task-scratch TEMP/TMP; `GCAM_PROVENANCE_GIT_TESTS=0`.
10. `python -B samples/testing/test_records.py check-catalog --discover`: no catalog edit; no-build discovery, task-scratch TEMP/TMP and UI/render flags `0`.
11. `python -B "$taskRoot/turn2/final_source_check.py"`: LF/path/view-companion check; normalization list empty, no repository rewrite. `git --no-optional-locks diff --check` passed.

No cleanup/deletion, git state change, installation, process stop, upload or desktop command was issued. All started commands completed.

### APPROVAL REQUESTS

None.

## Turn 4 — rejected selections now restore the bound controls

Status: implemented and verified offscreen/headlessly; no desktop execution in this turn. The planner's reported failing desktop scenario remains for the planner to rerun on the author's go.

### Cause and fix

`ReadoutMode` refused an unsupported selection and synchronously raised PropertyChanged while WPF's Selector was still updating its source. The accepted VM value remained DirectCrystal, but the selection transaction could leave the control's selected index/display on FourOutputAnger. Reading only the VM missed the discrepancy. A direct SetCurrentValue test also failed to exercise the stale-index path; driving SelectedIndex exposed it.

Rejected readout-mode notifications are now posted through the current UI SynchronizationContext, so the control re-reads the current accepted source after the update finishes. Without a UI context, notification remains synchronous for headless callers. The callback reads the current value rather than capturing an old value, so it does not overwrite a later accepted edit. Core stays WPF-free; no view code-behind or ancestor binding was added. Rejected keV window edits use the same deferred refresh for both bounds.

The other selector check found the same stale selected-index behavior when physical readout rejects Wide FOV. Merely repeating a SelectedPreset notification did not restore its index: the accepted preset is a reference object already equal to the target item. The optics combo now binds two-way to the editor's explicit SelectedPresetIndex. The editor checks the shell's physical-head predicate before changing a preset, preserves the accepted value on rejection, and posts a refresh of the accepted index. Existing SelectedPreset callers remain supported. MainWindow's ordinary DataContext binding is used; no ancestor lookup. The shell's direct Optics guard still protects programmatic changes.

Files: `src/Gcam.Studio.Core/ViewModels/MainViewModel.Readout.cs`, `MainViewModel.cs`, `OpticsEditorViewModel.cs`; `src/Gcam.Studio/Views/MainWindow.xaml`; `tests/Gcam.Studio.RenderTests/ReadoutRenderTests.cs`; formatting only in `tests/Gcam.Studio.UiTests/ReadoutScenarioTests.cs`; the previously added readout catalog entries/discovery rows in `docs/VV.Tests.md`; this appendix. No engine or Services acquisition math changed.

### Regression test and other refusals checked

`BoundReadoutControls_RejectedEditsShowAcceptedSourceValues` is a normal ungated Fact in the existing render class. It runs detached controls on an STA with DispatcherSynchronizationContext and drains the dispatcher after each edit. It parses the production ReadoutPanel, sets the combo's selected index, and checks the actual SelectedItem, SelectedIndex and Text after unsupported geometry. It then checks a valid FourOutputAnger selection succeeds, a locked attempt restores FourOutputAnger, and invalid low/high keV text boxes display 600/720 after rejection. An optics combo reproduces MainWindow's editor bindings and checks a rejected Wide FOV returns to Baseline in both SelectedItem and SelectedIndex. No Application, Window, popup, HWND or desktop input is created. Assertions are exact control/source identities, with no timing or pixel tolerance.

The SelectedIndex-driven pre-fix sequence failed when a subsequent valid selection still displayed DirectCrystal; the old immediate notification had left the first rejected transaction inconsistent. The optics check independently failed with selected index 3 instead of accepted index 2. Both failures were corrected; the final case additionally checks the first rejection's text/index directly. These checks would detect the class of defect that VM-only tests missed.

Other places checked: Detector's four tabs accept selections without a rejection guard. Ideal/rate replay, stripping, pile-up and legacy chain/gain controls are disabled for physical acquisition. Ambient/BSR and missing preparation refuse Start without replacing a selected value, so they do not have this Selector update pattern. The keV-window and optics-preset refusals were exercised and corrected as described above. The desktop scenario oracles were preserved.

The scenario file now uses separate statements, normal comma/operator spacing and surrounding scenario brace layout. Formatting preserved non-whitespace tokens and string literals; no scenario values, deadlines, actions or assertions changed.

### Verification

Final Release solution build: zero warnings, zero errors. An earlier rebuild reported the same two pre-existing Services xUnit analyzer warnings noted in turn 2. Full English-CLI solution tests with process-local scratch TEMP/TMP and UI/render/evidence opt-ins disabled: **883 passed, 32 skipped, zero failed**, versus turn 3's 882/32. Engine 538/0, Core 214/0, Services 114/7, UI 16/23, renders 1/2. The increase is the ungated detached binding regression. Separate readout render-class run: two passed, zero skipped/failed; 36 snapshots written under `%TEMP%/gcam-todo40/turn4/renders/readout-render/` with the same names listed in turn 3. Catalog discovery: 166 units, zero incomplete descriptions. LF/path/view-companion and `git diff --check` checks passed. No desktop tests were run.

### Every turn 4 command that wrote files

`$taskRoot` denotes `%TEMP%/gcam-todo40/` before reassignment and `$taskDir` its `turn4` child. All environment settings were process-local.

1. `apply_patch` edited the repository files listed above and wrote scratch scripts `turn4/reformat.py` and `turn4/format_finish.py`.
2. `New-Item -ItemType Directory -Force "$taskRoot/turn4"` created scratch only.
3. `python -B "$taskDir/reformat.py"` and `python -B "$taskDir/format_finish.py"` wrote whitespace-only scenario formatting; the latter checked token/literal identity.
4. Repeated `dotnet test tests/Gcam.Studio.RenderTests -c Release --filter FullyQualifiedName~BoundReadoutControls`: console logger variant and TRX variants `--logger "trx;LogFilePrefix=<prefix>" --results-directory "$taskDir"`, prefixes `before-fix`, `before-index`, `after-mode`, `bound-final`, `bound-fixed`, `bound-all-fixed`, `bound-layout`, `bound-complete`. Normal build outputs and scratch TRX only. Initial direct-value probe passed but did not expose stale index; the index-driven and expanded optics probes exposed the defects. The final targeted case passed.
5. `dotnet build Gcam.sln -c Release`, twice: normal build/restore outputs. Final build was 0/0.
6. `dotnet test Gcam.sln -c Release --logger "trx;LogFilePrefix=verified" --results-directory "$taskDir"`: normal build outputs and scratch TRX; English CLI, TEMP/TMP under scratch, `GCAM_UI_TESTS`, `GCAM_RENDER_SNAPSHOTS`, `GCAM_EVIDENCE_TESTS` all `0`.
7. `dotnet test tests/Gcam.Studio.RenderTests -c Release --filter FullyQualifiedName~ReadoutRenderTests --logger "trx;LogFilePrefix=readout" --results-directory "$taskDir"`: normal build outputs, scratch TRX and PNGs; `GCAM_RENDER_SNAPSHOTS=1`, `GCAM_RENDER_OUTPUT="$taskDir/renders"`, `GCAM_UI_TESTS=0`, English CLI and scratch TEMP/TMP.
8. `python -B "$taskRoot/turn2/catalog_add.py"`: refreshed only the turn2 readout entries/fingerprints/method lists and discovery rows after test additions/formatting; no new class entry. Other source fingerprints were preserved. UI/render flags `0`, English CLI and scratch TEMP/TMP.
9. `python -B samples/testing/test_records.py check-catalog --discover`: no catalog edit; no-build discovery with UI/render flags `0`, English CLI and scratch TEMP/TMP.
10. `python -B "$taskRoot/turn2/final_source_check.py"`: LF/path/InitializeComponent-only check; normalization list empty, no repository rewrite. Read-only `git --no-optional-locks diff --check` passed.

No git state change, deletion, installation, process stop, upload or desktop command was issued. No started command remains running.

### APPROVAL REQUESTS

None.

## Turn 5 — broken-verdict support for readout desktop scenarios

Added the existing `Broken` environment-variable pattern to `ReadoutScenarioTests`. Each desktop scenario corrupts its own decisive expectation without changing actions, waits or inputs:

- `UnsupportedGeometryAndField_ExplainRefusalWithoutZeroing`: expects FourOutputAnger instead of the accepted DirectCrystal; must fail at the selected-item `Assert.Equal`.
- `PreparationCancelRetryAndTabs_AreExplicit`: expects diagnostics presence to be false instead of true; must fail at `Assert.Equal(!Broken, ui.Rows("Readout.Diagnostics").Any())` after the unchanged preparation/tab steps.
- `FixedSeedRepeatAndContinuation_ConserveCountsAndExposeFourLanes`: adds one to the expected assigned count, preserving the other tuple components; must fail at the uninterrupted-versus-continued tuple `Assert.Equal`. The corrupted expectation is not passed to the count parser, so it cannot cause a parser/harness error.

CountOracle is unchanged and does not read Broken. No desktop tests were executed; the planner must confirm three assertion failures in a broken desktop run and a passing recovery run.

Verification: Release build 0 warnings/0 errors; full English-CLI solution tests **883 passed, 32 skipped, zero failed**, unchanged counts; catalog discovery 166 units/zero incomplete descriptions. Additional headless CountOracle run with `GCAM_UI_BREAK_VERDICT=1`, `GCAM_UI_TESTS=0`: one passed. LF/path and whitespace checks passed. Product source was not edited.

Writing commands (all environment changes process-local; `$taskRoot` = original `%TEMP%/gcam-todo40/`, `$taskDir` = its `turn5` child):

1. `apply_patch`: scenario file and this appendix. `New-Item -ItemType Directory -Force "$taskDir"`: scratch directory.
2. `dotnet build Gcam.sln -c Release`: normal build/restore outputs, English CLI and scratch TEMP/TMP, UI flag `0`.
3. `dotnet test Gcam.sln -c Release --logger "trx;LogFilePrefix=verified" --results-directory "$taskDir"`: normal build outputs and scratch TRX; English CLI and scratch TEMP/TMP, UI/render/evidence/broken flags `0`.
4. `python -B "$taskRoot/turn2/catalog_add.py"`: refreshed the readout scenario source fingerprint in its previously added catalog entry/discovery record; no new entry or other source-fingerprint change. UI/render flags `0`, English CLI and scratch TEMP/TMP.
5. `python -B samples/testing/test_records.py check-catalog --discover`: no catalog edit, no-build discovery; UI/render flags `0`, English CLI and scratch TEMP/TMP.
6. `dotnet test tests/Gcam.Studio.UiTests -c Release --no-build --filter FullyQualifiedName~CountOracle_RefusesNonConservationAndMissingIdentity`: headless only, UI flag `0`, broken flag `1`, English CLI and scratch TEMP/TMP; normal test-run outputs.
7. `python -B "$taskRoot/turn2/final_source_check.py"`: no normalization/rewrite needed. Read-only `git --no-optional-locks diff --check` passed.

Command-log issue: the catalog refresh and check overlapped after their shell calls yielded; the one-background-command limit was not maintained for those calls. Both completed successfully. No deletion, git state change, installation, upload, process stop or desktop command was issued; no command remains running.

APPROVAL REQUESTS: None.
