# PLAN.Studio.Readout — the physical four-output readout in GCAM Studio (TODO-40, stage 2 of TODO-19)

Scope: TODO-19 stage 1 adds a physical readout to the engine (`Detector.Readout`: DirectCrystal, FourOutputAnger,
IndependentSipm; pre-optical interaction records; light spread, SiPM, charge-division network, four outputs, a
time-domain four-channel pulse and pile-up model, trigger, flood-map LUT). This plan brings it into GCAM Studio so a
user can see the readout: the SiPM grid and charge division, the continuous Anger flood with its LUT, the four channel
waveforms, and the effect of the readout on Spectrum and Imaging. Studio stays the simulator's engineering viewer (D-34).

Status: **reference plan, 2026-10-08** — review turn (Codex) next. The engine part is not yet committed: it is in the
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

## Steps

1. Review turn (no code): verify the rows against Studio and the w19b engine; propose the design (view models, services,
   screens with ASCII mock-ups), SR wording, tests, performance measurements (a short headless probe in %TEMP% is fine),
   and the author's questions → `docs/PLAN.Studio.Readout.Review.md` (in `C:\gw\w40`).
2. Decisions after review; after TODO-19's merge, implementation in the same conversation; headless verification;
   desktop UI scenarios on the author's go.
