# PLAN.Evidence.RunProvenance — every committed evidence summary states the code that produced it

Scope: TODO-37. The seed driver records the engine commit and a dirty flag only in the machine-local
`run-info.json` of its output folder, so no committed summary says which code produced it; the calibration records
(CAL-01 … 04) therefore print "engine commit not recorded" (calibration-records review, CD-8). Several evidence runs of
this week ran on an uncommitted tree (`engine_tree_dirty: true`), where a commit hash alone does not identify the code.
This plan carries provenance from the run into every committed summary and makes the record generator print it.

Status: **done** 2026-10-07 — review and implementation by Codex (session `01a114b5-468c-78c2-a859-bcb3d92866ba`; [review](PLAN.Evidence.RunProvenance.Review.md), [turn 2](PLAN.Evidence.RunProvenance.Turn2.md)); planner checks: provenance tests 29 / 29 (the git-fixture test run on the author's approval), `calibration_record.py --release` unchanged, existing evidence files untouched, .NET suite green (783 passed).

## What exists (checked, 2026-10-07)

| Item | Where | What it does |
|---|---|---|
| Driver | `samples/evidence/run_seeds.py` (lines ~179–183) | writes `<out>/run-info.json`: `engine_commit` (`git rev-parse HEAD`), `engine_tree_dirty` (`git status --porcelain -- src samples rtl` non-empty), families, manifest + SHA-256, n override, jobs, start time; one isolated cwd per (family, seed) with `done.json`, stdout / stderr |
| Aggregators | `samples/evidence/aggregate.py`, `ambient/aggregate_ev.py`, `ambient/aggregate_gate.py`, `ambient/aggregate_csco.py`, `ambient/select_thresholds.py`, `ambient/select_csco_thresholds.py`, `angres/aggregate_angres.py`, `cascade_fit.py`, `rtl_seeds.py` | read the per-seed outputs and write the committed summaries / selection files; none copies `run-info.json` |
| Record generator | `samples/evidence/calibration_record.py` + `calibration-records.json` | prints "not recorded; the code landed in <commit>" in each CAL record's §6 |
| Example of a dirty run | `%TEMP%\gcam-todo34\runs\run-info.json` | `engine_commit` = the docs commit before the code was committed, `engine_tree_dirty: true` |

## Proposed decisions (verify / improve)

| ID | Decision |
|---|---|
| RP-1 | **What identifies the code:** besides the commit and dirty flag, record (a) the SHA-256 of the binary diff of the tracked engine paths against HEAD when dirty (`git diff --binary HEAD -- src samples rtl` plus the list of untracked files there), and (b) the SHA-256 of the built assemblies the runs executed (`Gcam.Cli.dll` and the engine DLLs it loads, or the probe's), plus the .NET SDK / runtime and Python versions. Verify what is cheap and stable; prefer (b) as the identity that cannot drift |
| RP-2 | **Every aggregator / selector embeds the run's provenance** (one `Provenance` object, same schema everywhere) in the file it writes; refuses to aggregate runs from more than one provenance unless told (`--allow-mixed`), and refuses a missing `run-info.json` |
| RP-3 | **Dirty runs are allowed but loud:** a summary from a dirty tree carries the diff hash, and the record generator prints "dirty tree — diff <hash>" beside the commit; a test or CI never fails on dirty provenance (it is a fact, not an error) |
| RP-4 | **calibration_record.py** prints the provenance from the pinned files in §6; files without it keep the honest "not recorded" line (the existing four records stay as they are unless their files are regenerated) |
| RP-5 | **No change to any evidence number**: existing committed summaries are not rewritten; the change applies to the next runs. Optionally a `--provenance-only` mode that re-attaches provenance to an existing summary from a kept run folder (only if the folder still exists) |
| RP-6 | **Tests:** a small Python test (run by CI next to `calibration_record.py --check`) for the provenance schema, the mixed-provenance refusal and the dirty-diff hash on a temporary git repository; stdlib only |

## Decisions after review (2026-10-07)

The review corrected the plan's central premise: copying the root `run-info.json` cannot identify completed seeds —
the driver overwrites it on every invocation and then skips successful seeds without checking their code or recipe —
and a commit or diff does not identify the build that ran (all six shared engine DLLs differ between the CLI and probe
output folders today). The planner adopts the review's eight proposals; the author answered its four questions.

| ID | Decision | Source |
|---|---|---|
| RD-1 | **Per-seed provenance**, versioned schema, binding source identity (commit, tracked-diff hash, untracked-content hash), execution identity (hashes of the executable closure actually run, per executor kind: CLI, probe, Python / RTL), recipe identity (manifest entry + request hashes) and the seed's output hashes; validated before a completed seed is reused | review #1, #3 |
| RD-2 | **Scope (author):** the seed driver and every downstream summary / selector that consumes its runs, including `ambient/bias_baseline.py`; direct generators and curated reports are out of scope until planned separately | **author** (recommended); review #4 |
| RD-3 | **No `--allow-mixed` (author):** aggregation fails closed when execution identities conflict; different executor kinds (CLI vs probe) are represented by kind, not treated as a mixed build; conflicting duplicate seeds are refused | **author** (recommended); review Q2 |
| RD-4 | **CSV outputs carry provenance in hash-bound sidecar JSON (author)**; JSON outputs embed an object envelope; a versioned format binds every member of an output set by hash; validation happens before any output is written | **author** (recommended); review #5 |
| RD-5 | **Drift: freeze everything that executes, Python and RTL included (author):** the driver stages an immutable copy of the managed artifacts (CLI / probe output folders) **and** of the Python / RTL closure a family runs (scripts and the local modules they import, the RTL sources and testbench files; interpreter / simulator versions recorded) into the run folder and executes from that copy, so that a concurrent edit or rebuild in the working tree cannot change a run in progress | **author** (chose the stronger option over the recommended managed-only freeze); review Q4 |
| RD-6 | **calibration_record.py** prints selection and validation provenance separately in §6; files without provenance keep the existing "not recorded" text byte for byte | review #6, #7 |
| RD-7 | **No backfill**, no provenance-only mode; existing evidence files and calibration blocks unchanged | review #7 |
| RD-8 | **Tests:** stdlib Python tests (provenance schema, refusal of mixed / missing / conflicting provenance, sidecar binding, staged-copy execution, dirty-diff hash on a temporary repository) added to the calibration CI job | review #8 |

## Steps

1. Review turn (no code): verify the table and RP rows against the scripts, check which aggregators write which
   committed files, propose the schema and the cheapest stable identity → `docs/PLAN.Evidence.RunProvenance.Review.md`.
2. Decisions after review; implement in the same conversation; the planner verifies (a fresh small run end to end,
   `calibration_record.py --release`, CI job).

## Not here

- Re-running old evidence to attach provenance (only if the author asks).
