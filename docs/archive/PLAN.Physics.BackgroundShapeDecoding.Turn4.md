# TODO-33 turn 4: sampling budget and parallel timing handoff

Status: prepared; both selection-only pilot batches completed. Validation and confirmation were not run. The planner starts the two long jobs after this turn.

## Files changed in this turn

- Sampling/provenance: new `samples/evidence/background-shape/request-v2.json` and `samples/evidence/manifest-background-shape-v2.json`.
- Probe: new `samples/evidence/probe/BackgroundRequest.cs`; modified `BackgroundShapeRecipe.cs`, `BackgroundSensitivityRecipe.cs`, and `Program.cs` in the same directory. Legacy modes remain available; new modes dispatch v2 explicitly.
- Analysis: modified `samples/evidence/background-shape/aggregate_background.py`; new `parallel_timing.py` in that directory. V2 aggregation refuses missing seeds, wrong request/pin/iterations and incomplete 100-acquisition estimator rows; analysis inputs bind the request, pin and seed catalogue. Timing analysis also binds its imported aggregation helper.
- Tests: three added cases in `samples/evidence/tests/test_background_shape.py`: budget/physics immutability, phase-seed separation and conditional screen cutoff.
- Report: this file. No product source, existing result, seed catalogue, original request, original manifest, pin, calibration record or VV document was edited in this turn. Other pre-existing working-tree edits are outside this turn.

## Versioning and unchanged selection

The v2 request is a copy of v1 with ValidationRepeats=100, PilotRepeats=4, and a BudgetRevision object binding the v1 request and immutable pin. The pilot repeat choice is timing only. Both C# recipes reject any other change to the selected physics fields. The pin continues to reference the identical v1 physics request; the v2 execution request explicitly references both. The iteration count remains 400, all 75 pinned regimes and their targets remain unchanged, as do the 10,000 whole-seed simultaneous bootstrap resamples, bootstrap seed 131071, phase lists and unchanged gate thresholds.

| File | SHA-256 |
|---|---|
| `background-shape/request-v1.json` | `847b6e8b97e0ec87ea0a26d3ed3ce30038ca223b04ac1a91b8d6b1c37afd849b` |
| `background-shape/request-v2.json` | `24cf9a75157f527008a7ee1ad8a9290d40693013120e6ce78b8d8e1ad1cd69b8` |
| `background-shape/pinned-v1.json` | `3c44d28d8c3484a2ae89d7a272cec492377120d8a9da9af7c8de28556aa94c24` |
| `manifest-background-shape-v1.json` | `e0081eef7ec6b6594665cf85e7a31f3ab20c49240c54994d98b742fb457509df` |
| `manifest-background-shape-v2.json` | `83c2c90669a9ce5fde22071cf82f21cd59c22ee1598266bb8fbf3650df34da0a` |
| `seeds.json` | `1de7a0c695314acd968e0a3910c2f41b44ad8d7ffd91b5907ac965942fbd1c82` |

New families: `background_shape_validation_v2` (32 BG_VALIDATION32 seeds), `background_shape_sensitivity_v2` (same 32 seeds), `background_shape_pilot_v2` and `background_shape_sensitivity_pilot_v2` (all 16 BG_SELECTION16 seeds each). The driver snapshots the complete inputs/executable closure into each seed attempt and binds its outputs in done.json; no provenance escape or changed-config switch was used.

## Conditional binomial screens

Validation and sensitivity each use 32 outer seeds x 100 acquisitions = 3,200 acquisitions per condition and estimator. Calibration/transport remains a seed cluster, so these are conditional acquisition screens, not 3,200 independent calibration acquisitions.

For each association and jointly trusted-association screen, solve BetaQuantile(.05, k, 3200-k+1) >= .95. Minimum k=3,061 successes, maximum 139 failures (95.65625% observed). The bound at 3,061 is 0.9501612431519745; at 3,060 it is 0.9498287113184153. Both screens must pass before a pinned regime can receive its simultaneous signed-vector pass verdict.

For reference only, the existing 1% conditional false-trust screen at this N permits at most 22 events: BetaQuantile(.95,23,3178)=0.009802677617222514, while 23 events give 0.010167671172016007. There is no new null-gate calibration or E4 trust guarantee in this turn. All-success lower95 is 0.9990642717315064. These numerical screens are derived from the specified conditional CP equations, not borrowed tolerances.

## Completed parallel pilots

Two foreground-waited driver invocations, sequential batches with 16 workers. Each used the complete stage-1 grid or the complete targeted sensitivity grid, independent full-size transport calibrations, four repeats per condition, and 400 iterations. Each invocation completed all 16 selection seeds with zero failed runs before the next was started; no job remains running. The raw numerical JSON is LF.

Seeds (BG_SELECTION16 only): 530001, 634730, 739459, 844188, 948917, 1053646, 1158375, 1263104, 1367833, 1472562, 1577291, 1682020, 1786749, 1891478, 1996207, 2100936. Development seeds 330001 / 330007 / 330019 were not needed. No validation or confirmation seed was run.

Main: 448 conditions, 16 x 4 = 64 acquisitions per condition; measured batch wall 292.6850318 s, amortised wall per completed seed 18.2928144875 s. Probe elapsed mean 273.2411807563 s, sample SD 9.7096050978 s, range 258.6540891 to 290.9400718 s.

Sensitivity: 24 base conditions with all pinned BD-7 variants, 64 acquisitions per condition/variant; measured batch wall 201.4480528 s, amortised wall per completed seed 12.5905033 s. Probe elapsed mean 195.1205570187 s, sample SD 3.5646130725 s, range 188.1887495 to 199.8958584 s.

| Seed | Main probe elapsed s | Sensitivity probe elapsed s |
|---|---:|---:|
| 530001 | 290.9400718 | 197.2900243 |
| 634730 | 274.4216636 | 194.4225161 |
| 739459 | 264.8135754 | 188.1887495 |
| 844188 | 267.2919146 | 199.3722828 |
| 948917 | 276.1416311 | 194.709499 |
| 1053646 | 269.0290054 | 189.5758886 |
| 1158375 | 277.8386486 | 199.0521324 |
| 1263104 | 288.541638 | 195.651123 |
| 1367833 | 265.4504136 | 192.4560368 |
| 1472562 | 278.6588877 | 196.8438781 |
| 1577291 | 271.9517584 | 199.8958584 |
| 1682020 | 288.2595232 | 191.9708283 |
| 1786749 | 262.5343533 | 197.6538426 |
| 1891478 | 272.2113347 | 198.5366916 |
| 1996207 | 258.6540891 | 191.7379085 |
| 2100936 | 265.1203836 | 194.5716523 |

The 16 seed times are concurrent elapsed measurements with shared CPU contention; their SD is descriptive, not an independent timing confidence interval. Each phase has one measured batch wall time (N=1 batch); no timing precision guarantee or confidence bound is asserted.

Extrapolation = measured 16-worker batch wall x (32/16 worker waves) x (100/4 repeats). All setup, transport and driver staging are scaled with the repeats, making this conservative only under approximately linear throughput and comparable machine load. It is not a mathematical upper bound on run time.

Main expected wall: 14634.25159 s = 4.06506989 h. Sensitivity expected wall: 10072.40264 s = 2.79788962 h. Sequential total: 24706.65423000 s = 6.86295951 h. There is no new serial-12-hour claim; the author explicitly replaced that budget decision with 100 repeats and 16 workers after turn 3 stopped.

Pilot artifacts: `%TEMP%\gcam-todo33\turn4-pilot`. Hash-bound timing summary `parallel-timing-v2.json` SHA-256 `7a851dd57af1c2dff375948457c2d233dfb848614aa1fe99b7d692ba36d9684d`, accompanied by its provenance sidecar. Per-seed done.json, frozen execution snapshots and the complete raw responses remain under main/runs/ and sensitivity/runs/.

## Exact planner commands

Working directory for every command: `<repo>`. PowerShell. Run the two long jobs sequentially: wait for the first to finish before starting the second, to retain the measured 16-worker budget. Use these fresh output folders without deleting/reusing a partial earlier run. The built Release probe is ready; if probe code changes, rebuild it and repeat the pilot rather than treating these times as verified for a changed executable.

Validation (expected about 4.07 h):

```powershell
python -B samples/evidence/run_seeds.py --manifest samples/evidence/manifest-background-shape-v2.json --family background_shape_validation_v2 --jobs 16 --out "%TEMP%\gcam-todo33\turn4-validation-v2"
```

Sensitivity after validation completes (expected about 2.80 h):

```powershell
python -B samples/evidence/run_seeds.py --manifest samples/evidence/manifest-background-shape-v2.json --family background_shape_sensitivity_v2 --jobs 16 --out "%TEMP%\gcam-todo33\turn4-sensitivity-v2"
```

Aggregation after the corresponding driver exits successfully; these commands refuse missing/failed/incompatible runs:

```powershell
python -B samples/evidence/background-shape/aggregate_background.py --version 2 --phase validation --runs "%TEMP%\gcam-todo33\turn4-validation-v2" --out "%TEMP%\gcam-todo33\turn4-validation-v2\background-shape-validation-v2.json"
python -B samples/evidence/background-shape/aggregate_background.py --version 2 --phase sensitivity --runs "%TEMP%\gcam-todo33\turn4-sensitivity-v2" --out "%TEMP%\gcam-todo33\turn4-sensitivity-v2\background-shape-sensitivity-v2.json"
```

Outputs are kept in the new scratch run folders with provenance sidecars. No existing committed evidence result is overwritten. The main aggregator reports E0 and the unchanged gate beside E4, preserves signed errors/ideal costs/paired signed vectors, and uses the unchanged pin to classify pass/fail/unqualified/outside-pinned regimes. Sensitivities remain descriptive, wrong-bound fitting remains diagnostic after public refusal; neither gets a new tolerance. No validation verdict is asserted here. Record proposals from Turn3 remain pending actual validation.

## Verification and deviations

- Release probe build: success, 0 warnings / 0 errors, reported build time 3.37 s.
- `dotnet build Gcam.sln -c Release`: success, 0 warnings / 0 errors, reported build time 2.14 s.
- Python inventory: 36 total (35 pass, 1 skip) before -> 39 total (38 pass, 1 skip) after. Final invocation: 0.619 s. All three added checks pass.
- `python -B samples/evidence/calibration_record.py --check`: 4 records current; all checks pass. No generator or VV write in this turn.
- Full .NET tests were not requested/run in this short turn; no product code changed. Prior arithmetic/full-suite records remain in Turn3.
- Original request, manifest, pin, seed hash, and pinned gate-threshold hash were checked unchanged. Git diff --check passes.
- Path deviation: the first Python-test invocation omitted GCAM_PROVENANCE_TEST_ROOT and created retained fixtures under `%TEMP%\gcam-todo37\provenance-test-*` (the suite default). This is outside this turn's stricter scratch root. It was immediately rerun with process-local GCAM_PROVENANCE_TEST_ROOT under `%TEMP%\gcam-todo33\turn4-tests\`, and final verification uses that override. The unintended fixtures were retained; no deletion, stop, or cleanup command was run. No approval is needed to leave them intact.

## Commands and tools that wrote anything this turn

All commands used the repository working directory. Process-local variables below die with their command process. Read-only reads, hashes, git diff/status and CP calculations are not writes.

1. apply_patch: added probe/BackgroundRequest.cs; added background-shape/parallel_timing.py; added three test cases. Target paths are in the files list above.
2. `python -B -` with a PowerShell single-quoted stdin script: wrote request-v2.json and manifest-background-shape-v2.json, and revised the two C# recipes and Program.cs to select explicit v2 modes. Exact stdin is in the command log; no shell substitution is possible inside that single-quoted here-string.
3. `python -B -` with a single-quoted stdin script: revised aggregate_background.py to validate v2 budget references and full per-estimator N=100 rows.
4. `dotnet build samples/evidence/probe/Gcam.EvidenceProbe.csproj -c Release`: normal build outputs in bin/obj, and allowed NuGet cache if needed.
5. `python -B -m unittest discover -s samples/evidence/tests -p 'test_*.py'`: first invocation wrote default gcam-todo37 retained fixtures (deviation above). Subsequent invocations used `$env:GCAM_PROVENANCE_TEST_ROOT=Join-Path $env:TEMP 'gcam-todo33/turn4-tests'` and `$env:PYTHONDONTWRITEBYTECODE='1'`; they wrote retained fixtures only in that task scratch directory.
6. Main pilot driver command (completed):

```powershell
python -B samples/evidence/run_seeds.py --manifest samples/evidence/manifest-background-shape-v2.json --family background_shape_pilot_v2 --jobs 16 --out "%TEMP%\gcam-todo33\turn4-pilot\main"
```

The same single foreground-waited PowerShell command timed it with Stopwatch and wrote main-wall-seconds.txt using File.WriteAllText, invariant-culture seconds plus LF. Exit code was propagated. Driver outputs/snapshots stayed inside the quoted root.
7. Sensitivity pilot driver command (completed):

```powershell
python -B samples/evidence/run_seeds.py --manifest samples/evidence/manifest-background-shape-v2.json --family background_shape_sensitivity_pilot_v2 --jobs 16 --out "%TEMP%\gcam-todo33\turn4-pilot\sensitivity"
```

Its single foreground-waited command wrote sensitivity-wall-seconds.txt by the same Stopwatch/File.WriteAllText method. Both driver commands set process-local PYTHONDONTWRITEBYTECODE=1.
8. `python -B -` with a single-quoted stdin script: added explicit analysis input-file hashes to aggregate_background.py and parallel_timing.py.
9. Timing aggregation command (completed):

```powershell
python -B samples/evidence/background-shape/parallel_timing.py --main-runs "%TEMP%\gcam-todo33\turn4-pilot\main" --sensitivity-runs "%TEMP%\gcam-todo33\turn4-pilot\sensitivity" --main-wall-seconds 292.6850318 --sensitivity-wall-seconds 201.4480528 --out "%TEMP%\gcam-todo33\turn4-pilot\parallel-timing-v2.json"
```

10. `dotnet build Gcam.sln -c Release`: normal solution bin/obj outputs only; no application was launched.
11. `python -B -` with a single-quoted stdin report writer: created docs/PLAN.Physics.BackgroundShapeDecoding.Turn4.md (this report), English/LF.

No irreversible commands, installations, git mutations, network uploads, desktop actions or validation/confirmation runs were issued. No approval requests.
