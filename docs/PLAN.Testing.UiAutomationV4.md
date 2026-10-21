# PLAN.Testing.UiAutomationV4 — UI automation under the procedure v4.0

Scope: TODO-54. Bring GCAM Studio's UI automation (`tests/Gcam.Studio.UiTests`, its isolation folder, the observed-run
classifier, the test-record and manual generators where they consume desktop results) and
[AGENTS.UiAutomation](AGENTS.UiAutomation.md) from the author's UI-automation procedure v3.4 to **v4.0 (2026-10-21)**.
v4.0 is a simplification: one core document holds every mandatory rule, the people's part shrinks to four acts, and the
production steps run on machine verdicts only. It also carries two rules added in v3.5 (2026-10-19): a video with a machine
check for every test that drives the app, and crashes / unhandled exceptions / hangs as failures.

Status: reference plan written 2026-10-21; D4 decided by the author the same day (option a: "a가 맞겠네. a로 안 되면 지침은
나중에 수정하자."); next: turn 1 (implementer review only).

## Why (2026-10-21)

The procedure's owner simplified it because its many conditional rules kept failing in use ("지침에서 자꾸 펑크가 나는데,
지침을 좀 단순화할 필요가 있겠어"). G-CAM's document still follows v3.3 / v3.4: human classification of undecided
candidates (HUM-08), work profiles P0–P4, approvals per human act HUM-01 / 04 / 07 checked in code, a video whose faults never
matter. Several of these no longer exist in v4.0; the new video and crash rules are not met.

## What v4.0 requires that G-CAM does not do yet (procedure section in brackets)

| Topic | v4.0 | G-CAM now |
|---|---|---|
| Gates | G0 charter, G1 safety, G3 pilot, G4 scale-out, G5 production; G2 measurement and G6 operation are recommended only | stages 0–6, all treated as mandatory |
| People | four acts: charter approval, real-world-effect permission, final review, physical act; nothing else is asked for [§8] | HUM-01 / 04 / 07 approval records required by the probe package and `stage0.py preflight`; HUM-08 classification; HUM-10 figure review in the P4 manifest |
| Undecided candidates | none: an effect attributed to the app outside isolation is `unsafe_effect`; a change that cannot be attributed is recorded for reference and never judged; re-judge from the run's records instead of rerunning; stop before a third attempt on the same trigger [§4.6, §4.7] | HUM-08 records-first path; a "recorded difference, stricter than the procedure" |
| Profiles | removed (P0–P4); the charter names the result use (exploratory / regression / test evidence / manual) [Appendix A] | P1 / P2 in the charter, P4 in the manual manifest |
| Crash | abnormal end of the app process, an unhandled-exception record left by the product, no response within a time limit = failure, even if the scenario's state was right; the detection location is in the charter [§5] | an exit other than `exit 0` fails cleanup; nothing names a crash or a hang |
| Video | one video per test that drives the app, each machine-checked (decodes to the end, length matches the test interval, single-colour / masked / dropped frames within product criteria, step times cover the steps); a `passed` result without a passing video is not produced (G5); new / changed scenarios' passed videos and failed / aborted attempts go to a final-review list that does not block [§6] | video faults "never change the verdict"; only metadata validation (`VideoEntry.Validate`); generators ignore videos |
| Test data | a list of data bundles with SHA-256 and the product's data slot; checked before launch [§4.2] | none written (Studio has no data slot) |
| Allowed peers | connections outside the allowed peers are blocked technically, or the charter says why not and the residual risk [§4.3] | no network observed; nothing blocks |
| Pre-launch checks | five machine checks before every launch and again for every new app instance [§4.5] | partly (privilege, launch values inside the sandbox, no unowned Studio); the action policy is loaded after the window appears |
| Shell records | an item the shell or OS records for a name the app handed it is the app's effect; pass needs every attributed write inside isolation or harness-owned locations [§4.6] | the save dialog's history and recent-document links (E6) are "declared OS behaviour" outside isolation |

Unchanged in substance: only Studio-attributable changes are judged (v3.3, now clearer); ownership by evidence; the
observed run and its positive control per channel; forbidden actions read by the runner; the shared-desktop stop condition
(§3.7: the interactive desktop's current owner and release must be known) — G-CAM's announce / pause practice between
sessions stays as it is.

## Facts in the code (checked 2026-10-21; the implementer re-checks each)

1. Studio registers no `DispatcherUnhandledException`, `AppDomain.UnhandledException` or `UnobservedTaskException` handler
   (`src/Gcam.Studio*`): an unhandled exception ends the process. `OwnedStudioProcess.Close` records `Ending` (`exit N`,
   `still running …`); `Scenario.cs:86/95` turns anything but `exit 0` into `cleanup_failed` and an assertion failure.
   `MainWindow()` throws `Studio exited: N` if the process is gone. Waits throw `TimeoutException` (`PollingWait.WaitUntil`).
   No crash or hang class is recorded; no event log is read.
2. Video: `VideoEntry.Validate` checks metadata (run id, duration > 0, frames, size); `VideoTimeline` writes captions; no
   decode check, no duration-versus-test-interval check, no blank-frame or step-coverage check. `VideoCoverage.Exclusions`
   holds `ObservedRunTests` (launch-idle-exit without automation) and `PlotViewTests`, which hosts the `PlotView` control
   in the test process and never launches `Gcam.Studio.exe`.
3. Launch: `OwnedStudioProcess.Start` refuses an elevated token, validates the launch values inside the sandbox
   (`IsolationPaths.ValidateLaunchValues`), refuses an existing `Gcam.Studio` process and an existing run folder.
   `ActionPolicy.Load` runs in `Scenario.cs:53` / `StudioWindow.cs:20`, after the window is found.
   `ReportExportDialog.Save` refuses a path outside the sandbox before typing it.
4. Studio has no data slot: no open dialog and no file read reachable from Studio; `ReportFileWriter` is the only file
   writer. Data files ship in the build (for example `ambient/terrestrial-unscear2000-v1.json`) under the deployment
   fingerprint.
5. Human-act ids in code: `ApprovalRecord.HumanActId`; `ProbePackage` and `stage0.py` (`preflight`, line ~389) require
   HUM-01 / 04 / 07; `ManualObservation` requires HUM-01 (the desktop go); `UiRunManifest` names P4 and HUM-10;
   `classification-rules.json` statuses cite HUM-07; `test_stage0_isolation.py` tests these.
6. Declared writes outside isolation (`surface-list.json`): E1 `app-tsf-composition-window` (HKCU CTF key, text-input
   framework on Studio's UI thread), E3 `app-gpu-driver-timestamps` (GPU driver file under ProgramData), E6 save-dialog
   history and recent-document links (shell records; the typed export name carries the run ID, so they are marker items).
   Since TODO-52 (D2) no per-process write record is taken.
7. The background filters are retired (`backgroundFilterHistory`, inactive since TODO-52 D3); the machinery remains.

## Decisions (proposed / verify)

| # | Topic | Proposal | Kind |
|---|---|---|---|
| D1 | Charter and gates | Rewrite §0 of AGENTS.UiAutomation as the v4.0 charter items (§1): purpose, readers, result use, target build, environment and launch permission, where the G1 lists live, test data, manual / excluded scope with residual risk, observation method, crash detection location, video criteria and exceptions, artefact locations. The stage table becomes gates G0 / G1 / G3 / G4 / G5 (G2 / G6 kept as recommended practice). The rewritten charter is the one charter approval the author gives | proposed |
| D2 | People | The four v4.0 acts, plus the author's **standing rule** of a go before every desktop run (shared desktop, §3.7), recorded as the author's decision rather than a procedure act. New approval records use act names (`charter`, `real-world-effect`, `final-review`, `physical`, `desktop-go`); readers keep accepting HUM-xx in committed records, so editions, milestones and observed runs replay unchanged. The HUM-07 requirement goes: a widened list, policy or classifier triggers an observed run (machine), as §4.6 already does in the harness; HUM-04 becomes part of the charter approval | proposed |
| D3 | Candidates | Replace the HUM-08 section and the "recorded difference" with §4.6 / §4.7: attributed outside isolation → `unsafe_effect` (what `diff-classify` already does), unattributable → reference list, re-judge from records without a rerun, stop before a third attempt. **Verify** that no class in `diff-classify`'s order (control window A→B, outside the Studio window, another git working tree, recorded filter, OS run record) can move an attributed item out of judgement | verify |
| D4 | **Shell records and declared outside-isolation writes** — decided 2026-10-21: **(a)**; if (a) does not hold up, the procedure is revised later (author) | v4.0 counts what the shell records for an app-handed name as the app's effect and has no "allowed by declaration" outside isolation, so every export scenario's dialog-history and recent-document records (E6) would end as `unsafe_effect`. Options: **(a, recommended)** treat them as a real-world effect and ask for it inside the existing per-run desktop go ("this run's export scenarios leave their file names in the Windows dialog history and recent list") — no new prompt, the record names it; (b) suppress the records with the shell policy value for the run (an in-place OS setting change, itself a real-world effect, and §2.7 restore duty); (c) ask the procedure's owner for a v4 rule for unavoidable shell records. E1 / E3 are not attributable without a per-process record (D2 of TODO-52) and lie in shared locations: under §4.6 they become reference items, kept in `surface-list.json` as findings with their residual risk in the charter | **decided (a)** |
| D5 | Crash and hang | Classify the owned process's ending: `app_exited` (gone before the harness asked; exit code), `unhandled_exception` (Application event log entries for the owned PID and start time inside the run window — .NET Runtime 1026, Application Error 1000, Application Hang 1002; read-only), `not_responding` (on a wait timeout, probe the window with `SendMessageTimeout` for a limit before reporting the timeout). Each is a scenario failure with its own issue code in the run record. Detection location in the charter. **Verify** that the Application log is readable without elevation and measure the longest normal UI-thread stall to set the limit | verify |
| D6 | Video check | After the recording stops: decode to the end (Media Foundation source reader, built into Windows), duration against the recorded interval, single-colour frames, masked / neutral / suppressed frames, dropped slots, and step-caption times covering the executed steps; result in the video index entry and `video-status.json`. Criteria in the charter, **proposed from the distribution measured on the existing local videos** (`artifacts/ui-videos`, 40 of them) — never chosen to pass them all | verify |
| D7 | Production gate (G5) | `test_records.py collect --profile desktop` and `manual_records.py generate` refuse to produce a `passed` desktop result whose video is missing or failed its check (exempt classes excepted). They write a final-review list beside the record / edition: passed videos of new or changed scenarios (catalog source hash differs from the previous milestone or edition), and failed / aborted attempts; the list never blocks. Committed milestones and editions replay as they are | proposed |
| D8 | Video exceptions | Keep both: `ObservedRunTests` (recording would break the observation boundary) and `PlotViewTests` (does not drive the app: an in-process control benchmark). Reasons rewritten to the v4.0 wording | proposed |
| D9 | Test data | One list (`Harness/Isolation/test-data.json`): no product data slot; bundle `harness-typed-scenes` (synthetic, generated by the scenarios); the data files shipped in the build with SHA-256. Pre-launch check: the deployment fingerprint and the list hashes match. **Verify** the shipped data inventory | verify |
| D10 | Allowed peers | Allowed peers: none. Not blocked technically: a firewall rule needs elevation and is an OS setting change each run; an AppContainer launch changes the environment under test. The charter records this with the residual risk (native framework / driver connections), backed by `SurfaceSurveyTests` (no network API in the deployed IL) and the owned-PID connection check of observed runs. **Verify** whether a non-elevated per-process block exists that leaves Studio's behaviour unchanged | verify |
| D11 | Pre-launch checks | One `Preflight` before `Process.Start` with the five §4.5 checks: data list (D9), resolved write surfaces inside isolation (existing), launch values in the allowed list (existing), peer-block state (D10: "exception declared in the charter"), action policy loaded (moved before launch). A new `Gcam.Studio` instance during a run stops the run (Studio never restarts itself) | proposed |
| D12 | Removed devices | Drop the P0–P4 labels (result use instead) and HUM-10 from the manifest. Keep implemented but unused devices (background-filter machinery, conditional scope) and say "unused" in the charter, as Appendix A allows | proposed |
| D13 | In-place switches (§2.7) | G-CAM changes no product setting in place (Studio keeps none). **Verify** that the registry marker is a harness-owned value and not an in-place switch | verify |
| D14 | App-only locations | Studio has no product-specific declared default outside isolation, so no during-run file watcher is needed. **Verify** against `surface-list.json` | verify |
| D15 | Document automation v4.0 | The manual generator follows the author's document-automation procedure, also v4.0 since 2026-10-21. Turn 1 lists only the gaps this task touches (run record contract, final-review list, refusal to produce); a wider alignment is a separate task | proposed |

## Steps

1. **Author:** D4 — decided 2026-10-21, option (a).
2. **Turn 1 — review only** (Codex, new conversation, guard block): check every *verify* row and fact 1–7 in the code,
   derive D5's stall limit from existing run records and measure D6's video distribution on the local videos (headless, no desktop),
   list the D15 gaps, propose the AGENTS.UiAutomation wording for the code-bound parts; write
   `docs/PLAN.Testing.UiAutomationV4.Review.md`. Give the implementer both v4.0 guides by path in the prompt only.
3. **Planner:** "Decisions after review" here; charter text (D1) for the author's charter approval.
4. **Turn 2 — implement** in the same conversation: D2, D3, D5–D14 in code and tests; old records still replay.
5. **Planner verification:** build, `dotnet test`, `test_records.py check-catalog --discover`, `check --archives`,
   `manual_records.py check --all`, stage0 tests; diff read; AGENTS.UiAutomation, AGENTS.Rationale and the CLAUDE.md line
   updated by the planner.
6. **Desktop session** (author's go per run, announced to the other sessions; the CZT batch stays paused): the isolation
   folder changes, so a new observed launch-idle-exit run re-establishes G1; the pilot with its broken-verdict and recovery
   runs (G3); the scenarios touched by D5 / D6 with videos, including one export scenario under D4. Record in
   VV.Studio History.

## Done when

- AGENTS.UiAutomation reads as the v4.0 charter with gates G0 / G1 / G3 / G4 / G5 and the four people's acts plus the
  desktop-go rule; no HUM-08 path, no profile labels.
- A crash, an unhandled exception and a hang each fail a scenario with their own issue code (shown by a test, without the
  desktop where possible).
- Every app-driving desktop test's video is machine-checked; a generator refuses a passed result without a passing video
  and writes the final-review list.
- Pre-launch preflight runs the five checks before launch; test-data list and peer exception exist.
- All verification commands green; committed milestones and editions replay; one observed run, pilot, broken verdict and
  recovery pass on the new harness.

## Not here

- A new manual edition, test-record milestone or software-document edition (TODO-53): later, on the author's word.
- Any wording or content of the procedure's source project in this repository (procedure only; it is named "the author's
  UI-automation procedure").
- A disposable environment (VM or separate account) — recommended by §4.1, not required while the source survey closes the
  write surfaces.
