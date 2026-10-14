# PLAN.Docs.StudioManual.Charter — stage-0 observed run and proposed P4 charter

Scope: the safety basis (G1) for the manual's UI-automation runs. Draft for the planner and author.

Status: **draft, revised 2026-10-14 (MR8)**. The observation method changed from a machine-wide kernel trace to a scoped
before/after comparison that runs without elevation. The next observed run is an `approved-pilot` of this change
(launch–idle–exit). P4 approval (HUM-04) still follows the author's review of run artefacts (MR7).

## Purpose and result use

GCAM Studio is the simulator's engineering viewer, not a measuring instrument or product UI. The P4 bundle describes
procedures that automation actually drove, with automatic checks and readable captures. G1 asks one question: does running
Studio leave anything on the machine outside its declared, isolated surfaces? The method is chosen in proportion to that
question, as the procedure allows: "operating-system write tracing, file-system and registry before/after comparison,
child-process records and external-connection observation, **combined as fits the target**", with filters and blind spots
recorded.

Grounds for proportion: Studio's managed source has no file-writing, network or child-process code
([surface-list.json](../tests/Gcam.Studio.UiTests/Harness/Isolation/surface-list.json)); launch redirects
APPDATA / LOCALAPPDATA / TEMP / TMP to a harness-owned sandbox; Studio runs non-elevated as a 64-bit process, so the OS
refuses machine-wide writes (HKLM, Program Files) and UAC file / registry virtualisation (32-bit processes only) does not
apply. Runs 02–05 traced every kernel
file and registry event and found, after classification, only framework / driver writes now declared (E1, E3), an OS record
(E2) and metadata-only operations that change no state (E4, E5).

## Observation method (MR8)

A **candidate is a state change**: a file or registry value that exists, is gone, or differs after the run. Reads, opens,
queries, image loads, thread events and attribute calls that leave the state unchanged are not candidates. Each observed run
takes three snapshots:

```text
A ── null window (same length as the Studio window, nothing launched) ── B ── marker write, launch, idle, close ── C
```

| Channel | Scope | Recorded per entry | Positive control |
|---|---|---|---|
| file | `%APPDATA%`, `%LOCALAPPDATA%` (incl. real Temp and VirtualStore), `%USERPROFILE%\AppData\LocalLow`, Documents, Desktop, `%ProgramData%`, the Studio deployment folder, the sandbox | path, size, last-write time, attributes | the sandbox diagnostic write appears in B→C |
| registry | all of HKCU | key last-write time; value name, type and content hash | the harness marker `HKCU\Software\GcamUiaProbe\<run-id>` (A2) appears in B→C |
| process | the harness's owned-process ledger (launcher, 100 ms monitor) | PID, image, parent, start / end | the owned Studio process |
| network | not applicable (MR6) | — | — |

**Classification of B→C changes**, in this order:

1. Harness-owned (sandbox, run folder, lease, marker) → harness record.
2. A declared surface: E1 (HKCU `Software\Microsoft\CTF\CUAS\DefaultCompositionWindow`, values `Left` / `Top`) and E3
   (`<ProgramData>\NVIDIA Corporation\Drs\nvAppTimestamps`, content change of the existing file) → app effect inside a
   declared surface.
3. Last-write time outside the Studio window (launch − 1 s … exit + 2 s) → not of this window.
4. The same path also changed in the null window A→B, or matches a recorded background filter (`classification-rules.json`
   `backgroundFilters`, each with its reason and the run that evidenced it) → other-actor record.
5. Anything else → out-of-declaration (stops the run as `unsafe_effect`), or, when the evidence cannot decide, unclassified:
   the planner adds evidence (e.g. a repeat with a longer null window, or one diagnostic kernel trace of that path) and, if
   still undecided, asks the author (HUM-08), whose answer becomes a rule.

G1 passes when every positive control is seen, nothing is out-of-declaration and nothing is unclassified. Blind spots,
recorded in `writeAudit.blindSpots`: locations outside the scope (other users' profiles, removable and network drives,
machine-wide locations a non-elevated process cannot write), a content change that restores size and last-write time, and
a write that is undone before C. HKLM is outside the scope because a non-elevated 64-bit process cannot write it;
the BAM process record (E2) is an OS record there and is not a candidate.

The kernel trace (`Trace-Probe.ps1`, `Probe.wprp`, `stage0.py compact / classify`) stays in the isolation location as an
**optional diagnostic** for a single unexplained change. It is no longer required for G1 and needs no administrator
session in an ordinary observed run.

## Running an observed run

All commands run from the worktree root in a **normal (non-administrator) PowerShell 7**, after the change is committed and
built (`dotnet build Gcam.sln -c Release`). Use a new run name `probe-YYYYMMDD-NN`.

```powershell
$runId = 'probe-20261014-06'
$runDir = Join-Path $env:TEMP ('gcam-todo43/' + $runId)
New-Item -ItemType Directory -Path $runDir
python -B tests/Gcam.Studio.UiTests/Harness/Isolation/stage0.py pin --out (Join-Path $runDir 'pin.json')
# approvals.json: runId, commit, committedAt, isolationSha256, deploymentSha256, approvals[] (HUM-01 / HUM-04 / HUM-07,
# the author's actual quote, quotedAt not earlier than the commit). Personal statements and paths stay local.
python -B tests/Gcam.Studio.UiTests/Harness/Isolation/stage0.py preflight --run-directory $runDir
$env:GCAM_UI_TESTS = '1'; $env:GCAM_UI_PROBE = '1'; $env:GCAM_UI_PROBE_RUN = $runId; $env:GCAM_UI_PROBE_DIRECTORY = $runDir
dotnet test tests/Gcam.Studio.UiTests -c Release --no-build --filter FullyQualifiedName~ObservedRunTests --logger "trx;LogFileName=probe.trx" --results-directory $runDir
python -B tests/Gcam.Studio.UiTests/Harness/Isolation/stage0.py diff-classify --run-directory $runDir --out (Join-Path $runDir 'classified.json')
```

The desktop part (the `dotnet test` line) needs the author's go for each run (HUM-01). The run takes three snapshots
(about 80 s each on the original machine: ~1.1 M files, ~140 k HKCU values) plus the 12 s null window and the Studio window —
about 4.5 minutes; `diff-classify` takes about a minute more. Snapshots name machine paths and stay local with the
run folder (A4); the repository keeps only categories, counts and hashes.

## Isolation location, fingerprint and approvals

Isolation location: `tests/Gcam.Studio.UiTests/Harness/Isolation/` — launcher and cleanup, path and launch values, guard and
policy, ledger, monitor and lease, source linkage, snapshot and classification rules, survey and the diagnostic trace tools.
`stage0.py pin` records its fingerprint with the source and deployment identities. Fingerprint specification 1 (shared by
Python and C#): SHA-256 of raw file bytes; slash-normalised relative paths; path components `bin`, `obj`, `__pycache__`
excluded; deployment keeps only `.dll`, `.exe`, `.json`; sort by .NET ordinal order; join `path:lowercase-sha256` with LF,
no final LF, UTF-8, hash. Preflight checks the approval package, HEAD and commit date, clean checkout, pin against the live
fingerprints, a fresh run folder and the run ID.

A change in the isolation location is a permission change: commit, then the author's HUM-07 on that commit, then an
`approved-pilot` observed run. One "실행 허락" after a commit records HUM-01, HUM-04 (probe scope) and HUM-07 for it.

Human acts: HUM-01 desktop go per run; HUM-04 the probe charter now, the P4 charter after the run artefacts (MR7); HUM-07
each isolation or rule change; HUM-08 only for a change the evidence cannot decide; HUM-09 exclusions; HUM-10 final
figures; HUM-12 static chapters and edition; HUM-13 intentional baseline differences. The runner never asks the author to
operate Studio.

Adoption: `adoptionMode: existing`. Legacy regression and survey scopes stay historical / diagnostic until they are re-run
under a G1 basis (transition list owner: planner; review deadline 2026-11-13).

## Decisions carried forward

| ID | Decision | Status |
|---|---|---|
| A2 | Registry positive control: harness marker `HKCU\Software\GcamUiaProbe\<run-id>`, value `RunId`; never overwritten, never deleted by the harness | author |
| A3 (revised 2026-10-14) | Recorded background filters are allowed, each with its reason and evidencing run; adding or changing one is a rule change (HUM-07). Replaces "no broad filters" | author (MR8) |
| A4 | Raw evidence (snapshots, traces, run folders) stays local until the edition that used it is accepted | author |
| E1, E3 | Declared confirmed app write surfaces with residual risks (the IME composition-window position; the GPU driver's per-application timestamp store) | author, option 2 |
| E2 | BAM process record: OS record (outside the HKLM-free scope) | rule RC-03 |
| E4, E5 | Metadata operations with no state change: not candidates under MR8. The trace-classifier rules RC-04 / RC-05 remain for the optional diagnostic | MR8 |
| MR6 | Network not applicable | author |

## Record of the MR8 runs

Run 06 (2026-10-14, commit `0433c94`, approved pilot, launch–idle–exit, no elevation): three snapshots of ~1.1 M files and
~140 k HKCU values, about 40 s each; all three positive controls seen; B→C 159 changes: harness 11, declared 1 (E3),
outside the Studio window 117, null-window churn 3, out-of-declaration 27. All 27 were other tools running beside the probe
(other repositories' git internals, an AI coding session's TEMP files and scratch folder, a packaged app's cache, IDE
telemetry, the cloud-sync client, the shell's window-session record). They became eight recorded `backgroundFilters`
(evidencedBy run 06; a filter may be limited to one entry kind). Re-classified with them, run 06 has nothing out of
declaration. That replay is not a G1 basis: the filters are a rule change and need their own approved pilot.

## Record of the kernel-trace runs (02–05)

Runs 02–05 (2026-10-14) used the elevated kernel trace. Run 05 (commit `e5502f3`): all three positive controls, zero events
and buffers lost; harness device-form roots proven (602 sandbox writes attributed to the harness); E1 2, E2 1, E3 2 applied;
30 Studio-tree metadata operations left out of declaration (E4 21, E5 9) and 3,668 events of unmapped kinds (reads, image
metadata, open-or-create) left unknown. The added `Microsoft-Windows-Kernel-Registry` provider produced no events (the
collector session is in the ETL, the provider is not); not pursued under MR8. The traces stay local (A4); the decision
history is in git (this file before MR8).
