# AGENTS.Planning — writing a PLAN, handing it to the implementer, and the review loop

Scope: how work is planned and delegated in this repository since 2026-10-01 — who does what, how a `PLAN.*`
document is written, how the implementing agent (Codex) is called and kept in one conversation, how its review
feeds back into the plan, and how the result is verified and committed. Document naming is in
[AGENTS.Conventions.Docs](AGENTS.Conventions.Docs.md); the task list is [AGENTS.Todo](AGENTS.Todo.md).

## Roles

| Role | Who | Does | Does not |
|---|---|---|---|
| Author | the repository owner | sets priorities, takes product decisions, says when to commit, frees the desktop for UI tests | — |
| Planner | Claude (this repository's coding session) | writes and revises `PLAN.*`, checks premises in the code, reviews and re-verifies the implementer's work, runs its own investigations, writes results to the docs, commits on the author's word | implement features the plan hands to the implementer |
| Implementer | Codex (`gpt-6.1-sol`) | reviews a plan with measurements, proposes improvements, implements the agreed plan, reports numbers | edit `PLAN.*` or `AGENTS.Todo`, change git state, run desktop UI tests without the author's go |

**Why the split:** in the first week the planner's plans were wrong six times (below); the implementer, told to stop
when a plan is wrong, caught every one. Each role checks the other.

## Procedure

1. **Check premises before writing.** Every statement about existing behaviour is read in the code (file, member)
   or measured; every expected number is derived from the actual configuration (resolution, absorbers, grid) or
   from a measured distribution. **Never borrow a tolerance** from another scenario.
2. **Write the plan as a reference plan** (`docs/PLAN.<Area>.<Name>.md`): title + scope + `Status:`; "What exists
   (checked in the code)" with file references; a decisions table whose rows are *proposed* or *verify* (anything
   not established); steps; done-when; not-here. Register the task in `AGENTS.Todo` with a link.
3. **Turn 1 — review only.** Start one implementer conversation for the task. It reads the plan as a starting point,
   checks every *verify* row in the code, measures what is quantitative, proposes improvements, and writes
   `docs/PLAN.<Area>.<Name>.Review.md`. No code in this turn.
4. **Adopt or argue.** The planner writes "Decisions after review" into the plan (adopted corrections, planner
   additions with their evidence, rejected proposals with reasons) and re-points `Status:`. Where the planner
   disagrees with the review, the next turn of the **same conversation** is a discussion, not implementation.
5. **Turn 2+ — implement**, in the same conversation: "state material disagreements first and stop; otherwise
   implement". Plan corrections found later go back into the plan as a dated **Correction** paragraph.
6. **Verify independently.** The planner rebuilds, runs `dotnet test`, runs the headless render snapshots, reads the
   diff, re-checks physics numbers and evidence text. The implementer's "it passes" is not evidence until re-run.
7. **Record and commit.** Results to `AGENTS.Findings` / `VV.*`; plan `Status:` updated; commit on the author's word,
   split by concern (implementation, plan docs, findings), never sweeping in files another session is editing.
8. **Close.** Remove the `AGENTS.Todo` row, add a Backlog done entry linking the plan, set the plan's status to done
   with the commit. Desktop UI tests, the desktop survey and README screenshots run last, once the screens are final.

## Calling the implementer

| Item | Rule | Why |
|---|---|---|
| Model | `codex exec -m gpt-6.1-sol` | the author's choice |
| Sandbox | `-s workspace-write` by default; `--dangerously-bypass-approvals-and-sandbox` only for work needing the real desktop, and only after the author says the desktop is free | the sandbox's blocked desktop access is a safety net while another session drives UI automation; an app-level "bypass" setting does not reach `codex exec` — the command-line flag decides (the log's `sandbox:` line shows what applied) |
| Prompt | from a file on stdin (`- < prompt.md`), report with `-o report.md`, run in the background | long tasks; the report is the turn's result |
| One conversation per task | start once, read `session id:` from the log, continue with `codex exec -s workspace-write --skip-git-repo-check -C <repo> -o <report> resume <session-id> - < prompt.md` | the implementer keeps its findings across review, discussion and implementation |
| Check the log head | `model:`, `sandbox:`, `session id:` match what was intended | a silent fallback (new session, other sandbox) is otherwise invisible |

**Prompt checklist** — every implementer prompt states:
- what to read first, and which plan section is the specification;
- hard constraints: no git state changes; no desktop UI tests / app launch unless authorised; paths it must not
  edit (`docs/PLAN.*`, `docs/AGENTS.Todo.md`, other sessions' areas); repository rules that are easy to break;
- **"if the plan is wrong, or a physics test cannot meet its stated tolerance, stop and report — never loosen a
  tolerance or work around it"**;
- the verification commands, and "if the sandbox blocks commands, say which and stop after the edits";
- the final report's contents: files by group, test counts before → after, every physics number with expectation,
  tolerance and how the tolerance was derived, deviations, what could not be run.

## Cautions met so far

| Situation | What to do |
|---|---|
| The implementer stops on a plan premise | treat it as a plan bug: fix the plan with a dated Correction, then resume the same conversation |
| The sandbox refuses process creation (`CreateProcessAsUserW` access denied) mid-task | the planner runs build / tests / measurements and gives the numbers back in the next turn |
| Two agents in one working tree | the planner investigates in a separate `git worktree` (no build collisions); commits only its own files; checks `git status` first; to commit one task while another edits shared files, build the commit from a worktree or stage reconstructed blobs — never commit a half-edited file |
| Implementer-written docs | check for "pending" placeholders, duplicate IDs (e.g. two AN-11 rows), change history inside VV requirement rows, VV documents linking `AGENTS.*` / `PLAN.*` |
| Implementer-written views | check for ancestor lookups (`RelativeSource AncestorType=…`) and other context tricks; the offscreen renders expose them |
| Physics claims | a precision that has no guarantee is **measured and recorded**, not asserted; assert what is guaranteed (association, conservation, a derived k·σ); a bias found is a finding with a TODO, never tuned away |
| Inherited evidence | re-measure before building on it — the implementer could not reproduce EV-03's rank-23 headline (TODO-18) |

## The six plan errors of the first week (examples)

| Plan | Wrong premise | Caught by |
|---|---|---|
| Spectrum | "three bands for Cs + Co" — Cs-137 also lists its Ba K X-rays | implementer stopped |
| Spectrum | "the global maximum is the 662 keV bin"; "6 % resolution" (the chain gives 4.57 %) | implementer stopped |
| Imaging options A | "`EventStreamStudy` adds cascades"; "passing the sensitivity applies gain" | implementer stopped |
| Imaging options A | "the Ba K tallest bin holds only for the bare geometry" (over-correction) | measurement (2.32 with the absorber) |
| Imaging options B | a 1.5 mm localisation tolerance borrowed from another scenario (grid step 2.19 mm) | implementer stopped |
| Optics | the inherited coverage 0.9–1.4 rule | implementer's measured review |
