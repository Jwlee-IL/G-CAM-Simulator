# AGENTS.Todo — open work items handed between sessions

Scope: the short list of concrete, ready-to-start tasks. A task's context, decisions and steps live in its
`PLAN.*` document, linked from the row; longer-term or undecided work stays in [AGENTS.Backlog](AGENTS.Backlog.md);
results go to [AGENTS.Findings](AGENTS.Findings.md). A finished task is deleted here and recorded in the Backlog's
done list (and Findings, if it produced a result) in the same commit; its plan stays, marked done.

| ID | Task | Origin | Owner | State |
|---|---|---|---|---|
| TODO-07 | Spectrum workspace — [PLAN.Studio.Spectrum](PLAN.Studio.Spectrum.md) | `Gcam.Wpf` Spectrum tab | Studio session | committed `45eece8`; checked on the desktop (survey) |
| TODO-08 | Detector realism, gain σ, background; per-nuclide imaging and Compton strip — [PLAN.Studio.ImagingOptions](PLAN.Studio.ImagingOptions.md) | `Gcam.Wpf` Imaging tab; R-2 / R-5 | Studio session | phase A plan corrected after Codex stopped; phase B after review |
| TODO-09 | Editable optics + presets (lifts the "optics read-only" exclusion in VV.Studio.SRS §7) | `Gcam.Wpf` Optics / Presets tab | Studio session | open |
| TODO-10 | Waveform workspace: front-end chain presets, ADC + shaped traces | `Gcam.Wpf` Waveform tab | Studio session | open (needs `PlotView` decimation) |
| TODO-11 | Detector workspace (reflector gap, SiPM pitch, crosstalk) and depth (3D) / rangefinder in Imaging | `Gcam.Wpf` Detector tab, Imaging depth | Studio session | open |
| TODO-12 | Delete `src/Gcam.Wpf`; update `Gcam.sln`, CI, AGENTS / README / VV.Studio; README screenshot of Studio | end of the migration | Studio session | open (last) |
| TODO-13 | Live list-mode acquisition feeding every workspace — [PLAN.Studio.LiveAcquisition](PLAN.Studio.LiveAcquisition.md) | author decision 2026-10-01 (S-1 / S-4) | Studio session | committed `0ed17e0`; desktop tests migrated and passing (R-6) — open: AN-10, AN-11 |
| TODO-14 | Per-decay list-mode emission: correlated cascade gammas (Co-60 1173 + 1332) with one arrival time, so true coincidence summing comes out of the pile-up stage | found in TODO-08 phase A (wrong premise in R-2) | — | open — needs an engine design (biasing vs angular correlation) |

Next free ID: TODO-15. TODO-06 … TODO-13 are the `Gcam.Wpf` → Studio migration; order and shared decisions:
[PLAN.Studio.Migration](PLAN.Studio.Migration.md) (06 → 13 → 07 → 08 …).
