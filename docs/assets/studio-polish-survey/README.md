# Studio polish survey

Diagnostic captures only; no visual baselines or automatic pixel verdicts.

The 2026-10-01 attempt could not capture the screen: the desktop harness failed at `SetCursorPos` /
UI Automation `SetFocus`, and its failure screenshot reported an invalid handle. No screenshots or observed
polish issues are claimed. Existing desktop assertions remain unchanged. TODO-06 stays open until the
regression scenarios and this survey run on an accessible interactive desktop.

`PolishSurveyTests.Imaging_BothThemesAndWindowSizes_CapturesPolishSurvey` creates these files after simulation,
a distance measurement, keyboard heatmap zoom / reset and a Tab focus check:

- `imaging-dark-1280x800.png`
- `imaging-light-1280x800.png`
- `imaging-dark-1440x900.png`
- `imaging-light-1440x900.png`

After running, inspect every capture and list spacing, alignment, truncation, contrast and focus-order issues
here with the affected screenshot. The author chooses fixes. None are applied by the survey.
