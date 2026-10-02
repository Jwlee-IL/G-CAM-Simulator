# VV.Studio.Detector — detector geometry and retained-flood focus verification

Scope: editable reflector gap, the static Detector workspace and experimental focus analysis in GCAM Studio.

**At a glance**

- Detector displays a seeded relative-gain pattern and exact geometric dead gaps, with acquired and next-acquisition inputs separated.
- Focus projects retained channel floods; it generates no transport histories and has no calibrated depth accuracy.
- Half-maximum intervals are descriptive and censored at sweep edges; an external surface range is a separate observation.
- Deterministic tests run normally; long numerical evidence, offscreen renders and desktop tests require their separate opt-in variables. Current totals and retained desktop evidence are in [VV.Studio](VV.Studio.md#current-test-inventory).

The final desktop pass checks the face before acquisition, acquired-input locks through Reset, refocus projection and raw half-max endpoints. Earlier execution restrictions below are historical; they do not describe current workspace availability. See [VV.Studio](VV.Studio.md).

## Conditions and numerical expectations

Inherited Studio-path measurements used rank 13, mask cell 0.7 mm, D=80 mm, 30×30 crystals at pitch 0.6 mm,
GAGG, Cs-137 500 µCi, on-axis detector-referenced z=1000 mm, 60 s per seed, physical entrance/backing,
gain σ=3%, seed 1, and a 661.7-keV window ±1.5 chain FWHM (chain FWHM approximately 30.2 keV).
Three transport seeds measured total accepted rate 103.70/72.81/46.63 cps for gaps 0/100/200 µm,
or ratios 1/0.702/0.450. The independent analytic geometric expectations are 1/0.694444444/0.444444444.
These are conditional rate comparisons, not a general equality between count efficiency and area.
The primary-window fraction was about 0.30. These inherited values are context, not assertions or tolerances
in the new test, and no new MC result is claimed here.

`DetectorGapEvidenceTests.RateRatio_MatchesGeometricArea_WithinMeasuredSeedSpread` reproduces the full
Studio list-mode and measurement path. Twelve independent transport seeds (43001+i×1009) run 60 s at each
gap, with paired zero-gap references. For each gap, the first eight ratios measure sample standard deviation
s with divisor 7; tolerance is **4·s·sqrt(1+1/8)**, a per-seed predictive spread bound including calibration
mean estimation. The calibration mean and four independent holdout ratios are compared with the analytic
area expectation. Every seed's counts, cps, primary-window counts, ratios, s and derived tolerance are printed.
No fixed tolerance, borrowed precision or fallback bound is present. A failure is a measurement result to
investigate; this test does not establish a Gaussian coverage probability or universal fill-factor law.

Measured 2026-10-02 (opt-in run, Release), old and replaced random generator: at 100 µm the calibration mean is
0.69428 (s 0.01672, tolerance 0.07094) with the old generator and **0.69846 (s 0.01472, tolerance 0.06247)** with
xoshiro256**, against 0.69444; at 200 µm 0.45548 (s 0.01521, tolerance 0.06453) → **0.44753 (s 0.01074, tolerance
0.04557)**, against 0.44444. Both pass; the changes are within about one standard error of an eight-seed mean.

Inherited focus measurements used five seeds and 10/60 s at the same default optics, with near planes also
sampled on a 10 mm grid. At true z=300/500 mm on axis, sharpest broadband planes were approximately
360/580–586 mm, with half-max intervals 230–380/350–730 mm. At 30 mrad, sharpest planes were about
280/460 mm. At z=700 mm, off-axis best plane was about 560 mm and the interval reached the far edge.
From roughly 700 mm the far endpoint was censored; at 2 m best planes ranged 890–2900 mm across seeds.
These measurements motivate the visible bias/bound note, not a depth acceptance tolerance. The new
81-plane inverse-z grid is a sampling policy, with deterministic endpoint/spacing tests; the prior grids
and their exact best planes are not silently asserted for this grid. Width is never treated as uncertainty.

## Verification and commands

| Scope | Test / fixture | Execution record |
|---|---|---|
| Units, validation, snapshot and acquired (locked) face | DetectorWorkspaceTests; DetectorFaceTests; Face_GainsMatchMeasurementPattern; Config_RejectsGapAtServiceBoundary | Passed in Release, 2026-10-02 |
| Plane sampling, exact half-max crossings, censored edges, modes and angle linking | FocusSweepMathTests | Passed in Release, 2026-10-02 |
| Frozen identity, K, cancellation, snapshot consumption and obsolete-result rejection | FocusSweepViewModelTests; ImagingWorkspaceTests channel invalidation | Passed in Release, 2026-10-02 |
| Retained flood, no events/truth, empty flood and cancellation | FocusSweepServiceTests | Passed in Release, 2026-10-02 |
| Area/rate relation | DetectorGapEvidenceTests (Evidence) | Requires opt-in run; no measured s or numeric tolerance obtained in this turn |
| Both themes and sizes | RenderDetectorAndFocus: detector-before, detector-acquired, focus-near-resolved, focus-far-censored | Fixture prepared; 16 new PNGs not generated or inspected in this turn |

The render curves are analytic drawing fixtures, not synthetic physics evidence. The face fixture uses
the actual engine gain pattern. All fixtures detach MainWindow content, never Show/Run, create an HWND
or send desktop input. The existing single opt-in render case owns the sole STA/Application lifetime.

```powershell
dotnet build Gcam.sln -c Release
dotnet test Gcam.sln -c Release
$env:GCAM_EVIDENCE_TESTS = '1'
try {
    dotnet test tests/Gcam.Studio.Services.Tests -c Release --no-build --filter FullyQualifiedName~DetectorGapEvidenceTests --logger 'console;verbosity=detailed'
} finally { $env:GCAM_EVIDENCE_TESTS = $null }
$env:GCAM_RENDER_SNAPSHOTS = '1'
try {
    dotnet test tests/Gcam.Studio.RenderTests -c Release --no-build --logger 'console;verbosity=detailed'
} finally { $env:GCAM_RENDER_SNAPSHOTS = $null }
```

After process execution was restored, the Release solution build passed and the final ordinary solution
suite passed: engine 259, Studio Core 147, services 67 and UI oracles 10. Seven service evidence cases,
nine desktop cases and the one render case were skipped. Records are under `.artifacts/todo11/tests/`
with the `todo11-final` prefix. The final incremental build reports zero warnings/errors; the earlier rebuild
reported two existing xUnit analyzer warnings. No desktop test or
Studio launch occurred. Opt-in environment variables were absent; changing them requires the author's
approval under the execution limits. The two opt-in command blocks above restore that absent process-local
state; they have not been executed. Numeric MC tolerances and PNG inspection remain unverified.
No acceptance bound has been loosened.

## Limits

The engine still assigns events directly to crystals. Crosstalk and SiPM pitch are excluded until a light-sharing
and four-channel position readout exists. Minimum-cost angular tracking can exchange identities for close or
crossing rays; K requests candidates and does not prove K sources exist. A same-ray mixture is not separated
by this curve tool. Missing-plane tracks are unresolved. Background and clipped stripped floods change the
descriptive metric and cannot supply a Poisson likelihood interval. Decoder cancellation occurs between
planes; its bounded internal decode is not interruptible. External surface range is not proof of gamma-source range.

Requirements: [VV.Studio.SRS](VV.Studio.SRS.md). Design: [VV.Studio.SDS](VV.Studio.SDS.md).
