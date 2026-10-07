# PLAN.Evidence.RunProvenance.Turn2 — implementation and verification

2026-10-07. Implemented RD-1 through RD-8. No material disagreement with the adopted decisions. No Git state
changes, desktop activity, deletion, installation or upload. One local test is pending the approval required for
temporary Git mutations; it is enabled in CI. Existing evidence and calibration blocks were not rewritten.

## Files by group

| Group | Files and behavior |
|---|---|
| Provenance/driver | `samples/evidence/provenance.py` (new); `run_seeds.py`; `rtl_seeds.py`. Canonical identities, source capture, frozen repository-local closure and managed output copies, explicit runtime pin, root source index, per-seed bindings, reuse preflight and fresh attempt directories. |
| Legacy aggregates | `samples/evidence/aggregate.py`, `cascade_fit.py`. Validate consumed seeds and duplicate roots; embed provenance; JSON-list/values envelopes and output-set sidecars. The legacy aggregator also now honors manifest `seed_offset`, matching the driver. |
| Ambient pipeline | `samples/evidence/ambient/aggregate_ev.py`, `aggregate_gate.py`, `aggregate_csco.py`, `select_thresholds.py`, `select_csco_thresholds.py`, `bias_baseline.py`. Validate before numerical processing/output; inherit summary lineage for bias baseline; preserve numerical formatting and CSV columns. |
| Angular/calibration | `samples/evidence/angres/aggregate_angres.py`, `samples/evidence/calibration_record.py`. Cover all family/select/floor modes and print selection/validation source and analysis identities separately. CAL-04 filters the shared file by each role's families. |
| Tests/CI | `samples/evidence/tests/test_provenance.py` (new); `.github/workflows/ci.yml`. 29 stdlib tests, including real ambient-aggregator fixtures; calibration CI enables the isolated-Git test. |
| Documentation | `samples/evidence/README.md`; this turn report. Root README, plan, review, AGENTS, CLAUDE and VV documents were not edited. |

The pre-existing Todo edit and untracked plan/review belong to the planner/review turn and remain untouched.

## Schema and behavior

Schema version 1 has `Sources` keyed by the canonical SHA-256 of each complete source record and `Inputs` keyed
logically by family/seed. Each source record contains:

- `Source`: full commit, explicit scope, dirty flag, binary tracked-diff SHA-256, untracked content hashes and a
  digest binding those observations. Root build attributes/configuration are included in the source scope.
- `Executor`: CLI/probe/RTL kind; file hashes from the actual staged executable closure; OS/architecture, driver
  Python version, effective .NET runtime and applicable Python/NumPy/Icarus/vvp versions. RTL modes have typed
  variants. `ExecutionId` is the canonical executor digest. Driver/helper bytes participate for every kind.
- `BuildObservation`: Git and, when applicable, selected SDK version. A source observation does not assert that
  a build was compiled from that tree.

Each input binds `Family`, `Seed`, `SourceId`, `RecipeSha256` and `Outputs`. Recipe identity covers the manifest
family entry plus hashes of the captured sample data; this is deliberately conservative and includes other sample
inputs. Output hashes cover the successful attempt's config/logs/numerical files. Extra unbound files are refused.
Root `run-info.json` is an invocation log and source-record index, not an identity substituted for old seeds.
Invocation details do not participate in execution identity. Reuse preserves original seed source records.

Every child executes from its copied managed and repository-local script/data closure under
`runs/<family>/<seed>/attempts/<id>/snapshot/`. Copies preserve the repository layout needed by Python local imports
and AST-loaded RTL studies. Managed launches use explicit `--fx-version`; RTL frontend passes the same pin to its
CLI subprocess. A new attempt avoids stale simulator compilations. Successful attempt outputs are published at the
stable paths the numerical parsers already use, with `done.json` written last. Copies and attempts are retained;
nothing is deleted automatically.

Summaries add `Analysis`: writer/helper hashes, tool versions, semantic options and input-summary hashes where
applicable. JSON has an embedded `Provenance` object. The optional full legacy metric JSON is now
`{Provenance, Metrics}` and future values JSON is `{Provenance, Values}`. CSV columns are unchanged.

Every output receives `<filename>.provenance.json`: schema version, the same provenance, common-base depth and
relative member paths with hashes binding the whole output set. All serialization/validation happens before any
output write; binding sidecars are published last. JSON envelope/sidecar disagreement, missing members or changed
bytes fail validation. Sets spanning output directories use a shared logical base without recording machine paths.
An interrupted publication is invalid rather than an accepted partial set. Pinned calibration JSONs already bind
their own bytes and can use embedded provenance without requiring a separately pinned sidecar.

Mixed execution within one executor type, mixed recipes within a family, conflicting duplicate seeds, missing
provenance and malformed bindings fail closed. There is no mixed-mode or backfill switch. Dirty source is allowed.
Old calibration files retain the exact existing fallback text; a record with only one new role labels the other
role as not recorded.

## Verification output

Fresh smoke family: existing `deadtime`, seeds 12345 and 1000003, `--n 2 --jobs 1`; output only under
`%TEMP%/gcam-todo37/`. The CLI was built in Release first. Initial and final implementation checks were separate;
the final smoke was explicitly rerun with `--force` after the last execution-code edits.

```text
ok   deadtime 12345 exit=0 4.4s
ok   deadtime 1000003 exit=0 4.4s
2 runs, 0 failed
63 metric keys from 2 runs in 1 families
skip deadtime 12345
skip deadtime 1000003
2 runs, 0 failed
summary sidecar: valid
mixed aggregate exit 1 provenance refused: mixed execution identity for cli
changed reuse exit 1 provenance refused: deadtime/12345: reuse identity changed; use --force or a new output root
changed reuse refused before root mutation
```

Both final seeds have schema 1, four bound outputs each, the same recipe and CLI execution identity:

| Field | Recorded value |
|---|---|
| Commit | `4c65effb7b22353c8f8e6433167a4e2cde69857d` |
| Dirty | true (implementation in progress; accepted) |
| Tracked-diff SHA-256 | `203179d2a208ecd315b7972989a2216e3fcb4b9c3ab0e3efe26e567918c9b71e` |
| ExecutionId | `c71797f6cb50c9cddacf2821ad25652a3bca0eb3c3e4c7a24a54f2635586b253` |
| RecipeSha256 | `d2e2e23da3e8cc308867b4343b30ad9bc417b82c6c0b92e50c6635321b1537db` |
| Execution tools | Windows, AMD64, driver Python 3.11.9, explicitly pinned .NET runtime 9.0.13 |

The mixed-refusal fixture copies the final bound seed outputs into a separate scratch root and deliberately changes
one source's CLI identity, recomputing valid source/executor record bindings. The aggregator exits before creating
the requested summary. The reuse-refusal fixture copies the CLI output into scratch, changes the copied DLL bytes,
and supplies it through `--cli`; the driver refuses the mismatching build before launching it or changing the root
index. The original output root and repository binaries were not modified by either deliberate-refusal fixture.

`dotnet test Gcam.sln -c Release --logger 'console;verbosity=minimal'` exited 0:

| Project | Passed | Skipped | Failed |
|---|---:|---:|---:|
| Gcam.Tests | 465 | 0 | 0 |
| Gcam.Studio.Tests | 209 | 0 | 0 |
| Gcam.Studio.Services.Tests | 95 | 7 | 0 |
| Gcam.Studio.UiTests (oracles only) | 14 | 20 | 0 |
| Gcam.Studio.RenderTests | 0 | 1 | 0 |
| Total | 783 | 28 | 0 |

The baseline .NET counts supplied in the task are unchanged. Desktop scenarios were skipped; no UI-test enabling
variable was set. Release CLI build exited 0 with zero warnings/errors.

The final stdlib suite has **28 passed, 1 skipped, 0 failed** (29 tests). The skipped temporary-Git test is awaiting
local approval; ordinary tests cover source/untracked hashes via controlled queries, staging real Python imports,
managed-copy byte preservation, malformed/missing/mixed metadata, recipes, reuse, duplicate roots, tampering,
unbound files, output-set binding across directories, JSON envelopes, deterministic CAL roles, and a real ambient
aggregator invocation/refusal using small fixtures. The isolated-Git test is wired into CI with its enable flag.

`python -B samples/evidence/calibration_record.py --release`:

```text
docs/VV.Gcam.Calibration.md: 4 records up to date; all checks passed
```

`git diff --check` passed. Python syntax was parsed without writing bytecode. Existing evidence JSON/CSV and the
calibration document have no diff. Final LF/status checks are recorded in the command log.

## Deviations and limits

- Local temporary-Git test is pending approval rather than silently run or called passed. CI runs it. No baseline
  evidence or calibration blocks were regenerated to exercise new formatting.
- Verification uses a real cheap CLI family and a real stdlib ambient aggregator fixture. The full scientific
  NumPy/SciPy/RTL ensembles and probe were not rerun; staging tests exercise copied local imports and RTL bytes.
- Installed Python packages, runtimes and simulator programs are versioned external dependencies, not copied OS
  installations. Repository-local modules, scripts, data and managed output files are copied. No external packages
  were installed or updated.
- Source observations do not establish source-to-build correspondence. Hashing cannot prove that a third party
  maliciously mislabeled historical output; bindings ensure consistency and detect ordinary drift/tampering.
- Sample data capture is conservative, so an unrelated captured sample input can invalidate recipe reuse.
- `--force` uses fresh execution attempts without deleting old files. If previous stable paths contain an output no
  longer produced, unbound-file validation refuses it; use a fresh root. Attempt snapshots are retained and use
  additional disk space. Publication is preflighted and bound, not a transactional multi-file filesystem operation.
- `.NET tests` appeared as an existing test transcript in the prompt; I interpreted the required command as the
  repository's documented full Release solution test command, and ran it.

## Write-producing command audit

All implementation edits used `tools.apply_patch` for only the files listed above, including this report. No shell
editor, Git mutation or command deleting a file was used. Read-only commands, Python AST parsing, calibration
`--release`, Git status/diff checks and polling wrote no task files.

The shell commands that wrote files were:

1. `python -B -m unittest discover -s samples/evidence/tests -p 'test_provenance*.py' -v` (first 25-test pass), and
   subsequent invocations without `-v` (25, 28 and final 29-test suites). They retain uniquely named fixtures only
   under `%TEMP%/gcam-todo37/`; their successful aggregator subprocesses also write fixture summaries/sidecars there.
2. `dotnet test Gcam.sln -c Release --logger 'console;verbosity=minimal'` — project build/test intermediates under
   repository `bin`/`obj` and any normal NuGet cache activity. One running process was polled; no watcher loop.
3. `dotnet build src/Gcam.Cli/Gcam.Cli.csproj -c Release` — repository build intermediates/output and permitted cache.
4. `python -B samples/evidence/run_seeds.py --out "$env:TEMP\gcam-todo37\fresh-deadtime" --family deadtime --n 2 --jobs 1`
   — fresh seed snapshots, outputs and records. Same command was run again for validated reuse; only the invocation
   index is rewritten on reuse, while original completed seed records are preserved.
5. `python -B samples/evidence/aggregate.py --runs "$env:TEMP\gcam-todo37\fresh-deadtime" --family deadtime --n 2 --json "$env:TEMP\gcam-todo37\fresh-deadtime-summary.json"`
   — summary and bound sidecar.
6. `python -B samples/evidence/run_seeds.py --out "$env:TEMP\gcam-todo37\final-deadtime" --family deadtime --n 2 --jobs 1`
   — initial final-check root, then the same command with `--force` after execution-code edits, then the original
   command again for validated reuse.
7. `python -B samples/evidence/aggregate.py --runs "$env:TEMP\gcam-todo37\final-deadtime" --family deadtime --n 2 --json "$env:TEMP\gcam-todo37\final-deadtime-summary.json"`
   — final summary and bound sidecar.
8. The deliberate-refusal PowerShell here-string piped to `python -B -` — creates `%TEMP%/gcam-todo37/mixed-demo/`,
   copies bound seed outputs and writes modified fixture records/root index; creates `%TEMP%/gcam-todo37/changed-cli/`,
   copies managed files and appends bytes to the **scratch copy** of `Gcam.Cli.dll`. Both failing child commands write
   no summaries and do not rewrite the original root index. Assertions verified their refusal messages and absence
   of the requested mixed summary.

## APPROVAL REQUESTS

Run this exact command to verify the final isolated-Git test locally:

```powershell
$env:GCAM_PROVENANCE_GIT_TESTS='1'; python -B -m unittest discover -s samples/evidence/tests -p 'test_provenance*.py' -v
```

Why approval is required: hard limit 2 requires approval for any state-changing Git command, even inside a disposable
scratch fixture. That one test executes `git init -q --template=`, `git add .gitattributes source.py`, and
`git -c commit.gpgsign=false -c user.name="Provenance test" -c user.email=provenance@example.invalid commit -q -m fixture`
with cwd a new `%TEMP%/gcam-todo37/provenance-test-*/git-fixture/`. It neither destroys nor changes existing repository
content; it creates only a scratch Git repository and its initial commit. No undo is necessary for the working
repository. The fixture can be retained, or later removed with separately approved cleanup. The enabling environment
variable is scoped to that shell process. No other approval is requested.
