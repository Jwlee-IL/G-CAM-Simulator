# TODO-33 turn 2 implementation report (2026-10-07)

Status: **stopped on a specification conflict; not qualified, not complete**.

No material disagreement with BD-1 through BD-11 was found on the initial read. The mandated calibration check later exposed an incompatible requirement: BD-5 requires appending the new lists to `samples/evidence/seeds.json`, but `samples/evidence/calibration-records.json` pins the complete original file. The author's constraints permit existing manifests to gain a new family only and forbid editing `VV.*`. Changing this existing pin requires changing an existing calibration manifest and the generated SHA-256 tables in `docs/VV.Gcam.Calibration.md`. I have done neither and have not weakened the check. This needs a planner decision before further implementation or qualification.

Exact required-check failure:

```text
FAIL samples/evidence/seeds.json: SHA-256 01b7676e62322ec1bd1b8e960697f7ff2e43fe0591e81f3eccce9fa67e39d205 does not match the pin efe45efc63261cb9cc3c1882d875d6e91203c5b9b0df673cf450336801b77f37
1 check(s) failed; nothing written
```

Recommended resolution: explicitly authorize updating only the seed-file pin in `samples/evidence/calibration-records.json` and regenerating the existing calibration document, after verifying that every historical seed list and every historical numerical result remains unchanged. Alternative: decide on separate versioned seed catalogs and a driver change; that departs from BD-5's explicit instruction to pin these lists in `seeds.json`. Restoring the old seed file while claiming BD-5 complete, bypassing the hash check, or editing its expected hash without updating the generated records would be a workaround and has not been done.

## Files by group

Engine and configuration:

- `src/Gcam.Configuration/SimulationConfig.cs`: nullable `Decoder.BackgroundCorrection`; omitted on serialization when null, preserving default serialization as well as decoding.
- `src/Gcam.Configuration/BackgroundCorrectionConfig.cs` and `BackgroundCorrectionMode.cs`: explicit joint, fixed-scale MLEM, signed fixed-scale correlation modes.
- `src/Gcam.Decoding/BackgroundCalibrationMetadata.cs` and `BackgroundCalibration.cs`: copied, normalized, read-only shape, independent rate and uncertainty, optional calibration duration/counts; reject nonfinite, negative, zero-total or mismatched inputs. Metadata names dimensions, pitch, head response, window, bound and field distribution.
- `src/Gcam.Decoding/BackgroundEstimator.cs`, `BackgroundDecodeResult.cs`, `BackgroundAwareDecoder.cs`: reusable calibrated facade, existing `IDecoder.Decode(image)` retained; explicit result also reports fitted/known background counts and independent calibration expected counts/uncertainty. E6 retains negative subtracted pixels; E5 uses the existing pixel-area MLEM fixed-background path.
- `src/Gcam.Decoding/JointBackgroundMlem.cs` and `JointMlemSnapshot.cs`: separate nonnegative background amplitude, normalized shape column, cached pixel-area source matrix; SIMD float projections and double updates, optional double likelihood diagnostics, source/background snapshots. Initialization divides expected source counts uniformly across columns after sensitivity normalization, with half the observed total assigned initially to background. Zero beta remains zero by the multiplicative update. E4's confidence field is zero; it supplies no new significance or pair claim.
- `src/Gcam.Simulation/DefaultSimulationFactory.cs`: explicit calibrated overload and response identity covering mask, detector and mask spacing. Ordinary factory path refuses a nonnull correction without explicit calibration and live time. Null follows the original arithmetic. No generating ambient object is read by the calibrated facade.
- `src/Gcam.Cli/StudyDecoderGuard.cs`: studies reject a nonnull correction before running, retaining their existing decoder restriction. No CLI single-run calibration integration was added.

Tests:

- `tests/Gcam.Tests/BackgroundDecodingTests.cs`: 16 arithmetic and surface cases, described below.
- `samples/evidence/tests/test_background_shape.py`: five deterministic checks of signed vectors, whole-seed bootstrap resampling, reproducibility, common-regime iteration selection/ties, and the zero-failure selection count derivation.

Evidence scaffolding (not a completed measurement):

- `samples/evidence/seeds.json`: four new lists; existing lists preserved; all 83 new seeds distinct from one another and from every old list.
- `samples/evidence/manifest-background-shape-v1.json`: new selection, timing pilot and validation families through the existing TODO-37 driver.
- `samples/evidence/background-shape/request-v1.json`: stage-1 conditions and independent transport budgets.
- `samples/evidence/probe/BackgroundShapeRecipe.cs` and `probe/Program.cs`: phase-specific headless entry points, independent truth/calibration transport, exact per-pixel Poisson observations, paired ideal/field errors, pixel-area E4, E0, and targeted E5/E6.
- `samples/evidence/background-shape/aggregate_background.py`: provenance validation, rejection of partial/incompatible ensembles, signed-vector/seed-uncertainty summaries, common-regime iteration ranking, immutable pin writer, and prospective simultaneous cluster-bootstrap verdicts.

Report: this file. Pre-existing changes to `docs/AGENTS.Todo.md`, the reference plan, review, and `docs/PLAN.Docs.TestRecords.md` belong to the incoming workspace; they were not edited here. No Studio source, historical evidence result, Findings, VV record or reference plan was edited.

## Verification and numerical expectations

Full release solution build: succeeded. Two existing xUnit analyzer warnings remain in Studio service tests (xUnit2012 and xUnit2000); no new warning was reported. Evidence probe release build: succeeded with zero warnings/errors after correcting a spectrum-loader API spelling. The failed intermediate probe build was a compile error, not a physics tolerance failure.

Full English .NET test run, desktop tests disabled:

| Suite | Before passed/skipped | After passed/skipped |
|---|---:|---:|
| Gcam.Tests | 465 / 0 | 481 / 0 |
| Gcam.Studio.Tests | 211 / 0 | 211 / 0 |
| Gcam.Studio.Services.Tests | 108 / 7 | 108 / 7 |
| Gcam.Studio.UiTests (headless cases only) | 15 / 20 | 15 / 20 |
| Gcam.Studio.RenderTests | 0 / 1 | 0 / 1 |
| Total | 799 / 28 | 815 / 28 |

No failure; 16 added passing C# cases. Python evidence discovery after the additions: 34 cases, 33 passed, one intentionally skipped isolated Git-mutation fixture; five new passing cases. The prior inventory is therefore 29 cases (28 enabled, one skipped); it was not separately executed before editing. Python discovery took 3.589 s. Calibration-record check: failed with the exact whole-seed-file hash mismatch above; no records were written.

The arithmetic fixtures use no Monte Carlo seeds. The independent scalar reference has four detector pixels, three positive asymmetric source columns, raw counts `[17,83,29,71]` (N=200 exactly), and shape proportional to `[1,3,2,4]`. These are exact deterministic inputs, not a sample with statistical uncertainty.

For float unit roundoff u=2^-24, gamma(k)=k*u/(1-k*u). The small-matrix one-step comparison counts projection, division, dot product, update and conversions, giving gamma(22)=1.311303904574269e-6 as its relative forward-error allowance on each positive source update and beta update. No detector accuracy tolerance is used. Conservation over 60 updates allows N*gamma(26)=0.000309944633159665 counts: the exact real-arithmetic EM identity is sum_j sensitivity_j*lambda_j+beta=N; the update's rounded operations and final positive sum give this arithmetic bound. All 60 checks passed. For positive predicted means perturbed by relative g=gamma(26)=1.549723165798325e-6, the likelihood perturbation bound is N*(-log(1-g))+sum(mu)*g. Comparing two rounded iterates doubles that bound; at sum(mu)=N it is 0.0012397790129660457 log-likelihood units. The test uses each actual predicted total in this expression. All 60 monotonicity comparisons passed. These are small-matrix arithmetic checks, not an assertion of arbitrary-matrix precision.

Other checks have exact, zero-tolerance expectations: power-of-two shape normalization invariance; beta=0 boundary; empty-image total, beta and source zero; E5 at B=0 equals ordinary pixel-area MLEM; E5 at B=600 equals the direct fixed-background decode; E6 equals direct signed-image decode with B=600, including negative pixels; null-path estimates, origins, steps and reconstruction arrays equal retained constructions for lab/hand-held with both decoder methods. Calibration mismatch/invalid input checks expect exceptions. No tolerance was loosened.

## Seed and measurement state

Development only: 330001, 330007, 330019. Selection: `530001 + 104729*i`, i=0..15. Validation: `730001 + 130363*i`, i=0..31. Confirmation: `970001 + 154858*i`, i=0..31. These are pinned as BG_DEVELOPMENT3, BG_SELECTION16, BG_VALIDATION32 and BG_CONFIRMATION32. No validation or confirmation seed was run.

Selection recipe: 448 conditions = four cases x two cyclic settings x two live times x seven source levels x four fields including ideal. Cases are lab (z=160 mm, x=8 mm), hand-held (155,8), hand-held 1 m (1000,52.408), and hand-held 5 m (5000,262.04). Window open, bare-crystal bound; times 10/60 s; sources 25/50/100/250/500/1000 pilot-normalized counts and default 1 MBq; fields 0/0.05/0.10/0.20 microSv/h. Pilot source rates are the retained AB-12 rates in the request. Truth and calibration maps use independent 500,000-history transports per head and outer seed; source maps use 1,000,000 biased photons per case and seed. Correction uses only the independent calibration shape. Source/field pairs share the exact source Poisson realization, adding independent ambient Poisson noise. Cyclic/non-cyclic share streams and remain separate conditions; covariance belongs to the outer seed cluster.

Selection uses four acquisitions per condition per seed, N=64 if all 16 runs complete. The repeat budget was not specified by BD-6: it was set before running from the one-sided zero-failure Clopper-Pearson bound 0.05^(1/64)=0.9542702976692375; at least 59 all-success observations are needed to reach 0.95. This conditional bound does not establish a calibration-population guarantee. E4 snapshots cover 60/120/240/400/800. Comparison uses the intersection of association-valid conditions across candidates, avoiding a preference for candidates that discard difficult conditions by failing association. Trust-qualified regimes would then be pinned separately. Known-scale benchmark levels currently are S=100,250,1000 and default, spanning switching, saturated pull and the weak far-field defaults.

Already launched, one serial driver command:

```powershell
python -B samples/evidence/run_seeds.py --manifest samples/evidence/manifest-background-shape-v1.json --family background_shape_selection_v1 --out "$env:TEMP/gcam-todo33/turn2-selection" --jobs 1
```

At the stop/report point this command is still running (execution session 36742), with no completed seed published to the driver console. No process was stopped, no watcher loop was launched, and no further measurement command was scheduled. Its staged snapshot is isolated from later workspace edits; stdout is captured and published after each seed exits. Outputs belong to `%TEMP%\gcam-todo33\turn2-selection\`. The driver's recorded engine commit is `f4722bfbbc41358e3f3a26f862a7c167cf3f95e1` and this selection attempt's source identity is `9c130bea35dfb262ab3bc5b52660b9eca581cef5ee523ce4cd7bd2b2e4c4161d`; the snapshot records the dirty source and managed closure. These identify the attempt, not successful evidence.

No iteration count is frozen. No `pinned-v1.json` exists. No timing pilot, 12-hour extrapolation, validation, BD-7 sensitivities or aggregate was run. No physical measurement number, seed uncertainty, pass verdict or residual tolerance outcome can be reported from this incomplete command. The new recipe also still needs BD-7 sensitivity execution and explicit Z recording beside the E4 aggregates before it can be treated as the completed BD-10 recipe. I stopped instead of finishing those dependent steps after the required-check conflict.

Current input hashes:

| File | SHA-256 |
|---|---|
| seeds.json | 01b7676e62322ec1bd1b8e960697f7ff2e43fe0591e81f3eccce9fa67e39d205 |
| manifest-background-shape-v1.json | 6f6f711c8f5e5294c61f1cf9c2d1684b906a160d7d8a1c68e39ea538f2c3a102 |
| background-shape/request-v1.json | 847b6e8b97e0ec87ea0a26d3ed3ce30038ca223b04ac1a91b8d6b1c37afd849b |

Prospective engineering targets, calculated from the actual grids rather than borrowed accuracy tolerances: step = rank*cellPitch*z/(D*48); radial cell-equivalence = sqrt(2)*step. Lab 0.5499719409228704 mm; head 0.5812203466571243 mm; 1 m 3.7498086881104795 mm; 5 m 18.7490434405524 mm. These numbers have geometric derivations, not empirical confidence guarantees. Before validation the pin writer would declare 10,000 centered whole-seed bootstrap resamples, statistical seed 131071, 95% simultaneous maximum-vector-deviation radius over pinned regimes, and observed mean-vector norm plus that radius as the upper bound. Coverage is approximate bootstrap coverage, recorded as such. Association and jointly trusted association must retain one-sided conditional lower95 >=0.95 on validation; no pass is issued outside the pinned valid regimes. The existing Z/gate implementation and AB-11 threshold file are unchanged; their relation to E4 has no new guarantee.

## Proposed record wording (not applied)

These are **pending implementation/validation** wording proposals, not completed physics evidence. The planner should retain that status until a valid pin, timing decision, complete validation and sensitivities exist.

Findings theme 67:

"67. Background shape decoding: fitted nuisance amplitude and retained counting noise. TODO-33 adds opt-in joint-background pixel-area MLEM (E4), explicit known-scale MLEM (E5), and signed known-scale correlation (E6). Calibration shape and rate are supplied independently of the generating ambient field. E4 fits a nonnegative background amplitude separate from the source grid; it is not an ambient dose estimate. Null correction preserves the existing decoding arithmetic. Deterministic arithmetic checks pass; stage-1 qualification is pending. Report paired signed-vector excess, ideal cost, RMS, association, unchanged gate outcomes, calibration/scale/shape sensitivity, outer-seed uncertainty and provenance before making any precision claim. The AB-12 raw results remain unchanged. No E4 pair-resolution or new trust guarantee is claimed. Turn-2 execution stopped because BD-5 changes a whole-file calibration-pinned seed catalog while the corresponding manifest and VV record edits are excluded."

EV-35:

"EV-35 - Background-aware single-source decoding (pending qualification). E4 jointly fits the normalized background component; E5 and E6 require an explicit known scale. Default correction is null. Arithmetic/surface verification adds 16 passing cases; physical stage-1 validation and calibration sensitivities are not complete. A future qualification is scoped to its pinned head, source level, live time, field, counting window, bound, calibration population, cyclic setting and selected iteration count. The sqrt(2) grid-step convention bounds paired signed-vector excess only in independently association- and trust-valid regimes using the pinned simultaneous seed-cluster procedure; it is not detector accuracy. No numerical precision or E4 trust guarantee is released by the present record."

EV-01 pointer:

"The retained raw-decoder precision numbers are unchanged. Background-corrected single-source alternatives are tracked separately in EV-35; that qualification is pending and does not replace these numbers."

EV-12 pointer:

"The retained count/noise results and their estimator scope are unchanged. E4's ideal cost and corrected count regimes require separate evidence in EV-35; no existing count threshold is transferred to E4."

EV-34 pointer:

"The AB-12 raw-background pull is retained. Opt-in shape correction, its counting-noise cost and its calibration scope are tracked separately in EV-35; corrected precision remains pending qualification."

LIM-10:

"Background shape correction removes an expected spatial term, not the realized background counting noise. E4 can fit source structure into its nuisance component, including with no field, and its amplitude is not ambient dose. E5/E6 depend on an explicit known scale. A finite source-free calibration acquisition introduces shared shape/rate uncertainty; window, response geometry, field distribution and bound must match the acquisition. Metadata rejects declared mismatches but cannot establish that the physical declarations are correct. Transported bare/front-only maps remain bounds, not measured housing calibrations. The unchanged AB-11 gate has no established E4 trust guarantee; no E4 pair-resolution claim is made. Record ideal cost, scale/shape sensitivity, association failures and outer-seed uncertainty; physical corrected precision is not released until the scoped qualification completes."

D-52:

"Use joint-background pixel-area MLEM (E4) as the opt-in single-source shape correction. Keep fixed-background MLEM (E5) and signed known-scale correlation (E6) explicit. Keep background amplitude separate from source pixels; default correction is null and existing results remain unchanged. Do not claim E4 pair resolution from the no-background MLEM evidence."

D-53:

"Fit E4's background amplitude from the image and report an independent source-free calibration rate as a cross-check. Do not use a dose counter measuring source plus ambient as the subtraction scale. Require declared matching calibration dimensions, head response, window, field distribution and bound; validate finite-acquisition and deliberate scale/shape errors separately."

D-54:

"Judge corrected single-source bias only in independently association- and trust-valid regimes against the sqrt(2) grid-step cell-equivalence target. Pin regimes, targets, iteration rule and simultaneous seed-cluster paired-signed-vector procedure before validation. Record failures and bootstrap coverage limitations; do not widen a failed tolerance or assert detector accuracy."

D-55:

"Stage 1 covers the open-window bare-bound four edge cases, every declared source/field/time level, cyclic and non-cyclic decoding, 32 locked validation seeds and 300 acquisitions per seed. Run selection first, freeze iterations and qualification regimes, then execute a one-seed timing pilot of the validation recipe. Stop for an author decision if extrapolated serial time exceeds 12 hours. Other windows, bounds and centers remain deferred; a partial stage or a failed required check is not qualification."

## Commands that wrote anything

All mutations are repository edits, build/test artifacts, or scratch artifacts under `%TEMP%\gcam-todo33\`; dotnet may use its authorized NuGet cache. No Git state-changing command, installation, persistent environment change, GUI, upload, deletion command or process-stop command was issued. Local process environment assignments below expire with their command processes.

1. `apply_patch`: created the configuration/calibration/joint-EM files listed above and added the nullable decoder property. Subsequent `apply_patch` calls split the mode enum, added the facade/result/factory overload and response identity, added the C# tests, added the probe/aggregator/Python tests, added null-serialization omission and CLI study refusal, corrected the spectrum loader, cached ideal beta snapshots, and added phase seed checks. This report was also written through `apply_patch`. Exact patches are in the command/tool log; no prohibited file was patched.
2. Baseline: `$env:DOTNET_CLI_UI_LANGUAGE='en'; $env:GCAM_UI_TESTS='0'; dotnet test Gcam.sln -c Release --logger 'console;verbosity=minimal' | Tee-Object -FilePath "$env:TEMP/gcam-todo33/turn2-baseline-tests.log"`.
3. Targeted arithmetic run: `$env:DOTNET_CLI_UI_LANGUAGE='en'; dotnet test tests/Gcam.Tests/Gcam.Tests.csproj -c Release --filter FullyQualifiedName~BackgroundDecodingTests --logger 'console;verbosity=normal'`.
4. Probe builds: `dotnet build samples/evidence/probe/Gcam.EvidenceProbe.csproj -c Release` was issued four times, including the failed initial compile; subsequent calls set `$env:DOTNET_CLI_UI_LANGUAGE='en'`. Builds write ordinary repository bin/obj outputs; no clean was run.
5. Seed/request/manifest creation: the `python -B -c` command in the tool log read UTF-8 `samples/evidence/seeds.json`, verified disjointness, appended the four lists/rules, and wrote that file, `samples/evidence/background-shape/request-v1.json` and `samples/evidence/manifest-background-shape-v1.json` with LF and UTF-8. Its complete construction was: new lists `[330001,330007,330019]`, `[530001+104729*i for i in range(16)]`, `[730001+130363*i for i in range(32)]`, `[970001+154858*i for i in range(32)]`; the request values and pilot rates are in the written file; families are selection/BG_SELECTION16/16, pilot/BG_SELECTION16/1, validation/BG_VALIDATION32/32. An earlier attempt failed while decoding the original file before any write.
6. `python -B -m unittest discover -s samples/evidence/tests -p test_background_shape.py` (five passing tests; no fixtures written by these five).
7. Selection driver command reproduced above, after the last probe build. It writes isolated snapshots, attempt outputs and driver provenance to `%TEMP%\gcam-todo33\turn2-selection\`. It remains active at reporting; no watcher loop or additional evidence run was started.
8. Verification: `$env:DOTNET_CLI_UI_LANGUAGE='en'; dotnet build Gcam.sln -c Release; $env:GCAM_UI_TESTS='0'; dotnet test Gcam.sln -c Release --logger 'console;verbosity=minimal' | Tee-Object -FilePath "$env:TEMP/gcam-todo33/turn2-final-tests.log"`.
9. Python verification: `$env:GCAM_PROVENANCE_TEST_ROOT="$env:TEMP/gcam-todo33/python-tests"; python -B -m unittest discover -s samples/evidence/tests -p "test_*.py"; python -B samples/evidence/calibration_record.py --check`. Existing provenance tests retain their fixtures in the specified scratch root; the optional isolated Git-mutation test remained skipped. The calibration command wrote nothing.

Exact successful command for item 5 (the only non-build shell command that rewrote repository data):

```powershell
python -B -c "import json,pathlib,hashlib; p=pathlib.Path('samples/evidence/seeds.json'); d=json.loads(p.read_text(encoding='utf-8')); old=set(v for k,vs in d.items() if isinstance(vs,list) for v in vs); new={'BG_DEVELOPMENT3':[330001,330007,330019], 'BG_SELECTION16':[530001+104729*i for i in range(16)],'BG_VALIDATION32':[730001+130363*i for i in range(32)],'BG_CONFIRMATION32':[970001+154858*i for i in range(32)]}; flat=[v for vs in new.values() for v in vs]; assert len(flat)==len(set(flat)) and not(old&set(flat)); d.update(new); d['_rules'].update({'BG_SELECTION16':'530001 + 104729*i, i=0..15','BG_VALIDATION32':'730001 + 130363*i, i=0..31','BG_CONFIRMATION32':'970001 + 154858*i, i=0..31'}); p.write_text(json.dumps(d,indent=1)+'\n',encoding='utf-8',newline='\n'); root=p.parent/'background-shape'; root.mkdir(exist_ok=True); q={'SchemaVersion':1,'SelectionRepeats':4,'PilotRepeats':16,'ValidationRepeats':300,'AmbientHistories':500000,'SourcePhotons':1000000,'KnownScaleLevels':[100,250,1000,'default'],'Cases':[{'Name':n,'Scenario':s,'ZMm':z,'XMm':x,'PilotRatePerBq':r} for n,s,z,x,r in [('lab','scenario.json',160,8,7.707675168426954e-05),('head','scenario_handheld.json',155,8,0.00017324220377842224),('head1m','scenario_handheld.json',1000,52.408,4.036923162566556e-06),('head5m','scenario_handheld.json',5000,262.04,1.5748511650608925e-07)]]}; (root/'request-v1.json').write_text(json.dumps(q,indent=1)+'\n',encoding='utf-8',newline='\n'); manifest={'_about':'TODO-33 stage 1; selection first, pinned-v1.json required for pilot and validation. Serial timing limit 12 hours. Probe phases consume explicit independent calibration.','config_sha256':{s:hashlib.sha256((p.parent.parent/s).read_bytes()).hexdigest() for s in ['scenario.json','scenario_handheld.json','evidence/background-shape/request-v1.json']},'families':[{'id':'background_shape_'+ph+'_v1','kind':'probe','mode':'background-'+ph,'seeds':sl,'n':n,'evidence':'TODO-33 BD-1..11 single source; '+ph} for ph,sl,n in [('selection','BG_SELECTION16',16),('pilot','BG_SELECTION16',1),('validation','BG_VALIDATION32',32)]]}; (p.parent/'manifest-background-shape-v1.json').write_text(json.dumps(manifest,indent=1)+'\n',encoding='utf-8',newline='\n'); print('Pinned 83 mutually distinct seeds; no overlap with prior lists.'); print(hashlib.sha256(p.read_bytes()).hexdigest())"
```

The patches in item 1 are fully auditable in the retained tool log; no historical result was rewritten. Read-only commands (`Get-Content`, `rg`, `git status`, `git diff`, `Get-FileHash` and numeric Python expressions) are omitted from this write ledger.

No irreversible command is needed or requested. The next required decision is a correction of the incompatible document/manifest scope, not permission to delete, move, install or change Git state.
