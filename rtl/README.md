# RTL peak-detector co-simulation

The ADC front-end of the gamma camera, as real SystemVerilog RTL simulated by
**Icarus Verilog**. Each gamma interaction makes a pulse on the digitized detector
signal; this stage extracts its **pulse height (= energy)** and timestamp — the
values that feed the flood-map positioning upstream.

```
Python (pulse shaping)  ->  adc.txt  ->  [peak_detector.sv]  ->  peaks.txt  ->  Python (scoring/plots)
                                          iverilog + vvp
```

## Files
- `peak_detector.sv` — the DUT: baseline tracker + threshold/peak-hold discriminator
  (IDLE → TRACK → emit amplitude & time). Overlapping pulses merge → pile-up loss.
- `tb_peak_detector.sv` — streams `adc.txt` one sample/clock (100 MHz), writes each
  detected peak to `peaks.txt`.
- `gen_stimulus.py` — synthetic waveform: Poisson-timed events, Cs-137 energies
  (662 keV photopeak + Compton), bi-exponential pulses, baseline noise.
- `analyze.py` — matches detections to truth → efficiency, photopeak FWHM, pile-up;
  plots the waveform + reconstructed spectrum (`rtl_<label>.png`).
- `run.sh` — runs the whole chain.

## Run
```bash
./run.sh 50000  2000 lowrate    # sparse: ~98% eff, ~7% FWHM
./run.sh 1500000 2000 highrate  # pile-up: ~44% eff (events merge), spectrum degrades
```
Requires `iverilog`/`vvp` on PATH (`scoop install iverilog`) and Python+numpy+matplotlib.

## Result
The RTL reconstructs the Cs-137 spectrum from the raw ADC stream: at low rate the
662 keV photopeak comes through at the injected 7% FWHM; at high rate pile-up merges
overlapping pulses and detection efficiency collapses — the front-end's rate limit.

`adc.txt`, `truth.csv`, `peaks.txt`, `sim.vvp`, `rtl_*.png` are generated artifacts.

## Material rate-capability study (`material_rate_study.py`)
Feeds each scintillator's decay time (from the C# presets' `DecayTimeNs`) — plus a GAGG
afterglow model — into the same RTL and sweeps the count rate. Result (`material_rate.png`):
fast crystals resist pile-up (CeBr3 17 ns holds 87% at 2 Mcps), slow ones roll off
(NaI/BGO), and **GAGG's afterglow collapses it** (0.2% at 1 Mcps) — while **GAGG:Ce,Mg**
(afterglow fixed) behaves per its 55 ns decay. Quantifies why plain GAGG is rate-limited.

## Per-crystal uniformity study (`pixel_uniformity_study.py`)
Runs the RTL per pixel, each with its own gain and decay time, and aggregates the
pulse-height spectrum. Per-crystal gain scatter smears the photopeak (7.4% → 18% at 15%
gain σ); dividing each pixel's amplitudes by its calibrated gain restores ~7.4%. Energy
resolution is the ENERGY-domain counterpart of the C# uniformity study — unlike
coded-aperture *position* (robust), energy **requires per-channel gain calibration**.

## Front-end energy trust — peak-hold failure modes (`ballistic_deficit_study.py`)
Groundwork for multi-isotope work: **when can you believe the reconstructed energy?**
Drives the baseline `peak_detector.sv` three ways (`ballistic_deficit.png`): (1) isolated
pulses have no ballistic deficit (peak-hold gets the true max) but the Cs-137 gain leaves full
scale at ~859 keV, so **Co-60 saturates**; (2) two overlapping pulses **merge into a fake
higher-energy line**, and a slow crystal stays merged ~5× longer (CeBr3 80 ns vs GAGG 400 ns);
(3) at rate the photopeak centroid drifts and a fake sum-tail grows to ~20% at 2 Mcps.

## Front-end fix — charge integration + pile-up rejection (`integrating_peak_detector.sv`)
The real system measured every pulse → contaminated energy at rate. This module integrates
CHARGE over a runtime `window` (= shaping time) and **rejects** piled-up events instead of
mis-measuring them, using a **fast derivative arrival detector** that catches a 2nd pulse riding
on the first's tail (which a level/edge detector misses on slow crystals). `shaping_pileup_study.py`
compares it to the peak-hold (`shaping_pileup.png`): rejection ~halves the fake sum-tail
(9%→4% at 1 Mcps) at the cost of throughput (accepted/true); the window↔decay match is
crystal-dependent (fast crystals want a short window — best resolution AND throughput).
- `tb_integrating.sv` — streams `adc.txt`, integration length from `+WINDOW=<n>`, writes
  `<time> <energy> <reject>` to `peaks_int.txt`.

## Multi-isotope discrimination + dynamic range (`multi_isotope_study.py`)
Drives the A2 integrating+reject front-end with a **Cs-137 + Co-60 + Co-57** field (presets in
`../samples/isotopes/*.json`: multi-line + `cascadeCoincident`), each line with its Klein-Nishina
Compton continuum. Results: (`multi_isotope.png`) **set the gain to the highest line, not the primary
source** — a Cs-137 gain saturates Co-60, while a gain with 1332 keV under full scale covers the whole
122→1332 keV span with no low-line penalty at 12-bit (so dual-gain is only for bit-starved / noisy
front-ends); (`cascade_pileup.png`) **cascade sum (rate-linear) vs random pile-up (rate²)** separate
by an activity sweep, crossing at ~200 kdecays/s — the "is this fake sum peak real?" answer.

## Compton stripping — spectral count recovery (`compton_stripping_study.py`)
The spectral complement to the coded-aperture *spatial* separation (C# theme 15). A higher line's
Compton continuum spills into a lower line's window, so a raw window over-counts the lower isotope;
stripping models each line's KN continuum (net × (1−pf)/pf × shape) and subtracts it top-down
(`compton_stripping.png`). Recovers well-separated lines (Cs-137 662: raw +39% → stripped −5%) but
accumulates error top-down (close Co-60 doublet leak → Co-57 over-corrected) — so it's complementary
to the spatial lever, which is more robust for close/cascade/overlapping lines.

## Next (not done)
- **Crystal-internal Compton scatter** — done in C# (`ComptonCrystalDetector`); could combine with
  per-pixel stripping inside the coded pipeline (spectral clean-up + spatial separation together).
- Port to a **cocotb** testbench (ModelSim/Questa/Verilator) + drive from the C# MC event stream.
- Trapezoidal / CR-RC shaping (validated in Python) as the noise-optimal refinement of the
  fixed-window integrator.
- Fold the multi-line isotope model into C# `SourceConfig` for coded-aperture multi-source imaging.
