# PLAN.Docs.StudioManual.Charter — stage-0 probe and proposed P4 charter

Scope: draft for the planner and author; source survey, qualification and later manual use. No desktop/elevation or registry observation has been performed.

Status: **draft, not approved**. This file is not a HUM-04 approval. MR1–MR4 establish direction; the concrete charter, permission package and unresolved observation policy still require the author's actual statements on the reviewed commit.

## Purpose, readers and result use

GCAM Studio is the simulator's engineering viewer. The eventual P4 bundle describes procedures actually driven by automation, with automatic checks and safe readable captures, for repository users. It is not a measuring instrument or a regulated clinical product UI. MR7: P4 approval follows the author’s review of the first observed-run artefacts; the first run requires only a limited HUM-04 for the probe charter below. The initial probe is a separate P1 safety investigation: launch–idle–exit, no UIA input/tree dump, no normalisation, no simulation acquisition. Its successful launch alone does not qualify G1 or P4.

Scope: the current Studio deployment and the harness's launcher, isolation/policy, process/lease, record, trace classifier and capture contracts. Exclusions: real hardware, publication, external services, installs, OS changes, arbitrary process termination, personal/production data, and all desktop operations while the author is remote. A full P4 charter will also map chapters, requirements, risks and environmental exclusions after SM-7/SM-9/overlay/edition decisions.

Success criteria: source/deployment/isolation identities agree; all resolved effects are declared or remain explicit unknowns; no undeclared product effect or unresolved trace candidate; each required channel has a positive control; cleanup/lease release are recorded independently from product outcomes. Later P4 needs all G0–G6, independent product verdicts and final edition figure review. Empty/filtered traces and missing controls are blocked, never passed.

Roles (proposed): author sets scope, authority, source acceptance, exclusion risk and final figure/edition decisions; planner prepares the concrete commit/package and audits/replays results; implementer builds and tests headless components; non-elevated runner drives Studio; administrator runs only the named tracing wrapper. Raw ETL and identifying event/context details remain local. Public records contain filtered classifications/hashes and role identifiers, not machine paths/accounts.

## Existing-harness adoption and transition

`adoptionMode: existing`. Initial qualification scopes: isolation/launch/path rules, process ledger/monitor, desktop lease, policy/selector inventory, verdict/mutation, manifest finalisation, trace/classification, capture/template. Each manual scenario later depends on the scopes it uses and cannot outrank them. Current status is **blocked/unqualified**, not promoted by historical P1 records.

Transition list (draft owner role: planner; review deadline 2026-11-13): untouched legacy regression and survey scopes remain historical/diagnostic-only; current source assertions and original raw records remain intact. Their use for P2/P4 requires current source/binary/input/environment linkage, negative controls and an observed safety basis. No blanket rewrite of old evidence and no new coverage count from headless CI.

Human-act plan: HUM-01 for the free desktop/probe go, HUM-04 limited to the launch–idle–exit probe charter (not P4 approval), HUM-05 for policy scopes (initial probe has no accessibility/device interactions; later P4 scope is undecided), HUM-07 for the reviewed isolation/policy/classification commit before using it, HUM-08 only for candidates that improved machinery still cannot classify, HUM-09 for later exclusions, HUM-10 for final figures/new capture definitions, HUM-12 for controlled static-source/edition disposition, HUM-13 for intentional baseline differences or expiry extensions. Device actions, real-world effects and identity disclosure are outside scope. The runner does not ask the author to operate Studio to establish an observed-run gate. The matching document process's source and figure/edition decisions target hashes, with no duplicate figure-approval request.

## Survey summary and unresolved boundaries

The structured inventory is [surface-list.json](../tests/Gcam.Studio.UiTests/Harness/Isolation/surface-list.json). Reachable managed Studio state/calibration/theme work is in memory; ambient data are read beside the deployment and offer data are compiled resources. Save/study APIs in engine libraries are not exposed by Studio's current DI entry points. The conservative deployed metadata survey retains all managed/native references, including methods present but not proven reachable. The pin connects the actual Release build/deployment with exact source hashes; it is not a runtime audit.

The harness writes marked temporary records, lease state and approved trace outputs; old survey scenarios explicitly write repository assets and are excluded from the first probe. Launch redirects APPDATA/LOCALAPPDATA/TEMP/TMP. **Environment redirection does not prove WPF/CLR/native known-folder, registry, crash-reporting or shell effects absent.** These remain unknown until observation/classification. The machine's lack of a disposable environment is not replaced by a static absence claim. Stop on an undeclared app effect or unresolved attribution; revisit scope if containment cannot be established.

Isolation location: `tests/Gcam.Studio.UiTests/Harness/Isolation/`. Its content hash includes launcher/cleanup, path/launch values, guard/policy, ledger/monitor/lease, source linkage, classification/external-connection rules, survey and trace/fallback tools. `stage0.py pin` records this fingerprint plus source/deployment identities. New/renamed/removed targets and unknown bindings fail the static policy check; an unapproved policy never establishes qualified use. A4: raw ETL and run folders remain local until the edition that used them is accepted; no automatic deletion is implemented. A3: introduce candidate rules one at a time, with renewed author approval for each isolation/classification revision; no broad OS/runtime filters.

## First observed run: author instructions (prepared, not executed)

The planner first reviews and commits the concrete stage-0 change on the author's approval. The author’s HUM-04 quote must approve only this limited probe purpose, scope and declared harness writes; it does not approve the eventual P4 manual charter. After inspecting first-run artefacts, the author may approve a separately concrete P4 charter. Run the final headless build/tests and pin **after** that commit; changing isolation files afterward invalidates the package. Choose a new run name such as `probe-20261014-04`. All commands below start from the worktree root. They are future author commands, not permission granted to the implementer.

In a **normal, non-administrator PowerShell 7**:

```powershell
$runId = 'probe-20261014-04'
$runDir = Join-Path $env:TEMP ('gcam-todo43/' + $runId)
New-Item -ItemType Directory -Path $runDir
python -B tests/Gcam.Studio.UiTests/Harness/Isolation/stage0.py pin --out (Join-Path $runDir 'pin.json')
```

Create `approvals.json` in that run folder using the pin's `isolationSha256` and `deploymentSha256`, the actual full commit and its `git show -s --format=%cI HEAD` date. Fields: `runId`, `commit`, `committedAt`, `isolationSha256`, `deploymentSha256`, `approvals`. Each approval has `id`, `humanActId` (HUM-01, HUM-04 or HUM-07), `targetCommit`, the **author's actual nonempty `quote`**, and offset `quotedAt` not earlier than the approved commit. No example statement is an approval. The package is local; do not commit personal statements or paths. The harness checks the actual HEAD/date and hashes before launching. Use `probe-YYYYMMDD-NN` with a fresh increasing two-digit suffix; a completed or failed attempt is never reused. Before asking the author to open an administrator terminal, the planner runs this **non-elevated preflight**:

```powershell
python -B tests/Gcam.Studio.UiTests/Harness/Isolation/stage0.py preflight --run-directory $runDir
```

Proceed only on `Preflight PASS`. It checks the complete approval package, actual HEAD/commit date, clean checkout, pin/live isolation/deployment/source hashes, canonical pin self-consistency, run ID, fresh run-folder state, an executable PowerShell 7 and selected provider/keyword coverage. It collects explicit failures rather than starting a trace to discover them. An old pin has no fingerprint specification version and is refused; regenerate it and approvals against the reviewed change. The probe still independently validates source/package/live fingerprints after trace-ready.

On the free desktop, in a separate **administrator PowerShell 7** (the author starts it):

```powershell
$runDir = Join-Path $env:TEMP 'gcam-todo43/probe-20261014-04'
& tests/Gcam.Studio.UiTests/Harness/Isolation/Trace-Probe.ps1 -RunDirectory $runDir -TimeoutSeconds 120
```

`Trace-Probe.ps1` requires PowerShell 7 at parse time; use `pwsh`, never Windows PowerShell 5.1 / `powershell.exe`. This starts only the named instance and writes `trace.ready.json`; it waits for the harness signal. It never launches Studio, changes registry/settings, or cancels another WPR session. Its WPR temporary files and ETL destination are scratch-directed; OS ETW bookkeeping is not asserted to write only scratch. On timeout/abort, finally attempts to stop/save only the instance it started. A completed rerun is a no-op; unresolved prior state is refused. A WPR start/profile failure is a block, not a reason to reuse a global trace. Custom-profile runtime acceptance and loss statistics await this first elevated run.

Back in the **non-administrator** terminal, after `trace.ready.json` exists:

```powershell
$env:GCAM_UI_TESTS = '1'
$env:GCAM_UI_PROBE = '1'
$env:GCAM_UI_PROBE_RUN = 'probe-20261014-04'
$env:GCAM_UI_PROBE_DIRECTORY = Join-Path $env:TEMP 'gcam-todo43/probe-20261014-04'
dotnet test tests/Gcam.Studio.UiTests -c Release --no-build --filter FullyQualifiedName~ObservedRunTests --logger "trx;LogFileName=probe.trx" --results-directory $env:GCAM_UI_PROBE_DIRECTORY
```

These environment values are confined to that terminal process; close it after the run. Do not set the variables for ordinary headless tests. The probe idles for 5 s; graceful close is bounded at 5 s, wrapper timeout 120 s. Allow about **1–3 minutes** including test-host/trace-start/stop/export overhead; this is a scheduling estimate, not a measured runtime.

When the wrapper has stopped, export **locally** and classify (normal terminal; `tracerpt` may require administrator access to the local ETL on this machine):

```powershell
tracerpt (Join-Path $runDir 'probe.etl') -o (Join-Path $runDir 'events.xml') -of XML -summary (Join-Path $runDir 'trace-summary.txt') -export (Join-Path $runDir 'schema.man') -lr
tracerpt (Join-Path $runDir 'probe.etl') -rts -o (Join-Path $runDir 'events-rts.xml') -of XML -lr
python -B tests/Gcam.Studio.UiTests/Harness/Isolation/stage0.py classify --trace (Join-Path $runDir 'events-rts.xml') --context (Join-Path $runDir 'trace-context.json') --summary (Join-Path $runDir 'trace-summary.txt') --schema (Join-Path $runDir 'schema.man') --out (Join-Path $runDir 'classified.json')
```

The parser accepts explicit fixture/metadata labels and leaves unfamiliar provider/opcode data unknown. The first real export may need a reviewed normalisation rule, then its own approved observed pilot; do not guess numeric ETW semantics or suppress unrelated candidates broadly. Check trace loss and channel evidence before any promotion: the context's `traceHealth` starts unproven, and zero lost events/buffers must be supported by the retained trace summary, never filled from an assumption. A2: the probe writes a harness-owned registry control after `trace.ready.json` is validated and before Studio launch: `HKCU\Software\GcamUiaProbe\<run-id>`, one `REG_SZ` value named `RunId` whose data is the run ID. The namespace separates harness evidence from Studio; the key and value both identify the run. Native create disposition refuses an existing run key without overwriting it; choose a new run ID if refused. The key is retained even after a failed/incomplete write, and markers are never deleted by the harness. Each later cleanup needs the author’s explicit approval. The control record contains the harness PID, exact HKCU/native key identities, value declaration and write interval. A successful recognised set-value event at that exact key/name proves the channel; value payload is checked if exported, otherwise the run identity is established by the key path. Missing/unmapped marker evidence remains **instrument-not-proven**. The filtered Kernel-Registry manifest provider retains marker and mutation/name-association events; actual profile/export mapping still requires the observed run. Other registry candidates remain unknown under existing rules; A3 forbids broad actor/provider exclusions.

MR6: network observation is **not applicable**, rather than a blind spot or a positive control, because current Studio source has no network code or authorised external connection. Source evidence: `src/Gcam.Studio/App.xaml.cs:17`, `src/Gcam.Studio.Services/SimulationService.cs:105` and the `app-network-process` surface. Both trace context and writeAudit record this decision and evidence. Unexpected observed connections are still undeclared effects/unknown candidates; not-applicable does not filter them. A4 retains the raw ETL and run folder locally until the edition using them is accepted, without built-in deletion. Raw traces are never committed; public summaries strip destinations and retain raw/context/rule hashes.

Prepared non-elevated diagnostics, optionally run before/after on the approved desktop:

```powershell
& tests/Gcam.Studio.UiTests/Harness/Isolation/Fallback-Probe.ps1 -RunDirectory $runDir -Phase Before
& tests/Gcam.Studio.UiTests/Harness/Isolation/Fallback-Probe.ps1 -RunDirectory $runDir -Phase After
```

A third normal terminal can run `Fallback-Probe.ps1 -RunDirectory $runDir -Phase Watch -TimeoutSeconds 120` while the probe runs, retaining a bounded process/TCP poll stream. It remains diagnostic; no UDP/transient-event absence is asserted.

The fallback observes selected HKCU keys, owned-file hashes and process/TCP snapshots; the launcher supplies 100 ms process polling. Those measurements retain blind spots and **cannot alone qualify this G1 probe**. No fallback was run during headless implementation.

## Proposed AGENTS.UiAutomation wording (planner applies; protected file untouched)

“The manual bundle uses P4. Common safety/ownership gates and per-bundle qualification are separate from product assertions. Existing scopes qualify through current source/deployment pins, observed-run channel controls and pilot evidence. Schema-1 manifests remain historical diagnostics. Final UI manifests are saved after cleanup and linked separately from manual/verification records. Raw machine-wide traces remain local; classification must preserve unknown candidates. A free desktop and author-approved charter/permission package precede launch. Initial qualification runs launch–idle–exit without UIA input/discovery. Capture definitions and quality checks are machine-reviewed; the author judges final figures on their output hashes.”

Open decisions: approve charter/policy/qualification scopes; review real registry-marker channel/export proof and acceptable unresolved native effects (network is not applicable under MR6); set transition policy (A4 fixes local retention until edition acceptance); SM-7, SM-9, overlays and first-edition scope. No stage-0 headless pass is represented as runtime G1 or P4 qualification.


## Fingerprint specification and effect-profile budget

Specification 1 is shared by Python and C#: hash raw file bytes with SHA-256; use slash-normalised relative paths; exclude any relative path component named `bin`, `obj` or `__pycache__`. Isolation includes every other regular file; deployment includes only the case-sensitive suffixes `.dll`, `.exe`, `.json`. Sort the final relative strings by .NET ordinal (UTF-16 code-unit) order, independent of filesystem/path-component/case-insensitive order. Join `path:lowercase-sha256` with LF, without a final LF, encode UTF-8 and hash. The emitted deployment file map has the same canonical order, and its digest is checked against the pin. Cross-language fixtures cover nested punctuation, case, non-BMP names and exclusions. Changes invalidate isolation approval and the observed basis.

The probe selects classic kernel `FileIO`, `FileIOInit`, `Filename`, `Registry`, `ProcessThread` and `Loader`, rather than guessed manifest Event IDs. The installed SDK schema accepts these spellings; `ProcessThread` combines process and thread. The file pair enables operation events, Filename supplies name associations/rundown, Registry supplies key/value operations and key-name/KCB rundown, ProcessThread supplies parent/requesting-thread attribution, and Loader supplies image load/unload evidence. Sources: [WPR keywords](https://learn.microsoft.com/en-us/windows-hardware/test/wpt/keyword--in-systemprovider-), [required file flags](https://learn.microsoft.com/en-us/previous-versions/windows/desktop/xperf/file-io), [FileIo event types](https://learn.microsoft.com/en-us/windows/win32/etw/fileio), [Registry event types](https://learn.microsoft.com/en-us/windows/win32/etw/registry).

The planner accepted bundled reads and queries on 2026-10-14: SystemProvider has no opcode filter. Decoded reads/queries are non-effects, never writes; decoding errors, missing fields, attribution gaps and unmapped opcodes remain unknown. No broad exclusions are added. New individual mappings need HUM-07 approval under A3. No stacks, disk-I/O-detail keywords, sampling or network (MR6) are selected. One collector requests 64 buffers of 128 KiB (8 MiB). This bounds requested buffering, not total file size; never truncate evidence to meet a storage target. Measured channel controls and loss evidence still qualify the next observed run.

Budget about **460 MB per two minutes** until the next observed run measures the new profile. The untrimmed 121-second trace contained 4,009,481 FileIo, 853,931 Registry, 1,776 process, 24,771 thread and 72,856 image events, including 227,948 file reads. Removing network removes only 19,922 events (0.38% of the total), so does not support the old 20–60 MB target. Filename adds required name evidence; machine activity may change the budget. The later 29 MB trace had zero FileIo/Registry events and cannot establish a complete-channel size estimate. Reject missing controls or loss; new profile/mapping rules require HUM-07 reapproval.

Preflight follows the selected profile's collector/provider references and compares explicit effective keywords to the classifier's required channels. Missing file/name/thread, registry or process/image coverage blocks before elevation. Unreferenced declarations cannot satisfy coverage. Unknown channels, inheritance/overrides and unapproved collectors/keywords are refused. After commit/build, regenerate the pin and approvals: isolation changes invalidate the old approval basis. This static check does not replace actual channel controls.

Export locally with tracerpt as above. The classifier streams XML event subtrees (or JSONL), replays the file for owned-descendant discovery, hashes files in chunks and streams every sanitised candidate into a `.candidates.jsonl` sidecar. The summary stores counts and the sidecar/trace/context/rule hashes; it never silently caps unknown candidates. Small JSON arrays remain synthetic-fixture inputs. Raw XML and all identifying export/schema/context files remain local under A4.

The installed xperf dumper also supports [provider filtering](https://learn.microsoft.com/en-us/windows-hardware/test/wpt/dumper). A non-elevated, one-second FileIo-only diagnostic export succeeded (1,292,932 bytes). Its ANSI dump is not the classifier's XML/normalised replay contract; that sampled, filtered dump is not qualification evidence. Do not replace full candidate replay with provider filtering that hides unknown events or loses Unicode, clock or attribution evidence. No xperf adapter is claimed here.

The [documented tracerpt options](https://learn.microsoft.com/en-us/windows-server/administration/windows-commands/tracerpt) provide no provider/opcode dump filter. Non-elevated summary/schema-only export (omit `-o`) proves provider counts without creating an event dump, but cannot classify individual candidates. For repeated classification, stream XML once into a gzip-compressed normalised intermediate. Every normalised event, including unknowns, fields, opaque-payload digests, attribution and clock evidence is retained. The raw ETL/XML remains authoritative for data the normaliser cannot interpret. This reduces replay storage and repeated XML parsing; it does not claim direct filtered ETL export or remove raw evidence.

```powershell
python -B tests/Gcam.Studio.UiTests/Harness/Isolation/stage0.py compact --trace (Join-Path $runDir 'events.xml') --out (Join-Path $replayDir 'events.jsonl.gz')
python -B tests/Gcam.Studio.UiTests/Harness/Isolation/stage0.py classify --trace (Join-Path $replayDir 'events.jsonl.gz') --context (Join-Path $runDir 'trace-context.json') --summary (Join-Path $runDir 'trace-summary.txt') --out (Join-Path $replayDir 'classified.json')
```

Use a new scratch `$replayDir`; retained run folders stay read-only. The generation record links raw/compact hashes, sizes, event count and normalisation hash. Replay refuses stale normalisation; classification verifies the compact digest and links the original trace hash. No deletion is built in. A measured 219,089,757-byte export (215,151 events) produced a 10,826,911-byte compressed intermediate, about 20 times smaller. Reserve the earlier 6.32 GB XML budget for a two-minute complete-channel export, plus compact replay storage; the effect-heavy compression ratio remains unmeasured.


## Evidence-based normalisation and health

`Harness/Isolation/normalisation-rules.json` versions each GUID/opcode/field mapping and carries Microsoft documentation/schema evidence per row. Classic FileIo and Registry opcodes cannot be reused as manifest Event IDs or assigned to unrelated GUIDs. Process/image payload ProcessId identifies the target; execution headers identify emitters. The reader preserves System provider identity and ExtendedTracingInfo.EventGuid separately from rendered labels, parses hexadecimal IDs, and never decodes ProcessingErrorData or opaque binary blobs by guessing offsets. Thread lifecycles can link a requesting thread to its process; missing/recycled thread evidence remains unresolved. FileObject/FileKey name correlations expire at close. Registry open does not establish write access without access evidence.

Image-identity and volume-mapping metadata GUIDs are not file/registry writes. Recognising a metadata kind does not approve an actor exemption or establish absence of effects. Missing acquisition channels cannot be manufactured by normalisation: require actual file and registry effect providers and their positive controls. The classic keyword selections need another observed run before they establish that requirement. New mappings/classification rules require HUM-07 review/approval; retrospective diagnostic replay is not automatic G1 promotion.

Prefer `tracerpt -rts` when wall-clock offsets are inconsistent. Clock type 1 uses decoded Header.StartTime as UTC FILETIME at the header’s raw QPC timestamp, scaled by Header.PerfFreq; type 2 uses raw FILETIME. This follows Microsoft’s TraceEvent implementation and never shifts times to fit a process ledger. Unsupported/missing clock evidence blocks time attribution. Integer arithmetic preserves 100 ns ticks; the ordinary ISO export’s stated offset is not silently rewritten.

Health reader parses exact unique summary counters and records the summary hash. Processed buffers are not lost buffers. If the summary omits lost buffers, only a decoded TRACE_LOGFILE_HEADER.BuffersLost can supply them; record that separate source and XML hash explicitly. Parsed event count must match the summary. Missing, contradictory or duplicate evidence remains unproven. Zero loss does not prove missing provider/channel coverage.

The classifier records the captured normalisation version/hash, schema hash, source and derived context hashes, clock/health evidence, PID provenance and a redacted owned-tree problem table. Destinations are categories, not user/machine paths. Original ETL, XML, context and UI manifest remain untouched; later replay outputs go to a new scratch folder. Raw payloads and investigation results are not committed.
