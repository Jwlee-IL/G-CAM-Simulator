# AGENTS.Todo — open work items handed between sessions

Scope: the short list of concrete tasks that are **ready to start or running**. A task's context, decisions and steps
live in its `PLAN.*` document, linked from the row; deferred or undecided work stays in
[AGENTS.Backlog](AGENTS.Backlog.md) (it gets a row here only when picked up); results go to
[AGENTS.Findings](AGENTS.Findings.md). A finished task is deleted here and added to the closed-task index in
[docs/archive](archive/README.md#closed-tasks) (and Findings, if it produced a result) in the same commit; its plan
moves to the archive, marked done.

| ID | Task | Origin | Owner | State |
|---|---|---|---|---|
| TODO-53 | GCAM Studio software documents as a demonstration set (Word + PDF): SRS, SDS, V&V report, a software test report covering Studio and the simulation engine, the manual — [PLAN.Docs.SoftwareSet](PLAN.Docs.SoftwareSet.md) | author, 2026-10-20 | Claude plans, Codex implements | planned; implementer review next |
| TODO-54 | GCAM Studio UI automation under the author's UI-automation procedure v4.0: charter and gates, four people's acts, crash / hang failures, a machine-checked video per desktop test, test-data list, pre-launch checks — [PLAN.Testing.UiAutomationV4](PLAN.Testing.UiAutomationV4.md) | author, 2026-10-21 | Claude plans, Codex implements | planned; author decision D4, then implementer review |

Next free ID: TODO-55. The `Gcam.Wpf` → Studio migration (TODO-06 … TODO-24) is complete — `Gcam.Wpf` was deleted in
TODO-12; record: [PLAN.Studio.Migration](archive/PLAN.Studio.Migration.md). Source-determined binaries with the opt-in desktop result cache (TODO-51) and attributable-change observation per the UI-automation procedure v3.3 (TODO-52) are done. The manual's second edition with a video of every desktop test (TODO-48, edition `2026-10-16-1`), the Direct / Anger comparison (TODO-47, the Compare workspace), the report export (TODO-46: HTML, PDF, Word, event CSV) and the generated Studio manual (TODO-43, first edition `2026-10-15-1` with PDF and Word) are done; the physical readout with the ambient field and BSR (TODO-42) is done; Studio text density and the Options dialog (TODO-44 / 45) are done, their stage-2 desktop run waiting for the author's go; TODO-41 (readout on every preset) and TODO-40 (Studio readout) are done; TODO-25 closed (screening; gate validation in the Backlog); TODO-19 stage 1 and test records (28) are done; the ambient-background follow-ups are done (33) or parked in the Backlog (31–32); TODO-37 (evidence-run provenance) and TODO-39 (Studio strip count) are done; TODO-34 / 35 (performance-critical) and TODO-36 / 38 are done.
