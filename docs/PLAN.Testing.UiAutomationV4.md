# PLAN.Testing.UiAutomationV4 — UI automation under the procedure v4.0

Scope: TODO-54. Bring GCAM Studio's UI automation (`tests/Gcam.Studio.UiTests`, its isolation folder, the observed-run
classifier, the test-record and manual generators where they consume desktop results) and
[AGENTS.UiAutomation](AGENTS.UiAutomation.md) from the author's UI-automation procedure v3.4 to **v4.0 (2026-10-21)**.
v4.0 is a simplification: one core document holds every mandatory rule, the people's part shrinks to four acts, and the
production steps run on machine verdicts only. It also carries two rules added in v3.5 (2026-10-19): a video with a machine
check for every test that drives the app, and crashes / unhandled exceptions / hangs as failures.

Status: reference plan written 2026-10-21; D4 decided by the author the same day (option a: "a가 맞겠네. a로 안 되면 지침은
나중에 수정하자."). Turn 1 review done the same day ([review](PLAN.Testing.UiAutomationV4.Review.md); D3 and D14 premises
wrong, fact 4 corrected); **Decisions after review** below. Parked the same day (TODO-50 first) and **resumed 2026-10-22 by the
author with the lighter cut and two parallel implementation turns** ("좋아. 셋 다 수용."): see **Resumed: lighter cut and
parallel turns** below, which supersedes R7, R11 and R12 where they differ. The same day the author added the **STR evidence annex** (below;
"1번으로 해줘."). Next: turns A and B in parallel, then turn C.

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

## Decisions after review (2026-10-21)

The review (`PLAN.Testing.UiAutomationV4.Review.md`) re-checked facts 1–7, measured the 40 local videos (all decode to the
end, hashes match, 141 black frames, all declared privacy masks in the five export clips), queried the Application log
without elevation, and replayed the three milestones and three editions. Adopted unless marked otherwise.

| # | Decision |
|---|---|
| R1 (D3) | **Attribution first.** `diff-classify` first separates harness operations, Studio-attributed effects (product-specific scope, run marker, owned process) and unattributed references. Only unattributed items receive the time-window, A→B, null-window, git-working-tree and filter annotations; an attributed item is judged for containment, the registered child / peer policy or the exact D4 permission. The review's two marker probes (a marked file outside the time window, a marked file changed in both windows, both wrongly `completed`) become negative tests; the existing tests that assert the old results change with the new semantics version. OS-run-record rules may not clear a marker item. Eligibility combines the snapshot verdict with the run manifest's process issues and monitor errors |
| R2 (D4) | Option (a) as a structured `realWorldEffects` permission (effect E6, approval reference with the author's quote and date, run / batch, target revision and deployment, covered export scenarios and typed names, record families) carried by the approval package, preflight result, scenario manifest and observed context. Blanket E6 `declared` acceptance goes; a matching marked shell record becomes `authorized-real-world-effect` (compatible with `completed`) only under a covering permission; anything else stays `unsafe_effect`. One per-run statement of the author may carry both the desktop go and the named effect. E1 / E3 become shared-location references |
| R3 (D14) | **Premise wrong**: `surface-list.json` declares Studio's default export names (`gcam-(report\|events)-…`) under Documents, a product-specific default outside isolation. Add a during-run watcher on that declared pattern (create / change / rename / delete, transient items, overflow and errors, resolved root) with a harness positive control; qualification is refused when it cannot cover the root. The declaration stays |
| R4 (D5) | An independent liveness observer (owned HWND / PID / start time, bounded `SendMessageTimeout(WM_NULL)`, continuous non-response interval), not a catch after a wait times out — a blocked synchronous UIA call never reaches the catch. Provisional limit **10 s** continuous non-response (about twice the largest coarse simple-UI gap, 4.6 s, in 134 passed runs; the records hold no true UI-thread stall data), confirmed in the pilot and reported as a failure, never enlarged, if breached. The intentional close is marked before it is requested; an early exit **with code 0** fails too. Application-log entries (.NET Runtime 1026, Application Error 1000, Application Hang 1002; WER 1001 supplemental) are correlated to the owned instance as diagnostics; their absence clears nothing. Product failures get their own manifest axis (`UiRunManifest.Finalise` accepts only three safety issue types today) |
| R5 (D6) | Store an independent recording interval (UTC anchor + monotonic) and a step timeline with stable step IDs; fix the caption writer that drops final steps clamped to the media end (nine terminal captions missing today). Checks: decode to EOS with no error, sample count / dimensions / hashes; interval endpoints within 0.2 s; undeclared single-colour frames ≤ 0.2 s continuous and ≤ 1 %; dropped slots ≤ 1 % with no gap > 0.3 s (decoded timestamps, not only the overwrite counter); 100 % of executed step IDs mapped to a timed cue or a declared privacy interval. Criteria frozen in the charter before the pilot and challenged with corrupted, truncated and blank clips |
| R6 (D6, planner) | **Declared privacy intervals get no share cap** — the review's proposed 25 % would reject all five export clips (40–54 %). The black frames hide the native save dialog, which shows the user's folders and name; the export clips are short, so the share is high by construction, and padding them to pass would be footage made for the rule. Instead: each privacy interval is declared with start / end / reason / step IDs and matches the mask samples exactly, the steps before and after the dialog are visible, and the share is reported. Part of the charter the author approves |
| R7 (D7, D15) | Generators take an immutable per-attempt qualification record with the checked video's hash, never the mutable latest index; a failed / aborted record stays reportable and is never dropped or turned into a pass. Final-review list against an explicit predecessor milestone / edition. D15 stays narrow (the links this task needs); the full document-automation v4.0 run-record migration is a separate Backlog item, and nothing here claims full document conformance |
| R8 (D9) | Wording: **no user import slot**; fixed model inputs are read (the deployed ambient JSON, SHA-256 `4d48bc42…`; the embedded readout offer resource) and export rollback reads confirmed destinations. The test-data list pins the deployment file set and rejects unexpected data files; the typed-scene bundle is identified by its generator and scenario, not by an invented file hash. The deployment fingerprint covers DLL / EXE / JSON only: say so or widen it in a new fingerprint version |
| R9 (D10) | Charter exception kept (no non-elevated per-process block leaves Studio unchanged). Evidence corrected: `SurfaceSurveyTests` checks the Studio shell assembly, not every dependency; default observed runs do not sample sockets (`ProbeChannels.Network` "not applicable"). Because §4.6 needs a live channel for each of file, process and network, add a light owned-instance connection sampler (TCP / UDP tables, no elevation) with a harness positive control to observed runs |
| R10 (D8, D11–D13) | `PlotViewTests` is outside the app-driving scope (in-process control benchmark), not an isolation exception. One preflight in `OwnedStudioProcess.Start` before `Process.Start` (every failed check leaves the start delegate uncalled), the same parsed policy instance passed on; an unproven Studio instance stops further input. P1 / P4 / HUM-10 leave live emissions; collector modes `normal` / `desktop` stay. D13 verified (harness-owned key, created-new only) |
| R11 (replay) | New semantics are versioned (run record, video check, classifier, generators); readers enumerate supported versions; committed milestones, editions and observed records replay under their recorded contract and inputs; no live "legacy" bypass; re-judgement is a separate derived record |
| R12 (split) | Three implementation turns in the same conversation: **A** acts / preflight / manifest and context versions, R1, R2, R3, R8, R9; **B** crash / exit / liveness (R4) and the recording interval, step timeline and decoder checks (R5, R6); **C** production gates, final-review lists, versioned replay and full headless verification (R7, R11). Then the planner's charter rewrite for the author's approval, then the desktop session. No intermediate turn is called v4-qualified |

Cost (review's estimate): 35–45 code / data / test files, 60–90 new or changed headless cases, then the desktop session.

## Resumed: lighter cut and parallel turns (author, 2026-10-22)

The v4.0 core items stay (attribution-first classifier, real-world-effect permission, crash / exit / hang failures, a
machine-checked video, pre-launch checks with the test-data list, the peer exception, the Documents watcher). Lighter:

| # | Change |
|---|---|
| L1 (R4) | No Application-log correlation: liveness, early exit (also with code 0) and abnormal exit only |
| L2 (R5) | Checks: decode to the end with no error; decoded duration against the recorded interval within 0.2 s; undeclared single-colour frames ≤ 0.2 s continuous and ≤ 1 % of frames; no decoded timestamp gap > 0.3 s; 100 % of executed step IDs mapped to a timed cue or a declared privacy interval (R6 unchanged). The separate overwrite-counter share and extra sample statistics go. Challenged with corrupted, truncated and blank clips |
| L3 (R7) | Generators read the video status (checked video's SHA-256 and check result) of the exact run record they consume; no separate per-attempt record store. Failed / aborted attempts stay reportable and go to the final-review list |
| L4 (R11) | One `contract` field (`v4`) on new run records, video status and classification output; a record without it is the old contract and is read and replayed as today. No derived re-judgement records |

**Parallel turns.** A and B run at the same time in separate worktrees and touch disjoint files; C runs after B is merged
into A's branch.

| Turn | Worktree / conversation | Content | Owns (only these may change) |
|---|---|---|---|
| A | `todo54-uia-v4`, the review conversation (resume) | D2 / R10 act names (readers keep HUM-xx), P1 / P4 / HUM-10 out of live emission (D12), D11 / R10 one preflight before `Process.Start` with the five checks and the policy loaded before launch, D9 / R8 test-data list, R9's charter-exception state, R1 attribution-first classifier with the two negative probes, R2 `realWorldEffects` permission, E1 / E3 as references, L4 on run records and classification | `Harness/Isolation/**` (existing files), `Harness/{Scenario,StudioWindow,StudioProcess,ManualObservation,ManualRun,ManualScenario,UiRunManifest,RunRecord,ResultCache}.cs`, the csproj, `samples/evidence/tests/test_stage0_isolation.py`, and their tests |
| B | `todo54-b` (from A's base), new conversation | New standalone parts with tests, not yet wired: liveness observer (R4 / L1), Documents watcher (R3) with its positive control, owned-instance connection sampler (R9) with its positive control, video checker (L2 / R6); recorder changes: independent recording interval, step timeline with stable step IDs, the dropped-final-caption fix, video status with `contract` | new files under `Harness/Observation/` and `Harness/Video/`; `Harness/{VideoRecorder,VideoTimeline,VideoEntry,VideoCapture,VideoPublisher,VideoCoverage,MediaFoundationWriter,CaptureQuality}.cs`; new test files and the existing video / capture tests |
| C | A's branch after merging B, A's conversation | Wiring B's parts into the launch, scenario and observed run (product-failure manifest axis, live file / process / network channels), D7 production gates with L3, final-review lists, L4 replay, full headless verification | any |

Heavy commands (build, test) go through one lock shared by both turns
(`python -B %TEMP%\gcam-todo54\heavy.py -- <command>`: waits for the other turn, runs under memguard, waits and retries when
memguard refuses for low memory), so only one build or test runs at a time while the CZT batch keeps running.

## STR evidence annex (author, 2026-10-22)

The software test report of the TODO-53 set (`GCAM-SW-STR`, edition `2026-10-20-1`) holds no image: its UI part is case
names, counts and catalog text, and the 41 recorded videos stay machine-local (`artifacts/` is ignored). A reader cannot
see from the report that the app was driven. The author chose to add the evidence after this task, from videos that
passed the v4 check, rather than from today's unchecked videos (known defects: nine terminal captions missing).

| # | Decision |
|---|---|
| S1 | The STR gains an annex **UI automation evidence**: per desktop case of the milestone the report selects — run ID, verdict, the checked video's SHA-256 and video status (L3), and 3–5 frames (start, key steps, end), each with its step ID, cue text and media time |
| S2 | Frames come only from videos whose video status is `passed` under `contract: "v4"`; a case without one is listed with the reason, never illustrated from another attempt. Privacy intervals give no frame |
| S3 | Frame export is part of the C# video checker (turn B's decoder): it writes the frames and a manifest (video SHA-256, step ID, media time, frame SHA-256); the document generator stays standard-library Python and reads only the manifest and the images, refusing a frame whose video hash differs from the run record |
| S4 | Frames are committed downscaled (JPEG; the whole annex in the tens of MB at most), under the milestone's record folder; videos stay machine-local. Capture region and paths are checked so no local path or other window appears (verify) |
| S5 | A new STR edition needs a milestone whose desktop profile ran on the v4 harness (M4, author's go for the desktop collection); the other documents of the set are regenerated unchanged in content |

Heavy steps — frame export over the videos, the desktop collection, document generation (PDF rendering) — run under
memguard through the shared lock, one at a time, like the builds.

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
7. **STR evidence annex** (S1–S5), after the desktop session: frame export and the generator change (Codex turn), the M4
   desktop collection (author's go), then the new software-set edition with the annex for the author's look.

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
- The STR edition after M4 carries the UI automation evidence annex: every desktop case either illustrated from its
  v4-checked video or listed with the reason it is not.

## Not here

- A new manual edition: later, on the author's word. (The M4 milestone and the software-set edition are in scope only for
  the STR evidence annex, step 7.)
- Any wording or content of the procedure's source project in this repository (procedure only; it is named "the author's
  UI-automation procedure").
- A disposable environment (VM or separate account) — recommended by §4.1, not required while the source survey closes the
  write surfaces.
