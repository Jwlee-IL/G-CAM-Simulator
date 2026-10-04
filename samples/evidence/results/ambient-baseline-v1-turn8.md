# TODO-30 baseline — turn 8 (AB-11 gate re-selection and re-validation, AB-13 families, AB-12 bias baseline)

2026-10-04. **This turn was done by a substitute Claude implementer (Claude subagent), not by Codex.** Specification:
`docs/PLAN.Physics.AmbientBackground.md` (AB-11 … AB-13). No docs, PLAN, Todo, VV, README, PAPER, CLAUDE / AGENTS edits;
no Studio default change; no git state change. Machine-readable results (no machine paths, LF only):

| File | What |
|---|---|
| `ambient-baseline-v1-turn8.json` | summary of the numbers quoted below |
| `ambient-baseline-v1-turn8-gate.json` | AB-11 validation: every null configuration, every source condition (now with the signed decoder error), count gates, derived ratios (`aggregate_gate.py`) |
| `ambient-baseline-v1-turn8-sources.csv` | the source conditions, one row each |
| `ambient-baseline-v1-turn8-ev12.json`, `-ev02.json`, `-ev15.json`, `-ev01.json` | the four AB-13 family aggregates (`aggregate_ev.py`) |
| `ambient-baseline-v1-turn8-bias.json` | the AB-12 / TODO-33 decoder-pull baseline (`bias_baseline.py`) |

All runs: engine = HEAD `9caa3af` plus this turn's uncommitted changes (the driver's `run-info.json` says
`engine_tree_dirty: true`); spectrum `terrestrial-unscear2000-v1.json` (SHA-256 `4d48bc42…35b3f4`, validated, AB-10);
fields 0.05 / 0.10 / 0.20 µSv/h photon H*(10); bounds BareCrystalAllFaces (upper) and FrontOnlyThroughMask (lower).
"Ideal" everywhere means the same pipeline, seed and source map with no field (paired), as in turn 7.

## 1. AB-11 — gate re-selection and re-validation (per-configuration thresholds)

**Why more nulls, and how many.** Turn 7's failures were what its sizes predict, not a model fault. With 4096 selection
nulls a threshold allowed ⌊0.003 × 4096⌋ = 12 exceedances, whose one-sided 95 % Clopper–Pearson upper limit is a true
rate of 0.47 %; and a validation of 2048 nulls passes only with ≤ 12 false trusted (13 / 2048 has upper 1.007 %), which a
perfectly calibrated 0.3 % rate exceeds with probability 1.05 % per configuration (≈ 1 of 96 by chance) and a 0.5 % rate
with 23 %. Where the expected background is < 1 count, Z takes few values (279–1826 distinct values in 65 536 nulls), so
probability sits in atoms and the quantile moves in steps. Chosen sizes:

| | nulls per configuration | allowed exceedances | upper 95 % of the true rate at T* | validation passes if false ≤ | P(fail) at a true 0.30 / 0.35 / 0.50 % |
|---|---:|---:|---:|---:|---|
| turn 7 | 4 096 / 2 048 | 12 | 0.474 % | 12 | 1.1 % / 3.1 % / 23 % |
| **turn 8** | **65 536 / 8 192** | **196** | **0.337 %** | **66** | 1.2 × 10⁻¹² / 6.8 × 10⁻¹⁰ / 1.1 × 10⁻⁴ |

(binomial; the per-seed counts' dispersion index was 0.78–2.37, which widens these but keeps them negligible.) Every atom
with probability ≥ 10⁻³ is now seen ≥ 65 times. The validation count (8 192 ≥ the required 2 048) uses the same seeds'
cheap extra nulls; nothing about α, the 1 % target or the rule changed.

- **Selection seeds:** F128[1:65] — turn 7's selection seeds — with **1024** null repeats each (65 536 per configuration;
  family `gate_selection_v2`, `manifest-ambient-v2.json`). Each configuration's null stream is the same keyed stream
  continued, so the first 64 repeats per seed are turn 7's: checked, all 6 144 (seed × configuration) sequences equal
  turn 7's runs value for value, and the strict turn-7 rule applied to them (`select_thresholds.py --rule strict
  --first-nulls 64`) reproduces `gate-thresholds-v1.json`'s thresholds, universal value and selection record exactly.
- **Tie rule (inclusive):** Z is recorded and compared at 4 decimals; "trusted" = recorded Z ≥ T*; T* = the smallest
  recorded value v with #(Z ≥ v) ≤ 196, so the selection exceedance fraction **including ties** is ≤ α (max 196 / 65 536 =
  0.2991 %); up to 28 nulls tie at a T*. Engine: `AmbientGateStudy.Thresholds.Comparison = "RoundedAtLeast"` (the
  turn-7 file names none and keeps `Z > T*`). Thresholds range 1.163 … 4.6245; vs turn 7, 45 rose and 51 fell
  (−0.19 … +0.35); universal (largest) 4.6245 (turn 7 4.6432).
- **Pinned before validation:** `samples/evidence/ambient/gate-thresholds-v2.json`, SHA-256
  `6352b2f692894fde1d83c6ef2c3ac2c04ee1b332c511e150d66342a516b6cdd0`, written 16:18:56 and pinned in
  `gate_validation_v2`'s overrides; the validation family started 16:19:27 (`run-info.json`, manifest SHA-256 at that
  moment `7abce343…de638a11`; the AB-13 families were appended to the manifest later, the gate families unchanged). The
  engine checks the pin per run.
- **Validation seeds:** `F256[128:256]` — a new list in `seeds.json` that continues the F128 rule (same formula,
  i = 127 … 254; `F256[0:128] == F128`, rule recorded in `_rules`); the 128 new seeds are distinct from each other and
  from O128, F128, the RTL lists and the pilot seed 777, so neither F128[1:65] nor O128[0:128] nor anything else was used.
  N = 128 as turn 7; 64 null repeats per seed (8 192 per configuration) and 300 source repeats per condition per seed.

**Result: 96 / 96 configurations pass** (one-sided 95 % upper limit of the false-trusted rate ≤ 1 %).

| | value |
|---|---|
| pooled false trusted | 2 424 / 786 432 = **0.308 %** (one-sided 95 % limits 0.298 % … 0.319 %) |
| worst configuration | `lab|t=10|F=0.1|FrontOnlyThroughMask|cs662` (B = 0.04 counts): 40 / 8 192 = 0.49 %, upper **0.64 %** |
| turn 7's three failures | `head1m|t=60|F=0.05|Front|cs662` 17 / 8 192 (upper 0.31 %); `head5m|t=10|F=0.05|Front|open` 28 (0.47 %); `head5m|t=10|F=0.1|Front|cs662` 23 (0.40 %) |
| configurations with B < 1 count (27) | 719 / 221 184 = 0.325 % |
| universal threshold 4.6245 (inclusive; reported only) | 96 / 96, at most 28 / 8 192 |

The pooled rate sits at α (its lower limit 0.298 % is below 0.3 %): calibrated. AB-11 is met with per-configuration
thresholds as specified; no failure to report.

## 2. Count gates under background with the re-selected thresholds (EV-07 / EV-09, N = 128)

Recipe unchanged from turn 7 (§2 there); only the thresholds, the tie rule and the seeds changed. Ideal reference in
this pipeline (lab centre / hand-held centre, open window, 60 s; mean ± SD over seeds):

| S (net counts) | 25 | 50 | 100 | 250 | 500 | 1000 |
|---|---|---|---|---|---|---|
| lab, RMS mm | 5.74 ± 0.21 | 3.39 ± 0.28 | 1.10 ± 0.22 | 0.42 ± 0.01 | 0.37 ± 0.00 | 0.36 ± 0.00 |
| lab, failure > 3 mm; sub-mm seeds | 44.6 %; 0 | 14.8 %; 0 | 1.1 %; 48 / 128 | 0; 128 / 128 | 0; 128 | 0; 128 |
| hand-held, RMS mm | 6.90 ± 0.20 | 5.49 ± 0.24 | 3.47 ± 0.25 | 1.00 ± 0.28 | 0.24 ± 0.10 | 0.20 ± 0.00 |
| hand-held, sub-mm seeds | 0 | 0 | 0 | 60 / 128 | 128 / 128 | 128 / 128 |

(turn 7: lab 5.73 / 3.35 / 1.14 / 0.42 / 0.37, sub-mm 35 / 128 at 100; hand-held 58 / 128 at 250 — RMS the same within
the seed spread; the sub-mm count at S = 100 (48 vs 35) sits where the per-seed RMS, 1.10 ± 0.22 mm, straddles 1 mm.) Smallest S with pooled trusted-and-correct ≥ 0.95 (ideal column: decoder ≥ 95 % within one resolution
element), read off the grid 25 / 50 / 100 / 250 / 500 / 1000:

| Case / window | ideal | front-only, any field | bare 0.05 / 0.10 / 0.20, 10 s | bare 0.05 / 0.10 / 0.20, 60 s |
|---|---|---|---|---|
| lab centre, open | 100 | 100 | 250 / 250 / 500 | 500 / 500 / 1000 |
| lab centre, cs662 | 100 | 100 | 100 / 100 / 100 | 100 / 250 / 250 |
| lab edge, open | 100 | 100–250 | 500 / 500 / 1000 | 1000 / 1000 / > 1000 |
| lab edge, cs662 | 100 | 250 | 250 / 250 / 250 | 250 / 250 / 250 |
| head centre, open | 250 | 100–250 | 500 / 500 / 500 | 1000 / 1000 / > 1000 |
| head centre, cs662 | 250 | 250 | 250 / 250 / 250 | 250 / 250 / 500 |
| head edge, open | 500 | 250 | 250 / 250 / 500 | 500 / 500 / 1000 |
| head edge, cs662 | 500 | 250 | 100 / 250 / 250 | 250 / 250 / 250 |
| head 1 m centre, open | 250 | 250 | 250 / 500 / 500 | 500 / 1000 / 1000 |
| head 1 m centre, cs662 | 250 | 250 | 250 / 250 / 250 | 250 / 250 / 500 |
| head 1 m 3°, open | 1000 | 500 | > 1000 (all) | > 1000 (all) |
| head 1 m 3°, cs662 | > 1000 | 1000 | 1000 / 1000 / 1000 | 1000 / 1000 / 1000 |
| head 5 m centre, open | 100 | 100 | 250 / 250 / 250 | 500 / 500 / 1000 |
| head 5 m centre, cs662 | 100 | 100 | 100 / 100 / 100 | 250 / 250 / 250 |
| head 5 m 3°, open | 250 | 250 | 500 / 1000 / 1000 | 1000 / > 1000 / > 1000 |
| head 5 m 3°, cs662 | 250 | 500 | 250 / 250 / 500 | 500 / 500 / 500 |

**Against turn 7:** of 448 gate entries (trusted-and-correct and decoder-within, every case / position / window /
exposure / environment) **438 are identical**; 10 move by one grid level. Four sit on the 0.95 line in both turns
(e.g. head centre open 10 s 0.20 bare at S = 500: 0.9498 → 0.9509; head 5 m 3° open 60 s 0.10 bare, decoder, S = 500:
0.9474 → 0.9508). Six are the hand-held 1 m 3° source, cs662, front-only (all fields, both times): at S = 1000 trusted
is 1.000 in both turns, so not a threshold effect, but correct-among-trusted moved 0.926–0.930 → 0.956–0.959 (the
decoder-within criterion stayed 0.947–0.950). That source sits at the edge of the optics (turn 7 finding 4) and its
correct fraction is clustered by seed (each seed's maps), so the 128-seed pooled value carries a seed-ensemble spread
larger than its binomial error suggests; the gate (1000 vs > 1000) should be quoted as marginal. Turn 7's findings 1–4
stand unchanged.

## 3. EV-12 — mask / antimask under the absolute field (family `ev12_antimask`, O128, N = 128)

Recipe (`ev12-antimask-request-v1.json`): lab geometry, centred source, **400 expected net source counts** in the window
on the mask in the full live time t (the legacy 400; activity = 400 / (t × the seed's mask rate): open 5.0 × 10⁵ Bq at
10 s, 8.3 × 10⁴ Bq at 60 s), 200 repeats; (a) single exposure decoded raw, (b) calibrated subtraction of the
instrument's background model (an independent 5 × 10⁵-photon ambient map × field × t — the legacy study's "known mean"),
(c) t/2 mask + t/2 inverted mask at the same activity, difference decoded — **equal total time**. Antimask background:
front-only = the field through the inverted mask (0.584 vs 0.561 cps/µSv/h open); bare = the same map (the side and
rear faces never see the mask). Source rate through the antimask is 2.5 % higher (8.21 vs 8.02 × 10⁻⁵ cps/Bq open), so
its half sees 205 counts, not 200 (the legacy study normalised each half to 200). RMS error mm, mean ± SD over seeds:

| open window, condition | background / pixel | single (raw) | calibrated | mask / antimask | antimask − calibrated (seeds antimask lower) | failures > 3 mm: calibrated / antimask (seeds with any) |
|---|---:|---|---|---|---|---|
| ideal | 0 | 0.38 ± 0.01 | 0.38 ± 0.01 | **0.30 ± 0.01** | −0.08 ± 0.01 (128 / 128) | 0 / 0 |
| 10 s, 0.05, bare | 0.57 | 0.46 ± 0.02 | 0.39 ± 0.01 | 0.31 ± 0.01 | −0.08 ± 0.01 (128) | 0 / 0 |
| 10 s, 0.10, bare | 1.13 | 0.68 ± 0.17 | 0.40 ± 0.01 | 0.32 ± 0.01 | −0.08 ± 0.02 (128) | 0 / 0 |
| 10 s, 0.20, bare | 2.26 | 3.64 ± 0.32 | 0.45 ± 0.02 | 0.35 ± 0.01 | −0.09 ± 0.03 (128) | 0.00 % (1) / 0 |
| 60 s, 0.05, bare | 3.39 | 7.28 ± 0.22 | 0.49 ± 0.06 | 0.39 ± 0.05 | −0.10 ± 0.08 (126) | 0.02 % (5) / 0.01 % (2) |
| 60 s, 0.10, bare | 6.78 | 9.10 ± 0.09 | 0.92 ± 0.25 | 0.61 ± 0.18 | −0.31 ± 0.31 (107) | 0.70 % (99) / 0.27 % (56) |
| 60 s, 0.20, bare | 13.57 | 9.03 ± 0.09 | 2.24 ± 0.32 | 1.69 ± 0.32 | −0.54 ± 0.46 (117) | 6.25 % (128) / 3.98 % (128) |
| front-only, all six | 0.002–0.048 | 0.38 | 0.37–0.38 | 0.30 | −0.08 (128) | 0 / 0 |
| cs662, ideal / all twelve | 0 / 0.000–0.138 | 0.40 / 0.40–0.42 | 0.40 / 0.40–0.41 | 0.31 / 0.31–0.32 | −0.09 (128) | 0 / 0 |

Legacy EV-12 (uniform pedestal, N = 128): calibrated vs antimask 0.38 / 0.31 (none), 0.46 / 0.38 (2 / px), 0.55 / 0.44
(4 / px), 0.91 / 0.63 mm (8 / px). **Findings.** (1) With subtraction the absolute field reproduces the legacy ranking
and magnitudes at matching pixel loads (2.3 / px: 0.45 / 0.35; 6.8 / px: 0.92 / 0.61); the antimask stays lower in
107–128 of 128 seeds at every level. (2) Both are sub-mm up to 6.8 counts / pixel (bare, 60 s, 0.10 µSv/h); at
13.6 / pixel both exceed 1 mm (2.24 / 1.69 mm) with failures in every seed — the legacy sweep stopped at 8 / px. (3) The
raw single-mask decode (no subtraction) collapses in the bare bound from 2.3 counts / pixel (3.64 mm, 15 % failures) to
~9 mm (99.9–100 % failures) — the AB-12 pull (§7), which subtraction removes. (4) In the front-only bound and in the
cs662 window the field is ≤ 0.14 counts / pixel and nothing moves from ideal. D-19 (no rotating mask) is untouched by
this: the comparison is a trade at equal time, as before.

## 4. EV-02 — field of view at field distance under the absolute field (family `ev02_fov`, F128, N = 64)

Recipe (`ev02-fov-request-v1.json`): the legacy FieldOfViewStudy recipe (hand-held head, S = 1 m / 5 m, x and diagonal,
0–20° in 0.5°, N0 = 500 / 5000 on-axis counts, 100 Poisson repeats per angle, 10⁶ photons per mean map, wide non-cyclic
decode ±20° / 0.3° and cyclic one-period decode, success = within 1.04°) with the BSR pedestal replaced by
field × t × the transported ambient map, t = 10 s and 60 s (activity × t = N0 / on-axis rate), **open window only**
(§9), and the centroid flag's threshold (95th percentile of the in-field centroid offsets) **re-calibrated under each
field condition on 100 separate draws per in-field angle** (the legacy study used the evaluated draws). Background
counts per acquisition (the same at 1 m and 5 m): bare 168 / 335 / 670 (10 s) and 1005 / 2011 / 4022 (60 s); front-only
0.6–15. Values: the most frequent over 64 seeds, with its frequency when not 64 / 64.

| S, direction, N0 | metric | ideal | bare 10 s: 0.05 / 0.1 / 0.2 µSv/h | bare 60 s: 0.05 / 0.1 / 0.2 µSv/h | front-only (any) |
|---|---|---|---|---|---|
| 1 m, x, 500 | usable half-field, non-cyclic (°) | 5.5 (57/64) | 4.5 (34/64) / 4.5 (55/64) / 3.5 (60/64) | 0 (41/64) / 0 (63/64) / 0 | 5.5 |
| 1 m, x, 500 | same, cyclic | 2.5 (52/64) | 2.5 (58/64) / 2.5 (63/64) / 2.5 | 2.5 / 0 (58/64) / 0 | 2.5 |
| 1 m, x, 5000 | non-cyclic | 6.5 | 6.5 / 6.5 / 6.5 | 6.5 (51/64) / 5.5 / 5 (63/64) | 6.5 |
| 1 m, x, 5000 | cyclic | 3 | 3 / 3 / 3 | 3 / 3 / 3 | 3 |
| 1 m, diag, 500 | non-cyclic | 2.5 | 1.5 (61/64) / 1.5 (56/64) / 0 | 0 / 0 / 0 | 2.5 |
| 1 m, diag, 500 | cyclic | 3.5 (45/64) | 2 (53/64) / 1.5 (63/64) / 1.5 (61/64) | 1.5 (40/64) / 0 / 0 | 3.5 |
| 1 m, diag, 5000 | non-cyclic | 5.5 | 5.5 / 5.5 / 5.5 (62/64) | 2.5 (53/64) / 2.5 / 2 (63/64) | 5.5 |
| 1 m, diag, 5000 | cyclic | 4 | 4 / 4 / 4 | 4 (59/64) / 2.5 / 2 | 4 |
| 5 m, x, 500 | non-cyclic | 5.5 (39/64) | 5.5 (62/64) / 5 (53/64) / 4.5 (61/64) | 4 (56/64) / 0 (62/64) / 0 | 5.5 |
| 5 m, x, 500 | cyclic | 3 | 3 / 3 / 0 | 0 / 0 / 0 | 3 |
| 5 m, x, 5000 | non-cyclic | 6.5 | 6.5 / 6.5 / 6.5 | 6.5 (53/64) / 6 / 6 (44/64) | 6.5 |
| 5 m, x, 5000 | cyclic | 3 | 3 / 3 / 3 | 3 / 3 / 3 | 3 |
| 5 m, diag, 500 | non-cyclic | 1.5 | 1.5 / 1.5 (54/64) / 0.5 | 0.5 / 0 (57/64) / 0 | 1.5 |
| 5 m, diag, 500 | cyclic | 1.5 (59/64) | 1.5 (47/64) / 1.5 (52/64) / 0 | 0 / 0 / 0 | 1.5 |
| 5 m, diag, 5000 | non-cyclic | 4 (63/64) | 4 / 4 / 4 | 4 (62/64) / 1.5 (57/64) / 1.5 | 4 |
| 5 m, diag, 5000 | cyclic | 4 (49/64) | 4 (57/64) / 4 (59/64) / 4 (63/64) | 4 / 4 (36/64) / 2.5 | 4 |

The out-of-field cue along x (flag ≥ 90 %: first contiguous range past the fully coded field; unflagged wrong spot:
largest fraction over 7.5–12°, median over seeds; correct side ≥ 95 %):

| S, N0 (x) | quantity | ideal | bare 10 s: 0.05 / 0.1 / 0.2 | bare 60 s: 0.05 / 0.1 / 0.2 | front-only, worst |
|---|---|---|---|---|---|
| 1 m, 500 | outside flag (°) | 5 (38/64) – 11.5 | 7.5 (42/64) – 9.5 (38/64) / never (56/64) / never | never (64/64) for all three | — |
| 1 m, 500 | unflagged wrong spot, 7.5–12° | 0.09 | 0.37 / 0.46 / 0.51 | 0.58 / 0.66 / 0.74 | 0.13 |
| 1 m, 500 | correct side (°) | 1 – 20 | 1 – 13.5 / 1.5 – 13 / 1.5 – 12.5 (modes 33–61 / 64) | 1.5 – 12 / 2 – 11 / 3.5 – 3.5 (weak modes) | — |
| 1 m, 5000 | outside flag (°) | 4 (62/64) – 12.5 (61/64) | 4 – 12 / 4 – 12 / 4 (48/64) – 11.5 (51/64) | 4 – 11.5 / 4.5 – 11 / 4.5 – 10 (35–59 / 64) | — |
| 1 m, 5000 | unflagged wrong spot, 7.5–12° | 0.00 | 0.00 / 0.01 / 0.05 | 0.13 / 0.22 / 0.36 | 0.00 |
| 1 m, 5000 | correct side (°) | 1 – 20 | 1 – 20 (all) | 1 – 20 (all) | — |
| 5 m, 500 | outside flag (°) | 5.5 – 11.5 (51/64) | 8 – 9 (≤ 29/64) / never (63/64) / never | never (64/64) for all three | — |
| 5 m, 500 | unflagged wrong spot, 7.5–12° | 0.06 | 0.39 / 0.57 / 0.74 | 0.83 / 0.91 / 0.96 | 0.08 |
| 5 m, 5000 | outside flag (°) | 4 (62/64) – 12.5 | 4 – 12.5 / 4 – 12 / 4 – 12 (40–63 / 64) | 4.5 – 11.5 / 4.5 – 11 / 5 – 10 (45–62 / 64) | — |
| 5 m, 5000 | unflagged wrong spot, 7.5–12° | 0.00 | 0.00 / 0.00 / 0.03 | 0.14 / 0.58 / 0.86 | 0.00 |

Legacy EV-02 (conventional detector, BSR pedestal): non-cyclic along x 7.0° / 7.5° (N0 5000), 6.0° / 7.0° (N0 500),
5.0° / 6.5° at BSR 1 (N0 500); cyclic 3.0–3.5°; flag 4.0–12.5° (N0 5000), 6.0–11.5° (N0 500), never with BSR 1;
unflagged wrong spot ≤ 7 % without background, up to 49 % (N0 500) / 29 % (N0 5000) with BSR 1. **Findings.** (1) This
pipeline's ideal is 0.5–1° narrower than the legacy (5.5° / 6.5° vs 6.0–7.0° / 7.0–7.5° along x) — a detector-model
difference, kept beside every ambient value. (2) Front-only bound: no change at any field and time (≤ 15 background
counts). (3) Bare bound: at N0 5000 the field costs ≤ 1.5° along x up to B = 4022 (B/N0 0.8) and up to 3.5° on the diagonal,
while the unflagged wrong spot at 7.5–12° rises to 0.13–0.86 at 60 s; at N0 500 the usable non-cyclic field along x
falls to 3.5–5.5° at 10 s (B/N0 0.34–1.3) and to 0–4° at 60 s (B/N0 2–8; 0° from 0.10 µSv/h), and the centroid flag stops working from B/N0 ≈ 0.7 — the legacy "BSR 1
→ never" now with an absolute rate: 0.10 µSv/h × 10 s already gives it for a source that yields 500 counts on axis.
(4) The diagonal is the weaker direction throughout (non-cyclic N0 500 ideal only 1.5–2.5°).

## 5. EV-15 — Co-60 / Cs-137 under the field: premise problem, measured as declared (family `ev15_separation`, O128, N = 128)

**Premise.** The task says "take the exposure and activities from the legacy EV-15 recipe". The legacy recipes
(`mixedstrip`, `mixediso`, probe `spatial`) state **activities in the config of 1 Bq Cs-137 and 8 Bq Co-60** and a
**photon budget of 4 × 10⁶** split by emission weight — **no live time**; their counts are weighted mean-map counts
("not observed events", EV-15). The only live time the recipe implies literally is budget / emission rate
= 4 × 10⁶ / (1 × 0.851 + 8 × 1.998) s = **237 600 s (66 h)**. I ran that literal reading (declared in
`ev15-separation-request-v1.json`; activities and live time are request fields, so another reading is a one-line change)
and report it, but it is not a meaningful field scenario: at 1 Bq the Cs-137 signal is 6 counts in 66 h.

Recipe: lab geometry, Cs at (4, 0) + Co at (−5, 3) mm ("separated") and both at (0, 0) ("co-located"); windows cs662
(595.5–727.9 keV) and co1332 (1199.3–1465.8 keV, ± 10 % of 1332.5 as the legacy); R = Co downscatter into cs662 ÷ Co
photopeak, from a separate noiseless Co-only map at the axis (legacy calibration): **R = 1.134 ± 0.009** in this
pipeline (legacy 3.99 — the legacy uses the per-pixel-deposit window, this pipeline the event's total deposit; not
comparable). 200 repeats per condition; per seed the median relative error of each Cs-count estimate, then median
[quartiles] over seeds; "subtracted" = the instrument's background model (independent ambient map × field × t)
removed from both windows before stripping.

| Condition (separated scene; the co-located values lie inside these seed quartiles — JSON) | S (true Cs in cs662) | B in cs662 / co1332 | legacy per-pixel floored | unfloored | subtracted (per-acquisition SD of rel. error) | Cs located within 1 mm |
|---|---:|---|---|---|---|---:|
| ideal | 6.14 | 0 / 0 | +3.74 [3.71, 3.85] | −0.06 [−0.15, 0.02] | −0.06 (1.34) | 2.7 % |
| front-only 0.05 / 0.10 / 0.20 | 6.14 | 533 / 519; 1067 / 1038; 2134 / 2076 | +34 / +58 / +106 | −8.7 / −17 / −34 | ≈ 0 (5.8 / 8.1 / 11.3) | 1.4–1.6 % |
| bare 0.05 / 0.10 / 0.20 | 6.14 | 19 716 / 11 331; 39 432 / 22 661; 78 863 / 45 323 | +1174 / +2335 / +4655 | +1124 / +2245 / +4485 | ≈ 0 (30 / 43 / 60) | 0 |

(relative errors as fractions: +1174 = 117 400 %). **Findings (of the literal reading).** Even ideal, 6 Poisson counts
give a ±134 % spread per acquisition and the Cs peak is found within 1 mm in 2.7 % of acquisitions — the legacy's
"13 % median error" and "126 / 128 sub-mm" are mean-map (noiseless) properties, not acquisition properties. Under the
field the Cs window holds 87–12 800 × more background than Cs; stripping without background subtraction is off by
+1.1 × 10⁵ … +4.5 × 10⁵ % in the bare bound and −870 … −3400 % front-only (there the Co-window background
over-subtracts); with a background model it is unbiased but its per-acquisition spread is 6–60 × S. Nothing is recoverable
at 1 Bq / 66 h in either bound. **Pending (author / planner):** an absolute activity and live time for EV-15 (e.g. the
gate study's 10 s / 60 s with Cs at a declared S and Co : Cs = 1, 2, 4, 8 by activity); cost after the decision ≈ 8 min
of runs at N = 128 (39 s per seed for both scenes, 10 jobs), no new code.

## 6. EV-01 — localisation at the recipe's activity and live time (family `ev01_sweep`, O128, N = 64)

Recipe (`ev01-sweep-request-v1.json`): the legacy `sweep` (±18 mm in 1.5 mm steps = 625 positions, 300 000 photons per
position map, cyclic and non-cyclic decoding on the same ±18 mm / 0.75 mm grid, localized = error < 3 mm) and the
`precise` single runs (lab centre, (6, 0), (12, 0); hand-held centre and (8, 0) — the scenario's own decoder), at the
**activity and live time the recipe's scenarios carry: `Source.ActivityBq` = 1 MBq and `Source.AcquisitionTimeSeconds`
= 1 s** (explicit in `scenario_handheld.json`, the engine defaults in `scenario.json`). That gives S ≈ 80 counts (lab,
open) / 26 (lab, cs662) and 195 / 75 (hand-held) on axis. Each position: 32 Poisson acquisitions per condition; the
sweep result is the expected number of localized positions, Σ over positions of the fraction within 3 mm (mean ± SD
over seeds); fixed points: 300 acquisitions (cross-correlation), 30 (MLEM, 80 iterations, EV-11). Background at 1 s:
bare 8 / 16 / 33 counts (lab) and 17 / 34 / 67 (hand-held) in the open window; ≤ 0.9 in cs662 and ≤ 0.3 front-only.

| Expected localized positions (of 625) | legacy noiseless | ideal, 1 MBq × 1 s | bare 0.05 / 0.10 / 0.20 µSv/h | front-only (all) |
|---|---:|---|---|---|
| lab, non-cyclic, open | 278 | 180.4 ± 1.5 | 162.3 ± 1.2 / 145.4 ± 1.5 / 115.3 ± 1.3 | 180.0–180.4 |
| lab, cyclic, open | 146 | 94.4 ± 1.2 | 85.9 ± 1.3 / 77.7 ± 1.1 / 63.2 ± 1.0 | 94.1–94.3 |
| lab, non-cyclic / cyclic, cs662 | — | 84.7 / 47.9 | 84.6 / 84.3 / 83.6 and 47.8–48.0 | 84.6–84.9 / 47.8–48.4 |
| hand-held, non-cyclic, open | 361 | 229.9 ± 1.5 | 204.0 ± 1.1 / 181.3 ± 1.3 / 144.7 ± 1.3 | 229.4–229.8 |
| hand-held, cyclic, open | 174 | 112.2 ± 1.1 | 103.4 ± 1.2 / 94.6 ± 1.1 / 78.5 ± 1.2 | 111.9–112.2 |
| hand-held, non-cyclic / cyclic, cs662 | — | 151.1 / 74.5 | 150.6 / 150.0 / 148.7 and 74.0–74.8 | 150.8–151.0 / 74.5–74.8 |

Fixed points, open window (RMS error mm, mean ± SD over seeds; share within 3 mm in brackets):

| Point | legacy (noiseless) | ideal | bare 0.05 / 0.10 / 0.20 | MLEM ideal → bare 0.20 |
|---|---|---|---|---|
| lab centre (S 80) | 0.353 mm | 1.70 ± 0.27 (0.967) | 2.48 / 3.29 / 4.76 ± 0.20 (0.929 / 0.878 / 0.742) | 4.27 → 5.13 mm |
| lab (6, 0) | 6 → 6.03 mm | 2.91 ± 0.30 (0.919) | 4.00 / 5.04 / 6.77 (0.849 / 0.762 / 0.570) | 5.59 → 6.51 |
| lab (12, 0), outside the field | ghost −6.40 mm | at the ghost (within 3 mm of −6.67) in 0.839 | at the ghost 0.733 / 0.613 / 0.396; never within 3 mm of the truth | ghost-like (mean x error −17.6 → −15.3 mm) |
| hand-held centre (S 195) | 0.25 mm floor (EV-09) | 1.53 ± 0.23 (0.972) | 2.11 / 2.73 / 4.05 (0.945 / 0.907 / 0.791) | 1.99 → 3.15 |
| hand-held (8, 0) | 0.53 mm (EV-09) | 5.56 ± 0.33 (0.772) | 7.11 / 8.40 / 9.99 (0.614 / 0.446 / 0.184) | 1.99 → 5.20 (0.960 → 0.805) |

In cs662 the lab centre has 26 counts (RMS 5.7 mm ideal, unchanged by any field: 5.67–5.76) and the hand-held centre 75
(4.9 mm; 4.87–5.00). **Findings.** (1) The legacy EV-01 numbers are noiseless mean-map values; at the recipe's own
1 MBq × 1 s the lab detects only ~80 counts and the expected localized area is 65 % of the legacy (non-cyclic 180 vs
278) even without a field — the count budget dominates, not the field. (2) The field's own cost, bare bound, open
window: −10 / −19 / −36 % of that area at 0.05 / 0.10 / 0.20 µSv/h (lab, non-cyclic; hand-held −11 / −21 / −37 %);
non-cyclic still gives 1.8–2.1× the cyclic area in every condition. (3) Front-only: within 0.4 positions of ideal;
cs662 window, bare bound: at most −1.1 (lab) / −2.4 (hand-held) positions (≤ 1.6 %). (4) Outside the field the cyclic
ghost persists under background (40–84 % of acquisitions at the ghost); the field turns some ghost answers into other wrong spots, not into correct ones.

## 7. AB-12 / TODO-33 — the raw decoder's pull under the field (finding, not fixed)

From the gate re-validation (signed mean error per seed, then mean ± SD over 128 seeds; `ambient-baseline-v1-turn8-
bias.json`). Pull = length of the mean error vector at the source plane; ideal = the paired no-field acquisitions.
Cross-correlation decoder, **bare bound, open window**, B/S = expected background ÷ net source counts:

| Case, position | t, S | ideal pull | 0.05 µSv/h | 0.10 µSv/h | 0.20 µSv/h |
|---|---|---|---|---|---|
| lab 160 mm, centre | 10 s, 250 | 0.36 mm | 0.42 mm (B/S 0.33) | 1.39 mm (0.65) | 6.17 mm, 2.2° (1.3) |
| lab, centre | 60 s, 250 | 0.37 | 7.88 mm, 2.8° (1.95) | 8.13 (3.9) | 8.21 mm, 2.9° (7.8) |
| lab, centre | 60 s, 1000 | 0.36 | 0.43 (0.49) | 1.47 (0.98) | 8.13 mm, 2.9° (1.95) |
| lab, centre | 1 MBq, 10 / 60 s | 0.36 | 0.37 (0.10) | 0.38 (0.20) | 0.42 (0.41) |
| lab, 8 mm edge | 60 s, 1000 | 0.64 | 0.71 (0.49) | 6.56 (0.98) | 10.33 mm, 3.7° (1.95) |
| hand-held 155 mm, centre | 10 s, 250 | 0.17 | 3.19 mm, 1.2° (0.67) | 6.25 (1.34) | 7.09 mm, 2.6° (2.7) |
| hand-held, centre | 60 s, 1000 | 0.19 | 8.09 mm, 3.0° (1.0) | 9.13 (2.0) | 9.17 mm, 3.4° (4.0) |
| hand-held, 8 mm edge | 1 MBq, 10 s | 0.55 | 0.63 (0.10) | 0.60 (0.19) | 5.82 mm, 2.2° (0.39) |
| hand-held 1 m, centre | 60 s, 1000 | 2.63 | 5.34 mm, 0.31° (1.0) | 7.50 (2.0) | 16.34 mm, 0.94° (4.0) |
| hand-held 1 m, centre | 1 MBq, 60 s | 1.77 | 15.28 mm, 0.88° (3.5) | 17.98 (7.1) | 18.98 mm, 1.09° (14) |
| hand-held 5 m, centre | 60 s, 1000 | 38.1 mm (0.44°) | 37.31 mm (1.0) | 38.96 (2.0) | 50.92 mm, 0.58° (4.0) |

In the cs662 window and the front-only bound (lab and hand-held at 155 mm, the 377 conditions whose ideal pull is
below 1 mm) the excess over the ideal pull is at most 0.73 mm (lab centre, cs662, S = 50, bare 0.20 µSv/h, 60 s:
B/S 0.40) and ≤ 0.1 mm wherever B/S < 0.1.

**Finding.** The pull is not a gradual shift but a switch: below B/S ≈ 0.5 it is the ideal floor, between ≈ 0.6 and 1.3 a growing share of acquisitions jumps to the background's own correlation peak,
and above B/S ≈ 2 the mean saturates at ~8 mm (lab, 2.9°), ~9 mm (hand-held at 155 mm, 3.4°), ~19 mm at 1 m (1.1°) and
~40–50 mm at 5 m (0.5°) — the location the decoder assigns to the bare-bound background shape. That shape is far from flat in the open
window (edge-to-centre pixel rate 2.7 lab, 3.3 hand-held; 0.99–1.01 in cs662, 0.72–0.76 front-only), and the decoder's
answer for the expected bare open map alone is (0.8 ± 1.4, −8.8 ± 0.2) mm at the lab and (4.4, 8.4) mm at the
hand-held head (EV-01 family, 64 seeds) — the point the 60 s gate acquisitions saturate at: lab (0.9, −8.2),
hand-held (4.3, 8.1) mm. At 5 m the ideal itself is pulled 38 mm at S = 1000 (grid
and low-count effects), so the field's excess there is small. The background-studentised search statistic (§1–2)
does not show it (turn 7 finding 2).

**MLEM** (EV-11's decoder, no background term, 80 iterations; EV-01 fixed points at 1 MBq × 1 s, 30 acquisitions × 64
seeds): its noiseless answer for the bare open map lies elsewhere — (8.2, −7.8) mm lab, (−2.0, −5.1) mm hand-held — and
its pull grows more slowly than cross-correlation's: hand-held 8 mm edge, mean x-error −0.04 → −0.29 / −0.79 / −1.44 mm
(cross-correlation −1.37 → −2.51 / −3.72 / −5.61 mm), within-3-mm share 0.960 → 0.805 (cross-correlation 0.772 →
0.184); hand-held centre RMS 1.99 → 3.15 mm (cross-correlation 1.53 → 4.05). At the lab (80 counts) MLEM is worse than
cross-correlation even ideal (4.27 vs 1.70 mm), so no ranking is claimed there. Baseline for TODO-33: in the bare bound
both decoders need a background-shape model once B/S exceeds ≈ 0.5; MLEM with its background term (b_i in its update)
is the natural candidate — not measured here.

## 8. Budget

| Step | Wall clock (10 parallel jobs) |
|---|---|
| AB-11 selection, 64 seeds × 1024 nulls | ~10 min (16:08–16:18; 65–97 s per seed) |
| AB-11 validation, 128 seeds | ~27 min (16:19–16:47; mean 125 s per seed) |
| EV-12 + EV-15, 128 + 128 seeds (one pool) | ~27 min (16:51–17:18; mean 84 s / 39 s per seed) |
| EV-02, 64 seeds (open window) | ~92 min (17:20–18:52; mean 820 s per seed) |
| EV-01, 64 seeds | ~53 min (18:53–19:47; mean 526 s per seed) |
| pilots (seed 777, labelled, not evidence) | EV-12 90 s, EV-15 37 s, EV-02 29 min (both windows, under load), EV-01 9.5 min |

Total ≈ 3.5 h of runs inside ≈ 3.7 h of wall clock from the first edit (16:06–19:47; ≈ 4 h with the reading before it) —
at the ~4 h guidance; the families were run in the priority order (AB-11, then EV-12 / EV-15 together because both were cheap, then EV-02, then EV-01). N and null counts were never
reduced; the one scope cut is EV-02's cs662 window (§9).

## 9. Deviations and limits

1. **EV-02 open window only.** The timed pilot (both windows) took 29 min per seed under load; 64 seeds would have taken
   ~3 h. The open window is the legacy study's analogue (it counts every detected photon) and the worst case: at the
   hand-held head the cs662 ambient rate is 1.3 % of the open one (bare 4.28 vs 335 cps per µSv/h), so B/N0 ≤ 0.1 there
   even at 60 s, 0.20 µSv/h. **Pending:** cs662 for EV-02, ≈ 55 min of runs at N = 64 with the new narrow decoder path
   (no code).
2. **EV-15** run at the literal live time of the legacy photon budget (§5); a meaningful absolute recipe needs a
   decision.
3. Source and ambient both use the ambient bound's homogeneous crystal (as turn 7); every family's ideal is this
   pipeline's own, beside the legacy value.
4. EV-12's antimask half receives the inverted mask's own source rate (+2.5 %), not the legacy's renormalised 200 counts.
5. EV-02's flag threshold is calibrated on separate draws (legacy: the evaluated draws) — slightly more conservative.
6. EV-01 at the scenario's 1 MBq × 1 s, read as "the legacy recipe's activity and exposure": the sweep itself is a
   noiseless 300 000-photon mean map with no exposure; the scenario clone carries `ActivityBq` and
   `AcquisitionTimeSeconds`. Another reading changes the count budget, which dominates the result (§6).
7. Engine change for speed, exact by construction: `CorrelationSearch.Reconstruct` now uses SIMD and, when the absolute
   count sum is ≤ 32 767, 8-bit weights with 16-bit sums (|recon| ≤ Σ|n|, so no overflow); results are bit-identical
   (tests: decoder equality on both sides of the switch and on the wide EV-02 grid; the EV-02 rows of a re-run with
   the new path equal the pilot's row for row).
8. AB-3 high-energy limits apply (open window contains K-40 / Tl-208).

## 10. Tests and build

`dotnet build Gcam.sln -c Release`: 0 errors, 2 warnings (the pre-existing xUnit2012 / xUnit2000). `dotnet test Gcam.sln
-c Release --no-build` (process-local GCAM_UI_TESTS=0, GCAM_RENDER_SNAPSHOTS=0, GCAM_EVIDENCE_TESTS=0): engine
378 → **391**, Studio.Core 184 → 184, services 87 + 7 skipped → same, UI 13 + 14 skipped → same; all pass (실패 0).
New tests (13): `AmbientGateStudyTests` (3: the turn-7 comparison is the default and does not trust a tie; the AB-11
comparison trusts a tie at the recorded precision and never a missing candidate; an unknown comparison is refused);
`CorrelationSearchTests` (+4: exact equality with the decoder on both sides of the 8/16-bit ↔ 32-bit switch — absolute
count sums 32 767 / 32 768 / 250 000 — and on the > 8192-point EV-02 grid with signed counts); `AmbientEvidenceTests`
(6: a multi-line source map is the sum of its lines on their own streams; linear in the intensity; EV-12 ideal
calibrated = single and bare antimask background = exactly half; EV-02 on-axis expected counts = N0, fractions in
[0, 1], one flag threshold per series; EV-15 ideal subtracted = unfloored and Co adds to the 662 window; EV-01 tallies
and the per-bound pull). No golden expectation changed. No legacy study, the BSR path, the list-mode path or Studio was
touched (AB-8); the one shared engine change is `CorrelationSearch` (used by the gate study only), exact as tested.

## 11. Reproduce

```powershell
dotnet build Gcam.sln -c Release
$m = 'samples/evidence/manifest-ambient-v2.json'
python samples/evidence/run_seeds.py --manifest $m --out <dir> --family gate_selection_v2 --jobs 10
python samples/evidence/ambient/select_thresholds.py --runs <dir> --out <thr.json> --manifest $m --family gate_selection_v2 --rule inclusive   # = gate-thresholds-v2.json
python samples/evidence/ambient/select_thresholds.py --runs <dir> --out <v1.json> --manifest $m --family gate_selection_v2 --rule strict --first-nulls 64   # = turn 7's thresholds
python samples/evidence/run_seeds.py --manifest $m --out <dir> --family gate_validation_v2 --jobs 10
python samples/evidence/ambient/aggregate_gate.py --runs <dir> --manifest $m --family gate_validation_v2 --out <gate.json> --csv <sources.csv>
python samples/evidence/run_seeds.py --manifest $m --out <dir> --family ev12_antimask ev15_separation ev02_fov ev01_sweep --jobs 10
python samples/evidence/ambient/aggregate_ev.py --runs <dir> --family ev12_antimask --out <ev12.json>      # likewise ev02_fov, ev15_separation, ev01_sweep
python samples/evidence/ambient/bias_baseline.py --gate <gate.json> --sweep <ev01.json> --out <bias.json>
```

APPROVAL REQUESTS: none.
