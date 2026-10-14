# AGENTS.Todo — open work items handed between sessions

Scope: the short list of concrete tasks that are **ready to start or running**. A task's context, decisions and steps
live in its `PLAN.*` document, linked from the row; deferred or undecided work stays in
[AGENTS.Backlog](AGENTS.Backlog.md) (it gets a row here only when picked up); results go to
[AGENTS.Findings](AGENTS.Findings.md). A finished task is deleted here and added to the closed-task index in
[docs/archive](archive/README.md#closed-tasks) (and Findings, if it produced a result) in the same commit; its plan
moves to the archive, marked done.

| ID | Task | Origin | Owner | State |
|---|---|---|---|---|
| TODO-43 | **[GCAM Studio manual generated from UI-automation runs](PLAN.Docs.StudioManual.md):** operating chapters generated from desktop runs that drove the app (labels, steps, figures from one run's record), static chapters for the rest; the author's document-automation procedure from another project, process only | author 2026-10-12 | — | planned — plan written; review turn when the author starts it |
| TODO-46 | **Simulation report output from GCAM Studio:** export a report of the current acquisition — the inputs and settings it ran with, the images, spectrum and estimates, the user's measurements — to a file. To decide before a plan: contents, format (e.g. Markdown / HTML with PNG figures, PDF), where it is written. Consequences: Studio's **first write surface** — it reopens the scopes closed as not applicable for TODO-43 (declared and isolated destination, an observed run of the new flow, the action policy); the manual gains a chapter and the N-NOT-SAVED notice changes in the next edition. Start after TODO-43's first edition | author 2026-10-14 | — | idea — scope to decide |

Next free ID: TODO-47. The `Gcam.Wpf` → Studio migration (TODO-06 … TODO-24) is complete — `Gcam.Wpf` was deleted in
TODO-12; record: [PLAN.Studio.Migration](archive/PLAN.Studio.Migration.md). Open work is the generated Studio manual (TODO-43, in progress) and the simulation report output (TODO-46, idea); the physical readout with the ambient field and BSR (TODO-42) is done, its desktop scenarios waiting for the author's go; Studio text density and the Options dialog (TODO-44 / 45) are done, their stage-2 desktop run waiting for the author's go; TODO-41 (readout on every preset) and TODO-40 (Studio readout) are done; TODO-25 closed (screening; gate validation in the Backlog); TODO-19 stage 1 and test records (28) are done; the ambient-background follow-ups are done (33) or parked in the Backlog (31–32); TODO-37 (evidence-run provenance) and TODO-39 (Studio strip count) are done; TODO-34 / 35 (performance-critical) and TODO-36 / 38 are done.
