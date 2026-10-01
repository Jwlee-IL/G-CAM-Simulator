# VV.Studio.Acquisition — live list-mode verification and measurements

Scope: engine list-mode events and the Studio acquisition contract, state and imaging snapshots. Spectrum
processing is future work; no desktop acquisition validation was run in this change.

**At a glance**
- Fresh histories, importance-weight rejection and independent Poisson timing feed one cumulative event list.
- Eight added engine cases, five Core cases (four acquisition + one batch regression) and seven service cases pass.
- Release solution verification: 254 engine, 76 Core, 14 service and 10 UI-oracle passes; nine desktop cases skipped.
- All original 246 engine assertions remain unchanged. Legacy batch API and commands still compile.
- Active acquisition requirements are SR-RUN-09 … SR-RUN-19; the affected batch rows are withdrawn.
- Desktop migration and the retained compatibility paths are listed below.

## Physics verification

Measured 2026-10-01, Release, seeded configurations loaded from the lab sample. Independent references use
cloned configs with changed seeds. The weighted flood reference is the Compton Argmax pipeline accepting all
deposits; the ideal geometric detector is not a crystal-deposit reference.

| Test | Result | Tolerance / conditions |
|---|---|---|
| `PixelHistogram_MatchesIndependentWeightedComptonFlood` | maximum absolute z = 3.889 over 144 pixels | 5σ per fraction, allowing 144 simultaneous comparisons; 100,000 physical events vs 1,000,000 reference histories; reference uncertainty bounded by w² ≤ w_max·w |
| `MixedDistances_RejectionPreservesWeightedFloodAndTotalEmissionRate` | 4.8077% rejection acceptance; max pixel z = 2.467; running rate 166.131 vs 165.774 cps | 5σ per pixel; 4σ rate tolerance 5.634 cps; 40,000 physical events vs 2,000,000 reference histories; 160 mm / 1000 mm source distances, activity 1:40 — stresses unequal weights |
| `Deposits_MatchIndependentWeightResampledEventStream` | maximum absolute z = 1.199 over eight energy bins | 4σ including reference uncertainty; 100,000 events vs 150,000 weight-resampled deposits; conservative reference effective size n/2 for these nearly equal weights |
| `RateAndGaps_MatchPhysicalEfficiencyAndExponentialLaw` | 79.870 cps vs independent weighted MC 80.236 cps | 4σ = 1.278 cps; last 90,000 of 100,000 events, excluding startup rate-estimator transients |
| Same test, normalized gap mean | 0.99837 vs 1 | 4/√50,000 = 0.01789; gaps multiplied by the running rate actually used |
| Same test, normalized gap variance | 0.99713 vs 1 | 4√(8/50,000) = 0.05060, using the Exp(1) fourth central moment |
| Same test, gap CDF at 1 | 0.63574 vs 1−exp(−1) = 0.63212 | 4 binomial standard errors = 0.00863 |
| Same test, unique events | 100,000 distinct records; strictly increasing arrival times | exact; producer emits each accepted history once, with no deposit pool |
| `SeedAndCancellation_AreDeterministicAndPrompt` | same seed → identical 1,000-event sequences; pre-cancelled Advance throws | exact; cancellation checked every history |
| `PixelSink_PreservesLegacyEnergySinkAndTransport_WithOpticalCrosstalk` | old energy sink, richer energy sink and pixel deposits identical; flood unchanged | exact, shared photons and separately seeded detector cascades; optical crosstalk 0.2 |
| `ShortAcquisition_LocalizesAfterCountThreshold_SnapshotsAreImmutable` | 450 counts in 5 live s; localization error 2.158 mm | ≥50 counts, error below one resolution element 8.750 mm; Cs-137 500 µCi at (15,8) mm, distance 1 m, default Studio optics |

The running-rate timing is intentionally adaptive as specified: its finite-startup rate estimate is not a
known exact constant. The gap tests normalize by that conditional rate, and the physical-rate comparison
uses the mature prefix. No tolerance was loosened.

## Throughput and reconstruction

Machine conditions: Intel64 family 6 model 183 stepping 1, 24 logical processors, Windows build 26200,
.NET SDK 9.0.311 / runtime 9.0.13. Accepted throughput is physical events per measured wall second, including
MC generation, transport and rejection. It excludes reconstruction and UI drawing. One producer thread;
2,000-event warmup followed by 100,000 events. Twenty decoder calls follow one decoder warmup.

| Configuration | Accepted events / wall s | Rejection acceptance (accepted / detected histories) | Accepted / all proposals | Decode mean / max |
|---|---|---|---|---|
| `samples/scenario.json`, seed 12345 | 438,731 (100,000 / 0.2279 s) | 99.8841% (100,000 / 100,116) | 21.0110% (475,942 emitted) | 4.585 / 4.917 ms |
| `samples/scenario_handheld.json` cloned; source–mask distance = 945 mm, detector distance = 1 m, Cs-137, seed 12345 | 330,918 (100,000 / 0.3022 s) | 99.9950% (100,000 / 100,005) | 27.4224% (364,666 emitted) | 8.598 / 12.031 ms |

The default Studio grid has more detector pixels than these sample rigs. A real service refresh in the short
acquisition test decoded in 28.907 ms (includes first-use/JIT effects); still below the 250 ms refresh interval.
The service budgets transport to 200 ms per refresh, then decodes the cumulative flood. These are local CPU
measurements, not desktop rendering performance or guaranteed throughput. Large event lists are kept for
future spectrum reprocessing; detached snapshot copies have memory / copy cost proportional to acquired counts.

Reproduce without desktop UI:

```text
dotnet build Gcam.sln -c Release -m:1 -nr:false
dotnet test Gcam.sln -c Release -m:1 -nr:false
dotnet test tests/Gcam.Tests -c Release --no-build --filter FullyQualifiedName~ListModeSourceTests --logger "console;verbosity=detailed"
dotnet test tests/Gcam.Studio.Services.Tests -c Release --filter FullyQualifiedName~AcquisitionServiceTests --logger "console;verbosity=detailed"
```

Leave `GCAM_UI_TESTS` unset. Build has zero errors; NU1900 reports unreachable NuGet vulnerability metadata.
Core's 76 cases include concurrent additions to the existing plot tests; this change adds five Core cases.

## Requirement and design trace

[VV.Studio.SRS](VV.Studio.SRS.md): SR-RUN-01, -02, -04, -05, -06, -07, -08 retain their IDs marked withdrawn.
SR-RUN-03 (failure handling) remains active. SR-RUN-09 … -19 cover background acquisition, Stop retention,
live-time progress, input locking, Start availability, stale state, input validation, immutable shared events,
4 Hz decode / measurement refresh, MC limitation and preset/restart behavior. There are 51 active requirements
and seven withdrawn rows. The matrix is in [VV.Studio](VV.Studio.md); SU-19 is added in
[VV.Studio.SDS](VV.Studio.SDS.md).

Virtual-clock tests verify snapshots grow, preset completion, Stop retention, stale edits, measurement geometry
and input locking. Service tests verify real transport/localization, detached immutable snapshots, count/flood
conservation, look-ahead boundaries, input rejection and an extreme MC-limited Stop on an injected clock.
Read-only cross-review identified and rechecked fixes for Stop's live horizon and true deposits before optical
light spread. No desktop session was started.

During full verification, an existing batch progress race reproduced once: a late callback overwrote terminal
progress 1 with 0.5. The retained batch path now serializes callback guard/write with terminal progress, closes
each run's callback acceptance and rejects obsolete reports during a later run. A queued synchronization-context
regression forces that late delivery without sleeps. Existing tests and assertions were not changed.

## Required UI-test migration (not applied)

The existing UI-test project compiles unchanged. Its desktop scenarios are historical batch tests and need
these updates once the author frees that project and desktop:

1. `Harness/StudioWindow.cs`: invoke `StartAcquisition`; terminal states are Completed / Stopped / Failed and
   active state Acquiring. Replace photon-budget setup with `AcquisitionLiveTime` and `AcquisitionSpeed`.
2. `PilotTests.cs`, `ScenarioTests.cs`, `PolishSurveyTests.cs`: all callers of Simulate/Succeeded use acquisition
   controls and Completed; wait for sufficient counts / acquisition completion before numerical readings.
   Keep geometry oracles and their existing tolerances.
3. The cancel scenario becomes Stop retention: assert Start clears prior data; use a sufficiently long preset,
   wait for a snapshot, capture its counts / image, invoke `StopAcquisition`, verify Stopped and acquired data
   retained, then editing restored. Retaining the previous run's peak is no longer the required behavior.
   Verify both live-time and speed fields lock, plus source fields / add / remove / marker dragging.
4. Status assertions parse live time, counts and cps with the optional MC-limited suffix. Re-run localization,
   stale-chip, ROI/readout, measurement, keyboard/default-action and theme scenarios against a frozen terminal
   snapshot; live ROI values may change between refreshes. Add preset auto-completion, restart-clears-data,
   count monotonicity and MC-limited presentation scenarios. Trace them to SR-RUN-09 … -19.
5. Preserve the plotting gate/oracles; the polish survey's acquisition helper needs updated controls only.
   Runtime UI evidence remains pending; compilation does not establish desktop behavior.

## Compatibility and scope

`RunAsync`, `RunCommand`, `RunCancelCommand`, `Photons` and old batch enum values remain available to keep
existing callers and tests compiling; the view uses Start / Stop exclusively. This is the deliberate deviation
from deleting the old API. The new producer explicitly rejects nonzero ambient/background configurations;
Studio currently exposes none, and background requires a separately transported process rather than a reused
shape. List-mode deposits are unsmeared and unwindowed, located by Argmax; front-end resolution, energy windows,
pile-up, spectrum and waveform work remain outside this change.
