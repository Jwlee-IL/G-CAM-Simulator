# PLAN.Docs.TestRecords.Turn2 — implementation and draft collection

Scope: implementation of TD-1 through TD-10, tooling/catalog/CI verification, scratch evidence and proposed documentation wording. No milestone, product behavior, test assertion or tolerance changes.

Status: implemented and scratch-verified, 2026-10-08. Catalog descriptions marked incomplete require planner review. GitHub Actions and successful native RTL execution remain unverified.

## Files by group

| Group | Files / change |
|---|---|
| Tooling | `samples/testing/test_records.py`: collect/generate/check/check-catalog/self-test; schema v1; source-content and pre/post subject hashes; explicit selection; provenance distinct from coverage; deterministic sanitization; replay/export/aggregation. `samples/testing/adapters.py`: TRX, unittest and cocotb normalization. `samples/testing/unittest_adapter.py`: structured callbacks, parent/subtest worst-of, failure/error/skip events. Standard library only. |
| RTL infrastructure | `rtl/run_cocotb.py`: fresh `--output-root`, configuration/parameter/seed JSON and before/after compiled-simulation hashes. Assertions in the RTL benches are untouched. |
| Catalog | `docs/VV.Tests.md`: 153 entries with JSON metadata, source fingerprints, source-linked comparison/input/comment excerpts, method groups, gates, exact trace selectors and generated expanded-discovery block. |
| Generated Studio content | `docs/VV.Studio.md`: TEST INVENTORY and AUTOMATED TRACE RESULTS marker pairs and generated content only. Byte comparison against the scratch before-image passed outside these regions. Historical statuses, matrix prose and measured values are untouched. |
| CI | `.github/workflows/ci.yml`: producer collections, per-project TRX, always-run generation/upload, derived summary, standalone checker records, aggregate/replay, 90-day artifacts. Build-warning log moved by configuration to runner scratch rather than creating an untracked file in the checkout. |
| Turn report | This file. |

The plan, review, AGENTS files, other VV documents, README, CLAUDE and product/test sources were not edited. `docs/AGENTS.Todo.md`, `docs/PLAN.Docs.TestRecords.md` were already modified by the planner; the review was already untracked. No `docs/VV.Tests.Results.md` was created in the tree: the generated draft report is scratch-only. No records/M1 directory was created. A pre-existing machine identifier in the historical Plot performance gate paragraph of VV.Studio is outside the permitted blocks and was preserved. The planner must redact it before public commit; new content and generated blocks pass the privacy scan.

## Verification and measured results

| Verification | Result |
|---|---|
| `dotnet build Gcam.sln -c Release` | Passed, 0 warnings / 0 errors in the captured build. |
| `DOTNET_CLI_UI_LANGUAGE=en dotnet test Gcam.sln -c Release` | Corrected invocation passed: 815 passed, 28 skipped, zero failed. |
| Fresh per-project collection | Same 843 cases, no missing xUnit cases; five sanitized TRX files. |
| Python suites through the result adapter | 38 passed, one skipped; 29 provenance cases plus ten background-shape cases. NumPy/SciPy were already available. GCAM_PROVENANCE_GIT_TESTS was explicitly 0, so the Git-mutation fixture was not run locally. |
| Standalone checker collections | Calibration release check, catalog check, record self-test and archived replay each passed. Archive discovery reports zero milestones explicitly; this is not a passing milestone claim. |
| `python samples/evidence/calibration_record.py --check` | Passed: four calibration records current. |
| Expanded catalog discovery | Passed: 118 xUnit classes, 590 methods, 843 expanded cases. Source methods and discovered methods agree; all other catalog units also match source membership. |
| Self-test | 22 distinct seeded defects rejected for the expected reason; additional sanitizer/idempotence, all-skipped TRX, assertion/exception, XML outcome, URL/account-token and real unittest/subtest callback checks passed. Defect copies retained in scratch. |
| Record/report check | Current scratch bundle, portable export, generated Studio blocks and simulated three-producer aggregate all passed replay. |
| Workflow | Parsed locally with the already-installed YAML parser; jobs/steps/needs/retention structure and Python syntax checked. `git diff --check` passed. No Actions run was started. |
| Privacy / line endings | Every retained exported evidence file passed privacy checks and contained no CR bytes. New repository deliverables are English/LF. |

One initial full test invocation failed because the implementer set GCAM_CRRC_VECTORS to an empty string. The unchanged test accepts any non-null value as an export-directory request and rejected that empty path. The command was corrected to unset the variable; the collector now removes it from its child environment in the normal profile. This was an invocation error, not a tolerance failure, and the initial log is retained. No assertion was changed or loosened.

| Project | Before passed/skipped | After passed/skipped | Classes / cases |
|---|---|---|---|
| Gcam.Tests | 481 / 0 | 481 / 0 | 72 / 481 |
| Gcam.Studio.Tests | 211 / 0 | 211 / 0 | 23 / 211 |
| Gcam.Studio.Services.Tests | 108 / 7 | 108 / 7 | 14 / 115 |
| Gcam.Studio.UiTests | 15 / 20 | 15 / 20 | 8 / 35 |
| Gcam.Studio.RenderTests | 0 / 1 | 0 / 1 | 1 / 1 |

No desktop, render or long numerical-evidence test was executed. All three xUnit opt-ins were explicitly off. UI headless oracles ran normally. No installation, deletion, process stop, upload, git state change or persistent environment change occurred.

### Catalog coverage and limits

| Runner | Units / entries | Marked incomplete |
|---|---:|---:|
| xUnit | 118 | 101 |
| unittest | 2 | 0 |
| cocotb module/configuration | 29 | 29 |
| standalone checker | 4 | 0 |
| Total | 153 | 130 |

Incomplete entries deliberately quote the numerical comparison and local comments rather than invent an independent oracle or a derivation for an inherited fixed bound. Planner review remains necessary for these entries. Stat.Within descriptions cite this repository's own helper and its default k=4 / estimator-standard-error definition; that is not a new bound. The two Python entries state exact/structural fixture comparisons; the checker entries state hash/membership/refusal/byte equality. Cocotb entries include the actual module methods, configuration identity and source excerpts but remain incomplete because a successful native run was not established here.

Trace selectors expand exact/uniquely resolved VV citations and deliberate class-wide citations into explicit methods. The catalog explains that these are coverage drafts. Ellipses, unnamed descriptions and I/C/M judgments are not converted into invented automated tests. Method groups preserve mixed gates within classes. Source fingerprints include local harnesses and Fact attributes, so changed shared fixture/statistical/gate code also flags review.

### RTL attempt and retained error

Icarus, vvp and cocotb were available, so collection attempted the first configuration. The native bridge emitted “Unable to open lib”; no results.xml was produced, and the unchanged runner subsequently reported the missing file. The collection retained its log/configuration, exit 1, and all 137 expected cases as missing. No RTL case is counted passed or skipped from this attempt. The same bootstrap problem was observed in the scratch attempts; no package installation or library-path investigation outside the permitted roots was made.

The positive XML adapter paths are tested with fixtures, and a real negative invocation exercises partial/error retention. Successful real cocotb XML normalization and Linux CI execution remain unverified. CI still omits C# vector export explicitly; vector comparisons are a listed gap. Background-shape numerical tests run locally but remain a listed default-CI gap because the Python job does not install numerical dependencies.

## Scratch collection, selection and size

Current collection: `%TEMP%/gcam-todo28/turn2/verified-bundle/`.
Portable retained export: `%TEMP%/gcam-todo28/turn2/verified-export/`.
Aggregate simulation: `%TEMP%/gcam-todo28/turn2/verified-aggregate/`.

Eleven invocation records share one source snapshot: five .NET, one Python suite, one RTL attempt and four standalone checkers. Summary is **857 passed, 29 not executed, zero failed test cases, 137 missing RTL cases**. The RTL invocation itself failed with exit 1 and is visible in the run table/diagnostics. Provenance is draft because collection/generation occurred on a dirty tree. Passing checker invocations do not erase that error or the missing coverage.

| Retained content | Bytes |
|---|---:|
| Five sanitized TRX | 1209452 |
| JSON records, runner JSON and manifests | 2338518 |
| Pinned catalog and generated Markdown outputs | 1378317 |
| Sanitized logs | 98473 |
| Total, 44 files | 5024760 |

TRX + JSON is **3,547,970 bytes** (about 3.38 MiB); the complete replay bundle is about 4.79 MiB. This is a measured scratch example, not a promised M1 size: a successful RTL run adds its XML/configuration records, and failure output sizes vary. Source manifests are intentionally repeated per invocation. Export retains only replay inputs/outputs: no DLLs, simulator binaries, scratch fixtures or screenshots are copied. Built DLLs remain hash-only as TD-2 requires. The attempted RTL compiled subject's before/after hashes agree; its native startup error remains visible independently.

Sanitization parses XML, unconditionally tokens identity attributes, substitutes path/identity text and emits canonical XML/LF. Original raw hashes and sanitized hashes are distinguished in records. The replay checks sanitized files for hash/size/privacy/idempotence and re-normalizes their cases before comparing JSON/report output. Raw-original hashes and historical binaries are collection claims, not independently re-opened artifacts. Generic account words such as runner/root remain usable as schema/prose words; identity slots and absolute paths are independently tokenized, with tests for that CI case.

The selection manifest pins records and selects each case's run explicitly. Repeated invocations retain earlier records and keep existing selections unchanged; the author edits the manifest to choose another run. Fault-injection/capture profiles cannot be selected as product verification. Across producers, source commit/content identity and overlapping subject hashes must agree. Aggregation of three copied producer bundles passed locally; that simulation does not establish actual Actions download behavior or cross-platform native runtime results.

Archived checks use the original generation provenance face, not today's replay tree status. The generation record binds its source manifest, generator identity, pinned catalog/selection and output hashes. The current schema-compatible reader regenerates bytes; any incompatible output change refuses replay. No human sign-off fields are generated.

## CI changes and unverified behavior

- Windows builds once, records tests per project with --no-build/TRX and explicit opt-ins off, checks expanded discovery, generates summary/report and uploads with always-run conditions. Collection returns nonzero when any test invocation fails; the original failed step is not converted to success by later report/upload steps.
- RTL writes to an explicit fresh output root, retains configuration/error/partial output and uploads its records/results/logs. No bench assertion changed.
- Python uses the unittest adapter for the existing provenance selection, retains the CI-only Git-fixture opt-in, and records calibration release/catalog/archive/self-test checker invocations. It does not silently count the numerical Python suite.
- The aggregate job downloads producer artifacts, verifies source/subject bindings, generates/checks the report and uploads with 90-day retention. A missing producer remains absent coverage; invalid/missing required input hashes refuse replay.
- The fixed desktop-case count was removed; summaries derive their counts from collected discovery/results. No old desktop/render run is merged into the current subject.

YAML/Python parsing and the three-bundle aggregate simulation passed. Hosted job execution, checkout line-ending behavior, artifact availability/download, actual Linux RTL results and Git-fixture execution are unverified locally. The native Windows bridge failure remains an environment limitation, not a reported RTL pass.

## Exact M1 commands (planner, after implementation commit)

Run from `<repo>` on the clean committed tree. Use a new scratch directory and a nonexistent milestone destination. Collect every family **before** writing generated repository outputs, so those writes do not make later collection draft. Leave the local Git-fixture switch off unless its mutation commands have separately been authorized.

```powershell
$env:PYTHONDONTWRITEBYTECODE='1'
$env:DOTNET_CLI_UI_LANGUAGE='en'
$env:GCAM_UI_TESTS='0'
$env:GCAM_RENDER_SNAPSHOTS='0'
$env:GCAM_EVIDENCE_TESTS='0'
$env:GCAM_CRRC_VECTORS=$null
$env:GCAM_PROVENANCE_GIT_TESTS='0'
$m1 = Join-Path $env:TEMP 'gcam-todo28/M1'
if (Test-Path $m1) { throw 'Choose a new scratch directory; preserve existing records.' }
if (Test-Path 'samples/testing/records/M1') { throw 'M1 destination must not exist.' }
dotnet build Gcam.sln -c Release
if ($LASTEXITCODE -ne 0) { throw 'Build failed.' }
python -B samples/testing/test_records.py check-catalog --discover
if ($LASTEXITCODE -ne 0) { throw 'Catalog check failed.' }
python -B samples/testing/test_records.py collect --family dotnet --no-build --out $m1
if ($LASTEXITCODE -ne 0) { throw 'Read retained .NET failure evidence before proceeding.' }
python -B samples/testing/test_records.py collect --family unittest --out $m1
if ($LASTEXITCODE -ne 0) { throw 'Read retained Python failure evidence before proceeding.' }
python -B samples/testing/test_records.py collect --family cocotb --out $m1
$m1RtlExit = $LASTEXITCODE
python -B samples/testing/test_records.py collect --family checker --checker calibration --out $m1
python -B samples/testing/test_records.py collect --family checker --checker test-catalog --out $m1
python -B samples/testing/test_records.py collect --family checker --checker archived-replay --out $m1
python -B samples/testing/test_records.py collect --family checker --checker test-record-self-test --out $m1
python -B samples/testing/test_records.py generate --bundle $m1 --output docs/VV.Tests.Results.md --studio docs/VV.Studio.md --export samples/testing/records/M1
python -B samples/testing/test_records.py check --bundle samples/testing/records/M1 --output docs/VV.Tests.Results.md --studio docs/VV.Studio.md
python -B samples/testing/test_records.py check --archives
Write-Output "RTL invocation exit retained in M1: $m1RtlExit"
```

The native bridge may still fail; retaining its error is required. No evidence is promoted to pass. To include numerical Python tests when dependencies are available, use `--include-background` on the **single** unittest collection rather than adding an implicit replacement run. A successful Linux RTL family can be attached later through a reviewed selection manifest with compatible source identity. Current collector normal mode intentionally keeps xUnit evidence/render/desktop off; such additional profiles remain separate, explicitly selected runs. Desktop requires the author's go.

The commands above do not commit. The planner reviews the retained record and commits M1 separately on the author's instruction. Missing tools/coverage can make that record draft/incomplete even when the source tree began clean.

## Proposed wording — planner applies, not applied here

| Destination | Proposed text |
|---|---|
| AGENTS.Conventions.Docs | “VV.Tests.md owns the human-reviewed test catalog and fenced JSON metadata. VV.Tests.Results.md is generated from one explicitly selected subject's retained run records. The TEST DISCOVERY, TEST INVENTORY and AUTOMATED TRACE RESULTS marker interiors are generator-owned; edit their inputs, not their numbers. Historical records and inspection/manual/compiler judgments remain outside generated automated results. Archived replay pins its catalog, selection and sanitized runner evidence; historical binaries are identified by hash and are not rebuilt as a substitute for the measured subject.” |
| AGENTS.md / README | “After `dotnet build Gcam.sln -c Release`, run `python -B samples/testing/test_records.py check-catalog --discover`. Collect normal Release results with `python -B samples/testing/test_records.py collect --family dotnet --no-build --out <scratch-bundle>`, then `generate --bundle <scratch-bundle>` and `check --bundle <scratch-bundle>`. Current inventory is in VV.Studio's generated block; milestone reports are in VV.Tests.Results. Opt-in suites remain not executed until explicitly run.” |
| AGENTS.Rationale | “Count case outcomes, not adapter summary skip counters: the measured adapter reports zero notExecuted despite skipped cases. Preserve unknown failure subtype: TRX does not prove assertion-versus-exception classification. Use final runner outcomes: exploratory desktop manifests can precede cleanup assertions. Separate provenance from acceptance/coverage: a formal failure or an incomplete draft is still a truthful record. Pin selection and source/overlapping subject hashes: repeats or different binaries must not silently replace evidence. Sanitize retained runner outputs deterministically: public evidence does not require local host/user/path identifiers. Byte-exact archived replay uses retained runner evidence and its catalog, rather than reconstructing historical binaries.” |
| VV.Studio around Current test inventory | “The generated block below identifies its subject, provenance and selected runner results. Historical inventories remain dated evidence; they are not current totals. Retained desktop evidence is in VV.Studio.History; absent desktop execution for the selected subject remains not executed.” |
| VV.Studio beside matrix | “The generated automated-results table reports selected test executions only. The matrix's inspection, compiler and manual judgments and historical desktop statuses remain separately reviewed evidence; passing automated cases do not alone establish a requirement-level sign-off.” |
| VV.Studio historical Plot performance gate | Replace its existing machine-name field with “local verification host”; preserve date, OS/build, SDK/runtime, DPI, timings and the historical verdict. This redaction is outside the implementer's permitted blocks and was not applied. |

## Write-command audit

The command log contains the complete here-string/script bodies. Every task-writing command category and invocation destination is listed here; read-only rg/Get-Content/git status/rev-parse/ls-files/diff and version queries are excluded.

| Command / tool | Written outputs |
|---|---|
| apply_patch, all calls this turn | Three tooling files; RTL runner infrastructure; CI; this report. No product/test assertion files. |
| Python stdin privacy-fixture source rewrite | Replaced literal synthetic negative-fixture paths in test_records.py with runtime-assembled paths; same sanitizer refusal test, no local identities introduced. Duration-regex spelling was subsequently corrected via apply_patch to avoid a false drive-path match in source scans. |
| `New-Item -ItemType Directory -Force -Path "$env:TEMP\gcam-todo28\turn2"` | Scratch directory. |
| `dotnet build Gcam.sln -c Release` and the initial/full corrected `dotnet test Gcam.sln -c Release` command processes | Normal bin/obj/allowed NuGet outputs; scratch build.log, full-test.log, full-test-corrected.log. Initial vector-switch failure retained, corrected invocation passed. |
| Here-string Set-Content for `turn2/draft_catalog.py` | Scratch drafting utility. Subsequent Python stdin rewrites fixed exact method-gate extraction, source-summary filtering and VAL mappings in that scratch utility. |
| `python -B <scratch>/turn2/draft_catalog.py`, repeated after source changes | Drafted/re-drafted docs/VV.Tests.md from current source/metadata. |
| `python -B samples/testing/test_records.py check-catalog --update-discovery`, repeated after drafting | Updated catalog discovery marker only. Plain check-catalog/--discover invocations are read-only. |
| `python -B samples/testing/test_records.py self-test --out <scratch>/turn2/<name>` | Retained copies under self-test-initial, self-test-initial2, self-test-expanded, self-test-final, self-test-callbacks, self-test-export, self-test-final-current, self-test-privacy, self-test-public-source, self-test-deliverable, self-test-partials, self-test-final-subjects. Initial sanitizer check failed before fixture output; subsequent checks passed after corrections. |
| Python stdin marker insertion | Scratch VV.Studio.before.bin and the two VV.Studio marker wrappers/placeholders. |
| `collect --family dotnet --no-build --out <scratch>/turn2/<bundle>` for proof, final-proof, proof-current, public-proof, deliverable-proof, final-bundle, verified-bundle | Per-project fresh result folders, sanitized TRX/discovery/runner logs, records and explicit selection. Earlier source versions retained as separate scratch bundles; final report uses verified-bundle. Shell redirections: collect-dotnet.log, collect-final-dotnet.log, collect-current-dotnet.log, collect-public-dotnet.log, collect-deliverable-dotnet.log, collect-final-bundle-dotnet.log, collect-verified-dotnet.log. |
| `collect --family unittest --include-background --out <scratch>/turn2/<bundle>` for proof, proof-current, deliverable-proof, verified-bundle | Structured suite JSON, sanitized log, source/subject record, selection and retained provenance fixtures under the named bundle; first shell log collect-unittest.log. Git fixture off. |
| `collect --family cocotb --out <scratch>/turn2/<bundle>` for proof, proof-current, deliverable-proof, verified-bundle | Fresh RTL build/configuration/diagnostic output, error records and selection. First shell log collect-cocotb.log. No results XML was invented. |
| `collect --family checker --checker <name> --out <scratch>/turn2/<bundle>` for proof, proof-current, deliverable-proof, verified-bundle; each calibration/test-catalog/test-record-self-test/archived-replay | Invocation JSON, sanitized logs, records/selection; self-test checker also creates retained defect fixtures under its invocation folder. Calibration/archive/catalog checks themselves do not rewrite their documents. |
| `generate --bundle <scratch>/turn2/proof --studio docs/VV.Studio.md` | Initial draft catalog snapshot/report/blocks/generation record in scratch; generated Studio block interiors. |
| `generate --bundle <scratch>/turn2/<bundle> --studio docs/VV.Studio.md --export <scratch>/turn2/<export>` for proof-current/export-proof, deliverable-proof/deliverable-export, verified-bundle/verified-export | Scratch report/pins, Studio block interiors and portable exports. No repository milestone or Results page. |
| Python stdin three-producer preparation/merge/generate/replay | Copied only retained evidence into scratch aggregate-proof and verified-aggregate producer directories, wrote selection manifests and generated aggregate outputs. Replay did not write. |
| Python stdin retained-size measurements | Wrote scratch deliverable-size.json and verified-size.json. |

All variables were process-scoped. No watcher loops; all started commands completed. No approval-requiring action was needed.
