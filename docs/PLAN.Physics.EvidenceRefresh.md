# PLAN.Physics.EvidenceRefresh — published evidence as value ± spread over seeds (TODO-27)

Scope: the evidence register quotes Monte Carlo results that are single realisations at seed 12345. The TODO-26 survey
(43 reproduce commands × 5 seeds, both generators) found several quotes that differ from today's seed-12345 output
independently of the generator, and others sitting at the edge of their own spread. Re-measure them over seeds and
publish **value ± spread (N seeds)** instead of one realisation. Procedure: [AGENTS.Planning](AGENTS.Planning.md);
survey: [PLAN.Physics.RngBias.Review](PLAN.Physics.RngBias.Review.md).

Status: **reference plan** 2026-10-02 — implementer: Codex.

## Known stale or fragile quotes (from the TODO-26 survey; *verify* each)

| Where | Quote | Today's seed-12345 output / note |
|---|---|---|
| EV-01 / README / Findings | "lab rig **282 vs 148**" of 625 (non-cyclic vs cyclic) | 273 / 144 |
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

## Steps
1. Review + measurement: verify the table, extend it to every MC-quoted EV entry (and the README headline), run the
   multi-seed measurements, propose the quote changes; write `docs/PLAN.Physics.EvidenceRefresh.Review.md`.
2. Decisions after review (author for any changed conclusion). 3. Apply the doc changes and the seed-capable runs.
