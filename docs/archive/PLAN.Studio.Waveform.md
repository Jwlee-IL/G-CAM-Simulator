# PLAN.Studio.Waveform — the Waveform workspace and front-end chain selection (TODO-10)

Scope: moving `Gcam.Wpf`'s Waveform tab into GCAM Studio — the ADC trace and the shaped trace of acquired events,
and the selectable detection chain (scintillator, photosensor, preamp / shaper) that also sets the spectrum's
resolution and pile-up. Order of work: [PLAN.Studio.Migration](PLAN.Studio.Migration.md); procedure:
[AGENTS.Planning](../AGENTS.Planning.md).

Status: **done** 2026-10-02 (decisions D-1 … D-11 implemented; renders `docs/assets/studio-render/waveform-*`).

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

## Reference readout: four-channel Anger (2026-10-02)

- Scintillator pixels are ordered to match the SiPM array **1:1**, with a **dead region between pixels**
  (reflector / kerf — Studio's `ReflectorGapMm`).
- The array is read through **four 14-bit ADC channels**; the event position comes from the **relative-signal
  (Anger-type charge-division) formula**, the crystal from the flood map.

Consequences checked against the code: every pixel feeds the same four ADCs, so pulse pile-up is **array-wide** (the
Spectrum's existing time-only grouping is the right channel model); two piled-up pulses in different pixels sum in
energy **and** shift the computed position toward their centroid; a multi-site Compton history is positioned by the
light centroid (engine `ComptonStrategy.Centroid`), whereas Studio's list-mode uses `Argmax`. The engine has no
four-channel charge-division model — that is TODO-19, outside this plan.

## Decisions after review (2026-10-02)

| # | Decision | Basis |
|---|---|---|
| D-1 | **One chain setting, captured at Start and carried by the snapshot**; Measurement, Spectrum (bands, resolving time, label), Imaging (windows, H-only calibration) and Waveform read the **acquired** chain; a pending selection is shown separately. Caches key on the acquisition. | review W-1 |
| D-2 | A chain change **marks the acquisition stale** and is disabled while acquiring (provenance policy, consistent with gain σ / I-2). | review W-2 (the planner's "impossible" argument corrected: it is a policy) |
| D-3 | **Scintillator selection maps to a transport material** through an explicit table: GAGG(Ce)→GAGG, NaI(Tl)→NaI, LYSO→LYSO, BGO→BGO. **CsI(Tl) is not offered** (no transport cross sections); a name with no material is an error, never a silent GAGG fallback. A scintillator change needs a new acquisition. | review N-2; checked: `CrystalMaterial.ForConfig` falls back to GAGG for unknown names. **Author (2026-10-02): GAGG is the reference crystal, so GAGG stays the default; the other scintillators exist for what-if comparison** — CsI returns once it has transport data (TODO-21) |
| D-4 | **Channel model = the array-wide channel of a four-ADC Anger readout**: Waveform shows the **summed (energy) channel** of the four ADCs, pulses from every pixel on one trace; Spectrum keeps its array-wide pile-up. The four position channels and pile-up mispositioning arrive with TODO-19. | author; review N-1 |
| D-5 | **Default view: real time base, 10 µs window triggered on a selected acquired event** (latest / next / index), with pretrigger, event markers (index, pixel, deposit, time); wider windows allowed up to the cap. | review W-3: default scene 72.8 cps, 10 µs window >1 pulse 0.07 % |
| D-6 | **Rate study** as a separate, labelled Waveform-only mode ("arrivals re-spaced at N kcps — not the measured rate"), fixed seed, from retained deposits in order; never changes counts, live time, Spectrum, Imaging or stale state. | review W-4: 50 kcps 2–9 % distorted pairs, 1 Mcps 35–74 % |
| D-7 | **Amplitude contract**: each event's amplitude is measured once (gain + chain response, the shared measurement stage); the rasteriser adds no second intrinsic smear (`intrinsicFwhm = 0`); the trace is labelled an ADC simulation whose shaped heights are not the analytic MCA spectrum. | review N-3 (double smearing found) |
| D-8 | **Ideal mode** removes intrinsic smear, analog **and ADC** noise and the rise; keeps the tail and filter; labelled as a shaper stimulus. | review W-5 |
| D-9 | **Readout**: N_pe(662) and FWHM from `FrontEndModel` (single-channel, excludes the pixel gain spread); rise / tail from `PulseSamples` (time constants, not 10–90 %); filter shown as configured (all CR-RC presets: order 4, same K); resolving interval from `EventStreamStudy`. Pulse energy: trapezoid flat-top with matched calibration only; CR-RC shows a calibrated peak with its quantisation caveat or nothing. | review W-6 |
| D-10 | **Window engineering**: origin-relative sample arithmetic (checked), requested length independent of the last event, empty windows show noise, warm-up includes preceding pulses within their support and shaper history; generate only when visible and when selection / window / chain changes; cancel stale work; 10 M samples is an allocation cap, not a 4 Hz promise. | review W-7, N-4, cost table |
| D-11 | Shapers are the existing integer `CrrcInt` / `TrapShape` (RTL bit-exact for the selected coefficients: 0 mismatches over 16 384 samples against the unchanged RTL in Icarus); no floating re-implementation. | review RTL section |

**Separate findings (not in TODO-10):** TODO-19 a conventional four-channel Anger readout; TODO-20 the CR-RC presets
share one filter constant and leave a 32 keV pulse at 0–1 shaped codes.

## Steps

1. **Review (no code):** check each *verify* row; measure (headless) the pulse counts per window at realistic rates
   for the default and a hot scene, the pile-up fraction at those rates for each preamp, and the cost of rasterising
   and shaping a window; check that the shapers used are the RTL-bit-exact ones; propose improvements. Write
   `docs/archive/PLAN.Studio.Waveform.Review.md`.
2. Planner revises this plan ("Decisions after review"); discuss disagreements in the same implementer conversation.
3. Implement: Core (chain setting, window selection, readout maths — pure, tested), services (waveform service on the
   worker), view (Waveform workspace: two `PlotView`s sharing X, chain selectors, readout), shared chain wiring for
   Spectrum / Imaging, tests, offscreen renders (both themes, both sizes).
4. Docs: SRS rows (`SR-WAVE-*`, chain), SDS, VV matrix, DESIGN.Layout; the Spectrum panel's read-only chain becomes
   the shared selector.

**Not here:** RTL changes; the cusp shaper (a Python benchmark by choice); desktop UI tests (last).
