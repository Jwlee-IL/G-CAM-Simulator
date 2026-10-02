# PLAN.Physics.EvidenceRefresh — published evidence as value ± spread over seeds (TODO-27)

Scope: the evidence register quotes Monte Carlo results that are single realisations at seed 12345. The TODO-26 survey
(43 reproduce commands × 5 seeds, both generators) found several quotes that differ from today's seed-12345 output
independently of the generator, and others sitting at the edge of their own spread. Re-measure them over seeds and
publish **value ± spread (N seeds)** instead of one realisation. Procedure: [AGENTS.Planning](AGENTS.Planning.md);
survey: [PLAN.Physics.RngBias.Review](PLAN.Physics.RngBias.Review.md).

Status: **done** 2026-10-02 — implemented by a Claude subagent standing in for Codex (out of credits), from the review
file; planner audit and verification passed (build 0 errors / 2 warnings, tests 298 / 179 / 83 + 7 / 13 + 14 unchanged;
no deletions; no machine paths in `samples/evidence/`). Results: Findings 63.

## Known stale or fragile quotes (from the TODO-26 survey; *verify* each)

| Where | Quote | Today's seed-12345 output / note |
|---|---|---|
| EV-01 / README / Findings | "reference lab geometry **282 vs 148**" of 625 (non-cyclic vs cyclic) | 273 / 144 |
| EV-01 | ghost position "12 mm → −6.6 mm" | differs (survey) |
| EV-25 / `samples/plot_shield.py` | "~8 mm W", "~20 mm", "Co-60 ~30 mm (~11 kg)"; plot annotations "~6 mm W", "floor 0.34 mm" | 8 / 20 / 25 mm at seed 12345; picks spread over seeds (8 mm 16/20, 20 mm 15/20, 30 mm 6/20); `shield.csv` predated theme 52 |
| EV-32 | floor "0.60 → 0.68 mm" | differs (survey) |
| Findings 44 | cascade slope 2.16 | 2.05 ± 0.18 over 24 seeds (TODO-14) |
| EV-02 | 4.0° | at the edge of its spread |
| EV-14 | per-pixel RMS 5.65 mm | 5.42 at seed 12345 |
| mixed-field R | 3.97 | 4.08 |

## Proposal (reference — verify / improve)

| # | Proposal |
|---|---|
| R-1 | A reproducible multi-seed run for each EV entry with an MC number (the reproduce command with a seed option, or a driver script under `samples/`), N chosen per quantity so the quoted spread is itself stable; record N and the seed list |
| R-2 | Quote format: median or mean ± sample SD (or the spread of a discrete pick, e.g. "8 mm in 16 / 20 seeds"); a single-seed value only where the quantity is deterministic |
| R-3 | Update every place that quotes the number (Evidence, README headline, Findings, PAPER.ko, plots' hard-coded annotations); Evidence model-history row; old → new kept in the history row |
| R-4 | Where a re-measured value changes a **conclusion** (e.g. the Co-60 shield thickness being "not carriable"), say so and report it to the planner before rewriting the conclusion |

## Decisions after review (2026-10-02)

Review: [PLAN.Physics.EvidenceRefresh.Review](PLAN.Physics.EvidenceRefresh.Review.md) (Codex) — 32 quotation families
re-measured over 32–128 seeds; eleven conclusion / interpretation flags (R-4 1–11) plus the P3 provenance question. The
planner put each flag to the author with the measured numbers and a recommendation; the author accepted all of them
(E12, E04 and E30 individually, the other nine together after reading them: "나머지 9건도 읽어보니 합리적이야. 수용."), and
asked that **the basis of every decision stays on record** — so each row below carries its numbers and its reason, and
the same basis goes into the Evidence history row of the entry it changes.

| # | Flag | Decision | Basis (measured; seeds) |
|---|---|---|---|
| ER-1 | E12 mask / antimask | Withdraw "calibrated subtraction matches antimask within 0.07 mm". Quote: at equal total time, antimask has **~20–30 % lower RMS** at every background level; both stay sub-mm with no failures through BSR 4. **D-19 (no mask-rotation hardware) stands**, re-based as a trade, not an equivalence | RMS calibrated / antimask 0.38 / 0.31 (BSR 0), 0.46 / 0.38 (2), 0.55 / 0.44 (4), 0.91 / 0.63 mm (8); 128 seeds; comparison is time-matched (`MaskAntimaskStudy` splits the budget). Author (2026-10-02): "어차피 더 좋았어도 기구 제작 측면에서 비용이나 안정성 측면에서 디메리트가 커서 마스크 회전은 기각하려고 했어" — the rotation mechanism's cost and mechanical stability outweigh the gain |
| ER-2 | E04 tungsten thickness | The default lab recipe cannot pick a thickness (every 6–32 mm row ties at the maximum radius); state that. The wide-field D20 recipe becomes the **defined** thickness experiment, and the quote becomes "**~10 mm** (10 mm in 21 / 32 seeds, 14 mm in 10 / 32, 8 mm in 1 / 32)", replacing "8–10 mm". Fix the analytic leak line: 7 mm leaks 28.8 % at μ = 0.178 / mm; 35 % is reached at 5.9 mm | `thickness B`: usable radius 8.87 mm for every 6–32 mm row, 64 / 64; `thickness_wide`: picks above, 32 seeds. Author: "수치는 분명히 하는 게 좋겠지" |
| ER-3 | E30 mask fabrication | PR-MFG-01's 40 µm gets a **defined gate**: seed-mean RMS ≤ 1.25 × the seed-mean ideal RMS. 40 µm passes (1.13×), 80 µm fails (1.43×). The old "within ~2× the ideal floor" criterion and the "floor ~0.95 mm" quote are withdrawn (the per-seed 2× test does not separate 40 from 80 µm). Conditional on the six fixed manufactured patterns; no arbitrary-device tolerance claimed | mean RMS 1.35 (0 µm), 1.52 (40), 1.93 (80), 3.65 mm (160); PSR 4.38 / 4.31 / 4.18 / 3.77; per-seed within-2× 108 / 128 (40 µm) vs 100 / 128 (80 µm); 128 seeds |
| ER-4 | E07 count threshold | Separate the two gates: below ~25 counts localisation collapses (5-mm success gate); **sub-mm needs ~250 counts** (centred RMS 0.44 mm, 128 / 128). "From ~50 counts sub-mm" is withdrawn. The biasing "within ~1 %, ~100× fewer photons" claim is not produced by `noise`: measure it with its own recipe (same config, `DirectionalBiasing` off, enlarged history) or label it historical | RMS 2.78 / 0.86 / 0.44 mm at 50 / 100 / 250 counts; sub-mm 0 / 128, 99 / 128, 128 / 128; failure 38 % at 25 counts |
| ER-5 | E03 rank-11 candidate | "≥ 90 % usable" quoted as observed in 31 / 32 seeds; not a re-certified global optimum (the full grid was not repeated). PAPER's "~7×" (thin-mask era) → the finite-slab "~2.5×" | `scan/r11/pass90` 31 / 32 |
| ER-6 | E06 array sampling | "16×16 halves the 12×12 error" → "**reduces it by about a third** (16 / 12 RMS ratio median 0.63 [0.49, 0.82]; 16×16 better in 114 / 128)"; 6×6 failure 20 % (was 21 %). Seed 12345 (ratio 1.20) was an outlier | 128 seeds |
| ER-7 | E25 shield | Quote pick frequencies and masses: Co-60 **25 mm (83 / 128, 6.8 kg) or 30 mm (45 / 128, 9.8 kg)**; Cs-137 20 mm (100 / 128, 4.5 kg) or 25 mm; scattered 8 mm (109 / 128, ~1.0 kg). "Not carriable" is judged against the existing **PR-PHY-01 ≤ 2.5 kg** requirement (no new criterion), so the conclusion stands. The "directional leak moves 6 → 8 mm" increment is removed (no change in 88 / 128). ~11 kg → 9.8 kg; `plot_shield.py` annotations follow | 128 seeds, current knee rule |
| ER-8 | P4 cascade slope | No per-seed slope is quoted (1.71 ± 0.46; ~2.7 of 7 distances have zero sum events and are dropped). Replace with **one pooled Poisson fit over all seeds including the zero rows**; quote that slope with its uncertainty and whether it is consistent with ε² | 128 seeds; pooled-fit data already in the review outputs |
| ER-9 | E33 depth | "Range < 150 mm" (150 mm is a clamp: the first sampled row already fails the gate); the 240-mm widths are censored by the 20–260-mm search window; the power fit is empirical over six distances; the viewer's current service and the historical manual probe are different estimators. Depth stays weak / bias-limited in the far field; no fusion verdict | estimate 140.0 at 150 mm in 64 / 64; width 240 at 150 and 200 mm in 64 / 64; viewer FarCensored at 700 mm in 20–32 / 32 |
| ER-10 | E17 front-end | Total 6.16 ± 0.25 % (was 6.4 %). The ">620 keV deposits" proxy (1.70 %) carries Compton-tail width, so it is not called electronic ENC; the shaper noise FWHM (cusp 1.01, CR-RC⁴ 1.09, trapezoid 1.69 %) and "cusp beats CR-RC in 32 / 32" stay. RTL bit-match and Fmax stay deterministic | 32 seeds |
| ER-11 | E15 spatial separation | Probabilistic quote: at Co:Cs = 2 the Cs source stays sub-mm in **126 / 128** seeds; ×4 / ×8 fail (Cs error 8.7 mm) as before. Weighted effective counts are not observed events | 128 seeds |
| ER-12 | P3 crystal gap | The original 9 / 13 / 20 / 69 / 45 % series stays in the history as **unverified** (its denominator and recipe are lost). Quote the replacement as a **labelled new experiment**: relative to zero gap 1 / 1.59 / 2.52 / 6.60 / 5.33 at 0 / 20 / 40 / 100 / 200 µm; the optimum stays ~100 µm | 32 seeds |

**Correction (2026-10-02, implementation).** Four statements in the rows above did not match the code or the data;
the implementer quoted the measured values instead and the planner checked each: (1) ER-4 — `noise` counts a failure at
error > **3 mm** (`failThresholdMm = 3.0`, `CoreCommands.cs`), not 5 mm; (2) ER-1 — the 0 / 2 / 4 / 8 levels of
`antimask` are background counts **per pixel**, not BSR (the source gives ≈ 2.8 counts / pixel, so BSR ≈ 0 / 0.7 / 1.4 /
2.9), and the calibrated method failed once in 200 repeats in 7 / 128 seeds, so "no failures through 4" became "both
sub-mm through 4 counts / pixel"; (3) ER-8 — the review's `cascade-pooled.json` was a log-log fit of pooled means, so a
real Poisson fit was made (slope **2.001 ± 0.027** over 896 rows including 344 zero rows; ε² implies 1.99–2.00 for this
geometry); (4) EV-25's "12–15 mm calibrated" was never a knee pick — quoted as measured RMS. ER-4's biasing claim was
measured with its own recipe: biased ÷ 4π = 0.999 ± 0.006 (32 seeds). Open question for the author: PR-SENS-02
("fewer than ~50 counts is unreliable") was left as written, although RMS is 2.8 mm at 50 counts.

Applies to all rows: quote format per R-2 (mean ± SD or median [IQR] with N, discrete picks as frequencies); every place
that repeats a number is synchronised (R-3: Evidence, README, Findings, PAPER, plot annotations, PRS / Limitations /
AGENTS headlines); the old value and the reason it moved go in the Evidence model-history row; the seed lists, config
hashes and aggregates are committed (TEMP outputs are not evidence). Unflagged families get their seed-qualified quotes
as proposed in the review.

## Steps
1. Review + measurement: verify the table, extend it to every MC-quoted EV entry (and the README headline), run the
   multi-seed measurements, propose the quote changes; write `docs/PLAN.Physics.EvidenceRefresh.Review.md`.
2. Decisions after review (author for any changed conclusion). 3. Apply the doc changes and the seed-capable runs.
