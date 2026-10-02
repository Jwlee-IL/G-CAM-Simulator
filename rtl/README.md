# RTL peak-detector co-simulation

## CR-RC fractional-state and preset contract

`crrc_shaper.sv`, `trap_ref.crrc_int` and C# `Waveform.CrrcInt` share Q16 coefficients and a separate
state fraction parameter: **F=0** retains the original whole-code recurrence/goldens, **F=12** retains
fractional state. Raw output is signed codes × 4096 for F=12; divide by 4096.0 for plotted codes, never
floor it back to whole codes before measurement. The default fixture still uses order 4, A=53656, K=26214,
F=0; Studio explicitly selects F=12 and its acquired preset coefficients. CR-RC energy readout remains
unavailable pending trigger/phase/overlap estimator validation.

All three Studio CR-RC presets keep **order 4**. Their explicit T_sum shaping times are **100/200/500 ns**,
separate from the DCR effective integration windows. The convention is T_sum = order × nominal Euler RC
time, K=round(65536×order×8/T_sum): **20972/10486/4194** at 125 MSPS. This is a **simulation convention**,
not measured rig circuitry, a peaking-time definition, or a derivation of the DCR equivalent noise window.
A=round(exp(-1/tailSamples)×65536) remains matched to the selected scintillator/CSP tail.

Bounded domain: signed-16 input, 0≤A≤65536, 1≤K≤65536, 1≤ORDER≤16, 0≤F≤12, Q=16.
Input is sign-extended before shifting. With B=2^(15+F), |imp|≤2B+1. An RC update is the floor
of a convex combination of integer endpoints, so every stage stays in that same interval from reset;
|u−acc|≤4B+2. The largest product is at most (4B+2)×65536 = **35,184,372,219,904** for F=12,
less than 2^46. RTL uses **signed 48-bit states and products**; C# signed long and Python integers produce
the same bounded results. RTL rejects widths below WIN+F+Q+3 (47 bits for signed-16/Q12/Q16).
This bound covers arbitrary bounded input sequences and valid gaps, not hardware clock timing.

`crrc_contract.py` computes expected coefficients independently of DUT readback. The cocotb matrix covers
the legacy operating point plus GAGG/NaI/LYSO/BGO × Fast/Original/Slow, each with F=0 and F=12. It checks
32/32.1/122/662-keV finite-rise pulses, a near-threshold sweep, signed full-scale/noisy inputs, reset/valid
holds, accepting-edge output (zero sequence offset), and optional C# vectors against Python and actual RTL.
The legacy 400→800 proportionality tolerance remains 0.05; preset paths use exact sample equality rather
than borrowing that legacy tolerance for their whole-code deadbands.

To include C# comparisons, export vectors with the engine test in a fresh temporary directory:

```powershell
$env:GCAM_CRRC_VECTORS = Join-Path $env:TEMP ('gcam-crrc-vectors-' + [guid]::NewGuid().ToString('N'))
dotnet test tests/Gcam.Tests -c Release --filter FullyQualifiedName~CsharpContractVectors_UseConfiguredCoefficientsAndOptionalExport
# Use a python.org Python with cocotb and Icarus on this process's PATH.
python rtl/run_cocotb.py --csharp-vectors $env:GCAM_CRRC_VECTORS
```

The runner creates fresh `%TEMP%/gcam-rtl-*` build/results directories, performs no cleanup, and exits
nonzero on any failure. Without `--csharp-vectors`, the C# comparisons are reported as skipped;
`--crrc-only` omits the unchanged trapezoid/BLR suites. On Windows the runner sets LIBPYTHON_LOC from
the executing Python installation. Do not use Store Python. No GUI is requested.

Execution 2026-10-02: **137 passed / 0 failed / 0 skipped** (130 CR-RC cases across 26 configurations,
six trapezoid cases and one BLR case). The 26 C# vectors compare **452,608 samples** exactly against both
Python and RTL. A/K/order/F/Q/width are pinned to independent fixture math. No synthesis/Fmax or
physical hardware result is claimed. Response measurements and reproduction are in
[VV.Studio.Waveform](../docs/VV.Studio.Waveform.md#cr-rc-arithmetic-and-response-evidence).
The final CR-RC-only rerun also passed all 130 cases after adding an explicit finite-rise piled-pulse
stimulus; the seven unchanged trapezoid/BLR cases passed in the full run.

## Peak-detector studies

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

## Trapezoidal shaper + cocotb co-sim (`trapezoidal_shaper.sv`)
The noise-optimal shaper, done in RTL and verified with **cocotb** (the project's first cocotb
co-sim — it drives the DUT directly instead of the file-I/O testbench). `trapezoidal_shaper.sv`
is the recursive **Jordanov-Knoll** trapezoidal filter with pole-zero (decay `M`) correction:
`d^{k,l}[n]=v[n]−v[n−RISE]−v[n−L]+v[n−RISE−L]`, `p+=d`, `r=p+M·d`, `s+=r`. The flat top ∝ energy
(no ballistic deficit), the `M` term deconvolves the exponential tail (baseline restoration), and
two piled-up pulses give two resolvable flat tops (`trap_shaper.png` via `trap_shaper_study.py`,
which uses the bit-exact integer reference `trap_ref.py`).
- `trap_ref.py` — integer reference, **bit-exact** to the RTL (Python `>>` = SV signed `>>>`).
- `test_trap_shaper.py` — cocotb testbench: single + piled pulses, asserts RTL == reference.
- `run_cocotb.py` — Icarus cocotb runner (`get_runner("icarus")`).

Run (⚠ needs a **non-Store Python**; the Windows-Store Python's `python311.dll` is access-denied
to Icarus's VPI loader — use the python.org 3.13 install):
```bash
<python.org 3.13 python> run_cocotb.py   # TESTS=2 PASS=2
python trap_shaper_study.py                                                        # -> trap_shaper.png
```

## FPGA synthesis (Yosys utilization + Vivado timing)
Real target-mapped numbers, not just simulation. **Yosys** (scoop `yosys` 0.9) synthesizes the shaper
to FPGA primitives — the module uses plain `parameter` + `integer` loops so both the cocotb/Icarus flow
and Yosys 0.9 parse it:
```bash
yosys -p "read_verilog -sv trapezoidal_shaper.sv; synth_xilinx -top trapezoidal_shaper; stat"   # Artix-7
yosys -p "read_verilog -sv trapezoidal_shaper.sv; synth_ecp5 -top trapezoidal_shaper; stat"     # Lattice ECP5
```
One channel on Artix-7 ≈ **809 LUT / 512 FF / 0 DSP / 0 BRAM** (~4 % of an XC7A35T); the constant M
multiply maps to LUTs, not a DSP. `ltp` longest path = 225 cells (un-pipelined accumulator chain) →
estimated Fmax ~85–125 MHz. **Exact Artix-7 timing** needs Vivado (FREE ML Standard covers Artix-7, no
paid licence): `vivado -mode batch -source vivado_trap.tcl [-tclargs <top> <part> <period_ns>]` runs
synth+place+route and prints Fmax. (nextpnr targets Lattice only, so the open flow's real Fmax is an
ECP5/iCE40 proxy, not Xilinx.)

`trapezoidal_shaper_pl.sv` is a **pipelined** variant (bit-exact, +3 samples latency): `s = q + mm` with
`q=Σp`, `mm=Σm`, so every feedback loop is a single adder (the FIR/multiply/combine are feed-forward
stages). cocotb verifies both tops against the same reference. **Measured Fmax** (open flow, no Vivado) —
OSS CAD Suite `yosys synth_ecp5` + `nextpnr-ecp5` on a Lattice ECP5-6 (needs the OSS-CAD-Suite bin+lib on
PATH for its DLLs):
```bash
OSS=~/scoop/apps/oss-cad-suite-nightly/current; export PATH="$OSS/bin:$OSS/lib:$PATH"
yosys -q -p "read_verilog -sv rtl/trapezoidal_shaper_pl.sv; synth_ecp5 -top trapezoidal_shaper_pl -json x.json"
nextpnr-ecp5 --json x.json --25k --package CABGA381 --speed 6 --freq 250   # -> Max frequency ... MHz
```
Result: **direct 59 MHz vs pipelined 119 MHz (×2.0)** — `rtl/fmax_ecp5.png`; the direct shaper misses a
100 MSPS clock on this part, the pipelined one clears it. Yosys `ltp` cell-count could NOT show this
(multiply/carry dominated); real place-and-route STA does.

## Completed integrations and remaining follow-ups
- **Done:** drive the cocotb testbench from the **C# MC per-event stream** (`mc_event_stream_matches_reference` and the BLR bench consume `event_stream.txt`;
  real event times/energies are rasterized alongside the synthetic fixtures).
- **CR-RC done:** fractional-state preset contract and C# vectors are documented above. Cusp remains a Python benchmark (`shapers.py`, `shaper_compare.py`); folding the trapezoid's resolution-vs-rate into the
  material study remains a follow-up.
- **Done:** C# `SourceConfig.Lines` / `EmissionLine` and mixed-field transport support multi-line, multi-source coded-aperture imaging.
