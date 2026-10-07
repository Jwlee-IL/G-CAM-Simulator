# VV.Studio.Imaging — per-nuclide imaging verification evidence

Scope: measured-energy imaging channels and Compton stripping in GCAM Studio. This page separates association
verification from measured localization precision; it makes no new precision claim.

**At a glance**
- Requirements SR-IMG-01 … SR-IMG-06 are in [VV.Studio.SRS](VV.Studio.SRS.md).
- Headless requirements passed; Release measurements below were supplied by the reviewer on 2026-10-02.
- The Co-60 y bias near −1.4 mm is not an energy effect: it is a position-dependent localisation error from
  undersampling the mask-cell shadow with the default optics at a 1 m focal plane (see "Localisation bias").
- Retained offscreen mixed-scene renders cover both themes and sizes. The final desktop pass verifies channel selection, stripping and bound found peaks; see [VV.Studio](VV.Studio.md).
- The measurements below are headless; the later desktop pass is separate evidence, not a new localisation-precision measurement.

Current normal-run totals are in [VV.Studio](VV.Studio.md#current-test-inventory). Dated verification records below retain the execution limits at that time; subsequent desktop evidence supersedes their pending status.

## Scenes and checks

Defaults: rank 13, mask pitch 0.7 mm, D 80 mm, focal/source distance 1000 mm, 30×30 pixels at 0.6 mm,
Studio detector realism (entrance 0.15 mm, backing 2 mm, reflector gap 0.1 mm, gain σ 3%, seed 1),
default physical front-end chain, N = 1.5. Cs activity 500 µCi; Co activity 1000 µCi.
All events come from ListModeSource; the measured response is MeasurementStage.

| Check | Method and acceptance | Measured result |
|---|---|---|
| Co-located Cs + Co at (0,0), 600 s | Raw low window must exceed an independent same-live-time Cs-only reference by 4√(L+C). Strip count acceptance is 4√(L + R²H + C + H²Var(R)); Var(R)=R²(1/Lcal+1/Hcal), delta-method independent Poisson calibration windows. Seeds 27182 mixed and 31415 Cs-only. | Passed. R(Co→Cs) = 0.4706 (6,018/12,787). Raw Cs L = 23,009, high H = 20,410, stripped Cs = 13,421.7843, Cs-only C = 13,331; error = 90.7843 counts versus 4σ = 1,007.2427 counts; Var(R) = 5.4128E-05. (Old generator: R 0.4775, error 112.0634 vs 4σ 1,016.5072.) |
| Off-axis association, 600 s | Cs (15,8) mm, Co (−15,−8) mm, seed 19283. Each refined top peak error must be strictly below step × √2 = 3.093592 mm (step 2.1875 mm). This is an association bound, not a precision tolerance. | Passed. Cs found (15.2480, 7.8387) mm, error 0.2959 mm; Co found (−15.3164, −9.3830) mm, error 1.4188 mm. Both below the 3.0936 mm association bound. R(Co→Cs) = 0.4709 (6,020/12,785). (Old generator: errors 0.1765 / 1.4238 mm, R 0.4825.) |
| Localization precision | 20 seeds 42000 … 42019 per scene, 600 s each. Mixed Cs / Co and Co-only control at the same Co position/activity. Record mean error vector, RMS radial error and maximum radial error, raw and Tent-refined. No precision assertion or tuning. | Completed; measurements in the table below. Co-60 y bias also appears without Cs. |
| Window replay | Seed 13579, 60 s mixed co-located acquisition. N 1.5 → 2; retained prefix matches fresh processing exactly, R recomputes, strip-only replay uses cached R. | Passed. At N = 2, R(Co→Cs) = 0.5269 (8,038/15,255), recomputed (old generator 0.5336). Strip toggle timings below are from the 2026-10-01 run: channels 0.299 ms, calibration 0 ms, decode 37.7 ms (co-located 600 s check). |

Calibration accepts 100,000 H-only events (above the required 20,000). The source's complete emission lines,
geometry, gain and detector response are preserved. Background is disabled for H-only calibration. The primary
Co window is 1173.2 keV; calibration includes its 1332.5 keV emission. R and channel / calibration / decoding
milliseconds are emitted by each test and displayed in the panel. For the off-axis check, 100,000 H events
were accepted; worker costs were channels 216.2 ms, calibration 696.8 ms and decode 90.7 ms.

| Scene / isotope | Peak method | Mean error (x, y), mm | RMS radial error, mm | Maximum radial error, mm |
|---|---|---|---|---|
| Mixed / Cs-137 | Raw grid peak | (−0.125, −0.781) | 1.350 | 1.448 |
| Mixed / Cs-137 | Tent-refined | (−0.080, −0.115) | 0.234 | 0.418 |
| Mixed / Co-60 | Raw grid peak | (0.344, −2.281) | 2.307 | 2.307 |
| Mixed / Co-60 | Tent-refined | (−0.208, −1.368) | 1.387 | 1.483 |
| Co-only / Co-60 | Raw grid peak | (0.344, −2.281) | 2.307 | 2.307 |
| Co-only / Co-60 | Tent-refined | (−0.199, −1.368) | 1.387 | 1.481 |

Re-measured 2026-10-02 after the random generator was replaced (xoshiro256** with independent per-event smear
streams). Earlier values (old generator): Cs raw (−0.453, −0.781) / 1.380 / 1.448; Cs refined (−0.093, −0.084) /
0.167 / 0.227; mixed Co refined (−0.194, −1.378) / 1.395 / 1.456; Co-only refined (−0.200, −1.367) / 1.384 / 1.465.
The Co-60 y bias is unchanged (−1.378 → −1.368 mm). The Cs refined RMS rose 0.167 → 0.234 mm — about 1.5 standard
errors of a 20-sample RMS, so not established as a change; the old generator smoothed the per-event energy smear
(window counts fluctuated less than binomially), which acts in this direction.

The engine EV-15 scalar subtraction model motivates this method, but does not substitute for these Studio
measurements. Finding TODO-17: at Co-60 (−15,−8) mm, 1000 µCi, with the defaults above, N = 1.5,
stripping enabled, 600 s and 20 seeds, the refined mean y error is −1.368 mm in the mixed scene and
−1.368 mm in the Co-only control. This records a systematic bias under these conditions; its cause is given under "Localisation bias" below.
The Cs refined mean error is (−0.080, −0.115) mm in this mixed-scene sample. These numbers do not establish
precision at other positions, geometries, activities or live times.

## Localisation bias: detector sampling, not energy (2026-10-02)

Single-source, weighted engine runs (3 × 10⁶ biased photons, noise negligible; default optics, focal 1000 mm):
the same bias appears with the ideal geometric detector and with Compton Argmax events, for Cs-137 as for Co-60
(at (−15, −8): Cs −1.26 mm, Co −1.40 mm in y), and it depends on the source position (y = −8: −1.3 … −1.4 mm at
any x; y = +8: −0.3 mm; y = 0: +0.15 mm). A 0.25 mm reconstruction grid does not remove it, so it is not the
sub-cell interpolation. Varying the detector pixel pitch at constant detector size (Cs-137, x = 0,
y = −10 … 10 mm in 1 mm steps, 0.25 mm grid):

| Pixel pitch | Samples per mask-cell shadow (0.761 mm) | RMS error | Max error |
|---|---|---|---|
| 0.6 mm (default) | 1.27 | 0.95 mm | 1.91 mm |
| 0.3 mm | 2.54 | 0.51 mm | 0.91 mm |
| 0.2 mm | 3.80 | 0.24 mm | 0.41 mm |

The error repeats with the source position (period ≈ 3.4 mm at 0.6 mm pitch): the shadow's phase on the pixel grid
decides where the decoded peak lands. The default optics reach ≥ 2 samples per cell only near a 160 mm focal plane
(shadow 1.4 mm), not at 1 m.

## Reproduction and renders

Run `dotnet build Gcam.sln -c Release`, then `dotnet test Gcam.sln -c Release` with GCAM_UI_TESTS unset.
The 20-seed precision measurement and the full sampling regression are now long-running evidence
tests, skipped by default and in CI. Their budgets and historical measured values above are unchanged.
The full automated sampling sweep uses 1,000,000 biased histories per position, pitches 0.6/0.3 mm,
y=-10..10 in 1 mm steps, x=0, a geometric Cs-137 detector at 1 m, fixed 18 mm detector size,
seed 12345 and 0.25 mm grid. It measured RMS/max 1.0734/1.9966 mm and 0.5847/0.9525 mm (old generator: 1.0810/1.9966 and 0.5837/0.9511);
this is distinct from the original 3,000,000-history, three-pitch table above.
Reproduce these automated measurements with the independent evidence opt-in:

```powershell
$env:GCAM_EVIDENCE_TESTS = '1'
dotnet test tests/Gcam.Studio.Services.Tests -c Release --filter Category=Evidence --logger 'console;verbosity=detailed'
Remove-Item Env:GCAM_EVIDENCE_TESTS
```

`Sampling_PositionSweepAtConstantDetectorSize_FinerPixelsReduceLocalizationError` preserves the full
position sweep; `Precision_TwentySeeds_MeasuresMixedIsotopesAndCoOnlyControl` preserves the 20-seed
measurement. `Sampling_SmallBudgetSeedSpread_ReportsRegressionMargin` measures noise in the separate
small default regression budget. None of these opts into desktop input.

The default sampling regression pins seed 12345, 100,000 biased histories per position at x=0,
y={-8,-4,4,8} mm, geometric Cs-137 at 1 m and an 18 mm detector (30×0.6 / 60×0.3 mm).
Its assertion is fine-pixel RMS < 0.85 × coarse-pixel RMS. The reduced photon/position budget was
measured with five seeds using the original full-field 0.25 mm grid, independently of the high-budget
0.95/0.51 mm finding:

| Seed | Coarse RMS, mm | Fine RMS, mm | Fine / coarse |
|---|---|---|---|
| 12345 | 1.109679 | 0.631894 | 0.569438 |
| 23456 | 1.108024 | 0.622447 | 0.561763 |
| 34567 | 1.068757 | 0.631782 | 0.591137 |
| 45678 | 1.124369 | 0.520752 | 0.463150 |
| 56789 | 1.110107 | 0.632080 | 0.569386 |

The observed ratio range is 0.127987; max + twice that range is 0.847111, rounded up to 0.85.
This gives 0.258863 ratio headroom above the worst sampled seed. It is an empirical margin for a
pinned deterministic test, not a confidence bound or a guarantee for arbitrary seeds.

Re-measured 2026-10-02 with the replaced random generator on the final cropped grid (the opt-in
`Sampling_SmallBudgetSeedSpread_ReportsRegressionMargin`): fine / coarse ratios 0.528149 (12345), 0.523767 (23456),
0.506160 (34567), 0.522627 (45678), 0.433514 (56789) — range 0.094635; max + twice the range = 0.717, inside the
0.85 gate (the gate is unchanged).

The calibration run above took 193.9 s: photon reduction alone did not remove full-grid decoding cost.
The final small regression therefore uses half-extent 12.125 mm and step 0.25 mm (98×98), retaining
the nominal half-field 56.875 mm grid's phase. All four source positions are inside this region; every
sampled radial error is at most twice its four-position RMS (≤2.249 mm), so the measured peaks and
their local interpolation neighbours lie inside it. Transport/flood scoring is unchanged. The same
budget's seed-spread opt-in now uses this small grid too. Its final equivalence, runtime (<10 s target)
and default service-suite duration could not be checked after a shell command was blocked with
`CreateProcessAsUserW failed: 5 (access denied)`; the table records the full-grid calibration, not a
completed execution of the final cropped-grid test.
Set GCAM_RENDER_SNAPSHOTS=1 only for
`tests/Gcam.Studio.RenderTests`. Never enable desktop tests for this check.

Generated additional snapshot paths:
`docs/assets/studio-render/imaging-mixed-{all,cs-137}-{dark,light}-{1280x800,1440x900}.png`.
These use analytic drawing fixtures to exercise the production selector, selected image binding and diamond
overlay. Physics evidence comes exclusively from the real-engine tests above.

The pre-fix mixed render failed at the channel-items assertion before the dispatcher binding flush.
Instrumentation of `BindingOperations.GetBindingExpression` showed the options panel's ancestor
DataContext binding was Active, with a `System.Windows.Controls.Border` source whose DataContext was
`Gcam.Studio.Core.ViewModels.ImagingWorkspaceViewModel`. However, the ComboBox ItemsSource binding
(`Isotopes`) had status Unattached and a null DataItem at that point. The observed failure was deferred
attachment of the child binding, not a demonstrated lookup of the wrong ViewModel.

The options panel now inherits the workspace directly. Only its sibling measurement section binds to
`Measurements`; the Border ancestor lookup is removed. At the same pre-flush assertion, ItemsSource now
has status Active and DataItem equal to the workspace instance. The render test checks these identities,
the All / Cs-137 / Co-60 items, selection, image identity and found-peak count.

## MLEM reconstruction at the default optics (TODO-36, 2026-10-07)

Conditional evidence for SR-IMG-07 at Studio's default optics (rank 13, 0.7 mm cells, D = 80 mm, 30 × 30 pixels at
0.6 mm, focal plane 1000 mm; transported maps, 662 keV window, exact Poisson, the blind pair test). Not claimed for
other optics or focal planes; the UI says so.

- **Iteration count.** The pair-resolution rule (smallest resolved separation with single-source false splits ≤ 5 %;
  ties to fewer iterations) picked 360 on selection seeds (15 × 50); on check seeds (16 × 50) 360 passed 1.0 element
  at 0.949 [0.927, 0.968], so 400 was taken and confirmed on a third, disjoint set (16 × 50): **1.0 element 0.960
  [0.940, 0.979]** (point estimate against 95 %), false split ≤ 0.13 % [0, 0.37] at 2000 counts; cross-correlation
  resolves 2.0 elements. Cost at low counts: a 250-count single source shows a second peak ≥ ¼ of the main height in
  50.4 % [46.3, 54.2] of frames (≥ ½: 11.6 %); cross-correlation 100 % / 77.5 %.
- **MLEM + strip on Cs-137 under Co-60** (16 seeds × 50, Studio's windows; 360 iterations, ideal strip ratio). Cs
  position RMS / count bias (RMSE), cross-correlation + clipped strip against MLEM with the downscatter as background:
  1000 Cs counts, Co : Cs 1 : 1 — 0.071° / +88 (95) against 0.043° / +1 (39); 4 : 1 — 0.132° / +249 (253) against
  0.120° / 0 (54); 4000 counts, 1 : 1 — 0.055° / +45 (89) against 0.025° / −3 (80); 4 : 1 — 0.064° / +222 (241)
  against 0.030° / −1 (104). The clipped strip sum reads 4.5–25 % high (TODO-39).
- **Display rate** (real session, 5 channels, 60 s at speed 10): cross-correlation 3.4–3.6 Hz (worker decode 80–90 ms
  median); MLEM at 360 iterations 1.1–1.3 Hz (600–740 ms); about 1.0–1.2 Hz at 400 (scaled, not re-measured). The
  producer held 9.9–10× speed; transport never stalled.

## Verification status

Reviewer verification (Release, 2026-10-02): build 0 errors; engine 259, Studio Core 86, services 28 and
UI oracle 10 tests all passed. Numerical results above are the reviewer's measured ImagingServiceTests
output, not newly measured timings from the render fixture. Local verification and snapshot inspection
status are recorded in [VV.Studio](VV.Studio.md). Desktop validation remains pending; no desktop UI tests or
Studio launch were performed.
