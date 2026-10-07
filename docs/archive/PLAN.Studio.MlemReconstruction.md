# PLAN.Studio.MlemReconstruction — MLEM as a regular reconstruction path (config, CLI, GCAM Studio)

Scope: TODO-36. PR-IMG-04 is claimed with an MLEM whose forward model integrates each pixel's area (D-46, EV-11), but
the scenario configuration can select only cross-correlation: `DefaultSimulationFactory.CreateDecoder` always returns a
`CrossCorrelationDecoder`, so neither the CLI single run nor GCAM Studio can reconstruct with MLEM; MLEM exists only
inside study classes. The author found this odd (decision review, 2026-10-06). This plan makes MLEM a selectable
reconstruction in the config, the factory and Studio's Imaging workspace, without moving any existing number.

Status: **done** 2026-10-07 — implemented and verified (headless, then on the desktop 2026-10-07: six MLEM scenarios pass, see PLAN.Studio.DesktopChecks); [review](PLAN.Studio.MlemReconstruction.Review.md), [turn report](PLAN.Studio.MlemReconstruction.Turn2.md).

## What exists (checked in the code, 2026-10-07)

| Item | Where | What it does |
|---|---|---|
| Decoder selection | `src/Gcam.Simulation/DefaultSimulationFactory.cs` `CreateDecoder` | builds `CodedApertureGeometry` (grid: one FCFOV period, step period / 48 unless `Decoder.ReconHalfExtentMm / ReconStepMm`) and always returns `CrossCorrelationDecoder(MuraGenerator.DecodingArray(rank), geo, SubCellInterpolation)` |
| Decoder config | `src/Gcam.Configuration/SimulationConfig.cs` `DecoderConfig` | `Cyclic`, `ReconHalfExtentMm`, `ReconStepMm`, `SubCellInterpolation`; no method field |
| MLEM | `src/Gcam.Decoding/MlemDecoder.cs` | default pixel-centre model (bit-identical to the original); opt-in `MlemSystemModel(PixelSubSamples, ClosedCellTransmission)`, supplied columns, background term; `IDecoder` |
| How the study builds the claimed MLEM | `src/Gcam.Simulation/AngularResolutionStudy.cs` | `new MlemDecoder(MuraGenerator.Mosaic(rank, mosaicX, mosaicY), geo, 120, new MlemSystemModel(AreaPixelSubSamples, ClosedCellTransmission(config)))`, transmission = exp(−μ·μ_rel(E)·t); 120 iterations chosen at the hand-held head, 1 m, 1000 counts per source (DR-5) |
| Studio decode | `src/Gcam.Studio.Services/AcquisitionSession.cs` `ProduceAsync` | builds the decoder once through the factory, decodes the whole flood on every published tick (≈ 4 Hz) on the producer task (not the UI thread), publishes `ImagingResult` |
| Studio default optics | `src/Gcam.Configuration/SceneConfigBuilder.cs` | D = 80 mm, 30 × 30 pixels, focal distance 1000 mm — **not** the hand-held head the iteration count was chosen for |
| Studio imaging options | `src/Gcam.Studio.Core/ViewModels/ImagingWorkspaceViewModel*.cs` | isotope channel, stripping, focal plane, focus sweep (`FocusSweepService`, decodes per plane, non-cyclic) |

## Proposed decisions (reference — verify / improve)

| ID | Decision | State |
|---|---|---|
| MR-1 | **Config:** `DecoderConfig.Method` = `CrossCorrelation` (default) / `Mlem`; for MLEM `MlemModel` = `PixelArea` (default for MLEM) / `PixelCentre`, `MlemIterations` (default 120), `MlemPixelSubSamples` (verify the study's value); closed-cell transmission derived from the mask config as in the study, not a free parameter. JSON round-trip and `Clone()` keep the fields | proposed |
| MR-2 | **Factory:** `CreateDecoder` switches on `Method`; the MLEM grid is the same `CodedApertureGeometry` as cross-correlation's. With `Method` absent every existing scenario, test and evidence number is bit-identical | proposed |
| MR-3 | **Iteration count outside the validated geometry:** 120 was chosen for the hand-held head at 1 m. At other optics (Studio's default D = 80, 30 × 30) the false-split behaviour is unknown — **measure** it at Studio's default optics (single-source false split versus iterations, as DR-5 did) and decide the default from that measurement; if it differs, either a per-geometry rule or a labelled default. Never borrow 120 silently | verify (review turn) |
| MR-4 | **Studio cost:** MLEM per tick on a 30 × 30 image over Studio's grid may exceed the 4 Hz budget (dense matrix ≈ pixels × grid points per iteration) — measure; options: warm-start from the previous tick's λ (MLEM iterations continue as counts accrue), decode MLEM at a lower rate than the flood, or only on Stop. Pick by measurement; the cross-correlation path keeps its rate | verify |
| MR-5 | **Studio UI:** a reconstruction selector (Cross-correlation / MLEM) in the Imaging workspace, applied to the image panes and the estimate; MLEM's image labelled as non-negative λ (not correlation units); the focus sweep stays cross-correlation (state why in the UI tooltip / docs); per-isotope channels and stripping work with either method or the combination is disabled with a reason | proposed — verify channel / stripping interplay |
| MR-6 | **Estimate:** the MLEM estimate's peak refinement (argmax / sub-cell) and its single-source bias at Studio's optics are measured and stated; PR-IMG-02's precision (D-49) is a cross-correlation figure and is not claimed for MLEM | verify |
| MR-7 | **Docs:** VV.Studio SRS / SDS rows for the selector, AGENTS.Studio / DESIGN notes, EV-11 "Reproduce" gains the CLI single-run MLEM path; Evidence numbers unchanged | proposed |
| MR-8 | **Tests:** config round-trip; factory returns the right decoder; default path bit-identical; MLEM via the factory equals the study's construction for the same config; ViewModel tests for the selector (Studio.Tests, no UI stack); desktop UI test last, on the author's go | proposed |

## Decisions after review (2026-10-07)

The review ([PLAN.Studio.MlemReconstruction.Review](PLAN.Studio.MlemReconstruction.Review.md)) found seven wrong
premises; the planner confirmed the main one in the code (Studio's displayed image comes from
`ImagingProjection.Project`, which builds a new decoder per channel per refresh, `ImagingProjection.cs:29`) and adopts
the review's nine proposals, except where the author decided otherwise below.

| ID | Decision | Source |
|---|---|---|
| MD-1 | **Config / engine:** `DecoderConfig.Method` enum (CrossCorrelation default / Mlem) with its own string converter; `MlemIterations` kept; no `MlemModel` / sub-sample fields (pixel-area is the only MLEM offered; the sub-sample count a named constant shared with the study); closed-cell transmission per line energy through one helper the study also calls; the MLEM forward model honours `Mask.Invert`; an opt-in sub-cell argument on `MlemDecoder` (EV-11 history bit-identical); factory MLEM equals the study's construction bit for bit | review #2, #3, #7 |
| MD-2 | **Only the CLI single run honours `Method`;** study commands that call the factory refuse or ignore an MLEM config with a clear message (antimask subtraction, `CorrelationSearch`); the single-run printout is method-specific | review #3 |
| MD-3 | **Studio default stays cross-correlation; MLEM selectable (author)** | **author** (recommended) |
| MD-4 | **Studio MLEM iteration count by the DR-5 rule (author: "480 이상"):** extend the iteration grid at Studio's default optics until the rule (smallest resolved separation including the ≤ 5 % false-split limit, ties to fewer iterations) is bracketed, on seeds disjoint from the check seeds, and use the selected count; record the low-count side effect beside it (a 250-count single source shows a quarter-height second peak in ~51 % of frames at 480) and show it in the UI as a caveat; other presets labelled "iteration count not measured for these optics" | **author** (chose the rule over the recommended 120); review MR-3 |
| MD-5 | **Live on every refresh, cold decode, matrix cached (author):** no warm start (not equivalent: the likelihood has no unique maximiser here); if a refresh's decode exceeds the tick, only the display rate drops — transport never stalls; the measured display rate at the selected iteration count is reported | **author** (recommended); review MR-4 |
| MD-6 | **MLEM + stripping included now (author):** decode the raw low-window flood with the high-window contribution as the known background term `Decode(image, background)`; **first measure it** on a Cs-under-Co scene at Studio's optics (location and count of the weaker line versus cross-correlation + stripping), then implement; if the measurement shows it is worse than cross-correlation + stripping, stop and report before building the UI path | **author** ("이번에 포함"); review #6 |
| MD-7 | **Studio placement:** the method lives in `ImagingSettings` (a re-projection setting like the focal plane) with a per-geometry, per-energy matrix cache; `AcquisitionSession` stays cross-correlation; reconstruction unit label bound to the method; measurements kept across a method change; focus sweep stays cross-correlation with a note why | review #6 |
| MD-8 | **Precision claims:** PR-IMG-02 / D-49 stay cross-correlation figures; MLEM's single-source precision at Studio's optics is reported (with sub-cell refinement), not claimed | review MR-6 |
| MD-9 | **Known limit:** a 128 × 128 grid would take ~260 ms per decode at 120 iterations (×4 at 480) | review #9 |

**After turn 2 (2026-10-07).** Turn 2 implemented MD-1 … MD-9 ([Turn2](PLAN.Studio.MlemReconstruction.Turn2.md)); the
planner verified it (build 0 / 0; full tests green: engine 465, Studio 207, Services 95; the CLI single run with
`"Method": "Mlem"` decodes with the pixel-area MLEM; a study command refuses such a scenario; `calibration_record.py
--release` passes). Two findings went to the author:

| ID | Decision | Source |
|---|---|---|
| MD-10 | **Iteration count 400, confirmed on fresh seeds (author):** the DR-5 rule picked 360 on the selection seeds, but 360 passes 1.0 element at only 0.949 [0.927, 0.968] on the check seeds; 400 passes 0.961 on both. Because 400 was chosen after seeing the check seeds, it is re-checked on a third, disjoint seed set (16 seeds × 50, same conditions: pass at 1.0 element, false split at 2000 counts, the 250-count second-peak rate) before it becomes the Studio default; if it fails there, stop and report | **author** (recommended) |
| MD-11 | **Cross-correlation + strip's clipped count is a separate TODO (author):** Studio's cross-correlation strip shows the per-pixel clipped sum Σ max(0, low − R·high), which reads 4.5–25 % high (the floored-stripping bias of EV-15); MLEM + background shows the unclipped Σ low − Σ b. TODO-36 keeps both as they are and labels the difference; aligning them is TODO-39 | **author** (recommended) |

## Steps

1. Review turn (no code): verify the rows; measure MR-3 (false split vs iterations at Studio's default optics, and one
   resolved-pair point), MR-4 (decode time per tick, warm-start behaviour), MR-6; propose →
   `docs/PLAN.Studio.MlemReconstruction.Review.md`.
2. Decisions after review; implement engine + Studio; headless verification (Studio.Tests, render snapshots); desktop
   UI test on the author's go.

## Done when

- A scenario JSON with `"Method": "Mlem"` reconstructs with the pixel-area MLEM in the CLI single run.
- Studio's Imaging workspace switches between cross-correlation and MLEM live, within the tick budget or with a stated
  slower MLEM rate.
- The iteration default at Studio's optics is a measured choice; every existing number unchanged; docs updated.

## Not here

- A Studio pair-resolution verdict using CAL-04's floors (the floors are per geometry and exist only in the evidence
  aggregation) — a later item if wanted.
- MLEM in the focus sweep / depth estimate (TODO-25 territory).
