# AGENTS.Todo — open work items handed between sessions

Scope: the short list of concrete tasks that are **ready to start or running**. A task's context, decisions and steps
live in its `PLAN.*` document, linked from the row; deferred or undecided work stays in
[AGENTS.Backlog](AGENTS.Backlog.md) (it gets a row here only when picked up); results go to
[AGENTS.Findings](AGENTS.Findings.md). A finished task is deleted here and added to the closed-task index in
[docs/archive](archive/README.md#closed-tasks) (and Findings, if it produced a result) in the same commit; its plan
moves to the archive, marked done.

| ID | Task | Origin | Owner | State |
|---|---|---|---|---|
| TODO-52 | **[Attributable-change observation (UI automation guide v3.3)](PLAN.Testing.ObserveV33.md)**: a candidate is only a change the run can attribute to Studio (observation scope from the write-surface list, harness markers, owned processes); other changes are counted as `unattributed` and never block. Then the approved pilot, together with TODO-51's desktop cache check | author 2026-10-16 (guide v3.3) | planner → implementer | implemented; desktop session (pilot + cache check) next |
| TODO-51 | **[Source-determined binaries and cached desktop results](PLAN.Testing.ResultCache.md)**: a build of the same source gives the same bytes (no commit in the informational version or the PDBs), so observed runs and manual runs are reused across docs- and test-only commits; then reuse a desktop test's latest pass while its key (deployment fingerprint, its catalog source hashes, isolation fingerprint, environment) is unchanged, reported as reuse | author 2026-10-16 | planner → implementer | steps 1–2 done; desktop check of the cache open |

Next free ID: TODO-53. The `Gcam.Wpf` → Studio migration (TODO-06 … TODO-24) is complete — `Gcam.Wpf` was deleted in
TODO-12; record: [PLAN.Studio.Migration](archive/PLAN.Studio.Migration.md). Open: TODO-51 (desktop cache check), TODO-52 (observation v3.3). The manual's second edition with a video of every desktop test (TODO-48, edition `2026-10-16-1`), the Direct / Anger comparison (TODO-47, the Compare workspace), the report export (TODO-46: HTML, PDF, Word, event CSV) and the generated Studio manual (TODO-43, first edition `2026-10-15-1` with PDF and Word) are done; the physical readout with the ambient field and BSR (TODO-42) is done; Studio text density and the Options dialog (TODO-44 / 45) are done, their stage-2 desktop run waiting for the author's go; TODO-41 (readout on every preset) and TODO-40 (Studio readout) are done; TODO-25 closed (screening; gate validation in the Backlog); TODO-19 stage 1 and test records (28) are done; the ambient-background follow-ups are done (33) or parked in the Backlog (31–32); TODO-37 (evidence-run provenance) and TODO-39 (Studio strip count) are done; TODO-34 / 35 (performance-critical) and TODO-36 / 38 are done.
