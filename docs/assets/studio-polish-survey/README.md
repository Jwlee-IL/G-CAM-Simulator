# Studio polish survey

Diagnostic captures only; no visual baselines or automatic pixel verdicts. The author chooses which issues to fix.

Captured 2026-10-01 by `PolishSurveyTests.Imaging_BothThemesAndWindowSizes_CapturesPolishSurvey` on the real
desktop (Windows 11, 100 % scaling) after one 60 s list-mode acquisition of the default Cs-137 scene, a distance
measurement and a Tab focus step: `imaging-{dark,light}-{1280x800,1440x900}.png` and
`spectrum-{dark,light}-{1280x800,1440x900}.png`.

## Issues

| # | Where | Issue | Screenshot |
|---|---|---|---|
| P-01 | Imaging, flood header | The chip still says "weighted counts"; since list-mode every count is one event (integer counts). | imaging-* |
| P-02 | Imaging, readouts | The flood readout ends in a bare value ("x 4.5 mm, y 3.9 mm · 7") — no unit or word ("7 counts"). | imaging-dark-1440x900 |
| P-03 | Imaging, colour bars | Ticks are evenly divided, not round (0.0 / 6.2 / 12.5 / 18.8 / 25.0; −1027 / −1 / 1025 …); "−1" reads as a value, not a tick. `NiceTicks` exists for the plot. | imaging-* |
| P-04 | Imaging, layout | At 1440 × 900 the image panels leave about 40 % empty height below the colour bars. | imaging-*-1440x900 |
| P-05 | Top bar | Live time / Speed boxes are much taller and wider than the label line and the switch; units sit in the labels ("Live time (s)", "Speed ×") while the left panel puts units inside the field. | all |
| P-06 | Spectrum, bands | Addressed: centred / clamped labels with collision rows. Offscreen dark / light evidence in [studio-plot](../studio-plot/README.md); desktop re-survey deferred. | spectrum-* |
| P-07 | Spectrum, readout | Addressed: bin centre, bounds and counts with units and bin-width precision. Core readout test passes; desktop hover walkthrough deferred. | spectrum-* |
| P-08 | Spectrum, panel | Section title "Pile-up" above a checkbox labelled "Pile-up" — the label repeats. | spectrum-* |
| P-09 | Spectrum, chain text | The default chain is named "… CSP + CR-RC (your rig)" — a leftover first-person label from `Gcam.Wpf`; say "original rig". | spectrum-* |
| P-10 | Spectrum, table | "Emission windows" columns do not line up with their headers (Counts, Share), and the Ba K row is labelled only "Cs-137". | spectrum-* |
| P-11 | Spectrum, physics look | The 480–620 keV valley is empty and there is no backscatter peak: the bare geometry Studio uses (no entrance absorber, no backing) — fixed by TODO-08 phase A, not by styling. | spectrum-* |

## Not issues

- Focus rings are visible in both themes (Distance tool after Tab; the theme button after `Ctrl+2`).
- The status line reads well: "Completed · t = 60.0 s of 60 s · 6,463 counts · 110 cps".
- The workspace switch is legible in both themes; its UIA Select defect is a behaviour bug (VV.Studio AN-10).
