# PLAN.Studio.Waveform — the Waveform workspace and front-end chain selection (TODO-10)

Scope: moving `Gcam.Wpf`'s Waveform tab into GCAM Studio — the ADC trace and the shaped trace of acquired events,
and the selectable detection chain (scintillator, photosensor, preamp / shaper) that also sets the spectrum's
resolution and pile-up. Order of work: [PLAN.Studio.Migration](PLAN.Studio.Migration.md); procedure:
[AGENTS.Planning](AGENTS.Planning.md).

Status: **reference plan** (2026-10-02) — the implementer reviews it first (measured), then the plan is revised.
Rows marked *verify* are not established.

## What exists (checked in the code, 2026-10-02)

| Item | Where | Fact |
|---|---|---|
| Chain presets | `Gcam.Configuration/FrontEndParts.cs` | moved from `Gcam.Wpf` in TODO-07; `Default` = (Scintillators[0], Sensors[0], Preamps[1]); `AdcSampleRateHz` = 125 MHz; `PulseSamples` gives rise / tail in ADC samples |
| Chain use in Studio | `MeasurementStage`, `SpectrumService`, `ImagingService` | **each hard-codes `FrontEndParts.Default`** (three places); the Spectrum panel shows it read-only |
| Waveform engine | `Gcam.Detector/Waveform.cs` | `Rasterize(events, adc, tau, tauRise, noiseKev, intrinsicFwhm)`, `TrapShape`, `CrrcInt` (bit-exact to the RTL shapers per the cocotb tests), `DefaultAdc` = AD9648 14-bit 125 MSPS, `Blr`, `FlatTop` |
| `Gcam.Wpf` Waveform tab | `MainWindow.xaml(.cs)` `RenderWaveform`, `UpdateFrontEnd` | inputs: scope rate (kcps, default 50), events (40, max 4000), "Realistic front-end" (off = ideal pulses, no noise), three chain combos; two plots (ADC codes, shaped output); the shaper's pole-zero is re-matched to the chain's tail |
| `Gcam.Wpf` time base | `RenderWaveform` | draws `nEvents` deposits from the acquisition pool and spaces them by **exponential gaps at the scope rate**, not at the acquisition's real rate |
| Studio events | TODO-13 list-mode acquisition | every acquired event has pixel, true deposit and **real Poisson arrival time** |
| Plot | `PlotView` | min/max pyramid, 10 M-sample input cap, headless redraw query ~0.5 ms at 10 M |

**Time-base problem found while checking.** At Studio's real detected rate (~10² cps for the default scene) the
mean gap is ~10 ms ≈ 1.25 M samples; 40 pulses would need ~50 M samples — five times the plot's input cap — and
pile-up would almost never be seen. `Gcam.Wpf`'s scope rate shows pile-up by compressing time to an invented rate.
Neither choice is obviously right; this needs a decision backed by measurement.

## Proposed design (reference — the implementer may improve any of it)

| # | Proposal | Status |
|---|---|---|
| W-1 | **One shared chain setting** (scintillator, sensor, preamp) owned by the shell; `MeasurementStage`, `SpectrumService`, `ImagingService` and the Waveform workspace take it from there — no `FrontEndParts.Default` left in services. | proposed |
| W-2 | A chain change is a **detector input**: it marks the acquisition stale, like gain σ (decision I-2 in PLAN.Studio.ImagingOptions — a real detector's chain cannot change on recorded data). | proposed — *verify* the consistency argument; the alternative is "re-process retained events, never stale" (the events keep true deposits, so it is possible) |
| W-3 | Waveform shows the **real acquired stream**: an oscilloscope-style window (length in µs) triggered on a chosen event (latest / next / by index), ADC trace and shaped trace sharing the time axis, with the real arrival times. | proposed — *verify* how many pulses a useful window holds at realistic rates, and whether pile-up is visible at all |
| W-4 | Keep `Gcam.Wpf`'s **rate what-if** as an explicitly labelled mode ("events re-spaced at N kcps — not the measured rate") for showing pile-up and shaper behaviour. | proposed — *verify* it is worth keeping; if kept, it must never be the default and its label must say the rate is invented |
| W-5 | "Realistic front-end" off = ideal pulses (no rise, no noise, no intrinsic smearing) as in `Gcam.Wpf`, for teaching the shaper. | proposed |
| W-6 | Readout from the chain: N_pe(662), FWHM at 662 keV, rise / tail (ns), shaper type, resolving time; flat-top amplitude of the selected pulse vs its deposit. | proposed — *verify* each number's source function |
| W-7 | Trace length bounded by the plot's 10 M-sample cap (≈ 80 ms at 125 MSPS) with a visible note when a window is clipped. | proposed |

## Steps

1. **Review (no code):** check each *verify* row; measure (headless) the pulse counts per window at realistic rates
   for the default and a hot scene, the pile-up fraction at those rates for each preamp, and the cost of rasterising
   and shaping a window; check that the shapers used are the RTL-bit-exact ones; propose improvements. Write
   `docs/PLAN.Studio.Waveform.Review.md`.
2. Planner revises this plan ("Decisions after review"); discuss disagreements in the same implementer conversation.
3. Implement: Core (chain setting, window selection, readout maths — pure, tested), services (waveform service on the
   worker), view (Waveform workspace: two `PlotView`s sharing X, chain selectors, readout), shared chain wiring for
   Spectrum / Imaging, tests, offscreen renders (both themes, both sizes).
4. Docs: SRS rows (`SR-WAVE-*`, chain), SDS, VV matrix, DESIGN.Layout; the Spectrum panel's read-only chain becomes
   the shared selector.

**Not here:** RTL changes; the cusp shaper (a Python benchmark by choice); desktop UI tests (last).
