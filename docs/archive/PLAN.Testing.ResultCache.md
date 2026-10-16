# PLAN.Testing.ResultCache — source-determined binaries and cached desktop results

Scope: TODO-51. Make a build of the same source give the same bytes (step 1), then reuse a desktop test's latest pass
while nothing that can change its outcome has changed (step 2), so regression and observed runs stop repeating work that
a docs- or test-only commit cannot affect.

Status: steps 1 and 2 implemented 2026-10-16 (branch `todo51-build-identity`): step 1 by the planner, step 2 by a substitute
agent and audited by the planner (normal suite 1,182 passed / 63 skipped, catalog 239, test-record self-test 28, M1 replay
byte-identical). Step 2 decisions taken in the audit: a failed run removes the case's entry (a later cache run must not
reuse a pass the latest run contradicted); `surface-list.json` names `cache.json` (an isolation change, folded into the
new pilot the step-1 binaries need anyway); `PolishSurveyTests` is never reused. **Done 2026-10-16** (desktop session on
`a927311`, author's go): a normal run of the four report-export cases and the chrome case (5 passed) wrote `cache.json`
with one entry each; the same selection with `GCAM_UI_CACHE=1` skipped all five as `[cache-reused]` (0.7 s). The same
session's approved pilot `probe-20261016-03` completed on the step-1 binaries. A desktop-profile collection never reuses:
the collector sets `GCAM_UI_CACHE=0` and refuses a record with the cache on (test-record self-test), so it was not re-run.
History: proposed by the author on 2026-10-16 ("파일럿 결과를 캐싱하면 회귀 테스트를 좀 더 빠르게 할 수 있지 않을까?",
then "그렇게 해").

## Why (measured 2026-10-16)

- The .NET SDK appends the git commit to the informational version: `Gcam.Studio.dll` read `1.0.0+f029b3bc…`. Every
  commit therefore changed every assembly's bytes and the deployment fingerprint, though no product source changed between
  `10b55bf` and `46c9ada`.
- The procedure already reuses an observed run while its `validFor` (product build, isolation fingerprint, path rules,
  image set) is unchanged; with a commit-dependent build that reuse never applied. On 2026-10-16 the pilot and the observed
  manual batch were repeated on `46c9ada`, and the manual chapters were rerun twice, only because the bytes changed.
- The 32 recorded regression scenarios (about 11 min) were run three times in one day; most runs followed commits that
  touched no product source.

## Step 1 — source-determined binaries (done)

`Directory.Build.props`: `IncludeSourceRevisionInInformationalVersion=false` and `EnableSourceControlManagerQueries=false`.
The first removes the commit from the informational version; the second stops SourceLink writing it into each PDB (the DLL
records its PDB's content identity, so with the first alone every DLL except the apphost still followed the commit).
Deterministic compilation was already the default.

| Check | Result |
|---|---|
| The commit string is gone from `Gcam.Studio.dll` | `1.0.0` only |
| Two builds of commits that differ only in docs give the same deployment fingerprint | with the first setting alone: 9 of 10 DLLs differed; with both: `120f2085…` on both commits, no file differs |
| Normal suite | 1,171 passed / 63 skipped, 0 failed; catalog 238 units |
| Provenance | unchanged in substance: run records, `pin.json` and the video index record the source commit beside the deployment fingerprint; a binary maps to its source by a rebuild with the same fingerprint |

Trade-off: the report export's header ("Studio version") and the event-data metadata (`generator`) show `1.0.0` instead
of `1.0.0+<commit>` (`ReportExportService.Version`). The exported scene file and the run records keep the provenance a
reader needs. Same-folder caveat: the PDB path is embedded, so the fingerprint is the same per checkout folder, not across
worktrees.

## Step 2 — cached desktop results (proposed; verify in the implementer's turn)

| ID | Proposal |
|---|---|
| C1 | **Cache key per desktop test case**: the deployment fingerprint; the source hashes of that test class's catalog entry (`docs/VV.Tests.md` already lists them per class, including the harness files it uses); the isolation fingerprint; the environment (OS build, display size and DPI, UI culture); and the test's own opt-in switches |
| C2 | **Store**: the latest pass per case beside the videos (`artifacts/ui-videos/index.json` already records the run, commit, binary and deployment hashes). A cache entry names its source run; a failed or skipped run never becomes a cache entry |
| C3 | **Use**: `GCAM_UI_CACHE=1` makes a desktop test whose key matches a pass younger than its expiry return early as "reused from run <id>" (an xUnit skip with a fixed reason code, never a pass); default off, so a plain desktop run still runs everything. A command lists which cases would run and why (key part that changed) |
| C4 | **Expiry and full runs**: an entry expires after 7 days or when the OS build changes; the cache never applies to the observed run itself, the manual batch that generates an edition, or a run with `GCAM_UI_BREAK_VERDICT=1` |
| C5 | **Test records (STR)**: a reused case is reported as reused with its source run and key, separately from executed passes (the procedure's `reused` / `sourceRunId`); the desktop profile collector refuses the cache unless asked |
| C6 | **Observed runs**: with step 1, `validFor` reuse works as the procedure intends; the planner's run checklist states when a new pilot is needed (product source, isolation location or rules changed) |

## Done when

- Step 1: the same source gives the same deployment fingerprint across commits (checked), the normal suite passes.
- Step 2: a docs- or test-only commit reruns only the desktop classes whose key changed; reuse is visible as reuse in the
  run output and in test records; expiry and the exclusions hold (unit tests on fixtures); the full desktop run with the
  cache off is unchanged.

## Not here

- Caching headless tests (they are fast and run in CI).
- Reproducible builds across machines or folders (the PDB path).
