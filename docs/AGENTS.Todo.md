# AGENTS.Todo — open work items handed between sessions

Scope: the short list of concrete tasks that are **ready to start or running**. A task's context, decisions and steps
live in its `PLAN.*` document, linked from the row; deferred or undecided work stays in
[AGENTS.Backlog](AGENTS.Backlog.md) (it gets a row here only when picked up); results go to
[AGENTS.Findings](AGENTS.Findings.md). A finished task is deleted here and added to the closed-task index in
[docs/archive](archive/README.md#closed-tasks) (and Findings, if it produced a result) in the same commit; its plan
moves to the archive, marked done.

| ID | Task | Origin | Owner | State |
|---|---|---|---|---|
| TODO-41 | **[Physical readout on every Studio preset](PLAN.Studio.ReadoutPresets.md):** the 30 × 30 default geometry builds in ~45 s and its flood-map LUT calibration fails on every fixed-seed repeat (TODO-40 review); make the readout calibrate and run on all presets, with measured cost | TODO-40 SD-2, author 2026-10-08 | Codex | evidence complete (v4 confirmation + v5 repair: 662 / 1332 keV offered on every preset, 122 keV refused); next the Studio stage |
| TODO-42 | **Physical readout under ambient / background:** extend pre-optical interaction recording to the ambient and BSR transport so a physical readout can run with the field on; **first step: an implementation-size estimate** for the author | TODO-40 SD-3, author 2026-10-08 | — | open — size estimate first |
| TODO-43 | **[GCAM Studio manual generated from UI-automation runs](PLAN.Docs.StudioManual.md):** operating chapters generated from desktop runs that drove the app (labels, steps, figures from one run's record), static chapters for the rest; the author's document-automation procedure from another project, process only | author 2026-10-12 | — | planned — plan written; review turn when the author starts it |
| TODO-44 | **Studio text density — help marks instead of inline detail:** keep state, verdicts and key numbers on screen; move explanations, assumptions, provenance and long reasons behind a focusable circled "?" help control (tooltip + `AutomationProperties.HelpText`, keyboard and screen-reader reachable); first a standard control and an information hierarchy in `DESIGN.Controls`, then every workspace; SR wording that says "display" is reconciled (a help control counts as displayed; experimental labels and refusals stay inline). TODO-41's readout screens are done inside TODO-41; TODO-43 (manual) follows this task | author 2026-10-12 (desktop review of the TODO-41 screens) | — | open — plan first |

Next free ID: TODO-45. The `Gcam.Wpf` → Studio migration (TODO-06 … TODO-24) is complete — `Gcam.Wpf` was deleted in
TODO-12; record: [PLAN.Studio.Migration](archive/PLAN.Studio.Migration.md). Open work is the readout follow-ups (TODO-41 all presets, TODO-42 ambient) and the generated Studio manual (TODO-43, planned); TODO-40 (Studio readout) is done; TODO-25 closed (screening; gate validation in the Backlog); TODO-19 stage 1 and test records (28) are done; the ambient-background follow-ups are done (33) or parked in the Backlog (31–32); TODO-37 (evidence-run provenance) and TODO-39 (Studio strip count) are done; TODO-34 / 35 (performance-critical) and TODO-36 / 38 are done.
