# PLAN.Studio.ReadoutCompare — Direct crystal against four-output Anger in GCAM Studio

Scope: TODO-47. A **Compare** workspace shows one physical-readout acquisition read two ways, side by side: the ideal
direct crystal assignment and the four-output Anger readout (14-bit ADCs, flood-map LUT, pile-up). The goal is theory
against a realistic readout, with no second run.

Status: reference plan 2026-10-15, written by the planner. The UI decisions come from the author (below). Next: a Codex
review turn that checks the premises and proposes improvements, then "Decisions after review", then implementation in
the same conversation.

## The author's decisions (2026-10-15)

- "크리스탈 직접 - 4채널 앵거로 이론-실제를 비교할 수 있으면 그것도 괜찮겠지." Then, on a Todo row: "그럼 UI부터 먼저 생각해야 할텐데".
- Placement, chosen from three mockups: **a new workspace "Compare"** after Imaging · Spectrum · Waveform · Detector.
  The rejected options were a compare mode inside Imaging / Spectrum and a Detector view tab.
- Content, all four offered items chosen:
  1. the image pair with the estimates;
  2. the crystal-assignment difference;
  3. the spectrum overlay;
  4. the reference cost table.

## Reference decisions (planner defaults, to verify in the review)

| # | Decision | Reason |
|---|---|---|
| C1 | **Data: one acquisition, both readouts from the same transported histories.** Under the physical readout, each held conversion names its dominant hit (`ReadoutEvent.DominantHit`, an index into the processed hit list). The direct assignment of the same histories is the largest-deposit crystal with the total deposit, which is today's `DetectedEvent`. Verify that the acquisition retains, or can retain without a second transport, the per-history direct data (`PixelX`, `PixelY`, `DepositKeV`, `ArrivalTimeS`) beside the physical records. If it does not, propose the smallest change that keeps it (the transport already produces it). Never run a second simulation for the comparison. | "Every number from the same transported histories" (Todo row); one run, two readings. |
| C2 | **Availability.** Compare works only for an acquisition taken with the experimental physical readout. Otherwise it shows the same empty state as Waveform's Four outputs: "Available with the experimental physical readout" and "Choose readout…". | One rule for every physical-only view (SR-WAVE-09, SR-DET-06). |
| C3 | **Layout** (the mockup the author chose): centre columns "Direct crystal" and "Four-output Anger", each holding a flood map above a reconstruction, with truth ○ and found ◇ markers as in Imaging. A spectrum overlay strip spans the bottom: Direct solid, Anger dashed, the energy windows shaded. The right panel "Comparison" holds the numbers (C5) and the reference cost table (C6). View only: no measurements and no editing. | The author's pick; the image pair reuses `ImageStackPanel` and the heat-map controls. |
| C4 | **Crystal-assignment difference**: a third view of the flood pair, a toggle "Assignment difference". It is a per-crystal map of the share of direct-assigned events that the Anger LUT placed in another crystal. The panel shows the overall same-crystal share. 662 keV photopeak events land in another crystal than the largest deposit about 36 % of the time (Findings 68, EV-36), because multi-crystal Compton histories sit at their light centroid. Unassigned (LUT unknown) conversions are counted separately. | It makes the 36 % visible where it happens. |
| C5 | **Numbers in the panel**, every one from the snapshot with no new simulation: events (direct histories, held conversions, assigned, unassigned); same-crystal share; each path's estimate, its offset from the truth (mm and °), and the difference between the two; each line's FWHM and in-window counts on both paths. Each path's reconstruction uses Imaging's current decoder, channel and window rule (N × FWHM with that path's own resolution). | One acquisition can show these. F needs seed ensembles, so it is not computed here (C6). |
| C6 | **Reference cost table**: the 1 m offer-cost record rows for the current preset and line (F, gross-error share, RMS at 1 k / 4 k counts). The table uses the existing applicability rule from TODO-41: it is marked "reference only" with the differences listed whenever the scene differs from the benchmark. | It shows the measured ensemble cost without pretending one run measures it (Findings 69). |
| C7 | **Layering and performance.** The view models live in Core and are tested headless. The projection of the direct path from the retained histories lives in Services, behind a contract, and runs on a worker with the existing snapshot cadence. The cost is measured and stated, with a 4 Hz refresh budget. | Same rules as the other workspaces (`AGENTS.Studio.md`, `DESIGN.Architecture.md`). |
| C8 | **Out of scope for the first cut**: report-export sections for Compare, comparing independent-SiPM readout, multi-seed F inside Studio, and comparing two separate acquisitions. | Keep the first cut small. TODO-48 (manual) picks Compare up afterwards. |

## Premises to verify first (stop and report if one is wrong)

- C1: the per-history direct data for a physical-readout acquisition is retained, or can be retained cheaply.
- `DominantHit` maps a conversion back to the history the direct path would place. State what happens with pile-up,
  where one conversion has several contributing histories.
- The 36 % figure belongs to the lab-study geometry. State what Studio's presets give, measured, not quoted.
- A fifth workspace fits the top bar at 1280 px with the export icon present, and Ctrl+5 is free.

## Verification

- Build with 0 warnings and 0 errors. Run the full suite with `GCAM_UI_TESTS` unset, taking the baseline first.
- New tests:
  - the direct projection from the same histories equals a DirectCrystal acquisition of those histories (same seed, no
    pile-up);
  - the same-crystal share equals an independent count;
  - the empty state appears under the direct assignment;
  - the view models follow the snapshot;
  - the reference table applicability.
- Offscreen renders of Compare in both themes at 1280 × 800 and 1440 × 900.
- A desktop scenario for Compare: written and compiled, run by the planner on the author's go.
- Catalog entries; `check-catalog --discover`.
