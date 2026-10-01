# VV.Studio.Imaging — per-nuclide imaging verification evidence

Scope: measured-energy imaging channels and Compton stripping in GCAM Studio. This page separates association
verification from measured localization precision; it makes no new precision claim.

**At a glance**
- Requirements SR-IMG-01 … SR-IMG-06 are in [VV.Studio.SRS](VV.Studio.SRS.md).
- Headless requirements passed; Release measurements below were supplied by the reviewer on 2026-10-02.
- The Co-60 y bias near −1.4 mm is not an energy effect: it is a position-dependent localisation error from
  undersampling the mask-cell shadow with the default optics at a 1 m focal plane (see "Localisation bias").
- Offscreen mixed-scene renders pass in both themes and sizes; desktop validation remains unperformed.
- No desktop UI test or Studio launch is part of this verification.

## Scenes and checks

Defaults: rank 13, mask pitch 0.7 mm, D 80 mm, focal/source distance 1000 mm, 30×30 pixels at 0.6 mm,
Studio detector realism (entrance 0.15 mm, backing 2 mm, reflector gap 0.1 mm, gain σ 3%, seed 1),
default physical front-end chain, N = 1.5. Cs activity 500 µCi; Co activity 1000 µCi.
All events come from ListModeSource; the measured response is MeasurementStage.

| Check | Method and acceptance | Measured result |
|---|---|---|
| Co-located Cs + Co at (0,0), 600 s | Raw low window must exceed an independent same-live-time Cs-only reference by 4√(L+C). Strip count acceptance is 4√(L + R²H + C + H²Var(R)); Var(R)=R²(1/Lcal+1/Hcal), delta-method independent Poisson calibration windows. Seeds 27182 mixed and 31415 Cs-only. | Passed. R(Co→Cs) = 0.4775 (6,096/12,767). Raw Cs L = 23,213, high H = 20,570, stripped Cs = 13,409.0634, Cs-only C = 13,297; error = 112.0634 counts versus 4σ = 1,016.5072 counts; Var(R) = 5.5257E-05. |
| Off-axis association, 600 s | Cs (15,8) mm, Co (−15,−8) mm, seed 19283. Each refined top peak error must be strictly below step × √2 = 3.093592 mm (step 2.1875 mm). This is an association bound, not a precision tolerance. | Passed. Cs found (14.8344, 8.0613) mm, error 0.1765 mm; Co found (−15.1387, −9.4170) mm, error 1.4238 mm. Both below the 3.0936 mm association bound. R(Co→Cs) = 0.4825 (6,115/12,674). |
| Localization precision | 20 seeds 42000 … 42019 per scene, 600 s each. Mixed Cs / Co and Co-only control at the same Co position/activity. Record mean error vector, RMS radial error and maximum radial error, raw and Tent-refined. No precision assertion or tuning. | Completed; measurements in the table below. Co-60 y bias also appears without Cs. |
| Window replay | Seed 13579, 60 s mixed co-located acquisition. N 1.5 → 2; retained prefix matches fresh processing exactly, R recomputes, strip-only replay uses cached R. | Passed. At N = 2, R(Co→Cs) = 0.5336 (8,117/15,213), recomputed. Strip toggle: channels 0.299 ms, calibration 0 ms, decode 37.7 ms (co-located 600 s check). |

Calibration accepts 100,000 H-only events (above the required 20,000). The source's complete emission lines,
geometry, gain and detector response are preserved. Background is disabled for H-only calibration. The primary
Co window is 1173.2 keV; calibration includes its 1332.5 keV emission. R and channel / calibration / decoding
milliseconds are emitted by each test and displayed in the panel. For the off-axis check, 100,000 H events
were accepted; worker costs were channels 216.2 ms, calibration 696.8 ms and decode 90.7 ms.

| Scene / isotope | Peak method | Mean error (x, y), mm | RMS radial error, mm | Maximum radial error, mm |
|---|---|---|---|---|
| Mixed / Cs-137 | Raw grid peak | (−0.453, −0.781) | 1.380 | 1.448 |
| Mixed / Cs-137 | Tent-refined | (−0.093, −0.084) | 0.167 | 0.227 |
| Mixed / Co-60 | Raw grid peak | (0.344, −2.281) | 2.307 | 2.307 |
| Mixed / Co-60 | Tent-refined | (−0.194, −1.378) | 1.395 | 1.456 |
| Co-only / Co-60 | Raw grid peak | (0.344, −2.281) | 2.307 | 2.307 |
| Co-only / Co-60 | Tent-refined | (−0.200, −1.367) | 1.384 | 1.465 |

The engine EV-15 scalar subtraction model motivates this method, but does not substitute for these Studio
measurements. Finding TODO-17: at Co-60 (−15,−8) mm, 1000 µCi, with the defaults above, N = 1.5,
stripping enabled, 600 s and 20 seeds, the refined mean y error is −1.378 mm in the mixed scene and
−1.367 mm in the Co-only control. This records a systematic bias under these conditions; its cause is given under "Localisation bias" below.
The Cs refined mean error is (−0.093, −0.084) mm in this mixed-scene sample. These numbers do not establish
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
For numerical output use the detailed console logger on `tests/Gcam.Studio.Services.Tests`, filtering
`FullyQualifiedName~ImagingServiceTests`. Set GCAM_RENDER_SNAPSHOTS=1 only for
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

## Verification status

Reviewer verification (Release, 2026-10-02): build 0 errors; engine 259, Studio Core 86, services 28 and
UI oracle 10 tests all passed. Numerical results above are the reviewer's measured ImagingServiceTests
output, not newly measured timings from the render fixture. Local verification and snapshot inspection
status are recorded in [VV.Studio](VV.Studio.md). Desktop validation remains pending; no desktop UI tests or
Studio launch were performed.
