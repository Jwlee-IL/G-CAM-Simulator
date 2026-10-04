# PLAN.Studio.Detector — the Detector workspace, depth (3D) and the rangefinder (TODO-11)

Scope: moving the last `Gcam.Wpf`-only features into GCAM Studio — the Detector tab (reflector gap, optical crosstalk,
SiPM pitch, detector-face view) and the Imaging depth controls ("Estimate depth (3D)", rangefinder range, focus
fusion). Order of work: [PLAN.Studio.Migration](PLAN.Studio.Migration.md); procedure:
[AGENTS.Planning](../AGENTS.Planning.md).

Status: **done** 2026-10-02 (D-1 … D-8 implemented; renders `docs/assets/studio-render/{detector-*,focus-*}`).
Implemented by Codex (core, services, views, tests, docs; build and normal suites green) until its credits ran out;
the **substitute implementer** (a Claude subagent, [AGENTS.Planning](../AGENTS.Planning.md)) ran the evidence test
(gap ratio 0.6943 / 0.4555 vs 0.6944 / 0.4444, tolerance 4·s·√(1+1/8) from the measured spread, pass) and the renders,
and fixed what the renders showed: overlapping plot marker labels (now laid out like band labels), the chain combo
boxes' record text (ComboBox template ignored `DisplayMemberPath`), clipped empty-state text, the gap summary in µm,
a log-scale plane axis for the focus curve (`PlotViewport.LogX`, `NiceTicks.LogarithmicAxis`), self-consistent
synthetic focus fixtures. Planner re-verified: build 0 / 0, tests 259 / 151 / 67 (+7) / 10 (+9), renders reviewed.

## What exists (checked in the code, 2026-10-02)

| Item | Where | Fact |
|---|---|---|
| Reflector gap (engine) | `ComptonCrystalDetector` (`InReflectorGap`, ctor) via `Detector.ReflectorGapMm` | a photon entering the gap is lost; also respected on scatter re-entry; passed by `ListModeSource`, `EventStreamStudy`, `ComptonFactory`, `DoseStudy` |
| Reflector gap (Studio) | `DetectorSettings.ReflectorGapMm` = 0.1 mm; `MainWindow.xaml` Detector panel | **read-only** (shown as a fact); `MainViewModel.Detector` never sets it, so it is always 0.1; validated against the pixel pitch in `OpticsPolicy.Validate` and `SimulationService` |
| Optical crosstalk (engine) | `ComptonCrystalDetector` (`_crosstalk`, `ApplyOpticalCrosstalk`) via `Detector.OpticalCrosstalkFraction` | effective = contact × exp(−gap / 40 µm); per-event leak uniform in [0, 2·effective], ¼ to each of 4 neighbours, edge light lost; `ListModeSource` passes it |
| Optical crosstalk (Studio) | — | **absent**: `DetectorSettings` has no field, so Studio always runs at 0 |
| SiPM pitch (Wpf only) | `MainWindow.xaml.cs` `BlockifyFlood`, `PrepareLive` | readout pitch / crystal pitch rounded to a block; each block of the **mean flood** is averaged before decoding. Findings theme 35 calls it "the no-Anger worst case"; not an engine model |
| Detector face (Wpf) | `RenderDetector`, `DrawDetector` | 12 sub-cells per crystal, per-crystal gain from `CrystalUniformity(dcfg).Sensitivity`, gaps as NaN, SiPM grid lines; readout: fill factor ((p−g)/p)², active / pitch, crosstalk contact → effective, SiPM block, gain σ / seed. Static: needs no acquisition |
| Depth (3D) (Wpf) | `EstimateDepth_Click` → `MixedFieldStudy.LocalizeDepths(flood, cfg, factory, k, zMin = D+30, zMax = 3000, steps = 30)` | refocuses the accumulated **broadband** flood at 30 planes; metric = peak prominence (peak − mean) / std per plane; greedy track linking; K = scene source count (uses the truth) |
| Rangefinder (Wpf) | `OptFocal` slider 200–3000 mm, step 50 | it **is** the decoder focal plane (`Refocus` rebuilds the decoder on the same flood) |
| Focus fusion (Wpf) | `LiveTimer_Tick` every 8 ticks → `MixedFieldStudy.CheckFocus(…, laser·0.4 … laser·2.5, 14)` | if best SNR > 1.2 × laser SNR and \|Δ\| > 150 mm: refine the range when the focus-curve FWHM < 250 mm, else only warn. **The thresholds 1.2 / 150 / 250 are not derived anywhere** |
| Studio focus | `ImagingWorkspaceViewModel.FocalPlane`, `ImagingProjection.AtFocus`, `OpticsPolicy.ValidateFocus` | decoder focus is a view setting that re-projects All and every channel from retained data (TODO-09); grid cap 128 per side; Studio decodes **non-cyclic** (`SceneConfigBuilder`) |
| Depth evidence | Findings 18, 19, 24, 34 | measured near field of the reference lab geometry (S 40–200 mm): near accurate, far degrades; peak prominence is a **heuristic biased +7 mm near, −18 mm far** (theme 24); a seeded calibrated refine reaches ~1 % of the distance. **Nothing is measured for Studio's default geometry** (rank 13, 0.7 mm, D 80, sources at ~1 m) |
| Crosstalk evidence | Findings 35 | at 40 % contact, windowed efficiency 9 / 13 / 20 / 69 / 45 % at gap 0 / 20 / 40 / 100 / 200 µm (reference lab geometry) |
| Reference readout | CLAUDE.md, PLAN.Studio.Waveform | SiPM matched **1:1** to the crystals with dead regions between; four 14-bit ADCs, Anger-type positioning (TODO-19, not modelled) |

## Proposed design (reference — the implementer may improve any of it)

| # | Proposal | Status |
|---|---|---|
| T-1 | **Reflector gap editable** (µm) as a detector input: marks the acquisition stale, disabled while acquiring, carried by the snapshot (like gain σ, decision I-2). | proposed |
| T-2 | **Contact crosstalk** (%) added to `DetectorSettings` as a detector input (same policy); default 0 (engine default, Wpf default). The readout shows the effective value. | proposed — *verify* the theme-35 optimum (efficiency vs gap at a fixed contact) reproduces through Studio's list-mode path before quoting it in the UI or docs |
| T-3 | **SiPM pitch is not migrated.** The block-average is not physics (a light-sharing detector resolves crystals from the flood through Anger logic), the reference matching is 1:1, and readout modelling belongs to TODO-19. | proposed — **author decision**; *verify* nothing else in Studio needs it |
| T-4 | **Detector workspace** (fourth workspace): the detector face at sub-pixel resolution — per-crystal gain (from the acquired detector settings, or the pending ones before Start), reflector gaps as dead area — plus a readout: fill factor (analytic) and, after an acquisition, a measured comparison, effective crosstalk, gain σ / seed, crystal material. Works with no acquisition. | proposed — *verify* which measured quantity is honest to show (e.g. detected / expected counts vs the analytic fill factor needs a reference without gaps — maybe drop it) |
| T-5 | **Depth (3D)** in the Imaging workspace: an action on the retained data (never a new MC), per imaging channel (each nuclide's window flood, and All), reporting each peak's (x, y, z) **with the focus-curve width**, not a bare z; K not taken from the scene truth. | proposed — *verify* (a) depth precision and focus-curve width at Studio's default geometry for sources at ~300 / 500 / 1000 / 2000 mm with realistic live times (seeds, counts), (b) whether `LocalizeDepths`' peak prominence is acceptable or the theme-24 hybrid (seed-free pass, then calibrated refine) is needed, (c) the grid cap and non-cyclic decoding over the plane sweep, (d) cost per sweep on the worker |
| T-6 | **Rangefinder = an external range input** that sets the decoder focal plane (the existing focus control relabelled or extended); no new physics. | proposed |
| T-7 | **Focus fusion** reports, never auto-changes: the laser range vs the depth-from-focus estimate with its width, and a "source may be off the laser surface" note only when the difference exceeds a **derived** significance. The Wpf thresholds (1.2, 150 mm, 250 mm) are not reused. | proposed — *verify* how to derive the significance from the measured focus-curve noise; if it cannot be derived honestly, show the two numbers without a verdict |

## Measurements by the planner (2026-10-02)

The implementer's review ([PLAN.Studio.Detector.Review](PLAN.Studio.Detector.Review.md)) confirmed the code rows it
could read, then lost process creation (`CreateProcessAsUserW 5`), so the planner ran the numerical rows. Probe outside
the tree: `SimulationService.BuildConfig` → `ListModeSource` → `MeasurementStage` (Studio's path), Studio defaults
(rank 13, 0.7 mm, D 80, 30 × 30 at 0.6 mm, GAGG chain FWHM(662) = 30.2 keV, Cs-137 500 µCi), 662 window ±1.5 FWHM.

**Gap and crosstalk** (source at 1000 mm on axis, 60 s, 3 seeds):

| gap µm | rate cps (contact 0) | rate / no-gap | ((p−g)/p)² | window fraction, contact 0 / 0.4 / 0.9 |
|---|---|---|---|---|
| 0 | 103.70 | 1 | 1 | 0.301 / 0.304 / 0.303 |
| 100 | 72.81 | 0.702 | 0.694 | 0.302 / 0.303 / 0.303 |
| 200 | 46.63 | 0.450 | 0.444 | 0.304 / 0.297 / 0.297 |

The gap behaves as dead area. **Crosstalk is a no-op on Studio's path**: `ComptonCrystalDetector` hands the list-mode
sink the total deposit *before* the light spread, and the position is the arg-max site, which the main crystal keeps for
any leak below 0.8. Theme 35's optimum came from the engine's per-crystal window (`PerPixelWindow`), a different readout.
Under a conventional four-channel Anger readout, leaked light reaches the same four channels: it moves the computed position, not the
energy — so crosstalk is a light-sharing effect and belongs to TODO-19 with the SiPM pitch.

**Depth from focus** (peak prominence vs decoder plane on the retained flood; `LocalizeDepths` with K = 1 gives the
same planes; 5 seeds; `all` = every event, `win` = 662 window):

| true z mm | lateral | counts 10 s / 60 s (all) | sharpest plane (10 mm grid) | half-max interval | seed spread |
|---|---|---|---|---|---|
| 300 | axis | 7.7 k / 46 k | 360 (all), 310–320 (win) | 230–380 | 0–20 |
| 300 | 30 mrad | 7.2 k / 44 k | 280 | 240–380 | 0–5 |
| 500 | axis | 2.8 k / 17 k | 580–586 | 350–730 | 0–57 |
| 500 | 30 mrad | 2.5 k / 15 k | 460 | 350–730 | 0–4 |
| 700 | axis / 30 mrad | 1.4 k / 8.7 k | 680 / 560 | 430 – sweep edge | 0–130 |
| 1000 | axis / 30 mrad | 0.7 k / 4.3 k | 830–1450 (100 mm grid) | 600–700 – edge | 40–560 |
| 2000 | axis / 60 mrad | 0.18 k / 1.1 k | 890–2900 | ~800 – edge | 150–630 |

- Near (≤ 500 mm) the curve has a clear maximum; the estimate is **reproducible but biased, and the bias depends on the
  lateral position** (+60 / +80 mm on axis, −20 / −40 mm at 30 mrad; −140 mm at 700 mm off axis) — far larger than the
  seed spread. A finding, not tuned away (TODO-23).
- From ~700 mm the half-max interval runs into the far edge of the sweep: only a **lower bound** is measured; at 2 m the
  curve is nearly flat. Depth is a near-field measurement at this geometry.
- Cost: 30 planes × 50 × 50 grid 0.06–0.6 s per channel, 81 planes ~1.7 s (single thread) — worker work with cancel.

## Decisions after review (2026-10-02)

| # | Decision | Basis |
|---|---|---|
| D-1 | **Reflector gap editable** in µm (config mm), a detector input: stale on change, disabled while acquiring, carried by the snapshot; validated `0 ≤ gap < pitch` in the editor and the service; an optics change of pitch re-validates it; pending and acquired values shown apart. | T-1, review |
| D-2 | **Crosstalk is not added to Studio in this task.** It does nothing on the list-mode path (measured), and its real effect depends on the readout → TODO-19. | planner measurement |
| D-3 | **SiPM pitch is not migrated**; Wpf's block average is not a light-sharing model. SiPM pitch, light spread, crosstalk and crystal identification (LUT) become TODO-19, which now runs **before TODO-12**. | author, 2026-10-02 |
| D-4 | **Detector workspace**: the detector face — per-crystal gain pattern (pending settings before Start, acquired after; stale is marked, never silently re-drawn with new settings), reflector gaps drawn exactly (vector rectangles or coverage-aware rasterisation, so a 20 µm gap is not lost to a sub-cell grid), crystal material, gain σ / seed; readout: **geometric active-area fraction** labelled as area (not efficiency) and the acquired count rate shown separately. No "measured fill factor" (it needs a no-gap reference run). Room left for TODO-19's SiPM grid and flood map. | T-4, review |
| D-5 | **Focus sweep** (the depth tool) in the Imaging workspace: on the selected channel's retained flood, on the worker, cancellable, keyed on acquisition + channel + window/strip + optics. Planes from D + 30 mm to 3000 mm **uniform in 1/z** (the depth of field grows ~z²), using `ImagingProjection.AtFocus` (non-cyclic, the Studio grid). Shows the **focus curve** (prominence vs plane), the sharpest plane and the half-max interval; when the interval reaches a sweep edge it is shown as a bound ("≥ 700 mm"), never a finite width. Labelled "sharpest plane", not "distance", with a note that the estimate is biased near and unresolved far (TODO-23). K peaks chosen by the user (1–4, default 1), never from the scene; peaks linked across planes **by angle** (x/z), not by mm. | T-5, review, measurement |
| D-6 | **External range** is a separate optional input in Imaging (a measurement of the surface the laser hit) with a "Use as focus" action; the decoder focal plane stays a view control. | T-6, review |
| D-7 | **No fusion verdict.** The external range is drawn on the focus curve next to the half-max interval; no significance, no auto-change. | T-7: the measured bias is far larger than the seed spread, so a noise-based significance would be wrong |
| D-8 | Tests: settings validation / snapshot / stale, face geometry (gap rectangles, area fraction), sweep planes and censoring, cancellation and late-result rejection, angle linking; opt-in evidence test (`GCAM_EVIDENCE_TESTS`) re-measuring the gap rate ratio against ((p−g)/p)² with a tolerance derived from its seed spread. | review |

## Steps

1. **Review (no code):** check each *verify* row in the code and by measuring headlessly (a throwaway program outside
   the tree); propose improvements. Write `docs/archive/PLAN.Studio.Detector.Review.md`.
2. Planner writes "Decisions after review"; the author decides T-3; disagreements are discussed in the same conversation.
3. Implement: Core (settings, face model, depth result records — pure, tested), services (depth sweep on the worker,
   cancellable), views (Detector workspace, Imaging depth / rangefinder section), tests, offscreen renders (both themes,
   both sizes).
4. Docs: SRS rows (`SR-DET-*`, depth), SDS, VV matrix, DESIGN.Layout, Findings if a new measurement is made.

**Done when:** no `Gcam.Wpf`-only feature is left except those the author drops; tests and renders pass.

**Not here:** the four-channel Anger readout (TODO-19); desktop UI tests (TODO-22); deleting `Gcam.Wpf` (TODO-12).
