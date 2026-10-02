# AGENTS.Planning — writing a PLAN, handing it to the implementer, and the review loop

Scope: how work is planned and delegated in this repository since 2026-10-01 — who does what, how a `PLAN.*`
document is written, how the implementing agent (Codex) is called and kept in one conversation, how its review
feeds back into the plan, and how the result is verified and committed. Document naming is in
[AGENTS.Conventions.Docs](AGENTS.Conventions.Docs.md); the task list is [AGENTS.Todo](AGENTS.Todo.md).

## Roles

| Role | Who | Does | Does not |
|---|---|---|---|
| Author | the repository owner | sets priorities, takes product decisions, says when to commit, frees the desktop for UI tests | — |
| Planner | Claude (this repository's coding session) | writes and revises `PLAN.*`, checks premises in the code, reviews and re-verifies the implementer's work, runs its own investigations, writes results to the docs, commits on the author's word; when the implementer stops mid-task (quota, rate limit, crash), **hands the rest to a substitute implementer** (a Claude subagent); **does simple documentation work itself** — wording, quote synchronisation, renames in prose, decision records, no code or measurement (author, 2026-10-02: a subagent costs more than the edit) | implement features the plan hands to an implementer |
| Implementer | Codex (`gpt-6.1-sol`); when Codex stops mid-task, a **substitute implementer** — a Claude subagent the planner starts (author, 2026-10-02) | reviews a plan with measurements, proposes improvements, implements the agreed plan, reports numbers | edit `PLAN.*` or `AGENTS.Todo`, change git state, run desktop UI tests without the author's go |

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
| Sandbox | `-s danger-full-access` **with the guard block below** in every prompt (author, 2026-10-02); `-s workspace-write` while another session drives UI automation; desktop UI tests / app launch only after the author says the desktop is free | the Windows sandbox runs each command through `CreateProcessAsUserW` with a restricted token and has repeatedly lost process creation mid-task (error 5, access denied), ending the conversation's usefulness; one level up removes that, and the guard plus the planner's audit replace the sandbox's limits. The command-line flag decides, not the app setting (the log's `sandbox:` line shows what applied) |
| Prompt | from a file on stdin (`- < prompt.md`), report with `-o report.md`, run in the background | long tasks; the report is the turn's result |
| One conversation per task | start once, read `session id:` from the log, continue with `codex exec -s workspace-write --skip-git-repo-check -C <repo> -o <report> resume <session-id> - < prompt.md` | the implementer keeps its findings across review, discussion and implementation |
| Check the log head | `model:`, `sandbox:`, `session id:` match what was intended | a silent fallback (new session, other sandbox) is otherwise invisible |
| Watch for completion | launch `codex exec` through the shell tool's own background mode (or a monitor), **never a detached `&` / `start`**, so the planner is notified when the turn ends; on notification read the report and audit at once | 2026-10-02: a detached turn finished at 23:15 and the planner did not notice until the author asked |

### Guard block (every `danger-full-access` prompt)

Paste verbatim; the author set these limits on 2026-10-02 when the sandbox was lifted one level.

```text
HARD LIMITS (no sandbox is enforcing them — you are trusted to keep them; the planner audits your command log):
1. Paths. Write only inside the repository working tree (never inside .git) and %TEMP%\gcam-*; `dotnet restore`/`build`
   may fill the NuGet package cache. Read only the
   repository, %TEMP%, the NuGet package cache and the .NET SDK. Do not read or write C:\Windows, C:\Program Files*,
   C:\ProgramData, the registry, other users' folders, ~/.codex, ~/.claude, ~/.ssh, or any credential store.
2. Irreversible commands need the author's approval: deleting (Remove-Item, rm, del, rmdir, rd, git clean),
   moving or renaming over an existing file, `dotnet clean`, any git command that changes state (add, commit,
   checkout, switch, reset, restore, stash, rebase, merge, push, tag, branch -d), installing or updating anything
   (dotnet tool, winget, npm -g, pip), persistent changes to environment variables / PATH / settings (setx,
   user or machine variables, profiles), stopping processes,
   network uploads. Setting a variable for one command's own process (e.g. `$env:GCAM_RENDER_SNAPSHOTS='1'; dotnet
   test …`, `GCAM_EVIDENCE_TESTS=1 dotnet test …`) is allowed: it ends with the process. Editing repository files through your edit tool is allowed (git can restore them); a new file
   that replaces an existing one is not a reason to delete the old one.
3. When you need one: do NOT run it. Finish everything that does not depend on it, then end your report with a section
   "APPROVAL REQUESTS": each exact command, why, what it destroys, how to undo it. Stop there. Approval arrives in the
   next turn, naming the exact command.
4. No desktop: do not launch Studio or any GUI, no UI tests (GCAM_UI_TESTS), no window automation.
```

**Audit after each turn.** The planner scans the turn's log for executed commands matching
`Remove-Item|\brm\b|\bdel\b|rmdir|\brd\b|git (add|commit|checkout|switch|reset|restore|stash|clean|rebase|merge|push|tag)|dotnet clean|Stop-Process|Set-ItemProperty|setx|winget|Program Files|\Windows\` and checks
`git status` for unexpected deletions; any hit is reported to the author before the next turn. Codex's `rules`
(exec-policy prefix rules) cannot enforce this: commands arrive as one `pwsh -Command "<script>"` argument.

**Prompt checklist** — every implementer prompt states:
- what to read first, and which plan section is the specification;
- the guard block (for `danger-full-access`); hard constraints: no git state changes; no desktop UI tests / app launch unless authorised; paths it must not
  edit (`docs/PLAN.*`, `docs/AGENTS.Todo.md`, other sessions' areas); repository rules that are easy to break;
- **"if the plan is wrong, or a physics test cannot meet its stated tolerance, stop and report — never loosen a
  tolerance or work around it"**;
- the verification commands, and "if the sandbox blocks commands, say which and stop after the edits";
- the final report's contents: files by group, test counts before → after, every physics number with expectation,
  tolerance and how the tolerance was derived, deviations, what could not be run.

## Why the sandbox was lifted — `CreateProcessAsUserW failed: 5`

Recorded so the next session does not re-investigate it (author's decision, 2026-10-02).

**What the error is.** On Windows, Codex's `read-only` and `workspace-write` sandboxes do not run a shell command
directly: they build a **restricted token** (write access limited to the workdir and temp) and start the command with
the Win32 call `CreateProcessAsUserW` under that token. `5` is `ERROR_ACCESS_DENIED`: Windows refused to create the
process. Nothing of the command runs, so even `Get-Content` fails; Codex's own edit tool (`apply_patch`, no process)
keeps working. The log line looks like
`ERROR codex_core::tools::router: error=exec_command failed: CreateProcess { … Failed to create unified exec process:
CreateProcessAsUserW failed: 5 (액세스가 거부되었습니다.) | cwd=… | cmd=…\WindowsApps\pwsh.exe -Command … |
si_flags=256 | creation_flags=134743040 }` — `si_flags=256` is `STARTF_USESTDHANDLES`; `134743040 = 0x08080400` is
`CREATE_NO_WINDOW | EXTENDED_STARTUPINFO_PRESENT | CREATE_UNICODE_ENVIRONMENT`. Codex CLI v0.160.0.

**Observed pattern** (one `exec_command failed` line per refused command, counted in the planner's `codex exec`
logs of 2026-10-01 … 02): **every one of the 25 implementation / review turns ran sandboxed (`workspace-write`), and 23
had refusals** — TODO-06 7, TODO-07b 5, TODO-08a2 8, TODO-08b2 9, TODO-09 review 6, TODO-16b 8, … Typical turns
ran 10–30 commands first. TODO-11 made it blocking:

| Turn | Ran | Refused | Note |
|---|---|---|---|
| TODO-11 review (new conversation) | 5 | 4 | then every read failed; no measurement possible — the planner measured |
| TODO-11 implementation (resumed) | 0 | 2 | refused from the first command, even file reads; no edits made |
| TODO-11 retry (resumed) | 0 | 1 | same |
| one-line probe (new conversation, same minute) | 1 | 0 | `Get-Content AGENTS.md` succeeded |
| TODO-11 implementation (new conversation) | 19 | 2+ | ran normally for ~10 minutes, then refusals; edits continued through `apply_patch` |

- It is **not the command and not the path**: the same `pwsh.exe -Command` that is refused in one conversation runs
  in a fresh one at the same time.
- It **appears after a while inside a conversation and then persists** — also when that conversation is resumed.
- Unsandboxed: the first `danger-full-access` turn (TODO-11, resumed in the conversation that had just been refused 17
  times) ran its commands with **no refusal** — consistent with the mechanism (no restricted token → no
  `CreateProcessAsUserW`).
- Not established: the root cause. Candidates are a per-conversation sandbox state (restricted token / sandbox
  account ACLs) going stale, the "unified exec" launch path with extended start-up attributes, or `pwsh.exe` being a
  Microsoft Store app alias under `WindowsApps`, which restricted tokens often cannot start — the last does not
  explain why a fresh conversation succeeds with the same alias.

**Decision (author, 2026-10-02).** Implementation runs one level up, `-s danger-full-access`: no restricted token, so
`CreateProcessAsUserW` is never called and the error cannot occur. The sandbox's limits are replaced by the guard
block above and the planner's audit. `read-only` stays for short cross-verification calls (they finish before the
failure tends to appear); `workspace-write` returns while another session drives UI automation.

**If it shows up again** (a sandboxed run): (1) do not retry the same conversation — it stays broken; (2) probe with a
one-line fresh `codex exec`; (3) continue in a fresh conversation handed the written review, or resume with
`-s danger-full-access` and the guard block; (4) if a turn ended with edits but no build, the planner builds and tests.

## Cautions met so far

| Situation | What to do |
|---|---|
| The implementer stops on a plan premise | treat it as a plan bug: fix the plan with a dated Correction, then resume the same conversation |
| The sandbox refuses process creation (`CreateProcessAsUserW` access denied) mid-task | the planner runs build / tests / measurements and gives the numbers back in the next turn |
| A resumed conversation keeps failing process creation while a fresh `codex exec` runs commands (TODO-11, 2026-10-02) | probe with a one-line fresh run; if only the resumed conversation is broken, start a new conversation for the task and hand it the written review (`PLAN.*.Review.md`) and the plan's decisions as its context |
| The implementer stops before finishing — usage quota exhausted (the author's Codex 5-hour allowance; the log ends with `ERROR: Your workspace is out of credits` and no report file), rate limit, crash (author, 2026-10-02) | the planner does **not** implement it itself: it starts a **substitute implementer** — a Claude subagent (Agent tool, general-purpose) in the same working tree — with the same kind of prompt as a Codex turn: what to read (plan and its decisions, the review, the stopped turn's log tail / report), what is already done (`git status`), what remains, the guard block, the verification commands and the final-report contents. The subagent reports back; the planner then audits, verifies (build, tests, renders, diff) and records in the plan's Status which parts the substitute implemented, exactly as for a Codex turn — one flow, planner never the implementer. Exception: simple documentation work (no code, no measurement) is done by the planner directly — starting a subagent for it costs more than the edit (author, 2026-10-02). Watch the turn by its report file *and* the log's last lines — other Codex processes (the author's app) keep `codex.exe` alive, so a process check alone misses a dead turn. When the allowance returns, a short review turn of the Codex conversation may check the substitute's part; the author decides whether it is worth the allowance |
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
| CsI data (TODO-21, later) | a 0.92–1.0 model / NIST bound borrowed from the GAGG test (CsI's coherent share differs) | implementer stopped at the check |
