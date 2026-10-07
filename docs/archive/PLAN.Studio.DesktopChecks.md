# PLAN.Studio.DesktopChecks — TODO-38 fix and TODO-36's desktop checks as UI automation

Scope: two desktop items, bundled by the author (2026-10-07): (1) TODO-38 — the desktop test
`Imaging_ChannelAndStrip_MatchRetainedFloodAndFoundPeaks` fails deterministically since Studio's ambient default
(TODO-30 AB-15); (2) TODO-36's desktop checks (the MLEM Reconstruction selector, SR-IMG-07), written as UI automation
scenarios instead of a manual list, so they are repeatable. The implementer works headless; the planner runs the desktop
suite in an isolated worktree when the author frees the desktop.

Status: **done** 2026-10-07 — [turn report](PLAN.Studio.DesktopChecks.Turn1.md) (turns 1–2); the planner's desktop runs: 34 / 34 full, broken-verdict 7 / 7 at their corrupted assertions, recovery 7 / 7; recorded in VV.Studio.History (2026-10-07).

## What exists (checked, 2026-10-07)

| Item | Where |
|---|---|
| Failing test | `tests/Gcam.Studio.UiTests/WorkspaceScenarioTests.cs` `Imaging_ChannelAndStrip_MatchRetainedFloodAndFoundPeaks`, line 152 `Assert.True(high.Peaks[0].Xmm > 0)` (Co-60 channel peak on the +x side); scene from `TwoIsotopes` (Cs-137 default 500 µCi at −20 mm, Co-60 20 µCi at +20 mm, 1 m, seed 12345, 60 s); run `20261007-121043-87fbdc` (worktree `C:\gw\wui`, `01dbca6`), repeated identically |
| Ambient default | TODO-30 AB-15: Studio starts with the validated spectrum at 0.10 µSv/h, front-only; the UI tests do not set it |
| UI harness | `tests/Gcam.Studio.UiTests` (opt-in `GCAM_UI_TESTS=1`), `StudioWindow` helpers, `FloodOracle` (parses "(decoded)" readouts), manifests under `bin/…/ui-runs/<runId>/`; rules in `docs/AGENTS.UiAutomation.md` |
| New AutomationIds | `Imaging.Reconstruction`, `Imaging.ReconstructionNote` (TODO-36) |
| Desktop check list | `docs/PLAN.Studio.MlemReconstruction.Turn2.md` "Desktop checks for the planner" (8 items) |

## Proposed decisions (verify / improve)

| ID | Decision |
|---|---|
| DC-1 | **TODO-38 cause first, with data, headless:** reproduce the scene through the Studio services (same config builder, seed, ambient default) and report the Co-60 channel's counts and peak position with the ambient field on and off. Only then choose the fix |
| DC-2 | **Fix by the cause, never by loosening:** if the ambient default is the cause, the test either sets its environment explicitly (ambient off for a scene that tests channel separation, stated as such) or raises Co-60 to an activity whose channel count makes the +x side guaranteed by a derived criterion (e.g. the Co channel's expected counts against the ambient counts in its windows); if the cause is something else (a real regression), stop and report |
| DC-3 | **MLEM desktop scenarios** covering the turn-report list where automation can judge them against an independent oracle: selector switch during a live acquisition (unit "(MLEM λ)", note present / absent, image non-negative, status advancing); MLEM + strip on Cs + Co (found Cs peak side, "net counts" label); unmeasured-optics note at focal plane 1500 mm; measurements kept across a switch; focus sweep runs under MLEM; Stop → Continue and Reset keep the selection without a stale image. Keyboard reachability and both themes via the existing survey pattern if cheap. `FloodOracle` learns the MLEM unit |
| DC-4 | **No desktop runs by the implementer** (guard rule 4): it builds and runs the headless suite; the planner runs `GCAM_UI_TESTS=1` in a worktree on the author's go and records SR-RUN-29 / SR-IMG-07 desktop results in VV.Studio |

## Done when

- TODO-38's cause shown with numbers and the test fixed by a derived criterion (or a regression reported).
- MLEM desktop scenarios exist, compile, and pass on the desktop (planner's run); VV.Studio records the desktop
  results for SR-RUN-29 and SR-IMG-07.
