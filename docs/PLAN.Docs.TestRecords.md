# PLAN.Docs.TestRecords — test documentation and a generated test-result report

Scope: TODO-28 steps 2 and 3 (step 1, the calibration records, is done:
[PLAN.Docs.CalibrationRecords](archive/PLAN.Docs.CalibrationRecords.md)). Document what each test suite verifies, how,
with which oracle and tolerance; and produce the **test-result report** from the test runs themselves — reproduced by
a command or CI, never written by hand. The model is the author's existing procedure from another project; only its
process is adopted (structure, generation steps, what the report contains), none of its content.

Status: **reference plan, 2026-10-07** — waiting for the implementer's review (turn 1).

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
