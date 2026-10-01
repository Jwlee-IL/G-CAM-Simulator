# PLAN.Studio.Optics — editable optics, presets and the focal plane (TODO-09)

Scope: making the optics (mask rank, cell pitch, mask–detector distance, detector array, focal plane) editable in
GCAM Studio, with presets and derived geometry numbers, and lifting the "optics read-only" exclusion in
VV.Studio.SRS §7. Order of work: [PLAN.Studio.Migration](PLAN.Studio.Migration.md).

Status: revised after the implementer's measured review ([PLAN.Studio.Optics.Review](PLAN.Studio.Optics.Review.md),
2026-10-02); the review's corrections are adopted below. The reference proposals O-1 … O-7 are kept for the
record; **"Decisions after review" is the specification.**

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

## Decisions after review (2026-10-02)

| # | Decision | Basis |
|---|---|---|
| R-1 | **Physical optics are run inputs** (rank, cell pitch, D, detector pixels, pixel pitch): one normalised, validated *effective* settings record feeds the config, the readout and preset matching; the effective prime is shown; a preset applies atomically and the selector shows **Custom** once a field diverges; physical edits are disabled while acquiring; a physical edit marks stale. | review O-1 |
| R-2 | **Decoder focus is a separate view setting**, placed in the Imaging panel ("Decoder focal plane, mm from detector"). It re-projects **every** decode path from retained data — All and each isotope / stripped channel: reconstruction, estimate, found peaks — with no new transport, measurement, random draws or calibration; works while Acquiring, Stopped and Completed; latest focus revision wins over late snapshots; never sets or clears stale. Reconstruction measurements are cleared on a focus change with a visible note (their mm meaning changes); flood measurements stay. Decoder focus is distinct from `MaskConfig.FocalDistanceMm` (a transport input). | review O-2 — today the All reconstruction is **not** re-projected (measured) |
| R-3 | **Presets**: keep the four `Gcam.Wpf` geometries, Sharp default, labelled as engineering geometries; their documented performance is **conditional** (per-isotope channels pass a one-resolution-element gate at 160 mm and 1 m in the review's mixed scene; Baseline fails the broadband 1 m case). No retuning in this step. | review O-3 |
| R-4 | **Readout without pass/fail glyphs**: resolution element r = c·F/D, nominal cyclic field ± p·c·F/(2D), samples per mask cell, coverage in mask periods, physical mask width. Samples per cell carries an **evidence note, not a warning**: below ~2 the localisation error depends on the source position (Findings 55: RMS 0.95 mm at 1.27, 0.24 mm at 3.8, same detector size). The coverage 0.9–1.4 band is dropped (neither necessary nor sufficient, review sweep). | review O-4 + Findings 55 (planner addition) |
| R-5 | **No depth-reach hint.** | review O-5 |
| R-6 | **Layout**: collapsible *Physical optics* and *Detector* sections in the shared left panel, each with a one-line summary when collapsed; validation stays visible when collapsed and errors expand their section on Start. | review O-6 |
| R-7 | **Validation as Studio policy**, not engine physics: finite numbers only; rank from the tested set {5, 7, 11, 13, 17, 19, 23}; N integer 4–64; D ≥ 1 mm; pitches ≥ 0.05 mm; pixel pitch > reflector gap (cross-field error, never silently changed); every scene source in front of the mask; decoder focus > the acquired D; a bounded decode-grid allocation. | review O-7; rank set = presets + the configuration-scan ranks (planner) |

**Separate finding, not part of TODO-09:** the review could not reproduce Findings theme 3 / EV-03's rank-23
"usable ±64 mm (96 %)" (measured 0.149 usable under the stated conditions). It is tracked as TODO-18; the optics
UI must not cite that headline until it is resolved.

## Steps (after the review)

1. ~~**Review (no code):**~~ done — see the Review document. ~~Check every *verify* item, measure what needs measuring with the engine (CLI studies or a
   headless test), and return a written review: confirmed / corrected / dropped, with numbers and evidence.
2. ~~The planner revises this plan~~ done (Decisions after review); disagreements are discussed before implementation.
3. Implement R-1 … R-7: Core (effective settings, validation, derived numbers — pure, tested), services (one
   projection helper for All and channels, refocus caching / latest-revision rule), view (sections, presets, readout,
   focus control), tests as in the Review's "Proposed revised acceptance/evidence plan" items 1–5, plus a regression
   test for Findings 55 (localisation error falls with samples per cell at constant detector size), and render
   snapshots (left panel expanded / collapsed, a refocused image, both themes).
4. Docs: lift the §7 exclusion, SR rows for optics editing / presets / refocus / readout, SDS, VV matrix, DESIGN.Layout.

**Not here:** desktop UI tests (last), depth estimation (TODO-11), chain selection (TODO-10).
