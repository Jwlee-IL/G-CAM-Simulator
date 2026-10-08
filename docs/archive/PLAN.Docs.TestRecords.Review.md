# PLAN.Docs.TestRecords.Review — measured review of TODO-28 steps 2–3

Scope: review of the reference plan, test inventory, runner records, catalog and reporting design. No product or test changes. Only this review is a repository deliverable.

Status: review complete, 2026-10-08; decisions and implementation remain with the author and planner.

## Findings that change the plan

The three-layer process is suitable, but several premises are wrong. TRX does not provide a dependable assertion-versus-exception classification. Its summary skip counter is not dependable for this adapter. Desktop manifests are exploratory diagnostics and can disagree with the final xUnit result. Committing only JSON while requiring the generator to reopen every original binary and TRX is not a self-contained, reproducible design. A generated automated-test result must not overwrite inspection, manual or compiler evidence with a requirement-level “pass”. These corrections should precede implementation.

Review subject: HEAD `266b74072f30883b84c42bd949cd0ed61ecd767b`. At entry, `docs/AGENTS.Todo.md` and `docs/PLAN.Docs.TestRecords.md` were already modified; neither was edited by this turn. Measurements describe that working tree, not a clean committed release. No external project was investigated or used. No tolerance was selected, copied or changed.

Read first: repository `AGENTS.md`, [planning procedure](../AGENTS.Planning.md), [documentation conventions](../AGENTS.Conventions.Docs.md), and [reference plan](PLAN.Docs.TestRecords.md). Also checked [Studio V&V](../VV.Studio.md), [engine evidence](../VV.Gcam.Evidence.md), [calibration generator](../../samples/evidence/calibration_record.py), [CI](../../.github/workflows/ci.yml), test sources/project files, RTL runners/benches, and Python suites.

## Corrections to every “What exists” row

| Row | Verified result / correction |
|---|---|
| Test projects | All five exist under `tests/`, all xUnit 2.9.2 with VS adapter 2.8.2 and Test SDK 17.12.0. Engine/Core/services target net9.0; UI/render target net9.0-windows with WPF. Discovery finds 118 actual test classes, not one class per file: seven render source files extend the single `PlotViewRenderTests` class. |
| Opt-ins in the projects row | Only `GCAM_EVIDENCE_TESTS`, `GCAM_RENDER_SNAPSHOTS` and `GCAM_UI_TESTS` enable skipped xUnit tests. Evidence has seven attributed methods/cases in four classes. `GCAM_UI_BREAK_VERDICT` deliberately corrupts expectations, `GCAM_README_CAPTURE_ONLY` changes survey behavior, `GCAM_CRRC_VECTORS` optionally exports/consumes vectors, and `GCAM_UIA_RUN` is a child-process ownership marker. They are not four additional xUnit suites. `GCAM_RENDER_OUTPUT` redirects PNGs. Record these distinct roles. |
| Other test runs | Cocotb is a separate CI job. CI unittest discovery selects only `test_provenance*.py`: currently `test_provenance.py` (29 methods). `test_background_shape.py` has ten methods and is not selected by CI; it imports the numerical aggregator and requires NumPy/SciPy. Calibration `--release` is a deterministic checker, not a unittest suite and not a test-case count. |
| CI | Correct: normal .NET test step has console logging, no TRX or upload step. RTL and Python/checker outputs also are not uploaded. The summary's “14 desktop UI tests” is stale: current discovery has 20, consisting of 18 scenarios, gate and survey. |
| Hand-written inventory | The 703 passed / 22 skipped table is a dated measurement at `13387fb`, not a false historical measurement. It is stale as a current inventory: this run is 815 passed / 28 skipped. The surrounding “desktop pending since ambient default” prose conflicts with the later dated desktop evidence and matrix; preserve the historical record but replace its current-state role. |
| Requirement trace | The matrix exists and uses hand-written test references/statuses. Exact references still exist, but it also uses class-only references, abbreviated methods, ellipses and descriptions such as “Completed ViewModel test”. Those cannot all become machine selectors by parsing prose. Mixed T/I/C/M evidence is common. |
| Desktop manifests | Correct path is `tests/Gcam.Studio.UiTests/bin/Release/net9.0-windows/ui-runs/<runId>/manifest.json`, not `src/Gcam.Studio.UiTests`. `Harness/RunRecord.cs` explicitly labels them profile P1 / exploratory. Scenario final exit/sandbox assertions occur **after** `record.Save(result)` in `Harness/Scenario.cs`; a manifest marked passed is therefore not the final test verdict. The plot gate writes separate `ui-runs/plot-performance/performance.json`, overwriting that path across runs. |
| Engine evidence trace | EV entries cite existing classes/methods, sometimes “none specific” or Python studies. The class citations do not establish which cases cover an EV, and a green unit run does not regenerate numerical seed-ensemble evidence. See the checked-citation appendix. |
| Generator pattern | Correct: pinned SHA-256 inputs, generated marker blocks, byte-exact checks, release refuses draft prose. It is standard-library Python and does not query git. Also relevant is `samples/evidence/provenance.py`: separate source/executor bindings, untracked-content hashes, output sidecars and refusal tests. Reuse concepts, not its numerical-evidence scope unchanged (that scope omits `tests`). |

## Measurement and boundaries

Normal Release command, in a fresh command process:

```powershell
New-Item -ItemType Directory -Force -Path "$env:TEMP\gcam-todo28\trx" | Out-Null
$env:GCAM_UI_TESTS='0'
$env:GCAM_RENDER_SNAPSHOTS='0'
$env:GCAM_EVIDENCE_TESTS='0'
dotnet test Gcam.sln -c Release --logger 'trx;LogFilePrefix=normal' --results-directory "$env:TEMP\gcam-todo28\trx" *> "$env:TEMP\gcam-todo28\normal.log"
```

Used `LogFilePrefix` instead of a shared `LogFileName` to retain all five assembly results in one results directory without collisions. This is a deliberate command variation from the requested example. Future wrappers should use per-project result directories and a fixed `LogFileName`, or unique prefixes. Restore/build were included; test command completed, zero failed cases. Two existing analyzer warnings appeared: xUnit2012 in `WaveformServiceTests.cs:118`, xUnit2000 in `ImagingServiceTests.cs:187`.

| Project | Test classes | Passed | Skipped | Expanded cases | Target |
|---|---:|---:|---:|---:|---|
| Gcam.Tests | 72 | 481 | 0 | 481 | net9.0 |
| Gcam.Studio.Tests | 23 | 211 | 0 | 211 | net9.0 |
| Gcam.Studio.Services.Tests | 14 | 108 | 7 | 115 | net9.0 |
| Gcam.Studio.UiTests | 8 | 15 | 20 | 35 | net9.0-windows |
| Gcam.Studio.RenderTests | 1 | 0 | 1 | 1 | net9.0-windows |
| Total | 118 | 815 | 28 | 843 | zero failures |

Discovery only, with all three opt-ins set to `1`:

```powershell
$env:GCAM_UI_TESTS='1'
$env:GCAM_RENDER_SNAPSHOTS='1'
$env:GCAM_EVIDENCE_TESTS='1'
dotnet test Gcam.sln -c Release --no-build --list-tests *> "$env:TEMP\gcam-todo28\discovery-optin.log"
```

This does not execute test bodies. All 843 discovered display names match the normal TRX names exactly, including expanded theory arguments. Total class/case inventories per project are unchanged under opt-in. Evidence enables seven service cases; render enables one render case; UI enables 20 UI cases while its 15 headless cases remain available. No enabled-suite outcome is claimed. Long numerical evidence was not run: the full sweep alone transports 42 million histories, and the other evidence methods have additional simulation budgets. Render was also discovery-only. No desktop test, GUI, cocotb or Python suite was executed. Python and cocotb counts below are static inventories, not this turn's outcomes.

### Opt-in case inventory

| Switch | Class | Methods / enabled cases |
|---|---|---|
| GCAM_EVIDENCE_TESTS | DetectorGapEvidenceTests | `RateRatio_MatchesGeometricArea_WithinMeasuredSeedSpread` (1) |
| GCAM_EVIDENCE_TESTS | ImagingServiceTests | `Precision_TwentySeeds_MeasuresMixedIsotopesAndCoOnlyControl` (1) |
| GCAM_EVIDENCE_TESTS | OpticsProjectionTests | `Sampling_PositionSweepAtConstantDetectorSize_FinerPixelsReduceLocalizationError`; `Sampling_SmallBudgetSeedSpread_ReportsRegressionMargin` (2) |
| GCAM_EVIDENCE_TESTS | WaveformServiceTests | `MaximumWindow_ReportsWorkerCostAndBoundedPlotSamples`; `RetainedMonteCarlo_IsolatedPulseReadoutsAcrossParts`; `MixedFieldCalibration_UsesAcquiredChainAndInvalidatesRatios` (3) |
| GCAM_RENDER_SNAPSHOTS | PlotViewRenderTests | `Spectrum_BothThemesFullAndZoom_RenderWithoutWindow` (1; many assertions and PNGs within one case) |
| GCAM_UI_TESTS | PilotTests | `Pilot_SimulateThenMeasureDistance_AddsRowWithExpectedLength` (1) |
| GCAM_UI_TESTS | ScenarioTests | `StopContinueReset_KeepsAccumulatesAndDiscards`; `Roi_OnFloodMap_CountsWholePixelsByCentre`; `Angle_EscAbandonsDraft_DeleteRemovesSelected`; `MoveSource_ResetEditStart_PutsPeakOnTheSource`; `Readout_AndOneCellRoi_MatchAbsolutePositionAndValue`; `ThemeToggle_RelabelsAndSwitchesBack` (6) |
| GCAM_UI_TESTS | WorkspaceScenarioTests | `Spectrum_BandCountAndWindowChange_MatchRetainedHistogram`; `Waveform_SelectedEventListAndMarkers_MatchArrivalWindow`; `Detector_FaceBeforeStart_LockedUntilReset`; `Imaging_ChannelAndStrip_MatchRetainedFloodAndFoundPeaks`; `Imaging_RefocusAndSweep_MatchProjectionAndHalfMaxInterval` (5) |
| GCAM_UI_TESTS | MlemScenarioTests | `Mlem_SelectedDuringAcquisition_NonNegativeWithUnitNoteAndAdvancingStatus`; `MethodSwitch_KeepsMeasurementsOnTheSameGrid`; `MlemSelection_SurvivesStopContinueAndReset_WithoutAStaleImage`; `Mlem_WithStrip_ReportsNetCountsAndFindsCsOnItsSide`; `FocusSweep_UnderMlem_IsTheCrossCorrelationSweep`; `Mlem_FocalPlane1500_FlagsTheUnmeasuredIterationCount` (6) |
| GCAM_UI_TESTS | PlotViewTests | `TenMillionSamples_ZoomAndResizeRedraw_Within16Milliseconds` (1) |
| GCAM_UI_TESTS | PolishSurveyTests | `AllWorkspaces_BothThemesAndWindowSizes_CapturesPolishSurvey` (1) |

### TRX contents

SDK 9.0.311; executing runtime reported by adapter is .NET 9.0.13; VSTest 17.14.1 x64. Assembly target, adapter identity and runtime identity are different fields and should remain separate.

| TRX under `%TEMP%/gcam-todo28/trx/` | Project | Bytes | Run start–finish (+09:00, 2026-10-08) | Wall seconds | Sum of case seconds |
|---|---|---:|---|---:|---:|
| normal_net9.0_20261008065633.trx | Studio Core | 297589 | 06:56:32.658–06:56:33.652 | 0.993 | 1.576 |
| normal_net9.0_20261008065638.trx | Render | 3875 | 06:56:37.418–06:56:38.074 | 0.656 | 0.001 |
| normal_net9.0_20261008065639.trx | UI | 65442 | 06:56:37.424–06:56:38.097 | 0.673 | 0.067 |
| normal_net9.0_20261008065651.trx | Services | 188864 | 06:56:33.183–06:56:51.539 | 18.356 | 61.457 |
| normal_net9.0_20261008065655.trx | Engine | 642948 | 06:56:32.681–06:56:55.664 | 22.983 | 263.569 |
| Total bytes | | 1198718 | | | |

Case durations span 0.0000007–17.0915928 s among passes; every skipped result reports 0.001 s. Parallel execution makes sums larger than assembly wall time; do not present their sum as elapsed run time. TRX has run UUID/name/runUser, creation/queuing/start/finish timestamps with offsets, deployment root, case test/execution IDs, computerName, duration, start/end timestamps, outcome, class/method/codeBase/storage, adapter URI, output and run warnings. It does not bind source commit, binary hashes or switch values. Paths/user/machine names are present in raw TRX; public normalized records must replace local paths with `<repo>` / `%TEMP%`, omit user identities, and use a non-personal runner label. Keep original hashes before sanitization and identify transformed copies separately.

All five ResultSummary outcomes are `Completed`, including the all-skipped render assembly. All failure/error/timeout/abort counters are zero. Crucially, all summary `notExecuted` counters are also zero, although 28 case outcomes are `NotExecuted`; count cases, not this counter. Skip reason is `Output/ErrorInfo/Message`, not a failure. Case results and complete discovery must be cross-checked against summary total/executed/passed/failed where those agree; record the adapter counter discrepancy explicitly.

No failed case occurred here, so failure encoding was not empirically exercised. TRX's case outcome/message/stack does not supply a reliable typed assertion-vs-unhandled-exception flag; aggregate `error` is not a per-case discriminator. Do not infer “mismatch” from every Failed case or “error” from every ErrorInfo element (skips also have ErrorInfo). Use failed/unknown unless a runner-specific structured exception/assertion field proves the subtype. Seeded adapter fixtures in the implementation must cover both assertion and ordinary exception. No production assertion changes are needed.

### Current skip messages

| Source | Message | Names switch? |
|---|---|---|
| EvidenceFactAttribute | `Set GCAM_EVIDENCE_TESTS=1 to run numerical evidence measurements.` | yes; seven normal skips |
| RenderSnapshotFactAttribute | `Set GCAM_RENDER_SNAPSHOTS=1 to render offscreen PNGs on an STA thread.` | yes; one |
| DesktopFactAttribute | `desktop UI test: set GCAM_UI_TESTS=1 to run (takes over mouse and focus)` | yes; twenty |
| Python ProvenanceTests decorator | `isolated Git mutations require explicit local approval; enabled in CI` | no; switch is GCAM_PROVENANCE_GIT_TESTS |
| Cocotb trapezoid/BLR missing stream | `no event_stream.txt — run ` plus the command `montecarlo eventstream <cfg>` and ` first` | no opt-in; missing fixture |
| Cocotb CR-RC vector decorator | `skip=not os.environ.get("GCAM_CRRC_VECTORS")` | no supplied custom reason; emitted message is not verified |

Both trapezoid MC-stream cases and the BLR case raise `cocotb.result.SkipTest` when the stream is absent. Compatibility of that exception reference with the CI-pinned cocotb version was not executed here; record an execution error if the missing-fixture path itself fails. The current repository supplies `rtl/event_stream.txt`. For xUnit, keep existing messages. For Python, propose appending `set GCAM_PROVENANCE_GIT_TESTS=1` while preserving the approval explanation. Cocotb adapters should assign missing-fixture / optional-vectors-off from the known case and captured environment, preserving raw XML rather than guessing from prose.

## What other runners can contribute

| Runner | Existing outputs and identification | Required adapter work |
|---|---|---|
| rtl/run_cocotb.py | Fresh UUID scratch root; per-configuration build artifacts including sim.vvp and results.xml; prints passed/failed/skipped counts and paths; parses testcase/failure/error/skipped; exits nonzero on subprocess error, missing/empty XML or failed case. Seed 20261002, Icarus top/source/parameters and optional vector directory are set in code. No consolidated JSON or hashes. Matrix: 13 fixtures × F={0,12} × five CR-RC cases + two tops × three trapezoid cases + one BLR = 137 cases. Without vectors 26 CR-RC cases skip, hence the retained 137/137 with vectors is not CI's expected all-pass count. No new RTL run was made. | Add explicit output-root option before using the TODO-28 scratch root. Capture command/exit/versions/source/configuration/seed, every XML and simulation artifact hash, vector/stream input hashes. Case identity includes configuration, top and module, not just function name. Preserve partial results when the runner fails early; unvisited configurations are missing, not passed. Actual XML time/unit attributes remain to be validated on a supplied run; do not claim this turn measured them. |
| Python unittest | `test_provenance.py`: one class, 29 methods; one gated Git-fixture method. Scratch root can be redirected by GCAM_PROVENANCE_TEST_ROOT; fixtures retained, no cleanup. `test_background_shape.py`: one class, ten methods; no opt-in decorators, numerical dependencies. Standard TextTestRunner emits stderr progress, failure/error tracebacks, skipped reasons, total and wall time; default invocation emits no per-case JSON/XML, hashes, machine or commit. | Use stdlib TestResult/TextTestRunner subclass to capture addSuccess/addFailure/addError/addSkip and monotonic per-case timing, discovery identities and exit result. Failure vs error is structured here. Use `-B`, direct scratch override, preserve logs. CI currently executes only provenance (with Git-fixture opt-in); adding background-shape needs a separate numerical-dependency CI decision. No Git-fixture mutation is authorized locally in this review. |
| Desktop scenarios | Manifest: schema/runId/scenario/profile/purpose; OS/culture/DPI-awareness; dirty file count; build exe/productVersion/mtime, process/start/sandbox/window; per-scenario oracle/measurements; timed steps, result, cleanup fields. Failures can add exception text/tree.txt/window.png/diagnostic error. These are diagnostics, not a final-run authority. | Link/hash bundles, preserve P1 label, and use xUnit/TRX outcome. Flag disagreement between manifest and final case, never upgrade pass. Mark deliberate break-verdict runs as fault-injection validation, exclude from product-verification aggregation. Resolve paths through output records, not console scraping alone. |
| Plot desktop gate | performance.json: machine, OS, processors, date, samples, DPI, CPU redraw arrays/event-to-render arrays/metric description; emitted before all assertions complete. Fixed path is overwritten. | Copy to unique run bundle immediately and hash; final result comes from TRX. Do not call CPU redraw timing presentation latency. |
| Calibration --release | Deterministic validation/check exit and diagnostics, pinned inputs, no per-test run record. | Record a checker invocation with tool/input hashes and exit verdict; keep it outside xUnit case totals. |

## Decisions TR-1 … TR-8

| ID | Recommendation after verification |
|---|---|
| TR-1 | Adopt one catalog entry per discovered test class, in `docs/VV.Tests.md`, with structured method-group details for classes with mixed gates/oracles. Define other runner units explicitly: unittest class; cocotb module plus configuration matrix (no artificial Python class); standalone checker. Use exact assembly-qualified class/method selectors for traceability. |
| TR-2 | Adopt stdlib Python orchestration/normalization, separate records per actual invocation. Separate product/test source, executing runner, report generator and built subject identities; commit+dirty alone is insufficient. Hash source content read-only, including relevant untracked files, rather than writing a Git tree. Capture binaries before and after running; reject changes. A binary hash captured afterward alone cannot prove what ran. |
| TR-3 | Outcomes passed / failed / not_executed; separate failureKind assertion_mismatch / execution_error / unknown. Do not require a fabricated failure subtype from TRX. Run-level infrastructure errors and missing cases must remain visible. Preserve original outcomes/reasons. |
| TR-4 | Adopt report with explicit coverage dimensions and incomplete states. Requirement view shows automated results alongside retained I/C/M evidence, not a generated requirement sign-off. Run completeness, report provenance and product acceptance are separate. |
| TR-5 | Recommend option (b): every CI run gets artifact bundle; selected milestone report and reduced normalized records committed. Commit `docs/VV.Tests.Results.md` and `samples/testing/records/<milestone>/`; catalog `docs/VV.Tests.md`. Raw TRX/binaries/PNGs belong to downloadable bundles, not git. Explicitly distinguish archived-record replay from full artifact verification; details below. |
| TR-6 | Adopt generated current inventory and automated trace-result blocks. Keep historical counts/statuses, measurements and manually judged evidence untouched. Replace conflicting “current” prose with a link to the pinned report and its subject/date. |
| TR-7 | Adopt schema/identity/hash/coverage checks plus deterministic defect fixtures, including adapter quirks and privacy checks. Missing runs generate visible incomplete coverage; they do not themselves masquerade as pass or necessarily prevent a draft report. Missing required input/hash mismatch blocks generation. |
| TR-8 | Planner applies documentation changes after decisions. Propose conventions for VV.Tests and VV.Tests.Results, marker ownership and archived snapshot replay; AGENTS/README command links without duplicated counts; rationale for skipped/missing/error handling, evidence authority and source identity. No edits to those files in this turn. |

### Catalog and discovery contract

Each class entry states: full class/assembly and source path; purpose and verified behavior; test level; inputs/fixtures/seeds/budget; independent oracle and limitations (including shared implementation when independence is absent); exact comparison and tolerance derivation, or “exact”, “structural” or “measurement only”; method groups for differing tolerances/opt-ins; requirement/evidence selectors (SR/EV/VAL); gates/dependencies; output artifacts and normal reproduction command. Reasons for a measured tolerance cite that test's own derivation. No new bounds are chosen. Source checks must establish each drafted statement.

Use human prose plus a fenced JSON metadata block per class, parsed with the standard library. Keep machine discovery as a separate generated block in the same document. Metadata supplies exact IDs and selectors, not duplicated case totals. Documentation review verifies oracle prose; automation verifies structure and membership. VV catalog does not link AGENTS, PLAN or Findings themes; use EV IDs, code and VV references.

Initial discovery can run one project at a time with `--list-tests --no-build` and parse only fully qualified test lines. This turn established equality with all 843 TRX names. Do not parse localized console summaries. Store discovered class/method/display identities, flags and assembly hash, and require exactly one catalog entry per class, no extras, unique IDs, nonempty required fields and selectors resolving to methods. Record manifest hashes for source files/fixtures; changed hashes flag prose for review rather than claiming prose automatically remains correct. Theory display arguments are runner-owned and can change; use method identity plus adapter case identity/display name, not splitting names at commas or parentheses.

For production, validate theory/inherited/generic/custom-attribute cases in a discovery self-test; use a small VSTest structured discovery helper if console output cannot preserve stable identities. A source-file regex alone is not sufficient: partial classes, helper attributes and theory expansion already matter here. Class discovery success must not be inferred from a failed build, empty output or an old DLL. Discovery/build/preflight success and subject binding are prerequisites. Gates within a class must be represented per method, not as a whole-class switch.

Make requirement selectors explicit machine data in catalog metadata (or a reviewed companion JSON trace map if the author prefers). Expand abbreviated matrix names. Class-wide selectors are allowed only when deliberately declared; new methods then alter coverage visibly. Inspection/compiler/manual records remain separate reviewed sources. The checker verifies current automated citations against discovery and SR/EV IDs against their owning VV documents; dated historical citations are not required to exist in the present tree. See full citation appendix: no missing exact or uniquely expanded names found. Ellipses and unnamed descriptive references need explicit mapping during implementation.

### Run-record schema and aggregation

Recommended schema version 1, JSON with deterministic UTF-8/LF serialization:

| Group | Fields |
|---|---|
| Identity | schemaVersion, runId, runnerKind, startedUtc, finishedUtc, command as argument array with tokenized paths, working-directory token, exitCode, discovery-id/hash, run completeness |
| Source | subject-source commit, dirty/unknown, sorted scope paths and SHA-256 content manifest (includes tests/build settings/data); runner-source identity separately; no git write-tree, no untracked-file omission |
| Subject | target/configuration; per test and product binary path/hash; relevant deps/runtimeconfig/fixtures and execution dependencies; content-set ID; pre/post hash equality. Capture dynamic inputs such as vector/stream files. |
| Tool | SDK, actual runtime, VSTest, xUnit/adapter versions; for Python interpreter and dependency versions; cocotb/Icarus/vvp/build parameters; collector version/hash; report generator identified separately in generation record |
| Environment | OS/version/architecture, culture/timezone, CPU information when relevant, DPI only when captured; allowlisted GCAM switches with unset distinct from 0; non-personal runner label. Never dump all environment variables. |
| Case | assembly/class/method, adapter identity and display name/arguments, raw outcome, normalized verdict, failureKind and classification basis, start/end/duration/unit, skipReasonCode/text/switch, diagnostics/artifact references |
| Run diagnostics | process/build/discovery errors, missing expected cases, raw runner warnings/counter inconsistencies, elapsed run time; no pseudo-test that silently changes case totals |
| Artifacts | relative/tokenized path, kind, bytes, SHA-256, retained location; raw and normalized hashes distinguished; portable bundle manifest |

Skip codes: `opt_in_off`, `optional_vectors_off`, `missing_fixture`, `desktop_unavailable`, `platform_unsupported`, `runner_skip_unknown`. Only assign desktop_unavailable when the runner actually establishes it; switch-off is not proof no desktop exists. Missing expected execution uses `missing_run` or `run_aborted` coverage reason, not a fabricated runner skip. `not_implemented` belongs to an explicit catalog requirement/gap, never inferred from absence. Unknown skip text retains `runner_skip_unknown` and flags the need for review.

Aggregation: count passed, failed, skipped and missing independently. A class with passed+skipped cases is partial; all skipped is not_executed. Worst-of gives failed precedence but must not erase incomplete coverage. Do not merge runs with different **overlapping** subject binary/input hashes. Different runner families have different subject sets: compatible records share the same source snapshot and matching hashes wherever their subjects overlap; they need not contain identical complete DLL lists. Associate every family with the same milestone subject source, rather than demanding Python and RTL identify a .NET-only subject.

Recommend an explicit run-selection manifest instead of silent “latest executing wins”. For each case/profile select a run ID; show repeats and prior failures. Normal skip plus later opt-in pass can supply execution coverage for that case if its subject matches, while both original records remain visible. Different evidence budgets or break-verdict mode are different profiles; do not substitute them. Never mix historical desktop passes from older binaries into the current subject.

Formal/draft is provenance, not pass/fail or regulatory approval. A clean, fully identified failing run can be a formal failure record. This dirty review measurement is draft. Dirty/unknown identities, missing mandatory tool/environment information or incomplete artifact verification give explicit draft/verification limitations. No human approval fields are filled by automation. Generator records input/catalog/template/script hashes, own commit+dirty, selected runs, output hashes and reasons; compute output hash outside the output itself to avoid self-reference.

### Artifact retention and measured size

Five raw TRX files total 1,198,718 bytes (about 1.14 MiB). A scratch JSON extraction of 843 cases with names, class/method, outcome, duration, skip messages and five assembly summaries is 218,826 bytes. A **draft size prototype**, adding tool/switch/source metadata, 40 built Gcam DLL path hashes and five raw TRX hashes, is 225,242 bytes compact / 268,800 bytes pretty. It is not a production run record: complete environment, source-content snapshot, dependency closure, case timestamps and pre-run hashes are missing. Budget approximately 0.3–0.5 MiB for a complete normal milestone JSON, plus catalog/report; actual implementation must measure again. Failures/logs and desktop PNG bundles are unbounded relative to that estimate and remain artifacts. The 40 paths include repeated DLL copies; do not confuse path count with distinct assemblies.

The plan's JSON-only proposal conflicts with requiring all original artifacts locally on every `--check`. Recommend two explicit modes:

1. Full verification: artifact bundle is present; recompute binary/raw-output hashes, re-normalize raw results, verify normalized record/report. A mismatch or absent required artifact refuses generation.
2. Archived replay: committed immutable normalized records with pinned hashes reproduce report bytes without requiring rebuilt historical DLLs. Clearly label artifact verification as “verified at collection; bundle not reverified here”. This does not prove missing raw artifacts or recompute their hashes. CI artifact URL/run ID/retention and optional durable milestone bundle location are recorded; artifact expiry is explicit. Do not pretend a content hash alone is independent evidence of a runner result.

If the author requires every archived check to independently revalidate raw evidence, choose a durable bundle or commit sanitized TRX as well. Rebuilding is not a replacement for retrieving the exact measured assemblies. Do not check a historical milestone by comparing its inventory to HEAD; current catalog freshness and archived snapshot replay are separate checks.

### Report layout and generated blocks

`docs/VV.Tests.Results.md`: At a glance; subject and report provenance (formal/draft, reasons, artifact-verification mode); invocation table (dates/source/tool/environment/switches/exit/completeness); per-project counts and wall times; per-class passed/failed/skipped/missing totals; skipped and absent suites with reasons; automated SR/EV/VAL trace result; failures/infrastructure diagnostics; selection/repeat history; artifact/generation manifest references and reproduction commands. Preserve the distinction between measured numerical evidence and tests protecting it.

Proposed generated marker pairs in `VV.Studio.md`:

```text
<!-- BEGIN GENERATED: TEST INVENTORY -->
<!-- END GENERATED: TEST INVENTORY -->
<!-- BEGIN GENERATED: AUTOMATED TRACE RESULTS -->
<!-- END GENERATED: AUTOMATED TRACE RESULTS -->
```

Replace the Current test inventory table and its current subject/date/commands inside the first block. Replace the automated part of matrix statuses via a generated companion table, or add an “Automated run” column while leaving manually reviewed I/C/M verdicts separate. Do not blindly replace the whole Status column. The 47-row coverage baseline is explicitly historical; leave it historical. Dated History records, desktop measurement tables and spectrum/physics measured values stay untouched: TRX does not provide structured replacement numbers for them. Replace stale current desktop-count prose/CI summary with discovery-derived text, preserving older dated counts. `VV.Gcam.Evidence.md` test lists can link catalog class anchors and have machine-checked exact selectors, but numerical EV results and “Used by” assertions remain evidence-generator/human responsibilities.

### Generator, checker, self-test and CI

Propose `samples/testing/test_records.py` (stdlib) with `collect`, `generate`, `check`, `check-catalog`, `self-test`, plus `unittest_adapter.py` and runner-specific import adapters. Collection owns command completion and captures failures; generation never reruns tests implicitly. An approved initial command can build/test/collect/generate a scratch report; promotion to a committed milestone is explicit. Deterministic rendering excludes volatile wall-clock generation time (use recorded run time or an explicit supplied generation timestamp).

Checks: duplicate/missing/stale classes and method selectors; changed fixture/source fingerprints; SR/EV/VAL references; record schema/identities; bad or missing hashes; pre/post subject mismatch; source conflicts; duplicate case IDs; truncated/empty TRX; runner error with incomplete discovery; nonfinite/negative durations; malformed skip codes; unknown failure classification; case-summary counter quirks; missing opt-in coverage; marker uniqueness; LF/no local paths; byte-exact generated outputs; no fabricated sign-off. Missing coverage appears in reports and is enforced only according to the selected profile; normal CI must not require desktop execution to pass.

Self-tests use retained copies under a specified scratch root, seed one defect at a time, and assert the precise refusal/diagnostic. Cover each hash mismatch, missing artifact, source/binary mixed runs, untracked source change, malformed identity, missing/stale/duplicate catalog entry, bad trace selector, theory rows, assertion vs exception vs unknown, all-skipped Completed TRX, zero notExecuted counter, incomplete/aborted runner, deliberate failure with nonzero command exit, tampered normalized record/report, duplicate markers, path leak, merge selection and archived-vs-full mode. A fault-injection failure must fail for the seeded reason, not an unrelated file/parse error. This needs infrastructure fixtures, not changes to product-test tolerances.

CI proposals:

1. Windows: build once; collect per-project TRX with `--no-build`, explicitly disable the three opt-ins; generate discovery inventory and report even on a failed test invocation. Preserve original test exit status; upload TRX/logs/records/report/generation manifest with `if: always()`. Derive skip-summary text, remove the literal 14.
2. Linux RTL: expose an output-root argument, wrap/import results including partial/error states; capture simulator/Python/cocotb versions and inputs. Upload all retained XML/build logs/records via always-run steps. CI currently omits C# vector export; do not relabel skipped vector checks as the retained 137-case vector evidence. Adding cross-platform vectors is a separate explicit scope choice.
3. Python/checker job: stdlib adapter for existing provenance selection, opt-in Git-fixture enabled only in CI as today; checker invocation recorded separately. Add test-record checker/self-tests and archived replay. Decide separately whether to install pinned numerical dependencies and run background-shape tests.
4. Aggregate downloaded family records after producer jobs, with always-run behavior and explicit missing-job records. Bind source/subject before combining. Publish generated report as artifact/job summary. Do not merge old desktop/render evidence. A separate author-run desktop/render/evidence workflow can attach compatible records later.
5. CI default checks current catalog freshness plus committed snapshot replay, not equality of today's stochastic timings with a previous milestone. Any formal/full artifact check requires its pinned bundle to be available. Define artifact retention explicitly (recommend 90 days for routine runs; milestone evidence needs a durable location or known expiry).

No CI or script implementation was made this turn.

## Author decisions requested for turn 2

| Question | Options | Recommendation |
|---|---|---|
| TR-5: report home/retention | (a) CI artifacts only; (b) artifacts plus pinned milestone report/JSON; (c) committed report only | (b), `docs/VV.Tests.Results.md` plus `samples/testing/records/<milestone>/`; keeps reviewable milestones without committing every run. |
| Raw milestone evidence | durable downloadable raw/binary bundle; commit sanitized TRX; normalized JSON with explicit archived-replay limit | JSON plus durable bundle when full independent revalidation is needed; allow archived replay when bundle is absent, label the limitation. No undocumented “full check” from JSON alone. |
| Catalog home | `docs/VV.Tests.md` with fenced JSON metadata per class; prose document plus separate metadata JSON | Single VV document with per-class metadata; fewer independently drifting sources. Author/planner reviews every oracle/tolerance statement. |
| Failure subtypes | add structured runner instrumentation; permit failed/unknown for TRX | Permit unknown now; structured failure/error classification for unittest/XML where available. Never rely on message-language heuristics as fact. |
| Requirement reporting | replace whole matrix statuses; generated automated column/companion table retaining manual evidence | Companion automated result block; keep T/I/C/M distinctions and historical evidence. |
| Run selection | implicit latest execution; explicit profile/run selection manifest | Explicit selection; failures/repeats remain visible and fault-injection cannot replace product results. |
| Other runner scope | catalog only xUnit now; catalog/normalize xUnit, unittest, cocotb and checker now | Catalog all runner units now, implement adapters for existing CI families; background-shape numerical CI and RTL C# vector CI are separate decisions. |
| Milestone completeness | require every opt-in; formal provenance with explicit skipped/missing coverage | Separate provenance from coverage; define desired milestone profile. Desktop remains author-controlled. |

These are design decisions, not requests to execute destructive commands. Implementation should wait for the adopted plan in the next turn.

## Write-command audit

Every command/tool that wrote task outputs:

| Operation | Writes |
|---|---|
| `New-Item -ItemType Directory -Force -Path "$env:TEMP\gcam-todo28\trx"` | Scratch/result directory. |
| Normal `dotnet test Gcam.sln -c Release ...` shown above | Restore/build outputs under repo bin/obj and permitted NuGet cache; five scratch TRX files; `normal.log`. No vector export switch was set. |
| Opt-in `dotnet test ... --no-build --list-tests` shown above | `discovery-optin.log`; no test bodies executed. |
| Here-string `Set-Content -Encoding utf8 "$env:TEMP\gcam-todo28\inspect.py"`; `python -B ...\inspect.py` | Scratch analysis script, inventory.md, trace.md, measurement.json. |
| Here-string `Set-Content ...\trace_check.py`; `python -B ...\trace_check.py` | Scratch citation/discovery analysis script and corrected trace.md. The initial scanner misassigned shorthand context; final scanner resolves unique current methods and exact class names. |
| Here-string `Set-Content ...\size_check.py`; `python -B ...\size_check.py` | Scratch script and record-size-prototype.json. First invocation wrote the prototype but stopped reading a UTF-8 source with the locale default. |
| `[System.IO.File]::WriteAllText` on size_check.py; `python -B ...\size_check.py` | Corrected scratch script to read explicit UTF-8, reran successfully and rewrote prototype. |
| `apply_patch` creating this review | Only repository deliverable. |
| Final `python -B` append/validation command | Appends measured inventory/citation appendices to this review, writes LF bytes, checks no local absolute user paths. |

Read-only commands included Get-Content, rg/rg --files, Get-ChildItem, git status --short, git rev-parse HEAD and dotnet --version. No git state changes, deletions, installs, process stops, uploads or persistent environment changes. Both started commands completed; no running command is left. No approval-requiring command was needed.

## Measured class inventory

Counts are expanded TRX cases, including theory rows. P = passed; S = not executed. Opt-in counts below are discovery, not pass predictions.

### Gcam.Studio.RenderTests

| Class | P | S | Total |
|---|---:|---:|---:|
| `PlotViewRenderTests` | 0 | 1 | 1 |

### Gcam.Studio.Services.Tests

| Class | P | S | Total |
|---|---:|---:|---:|
| `AcquisitionContinuationTests` | 6 | 0 | 6 |
| `AcquisitionServiceTests` | 9 | 0 | 9 |
| `AmbientAcquisitionTests` | 4 | 0 | 4 |
| `AmbientPresetTests` | 4 | 0 | 4 |
| `DetectorGapEvidenceTests` | 0 | 1 | 1 |
| `DetectorRealismTests` | 6 | 0 | 6 |
| `FocusSweepServiceTests` | 7 | 0 | 7 |
| `ImagingServiceTests` | 10 | 1 | 11 |
| `MeasurementRandomnessTests` | 1 | 0 | 1 |
| `OpticsProjectionTests` | 12 | 2 | 14 |
| `SpectrumServiceTests` | 10 | 0 | 10 |
| `StripCountEstimatorTests` | 6 | 0 | 6 |
| `StripProjectionTests` | 4 | 0 | 4 |
| `WaveformServiceTests` | 29 | 3 | 32 |

### Gcam.Studio.Tests

| Class | P | S | Total |
|---|---:|---:|---:|
| `AcquisitionViewModelTests` | 8 | 0 | 8 |
| `AmbientViewModelTests` | 25 | 0 | 25 |
| `DetectorFaceTests` | 7 | 0 | 7 |
| `DetectorWorkspaceTests` | 7 | 0 | 7 |
| `FocusSweepMathTests` | 7 | 0 | 7 |
| `FocusSweepViewModelTests` | 9 | 0 | 9 |
| `HeatmapViewportTests` | 11 | 0 | 11 |
| `HistogramPlotTests` | 10 | 0 | 10 |
| `ImagingWorkspaceTests` | 10 | 0 | 10 |
| `MainViewModelTests` | 11 | 0 | 11 |
| `MeasurementMathTests` | 5 | 0 | 5 |
| `MeasurementsViewModelTests` | 7 | 0 | 7 |
| `MinMaxPyramidTests` | 19 | 0 | 19 |
| `NiceTicksTests` | 5 | 0 | 5 |
| `OpticsPolicyTests` | 12 | 0 | 12 |
| `OpticsViewModelTests` | 4 | 0 | 4 |
| `OverlayLabelLayoutTests` | 6 | 0 | 6 |
| `PlotSeriesTests` | 1 | 0 | 1 |
| `PlotViewportTests` | 5 | 0 | 5 |
| `SpectrumBandTests` | 1 | 0 | 1 |
| `ThemeContrastTests` | 8 | 0 | 8 |
| `TickFormatterTests` | 18 | 0 | 18 |
| `WaveformWorkspaceTests` | 15 | 0 | 15 |

### Gcam.Studio.UiTests

| Class | P | S | Total |
|---|---:|---:|---:|
| `FloodOracleTests` | 12 | 0 | 12 |
| `MlemScenarioTests` | 0 | 6 | 6 |
| `PilotTests` | 0 | 1 | 1 |
| `PlotViewTests` | 0 | 1 | 1 |
| `PolishSurveyTests` | 0 | 1 | 1 |
| `ScenarioTests` | 0 | 6 | 6 |
| `WorkspaceOracleTests` | 3 | 0 | 3 |
| `WorkspaceScenarioTests` | 0 | 5 | 5 |

### Gcam.Tests

| Class | P | S | Total |
|---|---:|---:|---:|
| `AlignmentTests` | 4 | 0 | 4 |
| `AmbientAngularSamplerTests` | 7 | 0 | 7 |
| `AmbientEvidenceTests` | 6 | 0 | 6 |
| `AmbientFieldTests` | 21 | 0 | 21 |
| `AmbientGateStudyTests` | 3 | 0 | 3 |
| `AngularResolutionStudyTests` | 4 | 0 | 4 |
| `AutoFocusTests` | 2 | 0 | 2 |
| `BackgroundDecodingTests` | 16 | 0 | 16 |
| `BackgroundTests` | 6 | 0 | 6 |
| `CascadeEmissionTests` | 3 | 0 | 3 |
| `CascadeSummingTests` | 9 | 0 | 9 |
| `ComptonTests` | 5 | 0 | 5 |
| `ConfigLoaderTests` | 19 | 0 | 19 |
| `CorrelationSearchTests` | 10 | 0 | 10 |
| `CrrcContractTests` | 10 | 0 | 10 |
| `CrystalMaterialTests` | 8 | 0 | 8 |
| `CsIMaterialTests` | 12 | 0 | 12 |
| `CsUnderCoTests` | 20 | 0 | 20 |
| `DeadTimeTests` | 5 | 0 | 5 |
| `DecoderInvariantTests` | 7 | 0 | 7 |
| `DepthDesignTests` | 2 | 0 | 2 |
| `DepthLocalizationTests` | 1 | 0 | 1 |
| `DepthTests` | 5 | 0 | 5 |
| `DetectorDefectTests` | 4 | 0 | 4 |
| `DoiParallaxTests` | 1 | 0 | 1 |
| `DoseTests` | 6 | 0 | 6 |
| `EmissionKindTests` | 3 | 0 | 3 |
| `EntranceAbsorberTests` | 4 | 0 | 4 |
| `EventStreamTests` | 8 | 0 | 8 |
| `ExponentialIntegralTests` | 8 | 0 | 8 |
| `FieldOfViewTests` | 4 | 0 | 4 |
| `FiniteSourceTests` | 2 | 0 | 2 |
| `FocusFusionTests` | 2 | 0 | 2 |
| `FrontEndPartsTests` | 2 | 0 | 2 |
| `FrontEndTests` | 6 | 0 | 6 |
| `GateResponseTests` | 9 | 0 | 9 |
| `HalfSpaceUncollidedTests` | 4 | 0 | 4 |
| `IncidentSpectrumFileTests` | 4 | 0 | 4 |
| `Ir192Tests` | 20 | 0 | 20 |
| `KleinNishinaTests` | 4 | 0 | 4 |
| `ListModeBackgroundTests` | 3 | 0 | 3 |
| `ListModeSourceTests` | 8 | 0 | 8 |
| `MaskAttenuationTests` | 2 | 0 | 2 |
| `MaskFabricationTests` | 5 | 0 | 5 |
| `MaskGeometryTests` | 4 | 0 | 4 |
| `MaskScatterTests` | 3 | 0 | 3 |
| `MaskSecondaryTests` | 5 | 0 | 5 |
| `MixedFieldTests` | 9 | 0 | 9 |
| `MlemOptionTests` | 16 | 0 | 16 |
| `MlemReconstructionTests` | 17 | 0 | 17 |
| `MlemTests` | 2 | 0 | 2 |
| `MuraGeneratorTests` | 5 | 0 | 5 |
| `NonProportionalityTests` | 4 | 0 | 4 |
| `PileUpTests` | 6 | 0 | 6 |
| `PipelineTests` | 7 | 0 | 7 |
| `RandomQualityTests` | 7 | 0 | 7 |
| `RangeLocalizationTests` | 3 | 0 | 3 |
| `ResolvedPairTests` | 15 | 0 | 15 |
| `SamplingTests` | 14 | 0 | 14 |
| `ScattererTests` | 3 | 0 | 3 |
| `SceneConfigBuilderTests` | 7 | 0 | 7 |
| `SceneSimTests` | 2 | 0 | 2 |
| `SoilAirMaterialsTests` | 3 | 0 | 3 |
| `SoilAirTransportTests` | 4 | 0 | 4 |
| `SourceDistanceTests` | 2 | 0 | 2 |
| `SubCellTests` | 10 | 0 | 10 |
| `TerrestrialCatalogTests` | 3 | 0 | 3 |
| `TerrestrialSpectrumTests` | 5 | 0 | 5 |
| `ThermalDriftTests` | 6 | 0 | 6 |
| `ThermalReadoutTests` | 2 | 0 | 2 |
| `TransportInvariantTests` | 14 | 0 | 14 |
| `WaveformTests` | 9 | 0 | 9 |


## Checked VV test citations

Full names are checked against TRX definitions; abbreviated method names are resolved across the discovered methods. Class-only citations establish existence, not a requirement verdict.

### VV.Studio.md

| Citation | Check |
|---|---|
| `AcquisitionContinuationTests.Completed_RaisedPreset_ContinuesExactly` | exists (expanded shorthand) |
| `AcquisitionContinuationTests.Continue_IsRejectedWhileRunning` | exists (expanded shorthand) |
| `AcquisitionContinuationTests.Seed_FixedReproduces_OtherSeedIsAnIndependentAcquisition` | exists |
| `AcquisitionContinuationTests.StopAndContinue_ReproducesTheUninterruptedEventStream` | exists |
| `AcquisitionServiceTests.FloodAxis_MatchesTheDecodersPixelCentres` | exists |
| `AcquisitionServiceTests.InjectedClock_AdvancesLiveTimeAndPresetWithoutWallDelay` | exists |
| `AcquisitionServiceTests.InvalidInputs_FailBeforeStarting` | exists |
| `AcquisitionServiceTests.ShortAcquisition_LocalizesAfterCountThreshold_SnapshotsAreImmutable` | exists |
| `AcquisitionServiceTests.Stop_KeepsConsumedPrefix_AtExtremeMcLimitedSpeed` | exists |
| `AcquisitionViewModelTests.Completed_StartDisabledUntilPresetRaised_LowerPresetRejected_ProgressFollowsPreset` | exists |
| `AcquisitionViewModelTests.Completed_StartDisabledUntilPresetRaised_LowerPresetRejected_ProgressFollowsPreset` | exists (expanded shorthand) |
| `AcquisitionViewModelTests.Failure_WithData_KeepsItLocked_ResetOnly` | exists |
| `AcquisitionViewModelTests.Failure_WithoutData_BehavesAsEmpty` | exists (expanded shorthand) |
| `AcquisitionViewModelTests.LiveTimeAndSpeed_LockedWhileAcquiring_InvalidValuesFallBack` | exists (expanded shorthand) |
| `AcquisitionViewModelTests.Reset_DiscardsData_UnlocksInputs_NextStartIsANewAcquisitionWithANewSeed` | exists |
| `AcquisitionViewModelTests.Spectrum_SnapshotsGrow_ViewSettingsReuseAcquisition_SelectionSurvives` | exists |
| `AcquisitionViewModelTests.Spectrum_SnapshotsGrow_ViewSettingsReuseAcquisition_SelectionSurvives` | exists (expanded shorthand) |
| `AcquisitionViewModelTests.Start_CapturesDetectorInputs_AndLocksThemWhileDataExist` | exists |
| `AcquisitionViewModelTests.Start_SnapshotsGrow_StopKeepsDataAndLocks_ContinueAccumulates` | exists |
| `AmbientAcquisitionTests.BackgroundOnly_StopContinueRetainsExactlyTheFixedTimeStream` | exists |
| `AmbientAcquisitionTests.BackgroundOnly_WorkspacesShowCountsWithoutInventingIsotopeLines` | exists (expanded shorthand) |
| `AmbientAcquisitionTests.EmptyZeroField_CompletesAndBuildConfigFreezesInputs` | exists (expanded shorthand) |
| `AmbientPresetTests.BuildConfig_ResolvesTheValidatedSpectrum_OnTheFrozenCopyOnly` | exists (expanded shorthand) |
| `AmbientPresetTests.DefaultField_AcquiresWithTheValidatedSpectrum` | exists (expanded shorthand) |
| `AmbientPresetTests.Pin_EqualsRepositorySidecar_AndDeployedBytesAreTheRepositoryFile` | exists |
| `AmbientPresetTests.WrongPinOrMissingFile_IsRefusedAtStart` | exists (expanded shorthand) |
| `AmbientViewModelTests.AmbientPhysicalInputsLockAndDerivedBsrHasNoSetter` | exists |
| `AmbientViewModelTests.DefaultIsValidatedFrontOnlyField_ZeroIsIdeal_SourceFreeStartRequiresAbsoluteField` | exists |
| `AmbientViewModelTests.DoseTextMatchingThePattern_SetsTheDoseRate` | exists (expanded shorthand) |
| `AmbientViewModelTests.DoseTextOutsideThePattern_IsRefused_PreviousValueStays_StartBlocked` | exists (expanded shorthand) |
| `AmbientViewModelTests.DoseText_FollowsProgrammaticValue_AndLocksWithData` | exists (expanded shorthand) |
| `AmbientViewModelTests.InvalidAmbientDoseIsRefused_PreviousValueStays` | exists (expanded shorthand) |
| `AmbientViewModelTests.PresetListOffersOnlyTheValidatedSpectrum_ByPinnedReference` | exists (expanded shorthand) |
| `AmbientViewModelTests.Start_DefaultPassesValidatedPresetReference_ZeroTakesTheLegacyPath` | exists (expanded shorthand) |
| `CascadeEmissionTests` | exists |
| `CascadeSummingTests` | exists |
| `DetectorRealismTests.Gain_WidensPhotopeakInQuadrature_AndReducesTightWindowAcceptance` | exists |
| `DetectorRealismTests.Measurement_UsesFrozenInputs_AndReplaysAcrossSnapshotsAndPileUp` | exists (expanded shorthand) |
| `DetectorRealismTests.StudioDefaults_AreExplicit_AndSceneBuilderRemainsBare` | exists |
| `DetectorWorkspaceTests.Face_FollowsPendingBeforeStart_AcquiredAndLockedAfter_PendingAgainAfterReset` | exists |
| `EmissionKindTests` | exists |
| `FocusSweepViewModelTests.Sweep_RejectsLateResult_WhenIdentityChanges` | exists |
| `FrontEndPartsTests` | exists |
| `HeatmapViewportTests.Configure_ResizeKeepsZoom_NewImageSizeRefits` | exists |
| `HeatmapViewportTests.Fit_PreservesAspectAndCentres` | exists |
| `HeatmapViewportTests.MmMapping_PutsPixelCentresOnGrid` | exists |
| `HeatmapViewportTests.PanBy_CannotDragImageOutOfView` | exists |
| `HeatmapViewportTests.PixelAt_RespectsBoundsAndOrientation` | exists |
| `HeatmapViewportTests.ScreenImage_RoundTripWithYUp` | exists |
| `HeatmapViewportTests.Snapping_FitsWholeDevicePixelsPerCell` | exists |
| `HeatmapViewportTests.ZoomAt_IsClamped_FullZoomOutRefits` | exists |
| `HeatmapViewportTests.ZoomAt_KeepsAnchorPointFixed` | exists |
| `HistogramPlotTests.BinLookup_UsesHalfOpenEdges_AndReadoutUsesCountsAndUnits` | exists (expanded shorthand) |
| `HistogramPlotTests.Configure_PreservesZoomAndPanUntilRangeChanges_AndResetStillFits` | exists (expanded shorthand) |
| `HistogramPlotTests.Envelope_RetainsExtremaOfBinsCrossingColumns` | exists (expanded shorthand) |
| `HistogramPlotTests.Labels_ClampBothEdges_AndUseAdditionalRowsWithoutCollisions` | exists (expanded shorthand) |
| `HistogramPlotTests.Steps_IncludePartialBins_AndEmptyBinHasFullWidth` | exists |
| `HistogramPlotTests.VisibleExtent_ExcludesDistantPeak_IncludesIntersectingBins` | exists (expanded shorthand) |
| `ImagingServiceTests` | exists |
| `ImagingServiceTests.Channels_OffAxisCsAndCo_LocalizeAtTheirOwnSource` | exists |
| `ImagingServiceTests.CrossCorrelation_IsUnchangedByTheMethodField` | exists (expanded shorthand) |
| `ImagingServiceTests.Mlem_BuildsOneMatrixPerGeometryAndLine_AndReusesItAcrossRefreshes` | exists (expanded shorthand) |
| `ImagingServiceTests.Mlem_ChannelsLocalizeAtTheirOwnSource_NonNegative` | exists |
| `ImagingServiceTests.Precision_TwentySeeds_MeasuresMixedIsotopesAndCoOnlyControl` | exists (expanded shorthand) |
| `ImagingServiceTests.Strip_CoLocatedCsAndDoubleActivityCo_RecoversSameLiveTimeCsCount` | exists |
| `ImagingServiceTests.Strip_MultipleContaminants_ReportNetWithUnavailableUncertainty` | exists (expanded shorthand) |
| `ImagingServiceTests.Strip_OverlapCovariance_ReplaysWithWindowChangesAndIncrementalPrefixes` | exists (expanded shorthand) |
| `ImagingServiceTests.Strip_UsesSignedCcInput_AndMatchesMlemCountAndUncertainty` | exists (expanded shorthand) |
| `ImagingServiceTests.WindowChange_ReplaysRetainedEvents_RecalibratesAndMatchesFreshProcessing` | exists |
| `ImagingServiceTests.WindowChange_ReplaysRetainedEvents_RecalibratesAndMatchesFreshProcessing` | exists (expanded shorthand) |
| `ImagingWorkspaceTests.LateResponse_CannotReplaceNewerWindowResult` | exists |
| `ImagingWorkspaceTests.PeakChip_CountsSeveralFoundPeaks_AndNamesASingleOne` | exists |
| `ImagingWorkspaceTests.ReconstructionNote_FlagsUnmeasuredOptics_AndTheAcquisitionImageIsNotShownAsMlem` | exists (expanded shorthand) |
| `ImagingWorkspaceTests.Reconstruction_IsAReprojectionSetting_KeepsMeasurementsAndSweep` | exists |
| `ImagingWorkspaceTests.SharedWindow_SelectorStripAndRoi_ReuseFrozenAcquisition` | exists |
| `ImagingWorkspaceTests.StripCount_IsLabelledNetWithUncertainty_ForBothMethods` | exists (expanded shorthand) |
| `ImagingWorkspaceTests.StripMetadata_AndRoiUnits_FollowPublishedViewWhileStripIsPending` | exists (expanded shorthand) |
| `ListModeBackgroundTests` | exists |
| `ListModeSourceTests` | exists |
| `MainViewModelTests.AddRemove_SelectsNewSourceThenNeighbour` | exists |
| `MainViewModelTests.DetectorInputs_DefaultsAndValidationFallbacks_EditableWithoutData` | exists |
| `MainViewModelTests.IsotopePicker_OffersIr192_AndKeepsCs137AsTheDefault` | exists |
| `MainViewModelTests.NewResult_RefreshesMeasurements` | exists |
| `MainViewModelTests.SourceItem_ClampsValues_LabelFollowsEdits` | exists |
| `MainViewModelTests.SpectrumSummary_NamesTheOverflowAndTheFixedAxisEnd` | exists |
| `MainViewModelTests.Startup_HasOneSelectedSource` | exists |
| `MainViewModelTests.ThemeToggle_FlipsThemeAndRelabels` | exists |
| `MainViewModelTests.WorkspaceActivation_SelectsShell_WhenActiveIsSetWithoutCommand` | exists (expanded shorthand) |
| `MainViewModelTests.Workspace_SharedResultAndSelectionSurviveSnapshot_ViewSettingsKeepState` | exists |
| `MainViewModelTests.Workspace_SharedResultAndSelectionSurviveSnapshot_ViewSettingsKeepState` | exists (expanded shorthand) |
| `MeasurementMathTests.AngleDeg_DegenerateArm_IsZero` | exists |
| `MeasurementMathTests.AngleDeg_MeasuresAtTheVertex` | exists |
| `MeasurementMathTests.Distance_IsEuclidean` | exists |
| `MeasurementMathTests.Roi_ClipsToTheImage_AndIsEmptyBetweenCentres` | exists |
| `MeasurementMathTests.Roi_CountsPixelsByCentre_CornersInAnyOrder` | exists |
| `MeasurementsViewModelTests.Add_NumbersSequentially_AndSelectsTheNewOne` | exists |
| `MeasurementsViewModelTests.Add_WrongNumberOfPoints_Throws` | exists |
| `MeasurementsViewModelTests.Delete_SelectsTheNeighbour_ClearRestartsNumbering` | exists |
| `MeasurementsViewModelTests.Roi_HasNoValueBeforeARun_AndFollowsEachNewResult` | exists |
| `MeasurementsViewModelTests.Roi_OnAPaneWithoutData_StaysEmpty` | exists |
| `MeasurementsViewModelTests.Roi_WithNoPixelCentreInside_HasNoValue` | exists |
| `MeasurementsViewModelTests.ToolHint_FollowsTheActiveTool` | exists |
| `MinMaxPyramidTests.Query_EqualsBruteForce_IncludingBothEnds` | exists |
| `MinMaxPyramidTests.Range_ClipsOutsideData_RejectsNonFiniteSamples` | exists (expanded shorthand) |
| `MlemReconstructionTests` | exists |
| `NiceTicksTests.Linear_Uses125Steps_EngineeringLabels_NoNegativeZero` | exists |
| `NiceTicksTests.Logarithmic_LabelsDecades_WithEightMinorTicks` | exists (expanded shorthand) |
| `OpticsPolicyTests` | exists |
| `OpticsPolicyTests.Geometry_UsesEffectiveFieldsAndHasNoPerformanceBand` | exists |
| `OpticsPolicyTests.Presets_ApplyAtomically_MatchPhysicalFieldsAndShowCustom` | exists |
| `OpticsProjectionTests.EmptySnapshot_RefocusKeepsEmptyImagesAndDoesNotCalibrate` | exists (expanded shorthand) |
| `OpticsProjectionTests.Refocus_ReprojectsAllChannels_WithoutMeasurementCalibrationOrNewEvents` | exists |
| `OpticsProjectionTests.Sampling_SmallOffAxisSample_FinerPixelsReduceLocalizationError` | exists |
| `OpticsViewModelTests` | exists |
| `OpticsViewModelTests.Focus_AcquiringStoppedCompleted_KeepsEventsAndSpectrum_OpticsLockedWithData` | exists |
| `OpticsViewModelTests.InvalidInput_StartExpandsSectionsAndDoesNotStartTransport` | exists |
| `OverlayLabelLayoutTests` | exists |
| `PlotSeriesTests.LowerBound_HandlesUniformAndIrregularX_AndEnds` | exists |
| `PlotViewRenderTests` | exists |
| `PlotViewRenderTests.Spectrum_BothThemesFullAndZoom_RenderWithoutWindow` | exists |
| `PlotViewTests.TenMillionSamples_ZoomAndResizeRedraw_Within16Milliseconds` | exists |
| `PlotViewportTests.Mapping_RoundTrips_AndClampsLogFloor` | exists |
| `PlotViewportTests.ZoomPanReset_PreservesAnchor_AndBounds` | exists |
| `ScenarioTests.Readout_AndOneCellRoi_MatchAbsolutePositionAndValue` | exists |
| `SpectrumBandTests.BandOfXRayLines_IsNamedByEmitter_GammaBandByIsotope` | exists |
| `SpectrumServiceTests` | exists |
| `SpectrumServiceTests.Axis_IsFixedAt2000keV_ForAnyLinesFieldOrPileUp_AndOverflowKeepsEveryPulse` | exists (expanded shorthand) |
| `SpectrumServiceTests.CsAcquisition_PhotopeakBinAndFwhmMatchChain` | exists |
| `SpectrumServiceTests.CsAcquisition_PhotopeakBinAndFwhmMatchChain` | exists (expanded shorthand) |
| `SpectrumServiceTests.HighRate_PileUpLosesPulsesAndMovesCountsAbovePhotopeak` | exists (expanded shorthand) |
| `SpectrumServiceTests.Merge_SingleLineGivesOneBand` | exists (expanded shorthand) |
| `SpectrumServiceTests.Merge_UsesResolutionRatherThanWindowOverlap` | exists (expanded shorthand) |
| `SpectrumServiceTests.MixedCsCo_HasFourBandsAndUnionCountsEachBinOnce` | exists (expanded shorthand) |
| `SpectrumServiceTests.Processing_MeasuresFullAndIncrementalWorkAt100000Events` | exists (expanded shorthand) |
| `SpectrumServiceTests.SeedAndSnapshotPartition_AreDeterministic_ToggleReplaysExactly` | exists (expanded shorthand) |
| `StripCountEstimatorTests` | exists |
| `StripProjectionTests` | exists |
| `ThemeContrastTests` | exists |
| `TickFormatterTests.Significant_FourDigitsGroupedWithoutTrailingZeros` | exists |
| `TickFormatterTests.StepLabels_TakeDecimalsFromTheStep_AndGroupThousands` | exists |
| `WorkspaceScenarioTests.Imaging_ChannelAndStrip_MatchRetainedFloodAndFoundPeaks` | exists (expanded shorthand) |

Partial prose references (not stable test IDs): `Imaging_ChannelAndStrip_…`

### VV.Gcam.Evidence.md

| Citation | Check |
|---|---|
| `AlignmentTests` | exists |
| `AmbientAngularSamplerTests` | exists |
| `AmbientEvidenceTests` | exists |
| `AmbientFieldTests` | exists |
| `AmbientGateStudyTests` | exists |
| `AngularResolutionStudyTests` | exists |
| `BackgroundDecodingTests` | exists |
| `BackgroundTests` | exists |
| `ComptonTests.Argmax_RecoversMoreCountsThanPerPixelWindow` | exists |
| `ComptonTests.Contamination_IsImagedAtTheContaminantSource_NotTheTarget` | exists |
| `ComptonTests.Stripping_RecoversCsCount_EvenCoLocated` | exists (expanded shorthand) |
| `ConfigLoaderTests` | exists |
| `CorrelationSearchTests` | exists |
| `CrystalMaterialTests` | exists |
| `CsUnderCoTests` | exists |
| `DeadTimeTests` | exists |
| `DecoderInvariantTests` | exists |
| `DecoderInvariantTests.AnalyticShadow_PeaksAtItsSource` | exists |
| `DecoderInvariantTests.Cyclic_RejectsAModestPedestal_ButNotAnUnlimitedOne` | exists |
| `DecoderInvariantTests.Decode_IsLinearInTheImage` | exists (expanded shorthand) |
| `DepthDesignTests` | exists |
| `DepthTests` | exists |
| `DetectorDefectTests` | exists |
| `DoiParallaxTests` | exists |
| `DoseTests` | exists |
| `ExponentialIntegralTests` | exists |
| `FieldOfViewTests` | exists |
| `FrontEndTests` | exists |
| `GateResponseTests` | exists |
| `IncidentSpectrumFileTests` | exists |
| `Ir192Tests` | exists |
| `KleinNishinaTests` | exists |
| `MaskAttenuationTests` | exists |
| `MaskFabricationTests` | exists |
| `MaskGeometryTests.TaperedChannels_WidenTheFovOfAThickMask` | exists |
| `MixedFieldTests.ComptonStripping_RecoversCoLocatedCsCount` | exists (expanded shorthand) |
| `MixedFieldTests.MixedField_ThroughEnergyWindow_SeparatesIsotopesSpatially` | exists |
| `MixedFieldTests.MultiSource_AllLocalized_InOneRun` | exists |
| `MixedFieldTests.Superposition_IsActivityWeighted` | exists (expanded shorthand) |
| `MlemOptionTests` | exists |
| `MlemTests` | exists |
| `PipelineTests` | exists |
| `PipelineTests.Biasing_IsUnbiasedVersus4Pi` | exists |
| `PipelineTests.CenteredSource_LocalizesSubMillimeter` | exists |
| `PipelineTests.DenserCrystal_DetectsMore` | exists |
| `PipelineTests.LeakyMask_AddsCountsVersusOpaque` | exists |
| `PipelineTests.NonCyclicDecoding_SuppressesGhost` | exists (expanded shorthand) |
| `PipelineTests.OffAxisInsideFcfov_Tracks` | exists (expanded shorthand) |
| `PipelineTests.OffAxisOutsideFcfov_GhostsToOppositeSide` | exists (expanded shorthand) |
| `RangeLocalizationTests` | exists |
| `ResolvedPairTests` | exists |
| `SamplingTests` | exists |
| `SoilAirMaterialsTests` | exists |
| `SoilAirTransportTests` | exists |
| `SubCellTests` | exists |
| `TerrestrialCatalogTests` | exists |
| `TerrestrialSpectrumTests` | exists |
| `ThermalDriftTests` | exists |
| `TransportInvariantTests` | exists |
| `TransportInvariantTests.BiasedSource_MeanWeightIsTheDetectorSolidAngle` | exists |
| `TransportInvariantTests.ComptonCascade_NeverDepositsMoreThanThePhotonEnergy` | exists |
| `TransportInvariantTests.Crystal_StopsOneMinusExpOfMuTimesSlantPath` | exists |
| `TransportInvariantTests.Mask_PassesItsOpenFractionPlusTheTungstenLeak` | exists |
| `WaveformTests` | exists |

Partial prose references (not stable test IDs): 

