# VV.Studio.Acquisition — live list-mode verification and measurements

Scope: engine list-mode events and the Studio acquisition contract, state and imaging snapshots. This is the
acquisition-only verification baseline; Spectrum evidence and current test totals are in
[VV.Studio](VV.Studio.md#current-test-inventory). The original baseline below used no desktop; subsequent acquisition desktop validation is recorded in [VV.Studio](VV.Studio.md#final-desktop-verification-and-validation-2026-10-02).

**At a glance**
- Fresh histories, importance-weight rejection and independent Poisson timing feed one cumulative event list.
- Current realism and background evidence is below; original list-mode measurements remain the baseline.
- Current normal-run inventory is in [VV.Studio](VV.Studio.md#current-test-inventory); historical run counts remain below.
- All original 246 engine assertions remain unchanged. The unused batch API, commands and batch-only tests have been removed.
- Acquisition requirements extend through SR-RUN-28; SR-RUN-14 and the affected batch rows are withdrawn. Continue / Reset replace stale marking.
- Desktop migration history is below; the final four-workspace pass verifies current acquisition control.

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
| `samples/scenario.json`, seed 12345 | 438,731 (100,000 / 0.2279 s) | 99.8861% (100,000 / 100,114) | 21.1848% (472,036 emitted) | 4.585 / 4.917 ms |
| `samples/scenario_handheld.json` cloned; source–mask distance = 945 mm, detector distance = 1 m, Cs-137, seed 12345 | 330,918 (100,000 / 0.3022 s) | 99.9950% (100,000 / 100,005) | 27.4590% (364,180 emitted) | 8.598 / 12.031 ms |

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
Core's 69 cases include concurrent additions to the existing plot tests and four acquisition cases.

## Requirement and design trace

[VV.Studio.SRS](VV.Studio.SRS.md): SR-RUN-01, -02, -04, -05, -06, -07, -08 retain their IDs marked withdrawn.
SR-RUN-03 (failure handling) remains active. SR-RUN-09 … -19 cover background acquisition, Stop retention,
live-time progress, input locking, Start availability, input validation, immutable shared events,
4 Hz decode / measurement refresh, MC limitation and preset completion; SR-RUN-14 (stale marking) is withdrawn and
SR-RUN-23 … -28 add Continue, the raised preset, the seed, Reset, failure handling and the observed status rate. The matrix is in [VV.Studio](VV.Studio.md); SU-19 is added in
[VV.Studio.SDS](VV.Studio.SDS.md).

Virtual-clock tests verify snapshots grow, preset completion, Stop retention, Continue, Reset, measurement geometry
and input locking. Service tests verify real transport/localization, detached immutable snapshots, count/flood
conservation, flood pixel centres matching the decoder to nine decimal places, look-ahead boundaries, input rejection and an extreme MC-limited Stop on an injected clock.
Read-only cross-review identified and rechecked fixes for Stop's live horizon and true deposits before optical
light spread. No desktop session was started.

The batch progress implementation and its late-callback regression test have been removed with the unused
batch path. Acquisition progress comes from sequential snapshots.

## UI-test migration (applied 2026-10-01)

Historical migration record: the later acquisition-control change withdraws source drag and stale marking. Continue now preserves data; Reset clears them. Final desktop evidence is in [VV.Studio](VV.Studio.md).

The desktop scenarios now drive Start / Stop / live time: `StudioWindow.Acquire` waits for Completed / Stopped /
Failed and the status-line counts are parsed; the cancel scenario became
`Stop_KeepsAcquiredData_LocksThenUnlocksScene` (inputs locked while acquiring, counts frozen after Stop, Start
clears). On the desktop all 19 UI tests passed and, with corrupted verdicts, all 7 desktop scenarios failed at their
corrupted assertions. Not yet automated: preset auto-completion timing, count monotonicity during acquisition and the
MC-limited presentation.

## Scope

Studio uses Start / Stop / Continue / Reset. The batch members, ISimulationService and batch enum values have been
removed; SimulationService remains as the acquisition adapter and ImagingResult remains the snapshot image record.
Before list-mode acquisition the flood used geometric CrystalDetector scoring through DefaultSimulationFactory.
It now adds one count per ComptonCrystalDetector event at its Argmax pixel, so in-crystal Compton scatter
mispositioning is included in the image. No quantitative size of this change is asserted.

Studio supplies detector settings explicitly after cloning the scene config; existing builder callers keep
the bare geometry. List-mode deposits remain unsmeared and unwindowed, located by Argmax. Measurement applies
the seeded CrystalUniformity gain pattern before one chain smear. Snapshots retain their acquisition inputs,
and the gain inputs are locked while data exist, so the recorded response cannot change. Spectrum windows and optional
pile-up reuse these events. Per-nuclide imaging and stripping are outside this scope. A Co-60 or Na-22 decay is one event:
the summed deposit of its detected gammas at the largest-deposit pixel over their merged sites, one arrival time.

## Detector realism and background

Measured 2026-10-01, Release. Source tests use default Studio optics (30×30 pixels, 0.6 mm pitch, Cs-137 at
1 m), transport seed 12345, measurement seed 909, gain σ 3% and gain seed 1. Entrance is 0.15 mm steel-equivalent,
backing 2 mm and reflector gap 0.1 mm. Each absorber/backing comparison transports 2,000,000 proposals per
configuration; variations use Clone + mutate. Counts are per proposal budget, not normalized to a fixed accepted
event count, so attenuation remains visible. The Cs spectrum has 256 bins of width 2.9725 keV.

Re-measured 2026-10-02 after the random generator was replaced (xoshiro256**, per-event smear streams keyed by
(seed, index)): every row below is the new seeded realisation; all assertions pass with the unchanged tolerances.
The throughput table above keeps its 2026-10-01 timings; its seeded acceptance / proposal counts are re-measured.

| Test / check | Measurement | Expectation and tolerance |
|---|---|---|
| `BaKAbsorber_UncollidedBandMatchesIndependentNarrowBeam_ReportsMeasuredSpectrum`, unchanged Ba line energies | 50,064 → 23,777; ratio 0.474932 | Beer-Lambert prediction 0.474899 ± 0.014987 (4σ), weighted by the bare 32.1 / 36.4 keV line counts; independent log-log interpolation of iron μ anchors at 30 / 40 keV gives transmission 0.450696 / 0.571939; Poisson reference and transmitted-count uncertainty included |
| Same test, measured grouped Ba band | 57,718 → 35,736; ratio 0.619148 | independent weighted transport + measurement MC, seed 987 / 444, predicts 0.619379 ± 0.028971 (4σ); weighted-reference variance bounded by w² ≤ w_max·w |
| Same test, Ba K / 662 peak-bin ratio | 3.915454 without absorber, 2.223296 with absorber | descriptive, not a global-peak requirement; Ba K remains the global maximum at 31.2110 keV in both configurations |
| Same test, 480–620 keV valley (P-11) | 11,157 → 11,559 counts | descriptive; increase 402 counts is 2.7σ under an independent Poisson comparison (was 148 counts, 0.98σ, with the old generator); no claim of a large absorber tail |
| `Backing_AddsBackscatterRegion_FromTransport` | 170–210 keV counts 15,415 without backing → 17,578 with backing; excess 2,163 | excess exceeds 4σ = 726.5590 counts; Compton's 180° return energy E/(1+2E/m_ec²) = 184.3263 keV; finite-angle return broadens toward higher energies; the bare crystal already has continuum in this region |
| `Gain_WidensPhotopeakInQuadrature_AndReducesTightWindowAcceptance`, 53,690 MC full-energy events | measured variance 560.0673 vs expected 557.3291 keV²; equivalent FWHM 55.7281 vs 55.5917 keV; sampled pattern σ 2.9932% | pixel-amplitude variance + mean FrontEndModel noise variance; 4σ variance tolerance 13.7996 keV² from the mixture's fourth central moment |
| Same test, histogram FWHM | 54.3052 vs 55.5917 keV | quadrature prediction, tolerance 6.7932 keV = two bins + 5·FWHM/√(2(N−1)) |
| Same test, ±0.5 chain-FWHM window | fraction 0.761836 at zero gain spread → 0.477500 at 3%; decline 0.284336 | decline exceeds 4σ = 0.011332, conservative independent-binomial bound for paired samples |
| `BsrOne_DoublesRate_PreservesEverySourceEvent_AndMatchesBackgroundModel` | 79,943 source + 79,825 background events in 1000 s; mature total/source ratio 1.997237 | BSR=1 predicts 2; 4σ tolerance 0.021062, excluding first 100 s of running-rate startup |
| Same test, spatial / energy distributions | uniform-profile max abs z 3.0968 over 144 pixels; energy max abs z 1.3247 over eight bins | 5σ per pixel vs Background.SideLeakProfile(sideFraction=0); 4σ per energy bin vs 100,000 fresh cosine-flux deposits from BackgroundDepositSpectrum at 200 keV with independent seed 987 |
| `ZeroBsr_PreservesSourceStreamBitForBit`, `Background_SeedAndCancellationAreDeterministic` | source-only and explicit zero-BSR records, histories and weights agree; enabled streams replay; cancellation throws promptly | exact seeded equality, no extra RNG draws on the disabled path |
| `Measurement_UsesFrozenInputs_AndReplaysAcrossSnapshotsAndPileUp` | batch, incremental and toggle replay agree; shared MeasurementStage reproduces every histogram bin | exact; 8,427 fresh events; gain precedes smearing and is not applied twice |

Narrow-beam transmission describes uncollided photons, not the full measured Ba band: entrance Compton
scattering can leave a photon inside that broad band. Both checks are therefore reported separately. The
absorber suppresses Ba counts, but in this geometry does not make the Ba peak smaller than the 662 keV peak.
No tail, line or noise floor is added by hand. The valley increase is small and statistically unresolved here.

The ambient energy response matches the existing unmasked cosine-flux crystal model; pixel placement matches
the uniform detected pedestal. BSR is defined after detection, so this is not a prediction of incident flux,
entrance loss or shield leakage. Each background deposit is freshly transported and used once; no event pool
is repeated. Source records remain exactly intact when background is merged. Source and background arrival
streams use separate RNGs and the running source-rate estimator. Spatial structure beyond the uniform pedestal
is outside this implementation.

The first background test used a reference-only variance estimate, then incorrectly treated an empty finite
reference bin as zero probability. The two-sample estimate now pools both counts under the equal-distribution
null; bins with no variance in either sample require exact agreement. The 4σ energy criterion is unchanged.

### Throughput with realism defaults

`Throughput_RecordsBareAndRealisticAcquisition` warms each producer for 2,000 accepted events and times
100,000 further events, four runs per configuration. Bare Studio scene-builder geometry gives 536,360,
525,671, 538,449 and 538,924 accepted events/s (mean 534,851). Realism defaults give 285,234, 299,889,
291,842 and 284,532 events/s (mean 290,375): about 45.7% lower accepted throughput. This comparison includes
transport and rejection, excludes measurement / decode / UI, and changes physical geometry as well as its
compute cost; it is a local measurement, not a performance requirement.

### Traceability and pending desktop checks (historical implementation record)

Current status: the final desktop pass covers workspace clicks, detector locks, retained Spectrum windows and Imaging/focus projections. SelectionItemPattern and keyboard-only/screen-reader coverage remain separate gaps; see [VV.Studio](VV.Studio.md).

SR-RUN-20 … -22 cover explicit defaults, frozen measurement inputs and background. SR-NAV-01 / -02,
SR-SPEC-02 / -03 and SR-RUN-12 state the shared inputs, checked-state workspace activation, gained amplitudes
and input locking. Headless workspace activation fixes AN-10; SelectionItemPattern.Select still needs a real
desktop regression. No Studio window was launched and the UI-test project was not edited.

Needed UI additions: select both workspaces through SelectionItemPattern and assert the displayed content;
verify detector defaults, field reachability in both themes at minimum size, gain / seed / BSR locking during
acquisition and while data exist, preserved spectrum response after a later window change, and live BSR
count/rate presentation. Existing desktop tolerances remain unchanged.

## Start / Stop / Continue / Reset (2026-10-02)

The acquisition follows the multichannel-analyser model: Start acquires, Stop pauses keeping everything, Start
again (labelled Continue) resumes the same acquisition, Reset discards it. Physical inputs are locked while data
exist; view settings are not. Each new acquisition uses a new Monte Carlo seed unless one is fixed.

**Continuation is exact.** The session keeps the list-mode source with all its random streams, the events, flood,
decoder, live-time clock and the one event already drawn beyond the acquired live time. A stopped and continued
acquisition therefore produces the uninterrupted event stream, event for event. Measured on the real session with
an injected clock (`AcquisitionContinuationTests`, Cs-137 500 µCi at (0, 0) + Co-60 300 µCi at (20, 0) mm, 1 m,
seed 4711, preset 12 s, four stops at live 1, 2.125, 7.625 and 8.5 s at speeds ×4, ×1.5, ×11, ×0.7, ×6, 100 s of
wall time while stopped each time):

| Background | Events in 12 s | Result |
|---|---|---|
| BSR 0 | 1,907 (1,810 with the old generator) | identical to the uninterrupted session and to the direct source stream; arrival times strictly increasing; each continued segment starts at the stopped live time and counts |
| BSR 0.5 | 2,769 (old 2,728) | identical, as above |
| Co-60 500 µCi at (3, −2) mm, 100 mm, BSR 0 (same stops) | 139,933 (62 decays with both gammas detected; old 139,747 / 48) | identical, as above; one event per decay keeps the single look-ahead event sufficient |

A raised preset after Completed (3 s → 6 s) continues identically to an uninterrupted 6 s acquisition; the same
seed reproduces an acquisition and the next seed gives an independent one. Sensitivity check: with the look-ahead
event deliberately dropped at each Stop, all three continuation tests fail at the first stop (one real count lost
per stop). Wall-clock pacing never enters the events: speed only decides when the worker consumes them, and live
time, not wall time, is the physical clock, so it does not advance while stopped. The Poisson arrival process is
continued, not restarted: the next arrival is the one drawn after the last event.

Not verified here: the desktop (Reset placement, keyboard order, Continue label) — the rewritten desktop scenarios
are not run.
