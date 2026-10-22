# PLAN.Testing.UiAutomationV4.Review — measured implementer review

Scope: TODO-54 turn 1, review only, against `c7b671c` on `todo54-uia-v4`, 2026-10-21. The reference is the author's UI-automation procedure and, for D15, the author's document-automation procedure, both v4.0. No product, harness, plan or charter changes were made. D4 remains the author's decided option (a).

Status: **revise the plan before implementation**. D3's suggested equivalence with the existing classifier is false; D14's premise is false. D5's historical records do not measure UI-thread responsiveness. D6's files are decodable, but their present metadata cannot establish all the proposed checks. These are implementation requirements, not reasons to weaken the rules.

## 1. Findings that change the plan

| Priority | Finding | Consequence |
|---|---|---|
| High | Attributed candidates can be cleared by timestamp, null-window identity, OS-record rules, and the other-working-tree heuristic. Existing tests explicitly expect several of these results. | D3 needs code and negative tests, not just documentation. Establish attribution before applying any background explanation. |
| High | Documents contains a declared Studio-specific export-name pattern (`surface-list.json:23`). | D14 requires a narrowly filtered during-run watcher, including transient creations/removals, overflow/error reporting and a positive control. Do not remove the declaration to avoid this requirement. |
| High | E1/E3 and broad common-dialog scope are shared locations, not Studio-specific locations. Current scope construction treats them as attribution. | Separate inventory from attribution. Without current process/marker evidence these are references, as D4 intends for E1/E3. Historical attribution remains historical evidence. |
| High | Video media length is calculated from encoded slots; the actual recording start/end and step identities are not retained. Final steps can fall after the last encoded slot and disappear from VTT. | Store an independent interval and step timeline. Checking the index against itself cannot qualify a video. |
| High | A liveness probe only after `PollingWait` times out cannot catch a synchronous UIA call that never returns. | D5 needs an independent bounded liveness observer, not just a timeout catch. |
| Medium | New rules added unconditionally to current readers/renderers would invalidate old milestones and editions. Merely accepting old HUM identifiers is insufficient. | Version the semantics and retain the historical rendering/classification branches and their pinned inputs. |

## 2. Facts 1–7 rechecked

References below are repository-relative; line numbers identify the reviewed revision.

1. **Substantially correct, with qualifications.** No registration of the three named unhandled-exception events exists in the three Studio projects. `OwnedStudioProcess.MainWindow` (`Harness/Isolation/OwnedStudioProcess.cs:87`) detects an already exited process; `Close` at line 109 records the ending (line 121). `Scenario.cs:86` adds `cleanup_failed` and lines 92–95 assert cleanup. There is no crash/hang diagnostic class or Application-log reader. However, an exit **0 before the requested close** can currently be accepted at cleanup; checking only a nonzero ending misses it. `PollingWait.WaitUntil:25` checks elapsed time between synchronous predicate calls, so its deadline does not bound a blocked UIA predicate. No-handler source evidence is not an empirical test of every native/runtime crash path.
2. **Correct about missing validation.** `VideoEntry.Validate:26` checks identities, positive dimensions/duration/counts, ranges and nominal FPS; it does not decode pixels or compare against a separate interval. `VideoTimeline.Captions:20` clamps and coalesces timestamps and discards zero-length cues. `VideoCoverage.Exclusions:11` lists the two classes. The recorded rationale for `PlotViewTests` is timing perturbation; its code hosts the control in the test process and does not launch Studio. In the new charter define that benchmark as outside app-driving Studio scenarios; timing perturbation alone is not an isolation exception.
3. **Correct.** `OwnedStudioProcess.Start:32–60` checks privilege, launch values, existing processes and sandbox existence before `Process.Start`. `Scenario.cs:53` loads the action policy after discovery; `StudioWindow.cs:19` has a lazy load, not a pre-launch guarantee. `ReportExportDialog.Save:25–28` confines the path before typing. Move the policy check into the common launch path, including the observed probe.
4. **Incorrect as written.** No user-selectable input-file slot or open dialog was found, but “no file read reachable” is false. `SimulationService.cs:105,113` resolves the deployed ambient JSON through `ConfigLoader.ResolveAmbientSpectrum:27` and `IncidentSpectrumFile.Load:38`; `ReportFileWriter.cs:19` reads previous destination bytes for rollback. The compiled offer loader reads its assembly resource (`ReadoutOfferRecordLoader.cs:19`). Say **no user import slot; fixed deployed model data are read; export rollback can read confirmed destinations**. `ReportFileWriter` is the declared Studio file writer, not the only file-writing API present anywhere in its shipped libraries.
5. **Correct, but wider than the named fields.** `ApprovalRecord`, `ProbePackage.Validate:13`, `ManualObservation.Preflight:82`, `stage0.py:389`, `UiRunManifest.Finalise:36–92`, classification-rule status/history entries and their tests contain the old acts/profiles. `RunRecord.cs:26` also emits P1. See the implementation inventory below. The collector's `normal` / `desktop` profiles are separate execution modes and should remain.
6. **Correct historical inventory; wrong implication if scope means current attribution.** E1 and E3 are declared in `surface-list.json:30–31` and `classification-rules.json` `declaredAppSurfaces`; E6 is in `shellRecordLocations`. No current per-process write trace is taken by the default snapshot path. Common-dialog keys, IME coordinate values and a driver-wide timestamp file do not become Studio-only because they appear in the inventory. Run-ID-marked export records are attributable; unmarked changes at these shared locations are not thereby proven attributable.
7. **Correct.** `backgroundFilters` is empty, history entries are inactive, but `validate_filters` and the classification branches remain (`stage0.py:1441,1576`). OS-run-record rules remain active and distinct from the retired filters.

## 3. D3 — complete classification-order audit

`diff_classify` (`stage0.py:1464–1655`) computes scope and marker attribution at lines 1525–1539. Its actual priority is:

| Order | Branch | Can remove an attributed item from safety judgment? |
|---|---|---|
| 1 | No scope and no name/value marker → local unattributed list | Legitimate for snapshot writes with no attribution evidence. **There is no per-process-write evidence input in this function.** An owned-process list does not attribute a file or registry delta. |
| 2 | Inside a harness-owned root → `harness` | Legitimate only with owned-root evidence; unreadable ancestors merely containing a root are deliberately not cleared. A broad root must never be invented for D4. |
| 3 | Match a declared E1/E3/E6 surface → `declared` | Yes: currently permits outside writes by declaration, before all other tests. E1/E3 must become shared reference inventory for new snapshot semantics. E6 needs the per-run permission mechanism below. |
| 4 | Match `osRunRecords` → `other-actor` | **Yes for a marker**, intentionally (`test_os_run_record_with_a_marker_stays_other_actor`, test file line 958). Scope overlap is refused at load by `validate_filters`, but marker overlap is not. A path-family match cannot discard an app-handed export name. Keep a separately evidenced OS-observes-process classification only when it does not erase product/marker attribution. |
| 5 | Last-write/key timestamp outside launch−1 s … exit+2 s → `not-of-this-window` | **Yes for both product-specific scope and markers.** Timestamps can be preserved, reset or reflect another value in the same key. A change between B and C is not disproved by this timestamp. Removed entries have no timestamp and do not take this branch. |
| 6 | Same identity also changed A→B → `other-actor` | **Yes for both product-specific scope and markers**, even when B→C has a different size/hash. Background activity at a location does not prove the second write was by the same actor. |
| 7 | Recorded background filter, only when marker is absent | No valid loaded filter can cover scope: literal-prefix overlap is conservatively rejected. Marker items skip it. There is therefore no current accepted scope/marker bypass through this branch. Keep that invariant. |
| 8 | Another git working tree, only when marker is absent | **Yes for product-specific scope.** A default-pattern export inside a Documents subfolder holding `.git` is cleared. No product code performing git operations is required for an export there. Marker items are protected from this particular branch. |
| 9 | Readability uncertainty → `unclassified`; otherwise `out-of-declaration` | Uncertainty blocks; it is not made reference-only after attribution. Keep that behavior. An inaccessible relevant location cannot establish absence. |

The 18 existing snapshot tests passed in scratch. They include tests asserting the problematic window, null-window and working-tree results (`test_stage0_isolation.py:1051,1063,1092`). Two additional scratch probes demonstrated a **run-ID-marked** file before the time window and a marked file changed in both windows: both incorrectly returned `completed`, respectively `not-of-this-window` and `other-actor`. Results are retained in `%TEMP%/gcam-todo54/classification-probes.json`.

Owned-process effects require a separate correction: `diff_classify` uses `ownedProcesses` to prove a process positive control, but does not consume `processIssues` / `processMonitorErrors` when computing `runStatus`. `ObservedRunTests` records these issues and its UI manifest fails, yet the snapshot classifier alone can still say eligible. `ManualObservation.Context:119` does not aggregate those issues. New eligibility must combine the finalized run manifest, process monitor and snapshot judgment. No classification-order branch should clear a proven owned-process connection/write/spawn. Optional ETW `classify` has its own actor-based pipeline (`stage0.py:936`); migrate D4 there too, without pretending default snapshots contain ETW attribution.

**Proposed rule:** first distinguish proven harness operations, Studio-attributed effects and unattributed references. Only unattributed references receive time/null-window/working-tree/filter annotations. For Studio-attributed effects, enforce containment, registered child/peer policy or the exact authorized D4 effect. Time-window evidence may diagnose attribution uncertainty; it cannot silently reverse an established attribution.

## 4. D4 — carry the decided permission into the verdict

Implement option (a), with no extra approval prompt. A single per-run author statement can supply both the desktop go and the explicitly named shell-record permission. Preserve its actual quote/date; record two scoped act references if appropriate. A generic “run the desktop tests” record alone does not supply the named effect.

Add a structured `realWorldEffects` permission to the approval package, preflight result, scenario UI manifest and observed context: effect ID (E6), approval reference, run/batch ID, target revision and deployment, covered export actions/scenario IDs, file-name markers, allowed record families/operations and validity for this run. Link the actually typed export names to their scenario run IDs. Carry the observed items and permission references into `writeAudit`. Do not relabel shared shell locations as harness-owned or `other-actor`.

For new classifier semantics remove blanket E6 `declared` acceptance. An attributed matching export-name record is `authorized-real-world-effect` only when the permission covers this run, action, name and record family; that class is compatible with `completed`. A missing, generic, stale, wrong-run or mismatched permission leaves `unsafe_effect`. A real exported document outside the sandbox is not a shell record and never gains permission through E6. Mere membership in ComDlg32 is not attribution: unmarked shared bookkeeping is reference-only unless process evidence establishes it. Document this as the author's explicit per-run exception to the ordinary containment criterion, not as a new general declaration allowance. D4 remains decided; no alternative shell-policy switch is proposed.

Tests must cover each mismatch, marked RecentDocs value data, recent links, permitted addition/update versus unrelated deletion, sidecars, non-export runs, and the optional trace classifier. Finalize classification in a new output record; never overwrite the raw run or a historical verdict. Record classifier/rules hashes and the permission source used.

## 5. D5 — event log and responsiveness measurements

**Access:** `Get-WinEvent -LogName Application -MaxEvents 5` succeeded; `WindowsPrincipal.IsInRole(Administrator)` was false. This establishes this run user's present read access, not a guarantee for all accounts or later policy changes. No elevation, registry inspection, crash induction or desktop use occurred.

Read-only provider queries also returned retained `.NET Runtime` 1026, `Application Error` 1000, `Application Hang` 1002, and `Windows Error Reporting` 1001 records. XML inspection found:

| Provider / ID | Use and limit |
|---|---|
| .NET Runtime / 1026 | Managed unhandled-exception diagnostic. Sample payloads were unnamed text, not a universal structured PID/start-time schema. Correlate System execution PID where valid plus image/runtime/text and the owned process lifetime; ambiguous records remain diagnostic. |
| Application Error / 1000 | Native/application fault diagnostic. Sample fields include `ProcessId`, `ProcessCreationTime`, `AppPath`, exception code and report ID. Use EventData's faulted process, not the reporting provider's System PID. |
| Application Hang / 1002 | OS hang report. Sample fields include `ProcessId`, `StartTime`, `ExeFileName`, `HangType`. A live hang does not necessarily produce this event before the harness deadline; reporting may occur later. |
| Windows Error Reporting / 1001 | Supplemental report/bucket record, including `EventName` and P1…P10, not a dependable direct owned-PID record. Deduplicate by report/instance; absence proves nothing. |

These observations confirm readable event shapes, **not that the sampled faults were .NET 9 Studio faults**. No controlled Studio crash/hang was permitted this turn. A .NET 9 WPF managed unhandled fault is expected to use 1026 and may also yield 1000/1001; a native fault may lack 1026; OS-recognized hangs may yield 1002/1001. None is a required event sequence. Retain bounded event query range/bookmarks and raw XML/hash locally, record access/query errors, and allow for delayed reporting. A no-event result must not clear an unexpected process exit or independently detected hang.

**Historical timing:** 171 unique manifest paths parsed: 134 passed, 37 failed, from the allowed main-checkout run folder and temporary scenario-record folders. Among passed runs:

| Statistic | N | Minimum | Median | P95 | Maximum |
|---|---:|---:|---:|---:|---:|
| Adjacent logged-step gaps | 2081 | 0 s | 0.237 s | 2.131 s | 83.018 s |
| Gaps ending in a polling completion | 1668 | 0 s | 0.232 s | 3.553 s | 83.018 s |
| Subset ending in tab/selection/options/reset/workspace/drop-down responses | 903 | — | 0.221 s | 0.780 s | 4.649 s |

P95 uses the sorted value at floor(0.95×(N−1)); these are observations, not independent latency samples. The longest gap was run `20261017-200013-258382`, `HighResCoLine_ProgressTabsSensitivityAndSessionHit`: streamed-flood-progress completion → High-res preparation completion. The preceding progress gap was 72.532 s. Preparation runs asynchronously; these are **state-completion waits**, not measured periods of an unresponsive UI thread. The 4.649 s subset maximum also includes intervening unlogged work, from a pair acquisition completion to reset-confirmation completion (`20261017-195532-aa0870`).

Consequently **the requested longest normal action-to-window-answer delay is not recoverable from these records**. Actions and every predicate call are not timestamped; logs carry local time-of-day only; no WM_NULL response durations are retained. Do not claim 83.018 s is a measured UI stall or use it to permit a 90/120 s hang. A **provisional 10 s continuous nonresponse limit** has a 5.351 s margin over the largest coarse simple-UI gap (more than 2×), but is a charter policy proposal requiring normal-run measurement, not a calibrated conclusion. Keep task-specific acquisition/preparation deadlines separate. Add monotonic action/probe timing, verify the provisional limit in the authorized pilot, and report a failure rather than enlarging it if it is breached.

Observe liveness independently of synchronous UIA operations, with owned HWND/PID/start-time validation, bounded `SendMessageTimeout(WM_NULL)` calls and a monotonic continuous-unresponsive interval. Avoid flags that return early using Windows' unrelated hung-window heuristic. A missing window, modal transition, exited process and a message timeout need distinct diagnostics. Mark the intentional close **before** requesting it. Detect early exit even with code 0, startup failure, late failure after a successful assertion, and cleanup exceptions; keep the original assertion failure as well. Liveness failure is a product failure, while inability to release a still-running app also fails cleanup. No automatic force termination or restart adoption.

`UiRunManifest.Finalise:42` currently accepts only three safety issue types. Add product failure diagnostics on a separate axis (or explicitly extend and define that schema); simply adding new issue strings will throw during finalization. Ensure `runStatus != completed` cannot coexist with a published `passed` scenario. Current `Finalise("passed")` does not enforce that invariant itself.

## 6. D6 — headless video measurements and proposed criteria

The local index contained **40 clips**. A scratch x64 net9.0 program used the Windows Media Foundation synchronous source reader, selected the video stream, requested RGB32 and read samples until EOS. Every decoded pixel was inspected, excluding the intentional 2 px neutral border. A single-colour frame was defined as each RGB channel's spatial range ≤8; black required every channel maximum ≤8. This is a stated measurement definition, not a claimed privacy detector. MP4 and VTT SHA-256 were independently recomputed. No new codec or tool was installed.

The scratch program's initial exploratory run called the sample-duration slot incorrectly; that output was discarded. The corrected run used `IMFSample.GetSampleDuration` slot 37 and is the source of all duration results below (`decode-corrected.jsonl`). Frames, timestamps and EOS were read again for all clips. Scratch builds ran serially through the required memory guard and completed with zero warnings/errors.

| Measurement | Distribution / result |
|---|---|
| Decode to EOS / decode errors | **40/40; zero errors**; decoded frame count matches index in every clip |
| File identity | **40/40** MP4 and VTT hashes match the index |
| Total video | **7371 frames, 737.1 s** |
| Indexed/media duration | min 2.3 s; median 11.35 s; P95 60.5 s; max 166.2 s |
| Decoded final sample end minus indexed duration | **0 for every clip** |
| First decoded timestamp / largest inter-sample gap | **0 / 0.1 s for every clip** |
| Declared dropped slots / captured minus encoded frames | **0 / 0 for every clip** |
| Single-colour / black interior frames | **141 / 141**, in five clips; other 35 clips have zero |
| Declared masked / privacy-suppressed frames | **141 / 141**; all are full-frame privacy masks: **140 `privacy-pause`, 1 `privacy-transition`** |
| Foreign/survey-failed masks | none declared in these 40 clips; the privacy-transition frame is manual export frame 99 (zero-based) |
| VTT interval coverage | **100%** in all 40, from 0 to media end; floating-point roundoff only |
| Retained scenario manifest | found for all 40 |

The intentional neutral border is present by implementation on every captured frame; it is not evidence of a wholly neutral picture. Whole-picture neutral/privacy suppression is black and is the 141-frame population above. Single-colour decoding and the mask metadata agree in aggregate; that agreement does not prove a future unmarked black frame is safe. `droppedSlots` counts pending-frame overwrites (`VideoRecorder.Capture:102`), not every capture schedule missed; decoded timestamp gaps must also be checked.

| Clip (unique method/format) | Duration | Frames | Black = masked = suppressed | Share | Longest black run |
|---|---:|---:|---:|---:|---:|
| ReportExportScenarioTests.ExportReport… csv | 3.9 s | 39 | 20 | 51.28% | 2.0 s |
| ReportExportScenarioTests.ExportReport… pdf | 4.1 s | 41 | 22 | 53.66% | 2.2 s |
| ReportExportScenarioTests.ExportReport… docx | 4.0 s | 40 | 21 | 52.50% | 2.1 s |
| ReportExportScenarioTests.ExportReport… html | 3.9 s | 39 | 21 | 53.85% | 2.1 s |
| ManualScenarioTests.Export_ReportsAndEvents_WriteOnlyDeclaredFiles | 14.2 s | 142 | 57 | 40.14% | 1.7 s |

**Interval limitation:** `_duration` is last encoded slot + one period (`VideoRecorder.Write:132`, assignment at line 145); `_end` is collected but not serialized. The run's first/last diagnostic times are not the capture start/stop times. Logged-span minus media duration ranges from −1.007 to +0.544 s, because pre-video focus diagnostics and differing last logged work are included. This does **not** validate duration against an independent test interval. Startup, normalization, failure before recorder creation and cleanup are not currently recorded. Define the app-driving interval in the charter, start recording before the first window manipulation, and retain launch/no-window omissions without inventing footage.

**Step coverage limitation and observed defects:** VTT interval coverage is not executed-step coverage. Of 221 non-polling diagnostic strings, 140 had a literal cue match. This is not a valid 63% step score: 40 are pre-video focus diagnostics, dialog details are deliberately suppressed, and manual reader steps are separately added through `ManualRun.BeginStep`. Nevertheless nine terminal scenario log entries are absent, including the Waveform rate-study result, the four report-format terminal checks and four Readout terminal descriptions (PreparationCancelRetry, WideLocalisationHelp, SharpTwoLineBank and HighResCoLine). For example the Waveform VTT ends at 9.400 s with the event-index input caption, while the rate-study result is logged at 20:06:10.709. `Captions` drops steps clamped to the same media end. There are no stable step IDs or serialized privacy intervals to prove every executed step is represented. Manual step timestamp coverage cannot be reconstructed from its final prose/check lists alone.

**Proposed charter criteria**, to be frozen before the new pilot, then challenged by corrupted/truncated clips and independent recordings:

- Decode all samples through EOS without an error; dimensions and sample count match; timestamps increase; file hashes match. No error tolerance.
- Decoded start/end cover the independently recorded app-driving interval, with ≤**0.2 s** endpoint error (two 100 ms slots; 0.1 s scheduling margin beyond one slot). Persist UTC anchor plus monotonic duration; do not compare two values derived from the final sample. Unknown interval means unqualified, not pass.
- Undeclared single-colour/full-frame-black intervals: at most **0.2 s continuously** and **1% of interval**; exclude the fixed edge ring. The observed ordinary baseline is zero, so these allowances provide two slots of transient margin without accepting a blackout. Validate this detector on dark/light actual content and a synthetic blank defect; range ≤8 is the measurement proposal, not a final perceptual classifier.
- Declared privacy suppression must have explicit start/end, reason and affected step IDs, a safe generic cue, and exact agreement with full-frame mask samples. **Total obscured duration ≤25%** and no step requiring a visible response entirely obscured. This preserves at least 75% visible material; it is a product usefulness proposal, not fitted to the largest observation. It rejects all five current export clips (40.14–53.85%), rather than grandfathering them. Resolve the short clips' design through meaningful visible steps or an explicit charter decision; do not pad footage to meet the ratio. Any unsafe/unknown pixels are rejected regardless of percentage.
- Dropped media slots ≤**1%**, with no gap exceeding **0.3 s** (two missing 100 ms samples beyond the ordinary 0.1 s spacing). Observed baseline is zero; the stated margin allows isolated encoder pressure, not a hidden step. Count gaps including initial/final coverage, not only overwritten pending buffers.
- **100% of executed step IDs** represented by timed safe cues or explicit privacy-step mapping, with an associated sample/interval and positive cue length. Use ≤0.2 s timestamp alignment error; never discard a final step to make this pass. Failed/unreached steps remain identified. Zero lost required steps.

Under these proposals no existing clip can yet be fully qualified because the independent interval/step contract is missing. Five also fail the proposed obscuration budget, and at least nine terminal captions need repair. Scenario truth remains unchanged: video failure blocks G5 use of a pass; failed/aborted tests stay failed/aborted. `GCAM_UI_VIDEO=0` may still run exploratory tests but cannot supply an app-driving desktop pass for production. Cache reuse remains a skip; it must not resurrect an older video's qualification.

The per-clip decoded measurements and diagnostic comparisons are retained locally as `%TEMP%/gcam-todo54/video-measurements.json`, with `video-summary.json`, `decode-corrected.jsonl`, `Program.cs`, `videos.py` and the scratch project. They contain local diagnostic context and are not copied into the repository. No pixels or source-procedure content were imported.

## 7. D9, D10, D13, D14

### D9 — shipped data and reachability

The reviewed worktree has no built Studio deployment. I did not build into the repository or inspect the main checkout outside the allowed directories. Inventory combines project declarations, current source, and the retained `manual-20261017-01/pin.json` deployment manifest under the allowed temporary tree; it is **not a fresh measured deployment of c7b671c**.

| Item | Shipping / integrity | Reachable as user data? |
|---|---|---|
| `ambient/terrestrial-unscear2000-v1.json` | Only copied domain data file declared in the project files (`Gcam.Studio.Services.csproj:17`). Current source bytes and retained deployment pin agree: SHA-256 `4d48bc4272ce4c25ff6ae9b13ed7583e3c8f467afffdb0225956a70cc435b3f4`. | Fixed model input, enabled through the ambient preset; path resolved from `AppContext.BaseDirectory/ambient` and checked against `AmbientPreset` pin. No UI file chooser or editable input path found. |
| Readout offer/cost JSON | Embedded in `Gcam.Studio.Services.dll` (`csproj:24`, `ReadoutOfferRecordLoader:19`); source/pin SHA-256 `24b3bf6714e9b856a9b8aaac8d95c2bdbd212659808172440716e15fdeda8b6a`. | Fixed model resource, no external file slot. Deployment assembly hash covers the embedded bytes. |
| `Gcam.Studio.deps.json`, `Gcam.Studio.runtimeconfig.json` | Retained deployment manifest's other two JSON files; runtime/dependency configuration, not a scene bundle. | Read by runtime/host; not user scene data. Include in deployment integrity, not synthetic-scene inputs. |
| Assemblies, executable and compiled WPF/resources | Retained pin lists 23 DLL/EXE entries plus the three JSON files above (26 entries total). | Code/resources; not a user import location. Pin every deployment byte in the selected fingerprint scheme. |
| Typed scenes and exported files | Synthetic UI values generated by scenarios; outputs and rollback reads confined to the sandbox in the harness. | No shipped scene file is consumed by Studio. `ConfigLoader.Load` exists in libraries but is not exposed as Studio import. |

D9's “no product data slot” must mean **no user-controlled import slot**, not no model inputs. A synthetic typed-scene bundle needs generator/scenario identity and hashes of its defining inputs; do not invent a file hash for data that exists only in memory. Verify the exact permitted deployment file set, hashes and ambient path before launch; reject unexpected data files rather than merely finding the expected one. The current deployment fingerprint covers DLL/EXE/JSON only (`ContentFingerprint.Files:17`); don't claim it covers arbitrary added extensions. State that boundary or explicitly broaden the new scheme, retaining the old fingerprint version for old pins.

### D10 — outbound blocking

No currently available approach was established that simultaneously gives **ordinary-user, process-instance-wide outbound denial and unchanged Studio behavior**. This is an engineering assessment, not an exhaustive proof of impossibility. No firewall/registry/token setting was changed, no connection was made, and OS SDK documentation outside the permitted read set was not fetched.

| Approach | Cost / side effects / conclusion |
|---|---|
| Windows Firewall executable rule / WFP filtering | Normal rule installation needs administrator/service authorization; rules are machine policy and generally image-path rather than PID+start-time scope. Dynamic WFP sessions can limit lifetime but do not remove authorization requirements. Not a verified ordinary-user solution. |
| AppContainer without network capabilities | Can provide a network boundary, but changes token, filesystem/registry/COM access, package/capability assumptions and WPF/native behavior. Profile setup is persistent state unless separately managed. A different environment requiring its own qualification, not unchanged behavior. |
| Restricted token / low integrity / job object | Useful process/resource restrictions; no general network-deny guarantee follows. Changes access/environment and may break UI/COM; job resource limits do not supply a socket firewall. |
| Proxy environment / sink proxy / DNS changes | Not an outbound block: direct sockets, native code and ignored proxy settings bypass it. A loopback proxy also adds a peer and failure behavior. DNS/system proxy changes are persistent settings, outside this turn. |
| Injected API hooks or a wrapper network shim | Changes executing code and timing; must cover all native/managed transports and descendants, difficult to prove. Not equivalent to the shipped app. |
| VM / separate network namespace or disabled adapter | Different environment; needs setup/admin or an already supplied environment and may affect other work. Not a per-process unchanged local launch. |

Keep D10's charter exception with explicit residual native/runtime/driver risk. **Correct its claimed evidence:** `SurfaceSurveyTests.DeployedMetadata…:12` checks network references in the Studio shell assembly. The optional inventory enumerates Gcam assemblies; it does not assert absence in every deployed dependency or native driver. `ManualObservation` and default `ObservedRunTests` snapshots do not sample sockets. `ProbeChannels.Network` declares network observation not applicable. `Fallback-Probe.ps1` / optional tracing may collect PID connections, but that is not evidence for every default observed run. Either integrate a bounded owned-instance connection observer and channel proof, or state the actual missing observation in the charter and run record. Never describe observation as a technical block.

### D13 — registry control

**Verified harness-owned marker, not an in-place product switch.** `RegistryMarker.Write:11–25` fixes the root `Software/GcamUiaProbe`, appends validated run ID and writes `RunId=<runId>`. `WindowsRegistryMarkerStore.TryCreateNew:21–38` requires this exact convention and native created-new disposition before writing. An existing key is refused, not overwritten. The marker is retained; no product preference is read, changed or restored. `RegistryMarkerTests` are the headless seam. No registry access was performed in this review. Atomic key creation can leave an empty newly owned key if the subsequent write fails; that is a retained harness diagnostic, not a restoration obligation.

### D14 — false premise

`surface-list.json:23`, `app-report-export.scope`, explicitly declares Documents and all subdirectories matching Studio's default `gcam-(report|events)-YYYYMMDD-HHMMSS` export/sidecar names. `ReportExportService.cs:18–22` opens the save dialog with the default name and Documents. These are product-specific defaults outside isolation even though the harness normally types a different sandbox path.

Add a watcher before launch and retain events until after exit, restricted to that declared pattern. Include create/change/rename/delete and transient create-delete, old/new rename paths, timestamps and health/overflow state; refuse qualification when the watcher cannot cover the declared root or misses its positive control. Recursive Documents can be expensive or redirected; record actual resolved root and access failures. A harness-owned positive control matching the pattern must be positively identified as harness activity; do not authorize arbitrary default-name exports. E1/E3's shared names do not call for product-specific watchers. `StateSnapshot` before/after alone cannot satisfy this requirement.

## 8. D2 / D7 / D11 / D12 implementation inventory and replay

All paths prefixed `Harness/` below are under `tests/Gcam.Studio.UiTests/`. This is an edit inventory, not authorization to change historical artifacts.

| Area | Code / records that change | Tests / fixtures |
|---|---|---|
| D2 act identity and scope | `Harness/Isolation/ApprovalRecord.cs`, `ProbePackage.cs`; `stage0.py preflight`; `Harness/ManualObservation.cs` Preflight/Context; `ObservedRunTests.cs` package/context; approval-package and pin schema/version; `UiRunManifest` charter approval references. New act names need separate charter and per-run desktop-go scopes; do not map old HUM-01 blindly to both. | `ApprovalRecordTests`, `ManualObservationTests`, `UiRunManifestTests`; `test_stage0_isolation.py` preflight fixtures. New missing/duplicate/stale/wrong-run act tests; retain legacy fixtures. |
| D7 recording qualification | `VideoRecorder.cs` interval/timeline/status/stop ordering; `VideoEntry.cs` check evidence/version; `VideoTimeline.cs` IDs and final-step coverage; new MF reader/checker; `VideoPublisher.cs` pin qualified status, preserve last pass but never use it for a newer attempt; `Scenario.cs`, `ManualScenario.cs`, `ManualRun.cs`, `RunRecord.cs`; `ResultCache`/`CachedDesktopTestCase` provenance and flags as needed. | `VideoModelTests`, `VideoRecorderTests`, `MediaFoundationWriterTests` plus decoder tests, `ManualRecordTests`, `ResultCacheTests`. Truncated/corrupt/blank/mask mismatch/drop/interval/last-step defects. No new screenshot verdict. |
| D7 production consumers | `samples/testing/test_records.py` collect/finish_record/validate_record/render/generate/export_bundle/self_test; `samples/testing/adapters.py` if case-to-run links are normalized there. `samples/manual/manual_records.py` load_runs/validate_chosen/assemble/generate/check/self_test. Capture an immutable per-attempt qualification manifest and checked-video hash, not the mutable latest index. | `samples/evidence/tests/test_manual_records.py`, `test_provenance*`/collector self-tests; refusal fixtures for missing/failed/stale video and newer failed attempts. Every desktop theory row needs a case→run→video link. |
| D7 final review | Both generators emit a deterministic review-list sidecar with case/scenario/run IDs, build, definition hash, video/caption/check hashes and reason: new/changed pass, failure or aborted attempt. Explicit predecessor milestone/edition, not guessed filesystem “latest”. No predecessor means new. Retain failed/aborted attempts even when omitted from the printed manual. | Generator self-tests for unchanged/new/changed definitions, failed/aborted and missing-video attempts, theory rows, absent predecessor and deterministic order. No human approval field manufactured. |
| D11 prelaunch | Central preflight called by `OwnedStudioProcess.Start` before `Process.Start`; policy parse/load/hash there and pass the same instance to `StudioWindow` / `PointerBoundary`. New test-data list/checker; deployment/launch/surface/peer-exception result fields. `StudioProcess` wrapper, `Scenario`, `ObservedRunTests`, `ManualObservation` call sites. | `ActionPolicyTests`, `IsolationPathsTests`, `SurfaceSurveyTests`, new pure preflight tests. Inject a start delegate: each failed check must leave its call count zero. Verify check order and policy identity. |
| D11 new instances | `ProcessLedger.cs`, `ProcessMonitor.cs`, `Scenario` cleanup, observed context and classifier eligibility. Existing same-name test compares a full registered path with an image path/name; inaccessible processes may be skipped. Record discovery limitations and stop before further input on unproven instances, not merely after the scenario finishes. | `ProcessLedgerTests`, new process-monitor test seams; same image, different path, reused PID, inaccessible process and late child. No adoption or termination of an unowned process. |
| D12 output schema | `RunRecord.cs` P1 field/comment; `UiRunManifest.cs` `Bundle.Profile`, profile argument/switch, G2–G6 mandatory layout, `ReviewPlan.HumanActIds`, P4 charter text, `PROFILE-*` conditional entries. New result-use and recommended-practice representation. Keep unused conditional/filter machinery explicit. | `UiRunManifestTests` P4/HUM-10 expectations and schema checks; `ManualRecordTests`, observed-record tests. Keep old schemas as read-only fixtures. |
| Structured declarations shared by these rows | `surface-list.json`, `classification-rules.json`, new `test-data.json`, `ObservationScope.cs`, `ProbeChannels.cs`, classifier/context schema and fingerprint version where changed. D3/D4/D14 also need their own rules/tests and watcher evidence. `action-policy.json` changes only if new machine-readable effect references are needed. | Snapshot/trace tests, `StateSnapshotTests`, `TracingToolingTests`, `ContentFingerprintTests`, `ManualObservationTests`; catalog source/hash inventory updated by the planner's normal process. |

**Replay contract:**

1. Do not edit `samples/testing/records/M1–M3`, committed manual editions, old observed manifests, approvals or their hashes. Do not rebuild a historical subject or consult today's video folder when replaying it.
2. Introduce explicit new schema/qualification versions for new run, video-check and generator contracts. Readers enumerate supported versions; unknown versions fail. Legacy record parsing/rendering retains the old output shape, absence of review-list files and absence of video requirements. New production refuses legacy/unqualified input for a new v4 claim; archival `check` replays its original contract. Do not create a user-selectable “legacy” bypass for live collection.
3. `test_records.py.generate(checking=True):841` already uses retained catalog/generation provenance; extend it without adding fields or changing normalized cases for old artifacts. Export/import of any new evidence kind must be lossless; new video requirements cannot change old TRX verdicts or source hashes. `normal`/`desktop` collector modes stay intact.
4. `manual_records.py.check:1441` reassembles from retained sources, generator/writer provenance and files. Thread the recorded contract version through `assemble`, preserving the old exact file set. Persist new check metadata/review-list inputs for new editions; keep MP4 retention separate and hash-pinned so old editions do not acquire a video-file dependency. A check summary's own hash and origin must be verifiable, not just a string saying passed.
5. Observed-run replay needs its **recorded observation definition, surfaces/rules and classifier version**, not today's `ISOLATION` defaults. Current `diff_classify` defaults read live rules; a legacy record cannot replay byte-for-byte after changing them unless its exact inputs and semantic branch are supplied. Keep rejudgment as a separate derivative record with original-input hashes and changed/previously unresolved items. A new source hash necessarily makes a new classification record; it must not be represented as the old bytes.
6. Current archived check passed for **three milestones**. Retest these and all editions after implementation; a schema comment promising compatibility is not evidence. Retained histories may still say HUM/P4; “no profile labels” applies to new live emissions/charter, not rewriting history.

## 9. D15 — only the document-automation gaps touched here

The current manual record is a local GCAM contract, not the full v4.0 document run-record contract. Do not claim full conformance merely by adding a video field.

- **Run record linkage:** `ManualRun.BuildRecord:195` has steps/checks, execution status and a UI-manifest hash, but retains no manifest file (`retained=false`), no append-only execution log with step/check line references, no common execution/sibling-record linkage and no fully identified procedure/catalog record. Assertions outside `ManualRun.Check` are not automatically represented as step checks. The test collector normalizes TRX cases but does not link every desktop case to its finalized UI execution, G1 basis or video check. Add the narrow versioned links needed here; schedule the wider contract migration separately and explicitly list it as incomplete.
- **Closed-state correctness:** manual execution already becomes aborted for UI status other than completed, and `assemble` omits the latest aborted/nonpassed chapter. Preserve that distinction and retain reasons; never fall back to an older passing chapter. New UI failures/crashes/hangs and unsafe effects must reach both document-facing records consistently. A video fault does not rewrite functional checks.
- **Refusal to produce:** a new passed desktop result/printed chapter requires the corresponding passing video check or declared exemption. Missing, mismatched, corrupt or unsupported evidence must stop that production path before publishing outputs. A failed/aborted record remains retained and reportable; it is not converted to pass or silently dropped. A partial manual may state an explicit omission/draft according to its existing contract, but cannot print the affected generated chapter as passed. Freeze this distinction in D7 so “refuse” does not accidentally suppress failure reporting.
- **Final review list:** output it after machine checks, beside the new edition/record, bound to output and evidence hashes. Include new/changed passed videos and failed/aborted attempts, plus document render flags relevant to the generated delivery files. Do not await a review decision to generate, fill in a reviewer or fabricate approval. The current generation records' omissions/render information are useful inputs, not an existing video review list.

The source procedure's mandatory record contracts are not removed by v4's simplification. Full append-only logs, sibling procedure records and broader document qualification are a separate planned migration; if this task intends a full v4 document-conformance claim, D15's scope is insufficient and the plan must expand before implementation.

## 10. Proposed code-bound charter wording

These are new GCAM-specific proposals, not edits to `AGENTS.UiAutomation` and not copied source wording:

> The launcher records five preflight results before each new Studio process: permitted input/deployment integrity, resolved write boundaries, launch-value allow-list, network boundary or identified charter exception, and the parsed action policy. Any failed result prevents launch. An unproved Studio instance discovered during a scenario stops further input.

> The observed-run verdict uses only attributable Studio effects. Shared-location changes without process or run-marker evidence are retained as references. A product-specific default-name match or a run marker cannot be cleared by a file timestamp, background-window activity, or the presence of a git worktree. The Documents export-name scope is watched during execution as well as compared before and after it.

> An export run's desktop permission record explicitly covers its named shell-history effects. The classifier records the applicable permission for each authorized effect. Uncovered outside effects end the run unsafe; neither a declaration alone nor an old run's approval authorizes them.

> Process lifetime, independent window liveness and owned-instance Application-log diagnostics are observed through cleanup. An unexpected exit, correlated unhandled fault or continuous nonresponse beyond the charter limit fails the scenario even after successful numerical assertions. Event-log absence is not proof of health.

> A scenario's functional verdict is independent of its recording. New production of a passed desktop result requires a hash-bound passing video check, including an independent interval and complete executed-step mapping. A missing or failed recording is retained with its reason. The final-review list is generated from machine records and does not wait for human review.

The charter also needs D1's result use and retention fields, D8's precise benchmark scope, D9's fixed model inputs and D10's actual unblocked/unobserved paths. Remove statements that videos never affect **production**, while preserving that they do not alter scenario truth. Do not remove the unused safety machinery or rewrite historical records to erase old labels.

## 11. Implementation size and verification

This is too large for a single implementation turn under the same ~40-minute limit. Estimate **35–45 code/data/test files**, plus planner-owned documentation/catalog synchronization; **60–90 new or changed headless cases** and several later authorized desktop fault/recording checks. The major additions are an MF decoder/quality checker, independent crash/liveness observation, versioned preflight/run contracts, the Documents watcher and two production consumers. D3/D14 alone make it larger than the plan currently suggests.

Split in the same conversation, each with a reviewable contract boundary:

1. Turn 2: versioned acts/preflight/manifest/context, D3 attribution fixes, D4 permission, D9/D10 declarations, D13 clarification and D14 watcher. Preserve legacy readers and establish their tests.
2. Next turn: crash/exit/liveness detection and complete per-attempt recording interval/step/quality evidence; MF decoder defect tests. Record policy proposals approved by the planner/author before the pilot.
3. Next turn: test-record/manual production gates, immutable evidence export, final-review lists, versioned replay and full headless verification. Planner synchronizes charter/rationale/docs, then the authorized observed run and pilot validate real integration.

Do not call an intermediate split v4-qualified. The observed basis changes when isolation content changes; re-establish it on the final harness before producing new v4 results.

Verification performed this turn: read-only git status/HEAD; source/declaration inspection; Application-log access/provider-shape queries; guarded scratch decoder builds; two headless decoding passes (corrected pass authoritative), file-hash and VTT/record analysis; 18 existing synthetic snapshot tests plus two marker probes; **three committed test-record milestones and three committed manual editions replayed successfully**. No desktop tests, product build, registry queries, setting changes, installations, deletes, moves, git state changes or network uploads were performed.

Not run / limits: live .NET 9 Studio crash/hang and WM_NULL latency calibration; watcher/peer-block experiment; fresh deployment inventory; new production gates and corrupted-video checker tests (not implemented); test discovery requiring a Studio build. There is no empirical basis here to claim all native dependencies lack network activity. Scratch and raw runs were retained. All this turn's repository writes are confined to this Review file.

## APPROVAL REQUESTS

None. No irreversible command is needed to complete this review. Implementation, charter decisions and a later desktop run belong to subsequent turns; this report does not request an additional permission step for the already decided D4 mechanism.
