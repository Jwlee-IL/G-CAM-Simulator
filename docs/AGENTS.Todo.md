# AGENTS.Todo — open work items handed between sessions

Scope: the short list of concrete tasks that are **ready to start or running**. A task's context, decisions and steps
live in its `PLAN.*` document, linked from the row; deferred or undecided work stays in
[AGENTS.Backlog](AGENTS.Backlog.md) (it gets a row here only when picked up); results go to
[AGENTS.Findings](AGENTS.Findings.md). A finished task is deleted here and added to the closed-task index in
[docs/archive](archive/README.md#closed-tasks) (and Findings, if it produced a result) in the same commit; its plan
moves to the archive, marked done.

| ID | Task | Origin | Owner | State |
|---|---|---|---|---|
| TODO-42 | **[Physical readout under ambient / background](PLAN.Studio.ReadoutField.md):** extend pre-optical interaction recording to the ambient and BSR transport so a physical readout can run with the field on | TODO-40 SD-3, author 2026-10-08 | Codex | stage A (ambient, source scenes) done 2026-10-13; stage B (BSR) next |
| TODO-43 | **[GCAM Studio manual generated from UI-automation runs](PLAN.Docs.StudioManual.md):** operating chapters generated from desktop runs that drove the app (labels, steps, figures from one run's record), static chapters for the rest; the author's document-automation procedure from another project, process only | author 2026-10-12 | — | planned — plan written; review turn when the author starts it |

Next free ID: TODO-46. The `Gcam.Wpf` → Studio migration (TODO-06 … TODO-24) is complete — `Gcam.Wpf` was deleted in
TODO-12; record: [PLAN.Studio.Migration](archive/PLAN.Studio.Migration.md). Open work is the readout follow-up TODO-42 (ambient) and the generated Studio manual (TODO-43, planned); Studio text density and the Options dialog (TODO-44 / 45) are done, their stage-2 desktop run waiting for the author's go; TODO-41 (readout on every preset) and TODO-40 (Studio readout) are done; TODO-25 closed (screening; gate validation in the Backlog); TODO-19 stage 1 and test records (28) are done; the ambient-background follow-ups are done (33) or parked in the Backlog (31–32); TODO-37 (evidence-run provenance) and TODO-39 (Studio strip count) are done; TODO-34 / 35 (performance-critical) and TODO-36 / 38 are done.
