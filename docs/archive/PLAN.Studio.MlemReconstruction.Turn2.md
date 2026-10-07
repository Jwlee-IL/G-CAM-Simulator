# PLAN.Studio.MlemReconstruction.Turn2 — implementer's turn-2 report: MLEM as a selectable reconstruction

Scope: TODO-36 turn 2 (implementation) of [PLAN.Studio.MlemReconstruction](PLAN.Studio.MlemReconstruction.md),
"Decisions after review (2026-10-07)" MD-1 … MD-9. Written by the substitute implementer, a Claude subagent (Codex was
unavailable). I had no material disagreement with any MD row.

- I did not edit any plan, review, AGENTS, VV, CLAUDE or README file, and changed no git state.
- Scratch code and outputs are under `%TEMP%\gcam-todo36\`.

Status: implemented and measured, 2026-10-07. Turn 3, the same day: the default became 400 iterations (MD-10),
confirmed on a third seed set, and the strip counts were labelled (MD-11). See "Turn 3" at the end; where it differs,
it supersedes the 360 figures above.

## At a glance

- **Engine (MD-1, MD-2):**
  - `DecoderConfig.Method` (`CrossCorrelation` default / `Mlem`, a string in JSON) and `MlemIterations` (default 120).
  - One shared construction, `MlemReconstruction`:
    - pixel-area model with 8 × 8 samples;
    - the transmission comes from one helper that the study now calls;
    - it uses the physical mosaic, inverted when `Mask.Invert` is set.
  - An opt-in sub-cell argument on `MlemDecoder`.
  - The factory MLEM equals the angres study's construction bit for bit (test).
  - Only the CLI single run honours `Method`. Study commands refuse such a scenario with a message, and
    `CorrelationSearch` throws a clear error.
- **MD-4, the DR-5 rule at Studio's default optics, selects 360 iterations.**
  - 360 is the fewest iterations that resolve 1.0 element (0.50°). 1.0 element is the smallest separation reached
    anywhere on a 120 … 8000-iteration grid.
  - Selection seeds F256[241:256]: pass 95.3 %, no false split at 2000 counts.
  - Check seeds F256[225:241]: pass 94.9 % [92.7, 96.8], 0 false splits. This is on the 95 % edge. By point estimate
    the check seeds reach 1.0 element at 400 iterations.
  - The grid is bracketed. At 0.75 element the pass stops rising at 0.859 (6000 and 8000 iterations). Beyond 2000 the
    margins at 1.0 and 1.25 elements shrink (0.957 / 0.976 at 8000) and false splits creep up (0.8 %).
  - Low-count side effect at 360 iterations: a 250-count single source shows a second peak at ≥ ¼ of the main peak's
    height in **49.7 % [46.4, 53.0]** of frames (check seeds), and at ≥ ½ height in 11 %.
  - The UI shows this as a caveat.
- **MD-6, Cs under Co: MLEM + background term is not worse than cross-correlation + strip.**
  - Localisation is better in all 6 conditions. RMS at 360 iterations against CC + strip:
    - 0.043° against 0.071° (1000 Cs counts, Co:Cs 1 : 1);
    - 0.120° against 0.132° (Co:Cs 4 : 1);
    - 0.025–0.030° against 0.055–0.064° (4000 Cs counts).
  - The count shown for the MLEM strip path is the unclipped Σ low − Σ b. It is unbiased (bias ≤ 3 counts) with an
    RMSE of 39–104 counts.
  - CC + strip's existing count is the clipped sum. It reads high by +45 to +249 counts, and its RMSE is 89–253 counts.
  - The UI path was built.
- **MD-5, display rate.** Measured end to end through the Services layer: a real `SimulationService` session, each
  snapshot awaited through `ImagingService.ProcessAsync` as `MainViewModel` does. Scene: Cs, Co, Ir-192 and Na-22 (All +
  4 channels), 60 s live at speed 10.
  - MLEM: 1.10–1.32 Hz display. The worker decode median is 600–740 ms per refresh, so about 120–150 ms per channel.
  - Cross-correlation: 3.4–3.6 Hz display, 80–90 ms decode.
  - Transport never stalled. The producer's median speed was 9.90–10.0× and the MC-limited flag never appeared.
  - The display lags by up to one refresh: the final snapshot was received at 7.1–7.5 s, against 6.1 s for
    cross-correlation.
- **Studio (MD-3, MD-7):**
  - Cross-correlation is the default.
  - The method lives in `ImagingSettings`. It re-decodes the retained floods per channel, with that channel's line
    transmission; All uses the highest primary line. Matrices are cached by geometry and line.
  - The readout unit is "(MLEM λ)".
  - Measurements and the focus sweep are kept on a method change. The sweep stays cross-correlation, and the UI note
    says why.
  - `AcquisitionSession` is unchanged (cross-correlation).
- **Tests:** Gcam.Tests 448 → 465, Studio.Tests 205 → 207, Studio.Services.Tests 92 → 95 passed (7 skipped, as
  before), UiTests 13 / 14 skipped, unchanged. The full suite is green. The opt-in render snapshot case passes.

## Files

| Group | File | Change |
|---|---|---|
| Config | `src/Gcam.Configuration/DecoderMethod.cs` (new) | enum with its own `JsonStringEnumConverter<DecoderMethod>`. Other enums keep their numeric form |
| Config | `src/Gcam.Configuration/SimulationConfig.cs` | `DecoderConfig.Method`, `DecoderConfig.MlemIterations` (120) |
| Engine | `src/Gcam.Decoding/MlemDecoder.cs` | 5-argument constructor with `SubCellMethod`. `None` is the old decoder bit for bit: the estimate is `(bx + 0.0)·step`, identical in IEEE arithmetic |
| Engine | `src/Gcam.Simulation/MlemReconstruction.cs` (new) | `PixelSubSamples = 8`, `ClosedCellTransmission(config, E)`, `ForwardPattern` (honours `Invert`), `Create` |
| Engine | `src/Gcam.Simulation/DefaultSimulationFactory.cs` | `CreateDecoder` switches on `Method`; `CreateMlemDecoder(config, E)`; `ReconstructionGeometry` is factored out (same arithmetic) |
| Engine | `src/Gcam.Simulation/AngularResolutionStudy.cs` | `ClosedCellTransmission` delegates to the shared helper (same expression) |
| Engine | `src/Gcam.Simulation/CorrelationSearch.cs` | refuses a non-CC config with a clear message |
| CLI | `src/Gcam.Cli/StudyDecoderGuard.cs` (new), `src/Gcam.Cli/Program.cs` | study dispatch refuses a scenario JSON whose `decoder.method` is not CrossCorrelation. The single run prints a decoder line, no "Ghost margin" for MLEM, and an MLEM reconstruction title |
| Studio.Core | `Services/ImagingSettings.cs` | `Method` (default CrossCorrelation) |
| Studio.Core | `Imaging/StudioMlem.cs` (new) | `Iterations = 360`, `LowCountSecondPeakShare = 0.50`, `IsMeasured(optics, focal)` |
| Studio.Core | `ViewModels/ReconstructionOption.cs` (new) | selector item |
| Studio.Core | `ViewModels/ImagingWorkspaceViewModel.cs` | `Reconstructions`, `Reconstruction`, `ReconstructionUnit`, `ReconstructionNote`. Method passed in the settings. `ChannelSummary` gains " · MLEM". The acquisition's CC image no longer stands in for All under MLEM |
| Studio.Core | `ViewModels/ImagingWorkspaceViewModel.Focus.cs` | `SweepNote`: "The sweep always cross-correlates, whatever the reconstruction selector shows." |
| Studio.Services | `MlemDecoderCache.cs` (new) | decoders keyed by (grid geometry, rank / mosaic / invert, transmission, iterations, sub-cell, pixels). Matrix built under a lock; capacity 12; `Builds` counter |
| Studio.Services | `ImagingProjection.cs` | `Project(…, MlemProjection?)` overload. The old signature calls it with null, which is the old code path |
| Studio.Services | `ImagingService.cs` | per-channel `MlemProjection` (line energy; All = highest primary line; source-free = the field's maximum energy). The strip path keeps the clipped display flood, and MLEM decodes the raw window flood with b = Σ R·high, effective count Σ low − Σ b. The CC strip arithmetic is unchanged |
| Studio view | `Views/ImagingOptionsPanel.xaml`, `Views/MainWindow.xaml` | Reconstruction ComboBox (`Imaging.Reconstruction`, tooltip / help text on the sweep), note `Imaging.ReconstructionNote`; reconstruction `ValueUnit` bound |
| Tests | `tests/Gcam.Tests/MlemReconstructionTests.cs` (new) | 17 cases (below) |
| Tests | `tests/Gcam.Studio.Tests/ImagingWorkspaceTests.cs` | +2 |
| Tests | `tests/Gcam.Studio.Services.Tests/ImagingServiceTests.cs` | +3 |
| Tests | `tests/Gcam.Studio.RenderTests/{MainWindowRenderTests,PlotViewRenderTests}.cs` | MLEM render case `imaging-mixed-cs-137-mlem-*` |

## Tests

Command: `DOTNET_CLI_UI_LANGUAGE=en dotnet test Gcam.sln -c Release`, after `dotnet build Gcam.sln -c Release`
(0 errors; the 2 warnings are the pre-existing xUnit analyser warnings in Studio.Services.Tests).

| Assembly | Before (passed / skipped / total) | After |
|---|---|---|
| Gcam.Tests | 448 / 0 / 448 | **465 / 0 / 465** |
| Gcam.Studio.Tests | 205 / 0 / 205 | **207 / 0 / 207** |
| Gcam.Studio.Services.Tests | 92 / 7 / 99 | **95 / 7 / 102** |
| Gcam.Studio.UiTests | 13 / 14 / 27 | 13 / 14 / 27 (desktop scenarios not run) |
| Gcam.Studio.RenderTests (opt-in, `GCAM_RENDER_SNAPSHOTS=1`, output redirected to `%TEMP%\gcam-todo36\render`) | 1 / 0 / 1 | 1 / 0 / 1, with 4 new MLEM PNGs |

Every existing test passes unchanged, including:
- the bit-identity tests of the default `MlemDecoder` (MlemOptionTests);
- the angres study tests;
- the Studio strip / localisation evidence tests.

New tests and their tolerances:

| Test | Assertion | Tolerance and why |
|---|---|---|
| `Method_LoadsFromAJsonString_AndSurvivesCloneAndSave` (×2: "Mlem", "mlem") | Load, Clone, Save → Load; saved as the string `"Method": "Mlem"` | exact |
| `Method_AbsentMeansCrossCorrelation_AndOtherEnumsStayNumeric` | default CC / 120; `SubCellInterpolation` still serialises as a number | exact |
| `DefaultMethod_IsTheUnchangedCrossCorrelationDecoder` (×5: lab, lab non-cyclic, hand-held, hand-held 1 m, Studio) | factory decoder against a verbatim copy of the pre-change `CreateDecoder` body: image, estimate, confidence, origin, step | exact: same arithmetic in the same order |
| `FactoryMlem_EqualsTheAngresStudysConstruction_BitForBit` | hand-held at 1 m: the system matrix and λ equal the study's `BuildMlem("area")`; the estimate equals tent sub-cell on that λ | exact (MD-1) |
| `PixelSubSamples_IsTheValuePinnedInTheAngresRequests` | every `area` variant and ladder in `samples/evidence/angres/*.json` uses 8 = `MlemReconstruction.PixelSubSamples` | exact. This is the "shared constant" link: the study reads the pinned requests, which cannot change without re-pinning |
| `ClosedCellTransmission_IsShared_AndFollowsTheLineEnergy` | study = helper; at 662.0 keV it is exp(−0.178 · 10) (table anchor μ_rel = 1); Co-60 > Cs-137; `CreateMlemDecoder(c, 1173.2)` matrix = a direct construction | exact |
| `Mlem_UsesTheInvertedMosaic_WhenTheMaskIsInverted` | matrix equals the inverted-mosaic model and differs from the plain one | exact |
| `FactoryMlem_NoiselessModelDataAtAGridNode_PeaksAtThatNode` (×2: hand-held 1 m at 120 iterations, Studio at 120) | data = A·e_k at a grid node with a unique column → argmax = k | exact index. MLEM's fixed point for consistent data reproduces them (the far-source check in MlemOptionTests) |
| `SubCellArgument_NoneIsTheOriginalDecoder_TentRefinesTheReturnedImage` | `None` = the 4-argument decoder (image, estimate); tent = `PeakInterpolation.Estimate` on the returned λ; λ unchanged | exact |
| `CorrelationSearch_RefusesAnMlemConfig` | `ArgumentException` naming cross-correlation | — |
| `SingleRun_DecodesWithMlem_WhenTheScenarioSelectsIt` | single run with `Method = Mlem` gives λ ≥ 0 everywhere; the CC run has negative values | sign, guaranteed by the multiplicative update from a positive start |
| `Reconstruction_IsAReprojectionSetting_KeepsMeasurementsAndSweep` (Studio.Tests) | CC default; MLEM gives one request whose settings equal the previous settings `with { Method = Mlem }`, the same id and the same snapshot, and no new Start; the reconstruction ROI and the sweep result are kept; unit, note and summary follow | exact |
| `ReconstructionNote_FlagsUnmeasuredOptics_AndTheAcquisitionImageIsNotShownAsMlem` (Studio.Tests) | "not measured" at focal 1500 or cell 1 mm, absent at the defaults; All has no CC fallback under MLEM | exact |
| `Mlem_ChannelsLocalizeAtTheirOwnSource_NonNegative` (Services) | off-axis Cs and Co, 600 s, strip off, 360 iterations: found peak within one grid diagonal (3.09 mm); λ ≥ 0 | the existing association bound for CC (step·√2, SR-IMG-04). Measured errors were 0.63 mm and 0.47 mm; precision itself is measured, not asserted |
| `Mlem_BuildsOneMatrixPerGeometryAndLine_AndReusesItAcrossRefreshes` (Services) | Cs + Co: 2 builds (Cs line; Co line, shared by All); a repeat and strip add 0; focal 1500 adds 2; CC adds 0 | exact counts |
| `CrossCorrelation_IsUnchangedByTheMethodField` (Services) | default settings and an explicit CrossCorrelation give identical channels (image, count, peaks) | exact |

## MD-4 — iteration count by the DR-5 rule

- **Method.**
  - Studio's own config: `SimulationService.BuildConfig` with the defaults. Rank 13, 0.7 mm cells, D 80 mm, 30 × 30
    pixels at 0.6 mm, focus 1000 mm, 50 × 50 grid with a 2.1875 mm step, non-cyclic.
  - Maps are transported (`GateResponse.Source`, 4 × 10⁶ photons, 595.5–727.9 keV, 7 % FWHM), drawn with exact
    Poisson counts.
  - Per seed, a sampling phase of ±0.5 element, and the axis alternates x / y, as in the angres family.
  - Pairs are 1 : 1, 1000 counts per source, Δ = 0.75 … 3 elements. Nulls are a single source at the pair centre,
    2000 counts (the rule) and 250 counts (the side effect).
  - Judged by `ResolvedPair.Test` / `Assigned` with v = 0.25.
  - "Resolved at Δ" means pass ≥ 95 % and false split ≤ 5 % at Δ and every larger Δ. The choice is the smallest
    resolved Δ, with ties going to fewer iterations.
  - The MLEM ran in a batched form of the pixel-area loop. It equals `MlemDecoder.Snapshots` bit for bit (0 mismatches
    of 22 500 values, checked with and without a background term).
- **Seeds:**
  - Selection: F256[241:256], 15 seeds × 50. Extension to 8000 iterations: the same seeds × 25.
  - Check: F256[225:241], 16 seeds × 50. These are the turn-1 review seeds; no evidence family uses either set.

**Selection seeds** (pass at Δ in elements; the worst assigned false split over Δ at 2000 counts; resolved Δ; second
peak ≥ ¼ height at 250 counts):

| Iterations | 0.75 | 1.0 | 1.25 | 1.5 | 2.0 | 3.0 | Worst false split (2000) | Resolved | 2nd peak ≥ ¼ at 250 |
|---|---|---|---|---|---|---|---|---|---|
| 120 | 0.004 | 0.383 | 0.989 | 1.000 | 1.000 | 1.000 | 0 | 1.25 | 0.233 |
| 240 | 0.124 | 0.865 | 1.000 | 1.000 | 1.000 | 1.000 | 0 | 1.25 | 0.413 |
| 320 | 0.261 | 0.933 | 1.000 | 1.000 | 1.000 | 1.000 | 0 | 1.25 | 0.447 |
| **360** | 0.329 | **0.953** | 1.000 | 1.000 | 1.000 | 1.000 | **0** | **1.0** | **0.463** |
| 400 | 0.385 | 0.961 | 1.000 | 1.000 | 0.999 | 1.000 | 0 | 1.0 | 0.475 |
| 480 | 0.485 | 0.969 | 1.000 | 1.000 | 0.997 | 1.000 | 0 | 1.0 | 0.488 |
| 640 | 0.613 | 0.972 | 0.999 | 0.999 | 0.997 | 1.000 | 0 | 1.0 | 0.523 |
| 960 | 0.720 | 0.975 | 0.999 | 0.997 | 0.997 | 1.000 | 0 | 1.0 | 0.560 |
| 2000 | 0.821 | 0.971 | 0.999 | 0.996 | 0.995 | 0.997 | 0.3 % | 1.0 | 0.575 |
| 3000 (× 25) | 0.848 | 0.963 | 0.989 | 0.995 | 0.995 | 1.000 | 0.5 % | 1.0 | 0.592 |
| 4000 (× 25) | 0.851 | 0.957 | 0.984 | 0.995 | 0.989 | 0.997 | 0.5 % | 1.0 | 0.595 |
| 6000 (× 25) | 0.859 | 0.957 | 0.976 | 0.995 | 0.989 | 0.997 | 0.5 % | 1.0 | 0.587 |
| 8000 (× 25) | 0.859 | 0.957 | 0.976 | 0.995 | 0.992 | 0.997 | 0.8 % | 1.0 | 0.592 |
| CC | 0.000 | 0.000 | 0.145 | 0.692 | 0.939 | 0.935 | 0.3 % | not reached | 1.000 |

The intermediate counts 160, 440, 560, 800, 1280 and 1600 are in `%TEMP%\gcam-todo36\agg-select.txt`. They are
consistent: 1.0 element from 360 on.

- **Bracket:**
  - No count on the grid resolves 0.75 element. Its pass stops rising at 0.859 (6000 = 8000), well short of 95 %.
  - The smallest resolved separation is therefore 1.0 element, reached first at 360 iterations.
  - Above about 2000 iterations the margins degrade: 1.0 element falls from 0.975 to 0.957, 1.25 from 0.999 to 0.976,
    and the false split rises from 0 to 0.8 %.
- **Check seeds at 360:**
  - 1.0 element: 0.949 [0.927, 0.968] (bootstrap over seeds), 0 false splits. Larger Δ: ≥ 0.998.
  - By point estimate the check seeds resolve 1.25 elements at 360 and 1.0 element from 400 iterations (0.961). The
    selected count therefore sits on the rule's 95 % edge; 0.95 lies inside the check interval.
  - The selection used the selection seeds only, as MD-4 requires.
- **Low-count side effect at 360:**
  - A 250-count single source shows a second peak at ≥ ¼ of the main peak's height in 49.7 % [46.4, 53.0] of frames
    (check seeds; selection 46.3 %), and at ≥ ½ height in 11.0 %.
  - The assigned false split at 250 counts is ≤ 2.6 %.
  - For comparison, cross-correlation: 100 % (¼ height) and 78 % (½ height). 120 iterations: 25 % and 5 %.
  - `StudioMlem.LowCountSecondPeakShare = 0.50` is shown in the caveat as "≈50 %".

## MD-6 — MLEM + strip on Cs under Co

- **Method:**
  - Studio's optics and its actual windows from `SpectrumService.BuildBands` with N = 1.5: Cs 616.3–707.1 keV,
    Co 1100.4–1246.0 keV, chain FWHM 4.57 % at 662 keV.
  - Transported per-line maps: Cs 661.7 keV; Co 1173.2 and 1332.5 keV, summed.
  - Cs at the seed's sampling point; Co 3 elements away along the seed's axis.
  - Cs window counts 1000 and 4000, Co:Cs activity 1, 2 and 4.
  - R = the Co counts in the Cs window divided by the Co counts in the Co window, from the expected maps: the ideal
    H-only calibration. Median 0.450; Studio's calibration uses 100 000 MC events.
  - Exact Poisson draws of both windows.
  - Peak finding exactly as Studio: `MixedFieldStudy.TopPeaks`, K = 1, one-element blanking, tent sub-cell.
  - 16 seeds (F256[225:241]) × 50.
- **The paths compared:**
  - CC + strip: Studio's path, decoding max(0, low − R·high).
  - MLEM + b: decodes the raw low flood with b = R·high.
  - For reference, MLEM on the clipped flood, and both decoders unstripped.
- **"Count"** is the channel's effective count:
  - CC + strip: the clipped sum (Studio's existing number);
  - MLEM + b: Σ low − Σ b, as implemented.
  - MLEM's own Σⱼ sⱼλⱼ is also reported, as "Σ s·λ".

| Cs counts, Co:Cs | Co counts in the Cs window | CC + strip RMS / within 1 el | MLEM + b @ 360 RMS / within 1 el | CC + strip count, bias (RMSE) | MLEM + b count Σ low − Σ b, bias (RMSE) | MLEM + b Σ s·λ, bias |
|---|---|---|---|---|---|---|
| 1000, 1 | 319 | 0.071° / 1.000 | **0.043°** / 1.000 | +88 (95) | **+1 (39)** | +83 |
| 1000, 2 | 637 | 0.076° / 1.000 | **0.048°** / 1.000 | +148 (153) | **−1 (44)** | +138 |
| 1000, 4 | 1274 | 0.132° / 0.998 | **0.120°** / 0.999 | +249 (253) | **0 (54)** | +218 |
| 4000, 1 | 1274 | 0.055° / 1.000 | **0.025°** / 1.000 | +45 (89) | **−3 (80)** | +103 |
| 4000, 2 | 2548 | 0.057° / 1.000 | **0.026°** / 1.000 | +101 (132) | **−2 (90)** | +174 |
| 4000, 4 | 5097 | 0.064° / 1.000 | **0.030°** / 1.000 | +222 (241) | **−1 (104)** | +271 |

- **Without stripping both decoders fail at 4 : 1.** CC finds the Co position in 25–30 % of acquisitions and MLEM in
  36–42 %. At 2 : 1, 360 iterations, MLEM lands nearer Co in 0.3–1.5 %. The background term removes this: ≤ 0.3 % at
  every ratio.
- **Iteration count:**
  - MLEM + b at 120 iterations is a little better again (0.037–0.053° at 1000 counts).
  - On the clipped flood, MLEM degrades with iterations at 4 : 1: 0.153° at 360 against 0.120° with b.
- **Verdict:** MLEM + b is not worse than CC + strip in location or in count, in every condition, so the UI path was
  built.
- **By-product (no code change):** the clipped strip count that CC uses today reads 4.5–25 % high, because clipping
  turns negative pixel differences into zeros. The unclipped Σ low − R Σ high is unbiased for either decoder. Proposed
  to the planner below; I did not apply it.

## MD-5 — display rate at 360 iterations

- **Method:** scratch probe `%TEMP%\gcam-todo36\probe3`.
  - Real `SimulationService.Start` session (Cs-137, Co-60 at 1000 µCi, Ir-192, Na-22; default optics), 60 s live at
    speed 10, seed 4242.
  - Each snapshot awaited through `ImagingService.ProcessAsync`, exactly as `MainViewModel.ReadSegmentAsync` awaits
    `Imaging.WhenUpdated`.
  - 24 logical cores, otherwise idle.

| Method | Strip | Display rate | Worker decode, median (max) | Producer speed, median | Completed snapshot received | MLEM matrices built |
|---|---|---|---|---|---|---|
| CC | no | 3.38 Hz | 87 ms (109) | 10.00× | 6.10 s | 0 |
| CC | yes | 3.55 Hz | 83 ms (108) | 10.02× | 6.11 s | 0 |
| MLEM 360 | no | 1.10 Hz | 741 ms (1312) | 9.90× | 7.45 s | 4 |
| MLEM 360 | yes | 1.14 Hz | 671 ms (1477) | 9.95× | 7.08 s | 4 |

- **Transport:** the MC-limited flag was never set, and the producer kept the requested speed: 60 s live in about
  6 s of acquisition.
- **Display:** the slower MLEM display shows the last frames up to one refresh late. The maximum includes the first
  refresh's four matrix builds (about 110 ms each).
- **Per channel:** about 120–150 ms at 360 iterations (0.33 ms per iteration, review MR-4).
- **Known limit (MD-9):** the 128 × 128 grid allowed by SR-OPT-03 has 6.55× the grid points. At 360 iterations that is
  about 0.8 s per channel and about 4 s per refresh with 5 channels. Not measured.

## MD-8 — MLEM single-source precision at Studio's optics

Reported, not claimed. These are the turn-1 review's measurements (the same optics; 16 seeds × 50; tent sub-cell on λ,
which is now the factory and Studio estimate). RMS at 1000 / 4000 / 16 000 counts:
- 240 iterations: 0.033° / 0.021° / 0.016°;
- 120 iterations: 0.030° / 0.019° / 0.015°;
- cross-correlation with tent: 0.062° / 0.052° / 0.048°.

At 250 counts MLEM is worse than CC: 0.24–0.33° against 0.16°, from the false second peaks above. **360 iterations was
not measured for single-source precision.** It lies between the 240 and 480 columns. The noiseless tent error at 480
is a median of 0.014°. PR-IMG-02 / D-49 stay cross-correlation figures.

## Deviations

1. **The check seeds put 360 on the rule's edge** (0.949 at 1.0 element). I kept the rule's answer from the selection
   seeds as MD-4 says, and report it. If the planner wants a margin, 400 iterations passes both seed sets
   (0.961 / 0.961). Its side effect is 47.5–50.2 % at 250 counts.
2. **The MLEM strip path shows the unclipped Σ low − Σ b as its effective count**, while CC + strip keeps its clipped
   sum. The CC number is unchanged, so no existing test or number moves. The two counts therefore differ for the same
   flood. The proposal is below.
3. **The DR-5 runs used the batched MLEM loop**, proven bit-equal to `Snapshots`, and the turn-1 window (595.5–727.9
   keV, 7 % FWHM, the angres window). MD-6 used Studio's window (N = 1.5, 4.57 % FWHM).
4. **MD-6's R is the ideal expected-map ratio,** not Studio's 100 000-event calibration. The calibration noise
   (relative SE about 0.4 % at 40 000 high counts) is equal for both paths.
5. **"All" uses the highest primary line** (`Isotopes.Lines[0]`). For Na-22 that is 511 keV, not 1274.5 keV, so Na-22
   alone takes a smaller transmission than its highest line would give. With Cs + Co, All uses Co's 1173.2 keV.
6. **`StudioMlem.IsMeasured` compares the five physical optics fields and the focal plane exactly against the
   defaults.** Any preset other than Sharp, or any other focus, shows "Iteration count not measured for these optics".
7. **Measurement wall time was about 1 h 50 min:** selection 33 min, MD-6 5 min, extension 52 min, display probe
   1 min. The extension ran at N = 25 per cell.

## Proposed doc edits (not applied)

- **VV.Studio.SRS** — new SR-IMG-07 after SR-IMG-06:
  > "A Reconstruction selector (Cross-correlation default, MLEM pixel-area) re-decodes the retained All and isotope
  > floods without transport, measurement, random draws or calibration; latest revision wins. MLEM uses each channel's
  > primary-line closed-cell transmission (All: the highest primary line; source-free: the field's maximum energy) and
  > the fixed iteration count selected by the DR-5 rule at the default optics (360). With Compton strip it decodes the
  > raw window flood with the higher lines' downscatter as a known background, shows the clipped strip flood, and
  > reports Σ low − Σ R·high as the effective count. The reconstruction readout unit is '(MLEM λ)'. A caveat states the
  > low-count false second peak (≈ 50 % of 250-count frames at quarter height), whether the count was measured for the
  > current optics and focus, and the missing background term. Measurements and the focus sweep are kept; the sweep
  > always cross-correlates."

  Also:
  - SR-IMG-04: "pipeline-default sub-cell interpolation" now also applies to MLEM's λ.
  - SR-OPT-04: add the method to the re-projection settings.
  - §4 performance: "with MLEM selected the display refresh drops to about 1 Hz (default optics, 5 channels);
    transport is unaffected".
- **VV.Studio.SDS:**
  - SU-23: `ImagingSettings.Method`, `MlemProjection`, b = Σ R·high, the effective count.
  - SU-25: `Project` overload; `MlemDecoderCache`, which could become a new unit SU-xx under SI-3 (key, capacity 12,
    matrix built under a lock, concurrent decodes read-only).
  - SU-16: `Reconstruction`, `ReconstructionUnit`, `ReconstructionNote`, and the CC fallback rule.
  - Allocation table: SR-IMG-07 → SU-16, SU-23, SU-25, `MlemDecoderCache`, `StudioMlem`.
- **VV.Studio.Imaging:** add MD-4's table (as conditional evidence at the default optics), MD-6's table and MD-5's
  display rates.
- **VV.Gcam.Evidence EV-11 "Reproduce":** add the single-run path:
  `montecarlo <scenario with "decoder": { "method": "Mlem", "cyclic": false }>`. It uses the claimed decoder; with
  `mlemIterations` 120 it is bit-identical to the study's construction. No evidence number changes.
- **AGENTS.md:** Notes & gotchas — "`Decoder.Method` = Mlem is honoured only by the single run and Studio; study
  commands refuse it". The `src/…Configuration` row gains `DecoderMethod`.
- **AGENTS.Studio.md:** one line under "Detector and retained-flood focus": the MLEM selector is a re-projection
  setting, and the sweep stays CC.
- **For the planner, a separate decision:** CC + strip's effective count could use the unclipped Σ low − R Σ high, the
  same unbiased number as the MLEM path (MD-6: the clipped count reads +4.5 to +25 % high). This would change
  `Strip_CoLocatedCsAndDoubleActivityCo_RecoversSameLiveTimeCsCount`'s observed value; its derived tolerance would
  still hold, and probably more tightly.

## Desktop checks for the planner (author's go)

1. Imaging → Reconstruction → MLEM on a running default acquisition:
   - the reconstruction pane changes to a non-negative image;
   - the readout ends in "(MLEM λ)";
   - the caveat note appears;
   - the status line keeps advancing at the requested speed;
   - the image refreshes at about 1 Hz.
2. Switch back to Cross-correlation: the image returns, "(decoded)" returns, and the note disappears.
3. With MLEM selected:
   - Cs + Co with Compton strip on and off: Cs found peak, count and the strip note;
   - focal plane 1500 mm: "Iteration count not measured for these optics";
   - a Wide FOV preset: the same note.
4. Measurements across a method switch: an ROI on the reconstruction keeps its shape and updates its value;
   distance / angle stay.
5. Focus sweep with MLEM selected: it runs and shows the same tracks as under cross-correlation (it cross-correlates).
6. Stop → Continue and Reset with MLEM selected: no stale image, no error. A Reset keeps the MLEM selection.
7. Keyboard only and both themes: the selector is reachable and named "Reconstruction". Check the note's wrapping at
   1280 × 800 (the render shows it fits).
8. UI automation: the AutomationIds `Imaging.Reconstruction` and `Imaging.ReconstructionNote` are new. The readout
   oracle (`FloodOracle`) parses "(decoded)" and would need the MLEM unit if a scenario selects MLEM.

## What could not be run

- No desktop or UI scenarios and no app launch (hard limit).
- Single-source precision at exactly 360 iterations (deviation 7 and MD-8).
- The 128 × 128 grid display rate (MD-9 estimate only).
- MD-6 with Studio's own list-mode flood and 100 000-event calibration (I used expected maps and the ideal R).
- Other presets.

## Commands that wrote anything

- Repository source and test files listed under Files. This report, `docs/PLAN.Studio.MlemReconstruction.Turn2.md`.
- `DOTNET_CLI_UI_LANGUAGE=en dotnet build Gcam.sln -c Release` (before and after), plus project builds of
  `src/Gcam.Cli`, `src/Gcam.Studio.Core`, `src/Gcam.Studio.Services`, `src/Gcam.Studio` and
  `tests/Gcam.Studio.RenderTests`. These wrote the repository's `bin` / `obj` outputs.
- `DOTNET_CLI_UI_LANGUAGE=en dotnet test Gcam.sln -c Release --no-build` (before and after; logs
  `%TEMP%\gcam-todo36\test-{before,after}.txt`), plus filtered `dotnet test` runs of Gcam.Tests, Gcam.Studio.Tests and
  Gcam.Studio.Services.Tests.
- `GCAM_RENDER_SNAPSHOTS=1 GCAM_RENDER_OUTPUT=%TEMP%\gcam-todo36\render dotnet test tests/Gcam.Studio.RenderTests -c
  Release --no-build`. Its PNGs went to scratch, and `docs/assets` was not touched.
- A CLI smoke run of the single run and `noise` on `%TEMP%\gcam-todo36\handheld-mlem.json`. The `noise` run was
  refused and wrote nothing.
- Scratch probes `%TEMP%\gcam-todo36\probe2` (`check`, `select`, `strip`) and `probe3` (display rate), with their
  builds and outputs under `%TEMP%\gcam-todo36\{select,select-ext,strip}`, the `agg_select.py` / `agg_strip.py` scripts
  and their `agg-*.txt` outputs, and `display-rate.txt`.

## Turn 3 — 400 iterations on fresh seeds (MD-10), strip count labels (MD-11)

### Third seed set

The F256 rule continued: F[256:272] = `1 + int.from_bytes(sha256('gcam-todo27-20261002-' + str(i)).digest()[:4],
'little') % 1900000000` for i = 255 … 270. The 16 seeds are:

1062998586 699139738 1813491055 1560747299 188657401 884887855 1052686330 1361591362 1252749560 468276321 908941673
237436938 392636543 345422420 335980416 261925042

The set is disjoint from everything used before:
- The rule reproduces `seeds.json`'s F256 exactly (asserted before generating). The new seeds are distinct, and
  disjoint from every list in `seeds.json` (O128, F128, F256, RTL_PIXEL32, RTL_MULTI32) and from the pilot seed 777.
- Every evidence manifest draws its seeds by list name and offset from those lists. No seed of the set appears in any
  `.json`, `.py`, `.cs` or `.md` file of the repository.
- It is therefore disjoint from the selection seeds (F256[241:256]), the check seeds (F256[225:241]) and every
  evidence family.

### Result

- **Method:** the same conditions and code as the MD-4 table:
  - Studio default optics; 1 : 1 pairs at 1000 counts per source, Δ = 0.75 … 3 elements;
  - single-source nulls at 2000 and 250 counts;
  - 16 seeds × 50;
  - the batched pixel-area MLEM (bit-equal to `Snapshots`).
- **Intervals:** 95 % percentile bootstrap over seeds (4000 resamples).

| Decoder | Pass at 1.0 el | Pass at 1.25 / 1.5 / 2.0 / 3.0 el | Worst false split at 2000 counts | Resolved | 2nd peak ≥ ¼ height at 250 counts | ≥ ½ height | Worst assigned false split at 250 |
|---|---|---|---|---|---|---|---|
| **MLEM 400** | **0.960 [0.940, 0.979]** | 1.000 / 0.999 / 0.999 / 0.998 | **0.13 % [0, 0.37]** (at 1.25 el) | **1.0 el** | **50.4 % [46.3, 54.2]** | 11.6 % [8.6, 14.6] | 2.0 % |
| MLEM 360 (reference) | 0.959 [0.939, 0.978] | 1.000 / 0.999 / 0.999 / 0.998 | 0.13 % | 1.0 el | 49.4 % [45.3, 53.5] | 11.1 % | 1.9 % |
| CC | 0.000 | 0.141 / 0.726 / 0.959 / 0.980 | 0.50 % | 2.0 el | 100 % | 77.5 % | 1.1 % |

- **400 passes.**
  - At 1.0 element the point estimate is 96.0 % (≥ 95 %, the DR-5 / D-48 point rule). Every larger separation is
    ≥ 99.8 %.
  - The worst false split is 0.13 % (≤ 5 %; upper limit 0.37 %).
  - The lower end of the 1.0-element interval (94.0 %) lies below 95 %; the requirement is judged on the point
    estimate, as in DR-5.
- **The default is set:** `StudioMlem.Iterations = 400`.
- **Low-count caveat:** `LowCountSecondPeakShare = 0.50` stays, measured at 50.4 %. The UI caveat now reads "400
  iterations, chosen for pair resolution at the default optics. Below ~1000 counts a single source often shows a false
  second peak (≈50 % of 250-count frames at quarter height). …".
- **Display rate at 400:** not re-measured. It scales with the iteration count from MD-5's 360 figures: about
  1.0–1.2 Hz with 5 channels, and transport is unaffected (MD-5 showed the producer is independent of the decode).

### MD-11 — strip counts labelled

The cross-correlation strip count stays the clipped sum (TODO-39). The Imaging channel summary now names the count
whenever Strip is on and the selected channel has a contaminant (a strip ratio with it as the low isotope):
- cross-correlation: "… · N counts (clipped strip sum) · …";
- MLEM: "… · N net counts (Σ low − R·Σ high) · …";
- otherwise plain "counts" (All, unstripped, or no contaminant).

Change in `ImagingWorkspaceViewModel.Summary` / `CountLabel`. Test:
`StripCount_IsLabelledClippedForCrossCorrelation_AndNetForMlem` (Studio.Tests).

### Turn-3 files

- `src/Gcam.Studio.Core/Imaging/StudioMlem.cs`: 400, with the three seed sets' figures in the doc comment.
- `src/Gcam.Studio.Core/ViewModels/ImagingWorkspaceViewModel.cs`: caveat wording; `CountLabel`.
- `tests/Gcam.Studio.Tests/ImagingWorkspaceTests.cs`: +1 test.
- This section.

### Turn-3 verification

- `DOTNET_CLI_UI_LANGUAGE=en dotnet build Gcam.sln -c Release`: 0 errors, the same 2 pre-existing analyser warnings.
- `DOTNET_CLI_UI_LANGUAGE=en dotnet test Gcam.sln -c Release`, passed / skipped / total:

  | Assembly | Turn 2 | Turn 3 |
  |---|---|---|
  | Gcam.Tests | 465 / 0 / 465 | 465 / 0 / 465 |
  | Gcam.Studio.Tests | 207 / 0 / 207 | 208 / 0 / 208 (+1, MD-11) |
  | Gcam.Studio.Services.Tests | 95 / 7 / 102 | 95 / 7 / 102 (the MLEM tests now run at 400 iterations) |
  | Gcam.Studio.UiTests | 13 / 14 / 27 | 13 / 14 / 27 |
- Render snapshots (`GCAM_RENDER_SNAPSHOTS=1`, output in `%TEMP%\gcam-todo36ender3`): 1 / 0 / 1 passed. The MLEM PNG
  shows "MLEM (pixel-area, 400 iterations)", the 400 caveat and "net counts (Σ low − R·Σ high)".

### Turn-3 commands that wrote anything

- The scratch file `%TEMP%\gcam-todo36\seeds-third.txt` (seed generator; read-only grep over the repository).
- One background run: the probe `select` with `PROBE_ITS=360,400` on the third set, output in
  `%TEMP%\gcam-todo36	hird`, aggregated by `agg_third.py` into `agg-third.txt`.
- `dotnet test tests/Gcam.Studio.Tests -c Release --filter ImagingWorkspaceTests`.
- One background run: `dotnet build Gcam.sln -c Release`, `dotnet test Gcam.sln -c Release --no-build`, then the
  render snapshots with `GCAM_RENDER_SNAPSHOTS=1 GCAM_RENDER_OUTPUT=%TEMP%\gcam-todo36
ender3` (PNGs to scratch,
  `docs/assets` untouched).
- The repository edits listed above.
