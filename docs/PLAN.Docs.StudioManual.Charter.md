# PLAN.Docs.StudioManual.Charter — stage-0 probe and proposed P4 charter

Scope: draft for the planner and author; source survey, qualification and later manual use. No desktop/elevation or registry observation has been performed.

Status: **draft, not approved**. This file is not a HUM-04 approval. MR1–MR4 establish direction; the concrete charter, permission package and unresolved observation policy still require the author's actual statements on the reviewed commit.

## Purpose, readers and result use

GCAM Studio is the simulator's engineering viewer. The eventual P4 bundle describes procedures actually driven by automation, with automatic checks and safe readable captures, for repository users. It is not a measuring instrument or a regulated clinical product UI. The initial probe is a separate P1 safety investigation: launch–idle–exit, no UIA input/tree dump, no normalisation, no simulation acquisition. Its successful launch alone does not qualify G1 or P4.

Scope: the current Studio deployment and the harness's launcher, isolation/policy, process/lease, record, trace classifier and capture contracts. Exclusions: real hardware, publication, external services, installs, OS changes, arbitrary process termination, personal/production data, and all desktop operations while the author is remote. A full P4 charter will also map chapters, requirements, risks and environmental exclusions after SM-7/SM-9/overlay/edition decisions.

Success criteria: source/deployment/isolation identities agree; all resolved effects are declared or remain explicit unknowns; no undeclared product effect or unresolved trace candidate; each required channel has a positive control; cleanup/lease release are recorded independently from product outcomes. Later P4 needs all G0–G6, independent product verdicts and final edition figure review. Empty/filtered traces and missing controls are blocked, never passed.

Roles (proposed): author sets scope, authority, source acceptance, exclusion risk and final figure/edition decisions; planner prepares the concrete commit/package and audits/replays results; implementer builds and tests headless components; non-elevated runner drives Studio; administrator runs only the named tracing wrapper. Raw ETL and identifying event/context details remain local. Public records contain filtered classifications/hashes and role identifiers, not machine paths/accounts.

## Existing-harness adoption and transition

`adoptionMode: existing`. Initial qualification scopes: isolation/launch/path rules, process ledger/monitor, desktop lease, policy/selector inventory, verdict/mutation, manifest finalisation, trace/classification, capture/template. Each manual scenario later depends on the scopes it uses and cannot outrank them. Current status is **blocked/unqualified**, not promoted by historical P1 records.

Transition list (draft owner role: planner; review deadline 2026-11-13): untouched legacy regression and survey scopes remain historical/diagnostic-only; current source assertions and original raw records remain intact. Their use for P2/P4 requires current source/binary/input/environment linkage, negative controls and an observed safety basis. No blanket rewrite of old evidence and no new coverage count from headless CI.

Human-act plan: HUM-01 for the free desktop/probe go, HUM-04 for this concrete charter, HUM-05 for policy scopes (initial probe has no accessibility/device interactions; later P4 scope is undecided), HUM-07 for the reviewed isolation/policy/classification commit before using it, HUM-08 only for candidates that improved machinery still cannot classify, HUM-09 for later exclusions, HUM-10 for final figures/new capture definitions, HUM-12 for controlled static-source/edition disposition, HUM-13 for intentional baseline differences or expiry extensions. Device actions, real-world effects and identity disclosure are outside scope. The runner does not ask the author to operate Studio to establish an observed-run gate. The matching document process's source and figure/edition decisions target hashes, with no duplicate figure-approval request.

## Survey summary and unresolved boundaries

The structured inventory is [surface-list.json](../tests/Gcam.Studio.UiTests/Harness/Isolation/surface-list.json). Reachable managed Studio state/calibration/theme work is in memory; ambient data are read beside the deployment and offer data are compiled resources. Save/study APIs in engine libraries are not exposed by Studio's current DI entry points. The conservative deployed metadata survey retains all managed/native references, including methods present but not proven reachable. The pin connects the actual Release build/deployment with exact source hashes; it is not a runtime audit.

The harness writes marked temporary records, lease state and approved trace outputs; old survey scenarios explicitly write repository assets and are excluded from the first probe. Launch redirects APPDATA/LOCALAPPDATA/TEMP/TMP. **Environment redirection does not prove WPF/CLR/native known-folder, registry, crash-reporting or shell effects absent.** These remain unknown until observation/classification. The machine's lack of a disposable environment is not replaced by a static absence claim. Stop on an undeclared app effect or unresolved attribution; revisit scope if containment cannot be established.

Isolation location: `tests/Gcam.Studio.UiTests/Harness/Isolation/`. Its content hash includes launcher/cleanup, path/launch values, guard/policy, ledger/monitor/lease, source linkage, classification/external-connection rules, survey and trace/fallback tools. `stage0.py pin` records this fingerprint plus source/deployment identities. New/renamed/removed targets and unknown bindings fail the static policy check; an unapproved policy never establishes qualified use. No deletes are used for fixture/trace cleanup.

## First observed run: author instructions (prepared, not executed)

The planner first reviews and commits the concrete stage-0 change on the author's approval. Run the final headless build/tests and pin **after** that commit; changing isolation files afterward invalidates the package. Choose a new run name such as `probe-20261013-01`. All commands below start from the worktree root. They are future author commands, not permission granted to the implementer.

In a **normal, non-administrator PowerShell 7**:

```powershell
$runId = 'probe-20261013-01'
$runDir = Join-Path $env:TEMP ('gcam-todo43/' + $runId)
New-Item -ItemType Directory -Path $runDir
python -B tests/Gcam.Studio.UiTests/Harness/Isolation/stage0.py pin --out (Join-Path $runDir 'pin.json')
```

Create `approvals.json` in that run folder using the pin's `isolationSha256` and `deploymentSha256`, the actual full commit and its `git show -s --format=%cI HEAD` date. Fields: `runId`, `commit`, `committedAt`, `isolationSha256`, `deploymentSha256`, `approvals`. Each approval has `id`, `humanActId` (HUM-01, HUM-04 or HUM-07), `targetCommit`, the **author's actual nonempty `quote`**, and offset `quotedAt` not earlier than the approved commit. No example statement is an approval. The package is local; do not commit personal statements or paths. The harness checks the actual HEAD/date and hashes before launching.

On the free desktop, in a separate **administrator PowerShell 7** (the author starts it):

```powershell
$runDir = Join-Path $env:TEMP 'gcam-todo43/probe-20261013-01'
& tests/Gcam.Studio.UiTests/Harness/Isolation/Trace-Probe.ps1 -RunDirectory $runDir -TimeoutSeconds 120
```

This starts only the named instance and writes `trace.ready.json`; it waits for the harness signal. It never launches Studio, changes registry/settings, or cancels another WPR session. Its WPR temporary files and ETL destination are scratch-directed; OS ETW bookkeeping is not asserted to write only scratch. On timeout/abort, finally attempts to stop/save only the instance it started. A completed rerun is a no-op; unresolved prior state is refused. A WPR start/profile failure is a block, not a reason to reuse a global trace. Custom-profile runtime acceptance and loss statistics await this first elevated run.

Back in the **non-administrator** terminal, after `trace.ready.json` exists:

```powershell
$env:GCAM_UI_TESTS = '1'
$env:GCAM_UI_PROBE = '1'
$env:GCAM_UI_PROBE_RUN = 'probe-20261013-01'
$env:GCAM_UI_PROBE_DIRECTORY = Join-Path $env:TEMP 'gcam-todo43/probe-20261013-01'
dotnet test tests/Gcam.Studio.UiTests -c Release --no-build --filter FullyQualifiedName~ObservedRunTests --logger "trx;LogFileName=probe.trx" --results-directory $env:GCAM_UI_PROBE_DIRECTORY
```

These environment values are confined to that terminal process; close it after the run. Do not set the variables for ordinary headless tests. The probe idles for 5 s; graceful close is bounded at 5 s, wrapper timeout 120 s. Allow about **1–3 minutes** including test-host/trace-start/stop/export overhead; this is a scheduling estimate, not a measured runtime.

When the wrapper has stopped, export **locally** and classify (normal terminal; `tracerpt` may require administrator access to the local ETL on this machine):

```powershell
tracerpt (Join-Path $runDir 'probe.etl') -o (Join-Path $runDir 'events.xml') -of XML -summary (Join-Path $runDir 'trace-summary.txt')
python -B tests/Gcam.Studio.UiTests/Harness/Isolation/stage0.py classify --trace (Join-Path $runDir 'events.xml') --context (Join-Path $runDir 'trace-context.json') --out (Join-Path $runDir 'classified.json')
```

The parser accepts explicit fixture/metadata labels and leaves unfamiliar provider/opcode data unknown. The first real export may need a reviewed normalisation rule, then its own approved observed pilot; do not guess numeric ETW semantics or suppress unrelated candidates broadly. Check trace loss and channel evidence before any promotion: the context's `traceHealth` starts unproven, and zero lost events/buffers must be supported by the retained trace summary, never filled from an assumption. The current probe defines file/process controls; registry has no guaranteed declared effect, so its proof remains **instrument-not-proven** unless an approved registry control or already proven channel evidence is added. Network has no declared product traffic and is an explicit blind spot. Resolve these with the author rather than pretending a quiet channel proves absence. Raw traces are never committed; public summaries strip destinations and retain raw/context/rule hashes.

Prepared non-elevated diagnostics, optionally run before/after on the approved desktop:

```powershell
& tests/Gcam.Studio.UiTests/Harness/Isolation/Fallback-Probe.ps1 -RunDirectory $runDir -Phase Before
& tests/Gcam.Studio.UiTests/Harness/Isolation/Fallback-Probe.ps1 -RunDirectory $runDir -Phase After
```

A third normal terminal can run `Fallback-Probe.ps1 -RunDirectory $runDir -Phase Watch -TimeoutSeconds 120` while the probe runs, retaining a bounded process/TCP poll stream. It remains diagnostic; no UDP/transient-event absence is asserted.

The fallback observes selected HKCU keys, owned-file hashes and process/TCP snapshots; the launcher supplies 100 ms process polling. Those measurements retain blind spots and **cannot alone qualify this G1 probe**. No fallback was run during headless implementation.

## Proposed AGENTS.UiAutomation wording (planner applies; protected file untouched)

“The manual bundle uses P4. Common safety/ownership gates and per-bundle qualification are separate from product assertions. Existing scopes qualify through current source/deployment pins, observed-run channel controls and pilot evidence. Schema-1 manifests remain historical diagnostics. Final UI manifests are saved after cleanup and linked separately from manual/verification records. Raw machine-wide traces remain local; classification must preserve unknown candidates. A free desktop and author-approved charter/permission package precede launch. Initial qualification runs launch–idle–exit without UIA input/discovery. Capture definitions and quality checks are machine-reviewed; the author judges final figures on their output hashes.”

Open decisions: approve charter/policy/qualification scopes; decide registry/network instrument proof and acceptable unresolved native effects; set transition/retention policy; SM-7, SM-9, overlays and first-edition scope. No stage-0 headless pass is represented as runtime G1 or P4 qualification.
