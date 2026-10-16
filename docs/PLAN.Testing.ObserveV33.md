# PLAN.Testing.ObserveV33 — attributable-change observation (UI automation guide v3.3)

Scope: TODO-52. Bring the observed run's classification in line with the author's UI-automation procedure v3.3 (approved
2026-10-16, safety module §3): **a candidate is only a change the run can attribute to the app**. Changes outside the
observation scope, with no harness marker and no process attribution, are counted as `unattributed` and never block a
run. Then run the approved pilot this needs together with TODO-51's open desktop check.

Status: D1–D4 approved by the author 2026-10-16 ("넷 다 승인. 진행해."); next: one implementer turn, then one desktop
session on the author's go.

## Why (2026-10-16)

The 2026-10-16 runs were blocked or delayed only by other programs: a messenger's settings, the certificate revocation
cache, the Store's publisher folder, the shell's MuiCache. Each needed a recorded background filter, a rule change
(HUM-07) and another run. Under v3.3 none of these is a candidate: they lie outside every declared surface, carry no
harness marker and are not attributed to Studio's process. The procedure session reported the same pattern: five pilots,
zero app effects, all blocked by browser, cloud-sync and office records.

## What v3.3 requires (procedure §3, summarised)

A change in the app window (B→C) is a candidate when one of these holds:

1. **Inside the observation scope**: the declared defaults and this run's resolved values of every write surface, their
   isolation locations, harness-owned locations (owner marker) and the **shell record locations** (where the shell or OS
   records data the app handed it: recent documents, jump lists, open / save dialog history). A shared location enters only
   by the surface's own name pattern; a surface without its own pattern goes to `blindSpots`.
2. **Carries a harness marker** in its name or content, anywhere.
3. A **process or connection** of a registered image or its descendants.
4. A write **attributed by a per-process record** (an OS write trace) to a registered image or its descendants.

Everything else is not a candidate: not classified, not `unknown`, not blocking; its count and list location go to
`writeAudit.unattributed`. The blind spot this leaves (an app effect outside the scope without a marker) is written to
`blindSpots`; a product where that blind spot is large either adds (4) or records the decision not to and its residual
risk in the charter's observation method (HUM-04). Records the OS keeps about a process's own run and windows (prefetch,
window session, compatibility store, ...) stay other-actor records even when they contain the app's path or a marker.
Adopting this definition or changing how the scope is derived is a permission expansion: HUM-07 with an approved pilot,
and HUM-04 for the charter's observation method. Manifest `schemaVersion` 10 adds `observationScope`, `unattributed`
and `controlWindow`.

## Facts in the code (premises; the implementer re-checks each)

| Fact | Where | Consequence |
|---|---|---|
| `diff-classify` treats every B→C change in the snapshot roots as a candidate and needs a filter or rule for anything that is not the app's | `tests/Gcam.Studio.UiTests/Harness/Isolation/stage0.py:1274` (`diff_classify`), `classification-rules.json` `backgroundFilters` | the candidate filter goes before classification; most background filters become unnecessary |
| Snapshots store a registry value's content **hash** only; a marker inside value data cannot be seen afterwards | `StateSnapshot.cs:126` (`h = IsolationPaths.Hash(value.Data)`); `stage0.py:1219` | candidate 2 needs a per-value flag written at snapshot time (the run ID is known then) |
| The surface list declares each surface's destination as text, not as patterns the classifier can read; three framework rows are `unknown` (`framework-registry`, `framework-cache-shell`, `framework-crash`) | `surface-list.json:31`–`:33` | each surface needs machine-readable scope entries; the three `unknown` rows need a decision (D2) |
| The E6 save-dialog history is today a declared app surface; under v3.3 it is a shell record location (data the app handed the shell) | `classification-rules.json` `declaredAppSurfaces` E6 | it moves to a shell-record list kept in the isolation location; the classification result stays "app effect, declared" |
| The run manifest writes `writeAudit` without `observationScope`, `unattributed` or `controlWindow` | `tests/Gcam.Studio.UiTests/Harness/UiRunManifest.cs:93` | add the three fields; existing records keep their schema |
| Every Studio launch path passes the run ID (`GCAM_UIA_RUN`) and the sandbox name holds it (`gcam-uia-<run>`) | `IsolationPaths.cs:17`, `OwnedStudioProcess.cs` | paths the app hands the shell (export paths) already carry a marker; the export file names can carry one too (procedure recommends markers in non-displayed inputs) |

## Decisions (author, 2026-10-16: all four recommendations approved)

| # | Question | Recommendation |
|---|---|---|
| D1 | Adopt the v3.3 candidate definition for G-CAM's observed runs | **Yes.** It removes the class of failures seen today without weakening what G1 asks about Studio |
| D2 | The three `unknown` framework rows | **Close them as blind spots with stated residual risk**, not with a process-level trace: Studio's managed code has no write, network or child-process code (checked against the deployed IL each run); what remains is native framework and driver behaviour, already seen as E1 and E3 in the 2026-10-14 kernel traces. The charter's observation method records "no per-process record (4); residual risk: a framework or driver write outside the scope without a marker" (HUM-04) |
| D3 | Background filters | **Retire the filters that lie outside the new scope** (all but those touching a scope location, which v3.3 forbids as filters anyway); keep them in the rule history with the run that evidenced them |
| D4 | Markers in export file names | **Yes, in the harness's typed paths only** (not in figures the manual shows): the save dialog's history then carries the run ID, so candidate 2 holds even if a shell location is missing from the list |

## Proposed work (one implementer turn)

| ID | Work |
|---|---|
| W1 | `surface-list.json`: machine-readable scope entries per surface (location kind, path or key pattern, name pattern, basis: declared default / resolved value / isolation location); the three framework rows resolved per D2 |
| W2 | A shell-record list in the isolation location (save / open dialog history, recent documents, jump lists), with E6 moved there |
| W3 | `StateSnapshot` (C#) and `stage0.py`: a marker flag per registry value and per path (run ID in the name or value data), computed at snapshot time; files by name only (content scanning stays out: cost) |
| W4 | `diff-classify`: candidates = scope ∪ marker ∪ owned process; the rest counted and listed (local) as `unattributed`; `observationScope`, `controlWindow` and `blindSpots` in the result; background filters per D3. Schema bump; the 2026-10-14 … 16 runs re-classify under the new rules as a check (not as G1 evidence) |
| W5 | `UiRunManifest`: `schemaVersion` with the three fields |
| W6 | Tests on synthetic snapshots: an out-of-scope change is unattributed and does not block; an out-of-scope change with a marker is a candidate; an OS record of the process's own run with a marker stays other-actor; a scope change classifies as before; filters cannot cover scope locations |
| W7 | Docs: `AGENTS.UiAutomation` (observation method), charter (repo and the Confluence pages: observation method and residual risk, HUM-04), Rationale rows, catalog |

## Desktop session (author's go)

1. **Approved pilot** (HUM-07 + HUM-04 on the commit): launch–idle–exit under the new rules. It also qualifies the
   source-determined binaries of TODO-51 step 1.
2. **TODO-51 cache check**: one affected class run twice, first normally (writes `cache.json`), then with
   `GCAM_UI_CACHE=1` (shows `[cache-reused]` skips), and the desktop-profile collector refusing the cache.

## Done when

- An observed run with today's background activity completes with Studio's effects classified and other programs'
  changes counted as `unattributed`.
- The charter states the observation method, the scope's derivation and the residual risk; the author approved it.
- The approved pilot passed on the merged build; the cache check showed write, reuse and refusal.

## Not here

- A per-process OS write record (D2 keeps it out; the kernel-trace tools stay as an optional diagnostic).
- Re-qualifying the regression scopes (separate transition item, review date 2026-11-13).
