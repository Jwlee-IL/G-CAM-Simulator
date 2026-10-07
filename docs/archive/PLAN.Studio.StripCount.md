# PLAN.Studio.StripCount — Studio's cross-correlation strip count: unbiased, with its uncertainty

Scope: TODO-39 (from TODO-36 MD-11). With Compton strip on, Studio's cross-correlation path reports the channel's
count as the sum of the **clipped** strip flood Σ max(0, low − R·high), which reads 4.5–25 % high on a Cs-under-Co scene
(TODO-36 MD-6; the floored-stripping bias EV-15 reports, +17 % at 8 : 1). The MLEM path already reports the unclipped
net Σ low − R·Σ high. This plan makes the cross-correlation count unbiased and states its uncertainty, and decides what
the cross-correlation decoder itself should see.

Status: **done** 2026-10-07 — review and implementation by Codex (session `01a114d2-2425-7443-82a7-1cae27f52193`; [review](PLAN.Studio.StripCount.Review.md), [turn 2](PLAN.Studio.StripCount.Turn2.md)); planner checks: build 0 / 0, full tests 799 passed, engine and evidence unchanged, desktop in a worktree 35 / 35 with broken-verdict 7 / 7 at their assertions and recovery 7 / 7; VV records applied (SR-IMG-05 / 07, SR-MEAS-03, SDS, traceability, Imaging, History).

## What exists (checked in the code, 2026-10-07)

| Item | Where | What it does |
|---|---|---|
| Strip | `src/Gcam.Studio.Services/ImagingService.cs` (`ProcessAsync`, strip block ~l.130–152) | per pixel `flood = max(0, low − Σ R·high)` (raw high floods, never recursively stripped); the clipped flood becomes the channel's flood — displayed **and** decoded by cross-correlation; MLEM instead gets the raw low flood with b = Σ R·high and `EffectiveCounts = Σ low − Σ b` |
| Count label | `src/Gcam.Studio.Core/ViewModels/ImagingWorkspaceViewModel.cs` `CountLabel` / `Summary` | "counts (clipped strip sum)" for cross-correlation, "net counts (Σ low − R·Σ high)" for MLEM (MD-11) |
| Ratio | `ImagingService.Calibrate`, `StripRatio(LowIsotope, HighIsotope, R, LowCounts, HighCounts)` | R from a separate calibration acquisition |
| Test | `tests/Gcam.Studio.Services.Tests/ImagingServiceTests.cs` `Strip_CoLocatedCsAndDoubleActivityCo_RecoversSameLiveTimeCsCount` | compares the stripped count with a Cs-only acquisition within 4σ, σ² = low + R²·high + expected + high²·Var(R), Var(R) = R²(1/L + 1/H) |
| SRS | `docs/VV.Studio.SRS.md` SR-IMG-05 ("subtracts … per pixel, clamped at …"), SR-IMG-07 | |

## Proposed decisions (verify / improve)

| ID | Decision |
|---|---|
| SC-1 | **Count:** the cross-correlation strip count becomes the unclipped net N = Σ low − R·Σ high (same estimator as MLEM's), labelled "net counts" for both methods, shown with its 1σ: σ² = Σ low + R²·Σ high + (Σ high)²·Var(R), Var(R) from the calibration counts (delta method, as the existing test) — verify the variance against the calibration's actual construction (`Calibrate`) |
| SC-2 | **What cross-correlation decodes (verify, possibly an author decision):** the decoder is linear, so the unclipped difference image low − R·high is the unbiased input; clipping at zero biases the image wherever the expected difference is small (the floored-stripping bias). Options: (a) decode the unclipped difference, display the clipped flood (display only); (b) decode and display the unclipped difference (a diverging colour scale for negative pixels); (c) keep decoding the clipped flood. Measure (a) vs (c) on the TODO-36 MD-6 Cs-under-Co scenes (Cs location error and found-peak behaviour) before choosing |
| SC-3 | **Found peaks / ROI statistics** on the stripped channel use the same image the decoder used; an ROI sum on a clipped display must not be presented as a count (label or switch) — verify what ROI reads today |
| SC-4 | **Tests:** the existing strip test asserts the net count (tighter); a unit test that the reported σ matches the empirical spread over seeds (derived tolerance); SRS SR-IMG-05 / SR-IMG-07 text updated by the planner |
| SC-5 | **No engine / evidence change:** EV-15's numbers are the engine's study and stay; this is Studio's display and decode path only |

## Decisions after review (2026-10-07)

The review (16 fresh seeds × 50 per scene, Studio's default optics) measured: the net estimator's bias −3.4 … +1.6
counts against +50 … +250 for the clipped sum; empirical SD / predicted σ 0.942–1.042 (two bootstrap intervals exclude
one, so exact agreement is not claimed); signed cross-correlation decoding RMS 0.039–0.073° against 0.039–0.083° clipped,
one peak per frame, no selected peak outside one element or nearer Co-60. The planner adopts its six proposals; the
author answered its three questions with the recommended option each time.

| ID | Decision | Source |
|---|---|---|
| SD-1 | **Count:** the cross-correlation strip count is the signed net N = Σ low − R·Σ high, shown as `N ± σ net counts (1σ, counting + calibration)` for both methods; σ includes the overlap covariance the review identified, or the label reads "uncertainty unavailable" where the model does not support it; the unbiasedness claim is qualified to the scalar strip model; negative N is shown as is; data-presence checks are separate from the count's sign; no new detection gate | **author** (Q3, recommended); review #1, #4 |
| SD-2 | **Decode signed, display clipped (author):** cross-correlation decodes the unclipped difference low − R·high; the displayed flood stays the clipped strip view; the UI names the two roles | **author** (Q1, recommended); review #2 |
| SD-3 | **ROI on a stripped flood reports the displayed (clipped) values**, labelled as clipped strip statistics with explicit units; the channel-wide net ± σ is in the summary line | **author** (Q2, recommended); review #3 |
| SD-4 | **Tests:** the uncertainty test's bounds are derived independently (acquisition seeds do not vary today's calibration — vary or bound it explicitly); the existing strip test asserts the net count | review #5 |
| SD-5 | **No engine / evidence change**; EV-15 and the engine studies stay as they are | review #6 |

## Steps

1. Review turn (no code): verify the rows, measure SC-2 (a) vs (c) with a small seed set, check SC-3, propose →
   `docs/PLAN.Studio.StripCount.Review.md`.
2. Decisions after review (author for SC-2 if it changes what is displayed); implement in the same conversation;
   headless verification; desktop scenario update if a label or image changes (planner, on the author's go).
