# VV.Gcam.Evidence — the simulation evidence behind the Gcam requirements

Scope: every simulation or design-model result that a number in the [URS](VV.Gcam.URS.md), the
[PRS](VV.Gcam.PRS.md) or the [Limitations](VV.Gcam.Limitations.md) rests on — what it shows, under which
conditions, how to reproduce it and which test pins it. Each entry is complete on its own: a reader of the V&V set
does not need any other document to check a number. Not covered: GCAM Studio's software verification
([VV.Studio](VV.Studio.md)).

**At a glance**
- 33 entries, `EV-01` … `EV-33`, grouped as imaging, energy and isotopes, rate and dose, head design,
  manufacturing, and range.
- Each entry gives the grade of its evidence ([PRS §1](VV.Gcam.PRS.md#1-evidence-grades)): **MC** Monte Carlo,
  **RTL** front-end model, **AN** analytical design model.
- Nothing here is measured on hardware, and nothing has been compared with recordings of any real instrument.
- Commands are the repository's CLI, `montecarlo <command> <scenario>` (run as
  `dotnet run --project src/Gcam.Cli -c Release -- <command> <scenario>`), or a Python script; outputs land in
  `samples/` or `rtl/`.
- Monte Carlo numbers are quoted over **seed ensembles** (§1, "How MC numbers are quoted"), not as one run.

## 1. The tools behind the grades

| Grade | Tool | What it models | What it leaves out |
|---|---|---|---|
| **MC** | C# engine (`src/Gcam.*`); each transport stage is held to a closed form by `ConfigLoaderTests`, `SamplingTests`, `TransportInvariantTests` and `DecoderInvariantTests`, with k·σ tolerances that follow the sample size | point or multi-line sources with directional biasing (unbiased against 4π: ratio 0.999 ± 0.006, N = 32, EV-07); a ray-marched tungsten mask slab with NIST μ(E); Compton transport in a pixelated crystal with tabulated cross sections per material (xraylib / NIST); energy resolution from a photo-electron budget; Poisson counting; cross-correlation and MLEM decoding | room scatter, the shield's side and rear walls except where an entry says otherwise, hardware drift not listed in an entry |
| **RTL** | SystemVerilog shapers (`rtl/*.sv`) checked bit for bit against an integer reference, plus Python front-end models (`rtl/*.py`) | ADC waveform, pulse shaping, pile-up, afterglow, gain spread | the analog chain beyond its noise figures |
| **AN** | Python design models (`samples/*_study.py`, `samples/hardware_concept.py`) built on MC-validated laws | mass, envelope, balance, parallax, heat, hand motion, SiPM gain drift | transport-grade accuracy — first-order estimates |

**How MC numbers are quoted (since 2026-10-02).** Every stochastic number is measured over N outer seeds (the
scenario's `Seed` replaced, nothing else) and quoted as mean ± sample SD, or median [first, third quartile] where
the distribution is skewed, always with N; a pick or a pass/fail gate is quoted as a frequency, "k / N". The SD is
the spread of the study's result from seed to seed, not a tolerance and not the error of the mean. A value that
came out the same in every seed is "the same in N / N seeds", not a deterministic constant. Seeds are swept with the
study's fixed manufactured patterns held fixed (mask patterns, defect maps, the uniformity map, the RTL fixtures),
so such results are conditional on those patterns. Seed lists, the recipe per family, configuration hashes, the
engine commit and the per-metric summaries are in `samples/evidence/` (`manifest.json`, `seeds.json`,
`results/aggregate.csv`); `python samples/evidence/run_seeds.py` re-runs a family and
`python samples/evidence/aggregate.py` summarises it. Deterministic numbers (budgets, analytic models, RTL
bit-equality, synthesis timing) carry no SD.

### Ideal conditions and background

Unless an entry says otherwise, its MC numbers come from an **ideal environment**: only the simulated source(s), **no
ambient (natural) background radiation**, an ideal detector response, known geometry and calibration. They are
**best-case bounds**, not field performance. Where a study adds background, it is either a uniform pedestal or a
Poisson event stream whose rate is set **relative to the source** (background ÷ signal, BSR) — a controlled stress
test, not the absolute, source-independent ambient field a real camera sees (terrestrial K / U / Th lines, cosmic
and scattered radiation at a given dose rate). An absolute ambient-background model is being added; until it is
measured, the column "Under ambient background" states the expected direction and the evidence for it.

| Entries | Background in the quoted result | Under ambient background (expected; to be measured) |
|---|---|---|
| EV-01, EV-03, EV-05, EV-06, EV-08, EV-09, EV-13, EV-31, EV-32 (imaging precision, field, sampling, tolerances) | none | the Poisson part of each error and the counts needed for a given precision grow (statistical term roughly × √(1 + B/S)); a fixed geometric / grid floor need not move, but wrong-peak probability and the time to reach the floor do; a weak source's coded signal competes with background Poisson noise (direction shown in EV-12: at 400 source counts RMS 0.76 mm at BSR 1, ~2.9 mm at BSR 2) |
| EV-07 count gates (collapse below ~25, sub-mm from ~250 counts) and PR-SENS-02 | none — every count is a source count | the gates are ideal references: with background the same net source counts give lower precision (background adds variance), so a gate needs a calibrated significance (false-alarm rate on background-only acquisitions) plus a separate localisation-quality criterion |
| EV-10, EV-11, EV-14, EV-15 (multi-source, MLEM, Compton strategies, isotope separation) | none (other isotopes only) | ambient lines and continuum add to every window; stripping must also remove the background's own downscatter |
| EV-02 field of view | BSR 0 and 1 (relative) | wrong-spot and outside-field-cue rates at an absolute ambient rate depend on source strength and distance |
| EV-12 background and mask / antimask | BSR 0–4 (relative, uniform pedestal) | the absolute level sets which BSR a given source and distance falls at |
| EV-25 shield | relative background spectra (scattered, 662 keV, Co-60) | the needed shield follows the actual ambient spectrum and dose rate |
| EV-17, EV-21, EV-22 (front end, rate, dead time) | none / source-only event streams | background adds counts to pile-up and dead time; small at natural levels |
| EV-23 dose | source fields only | natural background adds ~0.05–0.2 µSv/h to every reading, a floor for low readings |

Geometries used below:

| Name | Scenario file | Head | Source plane |
|---|---|---|---|
| **reference lab geometry** | `samples/scenario.json` | 12 × 12 × 1 mm pixels, rank-7 MURA (2 × 2 mosaic, 1 mm cells, 10 mm W), mask–detector D = 60 mm | 100 mm from the mask |
| **hand-held head** | `samples/scenario_handheld.json` | 16 × 16 × 1 mm GAGG:Ce,Mg, 15 mm thick, same mask, D = 55 mm | 100 mm from the mask unless the entry says 1 m / 5 m |
| reference lab geometry, GAGG | `samples/scenario_orig_gagg.json` | the reference lab geometry with a 10 mm GAGG crystal | 100 mm |

## 2. Index

| ID | Evidence | Grade | Used by |
|---|---|---|---|
| EV-01 | Localisation in the fully coded field; ghosts outside it; non-cyclic decoding | MC | PR-IMG-01, -07; LIM-07; UN-01, UN-11 |
| EV-02 | Field of view at field distance and the out-of-field cue | MC | PR-IMG-01, -07, -08, -10; LIM-01, LIM-06, LIM-07 |
| EV-03 | Field of view ÷ resolution = rank | MC | PR-IMG-09, -10; LIM-01 |
| EV-04 | Mask thickness: leak against collimation | MC | PRS §2; LIM-01 |
| EV-05 | Tapered (bevelled) channels | MC | PR-IMG-10; LIM-01 |
| EV-06 | Detector sampling of the mask shadow | MC | PR-IMG-06; LIM-01, LIM-08 |
| EV-07 | Count threshold and directional biasing | MC | PR-SENS-02 |
| EV-08 | Sub-cell peak interpolation | MC | PR-IMG-02 |
| EV-09 | Hand-held head: sensitivity and precision | MC | PR-IMG-02, PR-SENS-01, -07; URS §5 |
| EV-10 | Several isotopes in one field | MC | PR-IMG-03, PR-NRG-06; LIM-04 |
| EV-11 | MLEM reconstruction | MC | PR-IMG-04; LIM-01, LIM-07 |
| EV-12 | Ambient background and mask / antimask | MC | LIM-07 |
| EV-13 | Depth-of-interaction parallax | MC | LIM-08 |
| EV-14 | Event positioning: total-energy window + largest deposit | MC | PR-NRG-03; LIM-08 |
| EV-15 | Isotope separation: the spatial limit and Compton stripping | MC | PR-NRG-04; LIM-04, LIM-05, LIM-08; UN-03 |
| EV-16 | One gain for 122–1332 keV | RTL | PR-NRG-01, -06; LIM-04 |
| EV-17 | Energy-resolution budget and pulse shapers | RTL, MC | PR-NRG-02, PRS §2; LIM-02, LIM-04, LIM-08 |
| EV-18 | Per-channel gain calibration | RTL, MC | PR-NRG-05 |
| EV-19 | Crystal materials | MC | PRS §2, PR-PHY-05; LIM-08 |
| EV-20 | Ir-192 in the engine | MC | URS §5 |
| EV-21 | Count-rate capability and afterglow | RTL | PRS §2, PR-SENS-03; LIM-02; URS §5 |
| EV-22 | Dead time | MC | PR-SENS-04, -05; LIM-02 |
| EV-23 | Dose rate from the detector spectrum | MC | PR-SAFE-01, -02, PR-SENS-05; LIM-02, LIM-06, LIM-09 |
| EV-24 | Mass, envelope and form | AN | PR-PHY-01 … -03, -05; LIM-01 |
| EV-25 | Shield thickness against background energy | MC | PRS §2, PR-PHY-01; LIM-05 |
| EV-26 | Scene-camera parallax | AN | PR-IMG-05, PR-RNG-01 |
| EV-27 | Electronics heat and hand motion | AN | PR-PHY-03, -06, PR-ENV-03, -04; LIM-01 |
| EV-28 | SiPM gain drift and its compensation | AN | PR-ENV-01, -02, -05; LIM-03 |
| EV-29 | Thermal drift during an acquisition | MC | PR-ENV-05 |
| EV-30 | Mask fabrication tolerance | MC | PR-MFG-01, PR-IMG-07; LIM-01 |
| EV-31 | Mask–detector alignment | MC | PR-MFG-02, PR-DUR-03 |
| EV-32 | Dead and hot pixels | MC | PR-MFG-03 |
| EV-33 | Source distance (depth) | MC | PR-RNG-01; LIM-01; UN-10 |

## 3. Imaging

### EV-01 — Localisation in the fully coded field; ghosts outside it

- **Shows.** A centred source is located to **0.35 mm** (0.353 ± 0.001 mm, N = 32) and an off-axis source inside the
  fully coded field to sub-mm (6 mm → 6.03 mm, 0.20 mm error — the same grid cell in 32 / 32 seeds). The fully coded
  field is one mask period, `rank × cell × (D+S)/D`: ±9.3 mm on the reference lab geometry. A source outside it aliases to a
  **ghost on the opposite side** (12 mm → −6.40 mm, −6.4016 ± 0.0008 mm, N = 32; one period is 18.7 mm). The
  reconstruction's ghost margin (primary ÷ secondary peak) is reported with every estimate.
- **Cyclic against non-cyclic decoding.** Both decoders search the same ±18 mm grid, wider than one period, and a
  position counts if found within 3 mm. Cyclic decoding cannot tell period replicas apart, so it fails even inside
  the field; the finite-mask (non-cyclic) decoder roughly doubles the area: **reference lab geometry 278 vs 146** of 625 positions
  (medians; quartiles 276–280 and 145–147, N = 64), **hand-held head 361 vs 174** (360–362 and 172–175, N = 64;
  period 19.7 mm). At D = 55 mm the cyclic ghost margin is only 1.35 (1.352 ± 0.003, N = 32).
- **Reproduce.** `montecarlo samples/scenario.json` (and `scenario_offaxis.json`, `scenario_ghost.json`);
  `montecarlo sweep samples/scenario_handheld.json` → `samples/sweep_{cyclic,noncyclic}.csv`, then
  `python samples/plot_sweep.py samples/scenario_handheld.json` → `samples/cyclic_vs_noncyclic.png` (one realisation,
  seed 12345). Ensembles: `samples/evidence` families `sweep`, `sweep_head`, `precise`.
- **Tests.** `PipelineTests.CenteredSource_LocalizesSubMillimeter`, `.OffAxisInsideFcfov_Tracks`,
  `.OffAxisOutsideFcfov_GhostsToOppositeSide`, `.NonCyclicDecoding_SuppressesGhost`;
  `DecoderInvariantTests.AnalyticShadow_PeaksAtItsSource`, `.Decode_IsLinearInTheImage`.

### EV-02 — Field of view at field distance and the out-of-field cue

- **Set-up.** Hand-held head, Cs-137 at **1 m and 5 m**, moved 0–20° off axis along x and along the diagonal. Fully
  coded field ±3.64° along x (±5.14° on the diagonal); resolution element 1.04° (the success threshold). Per angle,
  100 Poisson realisations of a source of fixed strength, N0 = 500 or 5000 counts if it were on axis, with no
  background or a uniform ambient pedestal equal to N0 (BSR 1).
- **Shows.** Thresholded angles on a 0.5° grid over 64 seeds: the value that occurred most often, with its frequency
  where it was not 64 / 64.

  | | N0 5000 | N0 500 | N0 500 + background = signal |
  |---|---|---|---|
  | usable half-field, non-cyclic (≥ 90 % within 1.04°), along x, 1 m / 5 m | **7.0° / 7.5°** | 6.0° (52 / 64; 6.5–7.0° in 12) / 7.0° | 5.0° (56 / 64; 4.0–4.5° in 8) / 6.5° (54 / 64; 5.5–6.0° in 10) |
  | same, cyclic | 3.0° / 3.5° | 3.0° / 3.5° (48 / 64; 3.0° in 16) | 3.0° / 3.0° |
  | same, non-cyclic, diagonal, 1 m / 5 m | 9.0° / 7.5° (40 / 64; 4.5° in 24) | 3.0° (50 / 64) / 3.0° (55 / 64) | 0.0° (45 / 64; 0.5° in 17) / 0.5° (62 / 64) |
  | correct side from the flood centroid (≥ 95 %), 1 m, x | 1.0–14.5° | 1.5–14.0° (medians; 1.0° in 27, 13.5° in 21) | 1.5–13.0° |
  | "outside the field" flag from the centroid (≥ 90 %), 1 m, x | 4.0–12.5° | 6.0–11.5° (medians) | never (64 / 64); the flag rate peaks at 0.71 [0.67, 0.74] |

  The usable field is set by angle, not distance, and depends on the count level: ±7–7.5° at N0 5000, ±6–7° at
  N0 500. On the diagonal at low counts with background, the wide non-cyclic search is worse than the cyclic one
  (0–0.5° against 2.0–2.5° at 1 m). **Beyond ~7.5° the non-cyclic decoder answers with a wrong spot inside the
  field**: at 7.5–11°, 1 m, N0 500, in 52 % [49, 57] to 87 % [85, 89] of acquisitions (lowest and highest angle,
  medians): it moves the ghost boundary from 3.6° to ~7°, it does not remove it. The flood centroid catches most of
  those answers — without background an unflagged wrong spot at 7.5–12° occurs in at most 7 % [5, 8] of acquisitions
  at N0 500 and in none at N0 5000 (64 / 64); with background equal to the signal, in up to 49 % [46, 52] (N0 500) and
  29 % [26, 33] (N0 5000) at 5–12°. **Past ~14° nothing gives the direction** (the aperture's shadow leaves the array
  at ≈ 15°), and the decoder still returns an in-field spot in 52 % [50, 55] to 71 % [69, 72] of acquisitions at
  14–20° (1 m, N0 500).
- **Limits.** One isotope (662 keV); the front plate is an infinite 10 mm W slab around the mask, no side walls;
  flat background; the centroid threshold is calibrated on the same series, so in-field false alarms are ≈ 5 % by
  construction.
- **Reproduce.** `montecarlo fov samples/scenario_handheld.json` (≈ 3 min) → `samples/fov.csv`;
  `python samples/plot_fov.py` → `samples/fov.png`. Ensemble: `samples/evidence` family `fov` (64 seeds from the
  list `F128`, 10⁶ photons per mean map, 100 Poisson repeats per angle).
- **Tests.** `FieldOfViewTests` (square field, non-cyclic beyond the coded field with the centroid giving the side,
  flag at 10°, no cue at 18°, reproducibility).

### EV-03 — Field of view ÷ resolution = rank

- **Shows.** Nominally, field ÷ resolution = rank: a coarser cell or a shorter D widens the field and coarsens the
  resolution by the same factor, and a higher rank widens the nominal field at the same resolution. The **usable**
  field is limited by the 10 mm tungsten slab: at short D its 1 mm channels collimate oblique sources away. With
  12 × 12 × 1 mm pixels and S = 100 mm, the widest field with ≥ 90 % of the swept points localised within one
  resolution element found by the scan is **rank 11 / 1 mm cells / D = 30 mm: ±21.6 mm** (21.57 ± 0.09 mm, usable
  fraction 0.907 ± 0.007, N = 32; it passes the 90 % gate in **31 / 32** seeds), ~2.5× the original rank-7 / D-60
  field (±8.55 ± 0.03 mm, 0.929 ± 0.006). Rank 23 / 1 mm / D = 20 mm is 0.150 ± 0.003 usable (median error
  66 ± 1 mm); with the slab thinned to 0.5 mm at the same μ·t it is 0.764 ± 0.007. Decoding also **collapses** when
  the detector cannot hold about one period of the shadow (2 mm cells: every rank ≥ 13 at D = 20–80 mm is at most
  0.29 usable).
- **Limits.** One source plane (S = 100 mm), one detector (12 × 12 × 1 mm), one mask thickness; "usable" is a coarse
  gate (error < one resolution element), not a precision. The thinner-slab numbers trade leakage geometry for
  acceptance and were not optimised. Only the selected rows were repeated over seeds, not the whole rank × cell × D
  grid, so rank 11 / 1 / 30 is the scan's pick with an observed pass rate, not a re-certified global optimum.
- **Reproduce.** `montecarlo scan samples/scenario.json samples/scan.csv`, then `python samples/plot_scan.py` →
  `samples/scan_collapse.png`, `samples/scan_pareto.png`. Ensembles of the selected rows: `samples/evidence`
  families `scan` and `scanextra` (11 × 11 positions, 200 000 photons per point).
- **Tests.** none specific (a sweep of the pipeline that `PipelineTests` covers).

### EV-04 — Mask thickness: leak against collimation

- **Set-up.** `thickness` sweeps 2–32 mm of tungsten at a fixed budget (400 000 emitted photons, 60 Poisson repeats
  per radial point) and reports, per thickness, the largest source radius still localised within one resolution
  element in at least half the repeats; the pick is the thinnest thickness with the largest radius. **The defined
  thickness experiment is the wide-field recipe**: the reference lab geometry with D = 20 mm (resolution 6 mm, radial sweep to
  20 mm), where the collimation of off-axis rays matters. On the reference lab geometry itself (D = 60 mm) the test cannot choose:
  every thickness from 6 to 32 mm reaches the end of its radial sweep (8.87 mm in 64 / 64 seeds), 2 and 4 mm do not.
- **Shows.** Optimum **~10 mm** tungsten at 662 keV: the wide-field recipe picks 10 mm in 21 / 32 seeds, 14 mm in
  10 / 32 and 8 mm in 1 / 32. Thinner masks leak — at μ = 0.178 / mm the closed cells pass 28.8 % at 7 mm (35 % at
  5.9 mm) — and 2–4 mm localise nothing at the field edge (32 / 32), 6 mm only out to 1.7 mm [0, 5.0]. Thicker masks
  gain no field and collimate off-axis rays through the open channels: edge / centre efficiency 0.85 at 6 mm, 0.80
  at 8 mm, 0.74 at 10 mm → 0.30 at 32 mm (± 0.002, N = 32). A 10 mm, 1 mm-cell mask is a 10 : 1 channel that clips
  rays more than ~atan(1/10) ≈ 5.7° off its axis.
- **Limits.** One energy (662 keV) and one budget; "localised" is the coarse one-resolution-element gate. The earlier
  wide-field figures (edge / centre 0.56 at 8 mm → 0.25 at 32 mm) came from an input that was not kept and are
  replaced by this defined recipe.
- **Reproduce.** `montecarlo thickness samples/scenario.json samples/thickness.csv` → `samples/thickness_opt.png`
  (reference lab geometry); the wide-field recipe is the same command on a copy of `scenario.json` with
  `geometry.maskDetectorDistanceMm` = 20 — `samples/evidence` family `thickness_wide`.
- **Tests.** `PipelineTests.LeakyMask_AddsCountsVersusOpaque`, `MaskAttenuationTests`,
  `TransportInvariantTests.Mask_PassesItsOpenFractionPlusTheTungstenLeak`.

### EV-05 — Tapered (bevelled) channels

- **Shows.** A thick (25 mm) straight mask has edge / centre efficiency 0.825 ± 0.002; bevelling the channel walls by
  ~4° restores **0.991 ± 0.001** and raises centre efficiency 35.2 ± 0.2 %, with edge localisation unchanged
  (0.256 ± 0.007 → 0.262 ± 0.005 mm RMS; N = 64, 400 effective counts, 200 repeats).
- **Limits.** An idealised per-ray acceptance model — an upper bound. A real bevel removes tungsten from the webs
  near the faces, so the mask is not fully opaque everywhere.
- **Reproduce.** `montecarlo masktaper samples/scenario.json` → `samples/masktaper.png`.
- **Tests.** `MaskGeometryTests.TaperedChannels_WidenTheFovOfAThickMask`.

### EV-06 — Detector sampling of the mask shadow

- **Shows.** Each mask-cell shadow needs ≥ 1 detector pixel. At 0.8 pixel per cell (6 × 6) decoding aliases
  (20 ± 3 % failures, RMS 3.9 ± 0.3 mm); **~2 pixels per cell (16 × 16) is the sweet spot**: it reduces the 12 × 12
  error by about a third (16 / 12 RMS ratio, per seed, median 0.63 [0.49, 0.82]; 16 × 16 better in 114 / 128 seeds;
  RMS 0.96 ± 0.25 against 1.47 ± 0.27 mm); finer helps only marginally (20 × 20: 0.75 ± 0.25 mm).
- **Limits.** Fixed 12 mm detector width and one count budget (250 repeats per array); a single run can rank 16 × 16
  below 12 × 12 (14 / 128 seeds did), so the sampling rule is a tendency, not a guarantee per acquisition.
- **Reproduce.** `montecarlo array samples/scenario.json samples/array.csv` → `samples/array_sampling.png`.
  Ensemble: `samples/evidence` family `array` (N = 128).
- **Tests.** none specific.

### EV-07 — Count threshold and directional biasing

- **Shows — two count gates, not one.** On the reference lab geometry with a centred source: **below ~25 detected counts
  localisation collapses** (failures — error above 3 mm — 38 ± 3 % at 25 counts, 10 ± 2 % at 50); **sub-mm precision
  needs ~250 counts**: RMS 2.8 ± 0.3 mm at 50 counts (sub-mm in 0 / 128 seeds), 0.86 ± 0.18 mm at 100 (99 / 128) and
  0.44 ± 0.01 mm at 250 (128 / 128; N = 128). A source at the 8 mm edge needs more (0.54 ± 0.04 mm at 250).
- **Shows — directional biasing.** Photons aimed at the detector with an importance weight give the 4π efficiency:
  biased (10⁶ photons) ÷ isotropic (10⁸ histories) = **0.999 ± 0.006** (N = 32; within 1 % in 30 / 32 seeds — the
  spread is the isotropic run's own Poisson noise, ≈ 25 000 counts). With 100× fewer photons the biased estimate is
  also the more precise one (seed spread 0.11 % against 0.59 %).
- **Limits — ideal environment.** Every count is a source count: no ambient background, an ideal detector. The two
  gates are best-case values for **net source counts**; under ambient background the same precision needs more
  counts ([§1](#ideal-conditions-and-background)).
- **Reproduce.** `montecarlo noise samples/scenario.json samples/noise.csv` → `samples/noise_study.png` (count gates;
  `samples/evidence` family `noise`, N = 128). The 4π comparison is not part of `noise`: `samples/evidence` family
  `bias` runs the same scenario twice, the second time on a clone with `Source.DirectionalBiasing = false` and
  10⁸ histories (`samples/evidence/probe`, mode `bias`).
- **Tests.** `PipelineTests.Biasing_IsUnbiasedVersus4Pi`, `TransportInvariantTests.BiasedSource_MeanWeightIsTheDetectorSolidAngle`;
  the Poisson and direction samplers in `SamplingTests`.

### EV-08 — Sub-cell peak interpolation

- **Shows.** Taking the integer peak cell quantises the estimate (RMS = step/√12, independent of counts). A
  three-point tent fit to the correlation peak beats it at every grid step, more so on coarse grids: step 1.8 mm,
  0.527 ± 0.004 → **0.131 ± 0.002 mm**; step 2.4 mm, 0.664 (the same in 64 / 64 seeds) → 0.194 ± 0.002 mm (N = 64,
  source-position mean maps). Tent is the pipeline default.
- **Reproduce.** `montecarlo subcell samples/scenario.json samples/subcell.csv`; ensemble: `samples/evidence` family
  `subcell`.
- **Tests.** `SubCellTests`.

### EV-09 — Hand-held head: sensitivity and precision

- **Shows.** Geometric efficiency for Cs-137 **2.48 × 10⁻⁴** (2.479 ± 0.002, N = 32), against 1.00 × 10⁻⁴ for the
  reference lab geometry with GAGG (**2.48×**, 2.479 ± 0.003: detector area × stopping ≈ 2.35×, plus a solid-angle factor from
  the shorter D). Localisation floor **0.25 mm RMS on axis** (0.247 ± 0.002 mm at 5000 counts, 0.242 ± 0.004 at
  500), 0.53 mm at the 8 mm edge (0.531 ± 0.009); sub-mm with ≥ 250 detected counts on axis (median 0.27 mm
  [0.26, 0.28] — a skewed spread, mean 0.33 ± 0.15) and ≥ 500 at the edge (0.54 mm [0.54, 0.55]; N = 64). The
  GAGG reference geometry's floor is 0.35 mm (0.354 ± 0.001). At 1 MBq on axis at the lab distance, 250 counts take ~1.2 s.
  The URS §5 analytic count-rate estimate (geometry × open fraction × 662 keV stopping) gives 2.44 × 10⁻⁴ — within
  2 % of this MC value.
- **Reproduce.** `montecarlo samples/scenario_handheld.json` and `montecarlo samples/scenario_orig_gagg.json` print the
  efficiencies; `montecarlo noise samples/scenario_handheld.json samples/noise_handheld.csv` (and the same for
  `scenario_orig_gagg.json` → `noise_orig_gagg.csv`) gives the precision against counts. Ensembles:
  `samples/evidence` families `precise`, `noise_head`, `noise_orig`.
- **Tests.** none specific to the head.

### EV-10 — Several isotopes in one field

- **Shows.** Cs-137 at (5, 1), Co-60 at (−6, 3) and Co-57 at (0, −6) mm, emitted together in one non-cyclic run on the
  reference lab geometry, are each located < 1 mm: **0.55 / 0.27 / 0.93 mm** — the same grid cells in 64 / 64 seeds (a high-count
  mean map; grid-stable in this ensemble, not a deterministic quantity). The number of sources is known; the
  reconstruction is limited to the fully coded field.
- **Reproduce.** `montecarlo mixedfield samples/scenario.json` → `samples/mixedfield.png`.
- **Tests.** `MixedFieldTests.MultiSource_AllLocalized_InOneRun`, `.Superposition_IsActivityWeighted`.

### EV-11 — MLEM reconstruction

- **Shows.** A Poisson maximum-likelihood reconstruction with the finite mask as its forward model separates source
  pairs **2–3 mm apart** that cross-correlation merges (at 3 mm, valley depth 0.822 ± 0.004 against 0.170 ± 0.001;
  N = 64). On a single source it is non-negative (cross-correlation dips to −103.5 ± 0.4) and sharper (FWHM
  1.93 ± 0.05 vs 2.50 mm), with a localisation bias of 0.87 mm against 0.55 mm (the same in 64 / 64 seeds). At 1.5,
  2 and 3 mm MLEM resolves the pair and cross-correlation does not (64 / 64 seeds each; 1.5 × 10⁶ photons,
  80 iterations, resolved = valley depth > 0.25); cross-correlation needs ~3.5 mm (64 / 64). Below 2 mm the test is
  centred on the true positions and therefore optimistic.
- **Limits.** Ideal high-count study on the reference lab geometry; results depend on the iteration count. The system matrix
  samples pixel centres (no pixel-area integration).
- **Reproduce.** `montecarlo mlem samples/scenario.json samples/mlem.csv`.
- **Tests.** `MlemTests`.

### EV-12 — Ambient background and mask / antimask

- **Shows.** A diffuse background is not coded: it lands as a uniform pedestal. Localisation stays sub-mm up to a
  background equal to the detected source counts (BSR 1: 0.76 ± 0.13 mm), then degrades (BSR 2 → 4: RMS
  2.9 ± 0.3 → 7.3 ± 0.2 mm, failures 10 ± 2 % → 66 ± 4 %; N = 64). **Calibrated background subtraction against a
  physical two-exposure mask / antimask is a trade, not an equivalence.** At equal total acquisition time (the study
  splits the 400 detected source counts, ≈ 2.8 per pixel, between the two exposures) the antimask gives **~20–30 %
  lower RMS at every background level**: 0.38 vs 0.31 mm (calibrated vs antimask, no background), 0.46 vs 0.38
  (2 background counts per pixel), 0.55 vs 0.44 (4 / pixel), 0.91 vs 0.63 mm (8 / pixel; N = 128). Both stay sub-mm
  through 4 / pixel with failures (error > 3 mm) at most one in 200 repeats per seed (calibrated: in 7 / 128 seeds;
  antimask: none). The decision against a rotating mask
  (D-19) rests on the mechanism's cost and mechanical stability, not on the two methods being equal. Neither removes
  a directional background source: it is coded like the signal and imaged as a second peak. A background graded
  across the face is harmless while the source wins, then drags the estimate toward its strong side at the knee.
  The subtraction assumes a static, measurable background; a background that changes during the acquisition favours
  concurrent two-exposure subtraction (with a static mask / antimask pair, not a moving part).
- **Reproduce.** `montecarlo background samples/scenario.json`; `montecarlo antimask samples/scenario.json
  samples/antimask.csv`; `montecarlo antimask-scene samples/scenario.json`. Ensembles: `samples/evidence` families
  `background` (N = 64) and `antimask` (N = 128).
- **Tests.** `BackgroundTests`, `DecoderInvariantTests.Cyclic_RejectsAModestPedestal_ButNotAnUnlimitedOne`.

### EV-13 — Depth-of-interaction parallax

- **Shows.** The decoder back-projects the crystal's front face, but gammas interact at depth; for an oblique ray this
  shifts the estimate. The shift is ≈ 0 on axis (|shift| < 0.01 mm) and grows with crystal thickness off axis: at
  9 mm off axis, 0.093 ± 0.002 mm for a 5 mm crystal, **0.299 ± 0.001 mm for 30 mm** (N = 64; 2.5 × 10⁶ rays, paired
  maps) — below the grid step, but a floor under sub-cell precision at the field edge.
- **Reproduce.** `montecarlo doi samples/scenario.json samples/doi.csv`.
- **Tests.** `DoiParallaxTests`.

## 4. Energy and isotopes

### EV-14 — Event positioning: total-energy window + largest deposit

- **Shows.** A 662 keV gamma often Compton-scatters between pixels. Windowing the **total** deposit and placing the
  event at the largest-deposit pixel keeps **12 %** of the ideal counts against **6 %** for per-pixel windows (2.0×),
  with lower RMS and fewer failures at a budget of 400 ideal effective counts (3.6 ± 0.3 vs 5.5 ± 0.3 mm;
  18 ± 3 % vs 40 ± 4 % failures; N = 64). The comparison, not the absolute RMS, is the result (only ~50 / ~25
  effective counts remain at that budget).
- **Reproduce.** `montecarlo compton samples/scenario.json` → `samples/compton_strategies.png`.
- **Tests.** `ComptonTests.Argmax_RecoversMoreCountsThanPerPixelWindow`,
  `TransportInvariantTests.ComptonCascade_NeverDepositsMoreThanThePhotonEnergy`.

### EV-15 — Isotope separation: the spatial limit and Compton stripping

- **Shows — the spatial limit.** In a 662 keV window with 1 mm GAGG pixels, Co-60 downscatter is **~55 %** of the
  window's counts (55.3 ± 0.5 %, N = 64) in an equal-photon-budget demonstration (not an activity-normalised field).
  Because that downscatter is coded from Co-60's direction, decoding places it at the Co-60 position — but the
  weaker Cs-137 peak survives only while Co-60 is ≤ 2× stronger. With Cs-137 at (4, 0) and Co-60 at (−5, 3) mm and a
  per-pixel window, the Cs-137 source stays sub-mm at Co × 2 in **126 / 128** seeds (0.47 mm; the other two lose it,
  8.7 mm); at Co × 4 and × 8 it is lost in 128 / 128 (error 8.0–8.7 mm). In the co-located `mixediso` scene at Co × 8
  the Cs-137 error is 8.75 mm in 57 / 64 seeds (8.0 mm in the rest). The reconstructions are weighted mean maps:
  their effective counts are not observed events.
- **Shows — the spectral lever.** Per-pixel Compton stripping subtracts `R ×` each pixel's Co-60 photopeak count from
  the 662 keV window, with R = downscatter-into-662 ÷ Co photopeak, then decodes. It recovers a **co-located**
  Cs-137 count: R = 0.469 ± 0.001, error 0–1 % co-located and 4–5 % separated (N = 64; `compton-strip`, where R is
  taken from the simulation's true contamination map — an oracle); in a true mixed field, with R calibrated from a
  Co-only run, R = 3.99 ± 0.04 and the stripped count is 3–4 for 3 true counts — error median 13 % [8, 20]
  co-located and 18 % [13, 23] separated, against ~1160 % unstripped (N = 64; `mixedstrip`).
- **Limits.** Not blind separation: it needs the lines known in advance and a calibrated ratio per geometry. R is a
  single global scalar applied per pixel; the aggregate count can look right while local residuals remain, and those
  are not checked.
- **Reproduce.** `montecarlo compton`, `compton-strip`, `mixediso`, `mixedstrip`, each with `samples/scenario.json`
  → `samples/compton_contamination.png`, `compton_strip_combined.png`, `mixediso.png`, `mixedstrip.png`. The
  activity-ratio scene (× 1 / 2 / 4 / 8, 4 × 10⁶ photons, 0.4 mm grid) is `samples/evidence` family `spatial`
  (`samples/evidence/probe`, mode `spatial`, N = 128).
- **Tests.** `ComptonTests.Contamination_IsImagedAtTheContaminantSource_NotTheTarget`,
  `.Stripping_RecoversCsCount_EvenCoLocated`; `MixedFieldTests.MixedField_ThroughEnergyWindow_SeparatesIsotopesSpatially`
  (at Co × 2), `.ComptonStripping_RecoversCoLocatedCsCount`.

### EV-16 — One gain for 122–1332 keV

- **Shows.** With the ADC gain set for Cs-137, Co-60 saturates. Setting it so 1332 keV sits under full scale resolves
  Co-60's two lines and leaves the 122 keV line unhurt (FWHM 15.9 ± 0.2 → 16.3 ± 0.2 %, 32 field seeds of the
  synthetic stimulus, NumPy generator, noise fixture fixed): at 12 bits one gain covers the whole span. A low line
  sits on every higher line's Compton continuum, so its window is contaminated by downscatter.
- **Reproduce.** `python rtl/multi_isotope_study.py` → `rtl/multi_isotope.png`; seed ensemble:
  `python samples/evidence/rtl_seeds.py multi <seed>` (`samples/evidence` family `rtl_multi`).
- **Tests.** none (a Python study).

### EV-17 — Energy-resolution budget and pulse shapers

- **Shows.** With a realistic front end (6 % intrinsic at 662 keV scaling as 1/√E, 3 keV electronic noise, finite
  rise, 14-bit / 125 MSPS ADC), the 662 keV photopeak is **6.2 % FWHM** (6.16 ± 0.25 %, N = 32 timing / noise seeds
  of a 1500-event MC stream); with the intrinsic term alone it is 5.92 ± 0.25 %, so the intrinsic width dominates.
  The ADC's own noise is negligible (removing it changes the width by 0.002 ± 0.002 percentage points). A run with
  intrinsic smearing off gives 1.70 ± 0.06 %, but it selects deposits above 620 keV and so still carries Compton-tail
  width — it is not the electronic noise alone. The C# photo-electron model gives 5.7 % for GAGG (a deterministic
  budget). Among shapers (electronic noise only, isolated pulses): cusp 1.01 ± 0.03 % < CR-RC⁴ 1.09 ± 0.04 % <
  trapezoid 1.69 ± 0.06 % (cusp below CR-RC⁴ in 32 / 32 seeds); at 2 Mcps the wider trapezoid keeps 42 ± 2 % of
  events within ±5 % against 69 ± 3 % for CR-RC⁴ and 71 ± 2 % for cusp. The trapezoid and CR-RC⁴ shapers and
  the baseline restorer exist in SystemVerilog, bit-exact to the shared integer reference; pipelining the
  trapezoid raises its Fmax from 59 to 119 MHz (Lattice ECP5, nextpnr timing — a proxy for the Xilinx target).
- **Reproduce.** `montecarlo eventstream samples/scenario.json 500 1500`, then `python rtl/frontend_study.py` and
  `python rtl/shaper_compare.py`; `montecarlo frontend samples/scenario.json`; `python rtl/run_cocotb.py`;
  Fmax: `rtl/README.md` → `rtl/fmax_ecp5.png`. Seed ensemble of the widths:
  `python samples/evidence/rtl_seeds.py frontend <seed>` (`samples/evidence` family `rtl_frontend`; the energy
  calibration stays the scripts' fixture, event timing and noise follow the seed).
- **Tests.** `FrontEndTests`, `WaveformTests`; the cocotb benches in `rtl/test_*.py`.

### EV-18 — Per-channel gain calibration

- **Shows.** Position is robust to per-pixel gain scatter (the balanced MURA decode suppresses smooth multiplicative
  distortion): the centred source's RMS goes from 0.49 ± 0.02 mm (uniform) to 0.55 ± 0.07 mm at a 40 % gain σ plus a
  60 % gradient, without flood correction (N = 64 transport seeds). Energy is not: a 15 % gain σ smears the summed
  photopeak from 7.3 % to 18.4 % FWHM (± 0.1, N = 32 stimulus seeds); per-channel calibration restores it
  (7.29 ± 0.06 %).
- **Limits.** Both results are conditional on one fixed non-uniformity pattern (the scenario's uniformity seed; the
  RTL study's gain / decay map) — the seeds sweep counting noise, not the manufactured pattern. One centred source.
- **Reproduce.** `montecarlo uniformity samples/scenario.json samples/uniformity.csv` →
  `samples/uniformity_robustness.png`; `python rtl/pixel_uniformity_study.py` → `rtl/pixel_uniformity.png`.
  Ensembles: `samples/evidence` families `uniformity` and `rtl_pixel`.
- **Tests.** none specific.

### EV-19 — Crystal materials

- **Shows.** Efficiency for a 10 mm crystal follows density: NaI 0.59 < LaBr3 0.76 ≈ CeBr3 0.77 < GAGG 1.00 <
  LYSO 1.13 < BGO 1.23 (× 10⁻⁴; seed spread ≤ 0.2 %, N = 64). GAGG's weakness is not efficiency but resolution and
  afterglow, hence GAGG:Ce,Mg
  (co-doped: low afterglow, non-hygroscopic, no internal radioactivity) or CeBr3 (~4 %, fast, but hygroscopic and
  costly). LYSO carries Lu-176 internal background.
- **Reproduce.** presets in `samples/materials/*.json`, e.g. `montecarlo samples/materials/CeBr3.json`;
  `samples/crystal_materials.png`.
- **Tests.** `CrystalMaterialTests`, `PipelineTests.DenserCrystal_DetectsMore`,
  `TransportInvariantTests.Crystal_StopsOneMinusExpOfMuTimesSlantPath`.

### EV-20 — Ir-192 in the engine

- **Shows.** Ir-192 is modelled from the evaluated decay data (nine gammas ≥ 1 % per decay, 2.137 γ / decay). On axis
  it images like Cs-137 (0.32 mm error, ghost margin 1.154 vs 0.35 mm, 1.157; seed spread ≤ 0.002, N = 32). Per
  emitted photon the hand-held head detects 2.62 × 10⁻⁴ for Ir-192 against 2.48 × 10⁻⁴ for Cs-137 (± 0.002); the URS
  §5 analytic estimate, which uses 662 keV
  stopping for every line, under-states Ir-192 by ~7 %: the reference source RS-1 gives ≈ 1.07 Mcps at 10 mSv/h at
  the device and ≈ 1.07 kcps at 10 µSv/h (the analytic estimate says 1.0 / 1.0).
- **Limits.** Ir-192's cascade summing is not modelled.
- **Reproduce.** `montecarlo samples/scenario_ir192.json` (imaging); `montecarlo samples/scenario_handheld_ir192.json`
  and `montecarlo samples/scenario_handheld.json` print the two efficiencies.
- **Tests.** `Ir192Tests`.

## 5. Rate and dose

### EV-21 — Count-rate capability and afterglow

- **Shows.** Crystal decay time sets rate capability. Plain GAGG's afterglow buries the baseline: **4 ± 2 %** of
  events recovered at 1 Mcps (4.3 ± 1.9 %, 128 stimulus seeds; the earlier 0.2 % was one unrepresentative run).
  GAGG:Ce,Mg follows its 55 ns decay; CeBr3 keeps 86 ± 1 % at 2 Mcps. A simple peak detector loses efficiency to
  pile-up (45 ± 1 % at 1.5 Mcps, N = 32), which is the ~1 Mcps practical limit of the front end. The stimulus is a
  synthetic NumPy pulse train through the actual peak-detector RTL.
- **Reproduce.** `python rtl/material_rate_study.py` → `rtl/material_rate.png`; `bash rtl/run.sh` →
  `rtl/rtl_lowrate.png`, `rtl/rtl_highrate.png`. Seed ensembles: `python samples/evidence/rtl_seeds.py material|peak
  <seed>` (`samples/evidence` families `rtl_material`, `rtl_peak`).
- **Tests.** none (Python studies).

### EV-22 — Dead time

- **Shows.** On the timed event stream with τ = 1 µs, the recorded rate follows the analytic curves: a
  non-paralysable system saturates toward 1/τ (912.5 ± 0.7 kcps at 10 Mcps true); a **paralysable** one peaks at
  1 Mcps (366.4 ± 0.8 kcps) and then collapses (500 ± 140 cps at 10 Mcps; N = 64) — it counts *fewer* events as the
  rate rises. The live fraction (recorded ÷ true) is the correction.
- **Reproduce.** `montecarlo deadtime samples/scenario.json samples/deadtime.csv`; ensemble: `samples/evidence`
  family `deadtime`.
- **Tests.** `DeadTimeTests`.

### EV-23 — Dose rate from the detector spectrum

- **Set-up.** Truth: the unscattered fluence at the detector × ICRP 74 H*(10) conversion coefficients. Response: MC
  of a point source 1 m away through the mask and crystal; every event's total deposit, smeared by the energy
  resolution, cut at 30 keV. A weighting G(E) is fitted on 14 frontal energies (50–1500 keV) and then checked without
  refitting.
- **Shows.** Means over 64 seeds, G(E) refitted for each seed; every ratio's seed spread is ≤ 0.004.

  | | Estimate ÷ truth |
  |---|---|
  | frontal, held-out 70 / 122 / 250 / 662 / 1173 / 1332 keV | 1.04 / 0.90 / 1.12 / 0.92 / 1.04 / 1.06 |
  | reference sources, frontal: Am-241 / Co-57 / Ir-192 / Cs-137 / Co-60 | 1.06 / 0.91 / 1.00 / 0.91 / 1.05 |
  | 662 keV at 0 / 2.5 / 5 / 10 / 20 / 45° | 0.92 / 0.82 / 0.65 / 0.41 / 0.22 / 0.08 |
  | 60 keV at 0 / 2.5 / 5 / 10 / 20° | 1.07 / 0.79 / 0.44 / 0.11 / 0.00 |
  | Cs-137 at 1 / 10 / 100 mSv/h, paralysable τ = 1 µs | 0.86 / 0.53 / 0.004 |
  | same, live-time corrected | 0.91 / 0.91 / 0.91; fails past ~150 mSv/h (live fraction < 10⁻³) |

  **Frontally, one G(E) keeps every tested energy and every reference source within ±13 %** (largest deviation
  12.5 ± 0.3 %, at 250 keV; within 13 % in 63 / 64 seeds) — a statement about this frontal energy grid, not an
  accuracy specification for every energy and angle. Off axis the imaging head is a
  collimator: 10° off axis it reads 0.11 (60 keV) to 0.66 (1250 keV) of the dose, ~0 at low energy past 20°. Above
  ~150 mSv/h the live fraction itself keeps falling and is the over-range signature. This result led to the
  decision to read dose from a separate small counter (D-37); that counter is not simulated.
- **Limits.** Front plate only (no side or rear walls), so oblique readings are an upper bound; no room or body
  scatter; one distance; analytic paralysable dead time.
- **Reproduce.** `montecarlo dose samples/scenario_handheld.json` (≈ 10 s) → `samples/dose_*.csv`;
  `python samples/plot_dose.py` → `samples/dose.png`; ensemble: `samples/evidence` family `dose`.
- **Tests.** `DoseTests`.

## 6. Head design

### EV-24 — Mass, envelope and form

- **Shows.** The tungsten **shield is ~2/3–3/4 of the mass** and the master weight knob (6 mm ≈ 1.5 kg … 12 mm ≈ 3 kg);
  crystal and mask are a few tens of grams. A 12 → 20 mm detector moves the build from ~2.7 to ~3.4 kg through the
  shield. Envelope: ~206 mm axial × 48 × 48 mm, ~0.47 L at a 12 mm shield, ~0.33 L / ~2 kg at 8 mm. The shield puts the
  centre of mass ~89 mm behind the muzzle (12 mm build), so the grip goes under it and the battery is a rear
  counterweight.
- **Reproduce.** `python samples/handheld_design_study.py` → `samples/handheld_design.png`,
  `samples/handheld_envelope.png`; `python samples/hardware_concept.py` → `samples/hardware_concept.png`.

### EV-25 — Shield thickness against background energy

- **Shows.** Side, top, bottom and rear background is uncoded noise; the thickness that reaches the localisation floor
  depends on its energy. The study's knee rule takes the thinnest wall (0–30 mm grid) whose RMS without background
  subtraction is within 1.15× the lowest RMS of the sweep, below 3 mm, with < 10 % failures (400 source counts, an
  unshielded background of 20 counts per pixel); over 128 seeds it picks, for the 5-sided shield:
  scattered ~250 keV background **8 mm W (0.98 kg)** in 109 / 128 seeds, 10 mm (1.38 kg) in 19; a 662 keV field
  **20 mm (4.5 kg)** in 100 / 128, 25 mm (6.8 kg) in 28; **Co-60 (~1.25 MeV) 25 mm (6.8 kg) in 83 / 128 or 30 mm
  (9.8 kg) in 45** — several times the ≤ 2.5 kg mass of PR-PHY-01, so not carriable. With calibrated background
  subtraction the 662 keV RMS at 12–15 mm (0.47 ± 0.03 / 0.43 ± 0.02 mm) already matches the unsubtracted RMS at the
  20 mm pick (0.43 ± 0.01 mm); the knee rule is applied to the unsubtracted column only. Concentrating the same
  scattered leak in the side walls leaves the pick unchanged in 88 / 128 seeds (thicker by 2 mm in 26, thinner in
  14), so no fixed increment is claimed for it.
- **Limits.** Narrow-beam, uncollided attenuation (no buildup); first-order thicknesses on a 2–5 mm grid; the
  masses are the study's 5-sided shield model.
- **Reproduce.** `montecarlo shield samples/scenario.json` → `samples/shield.csv`; `python samples/plot_shield.py` →
  `samples/shield.png` (curves: seed 12345; picks annotated from the ensemble). Ensemble: `samples/evidence` family
  `shield` (N = 128).
- **Tests.** `BackgroundTests` (side-leak profile).

### EV-26 — Scene-camera parallax

- **Shows.** The scene camera cannot sit on the mask axis, so a baseline b misregisters the overlay by
  Δθ = arctan(b/z): b = 40 mm gives 7.6° at 0.3 m and 2.3° at 1 m — larger than the ~1° gamma resolution for
  z < ~2.2 m. A measured range z and the known b correct it; one camera axis keeps the correction one-dimensional.
- **Reproduce.** `python samples/camera_parallax_study.py` → `samples/camera_parallax.png`.

### EV-27 — Electronics heat and hand motion

- **Shows.** Heat scales with channel count: a monolithic readout (~30 channels, ~4 W) runs passively at +8 °C; a pixel
  readout (256 channels, ~12 W) needs fins (+24 °C passive). The tungsten shield is a heat sink of 335 J/K
  (4 W for 60 s → +0.7 °C). Pointing tolerance ≈ 0.83° (half a cell shadow at D = 55 mm); free-hand drift of ~1°/s
  smears past ~0.8 s, so sources below ~240 cps need help — per-event IMU de-rotation with camera visual-inertial
  odometry against gyro drift, or a brace.
- **Reproduce.** `python samples/thermal_motion_study.py` → `samples/thermal_motion.png`.

### EV-28 — SiPM gain drift and its compensation

- **Shows.** For the Hamamatsu S13360-3050CS at fixed bias the gain tempco is −1.8 %/°C: a ±10 % energy window loses
  10 % of its counts after only **ΔT = 3.2 °C**. Temperature-compensated bias brings it to −0.25 %/°C (holds to
  ΔT ≈ 25 °C); with an LED pulser −0.17 %/°C (ΔT ≈ 36 °C). The crystal's own light-yield drift (~−0.15 %/°C) is
  invisible to both — only a spectral line catches it. A built-in reference source would track to σ ≈ 0.3 %; natural
  K-40 is too weak (σ ≈ 2.5 %). Global gain drift leaves localisation unchanged; it is an energy-window problem.
- **Reproduce.** `python samples/sipm_thermal_study.py` → `samples/sipm_thermal.png`;
  `python samples/sipm_stab_trend.py` → `samples/sipm_stab_trend.png`.

### EV-29 — Thermal drift during an acquisition

- **Shows.** With ambient +3 °C and self-heating +8 °C (centre hotspot) during one acquisition: without bias
  compensation the photopeak counts droop **100 → 90.6 %** and a flood residual of 6.7 % CoV appears; with 90 %
  compensation 99.9 % and 0.06 % (seed spread ≤ 0.01 %). **Localisation stays at the ~0.5 mm floor either way**
  (0.48 ± 0.03 mm uncompensated, 0.45 ± 0.03 mm compensated; N = 64). Flood correction taken at t = 0 cannot follow a
  time-varying drift; only the compensation loop can. The efficiency is normalised to t = 0 within each run.
- **Reproduce.** `montecarlo thermal samples/scenario.json samples/thermal`; ensemble: `samples/evidence` family
  `thermal`.
- **Tests.** `ThermalDriftTests`.

## 7. Manufacturing

### EV-30 — Mask fabrication tolerance

- **Shows.** A seeded per-cell machining error (hole placement σ, hole size, blocked cells, drill wander through the
  10 mm slab), decoded with the ideal pattern. Over 128 transport seeds the mean RMS is 1.35 mm for the ideal mask,
  1.29 / 1.42 / **1.52** / 1.93 / 3.65 mm at σ = 10 / 20 / **40** / 80 / 160 µm. **Gate (PR-MFG-01): seed-mean RMS
  ≤ 1.25 × the seed-mean ideal RMS** — 40 µm passes (1.13×), 80 µm fails (1.43×; 160 µm 2.71×). The
  peak-to-sidelobe ratio falls 4.38 → 4.31 → 4.18 → 3.77 (ideal, 40, 80, 160 µm; ± 0.06). The ideal RMS itself
  spreads widely from seed to seed (0.42 mm SD), so a per-seed test does not separate the tolerances: "within 2× of
  that seed's ideal RMS" holds in 108 / 128 seeds at 40 µm and still in 100 / 128 at 80 µm.
- **Limits.** Conditional on the study's six fixed manufactured patterns (seeds 100–105): the outer seeds sweep the
  transport and counting noise, not the pattern population, so no tolerance for an arbitrary manufactured mask is
  claimed. Warping (a global bow) is not modelled.
- **Reproduce.** `montecarlo maskfab samples/scenario.json samples/maskfab.csv`; ensemble: `samples/evidence`
  family `maskfab` (N = 128); the gate is computed from the per-seed `rms_mm` rows in
  `samples/evidence/results/aggregate.csv`.
- **Tests.** `MaskFabricationTests`.

### EV-31 — Mask–detector alignment

- **Shows.** An in-plane mask offset biases the source estimate by the magnification (D+S)/D: 1 mm → 2.62 ± 0.01 mm
  at the lab geometry (2.66 ± 0.04 mm change from the aligned estimate; N = 64), so **~0.4 mm registration** holds a
  ~1 mm bias budget. Spacing (2 mm → 0.38 ± 0.06 mm change) and roll (2° → 0.13 ± 0.03 mm) are far more forgiving.
- **Reproduce.** `montecarlo align samples/scenario.json samples/align.csv`; ensemble: `samples/evidence` family
  `align`.
- **Tests.** `AlignmentTests`.

### EV-32 — Dead and hot pixels

- **Shows.** On the 12 × 12 array, replacing each flagged pixel with the mean of its good neighbours keeps the floor
  at **0.48 ± 0.04 → 0.51 ± 0.03 mm** for up to 8 % bad pixels (hot pixels at 5× the mean; N = 64); unrepaired, it
  reaches 1.15 ± 0.12 mm at 8 %.
- **Limits.** Conditional on the study's eight fixed defect maps (seeds 200–207); the outer seeds do not draw new
  maps.
- **Reproduce.** `montecarlo defects samples/scenario.json samples/defects.csv`; ensemble: `samples/evidence`
  family `defects`.
- **Tests.** `DetectorDefectTests`.

## 8. Range

### EV-33 — Source distance (depth)

- **Shows.** The shadow's magnification depends on distance, so refocusing estimates it: S = 40 → 39.0, 100 → 97.4,
  150 → 140.0, 200 → 214.0 mm (seed spread ≤ 0.12 mm, N = 64). The focus curve's half-height width grows from 50 mm
  at 40 mm to 240 mm at 150 and 200 mm — there it fills the 20–260 mm search window, so 240 mm is a censored lower
  bound, not a resolved width. With noise the near field converges to sub-mm (on axis, 3000 counts: 0.33 ± 0.05 mm),
  the far field floors at ~10 mm (10.2 ± 0.2 mm at S = 150 mm). Lateral position stays at ~mm while depth is
  uncertain. On the viewer's sharp optics (rank 13, 0.7 mm cell — an 18 mm mosaic —, D = 80 mm, 30 × 30 × 0.6 mm)
  the depth width grows about as **z^1.74** (an empirical power fit over six distances, 150–1400 mm; 1.743 ± 0.001,
  N = 32), and the ±10 % range (width ≤ 0.2 z) is **< 0.15 m**: already the first sampled distance fails it
  (width / z = 0.236 ± 0.001; the analysis reports 150 mm because it returns the first sampled distance when that
  one fails); the ±20 % range is 196 mm. A coded aperture gives direction, a rangefinder gives distance. On the
  viewer's list-mode path at those optics (500 µCi, 60 s, source at 300 / 500 / 700 mm, 0 / 15 / 30 mrad; medians over
  32 seeds) the sharpest plane of a manual 150–950 mm scan is biased by +60 / +80 mm on axis at 300 / 500 mm, between
  +60 and −40 mm at 15–30 mrad, and by −120 to −140 mm at 700 mm off axis; the current focus-sweep service — a
  different estimator — by +51 / +69 mm on axis and −24 to −44 mm at 15–30 mrad, with its far edge censored at
  700 mm in 20–32 / 32 seeds (a lower bound only) and more than one mode in every curve (32 / 32). The cause is the
  focus score itself — a heuristic on a decode that does not model pixel area and slab transmission; a forward
  likelihood with a known bearing reduces the bias to a few mm, but no unbiased estimator without a known bearing
  has been shown. Depth stays weak and bias-limited in the far field; no fusion verdict is drawn.
- **Reproduce.** `montecarlo depth`, `depth-joint`, `depth3d`, `depthdesign`, each with `samples/scenario.json` →
  `samples/depth_estimation.png`, `depth_joint.png`, `depth3d.png`. The sharp-optics width and the viewer path have
  their own headless recipes: `samples/evidence/probe`, modes `depthsharp` and `viewer` (`samples/evidence` families
  `depthsharp`, `viewer`).
- **Tests.** `DepthTests`, `DepthDesignTests`, `RangeLocalizationTests`.

## 9. Model history

Numbers above are from the current engine. Changes that moved earlier results:

| Date | Change | Effect on the evidence |
|---|---|---|
| 2026-10-02 | Every MC quote re-measured over a seed ensemble (32–128 outer seeds per family, same engine) instead of one realisation at seed 12345; the author decided each changed interpretation with its measured basis (D-19, D-39, D-40) | All MC entries now quote mean ± SD, median [quartiles] or k / N (§1). Moved values and withdrawn statements are listed in the table below, with the reason. The row below this one ("no entry moved beyond its seed spread") compared two generators at five to twenty seeds; the larger ensembles show that several single-seed quotes sat at the edge of their spread or outside it. |
| 2026-10-02 | Random-number generator replaced: xoshiro256** seeded by SplitMix64 instead of the seeded legacy `System.Random`, whose streams were affine in the seed (nearby seeds gave shifted copies of one stream); Studio's per-event energy smear keyed by (seed, index); the studies' Poisson realisations drawn from their own stream instead of restarting the mean-map transport stream | **No entry moved beyond its seed spread**: every MC reproduce command above was run with both generators at five seeds (twenty where a difference was flagged); the transport itself is unchanged at 0.01–0.1 %. The figures quoted above are single-seed realisations (seed 12345); the commands now print different digits within those spreads (e.g. EV-14 per-pixel RMS 5.65 → 5.42 mm, EV-01 reference-geometry sweep 282 / 148 → 273 / 144, EV-15 mixed-field R 3.97 → 4.08). Only an unquoted curve moved: the shield study's RMS at its background knee (Co-60, 8 mm W) rose from a median 0.79 to 0.96 mm over 20 seeds — `samples/shield.png` is regenerated; EV-25's thicknesses keep their seed distributions. |
| 2026-10-02 | Depth from focus measured on the viewer's list-mode path at its default optics (1 m standoff) | EV-33 extended: near-field estimate reproducible but biased by tens of mm, far field a lower bound only; earlier near-field figures unchanged |
| 2026-10-02 | Configuration scan re-run with the engine in git (10 mm ray-marched tungsten slab); the earlier figures predated the slab model | EV-03: the max-FOV pick changes from rank 23 / 1 mm / D 20 ("±64 mm, 96 %", not reproducible: 0.149) to rank 11 / 1 mm / D 30 (±21.5 mm, 90 %); "~7×" becomes ~2.5×; high ranks collapse at short D by collimation. |
| 2026-10-01 | Crystal attenuation and photoelectric share taken from tabulated cross sections per material, replacing one 662 keV μ and one power law for every crystal (which absorbed too much, too photoelectrically) | EV-14: per-pixel window 24 → 6 % of ideal counts; EV-15: Co-60 share of the 662 keV window 22 → 55 %, Cs-137 lost from Co × 4 (was shown at × 8); EV-09: hand-held efficiency 2.65 → 2.48 × 10⁻⁴; EV-19: same ranking, lower values. All PRS numbers were re-measured on the new model. |
| 2026-10-01 | Ir-192 added; tungsten μ(E) points at 200–600 keV | EV-20; earlier lines unchanged |
| 2026-10-01 | Field-distance FOV study and dose model added | EV-02, EV-23 replace the estimates the PRS carried before (usable field ±5°, dose "not modelled") |
| 2026-07 | Tent sub-cell interpolation made the pipeline default | lowered localisation floors (EV-08, EV-09) |

**Seed-ensemble refresh (2026-10-02): what moved and why.** Old = the single-realisation quote; new = the ensemble
quote above. Values that stayed within rounding are not listed.

| Entry | Old | New | Reason / decision basis |
|---|---|---|---|
| EV-12 | calibrated subtraction "matches" mask / antimask "to 0.07 mm", so a rotating mask is not needed | at equal time the antimask has ~20–30 % lower RMS at every background level (0.38 / 0.31 … 0.91 / 0.63 mm, N = 128); both sub-mm through 4 counts / pixel | 0.07 mm was one run's largest gap between the two methods; over 128 seeds that largest gap is 0.35 ± 0.20 mm, and the mean RMS favours the antimask at every level. D-19 stands on a new basis: the rotation mechanism's cost and mechanical stability outweigh the gain (author, 2026-10-02) |
| EV-04 | optimum "8–10 mm"; edge / centre 0.56 at 8 → 0.25 at 32 mm; "below ~7 mm leak > 35 %" | ~10 mm (10 mm in 21 / 32, 14 mm in 10 / 32, 8 mm in 1 / 32) from the defined wide-field recipe (D = 20 mm); edge / centre 0.80 at 8 → 0.30 at 32 mm; 28.8 % at 7 mm, 35 % at 5.9 mm | the reference-geometry command cannot pick (every 6–32 mm row reaches its sweep end, 64 / 64); the old wide-field input was not kept; the leak line was arithmetic (exp(−0.178 × 7) = 0.288). Author: "수치는 분명히 하는 게 좋겠지" (D-40) |
| EV-30, PR-MFG-01 | RMS "holds the ideal floor (~0.95 mm) to σ ≈ 40 µm"; requirement "within ~2× the ideal floor" | gate: seed-mean RMS ≤ 1.25 × seed-mean ideal RMS; 40 µm 1.13× (pass), 80 µm 1.43× (fail); ideal 1.35 mm | the ideal RMS spreads 0.42 mm between seeds and the per-seed 2× test passes 108 / 128 at 40 µm and 100 / 128 at 80 µm, so it does not identify 40 µm; conditional on six fixed patterns (D-39) |
| EV-07 | "from ~50 counts it is sub-mm"; biasing "within ~1 %, ~100× fewer photons" (not produced by `noise`) | collapse below ~25 counts; sub-mm needs ~250 (RMS 2.8 / 0.86 / 0.44 mm at 50 / 100 / 250; sub-mm 0 / 99 / 128 of 128); biasing ratio 0.999 ± 0.006 (N = 32) from its own recipe | the coarse 3 mm success gate and sub-mm precision are different gates (D-40) |
| EV-03 | "≥ 90 %" as a property of rank 11 / 1 / 30 | passes the 90 % gate in 31 / 32 seeds; ±21.6 mm | an observed gate on the selected rows; the whole grid was not repeated (D-40) |
| EV-06 | 16 × 16 "about halves" the 12 × 12 error; 6 × 6 21 % failures | reduces it by about a third (ratio median 0.63 [0.49, 0.82], better in 114 / 128); 20 ± 3 % | seed 12345 had 16 × 16 worse than 12 × 12 (ratio 1.20) — an outlier (D-40) |
| EV-25 | scattered ~8 mm (~1.2 kg); 662 keV ~20 mm (~5 kg); Co-60 ~30 mm (~11 kg); side-wall leak moves 6 → 8 mm | picks with frequencies: 8 mm 0.98 kg (109 / 128); 20 mm 4.5 kg (100 / 128); Co-60 25 mm 6.8 kg (83 / 128) or 30 mm 9.8 kg (45 / 128); side-wall leak: no change in 88 / 128 | single-seed picks; "not carriable" is judged against PR-PHY-01's 2.5 kg, so the conclusion stands (D-40) |
| Cascade summing (no EV entry) | log-log slope 2.16 (one run) | pooled Poisson fit over 128 seeds incl. zero-count rows: 2.00 ± 0.03; geometry implies 1.99–2.00 | per-seed slopes drop the ~2.7 of 7 distances with no sum events and scatter (1.71 ± 0.46) (D-40) |
| EV-33 | resolution "about as z^1.5"; ±10 % range "~0.15 m"; width "~240 mm beyond 150 mm"; viewer bias "+60…+80 / −20…−140 mm" | z^1.74 (empirical, six distances); range < 0.15 m (150 mm is the first sampled distance, which already fails); 240 mm is censored by the search window; manual scan and current focus-sweep service quoted separately | 150 mm was a clamp of the range analysis; two estimators had been merged in one quote (D-40) |
| EV-17 | 6.4 % FWHM = √(6.0² + 2.3²); shapers 1.02 / 1.10 / 1.74 %; 40 / ~65 % at 2 Mcps | 6.16 ± 0.25 %; 1.01 / 1.09 / 1.69 %; 42 / 69 / 71 %; the 1.70 % "intrinsic off" width is not called electronic noise | it includes Compton-tail width from the > 620 keV selection (D-40) |
| EV-15 | spatial limit stated as holding at Co × 2; mixed-field R 3.97, error 0–11 % | holds at Co × 2 in 126 / 128; R 3.99 ± 0.04, error median 13 % [8, 20] co-located, 18 % [13, 23] separated | probabilistic; the 0–11 % was one realisation of ~3 true counts (D-40) |
| PAPER crystal gap (no EV entry) | 0 / 20 / 40 / 100 / 200 µm gave 9 / 13 / 20 / 69 / 45 % | kept as unverified history; replacement experiment, relative to zero gap: 1 / 1.59 / 2.52 / 6.60 / 5.33; optimum ~100 µm | the old recipe and its denominator are lost (D-40) |
| EV-01 | ≈ 0.5 mm; 6 → 5.8 mm; 12 → −6.6 mm; 282 / 148; 360 / 172 | 0.35 mm; 6 → 6.03 mm; −6.40 mm; 278 / 146; 361 / 174 | one realisation, mostly from the earlier generator |
| EV-02 | x, 1 m, N0 500 + background 4.0°; diagonal 5 m N0 5000 4.5°; ≤ 3 % / 10–45 % unflagged | 5.0° (56 / 64); 7.5° (40 / 64) or 4.5° (24 / 64); ≤ 7 % / up to 29–49 % | thresholded single-series values at the edge of their spread |
| EV-05, EV-09, EV-11, EV-21, EV-22, EV-29, EV-32 | edge RMS ~0.43 mm; floor 0.24 mm, 0.26 mm at 250 counts, original 0.36 mm; MLEM FWHM 1.76 mm, "800 k photons"; GAGG 0.2 % at 1 Mcps, peak detector ~44 %; 550 cps; ~0.6 mm floor; 0.60 → 0.68 mm | 0.26 mm; 0.25 mm, 0.27 mm [0.26, 0.28], 0.35 mm; 1.93 ± 0.05 mm at 1.5 × 10⁶ photons; 4 ± 2 %, 45 ± 1 %; 500 ± 140 cps; ~0.5 mm; 0.48 → 0.51 mm | one realisation (several from the earlier generator or an earlier command budget) |

## 10. Changing this document

- A new result that a V&V number rests on gets an entry here (next free `EV-` ID) in the same commit as the
  requirement row that cites it; a changed result updates its entry and every row in the "Used by" column.
- IDs are stable: add, don't renumber. An entry whose result is withdrawn keeps its ID, marked *withdrawn*.
- An entry must stand on its own: result, conditions, limits, reproduce command, tests. It cites code, tests and
  outputs, never a working note.
