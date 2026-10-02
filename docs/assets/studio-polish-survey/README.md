# Studio polish survey

Diagnostic captures only; no visual baselines or automatic pixel verdicts. The author chooses which issues to fix.

Captured 2026-10-02 by `PolishSurveyTests.AllWorkspaces_BothThemesAndWindowSizes_CapturesPolishSurvey`
on the real desktop (Windows 11, 100 % scaling), Release at b8fddcd plus the desktop-test changes.
All 16 visible-window frames were opened and inspected. Sizes are the requested outer window sizes;
the captured visible frame excludes invisible resize borders.

One shared 60 s acquisition, speed ×10, seed 12345: Cs-137 at (−20, 0) mm, 500 µCi;
Co-60 at (20, 0) mm, 20 µCi; both at 1000 mm from the detector, default optics / chain.
Acquired 4,223 counts. Imaging uses All, N=1.5, strip off, 1000 mm focus; Waveform selects event #10,
10 µs window, real arrivals. The measurement table is empty. No focus sweep is displayed in this survey.

Files: `{imaging,spectrum,waveform,detector}-{dark,light}-{1280x800,1440x900}.png`.
The repository README uses a separate Imaging / dark / 1440×900
[studio-desktop-imaging.png](../studio-desktop-imaging.png) capture, retaken with Co-60 at **200 µCi**;
Cs-137 remains 500 µCi and the positions, distance, seed, live time and other settings are unchanged.
It has 6,974 counts and found peaks Cs-137 (−20.6, 0.3) mm / Co-60 (20.2, 0.5) mm.
Their unrounded position errors, 0.676 / 0.546 mm, are below one 8.75 mm resolution element.
Run `20261002-130331-aa0ebe`; hash and raw coordinates are in [desktop evidence](../studio-desktop-evidence.json).
The 16 survey frames retain their original 20 µCi Co-60 scene. Their current hashes identify the planner's
verification capture run `20261002-123249-231a4f`; no survey image was changed by the README retake.

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
| P-09 | Spectrum, chain text | The default chain carried a leftover first-person label from `Gcam.Wpf`; say "CSP + CR-RC (200 ns)". | spectrum-* |
| P-10 | Spectrum, table | "Emission windows" columns do not line up with their headers (Counts, Share), and the Ba K row is labelled only "Cs-137". | spectrum-* |
| P-11 | Spectrum, physics look | The 480–620 keV valley is empty and there is no backscatter peak: the bare geometry Studio uses (no entrance absorber, no backing) — fixed by TODO-08 phase A, not by styling. | spectrum-* |

## Not issues

- Focus rings are visible in both themes (Distance tool after Tab; the theme button after `Ctrl+2`).
- The status line reads well: "Completed · t = 60.0 s of 60 s · 6,463 counts · 110 cps".
- The workspace switch is legible in both themes; its UIA Select defect is a behaviour bug (VV.Studio AN-10).

## Current observations (2026-10-02)

P-01 … P-11 above are the historical survey, not current defect claims. Counts chips, round colour-bar ticks,
compact top-bar inputs, explicit pile-up wording, a neutral preset name, Ba K emitter naming and table header alignment
are now visible. No layout was changed in this pass.

| # | Where | Observation | Screenshot |
|---|---|---|---|
| P-12 | Spectrum emission table | The Co-60 line energy and window bounds touch: `1173.2` immediately precedes `1100.4–1246.0`. The same fixed columns touch at both window sizes and in both themes. | spectrum-* |
| P-13 | Imaging found-source overlays | At 1280×800 the found Cs-137 and Co-60 text labels nearly touch; truth and found markers are close together. At 1440×900 there is more room. | imaging-*-1280x800 |
| P-14 | Shared locked inputs | Disabled chain selectors, source fields and seed are faint, especially in light theme. They remain readable in these captures; this is a visual observation, not a contrast measurement. | all light frames |
| P-15 | Waveform | Intended: event labels appear only above the ADC plot (batch-3 label policy); the shaped plot keeps the same marker lines and shared time axis. Event #10 remains listed on the right. | waveform-* |
| P-16 | Detector | The face and gain legend fit in both sizes and themes; the acquired-settings caption clearly says locked until Reset. The right geometry panel has substantial empty space below its explanatory text. | detector-* |

The status bar, four workspace selectors, complete Spectrum table and both Waveform plots fit in all inspected
frames. The focus-sweep interval is verified numerically by its desktop scenario, not by these captures.

Batch-4 offscreen verification (2026-10-02) addresses P-12, P-13, P-14 and P-16: content-sized emission columns
with an explicit gap, bounded collision layout for truth/found chips, disabled text contrast above 4.5:1 in both
themes, and a Detector geometry card ending after its text. P-15 remains intended and unchanged.
Fresh analytic fixtures are in [studio-render](../studio-render/README.md); both themes' affected 1280×800
renders were opened and inspected. These survey desktop captures have not been replaced or revalidated.
