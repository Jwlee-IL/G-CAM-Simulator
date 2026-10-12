# PLAN.Studio.ReadoutPresets — the physical readout on every Studio preset (TODO-41)

Scope: TODO-40 brought the experimental four-output readout into GCAM Studio for the 12 × 12 Baseline head only (SD-2).
This plan makes a physical readout prepare, calibrate and run on every Studio optics preset — Sharp (the default),
Wide FOV, High-res — and decides what Custom geometries get, with the preparation cost measured. Where a preset cannot be
calibrated honestly, the outcome is an explicit, measured refusal, not a tuned pass. Studio stays the simulator's
engineering viewer (D-34); the engine default readout stays DirectCrystal (D-57).

Status: **done 2026-10-12** (closed; archived). Evidence: v4 confirmation plus the final declared v5 repair — offers at
661.7 / 1332.5 keV on every preset, 122.06 keV refused. Studio stage: implementation `bc542fb` (turn 8, Codex then a
substitute Claude implementer), Codex review with six findings fixed in `de5cc82`, desktop split / failure recording /
polling robustness `2518821` ([Turn 8](PLAN.Studio.ReadoutPresets.Turn8.md), "Fixes after review", "Desktop split",
"Polling robustness"). Planner checks: build 0 / 0, 955 passed / 37 skipped, offer record `--check` both modes, renders,
catalog; desktop: full suite and readout scenarios pass, broken-verdict / recovery pairs as recorded in VV.Studio.History
(2026-10-12); the cold High-res 661.7 keV scenario is a long test (`GCAM_UI_LONG_TESTS=1`). Records: SR-DET-05 / -10 /
-11, SR-RUN-34, EV-36 and LIM-11 scope notes, Findings theme 69.

## What exists (checked in the code)

| Item | Where | Fact |
|---|---|---|
| Studio presets | `src/Gcam.Studio.Core/Optics/OpticsPreset.cs`; defaults in `src/Gcam.Configuration/SceneConfigBuilder.cs` | Sharp (default) 30 × 30 @ 0.6 mm (rank 13, cell 0.7 mm, D 80 mm); Baseline 12 × 12 @ 1 mm; Wide FOV 34 × 34 @ 0.75 mm; High-res 44 × 44 @ 0.4 mm; Custom = any geometry (pixels clamped 4 … 64) |
| Geometry gate | `src/Gcam.Studio.Core/Services/ReadoutPolicy.cs`, `GeometryError` | anything but the exact Baseline geometry is refused with a reason; `Key` already contains pixels, pitch, gap, absorbers |
| Studio readout preset | `src/Gcam.Studio.Services/ReadoutPreparationService.cs`, `Preset` | FourOutputAnger, DPC column 10 Ω / row 1000 Ω (ratio 0.01); every other section at engine defaults — sensors 1:1 (null sizes), specular 0.98, 8 depth bins, 100 000 photons per bin, calibration 2000 events per crystal, 32 bins per crystal, σ 1 bin, separation 0.5 |
| Hard-coded 12 × 12 | `ReadoutPreparationService.Build` (flood size `144 *`, validation `144 * 200`, arrays of 144, `DirectCrystal(sites, 12)`, `* 12`); `DetectorWorkspaceViewModel` status text (`/ 144 peaks`, `/ 144 crystals`) | must follow the geometry once the gate opens |
| Sensors | `src/Gcam.Configuration/Readout/ReadoutSensorConfig.cs` | null sizes = 1:1 coupling with a **virtual** sensor whose active width is the crystal's (labelled assumption; the only catalogue preset is a 3 × 3 mm device); explicit `CountX/Y`, `PitchMm`, `ActiveWidthMm`, offsets exist |
| Optics | `ReadoutOpticsConfig` (light guide thickness / index exist, 0 = direct coupling); `src/Gcam.Detector/Readout/OpticalResponse.cs` | the table is traced per symmetry class (mirror + transpose): ≈ (n/2)² / 2 classes × depth bins × photons per bin; no translation reuse |
| LUT | `src/Gcam.Detector/Readout/FloodLut.cs`, `Calibrate` | local maxima of the smoothed raw-position histogram → non-maximum suppression → the **n highest** kept → ordering by sorting into rows → regularity check → watershed (default) or Voronoi; explicit failure messages |
| Measured (TODO-40 review, busy machine, one build) | `docs/archive/PLAN.Studio.Readout.Review.md` | 30 × 30 @ 0.6 mm: device build 45.4 s, calibration 33.6 – 37.0 s, 1988 peaks kept for 900 crystals, failure "cannot be ordered … (row 0, columns 24/25)" on every same-seed repeat; 12 × 12: 7.7 s / 0.7 – 1.0 s, success; per input hit 30 µs (30 × 30) vs 8 – 10 µs (12 × 12) |
| Theme 68 | `docs/AGENTS.Findings.Part2.md`, EV-36, LIM-11 | the readout was selected and confirmed at 1 mm pitch (3.2 mm / 0.5 mm wall as the comparison); microcells unlimited (LIM-11) — already conditional at 1 mm (≈ 9 photoelectrons per 50 µm cell); finer pitches were never validated; the review warned: do not tune smoothing / separation after seeing the failure and call the preset validated |

## Proposed decisions (verify / improve)

| ID | Decision | Kind |
|---|---|---|
| RP-1 | **Diagnose before fixing.** For each preset, separate the three possible causes with measurements that do not use the LUT: (a) the flood is not physically resolved at that pitch (peak-to-valley along true crystal centres, spot width vs spacing from truth-labelled single-crystal events); (b) it is resolved but the LUT algorithm fails (candidate selection by height, row ordering on a distorted grid, edge compression); (c) a numerical budget is too small (calibration events per crystal, optical photons per bin, bins per crystal). Report which cause holds, per preset | verify |
| RP-2 | **Readout design per preset — two candidates, measured, the author chooses:** (A) the current 1:1 virtual sensor at the crystal pitch (0.4 – 0.75 mm sensors do not exist as catalogue parts; microcell saturation, LIM-11, becomes worse); (B) published practice for fine-pitch arrays: catalogue-size SiPMs (e.g. 3 mm, the engine's preset) with light shared over several crystals through a light guide, so the network has fewer nodes. Measure both on each preset with the same blind criterion | proposed (author) |
| RP-3 | **Algorithm changes only with independent validation.** If cause (b) holds, a changed LUT method (e.g. ordering by known grid topology instead of the n highest peaks) is fixed before validation, selected on development seeds and confirmed on fresh seeds with a rule pinned by hash (theme 63 discipline), and it must keep the 12 × 12 result and its tests unchanged. Smoothing / separation are not retuned per preset to make a calibration pass | proposed |
| RP-4 | **Acceptance per preset** (the same blind criterion as theme 68, derived per geometry): calibration succeeds on every confirmation seed; 662 keV single-crystal photopeak mis-identification and acceptance reported with seed spread; no fixed pass threshold borrowed from the 12 × 12 head — the author decides from the measured numbers whether a preset is offered, labelled experimental, or refused with the measured reason | proposed (author after data) |
| RP-5 | **Cost.** Measure device build, calibration and per-hit cost per preset on an idle machine; reduce construction cost only by exact means (e.g. translation reuse of interior optical classes, if proven identical, a persisted table keyed by content) — never by a smaller photon budget than the converged one, unless re-measured for that pitch; Prepare stays cancellable with progress | verify |
| RP-6 | **Generalise the Studio path** — every hard-coded 12 / 144 in preparation, validation, diagnostics and status text follows the geometry; `ReadoutPolicy.GeometryError` becomes a per-geometry decision (offered / refused with a measured reason); Custom: offered only if it matches an offered preset's readout design and passes the same calibration at Prepare, otherwise refused with the calibration's own failure | proposed |
| RP-7 | **Requirements and tests:** SR-DET-05 / SR-RUN-34 wording follows the new gate; headless tests per preset (calibration outcome, generalised diagnostics), renders of the Detector LUT view for a large grid; the desktop scenario's refusal case moves to a still-refused geometry; desktop run last, on the author's go | proposed |
| RP-8 | **Not here:** ambient / BSR with a physical readout (TODO-42); microcell saturation model (LIM-11); measured reflector data; changing the engine default readout | planner |

## Decisions after review (2026-10-08)

The review ([PLAN.Studio.ReadoutPresets.Review](PLAN.Studio.ReadoutPresets.Review.md), Codex session `01a11a29-375e-7cd1-9cf5-671f56806f42`)
corrected the plan: Imaging (`PhysicalReadoutProjection`), Waveform (`PhysicalReadoutWaveform`) and the circuit renderer
(`ReadoutMapView`) are also hard-coded to 12 × 12 (planner checked); `PeaksFound` counts retained maxima before the
n highest are taken. Diagnosis (two response seeds on one optical / transport realisation each — diagnostic, not
validation): with the current 1:1 sensors (A) every fine head has an identifiable raw grid — worst spot FWHM / spacing
0.50 / 0.58 / 0.76 for Sharp / Wide / High-res, truth nearest-centre error 0.65 % / 1.8 % / 8.1 % — and the LUT fails
because "the n highest maxima" drops true spots (corners, borders) for continuum shoulders before ordering; four times
more calibration events does not help; 20 k vs 100 k optical photons moves centres by < 1 % of a spacing. High-res also
has real physical overlap. The published-practice design (B: 3 mm sensors on 3.2 mm pitch, 2 mm PMMA guide) compresses
the edges (edge / centre spacing 0.17 – 0.35) and fails on every head, the 12 × 12 control included. Busy-machine costs:
device build 39 / 49 / 93 s and calibration 18 / 28 / 76 s for Sharp / Wide / High-res. Interior translation reuse of the
optical table is not exact (grazing rays through the couplant); exact content reuse, removing the second network solve at
Start, and four adjoint network solves are.

| ID | Decision | Source |
|---|---|---|
| PD-1 | **Every preset is measured; refusal is an outcome, never a premise.** Whether a preset (or a condition of it) is offered is decided only from its confirmation data; a preset that fails is refused on screen with its measured reason. The diagnosis above predicts nothing for a preset | **author** |
| PD-2 | **Design A with a new LUT:** keep the 1:1 virtual sensor, labelled on screen and in the records as a virtual sensor with unlimited microcells (LIM-11 grows with fine pitch: ≈ 11 – 62 photoelectrons per 50 µm cell); add the review's opt-in **topology-aware LUT** (maxima before global suppression, monotone lattice, one-to-one matching, explicit refusal of unsupported slots, watershed segmentation kept), fixed on development seeds before selection. The 12 × 12 head keeps the legacy LUT and its artifacts byte for byte. Design B goes to the Backlog as a separate optical study (reflecting / partitioned guide, package) | **author** |
| PD-3 | **Offer per gate, not all-or-nothing:** each preset gets a qualification matrix over the pinned conditions — energy line (122 / 662 / 1332 keV) × count rate (1e3 … 1e6 cps). A cell is qualified when, over the confirmation seeds, every calibration succeeds, the upper one-sided 95 % bound of the readout's added angular RMS per axis is ≤ (pixel pitch / D) / √12 for that geometry (Baseline 0.276°, Sharp 0.124°, Wide 0.248°, High-res 0.066°), and the weakest crystal's acceptance is ≥ 0.8 × the median crystal's (planner's proposal — the author may change it before selection). Studio shows the matrix and, for the current scene, whether the expected count rate and lines fall in qualified cells; outside them it says "not qualified for this rate / line" (a notice, not a block — the viewer stays an engineering tool) | **author** (gates per condition); planner (thresholds, notice) |
| PD-4 | **Custom:** any geometry may Prepare; if its calibration succeeds the readout runs, labelled **experimental, unvalidated** (no qualification matrix); a failed calibration is refused with its own failure. A Custom module that matches a qualified preset's module fingerprint (crystal count / pitch / gap / depth / absorbers, sensors, optics, electronics, calibration policy) inherits that preset's matrix | **author**; planner (fingerprint inheritance) |
| PD-5 | **Cost:** exact means only — immutable content-keyed reuse of optical / network artifacts, no second network solve at Start, four adjoint solves verified against the old solve and the independent netlist solver with conditioning-derived bounds, network solve cancellable; no translation reuse; no budget cut without development measurement for that pitch; Prepare keeps stage progress and cancellation; desktop timeouts follow measured cost | review; planner |
| PD-6 | **Seed discipline:** the review's families T41_DEV4 / T41_SELECTION16 / T41_CONFIRM32 / T41_REPAIR32 (disjoint, checked) ported into `samples/evidence/` with pinned rule, recipe, source / binary closure and a failing-closed selector; development freezes the method and the numerical recipe (optical photons, DOI bins, calibration histories, histogram scale — numerical error ≤ 10 % of the spatial allocation); the implementer prepares and pilots, the **planner** runs each family as one background job, phase by phase; failures are kept, never dropped | review; planner |
| PD-7 | **Records and Studio:** the review's SR-DET-05 / SR-RUN-34 replacements and SR-DET-10 / 11 as the starting point, rewritten for PD-3 / PD-4; acquired module geometry captured immutably and used by Detector, Imaging, Waveform and circuit views; large-grid renders; EV-36 / LIM-11 / theme 68 scope wording synchronised with TODO-40's live path | review; planner |

**Correction (2026-10-08, after the stage-1 pilot).** Codex ran out of credits mid turn 2; a substitute Claude implementer
finished it ([Turn2](PLAN.Studio.ReadoutPresets.Turn2.md); branch commits `a5d108a`, `2184124`; planner re-ran build 0 / 0
and tests 895 passed). The full-budget Sharp development pilot (seed 41000001, 776 s, 3.1 GB) fails the topology LUT on
all three lines: the four-output network bows the outer flood columns sideways by 0.13 / 0.99 / 1.30 / 2.25 crystal
spacings (Baseline / Sharp / Wide / High-res) while the method searches ±0.5-spacing axis-aligned windows, and at 32 bins
per crystal with σ 1 bin the histogram has ~30 shot-noise maxima per crystal; every true spot still has its own maximum
within 0.2 spacing of its centre. Author decisions: (1) a **LUT v2 development turn** first — a matcher that follows
curved columns, noise-robust peak detection, fixtures with the engine's real lattice shape and counting noise —
development seeds only; T41_DEV4 runs after it. (2) PD-3's **weakest-crystal acceptance is judged on the isolated
held-out validation rows** (3 000 histories per crystal per line); the rate cells gate on the spatial bound and report the
rate-dependent loss as global throughput (10 000 hits per rate cell cannot resolve a per-crystal 0.8 ratio: a perfect
readout scores ≈ 0.77 on Sharp). Planner: rule v2 bounds the recipe's residual numerical bias by three refinement levels
(contraction ratio r ≤ 0.5, residual ≤ Δ₁ / (1 − r), sum ≤ 10 % of the allocation); the selection family is skipped if
development freezes a single candidate with clear margin.

**Correction (2026-10-08, turn 3 — decided before the DEV4 v2 data).** LUT v2 (`LatticeGrowth`: σ = bins per crystal / 8,
ring-by-ring lattice growth with a locally measured step, 3σ counting-statistics valley test; legacy default unchanged,
Baseline hashes identical; tests 906 passed, planner re-run) calibrates on development seed 41000001 at 662 / 1332 keV
on Baseline, Sharp and Wide and at 1332 keV on High-res; it refuses 122 keV everywhere (no spot structure) and High-res
662 keV at 2 000 histories per crystal (edge valleys < 1σ). Rule v3, fixed now, before DEV4 v2 is read (the runs record
both event sets): (1) the **identification gate uses single-crystal histories** — for a multi-crystal Compton history the
"true crystal" is only the direct assignment's largest-deposit convention, and the Anger readout places it at the light
centroid by design (theme 68); all-deposit RMS and acceptance, and the same geometry's direct-reference imaging
comparison, are always reported beside the matrix (author: no preference; planner). (2) A refinement component whose two
successive differences both lie within the measured replicate floor is bounded by D₁ + D₂ + floor; otherwise D₁ / (1 − r)
with r ≤ 0.5 (planner). (3) High-res 662 keV: DEV4 v2 records the 8 k / 32 k calibration levels; whether a larger
calibration budget is adopted for it is decided from those data, before confirmation, else the refusal stands. DEV4 v2
runs with `--jobs 2` (author).

**Correction (2026-10-08, metric review — supersedes rule v3 item (1); DEV4 v2 stopped by the author after the four
Baseline seeds, kept as a record).** Review of the recipe (`StudioReadoutRecipeV2.Measure`) and the Sharp pilot: the
"truth" was `DirectCrystal` — the largest-deposit crystal, i.e. the direct readout's own output — so the all-deposit RMS
measured the disagreement of two estimators, not readout error (Sharp 662 keV: 52 % of assigned photopeak events in the
reference crystal, 14 % one step away, 35 % two or more; RMS 6.2× the allocation); an "added" allocation was compared with
an absolute displacement; and the acceptance denominator (all scored histories) carried the crystal's own edge escape.
The single-crystal gate is no remedy: single-crystal histories are only 41 % (662 keV) / 28 % (1332 keV) of the assigned
photopeak events. **Rule v3 (author: fix the metric first):** truth is the **first interaction** of the history (earliest
site: its crystal and position — where the coded shadow is cast); per axis, both the Anger LUT crystal and the direct
largest-deposit crystal are scored against the first-interaction crystal on the **same accepted events**; the spatial
gate is the upper one-sided 95 % seed-cluster bound of RMS_Anger² − RMS_direct² ≤ allocation² (paired, signed); the
acceptance gate is, on the isolated held-out rows, the weakest crystal's ratio (Anger-accepted ÷ full-energy histories,
by first-interaction crystal) ≥ 0.8 × the median ratio, so the crystal's escape cancels; rate cells gate on the same
spatial statistic with piled events scored by their dominant hit, and report throughput; single-crystal and
largest-deposit numbers stay as diagnostics. Rule v3 items (2) and (3) stand; the refinement bounds use the new gate
statistics.

**Correction (2026-10-08, turn 4).** Implemented as recipe / rule / manifest v3 (tests 912 passed, planner re-run). One
development seed, Sharp, full budget: G = RMS_Anger² − RMS_direct² is −5.7 allocation² at 662 keV (Anger closer to the first
interaction than the largest deposit) and +18 at 1332 keV (Anger worse); both RMS are 3 – 7 allocations, dominated by
multi-crystal Compton tails. The full-energy acceptance ratio was ~1.000 everywhere — every full-energy history passes
the window and the LUT tiles the whole plane — so **the acceptance gate becomes a per-crystal sensitivity ratio** (author):
Anger events assigned to crystal k ÷ full-energy histories whose first interaction is in k, on the isolated held-out
rows, weakest ≥ 0.8 × median; it catches a LUT that compresses edge crystals and moves their counts to neighbours, and
escape still cancels. Planner: the numerical budget is 0.1 × allocation² on G (≈ 5 % of the allocation in RMS terms);
the direct reference carries no pile-up by construction, so the rate cells measure what the physical readout adds. DEV4 v3
starts once this is pinned (author).

**Correction (2026-10-09, after the imaging exploration — supersedes the performance gates of PD-3 and rules v2 / v3; DEV4
v3 was never started).** Turn 5's sensitivity ratio refused every head, the validated Baseline included (Anger centroid
placement at the edges). A new Codex conversation (`01a11b5b-c040-7991-a0d2-aa6d0558e2f8`,
[Explore](PLAN.Studio.ReadoutPresets.Explore.md)) measured the imaging-level cost on the four development seeds: same
histories, equal emitted fluence, nine in-FCFOV positions at 1 m, finite-mask cross-correlation, per-path flood correction.
At 1 000 direct counts (fresh histories) the Anger source RMS is 0.65° against 0.16° on Baseline 662 keV (F = 16
[13, 19]) and 1.13° against 0.10° on Sharp 1332 keV (F = 120); F changes with exposure (bias and remote peaks), so it is
not a measurement-time factor. The author found the numbers suspicious; a discrepancy check reproduced theme 68 exactly
(probe = `ReadoutStudy` to 1e-9 mm; 0.394 / 0.362 mm against the published 0.406 / 0.367 mm) and found no mapping,
orientation, pairing, decoder, flood or window error. Mechanism: Anger places ≈ 70 % of multi-crystal histories in another
crystal, which blurs the coded shadow, so at low counts competing remote peaks win (95 – 99 % of the Anger MSE);
substituting the largest-deposit placement on the Anger-accepted events restores the direct RMS, substituting the true
energy window does not. Theme 68 measured one centred near-field source at 5 000 – 12 000 counts, where remote peaks do
not occur — correct, but narrower than a field-wide low-count claim. **Author decision: offer the physical readout
wherever its calibration succeeds, with the measured cost shown** — per (preset, line), offered (experimental) when the
blind calibration succeeds on every confirmation seed; no performance gate; Studio and the records show the measured
imaging cost beside it (absolute source RMS, bias and remote-peak probability for both readouts, and F, at declared
exposures, with seed uncertainty), plus the crystal-level diagnostics. High-res 662 keV: offered only if a calibration
budget fixed from development data succeeds, else refused. Readout improvements (migration-aware forward model / MLEM,
multi-crystal event handling) are a separate Backlog item.

**Correction (2026-10-10, confirmation and declared repair).** CONFIRM32 v4 (128 runs, all exit 0; interrupted twice by
host memory pressure and resumed with one worker into the same root; selector publication bug fixed before
aggregation): offered (experimental) — Baseline, Sharp 662 / 1332 keV, Wide and High-res 1332 keV; refused — 122 keV on
every head (32 / 32 failures, no spot structure), Wide 662 keV (4 / 32 at 2 k histories per crystal), High-res 662 keV
(1 / 32 at 32 k). Imaging cost (8 seeds, isolated): F at 1 k / 4 k direct counts 14.6 / 1.7 (Baseline 662), 121 / 5.0
(Sharp 1332), 74 / 9.5 (Wide 1332), 110 / 2.7 (High-res 1332). **Author decision: a declared repair judgement** for Wide and
High-res 662 keV on the fresh T41_REPAIR32 family (rule / recipe / manifest v5): only the calibration budget may change,
chosen on development seeds by a pre-stated rule; offered iff calibration succeeds on all 32 repair seeds; the v4 record
stays as published and the repair outcome is final for those two cells.

**Correction (2026-10-12, repair outcome).** Development (T41_DEV4, calibration only) fixed the budgets at 8 k histories
per crystal for Wide and 64 k for High-res (smallest level passing 4 / 4). REPAIR32 v5 (64 runs, one worker, all exit 0;
rule `5c088587…`, recipe `e2d5f1f3…`, manifest `137377a3…`): calibration succeeded on **32 / 32** seeds in both cells →
**Wide 662 keV and High-res 662 keV offered (experimental)**. Imaging cost (8 seeds; F at 1 k / 4 k direct counts,
isolated): Wide 2.08 / 0.83, High-res 4.67 / 2.59; at 1e5 cps 2.80 / 0.83 and 4.68 / 2.69. Wide's F < 1 at 4 k is a
bias effect, not an Anger advantage: the direct path's error there is almost all bias (RMS 0.517°, bias 0.511°; the same
≈ 0.50° direct bias appears at 1332 keV), so a record must quote it with that reason. Diagnostics: single-crystal
mis-identification 1.7 % (Wide) / 7.9 % (High-res); multi-crystal share 0.61 / 0.60. Planner re-run of the first seed of
each v5 family: identical to the run outputs apart from timing and process-memory fields.

**Decisions for the Studio stage (2026-10-12, after [Turn 7](PLAN.Studio.ReadoutPresets.Turn7.md)).** Turn 7 measured
cold Prepare through a copy of the Studio service path (one seed, one worker): Baseline 8 / 9 s, Sharp 60 / 68 s, Wide
177 s (662 at 8 k) / 90 s, High-res **2 672 s and 6.2 GiB committed** (662 at 64 k) / 178 s at 662 / 1332 keV; warm hits
< 0.1 ms; a retained High 662 flood would exceed 18 GiB, so floods are streamed. Planner-checked premises: today one
661.7 keV LUT serves every window (`ReadoutPreparationService`: `144 * EventsPerCrystal`, validation at 661.7, `DirectCrystal(sites, 12)`),
and the cache holds two entries.

| ID | Decision | Source |
|---|---|---|
| SD-A1…A3 | **Per-line calibration banks:** prepare each offered analysis line present in the scene at its pinned budget; share only line-independent optics / network artifacts; acquire the held ADC facts once and project each selected line through its own entry — never dispatch by generating isotope or true energy. A selected refused line (122 keV) shows "unavailable" with its measured reason, no fallback LUT; incidental emissions stay in transport and do not block other prepared lines; unknown lines are not offered | **author** (Turn 7 A1–A3) |
| SD-A4, A5 | **Flood correction:** an opt-in exact per-line correction histogram built from the events calibration already holds (no second flood), verified equal to a replay; raw counts kept separate; an empty correction bin makes the corrected image unavailable (explicit raw diagnostic view only), never capped or divided silently | **author** |
| SD-A7, A8 | **Session-only reuse**, no persisted cache (Studio keeps no write surface, `AGENTS.UiAutomation` §1 unchanged); background Prepare with real line / stage / history progress and cancellation, one worker | **author** |
| SD-A9, A10 | **Custom:** a module + line + policy fingerprint match inherits the calibration offer or refusal and its budget (Custom cannot bypass High 64 k); the reference cost table is inherited only when the full imaging profile (mask, distance, focus, decoder, window, correction) also matches, otherwise "imaging cost unmeasured for this configuration"; an unmatched module calibrates locally at 2 k with Growth / Adjoint, labelled experimental, unvalidated | **author** |
| SD-A6, A11–A16 | Adopted as Turn 7 recommends: split cache keys, pinned active entries, byte-accounted limit after measurement; both benchmark exposures and rates shown, no interpolation or Anger-to-Direct count mapping; MLEM / other windows keep a benchmark-not-applicable notice; the cost table is a generated, hash-pinned record (`offer-cost-v1.json` from the archived published outputs, `--check` in CI), no runtime TEMP / download / write; large grids drawn from a cached raster with zoom / inspection; one cold High desktop scenario bundling its checks; the table's Direct labelled the ideal largest-deposit reference | planner |

## Steps

1. **Review turn (no code):** verify the "what exists" rows; measure RP-1 per preset with a headless probe in `%TEMP%\gcam-todo41\`
   (short, a few seeds; long runs prepared, not run — the planner starts them); estimate RP-2 (B)'s geometry and its
   expected effect; time each preset's build / calibration; propose the LUT change if (b), the acceptance metrics, the SR
   wording, tests and the author's questions → `docs/PLAN.Studio.ReadoutPresets.Review.md`.
2. Decisions after review (author: RP-2 design, RP-4 offering after the data).
3. Implementation in the same conversation: engine (LUT / optics cost), seed families and a pinned rule for any selection
   or confirmation run (prepared by the implementer, run by the planner), Studio generalisation and gate, tests.
4. Planner verification, records (Findings theme, EV, SRS rows, VV.Studio), desktop scenarios on the author's go, archive.

## Done when

Every Studio preset either prepares and calibrates a physical readout with measured, seed-quoted identification and cost,
or refuses it with a measured reason shown on screen; Custom follows a stated rule; the 12 × 12 head's results and tests
are unchanged; records and tests updated; desktop scenarios pass on the author's go.
