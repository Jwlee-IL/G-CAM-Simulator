# VV.Studio.Waveform — scope and acquired-chain verification status

Scope: shared physical detection-chain selection and the array-wide Waveform workspace; requirement/test mapping, execution limits and headless reproduction. Four-channel position simulation is excluded; the CR-RC Q12 arithmetic and preset timing contract are covered here.

The initial verification inventory and sandbox history below describe the original workspace delivery;
the CR-RC arithmetic/response execution record at the end supersedes its headless-test status, but adds
no desktop or offscreen-render result.

## At a glance

- Shared chain is captured in DetectorSettings at Start; retained results use acquired settings, and the chain is locked while data exist.
- GAGG / NaI / LYSO / CsI / BGO map explicitly to transport materials. GAGG remains the default; the others are what-if comparisons. CsI:Tl uses CsI host transport with the activator omitted.
- Default scope uses real acquired times, 10 µs with 20% pretrigger; rate study explicitly re-spaces retained deposits.
- Realistic response is applied once before ADC simulation; ideal removes smear/noise/rise while retaining gain/tail/filter.
- CR-RC uses Q12 state and Q16 coefficients, with per-preset order 4 and explicit T_sum simulation shaping times; plotted values retain fractional codes.
- Headless engine/service/Core tests have passed; actual C#/Python/RTL comparisons and measured response are recorded below. Desktop and offscreen render verification were not run for this change.
- Default headless tests verify transport mapping (incl. CsI), shared response and acquisition locks.

Current normal-run totals and the distinction from retained desktop/RTL records are in [VV.Studio](VV.Studio.md#current-test-inventory). The inventories, CsI-only render limitation and CR-RC execution counts below are dated implementation records, not current solution totals.

## Verification inventory

| Area | Added coverage | Execution |
|---|---|---|
| Configuration and transport | Five supported-material rows; unknown preset rejection; GAGG default preserved; CsI captured by a real acquisition | Default headless tests |
| Shared response | GAGG and CsI across four preamps: waveform amplitudes equal the index-addressed measurement, Spectrum bins agree, Imaging uses the same response/windows; acquired identities/cache reset | Default headless tests |
| ADC waveform | Fixed-length raster compared to legacy without second intrinsic smear; actual retained MC associations; real-time/prehistory/pixel pooling; late timestamps; empty noise windows; ideal noise removal | Default headless tests |
| Local study/readout | Fixed-seed rate-study identity/immutability; matched trapezoid readout; suppression on overlap/saturation/partial windows; CR-RC caveat | Default headless tests |
| Core requests/policies | Pure sample cap/conversion; prepared-pyramid identity; visible-only generation; held-event reuse; latest/next; acquired-chain policy; active-edit rejection; cancellation/latest-revision/new-run isolation; error reporting; Reset before selecting CsI for a new acquisition | Default headless tests |
| Evidence | Maximum window cost/cap; retained MC pulse readouts across supported parts/energies/modes; acquired mixed-field chain calibration and independent-service comparison | Opt-in, not run |
| Offscreen WPF | Real retained MC 10 µs / rate study / ideal, shared chain selector and pending/acquired labels, shared zoom/reset, both themes and sizes | Renderer edited, PNG generation not run |

Source files: `tests/Gcam.Studio.Services.Tests/WaveformServiceTests.cs`, `AcquisitionServiceTests.cs`, `tests/Gcam.Studio.Tests/WaveformWorkspaceTests.cs`, `tests/Gcam.Studio.RenderTests/WaveformRenderTests.cs`; the latter is called within the existing single-STA/Application renderer. CsI adds five default waveform service cases, one real-acquisition case, one Core selector/reset case and twelve engine material cases. The existing opt-in render test now expects five selectable materials including CsI; its test-case count does not increase. The existing GAGG physics tests and scenario-loading test remain unchanged; only the material inventory count increases. RTL/cocotb files are unchanged.

CsI transport evidence is in `samples/materials/evidence/crystal_tables.json` and `samples/materials/CsI_checks.txt`. The evidence folder is outside the scenario test's non-recursive material JSON scan. The host uses 4.51 g/cm³; omitting the Tl activator follows the NaI:Tl convention, with no quantified dopant-specific transport error. At 661.7 keV, generated photo+incoherent attenuation is 0.0743482267 cm²/g, photo fraction 0.144938419 and linear attenuation 0.0335310502 /mm.

At 300/600/800 keV, adding independently generated coherent scattering gives like-total NIST comparisons. The absolute tolerances are 0.000074998 / 0.0000147103 / 0.0000129459 cm²/g: CsI's measured maximum relative residual 0.000105808471 rounded upward to 0.00011, plus each NIST and C# literal half-unit. Above 800 keV, tests assert the documented Klein–Nishina plus power-law extension and attenuation below NIST total. The measured extension errors against NIST photo+incoherent are +0.0699545 % at 1000 keV and −0.352789 % at 1250 keV; they are reported, not hidden in an acceptance band. `CsIMaterialTests` also checks both K edges, density/lookup, reference normalization, clamping and interpolation. C# attenuation/fraction tolerances follow each stored literal's last-place half-unit; the full-precision formula check uses 1e-14 for floating-point arithmetic only.

## Reproduction

Run in the repository root. No desktop opt-in is needed or authorized by these commands.

```powershell
dotnet build src/Gcam.Studio/Gcam.Studio.csproj -c Release -m:1 /nr:false /p:UseSharedCompilation=false
dotnet test tests/Gcam.Tests -c Release -m:1 /nr:false /p:UseSharedCompilation=false
dotnet test tests/Gcam.Studio.Tests -c Release -m:1 /nr:false /p:UseSharedCompilation=false
$waveSuite = [Diagnostics.Stopwatch]::StartNew()
dotnet test tests/Gcam.Studio.Services.Tests -c Release -m:1 /nr:false /p:UseSharedCompilation=false
$waveSuite.Stop()
"Services command wall time: $($waveSuite.Elapsed.TotalSeconds) s"
# Runner-reported test duration is distinct from command wall time/build overhead.
$env:GCAM_EVIDENCE_TESTS = '1'
dotnet test tests/Gcam.Studio.Services.Tests -c Release -m:1 /nr:false /p:UseSharedCompilation=false --filter FullyQualifiedName~WaveformServiceTests --logger 'console;verbosity=detailed'
Remove-Item Env:GCAM_EVIDENCE_TESTS
$env:GCAM_RENDER_SNAPSHOTS = '1'
dotnet test tests/Gcam.Studio.RenderTests -c Release -m:1 /nr:false /p:UseSharedCompilation=false --logger 'console;verbosity=detailed'
Remove-Item Env:GCAM_RENDER_SNAPSHOTS
```

Planned outputs: `docs/assets/studio-render/waveform-{real10us|rate-study|ideal}-{dark|light}-{1280x800|1440x900}.png` (12 images). No PNGs were generated during the CsI validation. The renderer constructs/detaches the production XAML tree, measures/arranges and uses RenderTargetBitmap; it never calls Show, Studio App startup, creates an HWND or sends input. Shared chain selectors are included in each full-shell image and remain locked while acquired data exist. The actual MC fixture uses default scene/optics/detector, seed 12345, first 128 accepted ListModeSource events; trigger index 64. Rate study uses 1000 kcps, fixed seed 555, preserving original acquisition times in the event records.

## Known limits and execution record

The ADC simulation adds analog/ADC noise after the analytic measurement. Shaped heights therefore are not the analytic MCA spectrum; recovered trapezoid energy is a scope estimate only. CR-RC recovered energy remains unavailable because trigger, phase and overlap estimation have not been validated; fractional state removes the whole-code accumulator deadband but is not an energy-estimator validation. No new resolution/pile-up precision test uses an invented tolerance. The small real-MC regression asserts deposit/time/pixel association, and the rate-study test asserts multiple pulses under explicitly simulated arrivals, not a promised real-scene overlap fraction.

Array-wide energy grouping follows the instrument's shared readout. The four ADC position channels, centroid positioning and pile-up mispositioning are absent. Background events follow the existing acquired stream. No afterglow or hand-added continuum is introduced.

Warm-up covers amplitude-dependent pulse support, eight tail constants and eight times the configured sum of nominal RC times but resets the local integer filter; it does not reproduce an acquisition-wide baseline state. CR-RC raw Q12 output is divided by 4096 into fractional output-code equivalents for display, without BLR. A partial acquisition window includes simulated baseline beyond live time and is labelled; its trapezoid energy is suppressed when the required readout horizon is unacquired. A capped request reserves warm-up inside the 10 M total raster samples, so its displayed length is slightly below 80 ms. Plot pyramids are built on the worker; integer shaper and pyramid calls are bounded but cancellation is checked around them rather than inside every iteration. Obsolete results cannot publish.

The first default parallel `dotnet build src/Gcam.Studio -c Release --no-restore` returned exit 1 without compiler errors. A sequential Studio build was then started and completed with **0 warnings /0 errors** (2m15.50s including compiler-server connection delays). While it ran, subsequent read-only PowerShell creation failed with `CreateProcessAsUserW failed: 5 (access denied)`. Per the requested fallback, no further verification commands were launched; the previously running build was only collected. Later tests, render paths, documentation and small source fixes had no final build/test result in that original execution. The subsequent headless execution below supplies current build/test evidence; visual snapshot inspection remains unexecuted for this change.

## CR-RC arithmetic and response evidence

Implemented recurrence: coefficients remain Q16; input is sign-extended and shifted by F=12; pole-zero
and each same-sample RC update retain fractional state. Raw output is divided by 4096.0 for plotted codes.
Existing C#/Python calls default to F=0 and retain the legacy golden 304-code peak / sample-35 value 23.
All CR-RC presets have order 4 with explicit T_sum 100/200/500 ns, K=20972/10486/4194. These are simulation
times under T_sum=order×nominal Euler RC time, not peaking times, rig evidence or equivalent DCR windows.
The DCR integration fields remain independent. CR-RC energy recovery is still unavailable.

The bounded arithmetic domain is signed-16 samples, A∈[0,65536], K∈[1,65536], order 1–16, F 0–12.
For B=2^(15+F), |imp|≤2B+1, every RC state remains in that interval by convexity of the floored update,
and |u−acc|≤4B+2. Thus the maximum product for F=12 is **35,184,372,219,904 < 2^46**. Signed **48-bit**
RTL states/products and C# long are sufficient for any input sequence in this domain, from reset, including
valid holds. RTL enforces WACC≥WIN+F+Q+3; C#/Python reject inputs/parameters outside the bounded domain.
This is an arithmetic bound, not physical hardware or Fmax validation.

### Executed checks

| Check | Result, 2026-10-02 |
|---|---|
| `dotnet build Gcam.sln -c Release -m:1 /nr:false /p:UseSharedCompilation=false` | Success; 0 errors, 2 existing xUnit analyzer warnings in unchanged assertions (`ImagingServiceTests`, `WaveformServiceTests`) |
| `dotnet test Gcam.sln -c Release --no-build -m:1 /nr:false` | Engine 272 passed; Studio Core 164 passed; Services 75 passed / 7 Evidence skipped; UI oracle 13 passed / 14 desktop skipped; renderer 1 skipped. **524 passed / 22 skipped / 0 failed**. No desktop or renderer opt-in enabled. |
| CR-RC new .NET coverage | 10 engine cases (metadata independence, low-energy retention with derived arithmetic bound, signed full-scale bounds, parameter rejection, vector export); 3 Services cases for fractional plotted codes/time labels/readout suppression |
| Icarus 12.0 / cocotb 2.0.1 / python.org 3.13 | **137 passed / 0 failed / 0 skipped**: 26 configurations × 5 CR-RC cases =130; 6 unchanged trapezoid and 1 BLR case. Supported GAGG/NaI/LYSO/BGO × three presets × F=0/12, plus legacy F=0/12. |
| C# / Python / actual RTL equality | **26 vectors, 452,608 output samples, zero mismatches**. Each compares independently pinned A/K/order/F/Q/width; finite-rise energies, threshold sweep, negative/noisy/full-scale histories, valid gaps, reset and exact accepting-edge output offset 0. |
| Preserved tolerance | Legacy 400→800 peak ratio still requires abs(ratio−2)<0.05, unchanged. This benchmark tolerance is not extended to whole-code low-energy preset paths; those use exact sample equality. |
| Derived precision tolerance | For the floating recurrence on identical rounded ADC samples and A/K, each RC update floor contributes <1 raw unit with accumulated error ≤1/K; deconvolution contributes <1. Maximum difference is bounded by **(1+order×65536/K)/4096 output codes**. All-sample tests use that bound, not a tuned keV tolerance. |
| Measurement artifact build | `dotnet build docs/assets/crrc-presets/Measure.csproj -c Release`: success, 0 warnings / 0 errors |

The prior source-inventory counts above are not current totals. Default tests pass; opt-in maximum-window,
cross-parts MC calibration, render and desktop evidence remains unexecuted in this change.
The full RTL suite passed 137 cases; after retaining an explicit finite-rise piled-pulse stimulus,
the final CR-RC-only matrix was rerun and passed all 130 cases with no failures/skips. The seven unchanged
trapezoid/BLR cases retain their full-suite result.

### Response remeasurement

The standalone [measurement source](assets/crrc-presets/Measure.cs) calls the **implemented**
`Waveform.CrrcInt` for all paths, including legacy F=0, shared-K Q12 and configured Q12. Complete data:
[amplitude.csv](assets/crrc-presets/amplitude.csv), [resolution.csv](assets/crrc-presets/resolution.csv).
No data were copied from a floating candidate and presented as implemented output.

Conditions: GAGG / S13360-3050 / AD9648, 125 MSPS; gain 4.0955 codes/keV, ENOB noise 1.32746148 codes RMS.
Finite input rise 90 ns; CSP tails 150/320/800 ns; matched A=62132/63918/64884. Each deterministic pulse
is isolated at sample 100 in 1024 samples, no ENOB/analog noise or amplitude smear. This finite-rise
measurement stimulus is distinct from Studio's instantaneous-rise ideal mode.

Entries below are peak output-code equivalents at **32 / 122 / 662 keV**:

| Preset | Legacy shared K, F=0 | Shared K, Q12 | Implemented preset K, Q12 |
|---|---|---|---|
| Fast | 0 / 5 / 47 | 2.505127 / 9.609375 / 51.970459 | **2.234131 / 8.518555 / 46.140381** |
| Original | 0 / 14 / 90 | 4.591064 / 17.458740 / 94.780518 | **2.625732 / 10.028809 / 54.438232** |
| Slow | 0 / 17 / 114 | 5.719238 / 21.741943 / 117.933350 | **1.567627 / 5.989258 / 32.526855** |

At **32.1 keV**, implemented Q12 maxima are **2.234131 / 2.634521 / 1.569580**. The inherited seed-1
ENOB-on stimulus still gives F=0 maxima **0/0/1** at 32.1 keV and **47/90/114** at 662 keV; removing
ENOB noise gives 0/0/0 at 32.1 keV. Q12 output fractions, not an extra plotted gain, retain the signal.
Configured floating peak times are **104/200/448 ns**. Nominal stage times are **25/50/125 ns**;
the difference reinforces why T_sum cannot be labelled peaking time.

Resolution repeats **6000 isolated traces** at each energy/mode/path. Sample noise seeds 1…6000,
amplitude RNG seed 901. Electronics = 3-keV-RMS analog sample noise plus ADC ENOB; full adds one
`FrontEndModel.Measure` amplitude draw before rasterising. Baseline = mean of 64 pretrigger shaped
samples. Read amplitude at the known noiseless floating peak time. For g_float=unquantised-input
floating 662-keV peak/662, report R_equiv=2.3548×sample SD/(g_float×E). These are Gaussian-equivalent
widths, not fitted FWHMs of the discrete/biased legacy distributions and not an implemented energy estimator.

Entries are percentages at **32 / 122 / 662 keV**:

| Preset/path | Electronics R_equiv % | Full R_equiv % | Full mean bias % |
|---|---|---|---|
| Fast legacy F=0 | 85.230 / 22.759 / 4.624 | 86.213 / 23.815 / 6.659 | −9.515 / −5.797 / −1.398 |
| Original legacy F=0 | 47.184 / 12.892 / 2.486 | 49.486 / 15.043 / 5.299 | −7.394 / −3.677 / −0.882 |
| Slow legacy F=0 | 37.547 / 10.264 / 1.887 | 39.787 / 12.978 / 4.934 | −5.104 / −3.224 / −0.660 |
| Fast shared-K Q12 | 78.925 / 20.710 / 3.817 | 80.235 / 22.145 / 6.013 | +0.387 / +0.099 / +0.015 |
| Original shared-K Q12 | 42.948 / 11.268 / 2.076 | 45.241 / 13.734 / 5.086 | +0.226 / +0.056 / +0.007 |
| Slow shared-K Q12 | 34.588 / 9.070 / 1.672 | 37.374 / 11.991 / 4.933 | +0.188 / +0.042 / +0.005 |
| Fast implemented | **59.071 / 15.497 / 2.855** | **60.781 / 17.381 / 5.456** | +0.288 / +0.073 / +0.010 |
| Original implemented | **16.176 / 4.242 / 0.782** | **21.317 / 8.848 / 4.693** | +0.034 / +0.005 / −0.002 |
| Slow implemented | **7.697 / 2.019 / 0.372** | **16.083 / 8.079 / 4.653** | −0.105 / −0.031 / −0.009 |

Expectation: these implemented-path results reproduce the previously evaluated common-order Q12
convention under the same seeds/definitions. They agree to the stored CSV precision (7 decimal places
for deterministic amplitude, 4 for width/bias). This is reproducibility evidence, **not** an adopted energy
accuracy tolerance. The physical analytic chain alone predicts approximately **13.81/7.686/4.569%**;
the additional waveform sample noise explains the larger measured widths. The predicted analytic
number has not been substituted for measured waveform resolution.

Configured full 32-keV width ranges over three disjoint 2000-trial batches are Fast **60.02–62.15%**,
Original **21.20–21.38%**, Slow **15.90–16.22%**; at 662 keV **5.418–5.513 / 4.672–4.719 / 4.619–4.694%**.
These are observed seed-batch spreads, not confidence intervals or pass thresholds. Q12 quantisation
granularity in calibrated output is **0.00350/0.00297/0.00497 keV**; it is not detector energy resolution.
27…37 keV in 0.01-keV increments yields configured **68/303/674 distinct peak levels**, all nonzero,
versus legacy **1/1/3 levels** and **100/100/56.54%** zero maxima. Input ADC rounding still causes plateaus.

Reproduction: run the supplied console project from a fresh `%TEMP%/gcam-*` working directory, passing
the absolute path to `docs/assets/crrc-presets/Measure.csproj` to `dotnet run -c Release --project ...`.
It writes `deterministic.csv`, `resolution.csv` and prints coefficient/staircase/reproduction summaries in
that scratch directory. Its three paths are `current`, `currentQ12`, `configured`; output CSV columns
retain the estimator/gain definitions above, plus gated-peak diagnostics and observed batch spread.
The gated peak is a diagnostic only: selecting maxima can reduce width while biasing the mean upward.

Not established here: threshold-trigger efficiency, arrival phase / variable-rise response, pile-up
energy recovery, full-scale analog dynamic range calibration, synthesis/device Fmax, physical rig topology,
desktop label layout or new offscreen renders. Four-channel charge division and hardware validation remain
separate. All coefficients/state tolerances were kept or derived; none was loosened to obtain a pass.

On 2026-10-02, the final sequential Release solution build passed with 0 errors and two existing xUnit analyzer warnings (xUnit2000 in ImagingServiceTests and xUnit2012 in WaveformServiceTests). `dotnet test Gcam.sln -c Release --no-build --no-restore -m:1 /nr:false` passed after that build. All three opt-in environment variables (numerical evidence, rendering and desktop) were unset.

| Suite | Passed | Skipped | Failed |
|---|---:|---:|---:|
| Engine | 274 | 0 | 0 |
| Studio Core | 165 | 0 | 0 |
| Studio Services | 78 | 7 | 0 |
| UI oracle / desktop assembly | 13 | 14 | 0 |
| Offscreen render assembly | 0 | 1 | 0 |
| Total | 530 | 22 | 0 |

The service skips are long numerical evidence; the UI/render skips execute no desktop or WPF rendering. Only the 13 headless oracles ran in the UI assembly. The full console output is retained in `samples/materials/CsI_validation.txt`; the Python table checks passed separately. Engine cases increase 262→274, Core 164→165 and Services 72→78 passed (seven evidence skips remain). Long numerical evidence, offscreen renders and desktop input remain independently opt-in; they are not covered by the default suite's pass result. RTL was not rerun because transport-data additions change no RTL, native shaper or waveform contract.
