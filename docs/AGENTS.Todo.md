# AGENTS.Todo — open work items handed between sessions

Scope: the short list of concrete tasks that are **ready to start or running**. A task's context, decisions and steps
live in its `PLAN.*` document, linked from the row; deferred or undecided work stays in
[AGENTS.Backlog](AGENTS.Backlog.md) (it gets a row here only when picked up); results go to
[AGENTS.Findings](AGENTS.Findings.md). A finished task is deleted here and added to the closed-task index in
[docs/archive](archive/README.md#closed-tasks) (and Findings, if it produced a result) in the same commit; its plan
moves to the archive, marked done.

| ID | Task | Origin | Owner | State |
|---|---|---|---|---|
| TODO-25 | Research: a joint x / y / z forward-likelihood depth estimator (bearing from the data; pixel-area, slab, crystal and measurement response; intensity and background profiled) — converge templates independently, off-grid truths and held-out seeds, measure bias, spread, wrong-maximum rate and interval coverage before any Studio replacement of the "sharpest plane" | Findings 57, PLAN.Physics.DepthBias.Review | — | reviewed ([review](PLAN.Physics.DepthLikelihood.Review.md), Findings 62): gate **not yet** — 10 s wrong-maximum rate, count-scaled budget, mismatch open; continuation C-1 … C-7 (2026-10-08): turn 1 with Codex |
| TODO-40 | **Physical readout in GCAM Studio** (TODO-19 stage 2): readout selector, Detector SiPM grid + charge division + Anger flood with LUT, Waveform A–D channels, Spectrum / Imaging from the same measured records | TODO-19 RD-4 / RD-14, author 2026-10-08 | [PLAN.Studio.Readout](PLAN.Studio.Readout.md) | review (turn 1, Codex, worktree `C:\gw\w40`); implementation after TODO-19 (done 2026-10-08, branch `todo19-readout`) merges |

Next free ID: TODO-40. The `Gcam.Wpf` → Studio migration (TODO-06 … TODO-24) is complete — `Gcam.Wpf` was deleted in
TODO-12; record: [PLAN.Studio.Migration](archive/PLAN.Studio.Migration.md). Open work is physics (TODO-25, closing) and Studio (TODO-40); TODO-19 stage 1 and test records (28) are done; the ambient-background follow-ups are done (33) or parked in the Backlog (31–32); TODO-37 (evidence-run provenance) and TODO-39 (Studio strip count) are done; TODO-34 / 35 (performance-critical) and TODO-36 / 38 are done.
