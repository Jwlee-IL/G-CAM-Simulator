# PLAN.Studio.MlemReconstruction.Review — implementer's turn-1 review: MLEM as a selectable reconstruction

Scope: TODO-36 turn 1 (review only) of [PLAN.Studio.MlemReconstruction](PLAN.Studio.MlemReconstruction.md), by the
substitute implementer (a Claude subagent; Codex unavailable). I checked every "What exists" claim and every MR row in
the code, and measured the quantitative rows with headless probes. I changed no production code, plan, Todo or VV
document. Scratch code and outputs are under `%TEMP%\gcam-todo36\`. They are machine-local and not evidence.

Status: review, 2026-10-07. The numbers come from 16 outer seeds, F256[225:241], which no evidence family uses. They
are scoping numbers for a decision, not evidence-grade: no manifest, and no disjoint selection and validation seeds.

## At a glance

- **The plan's direction holds.** MLEM can be selected in the config, the factory and Studio without moving any
  existing number. Seven premises need correcting first:
  1. **Studio's displayed reconstruction does not come from `AcquisitionSession`.** The Imaging workspace shows
     `ImagingService` channels. These are built by `ImagingProjection.Project`, which creates **a new decoder for every
     channel on every refresh**, at the workspace's own focal plane. For cross-correlation (CC) a new decoder is free,
     because CC has no matrix. For MLEM it means rebuilding the 2500 × 900 pixel-area matrix every time: 111 ms per
     build, and 132 ms against 39 ms per decode. The method therefore belongs in `ImagingSettings`, a re-projection
     setting like the focal plane, with a decoder cache. `AcquisitionSession` keeps CC.
  2. **`"Method": "Mlem"` would not load.** `ConfigLoader` has no string-enum converter: today's enum
     (`SubCellInterpolation`) is read and written as a number. The new enum needs its own converter attribute.
  3. **About 30 study call sites take their decoder from `factory.CreateDecoder(config)`.** A scenario with
     `Method: Mlem` would silently switch them all to MLEM:
     - antimask subtraction would feed MLEM negative counts;
     - `CorrelationSearch` would throw;
     - per-configuration loops would rebuild a matrix for every configuration.

     The study commands need a guard.
  4. **The closed-cell transmission depends on the line energy:** about 0.17 at 662 keV, 0.34–0.38 for Co-60, 0.005
     for Ir-192's main line, about 0 at 122 keV. In Studio, `config.Source` is only the first scene source, and the
     "All" channel mixes energies. The transmission has to be chosen per channel.
  5. **The MLEM forward model must use the physical pattern, including `Mask.Invert`.** The CC decoder ignores
     `Invert` because it decodes with the rank-p array. An MLEM that did the same would invert the wrong mask on
     antimask frames.
  6. **`MlemDecoder.Decode` reports the integer argmax only; it applies no sub-cell refinement.** At Studio's optics
     the pipeline's tent on λ is 2–4× more precise than the argmax (MR-6).
  7. **The study does not hard-code 120 iterations or 8 sub-samples.** Both come from the evidence request JSON
     (`Iterations: [120]`, `PixelSubSamples: 8`). In code, `BuildMlem` takes `v.Iterations.Max()` and
     `v.PixelSubSamples`. The factory must state these as named constants with their source.
- **MR-3, measured at Studio's default optics.** These are rank 13, 0.7 mm cells, D = 80 mm, 30 × 30 pixels at
  0.6 mm, focus 1000 mm, which is 1.27 detector pixels per projected cell (the hand-held head has 1.06). One element is
  0.501°. The test is DR-5's: blind, 1 : 1, 1000 counts per source, 16 seeds × 50 + 50 per cell. Results for the
  pixel-area MLEM:
  - it resolves **1.25 elements at 120 iterations** (99.0 % [98.4, 99.5] pass) with **no false split at 2000 counts**
    (0 of 800 at every separation of the grid);
  - it resolves 1.5 elements at 40–80 iterations and 1.0 element at 480;
  - CC resolves 2.0 elements.
  The head's over-iteration failure is much weaker here. The worst assigned false split is ≤ 1.4 % up to
  240 iterations at every count level (250–64 000), and 2.5 % at 480 / 250 counts. On the head it was 9.5 % at 480.
- **The DR-5 rule would choose ≥ 480 iterations here, and the grid does not bracket that choice.** The cost is low
  counts. With a single source of 250 counts, a second peak at least a quarter of the main peak's height appears in
  25 % of acquisitions at 120 iterations and 51 % at 480 (5 % at 40). Studio's first ticks sit at about 100–250
  counts: the default scene gives 34 cps in the 662 keV window. CC at 250 counts shows such a peak in 100 %. Above
  1000 counts every MLEM up to 320 iterations is at about 0 %. **Recommendation: keep 120, as a choice measured at
  Studio's optics, not borrowed.** It is also the iteration count of the claimed decoder. Alternatives are under Open
  questions.
- **MR-4 cost on this machine** (24 logical cores; Studio's 30 × 30 image and 50 × 50 grid; medians):

  | Decode | Time |
  |---|---|
  | CC | 18.9 ms |
  | Pixel-area MLEM, 120 iterations, matrix cached | 39 ms |
  | Pixel-area MLEM, matrix rebuilt every call (today's `Project` pattern) | 132 ms |
  | Engine's default pixel-centre MLEM (scalar double loop) | 875 ms |

  With a cache, All + 3 isotope channels cost about 160 ms per refresh, inside the 250 ms tick. **Warm-starting is
  not equivalent to restarting:** 2500 unknowns against 900 pixels have no unique ML solution, so the start point and
  the iteration count are the regulariser. Measured at Δ = 1.25:
  - 20 ticks × 6 iterations, warm, pass 96.0 % against 98.8 % for a cold 120 on the same final counts;
  - the warm and cold images differ by 27 % of the peak.

  Recommendation: decode cold on every refresh, with the matrix cached.
- **MR-6:**
  - With the pipeline's tent sub-cell on λ, MLEM's single-source RMS at 1 m is 0.030° at 1000 counts and 0.015° at
    16 000. CC with tent gives 0.062° and 0.048°.
  - The MLEM argmax alone is stuck at its grid floor, 0.053° (step 0.125°).
  - The median bias over source positions is 0.011° (MLEM + tent) against 0.035° (CC).
  - At 250 counts MLEM is worse: 0.24° at 120 iterations against CC's 0.16°. Its outliers are spurious second peaks
    that win.
  - D-49's precision stays a CC figure, as the plan says.

## How the numbers were obtained

- **Configuration:** Studio's own: `SimulationService.BuildConfig` with the default `OpticsSettings` and
  `DetectorSettings` (GAGG, 10 mm crystal, tungsten 10 mm at μ = 0.178 /mm, 2 × 2 mosaic). It goes through
  `ImagingProjection.AtFocus`, so the grid is exactly Studio's: 50 × 50, step 2.1875 mm, half-width 54.03 mm, non-cyclic,
  tent sub-cell.
- **Maps:** expected per-pixel maps from `GateResponse.Source`: transported through the slab and the crystal, 4 × 10⁶
  photons each (about 3.9 % per-pixel MC noise), in a 595.5–727.9 keV window with 7 % FWHM. This is the same path and
  window as EV-11's angres family. Acquisitions are exact Poisson draws (`AmbientEvidence.Draw`) from those maps.
- **Sources and tests:**
  - Per seed, one sampling phase (±0.5 element in x and y; the axis is x for even seeds, y for odd), drawn exactly
    as in `AngularResolutionStudy`.
  - Pairs: 1 : 1, 1000 counts each, Δ ∈ {0.75 … 3} elements.
  - Nulls: one source at the pair centre, at 250, 1000, 2000, 4000, 16 000 and 64 000 total counts.
  - The test is `ResolvedPair.Test` / `Assigned` (v = 0.25, assignment radius min(Δ/2, 1 element)), with the median
    baseline for CC and 0 for MLEM.
  - Each null image is drawn once per count level and judged against every Δ hypothesis. The study draws fresh nulls
    per Δ; the rates are equivalent, only correlated across Δ.
  - "Resolved" follows DR-5: pass ≥ 95 % and null ≤ 5 % at Δ and at every larger Δ.
  - Brackets are 95 % seed-bootstrap intervals.
- **Viewer metric (new):** the share of single-source acquisitions whose highest other peak (relative prominence
  ≥ 0.25, the D-48 shape rule) stands at least 0.25 (or 0.5) of the main peak's height above the baseline. This is
  what a Studio user sees as a second source. The plain "any second peak with relative prominence ≥ 0.25" is
  uninformative here: MLEM's zero-valued background gives every noise speck a relative prominence near 1, so that
  rate is 100 % from 20 iterations on. The calibrated absolute floor of D-48 exists for exactly this reason.
- **Decoders:**
  - CC: Studio's factory decoder, through `CorrelationSearch`, which is bit-exact to it.
  - Pixel-area MLEM: `MlemSystemModel(8, t)` on the same grid, with t = exp(−0.178 · μ_rel(661.7) · 10) = 0.1685.
    Sensitivity variants use t = 0 and t = 0.31.
  - Pixel-centre MLEM: `MlemSystemModel(1, 0)`, the vector loop.
  - Iteration snapshots come from `Snapshots`.
- **Timing:** `Stopwatch` medians after one warm-up call, single-threaded, with the machine otherwise idle.
- **Warm start:**
  - A cumulative acquisition is drawn in 20 equal Poisson increments.
  - The warm strategy runs k iterations per increment from the previous λ. It uses my own float loop, which agrees
    with `Snapshots` to 1.0 × 10⁻⁶ of the peak at 120 iterations.
  - It is compared with a cold 120 (and a cold 20·k) on the final counts.
  - N = 16 seeds × 25.

## Per row

### What exists

| Plan claim | Verdict | Evidence |
|---|---|---|
| Decoder selection | holds | `DefaultSimulationFactory.CreateDecoder`: grid half = `ReconHalfExtentMm ?? period/2`, step = `ReconStepMm ?? period/48`, always `CrossCorrelationDecoder(DecodingArray(rank), geo, SubCellInterpolation)` |
| Decoder config | holds | `DecoderConfig`: `Cyclic`, `ReconHalfExtentMm`, `ReconStepMm`, `SubCellInterpolation` |
| MLEM | holds | `MlemDecoder(aperture, geo, iterations = 60)` is the unchanged pixel-centre path (scalar double loop over a float matrix); `MlemSystemModel` / `FromColumns` / `Decode(image, background)` are opt-in. `Decode` reports the integer argmax with no sub-cell, and a confidence equal to peak / second outside ±3 cells |
| How the study builds the claimed MLEM | correct it | `AngularResolutionStudy.BuildMlem` builds `new MlemDecoder(MosaicOf(config), Geometry(config, search, z), v.Iterations.Max(), new MlemSystemModel(v.PixelSubSamples, transmission))`. 120 and 8 come from `angres-request-v2-floor-*.json` / `-v1-select.json`, not from code. The geometry comes from the CC decoder's grid through `CorrelationSearch`, so it is identical to the factory's. Transmission is `ClosedCellTransmission(config)` = exp(−μ · `TungstenMuRel(config.Source.EnergyKeV)` · t) — the thin-mask model, without slab collimation |
| Studio decode | correct it (incomplete) | `AcquisitionSession.ProduceAsync` builds the factory decoder once (`_decoderBuilt`) and decodes the flood every tick on the producer task. But what the Imaging panes show is `ImagingWorkspaceViewModel.Result` = `SelectedChannel?.Image`, from `ImagingService.Process` → `ImagingProjection.Project`, which runs `new DefaultSimulationFactory().CreateDecoder(config)!.Decode(flood)` per channel per refresh at `ImagingSettings.FocalDistanceMm`. The session's decode is only a fallback until the first view arrives |
| Studio default optics | correct it (incomplete) | `OpticsSettings` defaults: rank 13, cell 0.7 mm, D 80, 30 px, 0.6 mm pitch, focus 1000; `MaskConfig` defaults: mosaic 2 × 2, 10 mm, μ 0.178. Grid from `ImagingProjection.AtFocus`: half 0.95 · 13 · 8.75 / 2 = 54.03 mm, step max(0.2, 8.75 / 4) = 2.1875 mm → 50 × 50; non-cyclic. Shadow cell / pixel 1.268 (head at 1 m: 1.058) |
| Studio imaging options | holds | channel, window N, strip, focal plane, focus sweep. `FocusSweepService` builds its own config from `SceneConfigBuilder.Build`, so it stays CC whatever the channel method is |

### MR-1 — config

**Verdict: holds, with corrections.**

- **Type:** use `enum DecoderMethod { CrossCorrelation, Mlem }` with
  `[JsonConverter(typeof(JsonStringEnumConverter<DecoderMethod>))]` on the enum. Then `ConfigLoader` reads
  `"Method": "Mlem"` and `Clone()` round-trips it. A global converter in `ConfigLoader` would instead change how `Save`
  writes `SubCellInterpolation`. Absent means `CrossCorrelation`.
- **No `MlemModel` field.** Pixel-centre is the decoder D-46 rejected. At Studio it false-splits 14.8 % at 80
  iterations (2000 counts), and through its default path it takes 875 ms per decode. Comparisons stay in the studies
  (`mlem`, angres). Keep one model: the claimed one.
- **No `MlemPixelSubSamples` field.** 8 × 8 is verified as the claimed value. At Studio it samples a 0.761 mm shadow
  cell about 10 times per edge. Make it a named constant with its source.
- **Iterations:** `MlemIterations` (default per MR-3), validated ≥ 1.
- **Transmission:** derived, never free. For the single run, from the source line. For Studio, per channel (MR-5).
  Move `ClosedCellTransmission` out of the study into a shared helper (for example next to `TungstenMuRel`). The study
  then delegates to it, so its numbers stay bit-identical.

### MR-2 — factory

**Verdict: holds, with three additions.**

- **The same grid is real.** The study's geometry is taken from the factory CC decoder's own grid (`CorrelationSearch`
  → `Geometry`), so "MLEM via the factory equals the study's construction" can be an exact, bit-equal test: same
  matrix, same loop, and `Parallel.For` writes disjoint rows.
- **Forward-model pattern:** the physical mosaic, inverted when `Mask.Invert`. Fabrication errors stay unknown to
  the decoder, as for CC.
- **A line-energy entry point:** `CreateDecoder(config, lineEnergyKeV)`, or a small `MlemDecoderFactory`. Studio
  channels then get their own transmission without editing `config.Source`.
- **A guard for the study commands.** A scenario with `Method: Mlem` must not silently change about 30 study call
  sites. Some would break (`MaskAntimaskStudy` / `AmbientAntimaskStudy` subtract frames; `CorrelationSearch` throws
  on non-integral weights). Others would rebuild a matrix per configuration (`MixedFieldStudy`, `DepthDesignStudy`,
  `ImagingProjection`).

  Simplest: the factory honours `Method`, and the CLI study dispatch refuses a scenario whose `Decoder.Method` is not
  `CrossCorrelation`, naming the single run as the MLEM path. The single run (`SimulationRunner.Run` / `RunFixedTime`)
  honours it.
- **Single-run caveat:** the CLI flood is a weighted MC image (directional-biasing weights), not Poisson counts. MLEM
  is scale-equivariant, so the weights are fine, but the image is effectively noiseless. The CLI's "Ghost margin
  (primary/secondary peak)" line is a CC notion; MLEM's λ outside the peak goes to ~0, so the ratio is huge. Print a
  method-specific line instead.

### MR-3 — iteration count at Studio's optics

**Verdict: measured; 120 holds, as a choice of its own.**

Pair pass, 1 : 1, 1000 counts per source (16 × 50 per cell), the worst assigned false split over the grid at 2000
counts (16 × 50), and the resolved separation by DR-5's rule:

| Decoder | Pass at 1.0 el | Pass at 1.25 el | Pass at 1.5 el | Pass at 2.0 el | Worst false split (2000) | Resolved |
|---|---|---|---|---|---|---|
| CC | 0.000 | 0.156 | 0.751 | 0.971 [0.943, 0.994] | 0.8 % | 2.0 el |
| area@20 | 0.000 | 0.016 | 0.693 | 1.000 | 0 | 1.75 |
| area@40 | 0.000 | 0.203 | 0.955 [0.941, 0.970] | 1.000 | 0 | 1.5 |
| area@80 | 0.035 | 0.846 | 0.999 | 1.000 | 0 | 1.5 |
| **area@120** | 0.330 | **0.990 [0.984, 0.995]** | 1.000 | 1.000 | **0** | **1.25** |
| area@240 | 0.870 | 0.999 | 1.000 | 0.999 | 0 | 1.25 |
| area@480 | 0.973 | 0.999 | 0.998 | 0.995 | 0.1 % | 1.0 |
| binary@8 | 0.000 | 0.191 | 0.871 | 0.999 | 0 | 1.75 |
| binary@40 | 0.616 | 0.966 | 0.990 | 0.973 | 2.8 % | 1.25 |
| binary@80 | 0.816 | 0.927 | 0.940 | 0.925 | 14.8 % | not reached |

Single source, viewer metric (second peak ≥ 0.25 of the main peak's height; 16 × 50 per cell):

| Decoder | 250 counts | 1000 | 2000 | 4000 | 16 000 | 64 000 |
|---|---|---|---|---|---|---|
| CC | 1.000 | 1.000 | 0.999 | 0.994 | 0.940 | 0.885 |
| area@40 | 0.051 | 0 | 0 | 0 | 0 | 0 |
| area@80 | 0.121 | 0 | 0 | 0 | 0 | 0 |
| **area@120** | **0.247** | 0 | 0 | 0 | 0 | 0 |
| area@240 | 0.424 | 0.011 | 0 | 0 | 0 | 0 |
| area@480 | 0.511 | 0.052 | 0.005 | 0 | 0 | 0 |

At ≥ 0.5 of the height: area@120 gives 5.4 % at 250 counts and 0 above; CC gives 73.5 % at 250 counts and 1.8 % at
1000.

- **Against the hand-held head (DR-5, EV-11):**
  - There, the pixel-area MLEM resolved 1.25 elements at 120 iterations and degraded beyond 240: 1.5 elements at
    320 and 3.0 elements at 480, with a 9.5 % false split.
  - Here, resolution keeps improving up to 480 iterations, and the assigned false split stays ≤ 1.4 % up to 240 at
    every count level. The pixel-centre MLEM is also milder: 14.8 % at 80 iterations against 49 % on the head. The
    likely reason is the sampling: 1.27 pixels per projected cell against 1.06, and 900 pixels against 256.
  - Studio's elements are half as wide in angle (0.501° against 1.042°), so 1.25 elements is 0.63°.
- **Why not the DR-5 rule's answer:**
  - The rule optimises pair resolution at 1000 counts per source. At Studio it runs off the end of the grid (480
    → 1.0 element), so the iteration count is not even bracketed.
  - A live viewer passes through every count level from ~0. The default scene gives 34 cps in the 662 keV window
    (`GateResponse` map: 1.83 × 10⁻⁶ counts per decay at 500 µCi). At speed 10 the first refreshes hold about 100–250
    counts. There 480 iterations draws a quarter-height ghost in half of all frames.
  - 120 resolves 1.25 elements at 1000 counts per source. It keeps that ghost at 25 % at 250 counts (CC: 100 %) and
    0 from 1000 counts. It is the claimed decoder's count, so one name ("pixel-area MLEM, 120 iterations") means one
    thing across EV-11 and Studio.
- **Transmission sensitivity (MR-5 input):**
  - A model that assumes opaque closed cells (t = 0) on 662 keV data passes more at small Δ, but ghosts more: 58 %
    at 250 counts, 120 iterations.
  - A model with t = 0.31 (Co-60's value) is smoother: it resolves 1.5 elements at 120 iterations, with 6 % ghosts at
    250 counts.
  - Over-estimating t is the conservative side.
- **Rule proposed for the plan:**
  - Studio's default is 120.
  - It is recorded as measured at Studio's default optics, with the two tables above. It is not claimed at other
    optics.
  - An optics preset or custom geometry far from the default (for example the Baseline / Wide FOV / High-res presets)
    uses the same 120, labelled "not measured at this geometry" in the docs.
  - A per-geometry rule is not worth building now: the spread between 80 and 240 iterations is small at every count
    level ≥ 1000.

### MR-4 — Studio cost and warm start

**Verdict: measured; MLEM fits the tick with a cache; warm start is not sound as a default.**

| Operation (Studio default image and grid) | Median |
|---|---|
| CC decode (factory decoder, reused or new — no matrix) | 18.9 ms |
| `ImagingProjection.Project`, CC, K = 1 | 18.9 ms |
| Pixel-area matrix build (`Parallel.For`) | 111 ms |
| Pixel-area MLEM, 120 iterations, matrix cached | 39.2 ms (0.33 ms / iteration) |
| Same, new decoder per call (today's `Project` pattern) | 132 ms |
| Pixel-area MLEM, 480 iterations | 196 ms |
| Engine default pixel-centre MLEM, 120 iterations (scalar double loop) | 875 ms |

- **Per refresh**, `ImagingService` decodes All plus one channel per isotope (`ImagingProjection.Project`). With a
  cached matrix, a Cs + Co scene costs about 3 × 40 = 120 ms and four channels about 160 ms, inside 250 ms. Without
  the cache it costs 400–530 ms.
- **What a slow decode affects:** `MainViewModel.ReadSegmentAsync` awaits `Imaging.WhenUpdated` before it reads the
  next snapshot. The producer's channel is bounded (2, drop-oldest), so transport never stalls. Only the display and
  status rate drop.
- **The cache:** one matrix per (grid geometry, transmission). A focal-plane change or a new transmission costs one
  111 ms build. Today a focal change rebuilds nothing (CC). Key it by the projection config's grid and t, and keep the
  last few entries.
- **The 4 Hz snapshot in `AcquisitionSession`** keeps CC: it is only the pre-first-view fallback, and the method is a
  projection choice.
- **Warm start.**
  - MLEM's update is a fixed-point map on the current counts. With nSrc = 2500 > nDet = 900 the Poisson likelihood
    has a set of maximisers, not one point. MLEM's limit, and every early-stopped iterate, depend on the start.
    Early stopping from the flat start is the regulariser that DR-5 and MR-3 tune.
  - Continuing from the previous tick's λ with new counts is therefore a different estimator, even in the limit. Its
    effective iteration count grows with the number of refreshes: 240 refreshes per 60 s at 4 Hz.
  - Measured on 20 increments to 2000 counts (16 × 25 per case):
    - 6 warm iterations per tick (120 in all) pass 96.0 % at Δ = 1.25, against 98.8 % for a cold 120;
    - 30 warm per tick (600 in all) pass 99.3 %, against 100 % for a cold 600;
    - warm and cold images on the same total differ by 27–35 % of the peak (pairs) and 13–15 % (single source).
  - Scaling is not the issue: one iteration restores Σⱼ sⱼλⱼ = Σᵢ yᵢ.
- **Options:**

  | Option | Cost | Iteration meaning |
  |---|---|---|
  | **(a) Cold MLEM every refresh, cached matrix (recommended)** | 39 ms per channel | 120 always means 120 |
  | (b) Warm start | 1 / 20 of the iterations | lost |
  | (c) MLEM at a lower rate on a separate latest-wins worker, as the focus sweep does | none per tick | kept; the image lags |
  | (d) MLEM only on Stop | none while acquiring | kept; no live MLEM |

  (a) fits the tick. (c) is the fallback if a heavy scene (four isotopes plus the 128 × 128 grid allowed by SR-OPT-03,
  which is 6.5× the matrix) exceeds it. A 128 × 128 grid gives a 16 384 × 900 matrix (59 MB float, 118 MB double) and
  about 260 ms per decode. The double copy `_ad` is kept only for `SystemMatrix`; drop it, or the bound must account
  for it.

### MR-5 — Studio UI and interplay

**Verdict: holds in outline; design corrections.**

- **Where the selector lives.** Add `Method` (or `Reconstruction`) to `ImagingSettings`, next to `FocalDistanceMm`. It
  is a re-projection of retained floods, like the focal plane (SR-OPT-04): live in Acquiring, Stopped and Completed,
  with no transport or calibration, and the latest revision wins. It must not enter the acquisition config, or Start
  would freeze it and the session would decode it redundantly.
- **Per-isotope channels:**
  - Each channel's MLEM uses its own primary line's transmission (Cs 0.17, Co-60 0.34 at 1173 keV, Ir-192 0.005,
    Co-57 / Am-241 about 0).
  - "All" mixes energies. Use the highest primary-line energy in the scene, which gives the largest t. That is the
    conservative side measured above.
  - `TopPeaks` (K = the isotope's source count, blanking one element) then `PeakInterpolation.Estimate` with the
    config's sub-cell. Both work on λ unchanged.
- **Stripping.** The stripped flood is max(0, low − Σ R·high): fractional and clipped, not Poisson. MLEM accepts it
  (y ≥ 0), but the clipping biases empty pixels upward. MLEM has a statistically proper alternative already in the
  decoder: decode the raw low-window flood with the known background bᵢ = Σ R·highᵢ (`Decode(image, background)`).

  Recommendation: MLEM + strip uses bᵢ; CC keeps the subtraction. This is new and unmeasured. Turn 2 should measure
  it on a Cs-under-Co scene (Cs localisation and the ghost metric, clipped flood against bᵢ) before it ships. The
  tooltip names the model.
- **Focus sweep.** It stays CC without any code change: `FocusSweepService` builds its own config. Its prominence is
  a CC z-score (mean / std over the image), and MLEM's λ is not depth-calibrated (TODO-25). Say so in `SweepNote`;
  the selector does not invalidate a sweep.
- **Reset / Continue.** Continue appends events, and `ImagingService` keeps its per-acquisition caches. A cold MLEM
  per refresh needs no MLEM state across Continue. Reset → `Begin` → a new id. The decoder cache is keyed by geometry,
  so it may survive Reset harmlessly.
- **Threading.** MLEM runs inside `ImagingService.Process` (`Task.Run`, semaphore), never on the UI thread. The
  matrix build already uses `Parallel.For`. The decode loop is single-threaded; parallelising channels is possible,
  but not needed at the measured cost.
- **Labels:**
  - The reconstruction `HeatmapView` has a fixed `ValueUnit="(decoded)"` in XAML. Bind it: "(correlation)" for CC,
    "(MLEM λ, ≥ 0)" for MLEM.
  - λ is the estimated source intensity per unit sensitivity. It is proportional to emission, so two peaks' heights
    compare source strengths. It is not counts.
  - ROI statistics refresh with the image (`Measurements.Refresh`). Their unit follows the label.
  - Geometry measurements stay valid: same grid, same mm. So, unlike a focus change, a method change need not clear
    reconstruction measurements.
- **The peak chip** (`PeakText` = `Result.Estimate`) would show MLEM's raw argmax unless MR-6's refinement is added.
- **Ambient field.** MLEM has no background term in Studio (the field's true map would be truth). EV-34 shows MLEM
  without bᵢ is pulled more slowly than CC but still pulled. State it in the selector's tooltip.

### MR-6 — estimate

**Verdict: measured; add sub-cell refinement to the MLEM estimate (opt-in, so EV-11 history stays unchanged).**

Single source at 1 m, one position per seed (16 seeds; Poisson rows 16 × 50 per level). Degrees; grid step 0.125°.

| Estimate | Noiseless \|error\|, median / max | RMS at 250 counts | 1000 | 4000 | 16 000 | Median \|bias\| over positions (1000 / 16 000) |
|---|---|---|---|---|---|---|
| CC + tent (Studio today) | 0.039 / 0.085 | 0.163 | 0.062 | 0.052 | 0.048 | 0.033 / 0.037 |
| area@120, argmax (`MlemDecoder.Decode`) | 0.049 / 0.072 | 0.246 | 0.059 | 0.055 | 0.053 | 0.027 / 0.042 |
| area@120 + tent | 0.011 / 0.018 | 0.241 | 0.030 | 0.019 | 0.015 | 0.011 / 0.011 |
| area@120 + gaussian | 0.011 / 0.017 | 0.240 | 0.030 | 0.019 | 0.014 | 0.011 / 0.011 |
| area@120 + parabolic | 0.014 / 0.028 | 0.242 | 0.034 | 0.024 | 0.020 | 0.013 / 0.015 |
| area@120 + 3 × 3 centroid | 0.015 / 0.025 | 0.240 | 0.031 | 0.021 | 0.017 | 0.010 / 0.014 |
| area@80 + tent | 0.010 / 0.016 | 0.167 | 0.029 | 0.018 | 0.013 | 0.011 / 0.011 |

- **The argmax floor is the grid:** step / √12 per axis · √2 = 0.051°, as measured.
- **Tent and gaussian on λ** are equally good and 2–4× better than CC above 1000 counts. Use the configured
  `SubCellInterpolation` (default tent), so CLI, Studio's chip and Studio's found peaks agree.
- **At 250 counts MLEM is worse than CC** (0.24° against 0.16°): spurious second peaks win the argmax (MR-3 table).
- **Implementation:** an opt-in sub-cell argument on `MlemDecoder` (default None), so `MlemStudy`'s EV-11 history and
  the bit-identical tests stay unchanged. The factory passes `config.Decoder.SubCellInterpolation`.
- **Precision is not claimed:** MLEM precision is measured and stated in docs, not claimed. PR-IMG-02 / D-49 stay CC.

### MR-7 — docs

**Verdict: holds.**

- **Requirement rows:** VV.Studio.SRS SR-IMG-03 / SR-IMG-04 / SR-OPT-04 are the neighbours. Add SR-IMG-07 for the
  selector: re-projection; per-channel transmission; strip with bᵢ, or disabled; the label; the focus sweep stays
  CC.
- **Design records:** VV.Studio.SDS SU-23 / SU-25 for `ImagingSettings`, the decoder cache and the energy entry
  point.
- **Evidence:** EV-11 "Reproduce" gains the single-run path. MR-3's tables go to VV.Studio.Imaging as conditional
  evidence at Studio's optics. Evidence numbers stay unchanged.
- **AGENTS.md:** its decoder notes say the pipeline defaults to CC. Keep that, and add the `Method` key.

### MR-8 — tests

**Verdict: holds; tolerances below are derived, not borrowed.**

| Test | Assertion and tolerance |
|---|---|
| Config | `"Method": "Mlem"` and `"mlem"` load through `ConfigLoader`; absent gives `CrossCorrelation`; `Clone()` and `Save` → `Load` keep it (exact) |
| Factory default | `Method` absent returns `CrossCorrelationDecoder`; the reconstruction and estimate equal a directly built decoder bit for bit on lab, Studio and hand-held configs |
| Factory MLEM = study | For `scenario_handheld.json` at 1 m, the factory MLEM's λ equals `new MlemDecoder(MosaicOf, Geometry(config, CorrelationSearch, z), 120, new MlemSystemModel(8, ClosedCellTransmission))` **bit for bit** — same matrix and loop |
| Invert | With `Mask.Invert`, the factory's `SystemMatrix` equals the matrix built from the inverted mosaic (exact) |
| Energy | The transmission at 661.7 keV is exp(−0.178 · 10) (μ_rel = 1 exactly); at another line it is exp(−μ · `TungstenMuRel(E)` · t) (exact) |
| Localisation through the factory | Noiseless self-consistent data y = A·e_j at a grid node j with a unique column: argmax = j exactly after 120 iterations. This reuses turn 2's "far source" construction; MLEM's fixed point for consistent data in the range of a unique column is that column |
| Sub-cell refinement | Opt-in default None leaves `Decode` bit-identical (existing tests); with tent on a synthetic λ that is an ideal tent at offset u, the estimate equals u to 1e-12 (`PeakInterpolation`'s matched case) |
| Study guard | A study command on a `Method: Mlem` scenario exits non-zero with the message; the single run decodes with MLEM |
| ViewModel (Studio.Tests, fake service) | The selector puts the method into `ImagingSettings`; one change gives one request with the other fields unchanged; the unit label follows; a sweep result survives; reconstruction measurements are kept |
| Services | A cached decoder is reused across refreshes (counted by a test hook, or timed: the second call does no matrix build); an MLEM channel's peak for a noiseless single source lies within the self-consistency bound above. A Poisson precision check is evidence, not a unit test |
| Desktop | last, on the author's go |

## Proposed changes to the plan

1. **"What exists" corrections:**
   - Studio's displayed reconstruction comes from `ImagingService` → `ImagingProjection.Project`, which creates a
     decoder per channel per refresh.
   - The study's 120 and 8 come from the request JSON.
   - The Studio grid is 50 × 50 at 2.1875 mm, and the optics are 1.27 pixels per projected cell.
2. **MR-1:**
   - The enum carries `[JsonConverter(JsonStringEnumConverter<DecoderMethod>)]`.
   - Drop `MlemModel` and `MlemPixelSubSamples`: one claimed model, with 8 as a named constant.
   - Keep `MlemIterations`.
   - Transmission is derived per line energy through a shared helper that the study delegates to.
3. **MR-2:**
   - The forward-model pattern honours `Mask.Invert`.
   - Add a line-energy entry point.
   - The CLI study commands refuse `Method ≠ CrossCorrelation`; the single run honours it.
   - The single-run printout is method-specific (no "Ghost margin" for MLEM).
4. **MR-3: Studio default 120 iterations, recorded as measured at Studio's default optics** with this review's
   tables. Do not adopt DR-5's pair-optimal rule for a live viewer (unbracketed at 480; quarter-height ghosts in 51 %
   of 250-count frames). Other presets use 120, labelled "not measured there".
5. **MR-4: cold MLEM on every refresh with a matrix cache** keyed by grid and transmission. No warm start. The
   fallback is a slower latest-wins MLEM worker, only if a heavy scene measures over the tick. `AcquisitionSession`
   stays CC.
6. **MR-5:**
   - The method lives in `ImagingSettings` (a re-projection, SR-OPT-04 semantics).
   - Per-channel transmission; "All" uses the highest primary-line energy in the scene.
   - MLEM + strip decodes the raw low flood with bᵢ = Σ R·high, after a turn-2 measurement on a Cs-under-Co scene.
   - The reconstruction unit label is bound to the method.
   - Measurements are kept on a method change.
   - The sweep stays CC, and `SweepNote` says why.
   - The tooltip states that MLEM has no background term and is pulled by an ambient field (EV-34).
7. **MR-6:** an opt-in sub-cell refinement on `MlemDecoder`, so the factory and Studio use the configured method
   (tent). The measured precision goes to VV.Studio.Imaging as conditional evidence; PR-IMG-02 stays CC.
8. **MR-8:** the tests in the table above, in particular the bit-equal factory = study test and the self-consistent
   grid-node localisation test.
9. **Not here:** a 128 × 128 grid with four channels may exceed the tick (about 260 ms per decode). Record it as a
   known limit with the fallback (5); drop the double copy `_ad` from the area model if memory matters.

## Open questions for the author

1. **Studio's default reconstruction: keep cross-correlation as the default, with MLEM selectable?** *Recommendation:
   yes.* D-49's precision, the existing Studio evidence and the UI scenarios are all cross-correlation. MLEM is better
   above about 1000 counts and worse at 250.
2. **MLEM iteration count in Studio:**
   - (a) fixed at 120, the claimed decoder's count, measured here (recommended);
   - (b) DR-5's pair-optimal ≥ 480 (resolves 1.0 element at 1000 counts per source, but ghosts at low counts);
   - (c) a user-editable count.

   *Recommendation: (a).* (c) invites exactly the over-iteration the evidence warns about.
3. **MLEM during acquisition: live every refresh, or only after Stop?** *Recommendation: live.* It costs about 40 ms
   per channel with the cache and keeps the Imaging workspace one behaviour for both methods.

## What was not run

- Other Studio presets (Baseline, Wide FOV, High-res), other focal planes, other isotopes' channels. I did not run
  MLEM on a stripped flood or with bᵢ, and did not run a 128 × 128 grid timing.
- Ambient fields; 1 : 4 pairs; near-edge placement; a pair point at other count levels; the D-48 significance floor
  at Studio's optics (CAL-04 has no Studio entry).
- Studio's own list-mode flood. The maps are `GateResponse`'s transported means with exact Poisson draws, and
  Studio's chain energy measurement is approximated by a 7 % FWHM window.
- No desktop, no UI tests, no production build or test run (nothing in the repository changed).

## Commands that wrote anything

- `dotnet build -c Release` of the scratch probe `%TEMP%\gcam-todo36\probe\Probe.csproj`, and once with
  `-o %TEMP%\gcam-todo36\probe2bin`. Its project references are the repository's engine and Studio projects, so
  their Release outputs (`src/*/bin`, `obj`) were already up to date or refreshed.
- Probe runs writing to `%TEMP%\gcam-todo36\{smoke,stats,null2000,warm}` and `timing-1.txt`; the aggregation script
  `%TEMP%\gcam-todo36\agg.py` and its outputs `agg-*.txt` and `seeds.txt`.
- This file, `docs/PLAN.Studio.MlemReconstruction.Review.md` (the only repository file written).
