# PLAN.Studio.Detector.Review — code findings and blocked numerical verification

Scope: TODO-11 reference-plan review only, 2026-10-02; Detector workspace, retained-data depth estimation and external range comparison. No implementation. This review is **incomplete because process creation became unavailable**; it does not establish T-2, T-5 or T-7 numerically.

## Recommendation to the planner

Proceed with the gap/contact inputs and a static detector-face design after checking the remaining code. Keep depth estimation experimental until the requested Studio-geometry measurements exist. Do not describe a focus-curve width as an uncertainty interval, import theme 24's precision, or quote theme 35's optimum for Studio. The reference plan is wrong if it treats any of those as established properties of the proposed implementation.

T-3 is a sensible removal: the repository-wide search found no SiPM-pitch setting in the engine or Studio. Preserve the fact that the original readout was four-channel Anger positioning, not one digitized channel per crystal. Matching crystals to SiPMs 1:1 does not make the engine's direct pixel assignment a model of that readout.

## Evidence boundary and execution failure

Initial PowerShell reads and `rg` searches succeeded. Subsequent commands failed before starting a process with:

```text
CreateProcessAsUserW failed: 5 (access denied)
```

Blocked calls were:

- Reading `SimulationService`, `ImagingService`, `SceneSource`, `DetectorSettings`, the scoring/crosstalk section of `ComptonCrystalDetector`, and the relevant `MixedFieldStudy` members.
- Reading the requested Findings theme bodies and `MixedFieldStudy` with a second shell selection; the reported launcher still used `pwsh.exe`.
- `dotnet --info`, with `login:false`, cwd `C:\Windows\Temp`; it failed with the same error independently of repository cwd.

No temporary measurement project could be created or executed. No MC samples, live-time counts, depth errors, focus widths, sweep costs, or significance values were measured. No build, test, desktop test or Studio launch was performed. No git commands were issued. The only requested repository edit is this review file.

Successful reads included the planning procedure, root AGENTS and CLAUDE, the complete Detector reference plan, `SceneConfigBuilder`, `ListModeSource`, `ImagingProjection`, `AcquisitionSession`, `MeasurementStage`, `OpticsPolicy`, and substantial `DepthStudy` code. The combined reads of Migration, ImagingOptions, Optics, Waveform, Studio guidance and Waveform review were output-truncated; Migration and I-2 were visible, but this is not a claim to have completed every requested document read. Searches located the requested Findings themes and Wpf/Studio members; several source bodies remained unread. PortView-specific persistent memory is not applicable to this GCAM-only task.

## Every “What exists” row

“Partial” below means the row must remain open, not that its unchecked portion is accepted.

| Reference-plan row | Disposition | Evidence / correction |
|---|---|---|
| Reflector gap, engine | Partially confirmed | `ListModeSource` passes `d.ReflectorGapMm`. Search results in `ComptonCrystalDetector` show an entry-gap rejection at line 125 and a backing-scatter re-entry gap check at line 168. `ComptonFactory`, `EventStreamStudy`, and `DoseStudy` call sites also pass the setting. Full constructor/gap geometry inspection blocked. |
| Reflector gap, Studio | Confirmed by code/search | `DetectorSettings` line 11 defaults to 0.1 mm. `MainViewModel.Detector` line 106 constructs only gain sigma, gain seed and chain. MainWindow line 267 displays a fact. `SimulationService` validation/copy sites were found at lines 26, 33–41. `OpticsPolicy.Validate` requires pitch greater than gap. Service body read blocked. |
| Optical crosstalk, engine | Partially confirmed | `ListModeSource` passes contact fraction. Search shows event crosstalk capped at `min(0.95, 2*crosstalk*rng)` and `ApplyOpticalCrosstalk` at lines 216/234. Thus “uniform [0, 2*effective]” needs the cap qualification. The 40 µm attenuation rule, neighbour distribution, and event-sink position/energy after crosstalk still require body inspection; do not infer sink semantics from a flood result. |
| Optical crosstalk, Studio | Partially confirmed | No Studio contact-crosstalk setting was found by repository search; the engine config property exists. Check the complete settings and service bodies before closing this row. |
| SiPM pitch, Wpf only | Confirmed dependency boundary; body audit pending | Search found `DetSipmPitch` in Wpf XAML and Wpf code at lines 723 and 1030; block-average commentary at 670/774. No spatial SiPM pitch property or use was found in engine/Studio. PDE, excess-noise factor and DCR are different quantities. |
| Detector face, Wpf | Partial | Search confirms per-crystal-gain/gaps/SiPM-grid render text at 1033–1050. The asserted 12-subcell discretization, exact sensitivity use and readout fields were not independently inspected. |
| Depth 3D, Wpf | Unverified body | The reference plan's description is not independent evidence. `EstimateDepth_Click`, `LocalizeDepths` and truth-derived K need their full body read. |
| Rangefinder, Wpf | Unverified body | Exact slider range, increment, and `Refocus` behaviour still require XAML/code read. |
| Focus fusion, Wpf | Unverified body; proposed thresholds dropped | Do not accept 1.2/150/250 as valid evidence even if the call-site audit confirms them. No significance measurement was possible. |
| Studio focus | Confirmed with a qualification | `ImagingProjection.AtFocus` validates focus, clones the acquired config, changes assumed source-mask distance and updates both grid extent and step. `AcquisitionSession` publishes immutable flood/events and acquired optics/detector settings. `SceneConfigBuilder` explicitly uses non-cyclic decoding. The cap is a **validation/rejection policy**, not an automatic resizing cap. Full ImagingService/ViewModel channel flow audit remains open. |
| Depth evidence | Not independently re-verified | `DepthStudy` explicitly calls prominence a heuristic, not calibrated GLRT/SNR. Its joint 3D path uses a high-profile centroid and picks lateral coordinates from the nearest plane. The inherited +7/−18 mm and ~1% statements remain historical claims from the plan until Findings and reproduce code are inspected and run. |
| Crosstalk evidence | Not independently re-verified | The 9/13/20/69/45% sequence is inherited text, not new Studio-path evidence. No optimum established in this review. |
| Rig | Confirmed documentary statement | CLAUDE states GAGG, crystals matched 1:1 to the SiPM array, dead regions, four 14-bit ADC channels and Anger-type positioning; array-wide pile-up and its position effects are not modelled (TODO-19). No physical readout fidelity should be inferred from the current direct pixel events. |

## T-1 … T-7

| Proposal | Disposition | Proposed change and reason |
|---|---|---|
| T-1 gap input | Retain | Use µm in the editor, mm in config. Validate finite `0 <= gap < acquired/pending pitch`; editability/stale policy follows I-2. Snapshot the setting, render pending versus acquired values explicitly. Optics changes reducing pitch must revalidate gap. |
| T-2 contact crosstalk | Retain input; defer quantitative claims | Keep default zero. Validate finite domain at the service boundary as well as in the editor. Show contact and model-effective values separately. Confirm the capped event law and sink semantics first. No Studio optimum can be stated from this turn. |
| T-3 SiPM pitch | Support omission | Nothing found in Studio or engine depends on this spatial pitch. The Wpf block-average is a deliberately coarse readout surrogate; it should not be copied as general detector physics. A future parameter belongs to an explicit readout model with light sharing, four signals and flood classification. Author's decision remains theirs. |
| T-4 detector workspace | Correct | Show a gain map and geometric active regions, not an acquired light-sharing heatmap. Default map is a static seeded response pattern. Drop “measured comparison to fill factor” unless paired no-gap reference transport is actually run under specified conditions. Show acquired counts separately, without equating them to active area. |
| T-5 retained-data depth | Material revision; measurement gate open | Call it a depth/focus analysis, not a guaranteed 3D localization. Reuse the Studio projection geometry and selected measured channel flood, preserve plane distance units, avoid truth-derived K, and expose unresolved/boundary/multiple-mode outcomes. Show the focus curve and its defined width. Do not choose prominence versus calibrated refinement until measured at Studio geometry. |
| T-6 external range | Retain with UI separation | Keep “decoder focal plane” as a view control. Add a separate optional external surface range, with provenance and uncertainty if known, and an explicit “Use for focus” action. A range to the laser surface is not automatically range to a gamma source. Merely relabelling focus “rangefinder” would conflate a user-controlled assumed plane with an observation. |
| T-7 off-surface verdict | Drop verdict for now | Report the external range and experimental depth result without a significance label. Focus-curve width is not sampling noise or a standard error. No measured noise distribution exists from this turn, so no honest numeric significance can be supplied. Never auto-change focus. |

### Analytic values established from the inspected geometry (not MC results)

For Studio defaults, rank 13, cell 0.7 mm, detector 30×30 at 0.6 mm, D=80 mm:

- Projected cell size at detector-referenced plane z is `r = 0.7*z/80`.
- `ImagingProjection.AtFocus` uses half extent `0.95*13*r/2` and step `max(0.2,r/4)`.
- At z=110…3000 mm, `r/4 >= 0.240625 mm`, so the validation-side dimension is always `round(49.4)+1 = 50`, below 128. At z=300/500/1000/2000 mm, steps are 0.65625/1.09375/2.1875/4.375 mm. These are formula-derived, not decoded-grid measurements; verify the actual decoder allocation rule before closing the cap audit.
- The same formula bounds the side by about `3.8*rank+1` when r/4 dominates. The cap does not establish correctness of calling `LocalizeDepths`: that function's own grid construction was not read successfully.
- Default geometric active fraction is `((0.6−0.1)/0.6)^2 = 0.694444`. This is area fraction, not 662-keV window efficiency or a no-gap count ratio. Mask-shadow alignment, scatter, backing response, optical losses and energy windows can alter the latter.
- For pending face rendering, use actual gap rectangles/vector overlays or coverage-aware rasterization. If the inherited 12-subcell representation is confirmed, its 50 µm subcell pitch cannot faithfully draw a 20 µm gap as binary dead subcells. Drawing precision must not imply physics precision.

## Required measurement protocol once execution works

This is a proposed reproducible protocol, **not an executed command log**. Place all probe source, build output and CSV outside the repository, for example `$env:TEMP\gcam-todo11-review`. Prefer references to already-built engine/Studio assemblies; if rebuilding, isolate all intermediate/output directories outside the source tree. First read the complete service/config/scene-source APIs and record actual defaults, including the default activity and window N; they were not established in this turn.

Suggested execution form after the probe is written:

```powershell
dotnet run --project "$env:TEMP\gcam-todo11-review\Probe.csproj" -c Release -- gap
dotnet run --project "$env:TEMP\gcam-todo11-review\Probe.csproj" -c Release -- depth
```

### T-2: efficiency versus gap

Build the acquisition config through `SimulationService.BuildConfig`, clone and mutate gap/contact for the unavailable new setting, and transport with `ListModeSource`. Use the same `MeasurementStage` and `FrontEndModel` window construction as ImagingService. Do not substitute `SimulationRunner`'s mean flood or a bare true-deposit window for Studio's measured window. Record chain, gain sigma/seed, isotope lines, activity, source position, live time, window, crystal material, absorber/backing and transport seed.

Sweep gaps 0/20/40/100/200 µm at contact 0.4, plus contact-zero controls at every gap and a denser sweep if a maximum appears. Report both 4π weighted window efficiency (fixed-history estimate using the weight bound and accepted-window fraction) and live-time window cps; separately report conditional window fraction. Otherwise “efficiency” can mix an absolute efficiency with a fraction of accepted events. Compare equal histories for MC convergence and equal live time for practical performance. Several seeds and binomial/Poisson uncertainty are needed before locating an optimum. Confirm how neighbour deposits are reduced into the event sink: that determines whether the historical flood-window mechanism is reproduced at all.

### T-5: depth performance, multi-source association, cost

Use detector-referenced true z=300/500/1000/2000 mm, acquired Studio defaults, actual default activity, and proposed live times 1/10/60 s. State actual activity and actual All/window counts for every run. Use several independent transport seeds (at least five for initial review; many more for a tail/significance calibration). Avoid source truth in analysis; truth is only for evaluation.

Compare the exact legacy `LocalizeDepths` method, a Studio-geometry prominence sweep, and the actual theme-24 calibrated refinement after inspecting its implementation. Record which uses truth, a seed, a calibration template, or fixed lateral coordinates. `DepthStudy`'s known-position noise study is not a replacement for blind joint estimation. Its high-profile centroid uses a `min+0.7*(max−min)` threshold; that threshold is an algorithm choice, not an uncertainty calibration.

For each method/channel/time/depth report signed mean error, RMS error, median, failure/boundary rate, and lateral error; preserve all focus curves. Define width explicitly (raw half-maximum or half-prominence, interpolation and treatment of disjoint intervals). A curve still above threshold at a sweep edge has censored width; never report it as a resolved finite FWHM. Report seed spread independently of width. Use the same fixed scan bounds for comparisons, not truth-centred bounds. Test scan-bound sensitivity and interpolation/discretization by a finer control sweep.

For two sources, include separated lateral rays at z=500/1000 mm and a same-ray case, both same isotope and different isotope. Channel separation cannot solve same-isotope superposition by itself. Use one-to-one truth association only when evaluating; report missed, merged and spurious tracks. Greedy nearest-x/y linkage over depths is suspect because fixed source angle maps to different plane coordinates as focus changes; evaluate linking in angular coordinates and compare joint template fitting before selecting a method. K must come from user intent or a separately justified detection procedure, not the scene.

Log actual decoder dimensions at each plane, validation failures, cyclic setting, plane/step units and tested source positions. Time one cold and multiple warm sweeps separately from transport/template calibration; report planes, pixels, channels, CPU and cancellation latency. A new analysis worker must not hold up the 4 Hz acquisition publisher. No sweep cost was measured here.

### T-7: what a significance calculation would require

Under the null, simulate a source on the external surface across lateral position, count level, depth and relevant settings. Include the complete peak search, track selection and method bias. Fit or tabulate the estimator's conditional error distribution with independent validation seeds. Add the rangefinder's uncertainty and alignment/model uncertainty; do not assume those are zero. Multiple peaks/planes/windows introduce selection effects.

Only if normal residuals and stable bias are supported would a candidate statistic be `(estimated_z − external_z − measured_bias) / sqrt(depth_error_variance + range_error_variance)` for independent errors, with covariance included otherwise. Prefer a validated empirical interval when residuals are multimodal, censored or non-normal. A likelihood/profile interval needs the actual forward model and measured-energy response, plus coverage checks; prominence is not a likelihood. Width alone does not yield either statistic. No sigma value, p-value, false-alarm rate or millimetre threshold is justified by this review.

## New design and verification requirements

1. Keep detector run inputs in the shared acquisition panel, consistent with Migration/I-2. Detector workspace shows pending settings before acquisition and acquired settings after it; stale results must not silently render with a newly edited gain/gap pattern.
2. Put retained-data depth next to the selected imaging channel and focal plane; place external surface range there as a separate optional observation. Keep mask focusing/taper geometry with physical optics. Use “mm from detector” consistently; engine S is source-to-mask, while the UI z is detector-to-source.
3. Store an analysis identity containing acquisition, channel/window/strip state, optics and plane grid. Cancel/discard results when that identity changes. Re-windowing/re-focusing should not regenerate histories. Background/stripped floods need explicit estimator validity rules: clipped, fractional stripped values are not independent Poisson counts.
4. Audit existing ImagingProjection truth-derived `peakCount` separately from the new depth path. `Project` takes peakCount as an argument; do not automatically inherit scene-source counts for blind depth detection.
5. Add deterministic settings validation/snapshot/stale tests and face-coordinate tests; test cancellation, boundary/unresolved/multimode states and late-result rejection. Keep numerical evidence as explicit opt-in multi-seed studies. Derive any physics acceptance bound from this configuration's measured counts/distribution; no borrowed localization tolerance.
6. After implementation, render Detector before acquisition and after stale settings, plus Imaging with broad/censored/multiple focus curves, both themes and sizes. No desktop automation is authorised for this review.
7. Preserve raw evidence and exact probe source/commands in an external artifact location, then document supported conclusions in Findings/VV during the authorised implementation/documentation turn. Do not copy this review's analytic numbers into an MC evidence claim.

## Open items for the next turn

Complete the unread document/source bodies, establish default activity and window settings, run T-2/T-5/T-7 probes, inspect the theme-24 calibration implementation, verify decoder allocation and event-sink semantics, and report actual commands/counts/costs. This file preserves the current findings for discussion; it is not a completed numerical approval of the plan.
