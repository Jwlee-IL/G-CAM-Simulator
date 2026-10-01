# PLAN.Studio.Optics.Review — the implementer's review of the TODO-09 reference plan

Scope: Codex's measured review of [PLAN.Studio.Optics](PLAN.Studio.Optics.md) before implementation (2026-10-02);
the plan's "Decisions after review" section records what was adopted.

Date: 2026-10-02. **Review only; implementation should wait for a revised plan.**

The proposed separation of physical optics and decoder focus is sound, but the plan's coverage warning is false, preset success depends on the scene and decoding path, and refocus currently leaves the All reconstruction at the old plane. Keep editable optics and refocus; replace the inherited performance rules with measured, conditional evidence.

## Scope and reproduction

Read AGENTS.md, CLAUDE.md, PLAN.Studio.Optics, PLAN.Studio.Migration, AGENTS.Studio, VV.Gcam.Evidence (especially EV-01/02/03/06/10/15/33), Findings themes 1–3, 7 and 53, the requested engine studies, WPF handlers, SceneConfigBuilder, Studio acquisition/session/imaging services and view models. Also inspected DESIGN.Layout and Studio's actual XAML. No Studio launch, desktop tests, git mutation, source/test edits, or changes to the reference plan.

Repository was clean before the review. All experimental source, build products and outputs live in `%TEMP%\gcam-todo09-review`. Only this review is written in the repository. No repository build was run, to avoid generated repository files.

Commands actually run (PowerShell):

```powershell
# First exploratory version referenced existing Release engine DLLs.
# All numbers below were then rerun from CURRENT SOURCE with this scratch project.
$reviewScratch = Join-Path $env:TEMP 'gcam-todo09-review'
dotnet run --project (Join-Path $reviewScratch 'Review.csproj') -c Release |
    Tee-Object -FilePath (Join-Path $reviewScratch 'results.txt')
# Follow-up Program.cs, reproduced in Appendix B:
dotnet run --project (Join-Path $reviewScratch 'Review.csproj') -c Release |
    Tee-Object -FilePath (Join-Path $reviewScratch 'followup-results.txt')
git status --short
```

The scratch project targets net9.0, nullable and implicit usings enabled. It compiles the repository's `src/Gcam.{Core,Configuration,Masks,Detector,Decoding,Simulation,Studio.Services}/**/*.cs` and `src/Gcam.Studio.Core/Services/*.cs` directly, excluding each project's `obj/**` and `bin/**`. It uses no ProjectReference or prebuilt DLL in the final measurements; all outputs/restores are under scratch. Appendices contain both experimental programs. Engine defaults are preserved through the builder or Clone; no fabricated spectral features or replacement transport models.

Some read-only `exec_command` calls intermittently failed at process creation with `CreateProcessAsUserW failed: 5 (access denied)`. Specifically, batches reading `ListModeSource.cs`, `AcquisitionSnapshot.cs`, `MeasurementsViewModel.cs`, CLI commands, XAML and grep references, and an initial parallel read of SceneConfigBuilder/WPF references/git status/bin inventory were rejected before execution. Retried successfully; no engine measurement was blocked. Initial scratch compilation failed because the throwaway program used invalid C# literals such as `80.`; fixed to `80.0`, then ran successfully. Wrong guessed paths (`SceneSource.cs`, root Studio MainWindow, MixedFieldCommands.cs, MuraTests.cs) were corrected or not used as evidence.

### Measurement conventions

`F` is distance from detector to decoder focal plane; `D` is mask–detector distance; `S=F-D` only for a source on that plane. Current factory honours positive `Source.Position[2]`; the old comment saying z is ignored is stale. All scenes below set actual source z explicitly to F. A new decode plane never moves those sources.

Unless specified otherwise: 2×2 mosaic, 10 mm tungsten, engine attenuation model, no background; finite-mask cross-correlation; half-grid `0.95*p*c*F/(2D)` and step `max(0.2,c*F/(4D))`. Spatial success in the normalized-scene experiments means every one-to-one matched error is below one resolution element `r=cF/D`. This is a coarse localization gate, **not** sub-mm precision or blind detection. `TopPeaks` knows K and always selects candidates; a CLI printout saying “All sources localized” does not establish a pass.

## Item decisions

| Item | Decision | Required plan change |
|---|---|---|
| O-1 | confirmed, with corrections | Five physical fields are run inputs; validate effective inputs once; atomic preset apply; freeze acquisition inputs. |
| O-2 | corrected | All retained data suffice, but current code does not refocus every result. Separate decoder focus, rebuild All and isotope reconstructions/estimates/peaks, and refresh after completion too. |
| O-3 | corrected | Four historical geometries may remain selectable; do not claim every preset resolves every 3-source field. Define scene, count budget and per-channel vs broadband acceptance. |
| O-4 | corrected / dropped rules | Keep geometric values; remove coverage ✓ 0.9–1.4 and universal Nyquist ✓ ≥2. Label nominal cyclic field accurately. |
| O-5 | confirmed: drop | Do not migrate the extrapolated depth-reach formula. |
| O-6 | confirmed with alternative | Collapsible shared physical-optics and detector sections; decoder focus in Imaging controls. No separate Optics workspace needed for this scope. |
| O-7 | corrected | Builder clamps are frontend policy, not engine physics limits. Add finite/range/resource and cross-field validation. |
| Coverage inconsistency | corrected | Arithmetic is right; warning rule is wrong. Actual WPF slider also defaults to 1000 mm. |
| New: inherited evidence | new | EV-03/the configuration-scan headline is not reproduced under its stated settings; do not use it to set validation or warning thresholds. |

### O-1 — physical edits, normalization and provenance

`SceneConfigBuilder.Build` lines 39–54 clamps D to ≥1 mm, pitches to ≥0.05 mm, N to 4–64, and F to ≥D+1; snaps rank to the nearest prime. `FcfovHalfMm` clamps D/F and snaps rank but does **not** clamp cell pitch. Thus a displayed readout can disagree with the acquired config for invalid cell input. `MainViewModel.OnOpticsChanged` line 83 currently marks **any** optics replacement stale, including focal-only replacement.

Use one normalized/validated effective physical settings record for building configs, displayed derived numbers and preset equality. Display the effective prime, with lower-prime tie behavior as now; alternatively offer an explicit prime selector. Show `Custom` when a field diverges from a preset. Apply a preset once, avoiding five stale notifications/partial intermediate configs. Keep source coordinates/activity unchanged; show sources beyond the selected search field rather than repositioning them automatically.

Disable physical edits during acquisition, consistent with source inputs, or explicitly define pending next-run edits. Refocus must never decode acquired pixel indices using a newly edited detector pitch/N/D/rank. A physical edit marks stale; subsequent focus changes must not clear that stale state.

### O-2 — retained-data refocus: feasible, not implemented end-to-end

Confirmed retained data:

- `AcquisitionSession.ProduceAsync` retains cumulative raw accepted `DetectedEvent` records (pixel, unsmeared deposit, arrival time) and an immutable raw flood per snapshot. Its decoder is created once before the acquisition loop.
- `ImagingWorkspaceViewModel.Begin` lines 43–49 freezes scene and optics for the acquisition; subsequent ProcessAsync calls use `_optics`, not current Shared.Optics. A change to Shared.Optics alone neither changes this stored value nor requests a channel rebuild.
- `ImagingService.Process` retains measured energies and primary window floods, derives stripped floods from raw high floods, constructs a decoder on every call and recomputes isotope estimates and interpolated peaks.
- **Exception:** `ImagingService` line 138 inserts `snapshot.Imaging` unchanged as All. All's displayed found peaks are the union of isotope-channel peaks, not the broadband reconstruction's TopPeaks. That distinction must stay explicit.

Measured proof using Sharp, F=1000, a completed 20,000-event mixed snapshot, the same acquisition id, strip on, changing only F to 800 in ProcessAsync:

```text
All reconstruction origin = -54.031 mm (old = -54.031)
Cs reconstruction origin  = -43.225 mm (new plane)
Calibration time          = 0.000 ms
All flood reference       = same retained snapshot flood
```

So isotope refocus already works at service level, while All remains old. WPF `Refocus` lines 626–635 clones the live config and replaces only its live decoder, but its guard requires `_liveRunning`; it is not evidence of completed-run refocus. Also distinguish decoder focus from `MaskConfig.FocalDistanceMm`, which represents physically converging mask channels and **is a transport input**.

Recommended design:

1. Separate a decoder-focus/view setting from physical `OpticsSettings` semantics, even if compatibility overloads retain the existing type. Preserve a frozen acquisition context with effective physical optics, scene, detector settings and acquisition id alongside the retained snapshot.
2. Build a decode config from that context, replacing only source-plane geometry and derived reconstruction grid. All and isotope/stripped floods then go through the same projection helper. Keep the acquisition's original snapshot immutable; displayed refocused projections belong to ImagingView.
3. Recompute All reconstruction/estimate and all isotope reconstructions/estimates/peak coordinates, without new transport, remeasurement, new random draws or calibration. Existing calibration needs real scene distances, not view focus. Avoid cancellation paths discarding energy/calibration caches just for rapid focus changes; coalesce/debounce and publish only the latest snapshot+focus revision.
4. Refresh while Acquiring, Stopped and Completed, including when focus changes without a new snapshot. An arriving old-focus snapshot must not overwrite the selected-focus projection. Empty floods remain empty rather than showing fabricated maxima.
5. Preserve flood viewport/measurements. Reconstruction coordinates and ROI meanings change with focus: explicitly clear reconstruction measurements/drafts on focal change (with a visible indication), or define an angular-coordinate migration policy. Current Measurements.Refresh only changes ROI values; it leaves lengths/angles at old mm coordinates. Reset/reconcile reconstruction viewport and markers against new extents. Do not silently keep old-plane measurements.

Every current Studio decode path **can** be rebuilt from retained data. That conclusion covers current finite-mask cross-correlation, not future arbitrary algorithms or replay of changed physical hardware.

### O-3 — preset results and the meaning of “correctly”

Derived numbers (source plane = focus):

| Preset | F mm | r mm | nominal half-field mm | samples/cell | coverage periods |
|---|---:|---:|---:|---:|---:|
| Sharp | 1000 | 8.750 | 56.875 | 1.268 | 1.820 |
| Baseline | 1000 | 16.667 | 58.333 | 1.064 | 1.611 |
| Wide FOV | 1000 | 20.000 | 170.000 | 1.404 | 1.425 |
| High-res | 1000 | 5.000 | 32.500 | 1.389 | 2.437 |
| Sharp | 160 | 1.400 | 9.100 | 2.333 | 0.989 |
| Baseline | 160 | 2.667 | 9.333 | 1.600 | 1.071 |
| Wide FOV | 160 | 3.200 | 27.200 | 1.939 | 1.031 |
| High-res | 160 | 0.800 | 5.200 | 3.333 | 1.015 |

**Normalized spatial scene:** for each preset let h be its nominal half-field. Truths `(-0.5h,-0.3h), (0.5h,-0.3h), (0,0.5h)` are inside every search grid and separated by more than one r. 1,000,000 biased photons/run; TopPeaks K=3 and separation=r; no added counting noise.

With three equal-activity Cs-137 sources including their real X-ray lines, seeds 1,2,3: Sharp, Wide and High-res pass at both distances; Baseline passes at 160 but fails at 1000, with matched errors **2.946, 1.318, 61.149 mm** in all three seeds. Largest errors over the three seeds: Sharp 0.928/0.247 mm, Wide 5.701/0.566 mm, High-res 2.129/0.152 mm (1000/160). These are raw grid candidate coordinates, before per-peak interpolation.

**True mixed-isotope scene:** same fractional positions; Cs-137:Co-60:Co-57 activities 1:1:1.5; complete SceneSource isotope line lists; seed 12345. Broadband geometric `MixedFieldStudy` errors in truth order:

| Preset | F=1000: Cs, Co-60, Co-57 error mm | F=160: errors mm | All within r? (1000 / 160) |
|---|---|---|---|
| Sharp | 0.692, 0.692, 0.928 | 0.247, 0.247, 0.148 | yes / yes |
| Baseline | 20.113, 1.318, 5.559 | 8.200, 1.135, 14.724 | no / no |
| Wide FOV | 140.828, 5.701, 3.808 | 0.566, 0.253, 0.609 | no / yes |
| High-res | 2.129, 1.425, 0.530 | 0.063, 0.063, 0.152 | yes / yes |

This geometric detector experiment is the engine's broadband spatial study, **not** Studio's Compton/list-mode acquisition. Activity and energy-dependent mask transmission make the peaks unequal; the coverage number alone cannot explain or guarantee their detection.

**Studio path:** same mixed scene, transport via current ListModeSource, exactly 20,000 accepted events per preset/distance. SimulationService.BuildConfig with default DetectorSettings: entrance 0.15 mm, backing 2 mm, reflector gap 0.1 mm, gain sigma 3%, gain seed 1. MeasurementStage and ImagingService use the physical default front end, 1.5 FWHM windows; stripping calibration 100,000 accepted events per high-isotope group. The same events are reused for strip off/on. These are independent fresh MC events, not resampled deposits. One seed/scene/count budget; interpolated channel peak errors:

| Preset | F | strip off: Cs / Co-60 / Co-57 mm | strip on: errors mm |
|---|---:|---|---|
| Sharp | 1000 | 1.058 / 1.419 / 0.306 | 1.168 / 1.419 / 0.297 |
| Baseline | 1000 | 2.951 / 3.101 / 0.847 | 4.050 / 3.101 / 0.842 |
| Wide FOV | 1000 | 12.627 / 6.322 / 1.593 | 12.160 / 6.322 / 1.608 |
| High-res | 1000 | 0.557 / 1.698 / 0.125 | 0.750 / 1.698 / 0.123 |
| Sharp | 160 | 0.371 / 0.100 / 0.077 | 0.387 / 0.100 / 0.078 |
| Baseline | 160 | 0.795 / 1.185 / 0.215 | 0.714 / 1.185 / 0.213 |
| Wide FOV | 160 | 0.706 / 0.928 / 0.197 | 0.806 / 0.928 / 0.193 |
| High-res | 160 | 0.016 / 0.038 / 0.043 | 0.022 / 0.038 / 0.043 |

**All four pass the one-r gate in their per-isotope channels at both distances, in this experiment.** This does not guarantee broadband success, sub-mm accuracy at 1 m, arbitrary activity ratios, or same-isotope resolution. At 1000 mm strip-off channel counts were respectively Sharp 1439/892/9067, Baseline 1348/781/9193, Wide 1712/1120/8085 and High-res 1607/1032/8248; these matter more than total event count alone.

**Original CLI scene, separately measured:** exact CLI positions `(5,1),(-6,3),(0,-6)`; activities 1/1/1.5; its explicit single Cs/Co-57 lines and two Co-60 lines; 3,000,000 photons; seed 12345; non-cyclic, 0.4 mm grid, TopPeaks separation 3 mm. Only physical optics/focal plane differ.

| Preset | F=1000 errors mm | F=160 errors mm |
|---|---|---|
| Sharp | 6.011, 6.666, 0.567 | 0.064, 0.161, 0.290 |
| Baseline | 10.047, 11.035, 1.918 | 0.550, 0.275, 0.929 |
| Wide FOV | 5.148, 7.212, 2.596 | 0.761, 0.165, 0.288 |
| High-res | 8.070, 0.426, 2.360 | 2.189, 2.211, 10.261 |

At 160 mm High-res search half-width is only 4.94 mm, so Co-60 x=-6 and Co-57 y=-6 are outside it. At 1000 mm these fixed positions are closely spaced relative to r; coarse gates can call a nearby sidelobe “localized” without resolving three distinct sources. For example Sharp's returned candidates all have x between -3.63 and 0.37, whereas truth includes x=5. A claim that all four are universally MC-verified for three sources is therefore rejected.

**Plan recommendation:** retain the four as historical engineering geometries, keep Sharp default, and document conditional performance. Do not silently resize detectors as focus moves: that is a different instrument. Do not retune merely to force coverage into an invented band. If “all presets resolve this specified same-isotope scene at 1 m” is an acceptance requirement, tune Baseline's pixel pitch/N in a separate controlled MC comparison, preserving physical size where possible, then record the new preset as a deliberate change. No such retuned preset was measured here.

### O-4 and coverage — values are useful; universal warnings are not

Confirm formulas for square detector and matching source/focal plane:

```text
resolution element r = c*F/D
nominal cyclic half-field h = p*c*F/(2D); full field/r = p
cell shadow a = c*F/(F-D)
samples per cell = a/pixelPitch
coverage C = N*pixelPitch/(p*a)
physical mask width = mosaic*p*c (2*p*c here)
```

Call r a **resolution element**, not achieved localization error. Call h a **nominal cyclic field/search scale**, rather than an unconditional fully coded or usable field. The finite aperture and actual detector size matter: for an ideal thin square mask, geometric full-illumination half-field would be `max(0,[maskWidth - (1-D/F)*detectorWidth]*F/(2D))`, not automatically h. This is an analytical containment calculation, not a measured usable-field rule; the real slab/channel model changes illumination further. For Sharp at F=1000 the thin-plane margin is 10.25 mm versus nominal h=56.875 mm. Actual measured source localization at ±28.4 mm succeeds despite partial illumination. Do not turn this analytical expression into a performance warning without further MC.

**Coverage sweep:** current-source Sharp, c=0.7, D=80, pitch=0.6; vary N only. Single Cs source, full lines, 100,000 biased photons/point, seed 12345, 5×5 truths at x,y/h in `{-0.8,-0.4,0,0.4,0.8}`. Non-cyclic; success error<r; no post-MC Poisson realization.

| N | C at 1000 | passes/25 | median / max error mm | C at 160 | passes/25 | median / max mm |
|---:|---:|---:|---|---:|---:|---|
| 8 | 0.485 | 13 | 5.701 / 73.098 | 0.264 | 0 | 13.265 / 22.521 |
| 12 | 0.728 | 25 | 1.182 / 2.931 | 0.396 | 18 | 0.309 / 8.991 |
| 16 | 0.971 | 25 | 0.798 / 1.714 | 0.527 | 23 | 0.208 / 10.003 |
| 20 | 1.213 | 25 | 0.710 / 1.734 | 0.659 | 25 | 0.128 / 0.331 |
| 24 | 1.456 | 25 | 0.634 / 1.673 | 0.791 | 25 | 0.140 / 0.338 |
| 30 | 1.820 | 25 | 0.582 / 1.600 | 0.989 | 25 | 0.114 / 0.316 |
| 40 | 2.426 | 25 | 0.434 / 1.591 | 1.319 | 25 | 0.111 / 0.320 |
| 50 | 3.033 | 25 | 0.334 / 1.009 | 1.648 | 25 | 0.110 / 0.184 |
| 64 | 3.882 | 25 | 0.314 / 1.359 | 2.110 | 25 | 0.082 / 0.190 |

The 0.9–1.4 band is **neither necessary nor sufficient** as a general non-cyclic localization guarantee. No universal replacement numerical band is established. Larger coverage up to 3.882 works here; coverage below 0.9 also works in some cases. Other geometries/counts/backgrounds can behave differently. Report the number without a pass/fail glyph; a small coverage can motivate an experiment, not an asserted hard threshold. Count density, pixel sampling, rank, source mixture/separation, finite-mask response, background and the search grid all matter.

The inconsistency is not explained by assuming the current WPF defaults to 160: its actual XAML line 227 sets **Minimum=200, Maximum=3000, Value=1000**. The handler's `??160` fallback is not the normal focal setting. The headless 160 runs above answer the requested historical comparison; that plane cannot be selected on the current WPF slider.

**Sampling remeasurement:** `ArrayStudy` with the exact CLI settings: scenario.json, physical detector 12 mm, photonBudget=300,000 emitted-equivalent photons, 250 Poisson realizations, centered source, failure>3 mm, seed 12345, MC mean 1,000,000 histories.

| samples/cell | array | RMS mm | failure fraction |
|---:|---:|---:|---:|
| 0.800 | 6×6 | 4.075 | 0.204 |
| 1.067 | 8×8 | 1.730 | 0.044 |
| 1.600 | 12×12 | 1.688 | 0.028 |
| 2.133 | 16×16 | 0.957 | 0.012 |
| 2.667 | 20×20 | 0.981 | 0.016 |
| 3.200 | 24×24 | 1.113 | 0.016 |
| 4.000 | 30×30 | 0.872 | 0.004 |

This supports EV-06's approximate 21% failure at 0.8 and the ~2 sampling design target under its stated count budget. It does **not** establish that <2 fails or ≥2 succeeds everywhere. A higher-budget check (MC mean 500,000 histories, photonBudget 1,000,000, 100 repeats) gave 0.8 samples/cell RMS 0.139 mm and zero failures: the centered-source symmetry and count level can conceal undersampling. Preset successes above also occur at 1.268–1.404 samples/cell. Label the readout `samples per mask cell`; use an explanatory target “about 2 in the baseline array study” rather than a universal Nyquist checkmark.

**FOV check:** actual FieldOfViewStudy, scenario_handheld.json; S=1000 mm (**F=1055**, not 1000); x direction; MC mean 1,000,000 photons; N0=5000; BSR=0; 50 Poisson repetitions; default wide grid ±20°, step 0.3°. Non-cyclic success fractions at 0/3/5/7/10° = **1/1/1/1/0.28**; cyclic = **1/1/0/0/0**; 10° wrong in-field fraction = **0.72**. This agrees with EV-02's directional, count-dependent extension and residual ghosts. It does not calibrate any Sharp coverage threshold. Do not describe finite-mask decode or grid clipping as eliminating ghosts.

### O-5 — depth hint

Confirm dropping `0.15*sqrt(aperture*D/(18.2*80))`. WPF itself calls it rough and anchors just one geometry; EV-33 discusses limited depth recovery, not a validated universal aperture×D law. No aperture/D sweep of that formula was run in this review. TODO-11 can add a studied depth result later. Retain physical aperture width as a geometry value, without translating it into reach.

### O-6 — layout

Actual XAML has one shared left ScrollViewer (line 138), Geometry facts (209 onward), then detector facts/inputs. DESIGN.Layout gives 300 DIP side panels and a fixed centre/right workspace layout. The reference plan's exact 1280×800 overflow observation was **not desktop-measured** here.

Prefer collapsible **Physical optics** and **Detector** sections in the shared panel, with effective rank/cell/D and N×N/pitch summaries when collapsed. Keep preset selector with physical inputs. Put **Decoder focal plane (mm from detector)** and associated projected-field values in Imaging's workspace panel because it operates on retained data. This follows Migration's run-input/view-setting distinction and avoids a global-looking focal control on Spectrum. A separate Optics workspace adds navigation for just five fields and separates scene feedback from editing; reserve it for a future geometry visualization if needed.

Validation remains visible when collapsed; auto-expand errors on Start. Focal control stays enabled during/after acquisition; physical editors use IsIdle. Use existing theme resources, accessible labels/unit suffixes, stable AutomationIds, commit-on-focus-loss/Enter for text inputs, and atomic preset changes. Validate via headless renders at the agreed small window sizes, both themes and keyboard tab order in the later UI phase; do not claim desktop verification from this review.

### O-7 — validation and limits

The engine has no comprehensive optics validator. ConfigLoader just deserializes; DetectorImage allocates width×height, decoder allocates n×n where `n=round(2half/step)+1`; nearest-prime search has no practical resource cap. MuraGenerator.Basic rejects non-primes but accepts any prime ≥2. Acceptance by this function is not proof that every accepted rank is a useful imaging design.

Verified existing frontend policies: N integer 4–64; D≥1 mm; c,pitch≥0.05 mm; F≥D+1 mm; minimum recon step 0.2 mm. There are no corresponding universal engine upper bounds for D/c/pitch/F/rank. Preserve these compatibility minima if desired, but document them as Studio policies; do not call them measured hardware limits.

Recommended requirements before enabling Start/refocus:

- Reject NaN, infinity, missing/non-numeric values; do not silently accept a display value that builds a different instrument. Rank/N integer; effective prime shown; lower-prime ties tested.
- Use the same effective settings in derived readout and Build. Check F>D for the **acquired** physical D during refocus; any stricter D+1 minimum is a chosen policy.
- Pixel pitch must exceed the configured reflector gap. Studio's default gap is 0.1 mm; the builder's 0.05 pitch minimum alone allows a configuration that SimulationService.BuildConfig rejects. Prefer a cross-field error instead of changing the gap invisibly.
- Changing D must validate actual scene source distances against the physical mask placement. ListModeSource checks positive finite source z but does not ensure a source lies in front of the mask. A decoder focal plane is not the actual source plane and must not move sources as a repair.
- Establish a bounded supported rank/input range and decoder-work allocation budget before shipping; cap at a finite documented set/range backed by the intended test matrix, not an unbounded prime search. A reasonable initial UI can expose tested ranks explicitly and require a planner decision for additional primes. Hard upper bounds are **new resource/UX policy decisions**, not claims derived from the existing engine.
- Bound total computed grid work and allocation (effective rank, n² and detector N²), and distinguish invalid configuration from a low-performance but valid experiment. Do not validate physics by the discarded coverage/sampling pass bands.

No exhaustive extreme-rank, overflow, invalid-focal or malformed-number execution sweep was run; these findings follow the constructors, builder and allocation paths. Add meaningful validation tests during implementation.

## New finding: configuration evidence needs correction

Re-ran the stated scan conditions for two rows, using ParameterScan itself: scenario.json, S=100 fixed, detector 12×12@1 mm, cyclic auto-grid, cell=1 mm, D=20 mm, rank=7/23, **11×11** sweep to 0.95 nominal field, **200,000 photons/point**, seed 12345+k.

| rank | nominal half-field mm | r mm | median error mm | usable fraction |
|---:|---:|---:|---:|---:|
| 7 | 21.000 | 6.000 | 1.318 | 0.975 |
| 23 | 69.000 | 6.000 | 65.458 | 0.149 |

The rank-23 “usable ±64 mm (96%)” headline in Findings theme 3 / EV-03 is **not reproduced**. An initial coarser 5×5/100,000-photon check also gave 0.160 usable, so this is not merely a low-count fluctuation in one row. The full configuration scan was not run; no claim is made about the true optimum over all configurations. Do not silently amend EV-03 in this turn or invent a cause. Planner should require a focused investigation/remeasurement before using its claimed optimum. The identity `nominal full-field/r=rank` remains algebraically true; it is not a law guaranteeing usable FOV for free.

## Proposed revised acceptance/evidence plan

1. Pure effective-settings/derived-geometry tests: presets, finite input, snapping ties, readout/config equality, gap/pitch and D/source constraints. Separate resource-policy tests from physics evidence.
2. Refocus service tests use real retained MC snapshot data: assert raw flood/event arrays/counts/live-time unchanged, spectrum unchanged, every All/isotope/stripped reconstruction and estimate agrees with an independent decoder at the new plane, peaks refreshed, no recalibration/remeasurement. Include zero counts and stopped/completed snapshots.
3. View-model tests: physical edit stale, focal edit never introduces stale or clears existing stale, atomically applied presets/Custom, acquired geometry survives current physical edits, rapid changes/latest revision wins, old acquisition completion cannot overwrite a newer acquisition's view. Exercise no-new-snapshot refocus and live-snapshot arrival races.
4. Physics evidence explicitly names scene positions, actual depths, isotope lines/activity ratios, count budgets/seeds, window/strip options, physical detector settings, decode grid and localization tolerance. Include same-isotope spatial separation and unequal mixed-isotope channels; a per-line result is not a broadband guarantee. Keep the normalized scene and original-CLI regression separate.
5. Do not grow implementation tests into the full scan/FOV suite on every change. Pin a small deterministic real-engine regression for the preset/refocus paths, and retain the larger measurements as evidence with reproducible harnesses. MC precision/acceptance claims need a multi-seed/count matrix before being promoted beyond these examples.
6. Update SRS optics/refocus/readout rows and exclusions, SDS frozen-input/projection/cache design, VV.Studio evidence and DESIGN.Layout after agreement/implementation. Physics-evidence correction is a separately justified revision, not a copied preset comment. Headless render review comes next; desktop tests remain last under the migration plan.

Not measured here: full scan optimum, complete FOV angular/directional/count/background matrix for all four presets, all same-isotope separation/activity ratios, tuned replacement presets, depth-reach scaling, exhaustive engine limits, physical hardware, or desktop layout/accessibility. Those are not silently treated as passes.

## Appendix A — main measurement program

The following is the actual final current-source Program.cs used for `results.txt` (stored outside the repository as FullProgram.txt).

```csharp
using Gcam.Configuration;
using Gcam.Simulation;
using Gcam.Core;
var factory = new DefaultSimulationFactory();
var presets = new[] { ("Sharp",13,.7,80.0,30,.6), ("Baseline",7,1.0,60.0,12,1.0), ("Wide",17,1.0,50.0,34,.75), ("Highres",13,.5,100.0,44,.4) };
foreach (double focal in new[]{1000.0,160.0}) foreach(var p in presets) {
 var opt = new OpticsSettings { MuraRank=p.Item2,CellPitchMm=p.Item3,MaskDetectorDistanceMm=p.Item4,DetectorPixels=p.Item5,PixelPitchMm=p.Item6,FocalDistanceMm=focal };
 double half=SceneConfigBuilder.FcfovHalfMm(opt), res=p.Item3*focal/p.Item4;
 var scene=new[]{new SceneSource{X=-.5*half,Y=-.3*half,DistanceMm=focal},new SceneSource{X=.5*half,Y=-.3*half,DistanceMm=focal},new SceneSource{X=0,Y=.5*half,DistanceMm=focal}};
 double shadow=p.Item3*focal/(focal-p.Item4), cov=p.Item5*p.Item6/(p.Item2*shadow);
 for(int seed=1;seed<=3;seed++) {
 var cfg=SceneConfigBuilder.Build(scene,opt,1_000_000,seed); // three equal Cs sources, full real emission lines
 var r=new MixedFieldStudy(factory).LocalizeMultiple(cfg,3,res);
 var errors=MixedFieldStudy.MatchOneToOne(r.TruthXY,r.Found).Select(x=>x.ErrorMm).ToArray();
 Console.WriteLine($"PRESET {p.Item1} F={focal} seed={seed} half={half:F3} res={res:F3} sample={shadow/p.Item6:F3} cov={cov:F3} errors={string.Join(",",errors.Select(x=>x.ToString("F3")))} peaks={string.Join(";",r.Found.Select(x=>$"{x.Xmm:F2}/{x.Ymm:F2}"))}");
 }
}
// independent coverage sweep: Sharp, fixed pixel pitch, changing N; noncyclic 5x5 single-source sweep
foreach(double focal in new[]{1000.0,160.0}) foreach(int n in new[]{8,12,16,20,24,30,40,50,64}) {
 var opt=new OpticsSettings{FocalDistanceMm=focal,DetectorPixels=n};
 double half=SceneConfigBuilder.FcfovHalfMm(opt),res=.7*focal/80;
 var errs=new List<double>();
 foreach(double x in new[]{-.8,-.4,0.0,.4,.8}) foreach(double y in new[]{-.8,-.4,0.0,.4,.8}) {
 var cfg=SceneConfigBuilder.Build(new[]{new SceneSource{X=x*half,Y=y*half,DistanceMm=focal}},opt,100_000,12345);
 var r=new SimulationRunner(factory).Run(cfg); var q=r.Estimate!.Position;
 errs.Add(Math.Sqrt(Math.Pow(q.X-x*half,2)+Math.Pow(q.Y-y*half,2)));
 }
 errs.Sort();Console.WriteLine($"COVER F={focal} N={n} cov={n*.6/(13*.7*focal/(focal-80)):F3} pass={errs.Count(x=>x<res)}/25 median={errs[12]:F3} max={errs[^1]:F3}");
}


// actual isotope mix, matching CLI lines / relative activities; same fractional positions
foreach(double focal in new[]{1000.0,160.0}) foreach(var p in presets) {
 var opt=new OpticsSettings { MuraRank=p.Item2,CellPitchMm=p.Item3,MaskDetectorDistanceMm=p.Item4,DetectorPixels=p.Item5,PixelPitchMm=p.Item6,FocalDistanceMm=focal };
 double half=SceneConfigBuilder.FcfovHalfMm(opt),res=p.Item3*focal/p.Item4;
 var scene=new[]{new SceneSource{Isotope="Cs-137",X=-.5*half,Y=-.3*half,DistanceMm=focal,ActivityUCi=1},new SceneSource{Isotope="Co-60",X=.5*half,Y=-.3*half,DistanceMm=focal,ActivityUCi=1},new SceneSource{Isotope="Co-57",X=0,Y=.5*half,DistanceMm=focal,ActivityUCi=1.5}};
 var cfg=SceneConfigBuilder.Build(scene,opt,1_000_000,12345);
 var r=new MixedFieldStudy(factory).LocalizeMultiple(cfg,3,res);
 Console.WriteLine($"MIX {p.Item1} F={focal} errors={string.Join(",",MixedFieldStudy.MatchOneToOne(r.TruthXY,r.Found).Select(x=>x.ErrorMm.ToString("F3")))}");
 var det=new Gcam.Studio.Core.Services.DetectorSettings();
 cfg=Gcam.Studio.Services.SimulationService.BuildConfig(scene,opt,det);
 var events=new List<DetectedEvent>();var flood=new DetectorImage(p.Item5,p.Item5);
 using(var src=new ListModeSource(cfg)) { while(events.Count<20_000) if(src.Advance() is {} ev){events.Add(ev);flood.Add(ev.PixelX,ev.PixelY,1);} }
 var decode=factory.CreateDecoder(cfg)!.Decode(flood);
 var image=new Gcam.Studio.Core.Services.ImagingResult(flood.ReadOnlyCopy(),-(p.Item5-1)*p.Item6/2,p.Item6,decode.Reconstruction.ReadOnlyCopy(),decode.ReconOriginMm,decode.ReconStepMm,decode.Estimate,events.Count,TimeSpan.Zero);
 var snapshot=new Gcam.Studio.Core.Services.AcquisitionSnapshot(events[^1].ArrivalTimeS,events.Count,0,0,false,image,events.AsReadOnly(),TimeSpan.Zero,true){Detector=det};
 var svc=new Gcam.Studio.Services.ImagingService();var id=Guid.NewGuid();
 foreach(bool strip in new[]{false,true}) {
 var view=await svc.ProcessAsync(id,snapshot,scene,opt,new(1.5,strip));
 Console.WriteLine($"STUDIO {p.Item1} F={focal} strip={strip} " + string.Join(";",view.Channels.Skip(1).Select((ch,i)=>$"{ch.Isotope}:counts={ch.Image.EffectiveCounts:F0},err={Math.Sqrt(Math.Pow(ch.Peaks[0].Xmm-scene[i].X,2)+Math.Pow(ch.Peaks[0].Ymm-scene[i].Y,2)):F3}")));
 }
 if(p.Item1=="Sharp" && focal==1000) {
 var focused=await svc.ProcessAsync(id,snapshot,scene,opt with{FocalDistanceMm=800},new(1.5,true));
 Console.WriteLine($"REFOCUS All origin {focused.Channels[0].Image.ReconOriginMm:F3} vs old {image.ReconOriginMm:F3}; Cs origin={focused.Channels[1].Image.ReconOriginMm:F3}; calibration_ms={focused.CalibrationTime.TotalMilliseconds:F3}; flood_reference_same={ReferenceEquals(focused.Channels[0].Image.Flood,snapshot.Imaging.Flood)}");
 }
}
var baseCfg=ConfigLoader.Load(@"C:\Users\leonh\source\repos\G-CAM-Simulator\samples\scenario.json");
baseCfg.PhotonCount=500_000;
foreach(var a in new ArrayStudy(factory).Run(baseCfg,12,new[]{2.0,1.0,.75,.5},1_000_000,100,3))Console.WriteLine($"ARRAY samples={a.SamplesPerCellShadow:F3} rms={a.RmsMm:F3} fail={a.FailRate:F3}");
foreach(var s in new ParameterScan(factory).Run(baseCfg,new[]{7,23},new[]{1.0},new[]{20.0},5,100_000))Console.WriteLine($"SCAN rank={s.Rank} half={s.FcfovHalfMm:F3} res={s.ResolutionMm:F3} median={s.MedianErrorMm:F3} usable={s.UsableFraction:F3}");
foreach(var f in new FieldOfViewStudy().Run(ConfigLoader.Load(@"C:\Users\leonh\source\repos\G-CAM-Simulator\samples\scenario_handheld.json"),1000,0,new[]{0.0,3.0,5.0,7.0,10.0},new[]{5000.0},new[]{0.0},50))Console.WriteLine($"FOV angle={f.AngleDeg:F1} nc={f.LocalizedNonCyclic:F3} cy={f.LocalizedCyclic:F3} false_in={f.FalseInField:F3}");
```

## Appendix B — fixed CLI-scene and exact scan/array follow-up

The same scratch project, with this Program.cs, produced followup-results.txt.

```csharp
using Gcam.Configuration;
using Gcam.Simulation;
var factory=new DefaultSimulationFactory();
var presets=new[]{("Sharp",13,.7,80.0,30,.6),("Baseline",7,1.0,60.0,12,1.0),("Wide",17,1.0,50.0,34,.75),("Highres",13,.5,100.0,44,.4)};
foreach(double focal in new[]{1000.0,160.0}) foreach(var p in presets) {
 var opt=new OpticsSettings{MuraRank=p.Item2,CellPitchMm=p.Item3,MaskDetectorDistanceMm=p.Item4,DetectorPixels=p.Item5,PixelPitchMm=p.Item6,FocalDistanceMm=focal};
 var cfg=SceneConfigBuilder.Build(Array.Empty<SceneSource>(),opt,3_000_000,12345);
 cfg.Sources=new[]{new SourceConfig{Position=[5,1,focal],ActivityBq=1,Lines=[new(){EnergyKeV=661.7,Intensity=.851}]},new SourceConfig{Position=[-6,3,focal],ActivityBq=1,Lines=[new(){EnergyKeV=1173.2,Intensity=.999},new(){EnergyKeV=1332.5,Intensity=.999}]},new SourceConfig{Position=[0,-6,focal],ActivityBq=1.5,Lines=[new(){EnergyKeV=122.1,Intensity=.856}]}};
 cfg.Decoder.ReconStepMm=.4;
 var r=new MixedFieldStudy(factory).LocalizeMultiple(cfg,3,3);
 Console.WriteLine($"CLI_SCENE {p.Item1} F={focal} errors={string.Join(",",MixedFieldStudy.MatchOneToOne(r.TruthXY,r.Found).Select(x=>x.ErrorMm.ToString("F3")))} peaks={string.Join(";",r.Found.Select(x=>$"{x.Xmm:F2}/{x.Ymm:F2}"))}");
}
var b=ConfigLoader.Load(@"C:\Users\leonh\source\repos\G-CAM-Simulator\samples\scenario.json");
foreach(var s in new ParameterScan(factory).Run(b,new[]{7,23},new[]{1.0},new[]{20.0},11,200_000))Console.WriteLine($"SCAN_EXACT rank={s.Rank} half={s.FcfovHalfMm:F3} res={s.ResolutionMm:F3} median={s.MedianErrorMm:F3} usable={s.UsableFraction:F3}");
foreach(var a in new ArrayStudy(factory).Run(b,12,new[]{2.0,1.5,1.0,.75,.6,.5,.4},300_000,250,3))Console.WriteLine($"ARRAY_EXACT samples={a.SamplesPerCellShadow:F3} rms={a.RmsMm:F3} fail={a.FailRate:F3}");
```


## Appendix C — scratch project creation

Run from repository root; paste Appendix A or B into scratch Program.cs. This compiles the current source without writing engine build artifacts into the source tree.

```powershell
$reviewScratch = Join-Path $env:TEMP 'gcam-todo09-review'
New-Item -ItemType Directory -Force -Path $reviewScratch | Out-Null
$repoRoot = (Get-Location).Path
$includes = @('Gcam.Core','Gcam.Configuration','Gcam.Masks','Gcam.Detector',
    'Gcam.Decoding','Gcam.Simulation','Gcam.Studio.Services') | ForEach-Object {
    '<Compile Include="' + $repoRoot + '\src\' + $_ + '\**\*.cs" Exclude="' +
    $repoRoot + '\src\' + $_ + '\obj\**;' + $repoRoot + '\src\' + $_ + '\bin\**" />'
}
$includes += '<Compile Include="' + $repoRoot + '\src\Gcam.Studio.Core\Services\*.cs" />'
Set-Content -LiteralPath (Join-Path $reviewScratch 'Review.csproj') -Value (
    '<Project Sdk="Microsoft.NET.Sdk"><PropertyGroup><OutputType>Exe</OutputType>' +
    '<TargetFramework>net9.0</TargetFramework><ImplicitUsings>enable</ImplicitUsings>' +
    '<Nullable>enable</Nullable></PropertyGroup><ItemGroup>' + ($includes -join '') +
    '</ItemGroup></Project>')
```

Original local output SHA256 (Windows Tee-Object encoding/newlines are part of these hashes):

- results.txt: `01D4EA368472CF5EB350AA596ED01AF45A14BC4F559CEC111B31AD23E8C1491E`
- followup-results.txt: `8B3AAC7B2BC87C8D64A8ACC0F850EC759FB635AE8702042351CE040F499A9D0E`

Final verification: `git diff --stat` empty; `git status --short` reports only `?? docs/reviews/`. No tracked source/test/doc was modified and no git index/HEAD operation was performed.
