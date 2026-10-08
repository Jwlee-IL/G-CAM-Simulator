# AGENTS.Todo — open work items handed between sessions

Scope: the short list of concrete tasks that are **ready to start or running**. A task's context, decisions and steps
live in its `PLAN.*` document, linked from the row; deferred or undecided work stays in
[AGENTS.Backlog](AGENTS.Backlog.md) (it gets a row here only when picked up); results go to
[AGENTS.Findings](AGENTS.Findings.md). A finished task is deleted here and added to the closed-task index in
[docs/archive](archive/README.md#closed-tasks) (and Findings, if it produced a result) in the same commit; its plan
moves to the archive, marked done.

| ID | Task | Origin | Owner | State |
|---|---|---|---|---|
| TODO-40 | **Physical readout in GCAM Studio** (TODO-19 stage 2): readout selector, Detector SiPM grid + charge division + Anger flood with LUT, Waveform A–D channels, Spectrum / Imaging from the same measured records | TODO-19 RD-4 / RD-14, author 2026-10-08 | [PLAN.Studio.Readout](PLAN.Studio.Readout.md) | decided (SD-1 … SD-6: live integration with engine prerequisite, 12 × 12 reference head, ambient off for physical readout); implementation next |
| TODO-41 | **Physical readout on every Studio preset:** the 30 × 30 default geometry builds in ~45 s and its flood-map LUT calibration fails on every fixed-seed repeat (TODO-40 review); make the readout calibrate and run on all presets, with measured cost | TODO-40 SD-2, author 2026-10-08 | — | open (after TODO-40) |
| TODO-42 | **Physical readout under ambient / background:** extend pre-optical interaction recording to the ambient and BSR transport so a physical readout can run with the field on; **first step: an implementation-size estimate** for the author | TODO-40 SD-3, author 2026-10-08 | — | open — size estimate first |

Next free ID: TODO-40. The `Gcam.Wpf` → Studio migration (TODO-06 … TODO-24) is complete — `Gcam.Wpf` was deleted in
TODO-12; record: [PLAN.Studio.Migration](archive/PLAN.Studio.Migration.md). Open work is Studio (TODO-40) and the readout follow-ups (TODO-41, 42); TODO-25 closed (screening; gate validation in the Backlog); TODO-19 stage 1 and test records (28) are done; the ambient-background follow-ups are done (33) or parked in the Backlog (31–32); TODO-37 (evidence-run provenance) and TODO-39 (Studio strip count) are done; TODO-34 / 35 (performance-critical) and TODO-36 / 38 are done.
