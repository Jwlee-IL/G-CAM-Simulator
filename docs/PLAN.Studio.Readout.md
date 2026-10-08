# PLAN.Studio.Readout — the physical four-output readout in GCAM Studio (TODO-40, stage 2 of TODO-19)

Scope: TODO-19 stage 1 adds a physical readout to the engine (`Detector.Readout`: DirectCrystal, FourOutputAnger,
IndependentSipm; pre-optical interaction records; light spread, SiPM, charge-division network, four outputs, a
time-domain four-channel pulse and pile-up model, trigger, flood-map LUT). This plan brings it into GCAM Studio so a
user can see the readout: the SiPM grid and charge division, the continuous Anger flood with its LUT, the four channel
waveforms, and the effect of the readout on Spectrum and Imaging. Studio stays the simulator's engineering viewer (D-34).

Status: **decided, 2026-10-08** — review by Codex (session `01a1196d-8612-7691-985f-647bf1bb3ca7`, [review](PLAN.Studio.Readout.Review.md)); decisions SD-1 … SD-6 below; implementation next (TODO-19 is merged). The engine part is not yet committed: it is in the
worktree `C:\gw\w19b` (branch `todo19-readout`, TODO-19 turns 2–5, docs `PLAN.Physics.RigReadout.md` RD-1 … RD-14 and
`PLAN.Physics.RigReadout.Turn2.md`); implementation of this plan starts only after that is merged.

## What exists (to verify in the code)

| Item | Where | Fact (verify) |
|---|---|---|
| Detector workspace | `src/Gcam.Studio.Core/ViewModels/DetectorWorkspaceViewModel.cs`, views | crystal face with per-pixel gain and reflector gaps (SR-DET-01 … 04); no SiPM grid, no flood map with LUT |
| Waveform workspace | `WaveformWorkspaceViewModel.cs`, `src/Gcam.Studio.Services/WaveformService.cs` | the summed energy channel only; the UI states "four position channels are not modelled" (SR-WAVE-01 … 08) |
| Acquisition | `SimulationService`, `ImagingService`, list-mode snapshots at 4 Hz | position = the engine's direct crystal assignment |
| Engine readout (worktree) | `C:\gw\w19b`: `src/Gcam.Configuration/Readout/`, `src/Gcam.Detector/Readout/`, `src/Gcam.Simulation/Readout/`, `ListModeSource` interaction records | API, cost per event (turn 2: 15–33 µs/event for the four-output path), what is refused on non-study paths today |
| Readout drawings | `C:\gw\w19b`: `docs/HW.Readout.md`, `samples/readout/` (schematic SVG, SPICE netlist, independent solver) | generated from the readout configuration |

## Proposed decisions (verify / improve)

| ID | Decision |
|---|---|
| SR-1 | **Readout selection in Studio:** a readout selector (DirectCrystal default; FourOutputAnger presets incl. the TODO-19 preset; IndependentSipm as a comparison) captured with the acquisition like other detector settings; changing it is a new acquisition, never a silent reinterpretation |
| SR-2 | **Detector workspace:** SiPM grid over the crystal face (active vs package area), the charge-division network (generated like HW.Readout), the continuous Anger calibration flood distinct from the crystal-count image, LUT boundaries / markers, calibration state (success / explicit failure), per-crystal acceptance and confusion diagnostics |
| SR-3 | **Waveform workspace:** A / B / C / D traces on a common time axis, threshold lines, trigger and hold markers, the summed energy, raw X / Y and LUT crystal ID for the selected event; pile-up visible as summed pulses in all four channels; the selected event is the same measured record Spectrum and Imaging use |
| SR-4 | **Spectrum and Imaging consistency:** with a physical readout, Imaging and Spectrum use the LUT crystal IDs and trigger / hold results of the same measured records, including pile-up mispositioning; direct vs physical comparison only as an explicit study view |
| SR-5 | **Performance:** the four-output path at the 4 Hz snapshot rate on Studio's worker (verify cost per event at Studio's count rates); calibration flood built once per settings change; cancellation |
| SR-6 | **Layering:** Core stays WPF-free; Services own the engine adapters; views InitializeComponent-only; reuse plot / heatmap contracts and tokens (DESIGN.*) |
| SR-7 | **Requirements and tests:** new SR rows (SR-DET-05…, SR-WAVE-09…, SR-RUN-…), headless tests and offscreen renders; desktop scenarios added and run last on the author's go |
| SR-8 | **Not here:** changing the engine's default readout (stays DirectCrystal per RD-14); microcell saturation beyond the engine's option |

## Decisions after review (2026-10-08)

The review corrected the plan: the engine supplies study components, not a live readout path — Studio's list-mode
source refuses a physical readout, interaction recording refuses ambient / background scenes, and the batch pulse
processor needs persistent state and observed-horizon semantics for Stop / Continue; Spectrum and Imaging must consume
the stored measured records without applying the legacy gain, smearing or pile-up again. Measured (short, busy-machine
pilots): the 12 × 12 reference head prepares in 9.2 s and processes 8–10 µs per input hit; Studio's default 30 × 30
geometry takes 45 s to build and 34–37 s to calibrate, and its LUT calibration failed on every fixed-seed repeat;
retained interactions cost ≈ 100 bytes per history.

| ID | Decision | Source |
|---|---|---|
| SD-1 | **Live integration with an engine prerequisite:** one immutable measured-record path shared by Spectrum, Imaging, Waveform and Detector; the engine first gains a stateful pulse processor (observed horizon, holds that survive Stop / Continue) and a list-mode path for the physical readout | **author** (recommended) |
| SD-2 | **Geometry: the 12 × 12 reference head only**, with the TODO-19 preset (four outputs, DPC 0.01, sum trigger 50 keV, specular, 1 mm), labelled experimental; other geometries cannot select a physical readout. Support for every Studio preset (including the 30 × 30 calibration failure) is a separate TODO (TODO-41) | **author** |
| SD-3 | **Ambient / background:** a physical readout starts only with ambient and BSR at zero, and the screen says why (no silent zeroing). Extending interaction recording to the ambient / background transport is a separate TODO (TODO-42) whose **first step is an implementation-size estimate** | **author** |
| SD-4 | **Adopted from the review's recommendations:** a fixed labelled GAGG physical preset with the inactive legacy gain / chain controls disabled in physical mode; an explicit Prepare step with a bounded content cache; compact retention (realised hits + measured records, a bounded truth sample); explicit keV windows with stripping disabled for the physical readout; four labelled lanes plus the sum sharing the time axis; measured-only scope (no replay studies yet); counts are assigned triggered events, unknowns separate, unfinished holds wait for the observed horizon | review; planner |
| SD-5 | **Requirements:** the review's SR-DET-05 … 09, SR-WAVE-09 … 11, SR-RUN-30 … 34 wording as the starting point, applied by the planner after implementation; headless tests, detached renders; desktop scenarios added, run on the author's go | review; planner |
| SD-6 | **Not here:** the engine default readout (stays DirectCrystal, D-57); microcell saturation (LIM-11) | planner |

## Steps

1. Review turn (no code): verify the rows against Studio and the w19b engine; propose the design (view models, services,
   screens with ASCII mock-ups), SR wording, tests, performance measurements (a short headless probe in %TEMP% is fine),
   and the author's questions → `docs/PLAN.Studio.Readout.Review.md` (in `C:\gw\w40`).
2. Decisions after review; after TODO-19's merge, implementation in the same conversation; headless verification;
   desktop UI scenarios on the author's go.
