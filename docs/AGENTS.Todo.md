# AGENTS.Todo — open work items handed between sessions

Scope: the short list of concrete, ready-to-start tasks. A task's context, decisions and steps live in its
`PLAN.*` document, linked from the row; longer-term or undecided work stays in [AGENTS.Backlog](AGENTS.Backlog.md);
results go to [AGENTS.Findings](AGENTS.Findings.md). A finished task is deleted here and recorded in the Backlog's
done list (and Findings, if it produced a result) in the same commit; its plan stays, marked done.

| ID | Task | Origin | Owner | State |
|---|---|---|---|---|
| TODO-11 | Detector workspace (reflector gap, SiPM pitch, crosstalk) and depth (3D) / rangefinder in Imaging | `Gcam.Wpf` Detector tab, Imaging depth | Studio session | open |
| TODO-12 | Delete `src/Gcam.Wpf`; update `Gcam.sln`, CI, AGENTS / README / VV.Studio; README screenshot of Studio | end of the migration | Studio session | open (last) |
| TODO-14 | Per-decay list-mode emission: correlated cascade gammas (Co-60 1173 + 1332) with one arrival time, so true coincidence summing comes out of the pile-up stage | found in TODO-08 phase A (wrong premise in R-2) | — | open — needs an engine design (biasing vs angular correlation) |
| TODO-16 | UI polish in batches — batch 1: design-system level (units, round ticks, input sizing, labels, plot axis and band colours) with whole-window render snapshots — [PLAN.Studio.Polish](PLAN.Studio.Polish.md) | author 2026-10-01 (polish is the focus) | Studio session | batches 1–2 done; layout batch after the workspaces exist |
| TODO-19 | Model the original rig's readout: 1:1 pixel-matched SiPM array with dead regions, **four 14-bit ADC channels with Anger-type charge division** (position from relative signals, crystal from the flood map / LUT). Brings: centroid positioning of multi-site histories (Studio list-mode uses Argmax today), array-wide pile-up that shifts the position as well as the energy, crystal mis-identification near dead regions, four-channel traces in the Waveform workspace. Needs the author's network details (resistor topology, thresholds, LUT method) | author, 2026-10-02 (TODO-10 discussion) | — | open — reference plan + author input |
| TODO-20 | CR-RC shaper presets: all three use order 4 and the same K (20 ns low-pass) regardless of name or integration time; a 32.1 keV pulse leaves 0–1 shaped codes (662 keV: 47 / 90 / 114). Decide whether that is intended; if not, re-parameterise with the RTL contract (cocotb expected math) updated deliberately | PLAN.Studio.Waveform.Review, 2026-10-02 | — | open |
| TODO-21 | CsI(Tl) transport data: add CsI to `CrystalMaterial` (mass attenuation and photoelectric share vs energy from the same tabulated sources as theme 52), so the CsI front-end preset can be offered for what-if comparison (the rig itself used GAGG, which stays the default) | author, 2026-10-02 | — | open |
| TODO-22 | **Final desktop pass** (last, after layout polish): desktop UI tests on Start / Stop and all workspaces (Imaging channels / strip / refocus, Spectrum, Waveform), the desktop polish survey, the 16 ms plot gate in the real window, README screenshot of Studio — covers the desktop parts left open by TODO-07, 13, 15, 16 | PLAN.Studio.Migration (order of polish and testing) | Studio session | open — after TODO-11 and the layout batch |

Next free ID: TODO-23. TODO-06 … TODO-13 are the `Gcam.Wpf` → Studio migration; order and shared decisions:
[PLAN.Studio.Migration](PLAN.Studio.Migration.md) (06 → 13 → 07 → 08 …).
