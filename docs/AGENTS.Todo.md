# AGENTS.Todo — open work items handed between sessions

Scope: the short list of concrete tasks that are **ready to start or running**. A task's context, decisions and steps
live in its `PLAN.*` document, linked from the row; deferred or undecided work stays in
[AGENTS.Backlog](AGENTS.Backlog.md) (it gets a row here only when picked up); results go to
[AGENTS.Findings](AGENTS.Findings.md). A finished task is deleted here and added to the closed-task index in
[docs/archive](archive/README.md#closed-tasks) (and Findings, if it produced a result) in the same commit; its plan
moves to the archive, marked done.

| ID | Task | Origin | Owner | State |
|---|---|---|---|---|
| TODO-19 | Model a conventional four-channel Anger readout: 1:1 pixel-matched SiPM array with dead regions, **four 14-bit ADC channels with Anger-type charge division** (position from relative signals, crystal from the flood map / LUT). Also owns (author, 2026-10-02, from TODO-11): **SiPM pitch / light sharing** (light spread over several SiPMs, per-SiPM photoelectron statistics, crystal identification from the flood map, edge compression) and **optical crosstalk** (on the list-mode path it is a no-op today — Findings 56; note the list-mode sink takes the energy before the light spread but the arg-max position after it, inconsistent once crosstalk is non-zero); Wpf's SiPM block average is not carried over. Brings: centroid positioning of multi-site histories (Studio list-mode uses Argmax today), array-wide pile-up that shifts the position as well as the energy, crystal mis-identification near dead regions, four-channel traces in the Waveform workspace. Defaults from published practice and physics / measurement — no former-instrument specifics (clean-room rule) | author, 2026-10-02 (TODO-10 discussion); [PLAN.Physics.RigReadout](PLAN.Physics.RigReadout.md) | — | reviewed ([review](PLAN.Physics.RigReadout.Review.md)); decisions after review next |
| TODO-25 | Research: a joint x / y / z forward-likelihood depth estimator (bearing from the data; pixel-area, slab, crystal and measurement response; intensity and background profiled) — converge templates independently, off-grid truths and held-out seeds, measure bias, spread, wrong-maximum rate and interval coverage before any Studio replacement of the "sharpest plane" | Findings 57, PLAN.Physics.DepthBias.Review | — | reviewed ([review](PLAN.Physics.DepthLikelihood.Review.md), Findings 62): gate **not yet** — 10 s wrong-maximum rate, count-scaled budget, mismatch open |

Next free ID: TODO-40. The `Gcam.Wpf` → Studio migration (TODO-06 … TODO-24) is complete — `Gcam.Wpf` was deleted in
TODO-12; record: [PLAN.Studio.Migration](archive/PLAN.Studio.Migration.md). Open work is physics (TODO-19, 25); test records (28) are done; the ambient-background follow-ups are done (33) or parked in the Backlog (31–32); TODO-37 (evidence-run provenance) and TODO-39 (Studio strip count) are done; TODO-34 / 35 (performance-critical) and TODO-36 / 38 are done.
