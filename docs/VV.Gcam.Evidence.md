# VV.Gcam.Evidence — the simulation evidence behind the Gcam requirements

Scope: every simulation or design-model result that a number in the [URS](VV.Gcam.URS.md), the
[PRS](VV.Gcam.PRS.md) or the [Limitations](VV.Gcam.Limitations.md) rests on — what it shows, under which
conditions, how to reproduce it and which test pins it. Each entry is complete on its own: a reader of the V&V set
does not need any other document to check a number. Not covered: GCAM Studio's software verification
([VV.Studio](VV.Studio.md)).

**At a glance**
- 35 entries, `EV-01` … `EV-35`, grouped as imaging (with the absolute ambient field, EV-34, and decoding with its shape, EV-35), energy and isotopes,
  rate and dose, head design, manufacturing, and range.
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
**best-case bounds**, not field performance. The older background studies (EV-02, EV-12, EV-25) set the background
**relative to the source** (background ÷ signal, BSR) — a controlled stress test, kept as such.

**The absolute ambient field (since 2026-10-04; [EV-34](#ev-34--absolute-ambient-field-and-the-background-aware-search-statistic)).**
The engine can add a source-independent terrestrial field, given as a photon H*(10) rate. Its spectrum and angular
distribution come from a soil / air transport generator: K-40, the U series and the Th series at the UNSCEAR 2000
population-weighted soil activities in a uniform soil half-space, evaluated decay lines only, scored 1 m above the
ground. Its air kerma per Bq/kg is **1.014 / 0.997 / 1.023 × UNSCEAR's** coefficients (K / U / Th), accepted within a
±3 % band chosen after seeing the ratios — a model comparison, not a statistical test. No housing is modelled; every
ambient result is a range between two bounds: **bare crystal** exposed on all faces (upper) and **front only**, the
field entering through the mask with sides and rear perfectly shielded (lower). High-energy limits: the tungsten μ
is clamped above 1332 keV, the crystal has no pair production, and the dose path cuts deposits at 2000 keV — while
29–54 % of the field's air kerma is carried by photons above 1332 keV. Acquisitions are exact Poisson draws per pixel
from expected source and ambient maps on a homogeneous crystal model (no gaps, entrance window or backing), so each
family's ideal is re-measured in this pipeline (same seeds and maps, no field) and quoted beside the ambient values;
the legacy ideal value stays the best-case bound. Field levels 0.05 / 0.10 / 0.20 µSv/h; "662 keV window" is
661.7 keV ± 10 %, "open window" counts every deposit.

| Entries | Background in the quoted result | Under the absolute ambient field (measured) |
|---|---|---|
| EV-01 localisation, ghosts, cyclic against non-cyclic | none | **measured** (N = 64), at the scenarios' own 1 MBq × 1 s (~80 counts on axis at the lab geometry, ~195 at the hand-held head): expected localised positions of 625, lab, non-cyclic, open window, **180.4 ± 1.5** ideal in this pipeline (legacy noiseless 278) → **162.3 / 145.4 / 115.3** bare at 0.05 / 0.10 / 0.20 µSv/h, 180.0–180.4 front-only; in the 662 keV window the bare bound costs at most 1.1 positions (lab) and 2.4 (hand-held). The count budget, not the field, makes most of the gap to 278 |
| EV-02 field of view | BSR 0 and 1 (relative) | **measured** (N = 64; 1 m and 5 m, 10 s and 60 s): front-only — no change; bare, 662 keV window — the usable non-cyclic half-field moves at most one 0.5° step from that window's ideal, the out-of-field flag works at every level, the unflagged wrong spot at 7.5–12° stays ≤ 0.28; bare, open window — at N0 500 the non-cyclic field along x falls from 5.5° (this pipeline's ideal) to 3.5–5.5° at 10 s and 0–4° at 60 s, and the flag stops working from background ÷ N0 ≈ 0.7; at N0 5000 the field costs ≤ 1.5° along x |
| EV-07 count gates and PR-SENS-02 | none — every count is a source count | **measured** (N = 128 fresh seeds): the gate is a calibrated significance; per-configuration thresholds keep false-trusted locations on background-only acquisitions ≤ 1 % in **96 / 96** configurations (pooled 0.308 %). Smallest net count for ≥ 95 % trusted and within one resolution element, lab centre: ideal 100; front-only 100; bare, open window 250 / 250 / 500 (10 s) and 500 / 500 / 1000 (60 s); bare, 662 keV window 100 / 100 / 100 (10 s) and 100 / 250 / 250 (60 s). The needed counts grow with the background, roughly as √B |
| EV-09 hand-held head | none | **measured** (hand-held centre, N = 128): net-count gate 250 ideal; front-only 100–250; bare, open window 500 / 500 / 500 (10 s) and 1000 / 1000 / > 1000 (60 s); bare, 662 keV window 250 (10 s) and 250 / 250 / 500 (60 s). Efficiency is unchanged (the field does not depend on the source) |
| EV-12 background and mask / antimask | BSR 0–4 (relative, uniform pedestal) | **measured** (N = 128, lab, 400 source counts): calibrated subtraction and mask / antimask keep the relative study's ranking and magnitudes at matching pixel loads (2.3 counts / pixel: 0.45 / 0.35 mm; 6.8 / pixel: 0.92 / 0.61 mm) and both exceed 1 mm at 13.6 / pixel (2.24 / 1.69 mm; bare, 60 s, 0.20 µSv/h); the raw single-mask decode collapses in the bare bound (3.64 mm at 2.3 / pixel, ~9 mm from 6.8 / pixel); front-only and 662 keV window: no change |
| EV-15 isotope separation | none (other isotopes only) | **measured** (N = 128, lab, 60 s, Cs-137 1 MBq with Co-60 8 or 2 MBq): no measurable effect — ≤ 20 background counts in the 662 keV window against 3 374–8 927 window counts; every estimate and location moves by less than its seed quartile range |
| EV-03, EV-05, EV-06, EV-08, EV-13, EV-31, EV-32 (field, sampling, tolerances) | none | expected; not measured: the Poisson part of each error and the counts needed for a given precision grow (statistical term roughly × √(1 + B/S)); a fixed geometric / grid floor need not move. In the bare bound, open window, EV-34 shows the raw decoder pulled toward the background's own correlation peak once B/S exceeds ≈ 0.5 |
| EV-10, EV-11, EV-14 (multi-source, MLEM, Compton strategies) | none (other isotopes only) | expected; not measured: ambient lines and continuum add to every window; stripping must also remove the background's own downscatter. MLEM at EV-01's fixed points under the field: EV-34 |
| EV-25 shield | relative background spectra (scattered, 662 keV, Co-60) | expected; not measured: the needed shield follows the actual ambient spectrum and dose rate; EV-34's bare and front-only bounds bracket an unknown housing |
| EV-17, EV-21, EV-22 (front end, rate, dead time) | none / source-only event streams | expected; not measured: background adds counts to pile-up and dead time; the field's detected rate is at most 335 cps per µSv/h (hand-held head, bare bound, every deposit) |
| EV-23 dose | source fields only | expected; not measured: natural background adds ~0.05–0.2 µSv/h to every reading, a floor for low readings |

Geometries used below:

| Name | Scenario file | Head | Source plane |
|---|---|---|---|
| **reference lab geometry** | `samples/scenario.json` | 12 × 12 × 1 mm pixels, rank-7 MURA (2 × 2 mosaic, 1 mm cells, 10 mm W), mask–detector D = 60 mm | 100 mm from the mask |
| **hand-held head** | `samples/scenario_handheld.json` | 16 × 16 × 1 mm GAGG:Ce,Mg, 15 mm thick, same mask, D = 55 mm | 100 mm from the mask unless the entry says 1 m / 5 m |
| reference lab geometry, GAGG | `samples/scenario_orig_gagg.json` | the reference lab geometry with a 10 mm GAGG crystal | 100 mm |

## 2. Index

| ID | Evidence | Grade | Used by |
|---|---|---|---|
| EV-01 | Localisation in the fully coded field; ghosts outside it; non-cyclic decoding | MC | PR-IMG-01, -07; LIM-06, LIM-07; UN-01, UN-11 |
| EV-02 | Field of view at field distance and the out-of-field cue | MC | PR-IMG-01, -07, -08, -10; LIM-01, LIM-06, LIM-07 |
| EV-03 | Field of view ÷ resolution = rank | MC | PR-IMG-09, -10; LIM-01 |
| EV-04 | Mask thickness: leak against collimation | MC | PRS §2; LIM-01 |
| EV-05 | Tapered (bevelled) channels | MC | PR-IMG-10; LIM-01 |
| EV-06 | Detector sampling of the mask shadow | MC | PR-IMG-06; LIM-01, LIM-08 |
| EV-07 | Count threshold and directional biasing | MC | PR-SENS-02; LIM-06 |
| EV-08 | Sub-cell peak interpolation | MC | PR-IMG-02 |
| EV-09 | Hand-held head: sensitivity and precision | MC | PR-IMG-02, PR-SENS-01, -07; URS §5 |
| EV-10 | Several isotopes in one field | MC | PR-IMG-03, PR-NRG-06; LIM-04 |
| EV-11 | MLEM reconstruction | MC | PR-IMG-04; LIM-01, LIM-07 |
| EV-12 | Ambient background and mask / antimask | MC | LIM-06, LIM-07 |
| EV-13 | Depth-of-interaction parallax | MC | LIM-08 |
| EV-14 | Event positioning: total-energy window + largest deposit | MC | PR-NRG-03; LIM-08 |
| EV-15 | Isotope separation: the spatial limit and Compton stripping | MC | PR-NRG-04; LIM-04, LIM-05, LIM-06, LIM-08; UN-03 |
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
| EV-34 | Absolute ambient field and the background-aware search statistic | MC | PR-SENS-02; LIM-01, LIM-06, LIM-07, LIM-08 |

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
- **Under the absolute ambient field** (EV-34; N = 64). The quotes above are noiseless mean maps; the scenarios carry
  1 MBq × 1 s, which gives ~80 counts on axis at the lab geometry (26 in the 662 keV window) and ~195 at the hand-held
  head (75). Re-measured at that budget with 32 Poisson acquisitions per position; expected localised positions of
  625 (Σ over positions of the fraction within 3 mm, mean ± SD over seeds), open window:

  | | legacy noiseless | ideal, 1 MBq × 1 s | bare 0.05 / 0.10 / 0.20 µSv/h | front-only (all) |
  |---|---:|---|---|---|
  | lab, non-cyclic | 278 | 180.4 ± 1.5 | 162.3 ± 1.2 / 145.4 ± 1.5 / 115.3 ± 1.3 | 180.0–180.4 |
  | lab, cyclic | 146 | 94.4 ± 1.2 | 85.9 ± 1.3 / 77.7 ± 1.1 / 63.2 ± 1.0 | 94.1–94.3 |
  | hand-held, non-cyclic | 361 | 229.9 ± 1.5 | 204.0 ± 1.1 / 181.3 ± 1.3 / 144.7 ± 1.3 | 229.4–229.8 |
  | hand-held, cyclic | 174 | 112.2 ± 1.1 | 103.4 ± 1.2 / 94.6 ± 1.1 / 78.5 ± 1.2 | 111.9–112.2 |

  The count budget, not the field, dominates: without a field the lab's non-cyclic area is 65 % of the noiseless 278.
  The field's own cost in the bare bound, open window, is −10 / −19 / −36 % of that area at 0.05 / 0.10 / 0.20 µSv/h
  (lab, non-cyclic; hand-held −11 / −21 / −37 %); non-cyclic still gives 1.8–2.1× the cyclic area in every condition.
  Front-only: within 0.4 positions of ideal. 662 keV window, bare bound: at most −1.1 (lab) / −2.4 (hand-held)
  positions (≤ 1.6 %). Fixed points (300 acquisitions): lab centre RMS 1.70 ± 0.27 mm ideal → 2.48 / 3.29 /
  4.76 ± 0.20 mm bare; outside the field, (12, 0) mm, the ghost persists (within 3 mm of it in 0.839 of acquisitions
  ideal, 0.733 / 0.613 / 0.396 bare) and is never within 3 mm of the truth — the field turns some ghost answers into
  other wrong spots, not into correct ones.
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
- **Under the absolute ambient field** (EV-34; N = 64, the same seeds). The BSR pedestal is replaced by field × time
  × the transported ambient map, t = 10 s and 60 s; the centroid flag's threshold is re-calibrated under each field
  condition on 100 separate draws per in-field angle; N0 is counted in the window (a 662 keV-window N0 of 500 is
  ~2.5× the activity of an open-window one). Background per acquisition, bare bound: open window 168 / 335 / 670
  (10 s) and 1005 / 2011 / 4022 (60 s) at 0.05 / 0.10 / 0.20 µSv/h; 662 keV window 2.1 / 4.3 / 8.6 and
  12.9 / 25.7 / 51.4; front-only 0.6–15 (open) and 0.1–1.3 (662 keV). Most frequent value over 64 seeds (frequency
  when not 64 / 64):

  | Series (non-cyclic usable half-field, °) | window | ideal | bare 10 s: 0.05 / 0.10 / 0.20 | bare 60 s: 0.05 / 0.10 / 0.20 |
  |---|---|---|---|---|
  | 1 m, x, N0 500 | open | 5.5 (57/64) | 4.5 (34/64) / 4.5 (55/64) / 3.5 (60/64) | 0 (41/64) / 0 (63/64) / 0 |
  | 1 m, x, N0 500 | 662 keV | 5 (51/64) | 5 (53/64) / 5 (51/64) / 5 (45/64) | 5 (49/64) / 5 (42/64) / 4.5 (41/64) |
  | 1 m, x, N0 5000 | open | 6.5 | 6.5 / 6.5 / 6.5 | 6.5 (51/64) / 5.5 / 5 (63/64) |
  | 1 m, x, N0 5000 | 662 keV | 6.5 | 6.5 / 6.5 / 6.5 | 6.5 / 6.5 / 6.5 |
  | 1 m, diagonal, N0 500 | open | 2.5 | 1.5 (61/64) / 1.5 (56/64) / 0 | 0 / 0 / 0 |
  | 1 m, diagonal, N0 500 | 662 keV | 2.5 (58/64) | 2.5 (56/64) / 2.5 (54/64) / 2.5 (51/64) | 2.5 (46/64) / 1.5 (35/64) / 1.5 (56/64) |
  | 5 m, x, N0 500 | open | 5.5 (39/64) | 5.5 (62/64) / 5 (53/64) / 4.5 (61/64) | 4 (56/64) / 0 (62/64) / 0 |
  | 5 m, x, N0 500 | 662 keV | 5.5 (59/64) | 5.5 (59/64) / 5.5 (56/64) / 5.5 (56/64) | 5.5 (61/64) / 5.5 (60/64) / 5.5 (63/64) |

  Out-of-field cue along x, 1 m, N0 500: the "outside" flag (≥ 90 %) holds from 5 (31/64) to 11.5° (51/64) ideal in
  the 662 keV window and from 5–5.5° to 11–11.5° at every field level (modes in 33–57 / 64 seeds); in the open window
  it is never reached from 0.10 µSv/h at 10 s (56 / 64 and 64 / 64 seeds) and at every level at 60 s (64 / 64). The unflagged wrong spot at
  7.5–12° (largest fraction, median over seeds) rises from 0.13 ideal to 0.13 / 0.13 / 0.15 (10 s) and
  0.16 / 0.20 / 0.28 (60 s) in the 662 keV window, against 0.09 → 0.37 / 0.46 / 0.51 and 0.58 / 0.66 / 0.74 in the
  open window (5 m: 662 keV 0.07 → at most 0.18, open 0.06 → up to 0.96); at N0 5000 it stays 0.00 in the 662 keV
  window (open: up to 0.36 at 1 m and 0.86 at 5 m). Front-only: no change from ideal in any series. This pipeline's
  open-window ideal is 0.5–1° narrower than the legacy values above (5.5° / 6.5° against 6.0–7.0° / 7.0–7.5° along
  x) — a detector-model difference — and the two windows' ideals differ slightly in their own right (e.g. 5 m
  diagonal N0 5000 cyclic: 1.5° in the 662 keV window, 4° open). So the open-window loss is a background-count
  effect that the 662 keV window removes at these field levels.
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
- **Under the absolute ambient field** (EV-34; N = 128 seeds not used to set the gate). In EV-34's pipeline
  (homogeneous crystal) the ideal lab-centre curve matches the gates above: RMS 5.74 ± 0.21 / 3.39 ± 0.28 /
  1.10 ± 0.22 / 0.42 ± 0.01 mm at 25 / 50 / 100 / 250 counts, failures 44.6 % at 25, sub-mm in 128 / 128 seeds from
  250. With background, net counts no longer make a gate: the gate is a calibrated search significance with
  per-configuration thresholds, which keep false-trusted locations on background-only acquisitions ≤ 1 % in 96 / 96
  configurations (pooled 0.308 %), plus ≥ 95 % within one resolution element. Smallest net count meeting both, lab
  centre: ideal 100; front-only 100; bare bound, open window 250 / 250 / 500 (10 s) and 500 / 500 / 1000 (60 s) at
  0.05 / 0.10 / 0.20 µSv/h; bare, 662 keV window 100 / 100 / 100 (10 s) and 100 / 250 / 250 (60 s). The hand-held
  head, 1 m and 5 m gates are in EV-34.
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
- **At use distance, averaged over position** (D-49; transported maps, 662 keV window, cross-correlation with sub-cell
  interpolation; source position randomised within one angular element, 128 seeds × 200 acquisitions). At **1 m**: RMS
  **0.38° at 250 counts** (6.6 mm), **0.21° at 1000** (3.7 mm); within one element (1.042°) in 98.9 % / 100 %;
  noiseless error median 0.15°, up to 0.34° by position (signed y −0.11°). At 5 m: 0.81° / 0.53°, 92.1 % / 98.5 %. The
  same head at its own S = 100 mm gives 1.38 mm at 250 counts and 0.57 mm at 1000, so the 0.25 mm floor above is one
  favourable on-axis sampling phase at the near field, not the head's precision.
- **Under the absolute ambient field** (EV-34; hand-held centre, N = 128). Ideal in EV-34's pipeline: RMS
  1.00 ± 0.28 mm at 250 counts (sub-mm in 60 / 128 seeds), 0.24 ± 0.10 mm at 500 (128 / 128). Smallest net count
  with ≥ 95 % trusted and within one resolution element: ideal 250; front-only 100–250; bare, open window
  500 / 500 / 500 (10 s) and 1000 / 1000 / > 1000 (60 s) at 0.05 / 0.10 / 0.20 µSv/h; bare, 662 keV window 250 (10 s)
  and 250 / 250 / 500 (60 s). At 1 MBq the centre stays at its ideal floor (0.20–0.22 mm) in every ambient
  condition; the 8 mm edge in the bare bound, open window, is pulled (raw decoder RMS 1.28 / 3.37 / 9.47 mm at
  0.05 / 0.10 / 0.20 µSv/h in 10 s, ideal 0.70–0.85 mm — EV-34). Efficiency is unchanged: the field does not depend on
  the source.
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

- **Shows — at use distance, in angle** (D-41; hand-held head, Cs-137 662 keV window, transported floods, non-cyclic,
  exact Poisson; one angular element = atan(1 / 55) = 1.042°; pair position randomised over one element; 128 seeds ×
  50 pair + 50 single-source acquisitions per cell). **Resolved** = two peaks found blind by topographic prominence,
  each within min(Δ / 2, 1 element) of a different source, valley ≥ 25 % of the lower peak, the second peak above a
  significance floor calibrated on single-source acquisitions (≤ 3 % false split at selection, disjoint seeds; CAL-04), in
  ≥ 95 % of acquisitions at Δ and at every larger Δ on the grid, with single-source false splits ≤ 5 % (D-48).
  - *Point response.* Cross-correlation's half-maximum width at 1 m, over source positions within one element: x
    1.46° [1.19, 1.70], y 1.22° [1.12, 1.34] (median [quartiles]) against the geometric 1.042°; at 5 m median 1.10° but
    the upper quartile reaches 2.0–2.1° (the decoder's columns repeat in ~0.5-element blocks there).
  - *Two equal sources at 1 m, inside the fully coded field (±3.6°):* the **pixel-area MLEM** (an analytic forward
    model integrating each pixel's area, 120 iterations; D-46) resolves **1.25 elements = 1.30°** from 1000 counts per
    source (98.4 % [98.0, 98.8] pass at 1000 counts; false split 0.05 %), 1.5 elements near the field edge, 1.75
    elements at 5 m. The engine's pixel-centre MLEM resolves 1.5 elements only at ≤ ~15 iterations (8 chosen); at
    80 iterations it splits about half of all single sources on this head. Cross-correlation reaches ≥ 95 % nowhere
    up to 3 elements (best 95.3 % at 2.0 elements, 16 000 counts, then 86 % / 65 % at 2.5 / 3.0 — a lumpy,
    shift-variant response). At 250 counts per source no decoder resolves any pair up to 3 elements.
  - *1 : 4 intensity:* pixel-area MLEM 1.5 elements on axis (1000–16 000 counts in the weaker source; false split
    ≤ 2.9 %), 1.75 / 1.5 / 1.5 near the edge; without the significance floor a strong single source's side maximum
    qualifies in up to 11 % and 1 : 4 is not resolved. Cross-correlation and the engine MLEM do not resolve 1 : 4.
  - *Matched upper bound* (a system matrix built from transported single-source maps, 32 seeds): 1.25 elements — the
    analytic pixel-area model already reaches it. Usable non-cyclic field (±7°, not claimed, D-47): pixel-area
    unchanged, engine MLEM ≤ 45 % and cross-correlation ≤ 73 % pass. Ambient 0.10 µSv/h, 60 s: no resolved separation
    changes (pass within 1.4 points, with or without a background term in MLEM).
  - *Floor validation:* judged by the upper bootstrap limit (the upper end of the two-sided 95 % interval, D-48), 4 of
    414 validation rows exceed 5 % false split, all in cells that resolve no separation (cross-correlation 1 : 1 at
    4000 counts; pixel-centre MLEM 1 : 4 at 16 000); no resolved separation changes. At and beyond each claimed
    separation the pixel-area MLEM's upper limit is ≤ 1.6 % (1 : 1, axis), ≤ 3.7 % (1 : 4, axis), ≤ 4.1 % (edge),
    ≤ 3.5 % (5 m).
  - *Iterations matter:* every MLEM splits single sources when run long (pixel-centre 49 % at 80 iterations,
    pixel-area 9.5 % at 480, matched 18 % at 640); counts are chosen on separate seeds by the smallest resolved
    separation including the false-split limit.
- **Shows — the earlier lab near-field demonstration (history).** A Poisson maximum-likelihood reconstruction with the finite mask as its forward model separates source
  pairs **2–3 mm apart** that cross-correlation merges (at 3 mm, valley depth 0.822 ± 0.004 against 0.170 ± 0.001;
  N = 64). On a single source it is non-negative (cross-correlation dips to −103.5 ± 0.4) and sharper (FWHM
  1.93 ± 0.05 vs 2.50 mm), with a localisation bias of 0.87 mm against 0.55 mm (the same in 64 / 64 seeds). At 1.5,
  2 and 3 mm MLEM resolves the pair and cross-correlation does not (64 / 64 seeds each; 1.5 × 10⁶ photons,
  80 iterations, resolved = valley depth > 0.25); cross-correlation needs ~3.5 mm (64 / 64). Below 2 mm the test is
  centred on the true positions and therefore optimistic.
  Step by step (64 seeds, lab geometry): with a blind test and single-source false splits counted, its MLEM figure
  becomes 1.25 lab elements (from 0.56) and cross-correlation 1.5 (from 1.31); with transported floods both MLEMs fall
  to 2.5 elements through false splits; non-cyclic decoding changes nothing; the hand-held head at 155 mm and at 1 m
  gives the pixel-area MLEM 1.25 elements. **The near-field "2–3 mm" does not survive a blind criterion** and is not
  a performance figure.
- **Limits.** MC only; ideal pixel identification, homogeneous crystal, no inter-pixel dead regions (before a
  four-channel readout model); a still head (no hand motion); the significance floor bounds false splits at the tested
  pair geometry — a floor that caps a second peak anywhere costs resolution in some cells (pixel-area 1 : 4 1.5 → 1.75
  elements at 1000 counts; pixel-area 1 : 1 unchanged); results depend on the iteration count, fixed by a stated rule.
- **Reproduce.** At use distance: `montecarlo ambient-evidence`, family `angres`, through
  `samples/evidence/manifest-angres-v1.json` (point response, resolved pairs, iterations, ladder, matched, ambient,
  ±7°, 5 m; `samples/evidence/angres/aggregate_angres.py` → `samples/evidence/results/angres-v1-*.json`) and
  `manifest-angres-v2.json` (significance floor → `angres-v2-floor.json`). The same pixel-area MLEM in a single run:
  `montecarlo <scenario>` with `"decoder": { "method": "Mlem", "cyclic": false }` (120 iterations unless
  `mlemIterations` is set; identical to the study's construction). Lab demonstration:
  `montecarlo mlem samples/scenario.json samples/mlem.csv`.
- **Tests.** `MlemTests`; `MlemOptionTests` (the default decoder bit-identical to the original algorithm; pixel-area,
  supplied-matrix and background options), `ResolvedPairTests` (blind two-peak test, prominence, floor selection),
  `AngularResolutionStudyTests`.

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
- **Under the absolute ambient field** (EV-34; N = 128). Lab geometry, centred source, 400 expected net source counts
  in the full live time t (5.0 × 10⁵ Bq at 10 s, 8.3 × 10⁴ Bq at 60 s), 200 repeats; (a) one exposure decoded raw,
  (b) calibrated subtraction of the instrument's background model, (c) t/2 mask + t/2 inverted mask at the same
  activity — equal total time. RMS error, mean ± SD over seeds, open window:

  | Condition | background / pixel | single, raw | calibrated | mask / antimask |
  |---|---:|---|---|---|
  | ideal | 0 | 0.38 ± 0.01 | 0.38 ± 0.01 | 0.30 ± 0.01 |
  | 10 s, 0.05 µSv/h, bare | 0.57 | 0.46 ± 0.02 | 0.39 ± 0.01 | 0.31 ± 0.01 |
  | 10 s, 0.10, bare | 1.13 | 0.68 ± 0.17 | 0.40 ± 0.01 | 0.32 ± 0.01 |
  | 10 s, 0.20, bare | 2.26 | 3.64 ± 0.32 | 0.45 ± 0.02 | 0.35 ± 0.01 |
  | 60 s, 0.05, bare | 3.39 | 7.28 ± 0.22 | 0.49 ± 0.06 | 0.39 ± 0.05 |
  | 60 s, 0.10, bare | 6.78 | 9.10 ± 0.09 | 0.92 ± 0.25 | 0.61 ± 0.18 |
  | 60 s, 0.20, bare | 13.57 | 9.03 ± 0.09 | 2.24 ± 0.32 | 1.69 ± 0.32 |
  | front-only, all six | 0.002–0.048 | 0.38 | 0.37–0.38 | 0.30 |

  With subtraction the absolute field reproduces the relative study's ranking and magnitudes at matching pixel loads;
  the antimask is lower in 107–128 of 128 seeds at every level. Both stay sub-mm up to 6.8 counts / pixel; at
  13.6 / pixel both exceed 1 mm with failures (> 3 mm) in every seed (6.25 % calibrated, 3.98 % antimask). The raw
  single-mask decode collapses in the bare bound from 2.3 counts / pixel (3.64 mm) to ~9 mm — the decoder pull of
  EV-34, which subtraction removes. In the 662 keV window the field is ≤ 0.14 counts / pixel and nothing moves from
  ideal (0.40–0.42 / 0.40–0.41 / 0.31–0.32 mm). The antimask half sees the inverted mask's own source rate (2.5 %
  higher; the relative study normalised each half). D-19 is untouched: the comparison is a trade at equal time.
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

### EV-34 — Absolute ambient field and the background-aware search statistic

- **The field.** A terrestrial photon field built by the engine, not borrowed: K-40, the U-238 series and the Th-232
  series at the UNSCEAR 2000 (Annex B) population-weighted soil activities 420 / 33 / 45 Bq/kg in a uniform, laterally
  infinite soil half-space under dry air, transported (Compton with free-electron Klein–Nishina angles, photoelectric
  absorption, pair production with 511 keV annihilation photons) and scored 1 m above the ground. Decay data: the
  evaluated photon lines with absolute intensities only (IAEA LiveChart / ENSDF snapshots, hash-pinned); a transition
  the evaluation lists without a photon intensity is listed as "not included" in the spectrum file, never given a
  substitute intensity. Cross sections NIST XCOM, air μ_en/ρ NIST, soil composition HASL-258. The output is a
  versioned, hash-pinned spectrum: discrete lines, scattered continuum and an energy × zenith-angle table.
- **Validation.** (a) Uncollided line fluence and its zenith distribution against the analytic half-space kernel:
  chain totals 0.99945 / 1.00001 / 0.99950 (K / U / Th); every tested cell within 6 SE (cells with < 30 crossings
  reported as not tested). (b) Air kerma at 1 m per Bq/kg against UNSCEAR's coefficients (0.0417 / 0.462 /
  0.604 nGy/h): **1.01388 ± 0.00032 / 0.99740 ± 0.00032 / 1.02268 ± 0.00026** (MC / UNSCEAR ± SE, N = 16 seeds ×
  10⁷ histories per chain). The gaps are many SE wide — a difference between two model calculations (UNSCEAR's
  coefficients come from another transport calculation, older decay data, another soil), and this model's omissions
  (bremsstrahlung, transitions without intensities, fluorescence) would raise its kerma, not lower it. Accepted
  within **±3 %**, a band chosen after seeing the ratios: a model comparison, not a statistical test. At the UNSCEAR
  activities the field is 60.76 nGy/h (UNSCEAR's own products: 59.94). Photons above 1332 keV carry 54.1 / 28.9 /
  36.1 % of each chain's kerma.
- **The instrument model.** Input: photon H*(10) rate (ICRP 74 conversion); fields 0.05 / 0.10 / 0.20 µSv/h. No
  housing; two bounds: **bare crystal** on all faces (upper) and **front only** through the mask, sides and rear
  perfectly shielded (lower). The pixel comes from where the photon actually deposits (no random placement). Detected ambient
  rate per µSv/h (mean over 128 seeds): lab geometry bare 162.8 cps (every deposit) / 1.66 cps (662 keV window),
  front-only 0.572 / 0.0447 cps; hand-held head bare 335.2 / 4.28 cps, front-only 1.283 / 0.104 cps. A 1 MBq Cs-137
  source gives 80.2 / 26.0 cps at the lab distance and 4.76 / 1.88 cps at 1 m on the hand-held head, so at
  0.10 µSv/h ambient ÷ source is 0.203 (lab, bare, every deposit) and **7.04 at 1 m** (bare, every deposit), 0.228
  at 1 m in the 662 keV window and 0.027 at 1 m front-only. The bare background is not flat in the open window
  (edge-to-centre pixel rate 2.7 at the lab geometry, 3.3 at the hand-held head; 0.99–1.01 in the 662 keV window,
  0.72–0.76 front-only).
- **The search statistic.** The scenario's decoder as a linear map G. With the instrument's background-shape model p
  (an independent MC estimate) and the acquisition total N as the only nuisance, a background-only acquisition is
  multinomial, so Z(θ) = (recon(θ) − N Σ G p) / √(N (Σ G² p − (Σ G p)²)) has mean 0 and variance 1 at every grid
  point; the statistic is the largest Z over the decoder grid and its location is that grid point. It is **not** a
  Gaussian significance: its threshold is calibrated on simulated background-only acquisitions.
- **The gate (PR-SENS-02).** 96 configurations: lab geometry and hand-held head at their scenario distance, and the
  hand-held head at 1 m and 5 m; 10 s and 60 s; three field levels; two bounds; open and 662 keV windows. Per
  configuration, the threshold is the smallest recorded Z with at most 0.3 % of the selection nulls at or above it
  (ties counted as exceedances), from **65,536 background-only acquisitions per configuration** (64 seeds × 1024);
  thresholds 1.163 … 4.6245, pinned before validation (calibration record CAL-02). Validation on **seeds never used for selection** (N = 128,
  64 nulls each = 8,192 per configuration): **96 / 96 configurations pass** (one-sided 95 % Clopper–Pearson upper
  limit of the false-trusted rate ≤ 1 %); pooled **2,424 / 786,432 = 0.308 %** (one-sided 95 % limits
  0.298–0.319 %); worst configuration lab, 10 s, 0.10 µSv/h, front-only, 662 keV window (0.04 expected background
  counts): 40 / 8,192 = 0.49 %, upper limit 0.64 %; the 27 configurations with < 1 expected background count
  0.325 %. A first selection on 4,096 nulls per configuration (CAL-01, superseded) passed 93 / 96: where the expected background is
  < 1 count Z takes few distinct values and the selected quantile moves in steps, so three configurations missed by
  0.007 points (13 / 2,048, upper 1.007 %); the selection was repeated with 16× the nulls and validated on new seeds,
  nothing else changed.
- **Count gates under the field.** Source acquisitions: 300 per condition per seed, N = 128; trusted = Z at or above
  the threshold; correct = trusted and within one angular resolution element (lab 0.955°, hand-held head 1.042°).
  Smallest net source count S on the grid 25 / 50 / 100 / 250 / 500 / 1000 with ≥ 95 % trusted and correct (ideal
  column: the decoder ≥ 95 % within one resolution element):

  | Case / window | ideal | front-only, any field | bare 0.05 / 0.10 / 0.20, 10 s | bare 0.05 / 0.10 / 0.20, 60 s |
  |---|---|---|---|---|
  | lab centre, open | 100 | 100 | 250 / 250 / 500 | 500 / 500 / 1000 |
  | lab centre, 662 keV | 100 | 100 | 100 / 100 / 100 | 100 / 250 / 250 |
  | lab 8 mm edge, open | 100 | 100–250 | 500 / 500 / 1000 | 1000 / 1000 / > 1000 |
  | lab 8 mm edge, 662 keV | 100 | 250 | 250 / 250 / 250 | 250 / 250 / 250 |
  | hand-held centre, open | 250 | 100–250 | 500 / 500 / 500 | 1000 / 1000 / > 1000 |
  | hand-held centre, 662 keV | 250 | 250 | 250 / 250 / 250 | 250 / 250 / 500 |
  | hand-held 8 mm edge, open | 500 | 250 | 250 / 250 / 500 | 500 / 500 / 1000 |
  | hand-held 8 mm edge, 662 keV | 500 | 250 | 100 / 250 / 250 | 250 / 250 / 250 |
  | hand-held 1 m centre, open | 250 | 250 | 250 / 500 / 500 | 500 / 1000 / 1000 |
  | hand-held 1 m centre, 662 keV | 250 | 250 | 250 / 250 / 250 | 250 / 250 / 500 |
  | hand-held 1 m 3°, open | 1000 | 500 | > 1000 (all) | > 1000 (all) |
  | hand-held 1 m 3°, 662 keV | > 1000 | 1000 | 1000 / 1000 / 1000 | 1000 / 1000 / 1000 |
  | hand-held 5 m centre, open | 100 | 100 | 250 / 250 / 250 | 500 / 500 / 1000 |
  | hand-held 5 m centre, 662 keV | 100 | 100 | 100 / 100 / 100 | 250 / 250 / 250 |
  | hand-held 5 m 3°, open | 250 | 250 | 500 / 1000 / 1000 | 1000 / > 1000 / > 1000 |
  | hand-held 5 m 3°, 662 keV | 250 | 500 | 250 / 250 / 500 | 500 / 500 / 500 |

  Ideal in this pipeline, lab centre, RMS at S = 25 / 50 / 100 / 250 / 500 / 1000: 5.74 ± 0.21 / 3.39 ± 0.28 /
  1.10 ± 0.22 / 0.42 ± 0.01 / 0.37 ± 0.00 / 0.36 ± 0.00 mm (sub-mm in 0 / 0 / 48 / 128 / 128 / 128 of 128 seeds);
  hand-held centre 6.90 ± 0.20 / 5.49 ± 0.24 / 3.47 ± 0.25 / 1.00 ± 0.28 / 0.24 ± 0.10 / 0.20 ± 0.00 mm. **Under
  background the gate is a significance gate, not a count gate:** in the bare bound, open window, the S needed grows
  roughly as √B (lab: 250 → 500 → 1000 for B ≈ 160 → 490–980 → 1950 background counts); in the 662 keV window it
  moves at most one grid level from ideal; front-only it stays at or below the ideal value at the lab and hand-held
  centres.
  Detection alone does not assure quality: at very low background almost every acquisition with counts is trusted,
  and the within-one-element criterion sets the gate. The hand-held 1 m, 3° source sits at the edge of the optics
  (it misses 95 % even ideal in the 662 keV window); its 1000 / > 1000 entries are marginal.
- **The raw decoder is pulled; the search statistic is not.** Pull = length of the seed-mean error vector at the
  source plane (cross-correlation decoder, bare bound, open window; N = 128). It is a switch, not a gradual shift:
  below background ÷ source counts B/S ≈ 0.5 it stays at the ideal floor; between ≈ 0.6 and 1.3 a growing share of
  acquisitions jumps to the correlation peak of the background's own shape; above B/S ≈ 2 the mean saturates at
  **~8 mm at the lab geometry (2.9°), ~9 mm at the hand-held head (3.4°), ~19 mm at 1 m (1.1°) and ~40–50 mm at 5 m
  (0.5°)**. Examples: lab centre, 60 s, S = 1000: 0.36 mm ideal, 0.43 / 1.47 / 8.13 mm at 0.05 / 0.10 / 0.20 µSv/h;
  lab centre at 1 MBq: 0.37 / 0.38 / 0.42 mm (B/S ≤ 0.41); hand-held 1 m centre at 1 MBq, 60 s: 1.77 mm ideal,
  15.28 / 17.98 / 18.98 mm (B/S 3.5 / 7.1 / 14). In the 662 keV window and in the front-only bound (lab and
  hand-held head at the scenario distance) the excess over the ideal pull is at most 0.73 mm, and ≤ 0.1 mm wherever
  B/S < 0.1. In the same acquisitions the search statistic locates the source correctly (at 1 MBq, the hand-held
  8 mm edge in the bare bound, open window: 100 % trusted and correct while the decoder's RMS reaches 9.47 mm at
  0.20 µSv/h, 10 s). MLEM without a background term (EV-11's decoder, EV-01's fixed points, 1 MBq × 1 s) is pulled
  more slowly — hand-held 8 mm edge, within-3-mm share 0.960 → 0.805 at 0.20 µSv/h against 0.772 → 0.184 for
  cross-correlation — but has its own background answer elsewhere; at the lab (80 counts) it is worse than
  cross-correlation even ideal (4.27 against 1.70 mm), so no ranking is claimed. Either decoder needs a model of the
  background's shape once B/S exceeds ≈ 0.5 in the bare bound; that decoder does not exist yet.
- **Limits.** Bounds, not a housing: a real head lies somewhere between them. High-energy transport is incomplete (tungsten μ clamped above 1332 keV, no pair
  production in the crystal, dose path cut at 2000 keV) while 29–54 % of the kerma is above 1332 keV. Sources only in
  the soil (no cosmic component, no airborne radon, no room scatter, no intrinsic crystal activity); one soil and one
  population-weighted activity set. Source and field both use a homogeneous crystal (no gaps, entrance window or
  backing), so the ideal values of this pipeline differ from the legacy entries' (e.g. EV-02's ideal field is 0.5–1°
  narrower). Acquisitions are Poisson draws from expected maps, not event-by-event transport (map MC noise ≤ ~1 % of
  the acquisition's Poisson variance at the largest backgrounds). The search location is a grid point (no sub-cell
  interpolation). Activities at 5 m reach 10⁸ Bq for S = 1000 (a declared count-level design, not a field claim).
- **Reproduce.** Spectrum: `python samples/ambient/build_catalog.py`, `python samples/ambient/build_materials.py`,
  then `montecarlo ambient-terrestrial samples/ambient/terrestrial-generator-v1.json <folder>` (generator and
  validation) and `montecarlo ambient-issue samples/ambient/terrestrial-unscear2000-v1-issue.json <folder>` (the
  validated file `samples/ambient/terrestrial-unscear2000-v1.json` and its `.sha256`). One seed of the gate or of an
  EV family: `montecarlo ambient-gate <gate-request.json>` and `montecarlo ambient-evidence <evidence-request.json>`
  (requests in `samples/evidence/ambient/`). Ensembles through the seed driver:
  `python samples/evidence/run_seeds.py --manifest samples/evidence/manifest-ambient-v2.json --out <dir> --family
  gate_selection_v2` (then `samples/evidence/ambient/select_thresholds.py --rule inclusive`, which reproduces
  `samples/evidence/ambient/gate-thresholds-v2.json`), `--family gate_validation_v2` (then
  `samples/evidence/ambient/aggregate_gate.py`), `--family ev12_antimask ev15_separation ev02_fov ev01_sweep` (then
  `samples/evidence/ambient/aggregate_ev.py` and `bias_baseline.py`); `manifest-ambient-v3.json` families
  `ev15_abs_a ev15_abs_b ev02_fov_cs662`. Committed results: `samples/evidence/results/ambient-baseline-v1-turn6*.json`
  (spectrum and validation), `-turn7*` (ambient rates), `-turn8*` (gate, EV-01 / 02 / 12, decoder pull), `-turn9*`
  (EV-15, EV-02 in the 662 keV window), each with its `.md` report.
- **Tests.** `KleinNishinaTests`, `ExponentialIntegralTests`, `SoilAirMaterialsTests`, `TerrestrialCatalogTests`,
  `SoilAirTransportTests`, `TerrestrialSpectrumTests`, `IncidentSpectrumFileTests`, `AmbientAngularSamplerTests`,
  `AmbientFieldTests`, `GateResponseTests`, `CorrelationSearchTests`, `AmbientGateStudyTests`, `AmbientEvidenceTests`.
- **The raw pull, corrected (EV-35).** The raw-decoder numbers above stay as measured; decoding with the
  background's shape (opt-in) removes the pull at the near-field heads within a stated count / field scope.

### EV-35 — Decoding with the background's shape

- **What.** Under the bare-crystal ambient bound the raw cross-correlation decoder is pulled toward the background's
  own correlation peak (EV-34). Opt-in correction (default off, every existing path unchanged): **E4**, a pixel-area
  MLEM with one extra non-negative background component — the normalised shape of an independent, source-free
  calibration — whose amplitude the reconstruction fits from the image; **E5** (the same MLEM with a known background
  B·p) and **E6** (signed cross-correlation of the image minus a known B·p, no clipping) as explicit known-scale
  modes. The scale comes from the image (D-53); a separate dose counter measures source plus ambient and is not a
  subtraction scale; the fitted amplitude is not a dose (lab, 1000 counts, 60 s, 0.10 µSv/h: 746 fitted against 977
  calibration counts).
- **Conditions (stage 1, D-55).** Open window, bare bound, a source at the specified edge of each head: lab (160 mm),
  hand-held (155 mm), hand-held at 1 m and 5 m; cyclic and non-cyclic decoding; 25, 50, 100, 250, 500, 1000 source
  counts and the 1 MBq default; 10 and 60 s; 0, 0.05, 0.10, 0.20 µSv/h. 448 cells, **32 validation seeds × 100
  acquisitions per cell** (3,200), disjoint from the 16 selection seeds that fixed E4 at **400 iterations** and from
  the 3 development seeds. Truth and calibration maps are independent Monte Carlo estimates per seed.
- **Pass rule (D-54), pinned before validation.** In each of the 75 regimes that passed association and trust on
  the selection seeds: association (one angular element) and trusted association ≥ 3,061 of 3,200 (one-sided
  conditional 95 % lower bound ≥ 0.95), and a simultaneous seed-cluster bootstrap bound (10,000 whole-seed resamples)
  on the **paired signed-vector excess** of E4 over its own ideal response below √2 × the grid step (lab 0.550,
  hand-held 0.581, 1 m 3.750, 5 m 18.749 mm) — an engineering convention, not detector accuracy.
- **Result.** **75 / 75 regimes pass** (lowest trusted association 3,121; tightest bound 0.415 mm against 0.550 mm,
  lab non-cyclic, 60 s, 500 counts, 0.10 µSv/h). No regime passes at 1 m cyclic. RMS, non-cyclic, 1000 counts, 60 s,
  0.10 µSv/h, raw → E4 (mean over 32 seeds): lab 8.53 → 0.50 mm, hand-held 11.2 → 0.71 mm, 1 m 8.19 → 9.04 mm,
  5 m 38.1 → 40.4 mm — non-cyclic decoding at 1 m and 5 m is barely pulled, and E4 costs a little there; the large
  far pulls are cyclic (1 m 39.5 → 13.6, 5 m 44.8 → 30.1 mm). Empty field, 1000 counts, non-cyclic: lab 0.65 → 1.29,
  hand-held 0.97 → 0.61, 1 m 6.28 → 7.55, 5 m 34.3 → 38.6 mm. 250 counts at 0.20 µSv/h, 60 s: E4 association 2025 /
  1252 / 1571 / 1823 of 3200 — low counts in a high field remain unsolved.
- **Sensitivities (descriptive, 24 base cells).** Source-free calibration of 600 s / 3600 s: E4 RMS × 0.78–1.15 /
  1.00–1.05; known scale off by −10 %: hand-held E6 association 1341 / 3200 (no universal scale tolerance); front-only
  shape on bare truth: E4 association 40 / 3200 (refused by the calibration metadata).
- **Not covered.** The 662 keV window, the front-only bound and centre sources (stage 2, not run); several sources;
  pair resolution with E4; a housing; the gate's trust is unchanged and does not certify E4's answer.
- **Reproduce.** `python samples/evidence/run_seeds.py --manifest samples/evidence/manifest-background-shape-v2.json
  --family background_shape_validation_v2 --jobs 16 --out <dir>` (and `background_shape_sensitivity_v2`), then
  `python samples/evidence/background-shape/aggregate_background.py --version 2 --phase validation|sensitivity --runs
  <dir> --out <file>`; selection: `manifest-background-shape-v1.json` family `background_shape_selection_v1`, pinned
  regimes `samples/evidence/background-shape/pinned-v1.json`. Committed results:
  `samples/evidence/results/background-shape-{selection-v1,pilot-v1,validation-v2,sensitivity-v2}.json` with
  provenance sidecars.
- **Tests.** `BackgroundDecodingTests` (default path unchanged, normalisation invariance, E5 / E6 identities, the
  β = 0 boundary, EM count conservation and non-decreasing likelihood); `samples/evidence/tests/test_background_shape.py`.

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
  are not checked (the per-pixel ratio below answers this). Stripping removes the Co-60 continuum's mean, not its
  Poisson fluctuation, so the Cs-137 detection limit grows with the Co-60 counts — quantified below ("The cost of
  stripping at use distance", D-42).
- **Under the absolute ambient field** (EV-34; N = 128). The recipe above has no live time (activities 1 Bq Cs-137 and
  8 Bq Co-60 with a photon budget); read literally it gives ~6 Cs-137 counts, unmeasurable even without background. It
  was re-measured at absolute activities: lab geometry, **60 s**, Cs-137 1 MBq with Co-60 **8 MBq** (a, the 8 : 1
  ratio) or **2 MBq** (b), scenes separated (Cs (4, 0), Co (−5, 3) mm) and co-located (both (0, 0)), 200 exact-Poisson
  acquisitions per condition per seed. Windows on the event's total deposit (662 keV: 595.53–727.87 keV; 1332 keV:
  1199.25–1465.75 keV), the event at its largest-deposit pixel; R from a noiseless Co-only map on the axis:
  **R = 1.1338 ± 0.0092**. It is **not comparable** with R = 3.99 above, which is defined with the per-pixel window (a
  Compton-scattered Co-60 photon can add counts to several pixels). Relative error of the Cs-137 count (true S = 1550
  separated, 1558 co-located): per-seed median, then median [quartiles] over seeds; ideal shown:

  | | (a) separated | (a) co-located | (b) separated | (b) co-located |
  |---|---|---|---|---|
  | stripping, unfloored | −4.5 % [−8.7, −1.0] | −1.1 % [−4.9, +2.8] | −1.2 % [−2.4, −0.2] | −0.3 % [−1.2, +0.9] |
  | per-acquisition SD | 8.4 % | 8.4 % | 4.8 % | 4.8 % |
  | per-pixel stripping floored at zero | +16.8 % | +17.3 % | +2.4 % | +1.9 % |
  | count read at the matched Cs peak (spatial) | +141.4 % [+138.5, +144.2] | — | +20.7 % [+19.7, +21.4] | — |
  | Cs within 1 mm: raw two-peak / stripped reconstruction | 0.000 / 0.497 | — / 0.799 | 0.997 / 0.999 | — / 1.000 |

  **The field has no measurable effect at 60 s**: in the bare bound at 0.20 µSv/h the background is ≤ 19.9 counts in
  the 662 keV window against 3 374–8 927 window counts (≤ 0.5 front-only), and every estimate and location moves by
  less than its seed quartile range (e.g. (a) separated, unfloored −4.0 … −4.6 % under the field against −4.5 %
  ideal; subtracting the background model makes it 0.1–0.5 points more negative at bare 0.20 µSv/h — the model map's
  own MC error). At 8 : 1 the spatial lever fails as above (the "Cs" peak reads +141 % — it is not the Cs peak) and
  stripping recovers the count; at 2 : 1 the spatial lever holds (0.997 within 1 mm) but the count read at the Cs peak
  is ~21 % high. Co-60 location: co-located 0.42–0.43 mm median; separated 1.07 mm in every condition, ideal included
  — an offset of this decode at that position, not a background effect (not investigated). The larger separated-scene
  stripping bias is **explained by R's direction**: the separated scene's own R (from the same runs' expected counts) is
  1.0 % below the on-axis calibrated R, which predicts −4.71 % against −4.48 % measured (8 : 1 separated), and the
  other three conditions within 0.25 points (−0.86 / −1.18 / −0.21 % against −1.11 / −1.18 / −0.35 %); the quartile
  spread is R's map MC noise (SD 0.8 % over seeds). With the photopeak reference window this direction dependence is
  real; with the side-window reference below it vanishes.
- **Shows — the cost of stripping at use distance** (D-42; N = 128 validation seeds, disjoint from 64 threshold-
  selection seeds). Hand-held head, Cs-137 and Co-60 at **1 m**, windows on the event's total deposit: cs662
  595.53–727.87 keV; the stripping reference is a **side window above the Cs-137 peak, 727.87–860.21 keV** (the product
  choice, D-43), with the Co-60 photopeak windows 1199.25–1465.75 keV (co1332) and 1055.88–1465.75 keV (both lines)
  measured beside it. Co-60 0–100 MBq; **28.72 MBq gives 10 µSv/h** photon H*(10) at the head (ICRP 74, unattenuated;
  0.348 µSv/h per MBq), the claim ceiling (D-45). Live times 10 / 60 / 300 s; ideal, and 0.10 µSv/h ambient.
  - *Detection limit.* Stripped count Y = n662 − R·n_ref, σ₀² = μ662|H₀ + R²·μ_ref, Currie limits computed exactly
    (two-window Poisson) at α = β = 5 %. Validated by acquisitions: **864 / 864** false-positive and detection rates
    within 4 binomial SE of their exact expectation (1000 × 128 acquisitions each; z mean +0.03, SD 1.01). Co-located,
    side window, ideal (median over seeds, quartiles within ±0.5 %):

    | Co-60 at 1 m (µSv/h at the head) | L_D at 10 s | 60 s | 300 s |
    |---|---|---|---|
    | none | 3.0 counts | 3.0 | 3.0 |
    | 1 MBq (0.35) | 16.2 counts = 0.86 MBq Cs | 35.9 = 0.32 MBq | 77.1 = 0.14 MBq |
    | 10 MBq (3.5) | 45.6 = 2.43 MBq | 108 = 0.96 MBq | 238 = 0.42 MBq |
    | **28.7 MBq (10, claim ceiling)** | **75.5 = 4.0 MBq (Co : Cs 7.1)** | **181 = 1.61 MBq (17.9)** | **402 = 0.71 MBq (40.3)** |
    | 100 MBq (35, beyond the claim) | 139 = 7.4 MBq | 336 = 3.0 MBq | 747 = 1.33 MBq |

    At fixed Co-60 the limit falls as √(A_Co / t) (60 s ÷ 300 s at 28.7 MBq: 2.25 against √5 = 2.24); at a fixed
    Co : Cs ratio it does not depend on t. The side window costs +4.1–4.6 % in L_D against co1332 and +17 % against
    both lines. Ambient 0.10 µSv/h (bare bound) matters only at weak Co-60 (60 s: no Co 3.0 → 22.8 counts; 1 MBq
    +16 %; ≥ 10 MBq ≤ 2 %). **Window counting** (no stripping) reads 1 Bq of Co-60 as **0.489 Bq of Cs-137** (on axis;
    0.51–0.55 off axis) — at 10 µSv/h, 60 s, 1 582 Cs-equivalent counts, 8.7 × L_D: a bias, not a detection limit.
    An instrument's plug-in decision (σ₀ from the counts, calibrated R) matches the exact rule from ~30 counts on
    (false positives 4.5–5.3 %); the R calibration's precision (0.27 %) adds at most 0.6 points within the claim.
  - *Imaging.* A stripped trust statistic Z_s (per-pixel R_i; decoded stripped image studentised by its Poisson
    variance) with thresholds selected per configuration on 65 536 Cs-free Co-60 + ambient nulls by the PR-SENS-02
    rule (CAL-03): **135 / 135** informative configurations ≤ 1 % false trusted on 8 192 fresh nulls each (pooled 0.303 %, worst
    upper limit 0.61 %; 9 further configurations — ideal, no Co-60 — have no counts at all and are not a test); trusted at Co-60's position ≤ 0.06 %. **≥ 95 % trusted and within one angular element (1.04°)** of a
    Cs-137 source two elements from Co-60 needs **500 / 1000 / 2000** Cs-137 counts at 10 µSv/h and 10 / 60 / 300 s
    (Co : Cs ≈ 1.0 / 3.1 / 7.6) — 5–7 × the count limit; 250–500 counts without Co-60 (grid steps of ~2, so upper
    bounds within a step). The raw 662 keV window through the PR-SENS-02 gate, by contrast, trusts a **false Cs-137 at
    Co-60's position in 92–100 %** of Cs-free acquisitions at 10 µSv/h (10–60 s, 0.10 µSv/h ambient; 46–99 % at
    3.5 µSv/h). Co-located sources are reported as detection, not location.
  - *Systematics of R.* Per 1 % gain error R moves **+0.8 %** (side), **−13 %** (co1332), −3.6 % (both lines). The side
    window's R is flat over the array (0.851 edge → 0.862 centre; co1332 0.92 → 0.55) and moves ≤ 0.1 % with direction
    out to 3.5° (co1332 −1.6 % at 2.4°). At 10 µSv/h a +1 % gain error lifts the count false-positive rate to 8 %
    (60 s) / 14 % (300 s) with the side window; with co1332 −1 % lifts it to 93 % (60 s) and +1 % drops detection at
    L_D to 0.4 %. Keeping false positives ≤ 10 % at 10 µSv/h needs **|gain error| ≲ 1.6 % at 60 s and ≲ 0.7 % at
    300 s** (side window; co1332 would need ≲ 0.12 %). A wrong R leaves a trusted false "Cs" at Co-60's position in
    2.2 % of Cs-free acquisitions only at δR = −10 % (10 µSv/h, 300 s; 28 % at 35 µSv/h), ≤ 0.2 % for |δR| ≤ 5 % — the
    count decision flags such an error first.
  - *Limits.* No mask, housing or room scatter of Co-60 into the windows (the side window's advantage is "before
    scatter"); no cascade summing (negligible at 1 m); the Gaussian resolution model leaves 3.8 × 10⁻⁴ of the cs662
    Cs-137 counts in the side window, carried exactly; results are before a four-channel readout model; MC only.
- **Reproduce.** `montecarlo compton`, `compton-strip`, `mixediso`, `mixedstrip`, each with `samples/scenario.json`
  → `samples/compton_contamination.png`, `compton_strip_combined.png`, `mixediso.png`, `mixedstrip.png`. The
  activity-ratio scene (× 1 / 2 / 4 / 8, 4 × 10⁶ photons, 0.4 mm grid) is `samples/evidence` family `spatial`
  (`samples/evidence/probe`, mode `spatial`, N = 128). The cost at use distance: `montecarlo ambient-evidence`, family
  `csco`, through `samples/evidence/manifest-csco-v1.json` (`csco_selection` → `ambient/select_csco_thresholds.py` →
  pinned `ambient/csco-thresholds-v1.json`; `csco_validation` → `ambient/aggregate_csco.py` →
  `samples/evidence/results/csco-v1-validation.json`).
- **Tests.** `ComptonTests.Contamination_IsImagedAtTheContaminantSource_NotTheTarget`,
  `.Stripping_RecoversCsCount_EvenCoLocated`; `MixedFieldTests.MixedField_ThroughEnergyWindow_SeparatesIsotopesSpatially`
  (at Co × 2), `.ComptonStripping_RecoversCoLocatedCsCount`; the cost at use distance: `CsUnderCoTests` (exact
  Currie against enumeration, net-count variance, gain-shifted windows, per-pixel ratios, Z_s linearity).

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
| 2026-10-04 | Absolute ambient field added: a terrestrial K / U / Th spectrum from a soil / air transport generator (UNSCEAR 2000 kerma ratios 1.014 / 0.997 / 1.023, accepted within ±3 % as a model comparison), given as photon H*(10), bounded by a bare-crystal and a front-only geometry; a calibrated, background-aware search statistic for the trust gate (EV-34). The relative (BSR) background studies and every path with the field off are unchanged | No ideal value moved: the quoted ideal values stay as best-case bounds. New: EV-34; an "Under the absolute ambient field" item in EV-01, EV-02, EV-07, EV-09, EV-12 and EV-15 and the measured column of §1, each with this pipeline's own ideal beside it (its homogeneous crystal gives ideals that differ from the legacy ones, e.g. EV-02 0.5–1° narrower). EV-15 was re-measured at absolute activities because its recipe has no live time; its new R = 1.134 (total-deposit windows) is not comparable with R = 3.99 (per-pixel window). PR-SENS-02's gate became a measured significance gate |
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
