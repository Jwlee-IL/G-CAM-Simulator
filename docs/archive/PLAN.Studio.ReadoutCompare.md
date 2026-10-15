# PLAN.Studio.ReadoutCompare — Direct crystal against four-output Anger in GCAM Studio

Scope: TODO-47. A **Compare** workspace shows one physical-readout acquisition read two ways, side by side: the ideal
direct crystal assignment and the four-output Anger readout (14-bit ADCs, flood-map LUT, pile-up). The goal is theory
against a realistic readout, with no second run.

Status: **done 2026-10-15** (merged `8bad1ee`; closed-task index in [archive](README.md#closed-tasks), Findings 71).
Implemented 2026-10-15 by Codex (turns 1–3: D1–D10, then render fixes and sizing; `aaf09a0`); docs applied by the
planner (SR-CMP-01 … 05, SU-28, V&V trace, History, AGENTS / DESIGN / README); desktop: Compare and the affected scenarios passed
2026-10-15 (20 passed, 1 opt-in skipped).
Earlier: review by Codex done 2026-10-15 (conversation `01a13e2e…`, [review](PLAN.Studio.ReadoutCompare.Review.md));
all ten proposals adopted below ("Decisions after review"); implementation next in the same conversation. History:
reference plan 2026-10-15, written by the planner. The UI decisions come from the author (below). Next: a Codex
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

## Decisions after review (planner, 2026-10-15)

The review measured Studio's own numbers on one seed (on-axis Cs-137 at 1 m, 10 s, ambient off). Same-crystal share was
71.4 % on Baseline and 52.6 % on Sharp (All), and the unknown share was 0 % and 1.2 %. The lab study's 36 % is therefore
not quoted as a live figure. Every proposal is adopted; the numbers refer to the review.

| # | Decision |
|---|---|
| D1 | C1 as proposal 1: the physical hit stream keeps each consumed input's direct facts (`RealisedReadoutHit` gains pixel x / y and deposit, +16 bytes per input), published with the existing chunk store; dominant indices are validated against the same snapshot; `AcquisitionSnapshot.Events` keeps its meaning (empty on the physical path). |
| D2 | The direct reference is "Direct crystal · ideal deposit energy": the largest summed-deposit pixel and the unsmeared total deposit of the same histories (proposal 2). No measurement stage or legacy chain runs on it, and no equivalence to a separately started DirectCrystal acquisition is promised. |
| D3 | Accounting uses explicit populations (proposal 3): histories, held, assigned, unknown, multi-contributor, no-dominant, distinct-dominant and never-dominant. Agreement excludes multi-contributor conversions, which are shown as their own count. |
| D4 | The difference view is the assignment-disagreement map of proposal 4: per direct pixel, different / eligible, with an empty denominator shown as unavailable (never 0 %). The overall share is Σ same / Σ eligible, and unknowns stay a separate count. |
| D5 | Windows and lines follow Imaging's physical settings (proposal 5): explicit keV edges, the captured calibration line through `ReadoutPolicy.LineForWindow`, and no fallback to another LUT. The spectrum overlay shows the same line, identical bins, raw counts and an overflow count. This replaces C5's per-path N × FWHM. |
| D6 | Compare gets its own Core contract and view model (`IReadoutCompareService`, `CompareWorkspaceViewModel`) and one atomic result per prefix (proposal 6). |
| D7 | The comparison runs on its own cancellable worker, only while Compare is active, with one latest pending request; nothing is added to the acquisition loop or the awaited Imaging / Spectrum pair (proposal 7). The refresh cost is measured and reported. |
| D8 | Layout as chosen, with explicit empty states (proposal 8): (a) direct mode → the physical-readout note and "Choose readout…"; (b) no snapshot → "Start an acquisition to compare"; (c) no held, assigned or window events → the direct image stays and the reason is shown; (d) while updating → keep the last consistent pair and its time. Separate `ImageStackPanel` per image; bounded rows; a scrolling side panel; Ctrl+5. |
| D9 | The reference cost table reuses the embedded, pin-checked record loader, the captured preparation plan and `ReadoutCostHelp` wording (proposal 9). A small evaluator lists the differences between this scene and the benchmark (isotope / line, distance, position, rate, module), and it is built once and tested. |
| D10 | Exclusions and verification as proposal 10. The report export notes that Compare is not in the report, and its capture must not read the new physical direct facts as legacy events. Tests use exact retained-input projections and hand-built fixtures, not a second run. |

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
