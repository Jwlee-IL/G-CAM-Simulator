# TODO-30 baseline — turn 9 (AB-14: EV-15 at absolute activities, EV-02 in the cs662 window)

2026-10-04. **This turn was done by a substitute Claude implementer (Claude subagent), not by Codex.** Specification:
`docs/PLAN.Physics.AmbientBackground.md`, decision AB-14 (commit `9f5ed23`). No docs / PLAN / Todo / VV / README / PAPER /
CLAUDE / AGENTS edits, no Studio change, no git state change. Base: HEAD `9f5ed23` (turn 8 committed as `aa9fcc4`,
`b6ebb26`). Machine-readable results (portable paths, LF):

| File | What |
|---|---|
| `ambient-baseline-v1-turn9.json` | summary of the numbers below |
| `ambient-baseline-v1-turn9-ev15a.json`, `-ev15b.json` | EV-15 (a) / (b) aggregates (`aggregate_ev.py`) |
| `ambient-baseline-v1-turn9-ev02-cs662.json` | EV-02 cs662 aggregate; compare with turn 8's `ambient-baseline-v1-turn8-ev02.json` (open) |

Manifest `samples/evidence/manifest-ambient-v3.json` (families `ev15_abs_a`, `ev15_abs_b`, `ev02_fov_cs662`); requests
`samples/evidence/ambient/ev15-separation-request-v2a.json`, `-v2b.json`, `ev02-fov-request-v2-cs662.json`. Spectrum
`terrestrial-unscear2000-v1.json` (validated, AB-10); fields 0.05 / 0.10 / 0.20 µSv/h; bounds BareCrystalAllFaces and
FrontOnlyThroughMask; "ideal" = same seed and maps, no field.

## 1. EV-15 at absolute activities (AB-14; O128, N = 128 each)

**Recipe.** Lab geometry (`scenario.json`), **60 s** live time; (a) **Cs-137 1 MBq + Co-60 8 MBq**, (b) **Cs-137 1 MBq +
Co-60 2 MBq** (661.7 keV × 0.851; 1173.2 + 1332.5 keV × 0.999); scenes "separated" (Cs (4, 0), Co (−5, 3) mm) and
"co-located" (both (0, 0)); the legacy decoding (non-cyclic, half extent 8.87 mm, 0.4 mm step; two peaks ≥ 3 mm apart);
200 exact-Poisson acquisitions per condition per seed, the cs662 and co1332 images drawn independently (disjoint windows
of one Poisson process).

**Window definition and R.** Both windows are on the event's **total deposit**, the event placed at its largest-deposit
pixel (this pipeline's event model, `ComptonStrategy.Argmax`): cs662 = 595.53–727.87 keV, co1332 = 1199.25–1465.75 keV
(± 10 %), pulse height exact (the lab scenario's resolution is 0). R = cs662 ÷ co1332 counts of a separate noiseless
Co-only map at the axis (laboratory calibration, as `mixedstrip`): **R = 1.1338 ± 0.0092** (N = 128). The legacy
R = 3.99 still used a different definition — `ComptonStrategy.PerPixelWindow` (each pixel's own deposit counted in the
window; checked in `ComptonCrystalDetector`), so a Compton-scattered Co photon can add counts to several pixels — and
the two R values are **not comparable**; no attempt was made to reconcile them.

**Estimates against the true Cs count S** (expected Cs counts in cs662; relative error per acquisition, then per-seed
median, then median [quartiles] over seeds; "SD" = median over seeds of the per-acquisition SD):
- stripping, unfloored: Σ n662 − R Σ nCo; background-subtracted: the same after removing the instrument's background
  model (independent ambient map × field × t) from both windows; legacy floored: Σ max(0, n662 − R nCo) per pixel;
- spatial: the 662-window reconstruction value at the peak matched to Cs, divided by the peak value per count of the
  noiseless Cs-only map (separated scene only; co-located sources share one peak);
- Cs location: raw 662 image, two peaks, matched to Cs (legacy `spatial`); and the stripped reconstruction
  recon(n662) − R·recon(nCo) (decoder is linear, so this is the decode of the stripped image); Co location: the Co-window
  image, the decoder's estimate.

**(a) Cs 1 MBq + Co 8 MBq** — N = 128, R = 1.1338 ± 0.0092

| Scene, condition | S (Cs in cs662) | window counts cs662 / co1332 | B cs662 / co1332 | stripping, unfloored | stripping, background-subtracted | legacy floored | spatial (at matched Cs peak) | Cs within 1 mm: raw two-peak / stripped recon | Co median error (mm) |
|---|---:|---|---|---|---|---|---|---|---:|
| separated, ideal | 1550 | 8848 / 6501 | 0.0 / 0.0 | -4.5 % [-8.7, -1.0] (SD 8.4 %) | -4.5 % [-8.7, -1.0] (SD 8.4 %) | +16.8 % | +141.4 % [+138.5, +144.2] (SD 10.0 %) | 0.000 / 0.497 | 1.07 |
| separated, 0.05 bare | 1550 | 8853 / 6504 | 5.0 / 2.9 | -4.0 % [-8.6, -0.8] (SD 8.5 %) | -4.1 % [-8.7, -1.0] (SD 8.5 %) | +17.0 % | +141.2 % [+139.1, +145.1] (SD 9.9 %) | 0.000 / 0.495 | 1.07 |
| separated, 0.1 bare | 1550 | 8858 / 6507 | 10.0 / 5.7 | -4.3 % [-8.6, -0.8] (SD 8.4 %) | -4.6 % [-8.8, -1.0] (SD 8.4 %) | +17.1 % | +141.7 % [+138.8, +144.6] (SD 10.1 %) | 0.000 / 0.494 | 1.07 |
| separated, 0.2 bare | 1550 | 8868 / 6513 | 19.9 / 11.4 | -4.4 % [-8.4, -0.4] (SD 8.5 %) | -4.9 % [-8.9, -0.9] (SD 8.5 %) | +17.5 % | +141.6 % [+139.2, +144.6] (SD 10.0 %) | 0.000 / 0.487 | 1.07 |
| separated, 0.05 front | 1550 | 8848 / 6502 | 0.1 / 0.1 | -4.6 % [-8.2, -1.2] (SD 8.5 %) | -4.6 % [-8.2, -1.2] (SD 8.5 %) | +16.7 % | +141.2 % [+138.8, +144.8] (SD 10.0 %) | 0.000 / 0.494 | 1.07 |
| separated, 0.1 front | 1550 | 8848 / 6502 | 0.3 / 0.3 | -4.4 % [-8.7, -0.8] (SD 8.5 %) | -4.4 % [-8.7, -0.8] (SD 8.5 %) | +16.7 % | +141.3 % [+138.7, +144.4] (SD 10.2 %) | 0.000 / 0.497 | 1.07 |
| separated, 0.2 front | 1550 | 8849 / 6502 | 0.5 / 0.5 | -4.4 % [-8.7, -1.2] (SD 8.5 %) | -4.3 % [-8.7, -1.2] (SD 8.5 %) | +16.7 % | +141.1 % [+139.0, +144.8] (SD 10.0 %) | 0.000 / 0.497 | 1.07 |
| co-located, ideal | 1558 | 8907 / 6494 | 0.0 / 0.0 | -1.1 % [-4.9, +2.8] (SD 8.4 %) | -1.1 % [-4.9, +2.8] (SD 8.4 %) | +17.3 % | — | — / 0.799 | 0.42 |
| co-located, 0.05 bare | 1558 | 8912 / 6497 | 5.0 / 2.9 | -0.9 % [-5.1, +3.0] (SD 8.4 %) | -1.0 % [-5.2, +2.8] (SD 8.4 %) | +17.3 % | — | — / 0.798 | 0.43 |
| co-located, 0.1 bare | 1558 | 8917 / 6499 | 10.0 / 5.7 | -1.1 % [-5.0, +3.1] (SD 8.4 %) | -1.3 % [-5.2, +2.9] (SD 8.4 %) | +17.4 % | — | — / 0.799 | 0.43 |
| co-located, 0.2 bare | 1558 | 8927 / 6505 | 19.9 / 11.4 | -0.7 % [-4.5, +3.1] (SD 8.4 %) | -1.1 % [-4.9, +2.6] (SD 8.4 %) | +17.6 % | — | — / 0.798 | 0.43 |
| co-located, 0.05 front | 1558 | 8907 / 6494 | 0.1 / 0.1 | -1.4 % [-4.5, +2.8] (SD 8.5 %) | -1.4 % [-4.5, +2.8] (SD 8.5 %) | +17.3 % | — | — / 0.799 | 0.43 |
| co-located, 0.1 front | 1558 | 8908 / 6494 | 0.3 / 0.3 | -1.0 % [-5.0, +2.8] (SD 8.4 %) | -1.0 % [-5.0, +2.8] (SD 8.4 %) | +16.9 % | — | — / 0.805 | 0.43 |
| co-located, 0.2 front | 1558 | 8908 / 6494 | 0.5 / 0.5 | -1.1 % [-5.1, +2.7] (SD 8.4 %) | -1.1 % [-5.1, +2.7] (SD 8.4 %) | +17.1 % | — | — / 0.801 | 0.42 |

**(b) Cs 1 MBq + Co 2 MBq** — N = 128, R = 1.1338 ± 0.0092

| Scene, condition | S (Cs in cs662) | window counts cs662 / co1332 | B cs662 / co1332 | stripping, unfloored | stripping, background-subtracted | legacy floored | spatial (at matched Cs peak) | Cs within 1 mm: raw two-peak / stripped recon | Co median error (mm) |
|---|---:|---|---|---|---|---|---|---|---:|
| separated, ideal | 1550 | 3374 / 1625 | 0.0 / 0.0 | -1.2 % [-2.4, -0.2] (SD 4.8 %) | -1.2 % [-2.4, -0.2] (SD 4.8 %) | +2.4 % | +20.7 % [+19.7, +21.4] (SD 6.8 %) | 0.997 / 0.999 | 1.07 |
| separated, 0.05 bare | 1550 | 3379 / 1628 | 5.0 / 2.9 | -0.9 % [-2.1, -0.1] (SD 4.8 %) | -1.0 % [-2.2, -0.2] (SD 4.8 %) | +2.5 % | +20.5 % [+19.8, +21.4] (SD 6.8 %) | 0.997 / 0.999 | 1.07 |
| separated, 0.1 bare | 1550 | 3384 / 1631 | 10.0 / 5.7 | -1.0 % [-2.0, -0.1] (SD 4.8 %) | -1.2 % [-2.2, -0.3] (SD 4.8 %) | +2.5 % | +20.4 % [+19.9, +21.2] (SD 6.9 %) | 0.997 / 0.999 | 1.07 |
| separated, 0.2 bare | 1550 | 3394 / 1637 | 19.9 / 11.4 | -0.7 % [-1.7, +0.2] (SD 4.8 %) | -1.1 % [-2.1, -0.3] (SD 4.8 %) | +2.8 % | +20.6 % [+19.8, +21.6] (SD 6.9 %) | 0.997 / 0.999 | 1.07 |
| separated, 0.05 front | 1550 | 3375 / 1625 | 0.1 / 0.1 | -1.3 % [-2.3, -0.2] (SD 4.7 %) | -1.3 % [-2.3, -0.2] (SD 4.7 %) | +2.4 % | +20.5 % [+19.6, +21.4] (SD 6.8 %) | 0.997 / 0.999 | 1.07 |
| separated, 0.1 front | 1550 | 3375 / 1626 | 0.3 / 0.3 | -1.3 % [-2.2, -0.1] (SD 4.8 %) | -1.3 % [-2.2, -0.1] (SD 4.8 %) | +2.5 % | +20.6 % [+19.8, +21.2] (SD 6.8 %) | 0.997 / 0.999 | 1.07 |
| separated, 0.2 front | 1550 | 3375 / 1626 | 0.5 / 0.5 | -1.3 % [-2.3, -0.4] (SD 4.7 %) | -1.3 % [-2.3, -0.4] (SD 4.7 %) | +2.3 % | +20.4 % [+19.5, +21.4] (SD 6.8 %) | 0.997 / 0.999 | 1.06 |
| co-located, ideal | 1558 | 3395 / 1623 | 0.0 / 0.0 | -0.3 % [-1.2, +0.9] (SD 4.8 %) | -0.3 % [-1.2, +0.9] (SD 4.8 %) | +1.9 % | — | — / 1.000 | 0.43 |
| co-located, 0.05 bare | 1558 | 3400 / 1626 | 5.0 / 2.9 | -0.2 % [-1.2, +0.8] (SD 4.7 %) | -0.3 % [-1.3, +0.7] (SD 4.7 %) | +2.1 % | — | — / 1.000 | 0.43 |
| co-located, 0.1 bare | 1558 | 3405 / 1629 | 10.0 / 5.7 | -0.1 % [-1.2, +1.0] (SD 4.8 %) | -0.3 % [-1.4, +0.8] (SD 4.8 %) | +2.1 % | — | — / 1.000 | 0.43 |
| co-located, 0.2 bare | 1558 | 3415 / 1635 | 19.9 / 11.4 | +0.1 % [-0.8, +1.2] (SD 4.8 %) | -0.4 % [-1.3, +0.7] (SD 4.8 %) | +2.5 % | — | — / 1.000 | 0.43 |
| co-located, 0.05 front | 1558 | 3396 / 1624 | 0.1 / 0.1 | -0.3 % [-1.3, +0.7] (SD 4.7 %) | -0.3 % [-1.3, +0.7] (SD 4.7 %) | +1.9 % | — | — / 1.000 | 0.42 |
| co-located, 0.1 front | 1558 | 3396 / 1624 | 0.3 / 0.3 | -0.3 % [-1.2, +0.6] (SD 4.7 %) | -0.3 % [-1.2, +0.6] (SD 4.7 %) | +1.9 % | — | — / 1.000 | 0.43 |
| co-located, 0.2 front | 1558 | 3396 / 1624 | 0.5 / 0.5 | -0.1 % [-1.3, +0.8] (SD 4.7 %) | -0.1 % [-1.3, +0.8] (SD 4.7 %) | +2.0 % | — | — / 1.000 | 0.43 |

**Findings.**
1. **The field changes nothing measurable in EV-15 at these activities.** Lab, 60 s, cs662: the background is ≤ 20
   counts against 3 374–8 927 window counts (bare 0.20 µSv/h), ≤ 0.5 front-only; every estimate and location moves by
   less than its seed quartile range from ideal (e.g. (a) separated, unfloored −4.5 % ideal vs −4.0 … −4.6 %; the
   background-subtracted estimate is 0.1–0.5 points more negative at bare 0.20 µSv/h — the model map's own MC error).
2. **(a) 8 : 1.** Spatial lever fails as in the legacy: the raw two-peak match puts Cs within 1 mm in ≤ 0.5 % of any seed's acquisitions (mean ≤ 0.02 %; median error 8.75 mm, the legacy's "lost, 8.7 mm"), and the value at the matched "Cs" peak reads
   +141 % [+139, +144] of S — it is not the Cs peak. Per-pixel stripping recovers the count: −4.5 % [−8.7, −1.0]
   separated and −1.1 % [−4.9, +2.8] co-located, per-acquisition SD 8.4 %; the legacy floor adds +17 %. The stripped
   reconstruction finds Cs within 1 mm in 0.49–0.50 of acquisitions separated (median error 1.0 mm) and 0.80 co-located
   (0.36 mm).
3. **(b) 2 : 1.** Spatial lever holds: Cs within 1 mm in 0.997 (median 0.47 mm — the legacy 0.47 mm, 126 / 128 seeds at
   Co × 2), but the count read off the Cs peak is +20.6 % [+19.7, +21.4] (Co downscatter / sidelobes at the Cs peak).
   Stripping: −1.2 % [−2.4, −0.2] separated, −0.3 % [−1.2, +0.9] co-located, SD 4.8 %; floored +1.9–2.8 %; stripped
   reconstruction locates Cs within 1 mm in 0.999 / 1.000.
4. **Co location.** Co-located: median 0.42–0.43 mm, within 1 mm in 100 %. Separated (Co at (−5, 3) mm): median
   1.07 mm in every condition, ideal included, so within 1 mm in only 0.3 % (a) / 11 % (b) — a field-independent offset of
   this decode at that position, not a background effect; reported, not investigated.
5. The separated-scene stripping bias (−4.5 % at 8 : 1, −1.2 % at 2 : 1, vs −1.1 % / −0.3 % co-located) is consistent with
   R calibrated on axis while Co sits at (−5, 3) mm; not investigated further.

## 2. EV-02 in the cs662 window (family `ev02_fov_cs662`, F128[0:64], N = 64)

Same request as turn 8's open-window run except the window (`ev02-fov-request-v2-cs662.json`), same seeds; per seed the
source and ambient response maps come from the same transport histories (a window only filters the tallied deposits) and
the Poisson draws use the same keyed streams, so the two windows compare row for row. N0 is counted in the window, so a
cs662 source of N0 = 500 is ~2.5× stronger in activity than an open-window one. Background per acquisition, bare bound:
2.1 / 4.3 / 8.6 (10 s) and 12.9 / 25.7 / 51.4 (60 s) counts, against 168 … 4022 in the open window; front-only 0.1–1.3.
Values: the most frequent over 64 seeds (frequency when not 64 / 64).

| S, dir, N0 | metric | window | ideal | bare 10 s: 0.05 / 0.1 / 0.2 | bare 60 s: 0.05 / 0.1 / 0.2 | B per acquisition, bare 10 s / 60 s at 0.2 |
|---|---|---|---|---|---|---|
| 1 m, x, 500 | non-cyclic usable (°) | cs662 | 5 (51/64) | 5 (53/64) / 5 (51/64) / 5 (45/64) | 5 (49/64) / 5 (42/64) / 4.5 (41/64) | 9 / 51 |
| 1 m, x, 500 | non-cyclic usable (°) | open | 5.5 (57/64) | 4.5 (34/64) / 4.5 (55/64) / 3.5 (60/64) | 0 (41/64) / 0 (63/64) / 0 | 670 / 4022 |
| 1 m, x, 500 | cyclic usable (°) | cs662 | 2.5 (63/64) | 2.5 (62/64) / 2.5 (62/64) / 2.5 (62/64) | 2.5 (63/64) / 2.5 (62/64) / 2.5 (63/64) | 9 / 51 |
| 1 m, x, 500 | cyclic usable (°) | open | 2.5 (52/64) | 2.5 (58/64) / 2.5 (63/64) / 2.5 | 2.5 / 0 (58/64) / 0 | 670 / 4022 |
| 1 m, x, 5000 | non-cyclic usable (°) | cs662 | 6.5 | 6.5 / 6.5 / 6.5 | 6.5 / 6.5 / 6.5 | 9 / 51 |
| 1 m, x, 5000 | non-cyclic usable (°) | open | 6.5 | 6.5 / 6.5 / 6.5 | 6.5 (51/64) / 5.5 / 5 (63/64) | 670 / 4022 |
| 1 m, x, 5000 | cyclic usable (°) | cs662 | 3 | 3 / 3 / 3 | 3 / 3 / 3 | 9 / 51 |
| 1 m, x, 5000 | cyclic usable (°) | open | 3 | 3 / 3 / 3 | 3 / 3 / 3 | 670 / 4022 |
| 1 m, diag, 500 | non-cyclic usable (°) | cs662 | 2.5 (58/64) | 2.5 (56/64) / 2.5 (54/64) / 2.5 (51/64) | 2.5 (46/64) / 1.5 (35/64) / 1.5 (56/64) | 9 / 51 |
| 1 m, diag, 500 | non-cyclic usable (°) | open | 2.5 | 1.5 (61/64) / 1.5 (56/64) / 0 | 0 / 0 / 0 | 670 / 4022 |
| 1 m, diag, 500 | cyclic usable (°) | cs662 | 2.5 (34/64) | 2.5 (36/64) / 2.5 (42/64) / 2.5 (35/64) | 2.5 (48/64) / 2.5 (55/64) / 2.5 (62/64) | 9 / 51 |
| 1 m, diag, 500 | cyclic usable (°) | open | 3.5 (45/64) | 2 (53/64) / 1.5 (63/64) / 1.5 (61/64) | 1.5 (40/64) / 0 / 0 | 670 / 4022 |
| 1 m, diag, 5000 | non-cyclic usable (°) | cs662 | 5.5 (42/64) | 5.5 (50/64) / 5.5 (46/64) / 5.5 (43/64) | 5.5 (41/64) / 5.5 (36/64) / 5.5 (35/64) | 9 / 51 |
| 1 m, diag, 5000 | non-cyclic usable (°) | open | 5.5 | 5.5 / 5.5 / 5.5 (62/64) | 2.5 (53/64) / 2.5 / 2 (63/64) | 670 / 4022 |
| 1 m, diag, 5000 | cyclic usable (°) | cs662 | 4 | 4 / 4 / 4 | 4 / 4 / 4 | 9 / 51 |
| 1 m, diag, 5000 | cyclic usable (°) | open | 4 | 4 / 4 / 4 | 4 (59/64) / 2.5 / 2 | 670 / 4022 |
| 5 m, x, 500 | non-cyclic usable (°) | cs662 | 5.5 (59/64) | 5.5 (59/64) / 5.5 (56/64) / 5.5 (56/64) | 5.5 (61/64) / 5.5 (60/64) / 5.5 (63/64) | 9 / 51 |
| 5 m, x, 500 | non-cyclic usable (°) | open | 5.5 (39/64) | 5.5 (62/64) / 5 (53/64) / 4.5 (61/64) | 4 (56/64) / 0 (62/64) / 0 | 670 / 4022 |
| 5 m, x, 500 | cyclic usable (°) | cs662 | 3 | 3 / 3 / 3 | 3 / 3 / 3 | 9 / 51 |
| 5 m, x, 500 | cyclic usable (°) | open | 3 | 3 / 3 / 0 | 0 / 0 / 0 | 670 / 4022 |
| 5 m, x, 5000 | non-cyclic usable (°) | cs662 | 6.5 | 6.5 / 6.5 / 6.5 | 6.5 / 6.5 / 6.5 | 9 / 51 |
| 5 m, x, 5000 | non-cyclic usable (°) | open | 6.5 | 6.5 / 6.5 / 6.5 | 6.5 (53/64) / 6 / 6 (44/64) | 670 / 4022 |
| 5 m, x, 5000 | cyclic usable (°) | cs662 | 3 | 3 / 3 / 3 | 3 / 3 / 3 | 9 / 51 |
| 5 m, x, 5000 | cyclic usable (°) | open | 3 | 3 / 3 / 3 | 3 / 3 / 3 | 670 / 4022 |
| 5 m, diag, 500 | non-cyclic usable (°) | cs662 | 1.5 | 1.5 / 1.5 / 1.5 | 1.5 / 1.5 / 1.5 | 9 / 51 |
| 5 m, diag, 500 | non-cyclic usable (°) | open | 1.5 | 1.5 / 1.5 (54/64) / 0.5 | 0.5 / 0 (57/64) / 0 | 670 / 4022 |
| 5 m, diag, 500 | cyclic usable (°) | cs662 | 1.5 | 1.5 / 1.5 / 1.5 | 1.5 / 1.5 / 1.5 | 9 / 51 |
| 5 m, diag, 500 | cyclic usable (°) | open | 1.5 (59/64) | 1.5 (47/64) / 1.5 (52/64) / 0 | 0 / 0 / 0 | 670 / 4022 |
| 5 m, diag, 5000 | non-cyclic usable (°) | cs662 | 4 (53/64) | 4 (51/64) / 4 (51/64) / 4 (54/64) | 4 (54/64) / 4 (53/64) / 4 (48/64) | 9 / 51 |
| 5 m, diag, 5000 | non-cyclic usable (°) | open | 4 (63/64) | 4 / 4 / 4 | 4 (62/64) / 1.5 (57/64) / 1.5 | 670 / 4022 |
| 5 m, diag, 5000 | cyclic usable (°) | cs662 | 1.5 (62/64) | 1.5 / 1.5 / 1.5 | 1.5 (63/64) / 1.5 / 1.5 | 9 / 51 |
| 5 m, diag, 5000 | cyclic usable (°) | open | 4 (49/64) | 4 (57/64) / 4 (59/64) / 4 (63/64) | 4 / 4 (36/64) / 2.5 | 670 / 4022 |

Out-of-field cue along x (flag ≥ 90 %, first contiguous range past the fully coded field; unflagged wrong spot = largest
fraction over 7.5–12°, median over seeds):

| S, N0 (x) | quantity | window | ideal | bare 10 s: 0.05 / 0.1 / 0.2 | bare 60 s: 0.05 / 0.1 / 0.2 |
|---|---|---|---|---|---|
| 1 m, 500 | outside flag (°) | cs662 | 5 (31/64)–11.5 (51/64) | 5 (33/64)–11.5 (57/64) / 5 (35/64)–11.5 (49/64) / 5.5 (34/64)–11.5 (40/64) | 5.5 (33/64)–11.5 (35/64) / 5.5 (52/64)–11 (52/64) / 5.5 (39/64)–11 (33/64) |
| 1 m, 500 | outside flag (°) | open | 5 (38/64)–11.5 | 7.5 (42/64)–9.5 (38/64) / never (56/64) / never (64/64) | never (64/64) / never (64/64) / never (64/64) |
| 1 m, 500 | unflagged wrong spot, max 7.5–12° | cs662 | 0.13 | 0.13 / 0.13 / 0.15 | 0.16 / 0.20 / 0.28 |
| 1 m, 500 | unflagged wrong spot, max 7.5–12° | open | 0.09 | 0.37 / 0.46 / 0.51 | 0.58 / 0.66 / 0.74 |
| 1 m, 5000 | outside flag (°) | cs662 | 4 (59/64)–12 (38/64) | 4 (60/64)–12 (37/64) / 4 (60/64)–12 (40/64) / 4 (63/64)–12 (39/64) | 4 (61/64)–12 (41/64) / 4 (60/64)–12 (44/64) / 4 (58/64)–12 (48/64) |
| 1 m, 5000 | outside flag (°) | open | 4 (62/64)–12.5 (61/64) | 4 (60/64)–12 / 4 (59/64)–12 / 4 (48/64)–11.5 (51/64) | 4 (34/64)–11.5 / 4.5 (59/64)–11 / 4.5 (35/64)–10 (40/64) |
| 1 m, 5000 | unflagged wrong spot, max 7.5–12° | cs662 | 0.00 | 0.00 / 0.00 / 0.00 | 0.00 / 0.00 / 0.00 |
| 1 m, 5000 | unflagged wrong spot, max 7.5–12° | open | 0.00 | 0.00 / 0.01 / 0.05 | 0.13 / 0.22 / 0.36 |
| 5 m, 500 | outside flag (°) | cs662 | 5.5 (45/64)–11.5 (58/64) | 5.5 (49/64)–11.5 (53/64) / 5.5 (45/64)–11.5 (54/64) / 5.5 (42/64)–11.5 (47/64) | 5.5 (52/64)–11.5 (40/64) / 5.5 (45/64)–11 (39/64) / 6 (34/64)–11 (30/64) |
| 5 m, 500 | outside flag (°) | open | 5.5 (51/64)–11.5 (51/64) | 8 (29/64)–9 (24/64) / never (63/64) / never (64/64) | never (64/64) / never (64/64) / never (64/64) |
| 5 m, 500 | unflagged wrong spot, max 7.5–12° | cs662 | 0.07 | 0.07 / 0.08 / 0.09 | 0.10 / 0.13 / 0.18 |
| 5 m, 500 | unflagged wrong spot, max 7.5–12° | open | 0.06 | 0.39 / 0.57 / 0.74 | 0.83 / 0.91 / 0.96 |
| 5 m, 5000 | outside flag (°) | cs662 | 4 (59/64)–12.5 (63/64) | 4 (61/64)–12.5 (63/64) / 4 (59/64)–12.5 (59/64) / 4 (59/64)–12.5 (61/64) | 4 (63/64)–12.5 (61/64) / 4 (58/64)–12.5 (55/64) / 4 (62/64)–12.5 (52/64) |
| 5 m, 5000 | outside flag (°) | open | 4 (62/64)–12.5 | 4 (60/64)–12.5 (44/64) / 4 (52/64)–12 (63/64) / 4 (40/64)–12 (55/64) | 4.5 (45/64)–11.5 / 4.5 (62/64)–11 / 5 (47/64)–10 (55/64) |
| 5 m, 5000 | unflagged wrong spot, max 7.5–12° | cs662 | 0.00 | 0.00 / 0.00 / 0.00 | 0.00 / 0.00 / 0.00 |
| 5 m, 5000 | unflagged wrong spot, max 7.5–12° | open | 0.00 | 0.00 / 0.00 / 0.03 | 0.14 / 0.58 / 0.86 |


**Findings.** (1) In the cs662 window the field leaves EV-02 at its ideal: front-only matches the cs662 ideal in every
series (0 differing modes); bare bound, the usable non-cyclic half-field moves at most one grid step (1 m, x, N0 500:
5° → 4.5° at 60 s 0.20 µSv/h; 1 m, diagonal, N0 500: 2.5° → 1.5° at 60 s from 0.10), the flag keeps working at every level
(open window: never from B/N0 ≈ 0.7), and the unflagged wrong spot rises at most from 0.13 to 0.28 (1 m, N0 500, 60 s,
0.20) and stays 0.00 at N0 5000 (open: up to 0.86). (2) The ideals of the two windows differ slightly in their own right
(same seeds): 1 m, x, N0 500 non-cyclic 5° (cs662) vs 5.5° (open); 1 m diagonal N0 500 cyclic 2.5° vs 3.5°; 5 m diagonal
N0 5000 cyclic 1.5° vs 4° — the windowed image keeps the photopeak events only, a different shadow at oblique incidence;
reported, not investigated. (3) So the turn-8 open-window degradation is a background-count effect that the energy
window removes at these field levels.

## 3. Budget

| Step | Wall clock |
|---|---|
| EV-02 cs662, 64 seeds, 10 jobs | ~86 min (20:44–22:10; mean 752 s per seed) |
| EV-15 (a) + (b), 2 × 128 seeds, 3 jobs, alongside EV-02 | ~64 min (20:51–21:55; mean 45 s per seed) |
| pilot EV-15 (a), seed 777 (not evidence) | 40 s |

≈ 1.5 h of wall clock for the runs, ~2 h in all. N was not reduced.

## 4. Deviations and limits

1. **EV-02 cs662 ran on a copy of the turn-8 build** (`%TEMP%` copy of `src/Gcam.Cli/bin/Release/net9.0`, taken from the
   clean committed tree before this turn's edits) so the engine could be rebuilt for EV-15 while it ran; the field-of-view
   study code is unchanged since turn 8. The two drivers shared one output folder, so its `run-info.json` records only the
   second (EV-15) launch.
2. **EV-15 code additions** (`AmbientSeparationStudy`): spatial count, stripped-reconstruction Cs location and Co
   location; no random draw added or reordered, so turn-8 fields reproduce. New `CorrelationSearch.ReconstructExpected`
   (real-valued maps), bit-identical to the decoder (tested).
3. Separated-scene Co offset (1.07 mm) and separated stripping bias are reported, not investigated.
4. Same model limits as turns 7–8 (homogeneous crystal for source and field; AB-3 high-energy limits).

## 5. Tests and build

`dotnet build Gcam.sln -c Release`: 0 errors, 2 warnings (pre-existing xUnit2012 / xUnit2000). `dotnet test Gcam.sln -c
Release --no-build` (process-local GCAM_UI_TESTS=0, GCAM_RENDER_SNAPSHOTS=0, GCAM_EVIDENCE_TESTS=0): engine 391 → **393**,
Studio.Core 184 → 184, services 87 + 7 skipped → same, UI 13 + 14 skipped → same; all pass (실패 0). New:
`CorrelationSearchTests.ExpectedMapDecode_ReproducesTheDecoderOnARealImage` (cyclic and non-cyclic, exact equality);
`AmbientEvidenceTests.Separation_…` extended (ideal spatial = background-subtracted spatial exactly, spatial undefined
co-located, location rates in [0, 1]). No golden expectation changed.

## 6. Reproduce

```powershell
dotnet build Gcam.sln -c Release
$m = 'samples/evidence/manifest-ambient-v3.json'
python samples/evidence/run_seeds.py --manifest $m --out <dir> --family ev15_abs_a ev15_abs_b ev02_fov_cs662 --jobs 10
python samples/evidence/ambient/aggregate_ev.py --runs <dir> --manifest $m --family ev15_abs_a --out <ev15a.json>   # likewise ev15_abs_b, ev02_fov_cs662
```

APPROVAL REQUESTS: none.
