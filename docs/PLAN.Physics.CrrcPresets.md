# PLAN.Physics.CrrcPresets — CR-RC shaper presets: fractional state and explicit shaping time (TODO-20)

Scope: the CR-RC⁴ integer shaper used by the Waveform workspace (and bit-exact to `rtl/crrc_shaper.sv`) loses
low-energy pulses because every accumulator update floors to whole output codes; the three CR-RC presets also share one
inherited benchmark coefficient K. Procedure: [AGENTS.Planning](AGENTS.Planning.md).

Status: **done** 2026-10-02 (Codex, worktree `C:\gw\p20`). Findings 58. Planner re-verified: build 0 / 0, tests 272 / 164 / 75 (+7) / 13 (+14),
cocotb 137 / 137 with the C# vectors. Review (served as the reference plan):
[PLAN.Physics.CrrcPresets.Review](PLAN.Physics.CrrcPresets.Review.md).

## Decisions after review (author, 2026-10-02)

| # | Decision | Basis |
|---|---|---|
| R-1 | **Fractional state (Q12)** in the CR-RC recurrence, in C# (`Waveform`), the Python reference (`rtl/trap_ref.py`) and the RTL (`crrc_shaper.sv`), with coefficients still Q16, sign-extended input shifted by F = 12, output kept fractional through calibration and plotting; accumulator / product widths widened and bounded (not assumed); the **legacy F = 0 path kept** as a regression fixture with its golden values | review: integer 32 keV → 0 codes, 122 keV reads 70–103 keV after a 662 calibration; Q12 bias ≤ 0.4 % |
| R-2 | **Order 4 kept** for all CR-RC presets and stated in metadata / readout; **an explicit per-preset shaping time** separate from the DCR noise window, using the review's common-order convention, labelled a simulation convention, not rig evidence | review; author |
| R-3 | cocotb contract: independently computed expected coefficients per preset (never read back from the DUT, lesson of `c884d53`), actual RTL driven with 32 / 32.1 / 122 / 662 keV finite-rise stimuli, near-threshold sweeps, negative / noisy / full-scale inputs, pinned latency; both legacy and precise paths bit-exact against C# and Python | review contract section |
| R-4 | CR-RC energy readout stays **unavailable** until an isolated-pulse estimator with trigger / phase / overlap behaviour is measured (separate task) | review |
| R-5 | Findings entry with the measured amplitude / resolution tables (current vs Q12 vs candidate) | review measurements |
