# PLAN.Studio.Optics — editable optics, presets and the focal plane (TODO-09)

Scope: making the optics (mask rank, cell pitch, mask–detector distance, detector array, focal plane) editable in
GCAM Studio, with presets and derived geometry numbers, and lifting the "optics read-only" exclusion in
VV.Studio.SRS §7. Order of work: [PLAN.Studio.Migration](PLAN.Studio.Migration.md).

Status: **reference plan** (2026-10-02). The implementer first reviews it, verifies every item marked *verify*, and
proposes improvements; the plan is then revised before implementation. Items marked *verify* come from `Gcam.Wpf`
or from the planner and are **not established**.

## What exists (checked in the code, 2026-10-02)

| Item | Where | Fact |
|---|---|---|
| Studio optics model | `Gcam.Configuration/SceneConfigBuilder.cs` `OpticsSettings` | rank 13, cell 0.7 mm, D 80 mm, 30 px, 0.6 mm, focal 1000 mm; rank snapped to the nearest prime in `Build`; recon grid kept inside the FCFOV |
| Studio UI | `MainWindow.xaml` Geometry section | read-only facts; `MainViewModel.Optics` already marks results stale when replaced |
| SRS | `VV.Studio.SRS.md` input table and §7 | "Optics … read-only defaults in this version"; "Editing optics … shown read-only" |
| `Gcam.Wpf` presets | `MainWindow.xaml.cs` `OptPreset_Changed` | Sharp (13, 0.7, 80, 30, 0.6) default; Baseline (7, 1.0, 60, 12, 1.0); Wide FOV (17, 1.0, 50, 34, 0.75); High-res (13, 0.5, 100, 44, 0.4). Comment: each keeps the detector spanning ~one mask period, "verified in the MC (mixedfield, 3 sources)" |
| `Gcam.Wpf` readout | `UpdateOpticsReadout` | resolution element = cell·F/D; FCFOV half = rank·res/2; shadow = cell·F/(F−D); Nyquist = shadow/pitch (✓ ≥ 2); coverage = N·pitch / (rank·shadow) (✓ 0.9–1.4); depth reach ≈ 0.15 m·√(A·D / (18.2·80)) "rough" |
| `Gcam.Wpf` focal plane | `OptFocal_Changed` / `Refocus` | moving the focal plane rebuilds only the decoder and re-decodes the accumulated flood — no new MC |

**Inconsistency found while checking.** At Studio's default focal plane (1000 mm) the default "Sharp" preset gives
coverage = 18 mm / (13 · 0.761 mm) = **1.82** periods — outside `Gcam.Wpf`'s own ✓ band (0.9–1.4). At 160 mm the
same preset gives 0.99. Either the presets were tuned for a near focal plane, or the ✓ band is not the right rule
for non-cyclic decoding. This must be settled by measurement, not by choosing one.

**Measured since (planner, 2026-10-02, Findings theme 55).** The default optics at 1 m sample the 0.761 mm mask
shadow at 1.27 pixels per cell; this produces a position-dependent localisation error (RMS 0.95 mm, max 1.91 mm;
0.24 / 0.41 mm at 3.8 samples per cell). The Nyquist row of O-4 therefore has evidence, and every preset must be
judged at the focal plane it will be used at.

## Proposed design (reference — the implementer may improve any of it)

| # | Proposal | Status |
|---|---|---|
| O-1 | Optics fields become editable **run inputs** (rank, cell pitch, D, detector pixels, pixel pitch): a change marks the acquisition stale; rank shows the snapped prime it will use. | proposed |
| O-2 | The **focal plane is a view setting**: changing it re-decodes the acquired events / channels at the new plane without a new acquisition (as `Gcam.Wpf` did), and never marks stale. | proposed — *verify* that every decode path (acquisition snapshot, imaging channels, found peaks) can be rebuilt from retained data |
| O-3 | A **preset** selector with the four `Gcam.Wpf` presets. | proposed — *verify* each preset with the MC at the focal plane Studio uses, and correct or re-tune it if it fails |
| O-4 | A **derived-geometry readout** (resolution element, FCFOV, Nyquist samples per cell, detector coverage in mask periods, aperture). | proposed — *verify* each ✓ / ⚠ rule against the engine's own studies (configuration scan, array sampling, field-of-view; evidence IDs in VV.Gcam.Evidence) or drop the rule; no warning without evidence |
| O-5 | The depth-reach hint is **dropped** unless an engine study backs a formula. | proposed |
| O-6 | Layout: the left panel already scrolls at 1280 × 800; editable optics make it longer. Collapsible sections (Geometry / Detector) with a one-line summary when collapsed. | proposed — alternatives welcome (e.g. a separate Optics workspace, as `Gcam.Wpf` had a tab) with reasons |
| O-7 | Validation ranges for every field, from the engine's limits (e.g. pixel count, prime ranks, D > 0, focal > D). | proposed — *verify* the limits in the engine |

## Steps (after the review)

1. **Review (no code):** check every *verify* item, measure what needs measuring with the engine (CLI studies or a
   headless test), and return a written review: confirmed / corrected / dropped, with numbers and evidence.
2. The planner revises this plan; disagreements are discussed before implementation.
3. Implement the revised plan: Core (settings, validation, derived numbers as pure functions with tests), services
   (refocus path), view (fields, presets, readout, layout), render snapshots of the left panel and a refocused image.
4. Docs: lift the §7 exclusion, SR rows for optics editing / presets / refocus / readout, SDS, VV matrix, DESIGN.Layout.

**Not here:** desktop UI tests (last), depth estimation (TODO-11), chain selection (TODO-10).
