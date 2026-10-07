# PLAN.Docs.TestRecords — test documentation and a generated test-result report

Scope: TODO-28 steps 2 and 3 (step 1, the calibration records, is done:
[PLAN.Docs.CalibrationRecords](archive/PLAN.Docs.CalibrationRecords.md)). Document what each test suite verifies, how,
with which oracle and tolerance; and produce the **test-result report** from the test runs themselves — reproduced by
a command or CI, never written by hand. The model is the author's existing procedure from another project; only its
process is adopted (structure, generation steps, what the report contains), none of its content.

Status: **implemented, M1 pending**, 2026-10-08 — review and implementation by Codex (session `01a1185d-c2a7-7cd1-be53-922321df54e0`; [review](PLAN.Docs.TestRecords.Review.md), turns [2](PLAN.Docs.TestRecords.Turn2.md), [3](PLAN.Docs.TestRecords.Turn3.md), [4](PLAN.Docs.TestRecords.Turn4.md), [5](PLAN.Docs.TestRecords.Turn5.md)); planner checks: build 0 / 0, full tests 815 passed / 28 skipped, Python suites pass, self-test 26 refusals, catalog 153 / 153; turn 4 reworked an unreadable first catalog (2.7 MB of copied source → 281 KB) and compressed the milestone (6.8 → 0.96 MB); next: commit, then milestone M1 collected by the planner on the clean tree (TD-10).

## The model process (what is adopted)

Three layers, each with one job:

```text
[runner] ── run record ──> [generator] ── report + generation record ──> [control: review / commit]
                                ▲
                       static sources (test catalog, traceability)
```

| Principle | What it means here |
|---|---|
| A document's facts come from **one run's record** | the report is generated from run records written by the test run (from TRX / logger output), never from console text read by a person or from memory |
| Run record identifies **subject, tool and environment separately** | subject = the built assemblies (SHA-256 per binary); tool = the repository commit (40 hex) + `dirty` (with a tree id when dirty); environment = OS, .NET SDK / runtime, opt-in switches set (`GCAM_*`) |
| Fixed verdict vocabulary, worst-of aggregation | `passed` / `failed` (mismatch vs error) / `not_executed` with a reason code (e.g. opt-in switch off, desktop not available, not implemented); a case is the worst of its parts; counts are recomputed, never typed |
| **Never turn fail, skip or pending into pass** | a skipped opt-in suite is reported as not executed, with its reason, separately counted |
| One subject per report | runs of different commits / binaries are not merged; merging is per test case, latest executing run wins, the choice recorded |
| Hashes re-computed by the generator | missing file or hash mismatch → stop, no report |
| **Formal vs draft** | formal only when every identification field is present, the tree is clean, the generator's own commit is clean; otherwise the report says "draft" on its face and the generation record says why |
| Omissions are shown, not filled | a catalogued test with no run → "missing run" in the report |
| The generator makes no human act | no reviewer, approval or sign-off fields filled by a tool or an AI |
| A checker with a self-test | the generator / checker has seeded-defect copies that must fail |

Not adopted (no counterpart here): patient-data handling, user-manual chapters, operator-confirmed steps, step-level
execution logs for unit tests (an xUnit case is one step; the desktop scenarios already write their own manifests,
which a run record links by hash).

## What exists (checked in the repo, 2026-10-07)

| Item | Where | What it does |
|---|---|---|
| Test projects | `tests/Gcam.Tests` (engine), `Gcam.Studio.Tests` (Core VMs), `Gcam.Studio.Services.Tests`, `Gcam.Studio.UiTests` (headless oracles + desktop scenarios), `Gcam.Studio.RenderTests` | xUnit; opt-ins by environment variable: `GCAM_EVIDENCE_TESTS` (Category=Evidence, 7 traits), `GCAM_UI_TESTS`, `GCAM_UI_BREAK_VERDICT`, `GCAM_RENDER_SNAPSHOTS`, `GCAM_README_CAPTURE_ONLY`, `GCAM_CRRC_VECTORS`, `GCAM_UIA_RUN` |
| Other test runs | `rtl/run_cocotb.py` (cocotb, CI job), `samples/evidence/tests/test_provenance*.py` (unittest, CI), `samples/evidence/calibration_record.py --release` (CI) | not xUnit; separate runs |
| CI | `.github/workflows/ci.yml` | `dotnet test Gcam.sln -c Release --no-build --logger "console;verbosity=normal"` — **no TRX, no artifact** |
| Hand-written inventory | `docs/VV.Studio.md` "Current test inventory" | a hand table at `13387fb` (2026-10-04): 703 pass / 22 skip — stale since TODO-34 … 39 (TODO-39 verification: 799 passed) |
| Requirement → test trace | `docs/VV.Studio.md` traceability matrix (SR → test names → status "pass (headless); desktop pending …") | test names typed by hand; statuses typed by hand |
| Desktop run manifests | `src/Gcam.Studio.UiTests/…/ui-runs/<runId>/manifest.json` (under the test output) | per desktop run: scenarios, verdicts |
| Engine evidence trace | `docs/VV.Gcam.Evidence.md` cites test classes per EV (e.g. `TransportInvariantTests`) | by hand |
| Generator pattern in this repo | `samples/evidence/calibration_record.py` (+ `calibration-records.json`, CI `--release`) | pinned inputs by SHA-256, generated blocks between markers, human prose untouched, byte-exact `--check`, refuses drafts on release |

## Proposed decisions (verify / improve)

| ID | Decision |
|---|---|
| TR-1 | **Test documentation = a test catalog** (static source): one entry per test class (not per case), with what it verifies, the oracle (closed form, independent implementation, recorded evidence, golden file), the tolerance and its derivation, the opt-in switch if any, and the requirement / evidence it traces to (SR-nn, EV-nn, theme). Human-written prose in a doc (proposed `docs/VV.Tests.md`), with a machine block generated from test discovery (`dotnet test --list-tests` or reflection) so a `--check` fails when a test class exists without a catalog entry or an entry names no existing class — verify the discovery method and the granularity (class vs method) |
| TR-2 | **Run record:** a small script (Python, standard library, like `calibration_record.py`) runs or wraps `dotnet test … --logger trx` and writes `run-record.json`: tool (commit, dirty, tree id), subject (SHA-256 of each test-assembly and product DLL under test), environment (OS, SDK, runtime, `GCAM_*` switches), status, per test case outcome + duration + skip reason, and the TRX files by hash. Separate run records for the opt-in runs (desktop, render, evidence, cocotb, Python unittest) — verify what each emits today (cocotb results XML? desktop manifest) |
| TR-3 | **Verdicts:** xUnit Passed → `passed`; Failed → `failed` (`mismatch` for an assertion, `error` for an exception — verify that TRX distinguishes them); Skipped → `not_executed` with a reason code read from the skip reason (`opt-in-off`, `desktop-unavailable`, …) — verify the skip messages are specific enough, propose changes to them if not |
| TR-4 | **Report** (`Test Result`): generated from run records + the catalog: header (commit, formal / draft and why, generation record id), per-run table (run id, date, commit, dirty, OS, SDK, switches, status), summary counts per project and per verdict (recomputed), per-catalog-class results, missing runs, skipped suites with reasons, the requirement view (each SR's named tests with their verdicts from the run — replacing the hand-typed statuses) |
| TR-5 | **Where the report lives (likely an author decision):** (a) CI artifact on every push (TRX + run record + report), nothing committed; (b) (a) plus a committed report for a release / milestone, regenerated on demand and `--check`-ed against its pinned run records (like the calibration records); (c) committed only. Recommendation to verify: (b), with the committed run records reduced to JSON (no TRX in git) — measure their size |
| TR-6 | **Replacing hand-written numbers:** `VV.Studio.md` "Current test inventory" and the traceability statuses become generated blocks (markers, as the calibration records); the dated History records stay hand-written history |
| TR-7 | **Checker + self-test:** the generator refuses missing / mismatched hashes, merges across subjects, typed counts; a `--self-test` seeds one defect at a time into copies and requires each to fail; runs in CI |
| TR-8 | **Docs:** `AGENTS.Conventions.Docs` (new VV document, generated blocks), `AGENTS.md` / README build-and-test section, `AGENTS.Rationale` rows — proposed by the implementer, applied by the planner |

## Decisions after review (2026-10-08)

The review ([PLAN.Docs.TestRecords.Review](PLAN.Docs.TestRecords.Review.md); subject `266b740`, normal Release run
118 classes / 843 cases: 815 passed, 28 skipped) corrected the plan: TRX does not reliably separate assertion failures
from exceptions and its `notExecuted` counter reads 0 despite 28 skipped results; the desktop run manifests are
exploratory (saved before the final cleanup assertions), so they are not the test verdict; only three switches gate
xUnit tests (`GCAM_EVIDENCE_TESTS`, `GCAM_RENDER_SNAPSHOTS`, `GCAM_UI_TESTS`) — the others change behaviour; CI's "14
desktop tests" is stale (20); commit + dirty alone does not identify the subject (hash the source content and the built
binaries before and after the run); the traceability matrix mixes exact names with abbreviations, ellipses and
descriptions, and mixes T / I / C / M evidence. The author answered four questions.

| ID | Decision | Source |
|---|---|---|
| TD-1 | **Where:** every CI run uploads its records, TRX, logs and report as artifacts (90-day retention); a **milestone** commits `docs/VV.Tests.Results.md` and `samples/testing/records/<milestone>/` (normalised JSON run records, generation record, selection manifest) | **author** (Q1, recommended) |
| TD-2 | **Raw evidence:** the milestone also commits the **TRX files**, sanitised (machine name, user, absolute paths replaced by tokens; the sanitisation is deterministic and recorded), so `--check` can re-normalise from TRX and verify the JSON; built DLLs are identified by hash only (not committed); measure the committed size per milestone | **author** (Q2) |
| TD-3 | **Runners: all collected now** — xUnit (TRX), Python unittest (a stdlib result adapter), cocotb (an output-root argument and a results adapter, partial / error states kept), and the standalone checkers as recorded invocations; a CI aggregate job binds the family records to one source snapshot and publishes the report; the background-shape numerical tests (NumPy / SciPy) and the C# vector export in the Linux RTL job are listed as gaps, not silently counted | **author** (Q3) |
| TD-4 | **Traceability:** a generated **automated-results block** beside the matrix (exact assembly-qualified selectors from the catalog); manually judged I / C / M evidence, desktop records and historical statuses stay as written | **author** (Q4, recommended) |
| TD-5 | **Catalog:** `docs/VV.Tests.md`, one entry per discovered test class (and per unittest class, cocotb module + configuration, checker): purpose, level, inputs / seeds, oracle and its limits, tolerance with its derivation ("exact", "structural" or "measurement only" where so), method groups where gates or tolerances differ, SR / EV / VAL selectors, gates, reproduce command; prose plus a fenced JSON metadata block per entry; a generated discovery block; the checker requires exactly one entry per class, no extras, resolving selectors, and flags entries whose source hash changed for review. No new tolerance is chosen; every statement comes from the test's own code | review; planner |
| TD-6 | **Run records (schema v1):** identity, source content manifest (incl. tests and untracked relevant files, read-only hashing), subject binaries hashed before and after (mismatch refuses), tool versions, environment (allow-listed `GCAM_*` switches, unset ≠ 0), per case raw outcome → `passed` / `failed` (`failureKind` assertion / error / **unknown** — no guessing from messages) / `not_executed` with skip codes (`opt_in_off`, `optional_vectors_off`, `missing_fixture`, `desktop_unavailable` only when established, `platform_unsupported`, `runner_skip_unknown`); run diagnostics and missing expected cases visible | review; planner |
| TD-7 | **Selection and status:** an explicit selection manifest per milestone (no silent "latest wins"; repeats and earlier failures visible; break-verdict and fault-injection runs never substitute product results); formal / draft is provenance only, separate from coverage and from pass / fail; no human-approval fields filled by tools | review; planner |
| TD-8 | **Generated blocks in VV.Studio:** `TEST INVENTORY` replaces the current-inventory table; `AUTOMATED TRACE RESULTS` beside the matrix; dated History records, measured values and the historical coverage baseline untouched; CI summary text derived from discovery | review; planner |
| TD-9 | **Tool:** `samples/testing/test_records.py` (stdlib; `collect`, `generate`, `check`, `check-catalog`, `self-test`) + adapters; the self-test seeds one defect at a time into scratch copies and requires the precise refusal; CI runs check-catalog, archived replay of committed milestones and the self-test | review; planner |
| TD-10 | **First milestone after the commit:** turn 2 builds the tooling and the catalog and proves it on a scratch (draft) collection; the planner commits, then collects milestone **M1** on the clean tree (normal profile; evidence / render opt-ins if they finish; desktop attached later only on the author's go) and commits it as a separate record | planner |

## Steps

1. Review turn (no code): check every row; list the test classes and current counts per project (normal run and
   each opt-in), what TRX / cocotb / unittest / desktop manifests contain, skip messages, run-record size; propose the
   catalog form, the run-record schema, the report layout and the CI changes →
   `docs/PLAN.Docs.TestRecords.Review.md`.
2. Decisions after review (author for TR-5 and the catalog's home); implement in the same conversation: catalog +
   discovery check, run-record writer, generator + self-test, CI; write the catalog entries (implementer drafts, the
   planner checks every oracle / tolerance statement against the test code).
3. Planner: regenerate independently, verify `--check` byte-exact, CI green, records applied, commit on the author's
   word. Desktop and render runs only on the author's go.

## Done when

- Every test class has a catalog entry; CI fails if one is missing or stale.
- One command produces the test-result report from run records; CI produces it on every push; counts in the VV
  documents come from it, not from hand.
- Skipped and pending suites are visible with their reasons; nothing not executed reads as passed.

## Not here

- Changing what any test asserts (only skip messages, if TR-3 needs it).
- The engine's numerical evidence (seed ensembles, `samples/evidence/`): it has its own provenance (TODO-37).
- Formal sign-off: the author's commit is the control step.
