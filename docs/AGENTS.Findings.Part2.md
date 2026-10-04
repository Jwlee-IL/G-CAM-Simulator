# AGENTS.Findings — themes 36–63

Part of the results log; the index, the baseline scenario and the theme → EV map are in [AGENTS.Findings](AGENTS.Findings.md). New results go to the end of Part 2.

---

## 36. Thermal drift DURING an acquisition + flood-field correction — `ThermalDrift` / `ThermalDriftStudy` / `montecarlo thermal`

> **Seed ensemble 2026-10-02 — [theme 63](#63-evidence-quoted-over-seed-ensembles--samplesevidence-2026-10-02).** Efficiency 90.62 ± 0.01 %, residual 6.71 %; position RMS 0.48 ± 0.03 (off) / 0.45 ± 0.03 mm (on) — a ~0.5 mm floor, not ~0.6.
First of the "physical realism gaps" queue (Codex gap-review). SiPM gain drift with temperature is a known problem of this camera class. We modelled
only STATIC per-crystal gain; this adds the TIME axis and asks what a per-crystal photopeak-window system loses as
the array's temperature drifts mid-acquisition — and what flood correction can and cannot fix.
- **The physics, no hand-tuning**: SiPM gain ∝ over-voltage = V_bias − V_breakdown(T); V_breakdown rises ≈ +21.5 mV/°C
  (Hamamatsu), so at a few volts of over-voltage **dGain/dT ≈ −0.7 %/°C** (`ThermalDrift.AlphaPerC = −0.007`). A gain
  change of `m(t)` walks the 662 photopeak centroid by `ε = m−1` relative to the FIXED calibration window, so the
  acceptance falls asymmetrically: `½[erf((w−ε)/√2σ) + erf((w+ε)/√2σ)]` (`CrystalUniformity.PhotopeakAcceptance`,
  a strict generalization — ε = 0 reduces to the old symmetric `erf(w/√2σ)`).
- **Two physically distinct temperature drivers** (the user flagged that ambient was missing — a fixed-mount camera (거치형) sees room temperature swings):
  **ambient** (room/HVAC) is spatially UNIFORM → walks every crystal together → a global efficiency droop, no flood
  non-uniformity; **self-heating** has a spatial GRADIENT (centre hotspot) + exponential warm-up → the walk differs
  across the face → a flood-correction RESIDUAL. `T_i(t) = T_amb(t) + ΔT_self·(1−e^{−t/τ})·shape(r_i)`.
- **Flood correction cannot save it, only bias-comp can**: the calibration map `S_cal` is a t = 0 snapshot; the
  acquisition-time sensitivity walks away from it, so dividing by the fixed `S_cal` leaves a residual that GROWS with
  time. A bias-compensation (temperature-compensation) loop that nulls a fraction of α is the only fix. Reusing the
  `UniformityStudy` "flood once, apply per-pixel" trick: the MC geometry flood is run ONCE, every time slice applies
  the analytic `S_i(t) = gain_i · acceptance(fwhm_i, w, ε_i(t))`.
- **Result** (`montecarlo thermal`, ambient +3 °C linear + self-heat +8 °C hotspot τ 0.3, over one acquisition):
  bias-comp OFF droops photopeak counts **100 %→90.6 % (−9.4 %)** and grows the flood residual **0→6.7 % CoV**;
  bias-comp ON (90 %) holds **99.9 %** efficiency, **0.1 %** residual. **Localization barely moves (~0.6 mm, the
  decoder floor) in either case.**
- **Independent confirmation of a prior claim**: the theme-22 note "SiPM thermal drift is an ENERGY-window not a
  position issue" was asserted from design; this MC reproduces it from first principles — drift is an efficiency /
  window-walk problem, and a smooth residual barely pulls the correlation peak. That is exactly why SiPM cameras
  handle it with cooling / bias compensation (an energy-stability problem), not with a position recalibration.
- Flood-field correction (gap #2) was already latent in `UniformityStudy` (÷ calibrated sensitivity recovers static
  non-uniformity perfectly); the honest new content is that it is powerless against the TIME-varying part. (Tests:
  `ThermalDriftTests`, +6.)

## 37. Random-coincidence pile-up SUM continuum in the spectrum — `EventStreamStudy.ApplyPileUp` / `montecarlo pileup`

> **Revised 2026-10-01 — [theme 52](#52-crystal-attenuation-from-tabulated-cross-sections--crystalmaterial-2026-10-01).** Pile-up fractions roughly halve (e.g. 1 Mcps: 5.56 % → 3.26 %) — less of each event's energy is fully absorbed.
Second physical-realism gap. Pile-up already lived in the RTL waveform (the shaper sees overlapping pulses) but NOT
in the per-event ENERGY spectrum — the WPF Cs-137 spectrum had no two-events-summing continuum. The honest fix:
realize the pile-up in the energy domain by MERGING the timed MC event stream, never by hand-adding a tail.
- **The physics, from the real event stream**: `EventStreamStudy` already overlays a Poisson arrival process on the
  MC deposit list (it drives the cocotb shaper). `ApplyPileUp` walks that timed stream and records any events whose
  arrivals fall within the shaper's resolving time as ONE event with SUMMED energy (extending / paralyzable — a burst
  piles to 3-fold+). The energies are the real MC spectrum; only their time coincidence is added.
- **Resolving time is an EFFECTIVE value derived from the shaper**, not a free knob (same discipline as theme 36's α):
  `ResolvingSamples = rise + 2·tail` — the bi-exponential pulse's significant duration (peak + ~2 fall constants).
  Default 1 + 2·5 = 11 samples = 88 ns @ 125 MSPS; a faster shaper (shorter tail) resolves pile-up better, as in
  hardware. It is a shaper-coupled model value (the true pulse-pair resolution also depends on the peak picker,
  threshold and tolerated distortion). The RATE is the operating point (detected cps = emission × efficiency, the
  WPF `_liveRateCps`); throughput ≈ exp(−R·τ) is exact for this fixed-dead-time extending Poisson cluster model.
- **Result** (`montecarlo pileup`, Cs-137): throughput 99.6 %→84.7 % and photopeak-kept 99.3 %→73.4 % as the rate
  climbs 50 k→2 M cps (R·τ 0.004→0.176); the counts migrate UP into a **self-convolution continuum** — a 662+662 →
  1323 keV SUM peak, a 662→1324 plateau (662 + Compton edge), and Compton+Compton lower. Throughput tracks exp(−R·τ).
- **Cross-checked against the analytic pile-up math**: the piled excess above the line matches the AUTOCONVOLUTION of
  the singles energy spectrum (`singles ⊛ singles`) in shape — the leading (2-fold) random-coincidence term, which
  dominates the excess at these rates (R·τ ≲ 0.18, so 3-fold ~ (R·τ)² is sub-%). It is a shape cross-check, not an
  identity: the full recorded spectrum is a 1/2/3-fold cluster mixture. Energy is conserved (merging sums, never
  invents, counts).
- **WPF**: a "Pile-up" checkbox on the Spectrum tab applies the merge to the spectrum path only (the Waveform scope
  keeps singles — it renders its own time-domain pulse overlap), and the energy axis auto-extends past the 2× sum
  peak so the continuum is visible. Ties the spectrum realism to the operating count rate. (Tests: `PileUpTests`, +6.)

## 38. Mask fabrication tolerances — a mis-machined tungsten mask vs an ideal decoder — `MaskFabrication` / `MaskFabricationStudy` / `montecarlo maskfab`

> **Seed ensemble 2026-10-02 — [theme 63](#63-evidence-quoted-over-seed-ensembles--samplesevidence-2026-10-02).** "Floor ~0.95 mm" and "within ~2× the ideal" below are withdrawn. Over 128 seeds the mean RMS is 1.35 (ideal) / 1.52 (40 µm) / 1.93 (80 µm) / 3.65 mm (160 µm); the defined gate **seed-mean RMS ≤ 1.25 × ideal** passes 40 µm (1.13×) and fails 80 µm (1.43×) (D-39). The per-seed 2× test does not separate them (108 vs 100 of 128). PSR 4.38 → 3.77. Conditional on the six fixed patterns.
Third physical-realism gap, user-flagged (★): tungsten is hard to machine, so a real mask is not the ideal MURA the
decoder assumes. The user's key steer: **model the CONSERVATIVE, usable tolerance, not the best-case a machine can
momentarily hit** — "machinable" ≠ "deployable", especially for a thick brittle tungsten slab.
- **`MaskFabrication`** (Masks project) stamps a fixed, seeded per-cell geometry error on the mask Transmit ONLY —
  the decoder keeps decoding the ideal pattern, so it is a forward-model mismatch. Four errors from one tolerance
  scale σ: **hole placement** jitter (σ), **hole size** jitter (0.7σ, its complement is the tungsten web width),
  **blocked cells** (brittle tungsten fails to open, rate climbs with σ), and **depth drill WANDER** (2.5σ — the
  high-aspect-ratio bore is not a clean straight channel through a thick slab; the hole centre drifts front→back, so
  a straight ray is clipped where the bore has wandered away). Wander is injected at the existing slab ray-march
  (`frac` depth), so it needs no new machinery — it is a per-depth hole-rectangle test. Requires a web (holeFraction
  <1, set to 0.71 = ρ≈0.5) for placement/size to have room to move.
- **Study framing = usability THRESHOLD**, averaged over several manufactured masks (fab seeds) so it is the typical
  mask, not one lucky draw. Sweep σ, decode with the IDEAL decoder, measure localization RMS, efficiency, and the
  reconstruction **PSR = (peak−mean)/std** (coded-shadow fidelity — more sensitive than peak/second-sidelobe).
- **Result** (1 mm pitch, 10 mm thick W, aspect ≈ 1:14, centered Cs-137): RMS holds the ideal-mask floor (~0.95 mm)
  to **σ ≈ 40 µm**, then degrades — 1.9 mm at 80 µm, 3.6 mm at 160 µm; efficiency 100 %→88.7 % (blocked cells 0→4 %
  + shrunken/wandering holes); PSR 4.4→3.8. So the **manufacturing requirement is σ ≲ 40 µm** to stay within ~2× the
  ideal floor — a conservative, actionable number, well below the ~1 mm pitch (a few-% of a cell).
- Burrs / rounded corners fold into the hole-size error; slab WARPING (a global bow → position-dependent shift) is a
  distinct structural defect, deliberately deferred. (Tests: `MaskFabricationTests`, +5.)

## 39. Mask–detector alignment / pose error — `CodedApertureMask` pose + `AlignmentStudy` / `montecarlo align`

> **Seed ensemble 2026-10-02 — [theme 63](#63-evidence-quoted-over-seed-ensembles--samplesevidence-2026-10-02).** 1 mm offset → 2.62 ± 0.01 mm bias (2.66 ± 0.04 mm change); 2 mm spacing → 0.38 ± 0.06 mm; 2° roll → 0.13 ± 0.03 mm (N = 64).
Fourth physical-realism gap (HIGH). The decoder back-projects the IDEAL geometry (mask centred at its nominal
plane, unrotated); a rigid-body mis-registration of the real mask casts a systematically shifted/rotated coded
shadow → a SYSTEMATIC localization bias, not just scatter. The other big localization gap after fabrication.
- **Implementation is a single ray transform**: at the top of `Transmit`, express the incoming ray in the mask's
  OWN (nominal) frame — `p_nom = Rz(−roll)·(p − offset)` — so the entire existing slab march (which assumes an ideal
  centred mask at PlaneZ) runs unchanged and composes automatically with focal/taper/fabrication. The decoder,
  still built from the nominal geometry, is unaware of the pose → the mismatch IS the bias. `MaskOffsetX/Y/Z`,
  `MaskRollDeg` on the config; the equivalence (offset mask at p ≡ ideal mask at p−offset; rolled ≡ Rz(−roll)·p) is
  unit-tested directly.
- **Three DOF, three distinct signatures** (swept independently, off-axis source near the FCFOV edge so the leverage
  shows): **in-plane offset** → a parallel bias AMPLIFIED by the magnification (D+S)/D (a 1 mm mask shift → ~2.5 mm
  source bias at D=60/S=100, ref 2.67×) — the dominant, most dangerous error; **spacing (z) error** → a radial
  magnification bias that grows off-axis (~0.4 mm at 2 mm dz); **roll** → a tangential bias that grows off-axis
  (~0.1 mm at 2°, zero on-axis). The aligned baseline sits at the ~sub-mm decoder floor.
- **Tolerance takeaway**: in-plane registration is the driver — the (D+S)/D amplification means the mount must hold
  the mask centre to a fraction of the wanted localization accuracy (≈0.4 mm offset for a ~1 mm bias budget here);
  spacing and roll are far more forgiving at practical off-axis distances. Pitch/yaw TILT (a tilted-slab geometry)
  is deliberately deferred, like warping. (Tests: `AlignmentTests`, +4.)

## 40. Bad (dead / hot) detector pixels + bad-pixel-map repair — `DetectorDefects` / `DetectorDefectStudy` / `montecarlo defects`

> **Seed ensemble 2026-10-02 — [theme 63](#63-evidence-quoted-over-seed-ensembles--samplesevidence-2026-10-02).** Repaired 0.48 ± 0.04 → 0.51 ± 0.03 mm over 0 → 8 % bad pixels (N = 64; was 0.60 → 0.68, an earlier-generator run); raw 1.15 ± 0.12 mm at 8 %. Conditional on the eight fixed maps.
Fifth physical-realism gap (MED-HIGH). We modelled per-crystal gain non-uniformity (theme 36) but not the discrete
channel defects a real SiPM array carries. **Dead** pixels (disconnected/failed channel) punch holes in the flood;
**hot** pixels (dark-count / breakdown runaway) add source-INDEPENDENT spikes — both imprint fixed structure that is
NOT part of the mask code, so the ideal decoder's correlation is pulled off the true peak.
- **`DetectorDefects`** (seeded dead + hot maps; a pixel is at most one kind). Applied post-hoc on the geometry
  flood — the "flood once, apply per-pixel" trick (same as `UniformityStudy`), so no MC re-run per level. Dead →
  count set to 0; hot → +HotFactor×(mean live-pixel counts). The decoder stays IDEAL.
- **Repair = the discrete analogue of flood-field correction**: given the KNOWN bad-pixel map, replace every flagged
  pixel with the mean of its non-defective 4-neighbours (interpolate over the defects). Real systems keep exactly
  such a map from calibration.
- **Result** (12×12 array, hot = 5× mean, averaged over defect maps): the repaired decode holds the ~0.60 mm floor
  flat (0.60→0.68 mm) across 0→8 % bad pixels, while the RAW decode degrades and scatters (up to ~1.2–1.8 mm at 8 %;
  the intermediate points are noisy because a 12×12 = 144-pixel array has only ~1 pixel per %). So bad pixels DO pull
  the localization, and a known bad-pixel map recovers it — the same lesson as flood-field, for discrete defects.
- The 144-pixel grid makes single-% defect counts small and the raw curve seed-noisy; the robust, monotone result is
  the repair holding the floor. (Tests: `DetectorDefectTests`, +4.)

## 41. Mask tungsten fluorescence + Compton scatter — `MaskSecondary` / `MaskSecondaryStudy` / `montecarlo masksec`
Sixth physical-realism gap. The mask was PURE ATTENUATION — a photon hitting a closed cell either leaked through or
vanished. A real tungsten mask emits SECONDARIES: a photon that interacts in the tungsten Compton-scatters or (via
photoelectric absorption above the K-edge) fluoresces a W K X-ray, and some head to the detector.
- **`MaskSecondary`** decides the secondary at an interaction: sample W photoelectric-vs-Compton (log-log table, PE
  9.5 % @662), then either Klein-Nishina scatter (reusing `ComptonModel.Scatter`) or, above the 69.5 keV K-edge, a W
  Kα 59.3 / Kβ 67.2 keV X-ray (yield ω_K 0.958 × K-shell 0.88), isotropic. The X-ray energies are the real W
  characteristic lines — no hand-added line. W μ(E) is extended below 122 keV so the fluorescence self-absorption is
  right (μ(59 keV) ≈ 47× μ(662)).
- **`MaskSecondaryStudy`** is a focused slab MC (it does NOT touch the coded pipeline's Transmit): sample an
  interaction depth in the slab, decide the secondary, require it to head to the detector (exit the back face) and
  survive the escape self-absorption. Output = the arriving-energy spectrum, normalized to the open-cell primary flux.
- **Result** (662 keV on 10 mm W, ~49 % open): escaping secondaries ≈ **9 % of the coded primary flux**, and they are
  almost entirely **forward Compton scatter** (backscatter heads away from the detector). The arriving scatter runs
  ~290→662 keV and PEAKS near the primary — so its high-energy tail sits INSIDE the photopeak window and the energy
  window canNOT reject it (a mildly mis-positioned imaging background); only the lower-energy scatter is rejected.
  (The ~9 % is a back-face hemisphere tally — "exits toward the detector side" — so it is an UPPER bound; a finite
  detector solid angle would collect somewhat less.)
- **W K-fluorescence is negligible at the detector** (< 0.2 % of the secondaries): a 59 keV X-ray in tungsten has a
  ~0.1 mm mean free path, and interactions are front-weighted, so almost none escape the 10 mm slab toward the
  detector. Honest result — the W X-ray lines exist but are self-absorbed away; the real mask effect is the forward
  scatter. (Tests: `MaskSecondaryTests`, +5.)

## 42. Counting-system dead time / count-rate saturation — `DeadTime` / `DeadTimeStudy` / `montecarlo deadtime`

> **Seed ensemble 2026-10-02 — [theme 63](#63-evidence-quoted-over-seed-ensembles--samplesevidence-2026-10-02).** Non-paralysable 912.5 ± 0.7 kcps at 10 Mcps; paralysable 366.4 ± 0.8 kcps at 1 Mcps and 500 ± 140 cps at 10 Mcps (N = 64).
Seventh physical-realism gap, the count-rate companion to pile-up (theme 37): pile-up SUMS overlapping pulses in the
spectrum; dead time is the counting THROUGHPUT loss while the DAQ processes each pulse. Reuses the same timed MC event
stream (`EventStreamStudy`), so the loss comes from the real Poisson arrival statistics.
- **`DeadTime`** applies the two classic models to the stream: **non-paralyzable** (dead for τ only after a RECORDED
  event → m = R/(1+Rτ), saturates at 1/τ) and **paralyzable** (EVERY arrival restarts the dead period → m =
  R·exp(−Rτ), peaks at R=1/τ then collapses — the system paralyses at high rate).
- **`DeadTimeStudy` / `montecarlo deadtime`** sweeps the true rate, applies both filters to the stream, and reports
  the recorded rate + **live fraction (= recorded/true = the live-time-vs-real-time correction)**, cross-checked
  against the analytic formulas.
- **Result** (τ = 1 µs, 1/τ = 1 Mcps): the MC recorded rate sits exactly on the analytic curves. Non-paralyzable
  saturates toward 1 Mcps (912 kcps at 10 Mcps true); paralyzable peaks at 1 Mcps (366 kcps ≈ R/e) then COLLAPSES
  (32 kcps at 5 Mcps, 550 cps at 10 Mcps). Live fraction falls 99 %→9 % (non-para) / 99 %→0 % (para) across R·τ
  0.01→10. The classic dead-time curves, reproduced from the event stream. (Tests: `DeadTimeTests`, +5.)
- **Codex cross-verification** (gpt-5.5, read-only): both filters CORRECT — non-paralyzable `deadUntil` semantics
  and `>=` boundary (live at exactly t+τ), paralyzable `prev`-update-every-arrival and `>τ` boundary, unit chain
  (µs→s→ADC samples), and the τ=0 / empty-stream / extreme-Rτ edge cases all sound. One latent limitation found and
  FIXED: the analytic comparison used the nominal source rate `r`, but if `config.Background` (BSR/DCR) is on,
  `EventStreamStudy` merges extra arrivals the DAQ actually sees — so the study now derives the **effective** arrival
  rate from the generated stream (nTrue over its span) for R·τ, recorded, and analytic. Source-only (the normal case)
  is unchanged to within 1/nTrue; background-on is now correct too.

## 43. Sub-cell peak interpolation — `PeakInterpolation` / `SubCellStudy` / `montecarlo subcell`

> **Seed ensemble 2026-10-02 — [theme 63](#63-evidence-quoted-over-seed-ensembles--samplesevidence-2026-10-02).** Step 1.8 mm: 0.527 ± 0.004 → 0.131 ± 0.002 mm; step 2.4 mm: 0.664 → 0.194 ± 0.002 mm (N = 64).
A precision WIN (not a physics gap): the decoder samples the source plane on a grid of step `ReconStepMm` and
`FindPeak` reported the **integer argmax cell**, so the estimate was quantized to one cell — a deterministic sawtooth
of amplitude ±step/2, **RMS = step/√12, independent of counts**. No amount of statistics beats it; only a finer (more
expensive, O(n²)×pixels) grid did. Interpolating the correlation-peak SHAPE around the argmax recovers a fractional
offset instead.
- **`PeakInterpolation`** (Decoding): separable 3-point estimators returning δ∈[−0.5,0.5] per axis — `Tent` (matched
  to the MURA autocorrelation core, **exact** for an ideal tent: δ=(f₊−f₋)/(2(f₀−f₋))), `Parabolic` (vertex of a
  quadratic; biased toward the cell for a tent — returns u/(2(1−|u|))), `Gaussian` (log-parabola; needs positive
  samples, falls back to parabolic near the ±1 array's negative sidelobes), `None`. All use DIFFERENCES so they
  tolerate the signed sidelobes (unlike a centroid). Wired opt-in on `CrossCorrelationDecoder` + `DecoderConfig`
  (`SubCellInterpolation`, default **Tent**); the decoder default stays `None` so direct-construction tests are
  unchanged; the factory reads the config.
- **`SubCellStudy` / `montecarlo subcell`**: sweeps a point source across the grid in sub-cell steps (Y on-axis) and,
  from ONE high-count (low-noise) mean image per position, decodes with each estimator — lifting the quantization
  sawtooth above the residual Poisson noise — over a range of recon steps.
- **Method choice was decided by the DATA, not a prior** (Codex design consult first flagged tent over Gaussian; the
  MC confirmed and refined it). RMS(mm) vs step, default geometry: interpolation beats the argmax at **every** step,
  and the gain GROWS as the grid coarsens (where the floor is largest) — step 1.8 mm: none 0.52 → tent **0.13 (4×)**;
  step 2.4: 0.66 → 0.19. **Tent is the most robust** (best/tied at 0.6/0.9/1.8/2.4 mm); parabolic edges it only at an
  intermediate 1.2 mm (rounded apex favours the quadratic) and degrades at coarse steps (2.4: parab 0.26 vs tent 0.19);
  Gaussian tracks tent but also degrades coarse. The honest headline: sub-cell interpolation lets you run a COARSE
  (cheap) recon grid and still localize ~3–4× better than the argmax floor. **Gaussian (a common peak-fit choice) =
  parabolic in log-space**; both are matched only to a bump-shaped peak, which this signed-correlation tent is not.
- Regression: defaulting the pipeline to Tent shifted one pinned bias value in `BackgroundTests` by ~0.0005 mm — the
  gradient's low-frequency residual, which the integer argmax hid, is now resolved sub-cell (loosened bit-identical →
  <0.01 mm). (Tests: `SubCellTests`, +9 — estimator math exact-cases + MC floor-beating.)
- **Codex cross-verification** (gpt-5.5, read-only): estimator MATH and decoder WIRING CORRECT — tent derived exact
  for an ideal tent (both u≥0 and u<0 branches), parabolic sign/guard right, Gaussian = log-parabola exact for a
  Gaussian and scale- (not pedestal-) invariant, argmax→estimate axis mapping consistent, no sign/off-by-one. Minor
  fixes applied from its notes: parabolic tent-bias generalized to u/(2(1−|u|)) (was written for u≥0 only; +a negative-u
  test); `Bias*` renamed `Mae*` (it is mean-|error|, not signed bias); "noise-free" softened to "high-count low-noise";
  `SubCellStudy` source depth pinned to the nominal decode plane (was reading `Position[2]`, which could desync the sim
  and decode planes). The "tent most robust" claim is backed by the 5-step MC sweep, not asserted in the unit test
  (which proves only that interpolation beats the argmax and tent beats the floor).

## 44. True (cascade) coincidence summing — `DecayScheme` / `CascadeSummingStudy` / `montecarlo cascade`

> **Seed ensemble 2026-10-02 — [theme 63](#63-evidence-quoted-over-seed-ensembles--samplesevidence-2026-10-02).** No per-seed slope is quoted any more (1.71 ± 0.46 over 128 seeds; ~2.7 of the 7 distances have no sum event and are dropped by the CLI fit). **One pooled Poisson fit over all 896 (seed, distance) rows, zero rows included: slope 2.00 ± 0.03** (seed bootstrap 95 % 1.95–2.06; Pearson dispersion 1.05). The slope the geometry implies for these distances — solid angle of the 14 mm face plus the W(θ) correlation — is 1.99 (2.00 with summing-out at its upper bound): consistent with ε². Sum yield at 18 mm 4.3 ± 0.7 × 10⁻⁶ per decay. `samples/evidence/cascade_fit.py`.

> *Correction (2026-10-02, TODO-14 review):* the angular correlation is not a "~10 % close-geometry correction" — at
> 1 m the correlated ε²-expectation is 11 % above the isotropic one and the effect shrinks only at very close range;
> also `DecayScheme` samples Co-60 isotropically, despite its summary. See [PLAN.Physics.CascadeEmission](archive/PLAN.Physics.CascadeEmission.md).

> **Revised 2026-10-01 — [theme 52](#52-crystal-attenuation-from-tabulated-cross-sections--crystalmaterial-2026-10-01).** Sum/single values fall (18 mm: sum/decay 1.15e-5 → 3.6e-6); the ∝ε² scaling stands (slope 2.04 → 2.16, noisier).
First target from the **physics-realism audit** (5 Codex subsystem audits over themes 1–43; this was the source-audit's
top structural gap): the sim emitted ONE photon per history, so two gammas from the SAME decay were never correlated.
Real cascade isotopes emit coincident gammas that, if BOTH deposit in the crystal, SUM into one recorded event — a sum
peak plus a loss of single-photopeak counts (summing-out). This is the opposite of random pile-up (theme 37, DIFFERENT
decays, ∝ rate): cascade summing is **rate-INDEPENDENT and ∝ ε²** — a geometric effect that grows as the source nears
the camera. (Activates the `EmissionLine.CascadeCoincident` flag that was reserved-but-informational.)
- **`DecayScheme`** models one decay's correlated gammas WITH angular correlation: Cs-137 (single 662, no partner),
  Co-60 (1173+1332 cascade, weak W(θ) → independent isotropic), Na-22 (1275 + a β⁺ annihilation pair of 511s emitted
  **back-to-back**). Each gamma is transported through the crystal with the SAME physics the main detector uses
  (`ComptonModel`: μ(E), photoelectric-vs-Compton, Klein-Nishina), so the sum peak and its continuum are REAL energy
  deposition, not a hand-added line.
- **`CascadeSummingStudy` / `montecarlo cascade`** sweeps source distance (varying ε) and reports single-photopeak vs
  sum-peak yield per decay. **The sum yield scales as the SQUARE of the single yield** — log-log slope **2.04** (Co-60),
  confirming the ∝ε² GEOMETRIC scaling. (A distance sweep alone does NOT separate cascade from random pile-up, which
  also scales ∝ε² in geometry; the true discriminator is that cascade summing is rate/activity-INDEPENDENT per decay.
  Here the pair is same-decay by construction, so this is cascade summing.)
- **Results** (default geometry, ±7 mm detector): Co-60 shows a clean **2505 keV sum peak** sitting ABOVE both Compton
  edges; slope 2.04 = ∝ε². Cs-137 → **exactly 0 summing** (single line — the honest null that confirms the backlog's
  "∝ε² so small" note). Na-22 → the back-to-back 511s can't both reach a one-sided detector, so the **511+511 (1022)
  sum is suppressed** and 511+1275 (1786) dominates; and 1022 sits on the 1275 Compton edge, so it is single-photon
  contaminated anyway. **Honest metric correction found during MC**: a sum window only proves ∝ε² coincidence if it lies
  ABOVE the highest single line (else a single photon's Compton continuum leaks in as a ∝ε event) — the study restricts
  the coincidence metric to sums above max(single line), which turned Na-22's slope from a spurious 1.1 into a true 2.1.
- Magnitude is genuinely small (sum/decay ~1e-5 even at 18 mm), confirming cascade summing is a minor effect for a
  coded-aperture camera's small solid angle — real, ∝ε², but second-order. (Tests: `CascadeSummingTests`, +5 — decay
  scheme correlation incl. back-to-back 511s + MC ∝ε² slope + Cs-137 null.)
- **Codex cross-verification** (gpt-5.5, read-only): physics broadly CORRECT — no sign error, no back-to-back mistake,
  no double-counting; decay schemes/branches, the `t=dist/(−dir.Z)` acceptance geometry, and the clean-sum metric all
  sound. Framing corrections applied: (i) **slope-2 does NOT by itself distinguish cascade from random pile-up** —
  pile-up also scales ∝ε² in a distance sweep; the real discriminator is rate-independence (fixed the "no random
  process reproduces this" overclaim in docs/CLI/tests). (ii) `CrystalDeposit` enters at NORMAL incidence only, so
  absolute yields are approximate (scaling/peak position unaffected) — now documented. (iii) Co-60 W(θ) is A₂≈0.10
  (~10% close-geometry correction), not "few %"; noted as an ignored approximation.

## 45. Mask forward-scatter folded into the coded image — `MaskScatterStudy` / `montecarlo maskscatter`
First target from the physics-realism audit's mask subsystem (audit item B): the mask `Transmit` is binary — a primary
either passes (open cell / straight leakage, full energy) or is absorbed. But a primary hitting a CLOSED tungsten cell
can Compton-scatter FORWARD (lower energy, small angle) and reach the detector as a blurred pedestal/halo AROUND the
true shadow, which the decoder's forward model doesn't expect. Theme 41 tallied that scatter as an escape SPECTRUM (a
hemisphere upper bound); this study folds it into the actual FLOOD by real photon transport and measures the IMAGING
impact + energy-window mitigation.
- **`MaskScatterStudy` / `montecarlo maskscatter`**: biased point source → mask cell → primary OR a transported
  mask-scatter photon (Compton via the shared `MaskSecondary`/`ComptonModel`, self-absorption escape, landing on the
  detector). Decodes THREE variants — primary, primary+scatter, primary+scatter after a ~±10% arriving-energy window
  (a proxy for the detector photopeak window) — over a sweep of the mask–detector gap. Self-contained (does not touch
  the main `Transmit`); the mask is a SIMPLIFIED ideal slab (mid-plane cell lookup, no hole-fraction/taper/fab), and
  deposits go straight into the flood (no crystal stopping-power/resolution). The source proposal covers the whole-mask
  back-projection so the scatter contamination is unbiased.
- **Honest result: mask forward-scatter is a MINOR, doubly-suppressed contaminant.** (1) GEOMETRY: over the mask→
  detector gap a wide-angle scatter drifts laterally off the small detector, so contamination falls from **2.8 % at a
  12 mm gap to 0.21 % at 65 mm** — the nominal geometry's finite-detector value is ~30× BELOW theme 41's ~9 % hemisphere
  upper bound. (2) DECODER: the balanced MURA ±1 array correlates the smooth scatter pedestal to ≈0, so the coded-image
  contrast (peak/secondary) barely moves even with a few-% pedestal.
- **The surviving scatter is the hardest to reject**: what reaches the detector is small-angle forward Compton (mean
  ~613 keV, **~75 % INSIDE a ±10 % window**), because large-angle scatter both down-shifts MORE and drifts off the
  detector. So the window removes only the down-shifted part — **~39 % at a 12 mm gap, falling to ≈0 at wide gaps**
  (where only near-forward in-window scatter survives the drift); the in-window forward tail is irreducible — exactly
  theme 41's point, now quantified as an imaging contamination. (Tests: `MaskScatterTests`, +3 — contamination vs gap,
  window removes the down-shifted part, balanced-decoder pedestal rejection + valid localization.)
- **Codex cross-verification** (gpt-5.5, read-only): geometry/sign conventions CORRECT — mid-plane crossing, front-face
  hit, truncated-exponential interaction depth, back-face escape path, detector landing, KN reuse all sound. One real
  METHODOLOGY bias fixed: the biased proposal originally aimed only at the detector, so mask-scatter from primaries
  whose STRAIGHT path would miss the detector was outside the support (contamination biased). Now the proposal covers
  the whole-mask back-projection → unbiased (contamination refined 3.4→2.8 % at 12 mm). Framing corrected per its
  notes: simplified ideal-slab mask scope, arriving-energy (not measured-detector-energy) window, confidence is an
  internal contrast proxy (not a full SNR), and window-removal is gap-dependent (~39 % close → ≈0 wide), not a flat
  "~25–35 %".

## 46. Scintillator non-proportionality — the intrinsic-resolution COMPONENT from first principles — `NonProportionality` / `NonProportionalityStudy` / `montecarlo nonprop`

> **Seed ensemble 2026-10-02 — [theme 63](#63-evidence-quoted-over-seed-ensembles--samplesevidence-2026-10-02).** GAGG at 662 keV: intrinsic 1.366 ± 0.002 % (~1.4 %), peak shift 0.889 ± 0.001 % (N = 64).

> **Revised 2026-10-01 — [theme 52](#52-crystal-attenuation-from-tabulated-cross-sections--crystalmaterial-2026-10-01).** Resolution components shift by 10–30 % (re-run CSV in `samples/`); the qualitative conclusions stand.
Next physics-realism-audit item (crystal subsystem, item D): the front-end sets the crystal's intrinsic resolution as
a hand-tuned CONSTANT (`FrontEndConfig.IntrinsicResolutionFwhm`, "GAGG ~0.05"). Its real origin is NON-PROPORTIONALITY:
the light yield per keV, nP(E), varies with the depositing ELECTRON's energy. A full-energy gamma deposits through a
Compton CASCADE of electrons whose composition varies event-to-event (a single photoelectron vs a recoil + a
photoelectron + …), so the total light Σ Eᵢ·nP(Eᵢ) fluctuates even though Σ Eᵢ is fixed at the photopeak — THAT is the
intrinsic resolution, with zero photon-counting noise.
- **`NonProportionality`** carries representative literature electron-response curves normalized to 1.0 at 662 keV:
  `NaI` (strong low-energy light deficit), `CsI` (intermediate-energy excess), `Gagg` (comparatively proportional,
  a few %), and `Proportional` (nP≡1). Not spectroscopic-grade — the shapes drive the mechanism.
- **`NonProportionalityStudy` / `montecarlo nonprop`** transports the cascade (the SAME `ComptonModel` the detector
  uses), applies every crystal's nP to the SAME cascade, and reports the intrinsic photopeak FWHM = FWHM(Σ Eᵢ·nP(Eᵢ))
  over full-energy events, per energy. Self-contained (does not change the detector).
- **Results**: the intrinsic resolution EMERGES from the physics and is **exactly 0 for a proportional crystal** (the
  mechanism check — the whole effect is non-proportionality, nothing else) and is **ENERGY-DEPENDENT**, which a constant
  floor cannot be: NaI **2.5 % @122 keV → 0.6 % @1332** (its low-energy deficit shrinks as high-energy electrons
  dominate), GAGG ~0.7–1.5 %, CsI ~2–2.9 %. There is also a non-proportional PEAK SHIFT / energy non-linearity (lines
  land off their true keV because nP is normed to 662; CsI +5 % @122 keV).
- **Honest decomposition**: GAGG's derived electron-non-proportionality is only **~1.5 % at 662** — well BELOW the
  config's hand-set ~3–5 % intrinsic floor. So most of that floor is OTHER (light-collection non-uniformity, Ce
  concentration, SiPM), not electron non-proportionality — the study separates the two. GAGG being comparatively
  proportional is exactly why this camera's crystal is a good choice. (Tests: `NonProportionalityTests`, +4 — response
  curves, proportional→0, energy dependence, GAGG < CsI.)
- **Codex cross-verification** (gpt-5.5, read-only): physics CORRECT — Welford variance, FWHM = 2.355·σ/mean, the
  ÷mean normalization, full-energy identification, and the shared-cascade variance reduction all sound; nP tables sane
  and 662-normalized; proportional→0 by construction. Framing tightened per its notes: the reported FWHM is the
  non-proportionality COMPONENT, not the whole intrinsic floor (already the finding's point, now labelled so in
  study/CLI/records); `PeakShift` is a RAW pre-calibration offset (a real Cs-137 calibration zeroes 662); the 662 CSV
  is the full pulse-height spectrum (photopeak + continuum). One documented SIMPLIFICATION: photoelectric absorption
  deposits the whole remaining energy as one electron (no shell-binding + Auger/X-ray sub-cascade), so the
  photoabsorption-event spread is a slight UNDER-estimate — the derived component is a lower bound.

## 47. Finite source size + capsule self-attenuation — `FiniteSourceStudy` / `montecarlo finitesrc`
Physics-realism-audit item (source subsystem, item E): the source was an ideal mathematical POINT. Real sources are a
finite active pellet in a sealed capsule. Two effects:
- **Source SIZE → reconstruction blur, then washout.** `FiniteSourceStudy.Run` builds the coded flood by transporting
  photons from emission points sampled inside a sphere (diameter swept) and decodes it. The reconstruction peak is the
  point response CONVOLVED with the source's projected profile, so the source blur adds roughly in QUADRATURE (a
  small-blur heuristic): negligible while the source is below the point resolution (≈2.5 mm here), then growing —
  **FWHM 2.50 mm (point) → 2.75 mm at 4 mm dia**.
  The dramatic effect is on CONTRAST (peak/secondary): it falls monotonically (1.18 → 1.09 at 4 mm → ~1.03 at 5–6 mm),
  and once the source approaches the coded resolution the shadow **WASHES OUT** — contrast → 1 (no peak above the
  sidelobes) and localization is lost (bias jumps to several mm). A hard source-size limit, not just a blur.
- **CAPSULE self-attenuation** (`CapsuleSweep`): gammas escaping the pellet + capsule attenuate ∝ exp(−μ·path) with an
  energy-dependent μ. The 662 keV primary mostly survives a few mm of steel (T 0.97 → 0.77 at 4 mm), but a **32 keV Ba
  K X-ray is killed far more** (T 0.61 → 0.014) — so a capsule strips the soft lines from a Cs-137 source's spectrum.
- Self-contained (does not change the main source/pipeline). The contrast (peak/secondary) is the clean washout tracker
  — the FWHM metric itself becomes meaningless once the peak dies (returns NaN on a missing half-max crossing). (Tests:
  `FiniteSourceTests`, +2 — blur→washout via contrast, capsule low-E suppression.)
- **Codex cross-verification** (gpt-5.5, read-only): geometry/transport CORRECT — `RandomInSphere` is volume-uniform
  (dir·radius·∛u), depositing at the detector landing `aim` is right (the source offset blurs the shadow through the
  mask-plane crossing, so no extra shift is needed), the `A·|cos|/(4πr²)` weight is correct, and there is NO
  source-support bias here (direct primaries land at `aim`); `RayExitDistanceFromSphere` and the mean exp(−μ·path) are
  correct. Framing tightened per its notes: the flood is the IDEAL binary zero-thickness coded shadow (not the full
  tungsten-slab response); "quadrature" is a small-blur heuristic (non-Gaussian projected sphere + z-defocus);
  "washout" is THIS argmax decoder's limit, not a fundamental information loss; the capsule sweep is a steel-EQUIVALENT
  normal thickness, not exact spherical-shell transport; and `PeakFwhmMm` now returns NaN (not a truncated width) when
  a half-max crossing is missing.

## 48. MLEM (Poisson-likelihood) reconstruction — `MlemDecoder` / `MlemStudy` / `montecarlo mlem`

> **Seed ensemble 2026-10-02 — [theme 63](#63-evidence-quoted-over-seed-ensembles--samplesevidence-2026-10-02).** Current command: 1.5 × 10⁶ photons, 80 iterations (not 800 k). MLEM resolves 1.5 / 2 / 3 mm pairs and cross-correlation does not in 64 / 64 seeds; valley at 3 mm 0.822 vs 0.170; single-source FWHM **1.93 ± 0.05** vs 2.50 mm (the 1.76 below was an older run); bias 0.87 / 0.55 mm in 64 / 64.
The biggest physics-realism-audit item (reconstruction subsystem, item C): the only decoder was cross-correlation — a
single linear back-projection with the ±1 decoding array. It is fast but leaves NEGATIVE sidelobes, aliases off-axis
sources into a ghost, and MERGES nearby sources into one blurred peak that its argmax then picks. `MlemDecoder` adds
the statistical alternative: iterate the physical FORWARD model to the source distribution λ ≥ 0 that maximizes the
Poisson likelihood of the detector counts, `λ_j ← (λ_j/s_j)·Σ_i A_ij·y_i/(Σ_j' A_ij' λ_j'+b_i)`, where the system
matrix `A_ij` is 1 when the ray from source cell j to detector pixel i crosses an OPEN mask cell (the true finite
aperture as the forward model).
- **`MlemDecoder` (Decoding)** implements `IDecoder`, builds/caches the system matrix from the same geometry the
  cross-correlation decoder uses, and iterates MLEM. Opt-in — the pipeline still defaults to cross-correlation.
- **`MlemStudy` / `montecarlo mlem`** decodes the SAME coded floods both ways and compares. Headline TWO-SOURCE
  SEPARATION: MLEM deconvolves a close pair into two non-negative peaks where cross-correlation shows one merged blob.
  **MLEM cleanly splits 2–3 mm pairs cross-correlation merges** (min resolvable ~2 mm vs ~3.5 mm), on the default
  geometry (point resolution ≈2.5 mm). At 3 mm the MLEM valley depth is 0.82 (two clear peaks) vs cross-correlation's
  0.17 (merged). (The metric is truth-centred, so its deepest-separation ~1.5 mm number is optimistic; the robust,
  test-backed claim is the 2–3 mm split.)
- **Single-source**: MLEM is **NON-NEGATIVE** (min recon 0.00) while cross-correlation dips to −103 (negative
  sidelobes), and MLEM deconvolves to a **sharper peak** (FWHM 1.76 mm vs 2.50 mm) at comparable localization bias
  (0.87 vs 0.55 mm). The cost is iteration and the usual MLEM resolution/noise trade-off with iteration count.
- Self-contained (builds both decoders + floods from the config geometry; the main pipeline is untouched). (Tests:
  `MlemTests`, +2 — non-negativity & sharpness vs sidelobes, resolves closer pairs than cross-correlation.)
- **Codex cross-verification** (gpt-5.5, read-only): the MLEM CORE is CORRECT — standard zero-background ML-EM update
  (forward-project, ratio, back-project, ÷ sensitivity, multiplicative), `A[j·nDet+i]` indexing consistent, flat λ=1
  start fine, and the system-matrix ray geometry MATCHES the flood generator (no sign/transpose/mirror/index bug).
  Framing tightened per its notes (no code change): `A_ij` is a binary pixel-CENTRE sample (no pixel-area / cos·r²
  integration) — a good approximation, not an exact forward model; the ghost-suppression benefit needs the FINITE
  forward model (`Cyclic = false`) and is a capability, not what the default cyclic demo exercises; the ~1.5 mm number
  is optimistic (truth-centred metric windows overlap below ~2 mm) so the robust claim is the 2–3 mm split; and the
  sharper-peak / closer-resolving results are iteration- and count-dependent (an ideal high-count study, not a general
  camera resolution limit).

## 49. Thermal DCR / PDE readout effects — `ThermalDrift` (extended) / `ThermalReadoutStudy` / `montecarlo thermalro`
Physics-realism-audit item G (readout): the thermal model (theme 36) mapped temperature only to the SiPM gain
(photopeak-centroid drift). It missed the DARK-COUNT and PDE consequences. `ThermalDrift` gains `DcrFactor(ΔT) =
2^(ΔT/doubling)` (silicon dark generation ≈ doubles every 8–10 °C) and `PdeFactor(ΔT)` (PDE follows the over-voltage,
≈ −0.2 %/°C). Key physical point: bias compensation nulls the GAIN drift but does NOT null the thermal dark
generation, so **DCR keeps climbing with temperature even under a comp loop**.
- **`ThermalReadoutStudy` / `montecarlo thermalro`** sweeps ΔT and, via `FrontEndModel`, reports DCR, the low-line vs
  photopeak resolution, and the PDE factor.
- **Result**: DCR rises **×13 over 30 °C** (2× / 8 °C). Its parallel-noise term is ∝1/E, so it degrades the **low line
  (60 keV) ~2× more (relatively) than the 662 keV photopeak** — but only **modestly in absolute terms** (60 keV
  10.1 %→10.4 %, 662 keV 4.30 %→4.36 %) because for this high-light-yield crystal the resolution is
  STATISTICS-dominated, not DCR-dominated. (The ∝1/E is the DCR TERM; the total FWHM is its quadrature sum with the
  statistics and intrinsic floor, so the total is not itself 1/E.) PDE droops −6 % over 30 °C. **Honest headline: the
  dominant thermal-readout effect is the raw DCR growth itself (a dark avalanche / pile-up load, cf. dead time theme
  42), not a resolution hit — and the 662 photopeak (this camera's line) is nearly immune, so the gain-centroid drift
  of theme 36 stays the main thermal concern for Cs-137 imaging.** (Tests: `ThermalReadoutTests`, +2 — DCR doubling &
  comp-immunity, 1/E low-E degradation + PDE droop.)
- **Codex cross-verification** (gpt-5.5, read-only): no implementation bug — `DcrFactor = 2^(ΔT/8)` and the −0.2 %/°C
  PDE droop are physically reasonable, bias compensation correctly affects gain/PDE but NOT the thermal DCR term, and
  `R_dcr = 2.355·√(ENF·DCR·τ)/npe` with npe∝E gives the ∝1/E term consistently. Clarified per its notes: the ∝1/E is
  the DCR TERM (not the total FWHM), and `dark_trigger_kcps = DCR/1000` is the raw dark avalanche-load proxy, not a
  modelled discriminator false-trigger rate.

## 50. Depth-of-interaction (DOI) parallax in reconstruction — `DoiParallaxStudy` / `montecarlo doi`

> **Seed ensemble 2026-10-02 — [theme 63](#63-evidence-quoted-over-seed-ensembles--samplesevidence-2026-10-02).** At 9 mm off axis: 0.093 ± 0.002 mm (5 mm crystal), 0.299 ± 0.001 mm (30 mm); on axis |shift| < 0.01 mm (N = 64).
Physics-realism-audit item F (decoder vs detector response): the decoder back-projects the pixel-CENTRE front-face
crossing, but a gamma interacts at a random DEPTH in the crystal. For an OBLIQUE (off-axis) ray the scintillation
centroid the detector reads is displaced from the front face by depth·tan(incidence) — a parallax the centre-back-
projecting decoder cannot correct. `DoiParallaxStudy` isolates it by building the SAME rays' flood twice — deposit at
the front-face crossing (no DOI) vs at the DOI-displaced interaction point — and comparing the decoded position.
- **Result**: the DOI localization SHIFT is **0 on-axis** (by symmetry the displacement averages out — individual rays
  still have DOI blur; it is the signed shift that vanishes) and grows off-axis WITH crystal thickness (deeper
  interactions → bigger parallax): at 9 mm off-axis it is 0.10 mm for a 5 mm crystal → **0.30 mm for a 30 mm crystal**.
  Sub-mm here because the far source (160 mm) gives a small (~2–4°) obliquity, but it is a real, systematic floor —
  comparable to the recon grid step (0.39 mm) and thus a genuine limit UNDER the sub-cell interpolation (theme 43) at
  the FOV edge with a thick crystal, uncorrected by the decoder.
- Because the shift is often sub-grid-step, the raw argmax quantizes it to 0 until it crosses a cell (one 0.88 mm
  argmax jump in the sweep is quantization behaviour, not smooth DOI scaling) — which is exactly why it matters only
  against the SUB-CELL precision, not the coarse grid. Self-contained. (Tests: `DoiParallaxTests`, +1 — zero on-axis,
  grows with thickness off-axis, sub-mm.)
- **Codex cross-verification** (gpt-5.5, read-only): geometry CORRECT — the lateral displacement `d_xy/|d_z|·depthZ`
  reduces to `d_xy·pathIn` (correct along-ray displacement), and the paired-flood (same rays, DOI on vs off) is a valid
  isolation. Framing tightened per its notes: "zero on-axis" is the SIGNED shift by symmetry (not zero per-ray DOI
  blur); the FWHM columns are a fragile reference (the metric NaNs off-axis, so only the SHIFT is asserted); the 0.88 mm
  row is argmax/quantization behaviour, not physical DOI scaling.

---

## Post-theme-50 follow-ons (2026-07-20/21) — WPF nuclide separation, Compton strip, CR-RC fix

Enhancements on top of the 50-theme base (WPF = theme 34–35, stripping = theme 16–17, CR-RC = theme 33), each
Codex-cross-verified and headless-tested (the WPF GUI can't be tested here, so the underlying physics is proven in
the `Gcam.Simulation`/`Gcam.Detector` layer and the app is verified to compile).

- **Nuclide separation IN THE RECONSTRUCTED IMAGE** (`Gcam.Wpf/{Scene.cs, MainWindow.xaml.cs}`): the imaging tab
  decoded ONE flood through a single window locked to `scene[0].Lines[0]`, so co-measured isotopes separated in the
  SPECTRUM but not the IMAGE. Now **one imaging channel per distinct isotope**, each windowed on its own primary line,
  decoded with the shared geometry-only decoder; per-nuclide recon **normalized + isotope-coloured composite** on the
  scene overlay; per-channel accumulation at each channel's own detected rate; `_floodAccum` is the COMBINED flood so
  depth/autofocus see all sources. Recon heatmap **flipped +y-up** (ScottPlot draws grid row 0 at top) to match the
  canvas + markers; **found peaks labelled by nuclide**. Test `PerNuclideWindow_SeparatesCsFromNa_InTheImage`
  (662 window → Cs, 511 window → Na). Co-60 stays physics-limited (1+ MeV punches through).
- **Compton stripping toggle** (co-located isotopes): per-pixel `max(0, low − Σ R·high)`, `R` calibrated per
  isotope-pair in `PrepareLive` from an isotope-only run (pair gated on the contaminant's MAX emission line, not its
  channel centre — so Na-22's 511 channel strips a Cs 662 window via its 1275 line). One-pass scalar model (matches
  the CLI `mixedstrip`): exact for a clean pair, approximate for 3+ overlap (a per-pixel response-matrix solve is the
  optional-polish full fix). Test `ComptonStripping_RecoversCoLocatedCsCount`.
- **CR-RC⁴ constant fix**: the RTL default + cocotb param carried the wrong `A_Q16 = 53667`; the correct value is
  `round(e^-0.2·65536) = 53656` (the C#/Python formulas). Realigned `crrc_shaper.sv` / `run_cocotb.py` /
  `trap_ref.py` — **C# ↔ RTL CR-RC is now genuinely bit-exact** (7 cocotb re-run green). The cocotb test read A from
  the DUT, so it was self-consistent and had masked the drift. **Closed 2026-10-01:** `test_crrc.py` now asserts the
  DUT's `A_Q16`/`K_Q16` equal the math (re-injecting 53667 fails 2 of 3 CR-RC tests), `run_cocotb.py` exits non-zero
  on any failure (the cocotb runner does not, outside pytest), and `WaveformTests.Blr_MatchesGolden` holds the C#
  baseline restorer to the same reference — so all three shaper paths are now C# ↔ RTL bit-exact under test, in CI.
- **Mask μ(E) extended < 122 keV** for soft lines (Am-241 59.5 → ~47×); documented approximation (K-edge above-edge
  region + < 50 keV clamp — no isotope line there, thick mask opaque regardless). Test `MaskAttenuationTests`.
- Docs: `docs/PAPER.ko.md` (+ rendered artifact) — a two-tier (expert/plain) physics & sweet-spot note covering
  §1–9 incl. in-crystal Compton transport, dead region, crosstalk, hardware realization (RTL/cocotb/FPGA), and
  productization; Codex physics-verified (6 wording/number fixes applied).

## 51. Ir-192 (industrial radiography) + tungsten μ(E) between 200 and 600 keV — `Isotopes` / `samples/isotopes/ir192.json` (2026-10-01)

> **Revised 2026-10-01 — [theme 52](#52-crystal-attenuation-from-tabulated-cross-sections--crystalmaterial-2026-10-01).** The efficiencies quoted below were from a run that used only the 316.5 keV line (a single `source` ignored its `lines` — fixed in theme 52); with all nine lines and the corrected crystal: on-axis 2.05e-4, error 0.3 mm, ghost margin 1.15. The open crystal item is closed by theme 52.

Ir-192 is reference source RS-1 of the locator concept (3.7 TBq radiography source) and the only reference source
the engine could not run. Added from ENSDF (C. M. Baglin, Nucl. Data Sheets 113, 1871 (2012)) via IAEA LiveChart and
NNDC NuDat — the same evaluation, checked line by line — and cross-checked by a Codex read-only review against an
independent decay-data table: the nine gammas ≥ 1 % per decay (316.5 / 468.1 / 308.5 / 296.0 / 604.4 / 612.5 / 588.6
keV β⁻, 205.8 / 484.6 keV EC), **2.137 γ/decay**, T½ 73.829 d. Pt / Os K X-rays (61–78 keV) are left out.

- **Tungsten μ(E) table was 2–7 % too opaque at the Ir-192 lines** (+4.5 % at 316 keV, +7.3 % at 206 keV): it
  jumped 250 → 400 → 662 keV and log-log interpolation overshoots the curved photoelectric fall-off. Added NIST
  (Hubbell & Seltzer) points at 200, 300, 400, 500, 600 keV (ratios to μ(662) = 0.09852 cm²/g); now within 0.5 %
  over 200–662 keV. Both copies (mask transmission, mask secondaries) updated. Effect on earlier themes: none
  measurable — no earlier line sits in 250–600 keV except Na-22's 511 (−3.6 % μ), and all 156 earlier tests pass
  unchanged. The iron entrance-window table was already within 0.3 % of NIST and is unchanged.
- **Cascade summing is not modelled for Ir-192** (real cascades exist, e.g. 468 → 316 keV). The preset says
  `cascadeCoincident: false` — the RTL study's notion of a cascade is "every line in one decay", which would invent
  coincidences between alternative branches — and `DecayScheme.From("Ir-192")` now throws instead of silently
  running the Cs-137 cascade it used to fall back to.
  Since 2026-10-01 (later the same day) every name without a modelled cascade throws — Co-57, Am-241, a typo or a
  blank name used to fall back to Cs-137 the same way (found by a rule review of `AGENTS.Rationale`).
- **Imaging works** (`samples/scenario_ir192.json`, `montecarlo`): on-axis estimate (0.3, −0.2) mm, 0.3 mm error,
  efficiency 1.96 × 10⁻⁴, ghost margin 1.15 — the same scenario with Cs-137 gives 0.4 mm, 2.49 × 10⁻⁴ and 1.16, so
  imaging quality matches; the efficiency gap is not interpreted here because the crystal stopping model is wrong at
  these energies (last bullet). `montecarlo compton` with the 316.5 keV line: 69 % in-window,
  0.32 mm RMS. In the Studio scene (1 m standoff, ~3 mm recon grid) Ir-192 localises inside one grid cell, no worse
  than Cs-137 at the same position, with and without a ±10 % window on 316.5 keV (`Ir192Tests`).
- **Open — crystal attenuation is not right at Ir-192 energies.** (a) The default `CrystalDetector` uses ONE μ (the
  662 keV value) for every energy: for 15 mm GAGG it stops 55 % at any energy, where NIST gives ~81 % at 316 keV, so
  Ir-192 count rates come out ~1.5× low. (b) `ComptonModel.MuRel` = (661.7/E)^1.56 for every material deviates from
  NIST GAGG by +54 % at 316 keV, +30 % at 468 keV and −48 % at 1332 keV. The RS-1 count-rate comparison (TODO-01
  step 8) waits for that fix; it is tracked as TODO-02.

## 52. Crystal attenuation from tabulated cross sections — `CrystalMaterial` (2026-10-01)

Found while adding Ir-192 (theme 51): the crystal physics was not material data. `CrystalDetector` stopped every
energy with the 662 keV μ; `ComptonModel` used one power law for μ(E) and one curve for the photoelectric share, for
every crystal; and the Compton paths defaulted to μ(662) = 0.09 /mm, which matches no preset (GAGG is 0.051).
Against NIST/xraylib for GAGG the old curves were off by +54 % (μ, 316 keV) and the photoelectric share by 2.7×
(0.35 vs 0.13 at 662 keV) — the modelled crystal was far more absorbing than real GAGG.

**Fix.** `CrystalMaterial`: μ/ρ(E) (photo + Compton) and the photoelectric fraction per preset (GAGG, GAGG:Ce,Mg,
CeBr3, LaBr3, LYSO, BGO, NaI), 20–3000 keV with points straddling every K edge, from xraylib 4.3.0 to 800 keV and
Klein–Nishina above (`samples/materials/gen_crystal_tables.py`). Coherent scattering is left out on purpose (no
energy deposit); pair production is negligible below 1.5 MeV. Both detectors and the cascade / non-proportionality
studies use it; the configured μ(662) stays the anchor where a config sets one, otherwise the material's own value.
Preset anchors were set to the physical values (they were 7–17 % off). Unknown or "ideal" materials use GAGG.

**What changed (same commands, same seeds; before = the old model on the same code):**

| Study | Before | After |
|---|---|---|
| `compton`: PerPixelWindow efficiency / RMS / fail | 24 % / 0.83 mm / 1 % | 6 % / 5.65 mm / 46 % |
| `compton`: Co-60 share of the 662 window | 22 % | **55 %** |
| `compton-strip`: R; true Cs; stripped error | 0.150; 93; 0 % | 0.469; 30; 0–4 % |
| `mixediso` (Co ×8): Cs error | 0.47 mm | **8.75 mm (lost)** — holds at Co ×2, fails from ×4 |
| `mixedstrip`: R; true Cs; stripped error | 1.16; 12; 0–1 % | 3.97; 3; 0–11 % |
| `cascade` (18 mm): sum / decay; ε² slope | 1.15e-5; 2.04 | 3.6e-6; 2.16 |
| `pileup` at 1 Mcps: pile-up fraction | 5.56 % | 3.26 % |
| `eventstream`, `deadtime`, `frontend` | — | unchanged |
| Material presets (efficiency, 10 mm) | NaI 0.67 … BGO 1.17e-4 | NaI 0.59 … BGO 1.23e-4, same ranking |
| Handheld head, Cs-137 (MC vs URS analytic 2.44e-4) | 2.65e-4 (+8 %) | 2.48e-4 (+2 %) |

**What it means.** The spectral lever survives: per-pixel stripping still recovers a co-located Cs count. The
spatial lever is weaker than reported: through a 662 keV window in 1 mm GAGG pixels, Co-60 downscatter is the
majority of the window, and the Cs peak is lost once Co-60 is ~4× stronger. Fewer full-energy single-pixel events
also mean far fewer counts per photon, so localisation at a fixed photon budget degrades (the RMS / fail rates
above). The earlier, better numbers rested on a crystal that absorbed too much and too photoelectrically.

**Ir-192 count rate (TODO-01 step 8).** Per emitted photon in the theme-22 head: Ir-192 (all nine lines)
2.62e-4 vs Cs-137 2.48e-4; the URS's analytic estimate (662 keV stopping for every line) is 2.44e-4, so it
under-states Ir-192 by ~7 %: RS-1 ≈ 1.07 Mcps at 10 mSv/h and ≈ 1.07 kcps at 10 µSv/h (was 1.0 / 1.0).

**Also fixed.** A single `source` with `lines` silently emitted only its `energyKeV` (multi-line emission worked only
in `sources[]`) — it affected `scenario_co60.json` (used only for its isotope name by `cascade`) and the first
Ir-192 scenario (theme 51 numbers corrected above). Test `SingleSourceWithLines_EmitsAllItsLines`.

**Note on sample CSVs.** Before this change, re-running `compton` / `mixediso` / `mixedstrip` / `compton-strip` on the
unmodified code already produced different CSVs from the committed ones (e.g. bias 0.354 vs 0.550 mm): those
committed outputs were from an older code state. They are regenerated here.

Tests: `CrystalMaterialTests` (NIST mixture, K-edge step, photoelectric ordering by Z), two tests re-stated to the
measured physics (theme 28 angular result; mixed-field separation at Co ×2), `SingleSourceWithLines_EmitsAllItsLines`.

**Addendum — PRS rows re-measured (TODO-03, 2026-10-01).** The PRS rows marked † quoted runs made before this theme.
Same commands on the current code:

| PRS row · command | Before | Now |
|---|---|---|
| PR-IMG-02 · `noise samples/scenario_handheld.json` | floor 0.34 mm; sub-mm at ≥ 250 counts on axis, ≥ 500 at the 8 mm edge | floor **0.24 mm** on axis, 0.53 mm at the edge; sub-mm at ≥ 250 (0.26 mm) on axis, ≥ 500 (0.55 mm) at the edge |
| PR-IMG-03 · `mixedfield samples/scenario.json` | Cs-137, Co-60, Co-57 each < 1 mm | unchanged — 0.55 / 0.27 / 0.93 mm, the same peak positions (only the peak heights moved) |
| PR-NRG-03 · `compton samples/scenario.json` | Argmax 38 % vs per-pixel 24 % of the ideal counts; RMS 0.74 vs 0.78 mm | Argmax **12 %** vs per-pixel **6 %** (2.0×); RMS 3.51 vs 5.65 mm, fail 16 % vs 46 % at the 400-photon budget |
| PR-SENS-01 ratio · `noise` on `scenario_handheld` / `scenario_orig_gagg` | 2.65e-4 / 1.08e-4 = 2.45× | 2.48e-4 / 1.00e-4 = **2.48×** |
| PR-IMG-01 context · `sweep samples/scenario_handheld.json` | localised 171 (cyclic) / 360 (non-cyclic) of 625 | 172 / 360 |

- **The lower floor is not from this theme.** The pre-theme-52 code (`af9bc96`) gives the same RMS values (0.24 mm handheld,
  0.38 mm GAGG reference geometry) with the old efficiencies. The committed `noise_*.csv` dated from 2026-07-10, before theme 43 made
  Tent sub-cell interpolation the pipeline default; the GAGG reference geometry's floor moves 0.55 → 0.36 mm for the same reason.
- **Argmax gains more with the physical crystal.** Fewer events are single-site full-energy in real GAGG, so recovering
  the Compton-split ones doubles the counts (was 1.6×). At a 400-photon budget only ~50 / ~25 counts remain, which is why
  neither strategy reaches sub-mm here — the comparison, not the absolute RMS, is the result.
- Regenerated: `noise_handheld.csv`, `noise_orig_gagg.csv`, `sweep_{cyclic,noncyclic}.csv` (handheld), `mixedfield*.csv`.

## 53. Field of view at field distance + out-of-field cue — `FieldOfViewStudy` / `montecarlo fov` (2026-10-01)

> **Seed ensemble 2026-10-02 — [theme 63](#63-evidence-quoted-over-seed-ensembles--samplesevidence-2026-10-02).** Thresholded angles over 64 seeds (frequencies in EV-02): x, 1 m, N0 500 + background **5.0°** in 56 / 64 (the 4.0° below occurred in 6 / 64); 5 m cyclic at N0 500 3.5° in 48 / 64; diagonal 5 m N0 5000 7.5° in 40 / 64 or 4.5° in 24; unflagged wrong spots ≤ 7 % without background (N0 500), up to 29–49 % with background equal to the signal.

TODO-04 / LIM-01 / D-16: the adopted narrow-FOV path relies on (1) the usable field of non-cyclic decoding, estimated at
±5° from the lab-geometry area ratio, and (2) an out-of-field cue. Neither had been simulated at field distance.

**Set-up.** Theme-22 head (`scenario_handheld.json`, theme-52 crystal), Cs-137, source at S = 1 m and 5 m, moved
0–20° off axis along x and along the diagonal. Fully coded field ±3.64° along x (square: ±5.14° on the diagonal),
resolution element 1.04° (= success threshold). Per angle: biased MC mean flood, then 100 Poisson realizations of a
source of FIXED strength — N0 = 500 or 5000 counts if it were on axis, N0·ε(θ)/ε(0) here — with no background or a
uniform ambient pedestal equal to N0 (BSR 1). Each realization is decoded non-cyclically over ±20° and cyclically
over its one period; the left/right cue is read from the decoded peak and from the flood centroid; the "outside"
flag from the centroid uses the 95th percentile of the in-field centroid offsets of the same series as threshold.

**Results** (`samples/fov.{csv,png}`; panel numbers refer to the plot, S = 1 m along x unless stated):

| | N0 5000 | N0 500 | N0 500 + BSR 1 |
|---|---|---|---|
| usable half-field, non-cyclic (≥ 90 % within 1.04°), x · 1 m / 5 m | **7.0° / 7.5°** | 6.0° / 7.0° | 4.0° / 6.5° |
| same, cyclic | 3.0° / 3.5° | 3.0° / 3.0° | 3.0° / 3.0° |
| same, non-cyclic, diagonal · 1 m / 5 m | 9.0° / 4.5° | 4.5° / 3.0° | 0.5° / 0.5° |
| side by centroid ≥ 95 % | 1.0–14.5° | 1.5–14.0° | 1.5–13.0° |
| "outside" flag by centroid ≥ 90 % | 4.0–12.5° | 6.0–11.5° | never (peaks 68 % at 9.5°) |

- **Usable field ≈ ±7° along x** — wider than the ±5° estimate, and the same at 1 m and 5 m (the field is set by angle,
  not distance). With background it shrinks to ±4–6.5°. On the DIAGONAL at low counts the wide non-cyclic search is
  *less* reliable than the cyclic one even inside the field (N0 500 + BSR 1: 0.5° vs 2.0°): searching ±20° gives the
  noise far more places to win. A usable-field claim therefore needs a count level and a direction.
- **Beyond ~7.5° the non-cyclic decode answers with a wrong spot inside the field** (50–90 % of acquisitions at
  7.5–11°, median error ~7°) — the partially coded source is not suppressed, it lands elsewhere. Non-cyclic decoding
  moves the ghost boundary from 3.6° to ~7°; it does not remove it (LIM-07).
- **The flood centroid catches those answers.** The aperture's shadow moves away from the source and the 10 mm front
  plate only leaks ~17 %, so the lit side of the array is opposite the source: the centroid gives the correct side
  from ~1° to ~14.5° and, at N0 ≥ 500 without background, flags "outside" in 95–100 % of the 7.5–12° acquisitions —
  the wrong in-field answers it misses stay ≤ 3 % there (panel 4). With background equal to N0 the flag weakens
  (≤ 68 %) and 10–45 % of the 5–12° wrong answers go unflagged.
- **Past ~14°, nothing tells the direction.** The aperture's shadow leaves the array at atan((7 + 8)/55) ≈ 15°; what
  reaches the detector is the plate leak (relative efficiency flattens at ~0.27), the side cue falls to chance, and
  the argmax still returns an in-field spot in ~60 % of acquisitions. That is the "is there a source at all" decision
  (PR-IMG-07), which this study does not implement — the decoder always reports its highest peak.
- **What it means for the adopted path.** (1) Quote the usable field as ±6–7.5° along the axes without background
  (±7–7.5° at N0 5000, ±6–7° at N0 500 — corrected 2026-10-01: the table above gives 6.0° / 7.0° at N0 500), ±4–6.5° with background equal to the signal. (2) The out-of-field cue works from the flood
  alone between ~4° and ~12–14° and is what makes non-cyclic answers past 7° safe to reject; it needs a background
  estimate to keep working in a real ambient field. (3) Beyond ~14° the device is blind to direction: the hand sweep
  (IMU / VIO) has to bring the source within ~14° before any cue exists, i.e. pointings ≲ 28° apart.

Caveats: one isotope (662 keV); the front plate is modelled as an infinite 10 mm W slab around the mask (no side
walls, so sources past ~20° are outside the model); the background is a flat pedestal; the centroid threshold is
calibrated on the same series (in-field false alarms ≈ 5 % over the pooled in-field samples by construction).
Codex review (read-only) found and we fixed: process-randomized RNG seeding (`HashCode.Combine`), an approximate
angular metric, the square field treated as a circle on the diagonal, and missing guards (negative angles, empty
calibration, zero efficiency).

Reproduce: `montecarlo fov samples/scenario_handheld.json` (≈ 3 min) → `samples/fov.csv`; `python samples/plot_fov.py`
→ `samples/fov.png`. Tests: `FieldOfViewTests` (square field, non-cyclic beyond the coded field, centroid side and
flag at 10°, no cue at 18°, reproducibility, argument guard).

## 54. Dose rate from the detector spectrum — `DoseStudy` / `montecarlo dose` (2026-10-01)

> **Seed ensemble 2026-10-02 — [theme 63](#63-evidence-quoted-over-seed-ensembles--samplesevidence-2026-10-02).** Frontal ±13 % holds in 63 / 64 seeds (largest deviation 12.5 ± 0.3 %, at 250 keV); live-time corrected Cs-137 0.91 (± 0.003). Every ratio's seed spread ≤ 0.004.

TODO-05 / PR-SAFE-01 / PR-SENS-05 / D-21: the dose-rate function is required (up to 10 mSv/h within ±50 % over
60 keV – 1.33 MeV, NSS-1) but the engine had no dose model. A scintillator meter weights its pulse-height spectrum
with a function G(E) so that Σ G(Eᵢ)·Nᵢ / t tracks H*(10); the simulator knows the truth, so it can say how well that
works for this head.

**Model.**
- **Truth:** the unscattered fluence at the detector position × ICRP 74 Table A.21 H*(10)/Φ (log–log; checked against
  the table as cited by SC&A/CDC 2019 and by Codex). H*(10) is defined in the field without the instrument, so the
  truth is free-in-air.
- **Response:** biased MC of a point source 1 m away through the mask and the Compton crystal. Every scored event's
  total deposit (the whole array, as a dose channel would sum it) is smeared by the energy resolution (7 % at
  662 keV, 1/√E), cut at a 30 keV LLD and histogrammed (5 keV bins) per unit fluence.
- **G(E):** Σₖ aₖ·(ln(E/662))ᵏ, k ≤ 4, least squares on the relative error over 14 frontal energies (50–1500 keV).
  It is then checked, without refitting, on other energies, the reference sources' line mixtures, oblique incidence
  and a paralyzable front end (τ = 1 µs, theme 42).

**Results** (`samples/dose_{ratio,g,overrange}.csv`, `samples/dose.png`):

| | Estimate / truth |
|---|---|
| Frontal, fit set 50–1500 keV | 0.91–1.09 |
| Frontal, held-out 70 / 122 / 250 / 662 / 1173 / 1332 keV | 1.05 / 0.90 / 1.13 / 0.91 / 1.04 / 1.07 |
| Reference sources, frontal: Am-241 / Co-57 / Ir-192 / Cs-137 / Co-60 | 1.06 / 0.91 / 1.00 / 0.90 / 1.05 |
| 662 keV at 0 / 2.5 / 5 / 10 / 20 / 45° | 0.91 / 0.82 / 0.66 / 0.40 / 0.22 / 0.08 |
| 60 keV at 0 / 2.5 / 5 / 10 / 20° | 1.07 / 0.79 / 0.44 / 0.11 / 0.00 |
| Cs-137 at 1 mSv/h / 10 mSv/h / 100 mSv/h, raw | 0.86 / 0.53 / 0.004 |
| same, live-time corrected | 0.90 / 0.90 / 0.90; fails past ~150 mSv/h (live fraction < 10⁻³) |

- **Frontal dose works.** One G(E) keeps every energy and every reference source within ±13 % — well inside ±50 %.
  Counts per µSv (frontal): Am-241 2.0 M, Co-57 1.38 M, Ir-192 343 k, Cs-137 193 k, Co-60 104 k.
- **Off axis it does not, and that is the head's design.** The 10 mm mask with 1 mm cells is a collimator
  (acceptance ~atan(1/10) ≈ 6°): 10° off axis the head reads 0.11 (60 keV) to 0.66 (1250 keV) of the dose, and beyond
  20° low-energy fields read ~0. A user standing in a field from the side gets a reading far below the dose where
  they stand. NSS-1 tests frontal incidence only, so a frontal type test would pass while the field reading fails —
  the imaging channel cannot be the dose-rate channel. The obvious fix (an unshielded small dose sensor, or a
  side-looking crystal) is a design decision for the VV owner, not taken here.
- **Over-range.** A paralyzable front end under-reads from ~1 mSv/h (−5 %) and reads 0.53 of the dose at 10 mSv/h —
  inside ±50 % only because the fit error happens to be small. Live-time correction (recorded / live fraction)
  restores the reading up to ~150 mSv/h; past that the live fraction falls below what a live-time clock can resolve
  and the reading collapses to zero. The live fraction itself keeps falling monotonically, so it is the over-range
  signature: show "over range" when it drops below ~10⁻³ instead of a number. With that, PR-SENS-05's "no
  under-reading from 10 mSv/h to 1 Sv/h" is met by the indication, not by the reading.

Caveats: the head is modelled with the front plate only (no side or rear walls), so oblique readings are an upper
bound — real walls cut them further; no scatter in the room or the body; one distance (1 m — the ratios do not depend
on it beyond the collimation geometry); the dead time is the analytic paralyzable model (verified against the event
stream in theme 42), not a full pulse-train simulation with pile-up; the 30 keV LLD drops the Cs-137 Ba X-rays.
Codex review (read-only): ICRP values, the fluence normalisation (the biased source already carries the projected-area
cosine), the fit and the dead-time model check out; fixed: detector options (entrance, backing, reflector, front-end
model) now pass through, the over-range ratios include the frontal calibration error, fractional angles in the CSV.

Reproduce: `montecarlo dose samples/scenario_handheld.json` (≈ 10 s) → `samples/dose_*.csv`;
`python samples/plot_dose.py` → `samples/dose.png`. Tests: `DoseTests` (ICRP points, interpolation and range, held-out
662 keV within ±25 % and collimated oblique field, paralyzable over-range and the live-time limit).

## 55. Localisation bias from undersampling the mask shadow — Studio optics at 1 m (2026-10-02)

Found while measuring Studio's per-nuclide precision (TODO-08 B): a Co-60 source at (−15, −8) mm, 1 m, localised
with a steady −1.4 mm y bias over 20 seeds, also without the Cs-137 source. Isolated with single-source weighted
runs (3 × 10⁶ biased photons, default `OpticsSettings`: rank 13, cell 0.7 mm, D 80 mm, 30 × 0.6 mm, focal 1000 mm):

- **Not energy, not Compton:** the same bias with the ideal geometric `CrystalDetector` and with Compton Argmax,
  for Cs-137 (−1.26 mm) as for Co-60 (−1.40 mm) at (−15, −8).
- **Position-dependent:** y = −8 gives −1.3 … −1.4 mm at any x; y = +8 gives −0.3 mm; y = 0 gives +0.15 mm.
- **Not interpolation:** a 0.25 mm reconstruction grid leaves it (errors up to 1.66 mm).
- **Sampling:** at constant detector size, pixel pitch 0.6 / 0.3 / 0.2 mm (1.27 / 2.54 / 3.80 samples per 0.761 mm
  shadow cell) gives RMS 0.95 / 0.51 / 0.24 mm and max 1.91 / 0.91 / 0.41 mm over y = −10 … 10 mm; the error
  repeats with the source position (period ≈ 3.4 mm at 0.6 mm pitch).

So the default optics (the `Gcam.Wpf` "Sharp" preset) sample the shadow at ≥ 2 pixels per cell only near a 160 mm
focal plane; at Studio's 1 m default they alias. This is theme 9's Nyquist rule (≳ 2 pixels per shadow cell) showing
up as a position-dependent bias, and it explains Studio anomaly AN-11. Consequence: TODO-09's presets must be
judged at the working focal plane. Reproduce: a throwaway program on `SceneConfigBuilder` + `SimulationRunner`
(sweep in `VV.Studio.Imaging`, "Localisation bias"); a regression test is to come with TODO-09.


## 56. Detector gap, crosstalk and depth from focus on Studio's list-mode path (2026-10-02)

Measured for TODO-11 ([PLAN.Studio.Detector](archive/PLAN.Studio.Detector.md), "Measurements by the planner") on Studio's own
path — `SimulationService.BuildConfig` → `ListModeSource` → `MeasurementStage` — at the default optics (rank 13, 0.7 mm,
D 80 mm, 30 × 0.6 mm), GAGG chain (FWHM 30.2 keV at 662), Cs-137 500 µCi, window 662 ± 1.5 FWHM.

- **Reflector gap = dead area.** Count rate at 1 m relative to no gap: 0.702 at 100 µm, 0.450 at 200 µm, against the
  area fraction ((p−g)/p)² = 0.694 / 0.444 (3 seeds × 60 s). The 662-window fraction stays 0.30.
  The opt-in evidence test (8 + 4 seeds, tolerance 4·s·√(1+1/8) from the measured spread) gives 0.6943 ± 0.0167 (s)
  at 100 µm and 0.4555 ± 0.0152 at 200 µm: the 200 µm ratio sits ~2 standard errors above the area fraction (the
  probe's 0.450 agrees in sign). Small, not significant at the test's bound; if real, a mechanism lets a few photons
  that enter a wider gap still count (e.g. scatter back into a crystal) — not investigated.
- **Optical crosstalk does nothing on this path.** Contact 0 / 0.4 / 0.9 give the same rate and window fraction: the
  list-mode sink records the total deposit before the light spread and positions by the arg-max site. Theme 35's
  crosstalk-vs-gap optimum belongs to a per-crystal windowed readout (`PerPixelWindow`), not to a four-channel Anger readout,
  where leaked light reaches the same four channels and moves the position instead → TODO-19.
- **Depth from focus is near-field and biased.** Peak prominence vs decoder plane on the retained flood (5 seeds,
  10 / 60 s): at 300 and 500 mm the sharpest plane is reproducible (seed spread ≤ 20 mm at 60 s) but off by
  +60 / +80 mm on axis and −20 / −40 mm at 30 mrad (−140 mm at 700 mm, 30 mrad); half-max interval 230–380 mm at
  300 mm, 350–730 mm at 500 mm. From ~700 mm the interval reaches the far sweep edge (a lower bound only); at 2 m the
  curve is nearly flat (best plane 890–2900 mm across seeds). The lateral dependence of the bias resembles theme 55's
  undersampling (1.27 pixels per shadow cell at 1 m); cause not established → TODO-23. Studio therefore shows the focus
  curve and its interval, labelled "sharpest plane", never a bare distance.
- Reproduce: a throwaway probe on the path above (sources on axis and at 30 mrad, planes 110–3000 mm in 30 steps and
  150–950 mm in 10 mm steps); the gap ratio becomes an opt-in evidence test with TODO-11.

## 57. Why the focus sweep's depth is biased — a heuristic score on a mismatched forward model (2026-10-02)

TODO-23 ([PLAN.Physics.DepthBias](archive/PLAN.Physics.DepthBias.md), review by Codex). Studio defaults, Cs-137, sources at
300 / 500 / 700 mm, 0 / 15 / 30 mrad; noise-free mean floods and the list-mode path (60 s, 5 seeds).

- **The bias needs no noise and no physics:** a single-line ideal geometric detector already gives the +60 / +80 mm
  on-axis offsets of theme 56. Scatter, slab clipping and cyclic vs non-cyclic decoding modify it; none causes it.
- **No single cause.** Peak-node sampling, the field normalisation, shadow undersampling (0.6 → 0.3 → 0.2 mm pitch) each
  move some of the nine maxima and spoil others (e.g. 700 mm → 160 mm); a fine lateral sweep at 500 mm puts the sharpest
  plane anywhere in 430–600 mm, oscillating with position — not a correctable offset.
- **Root:** the prominence score is a heuristic on a thin-plane, pixel-centre decode, while the acquisition integrates
  pixel area and slab transmission. A forward likelihood that models that integration gives −1…+1 mm on noise-free
  geometric controls; on the physical 60 s path, **with the bearing known**, it gives biases of a few mm and seed spreads
  of 1–11 mm (broadband, 300–700 mm). Without a known bearing no unbiased estimator was demonstrated.
- Premises corrected: the reconstruction grid's angular phase does not change with the plane; theme 24's seeded refine
  does not reach ~1 % at this geometry (−82 mm at 700 mm, 15 mrad).
- Consequence: Studio keeps the descriptive "sharpest plane" with its interval; a joint x/y/z forward-likelihood depth
  estimator is a research task (TODO-25). Reproduce: the probe programs and commands in
  [PLAN.Physics.DepthBias.Review](archive/PLAN.Physics.DepthBias.Review.md).

## 58. CR-RC shaper: whole-code state lost low-energy pulses; Q12 fractional state and explicit shaping times (2026-10-02)

TODO-20 ([PLAN.Physics.CrrcPresets](archive/PLAN.Physics.CrrcPresets.md), review and implementation by Codex). GAGG, S13360-3050,
AD9648 14-bit / 125 MSPS, isolated pulses.

- **Cause:** the integer CR-RC⁴ recurrence (C#, Python reference and `crrc_shaper.sv`, bit-exact to each other) kept
  whole output codes in every accumulator and floored each update, so a 32 keV pulse left 0 shaped codes and a
  662 keV-calibrated readout put 122 keV at 70 / 103 / 99 keV (Fast / Original / Slow). Not a preset value: changing
  K or order alone made it worse.
- **Fix:** Q12 fractional state (input sign-extended and shifted 12, coefficients Q16), output kept fractional; widths
  bounded — the largest product is 35,184,372,219,904 < 2⁴⁶, so signed 48-bit is safe; the legacy F = 0 path stays as a
  regression fixture. C#, Python and the RTL agree on **452,608 samples with 0 mismatches**; cocotb 137 / 137 with the
  C# vectors (planner re-run).
- **Shaping time:** order 4 kept (historically intended); K was an inherited benchmark constant, now per preset from an
  explicit T_sum = 100 / 200 / 500 ns (K = 20972 / 10486 / 4194) — a **simulation convention, not hardware evidence**.
- **Response now** (32 / 122 / 662 keV; peak in codes; full-noise resolution R_equiv %): Fast 2.23 / 8.52 / 46.1 codes,
  60.8 / 17.4 / 5.46 %; Original 2.63 / 10.0 / 54.4, 21.3 / 8.85 / 4.69 %; Slow 1.57 / 5.99 / 32.5, 16.1 / 8.08 / 4.65 %.
  The CR-RC energy readout stays off until an isolated-pulse estimator with trigger / phase behaviour is measured.
- Reproduce: `rtl/README.md` (vector export + `run_cocotb.py --csharp-vectors`); the measurement project and CSV in the
  Waveform VV record.

## 59. CsI(Tl) added to the crystal table — what-if material, same method as theme 52 (2026-10-02)

TODO-21 ([PLAN.Physics.CsIData](archive/PLAN.Physics.CsIData.md), Codex). xraylib 4.3.0 through the repository generator; CsI,
4.51 g/cm³, Tl omitted as an activator (as NaI:Tl); K-edge grid pairs at I 33.17 and Cs 35.98 keV.

- 661.7 keV: μ/ρ = 0.07435 cm²/g, photoelectric share 0.145, μ = 0.0335 /mm (GAGG 0.0773 cm²/g at 6.63 g/cm³, so CsI
  stops about two thirds as well per mm). GAGG remains the default (the reference crystal); CsI is a what-if in Studio.
- Validation like with like: photo + incoherent + coherent agrees with NIST's total within 0.011 % at 300 / 600 / 800 keV.
  K-edge jumps 3.28× (I) and 1.79× (Cs), close to NIST's 3.07× / 1.76×.
- Above 800 keV the photoelectric term is the method's power-law extrapolation (all materials): measured against NIST
  photo + incoherent it is +0.07 % at 1000 keV and **−0.35 % at 1250 keV** — the extension's approximation error,
  recorded, not hidden. The plan twice set a wrong check (a bound borrowed from GAGG, then omitting this extrapolation
  error); the implementer stopped both times.
- Evidence: `samples/materials/evidence/crystal_tables.json`, `CsI_checks.txt`, the NIST XCOM inputs.

## 60. The seeded `DefaultRandom` (legacy `System.Random`) is biased when draw counts vary (2026-10-02)

Found by the TODO-14 review (R-6), **reproduced independently by the planner**. `DefaultRandom(seed)` wraps
`new Random(seed)`, which in .NET is the legacy subtractive generator (seeded instances keep the .NET-Framework-compatible
algorithm; unseeded ones use xoshiro256**). Probe: per trial two draws, an isotropic direction scored on an 18 mm square
at 100 mm, then a partner angle by rejection sampling (variable draw count) on the same stream; 10⁸ trials per seed.

| Generator | Hit fraction vs analytic 2.5576 × 10⁻³ (seeds 1 / 2 / 3) |
|---|---|
| seeded `System.Random` | −1.58 % / −2.10 % / −1.66 % (z = −8.0 / −10.7 / −8.4) |
| xoshiro256** (same probe) | −0.07 % / +0.13 % / −0.04 % (z = −0.4 / +0.7 / −0.2) |

- The bias needs a data-dependent draw count between trials (rejection sampling); the review isolated it there and found
  a fixed-draw (inverse-CDF) sampler free of it.
- **Exposure:** `DefaultRandom` is created in 35 places across 21 source files; transport uses rejection loops (e.g.
  Klein–Nishina in `ComptonModel`). Which published numbers move, and by how much, is **not yet measured** → TODO-26.
- Reproduce: the probe in the planner's scratch (`rngprobe`: legacy vs xoshiro256**, same loop); TODO-26 brings a
  repository test.
- **Survey (TODO-26 review):** the bias above is pattern-specific — 4π transport efficiency, crystal stopping and
  photopeak fraction are clean at 0.01–0.1 %, and no published number moves with xoshiro256** (43 reproduce commands ×
  5 seeds). The real defect is that the legacy generator is **affine in its seed**: Studio's `MeasurementStage` reseeds
  per event (`seed + index·104729`), so consecutive events' smears are correlated (−0.81; raw draws +0.555 / −0.477,
  planner check) and Studio's window counts fluctuate far less than physically. Replaced in TODO-26.
- **Replacement (TODO-26, 2026-10-02):** `DefaultRandom` is xoshiro256** seeded by SplitMix64 (unseeded runs expose
  their seed); `MeasurementStage` uses a 64-bit (seed, index) key; study realisations have their own seed offset.
  Nearby-seed stream correlation now ±0.03 (was up to +0.99); Studio smear lag-1 correlation +0.002 (was −0.81) and
  window-count variance / binomial 1.022 (was 0.048). Klein–Nishina vs quadrature −0.3σ / +0.5σ. The only number that
  moved beyond its spread: the shield study's knee RMS (0.79 → 0.96 mm, unquoted; `samples/shield.png` regenerated).
  Three single-seed regression pins were re-derived from 64-seed distributions; the mask-fabrication claim became a
  24-seed paired t-test (t = 8.95 vs k = 3, false-fail ≈ 4 × 10⁻⁶).

## 61. Per-decay cascade emission in list mode — correct, and negligible at 1 m (2026-10-02)

TODO-14 ([PLAN.Physics.CascadeEmission](archive/PLAN.Physics.CascadeEmission.md), review and implementation by a substitute
Claude subagent). Co-60 and Na-22 are now emitted one decay per history in `ListModeSource`: all of a decay's gammas
are transported and detected ones form **one event** (summed deposit at the largest-deposit pixel, one arrival time);
directional biasing aims one randomly chosen gamma, weight n·w_k / (n̄·H), source choice ∝ activity × n̄.

- **Co-60 angular correlation** W(θ) = 1 + cos²θ/8 + cos⁴θ/24 (A₂ = 0.1020, A₄ = 0.0091), sampled by inverse CDF.
  The engine previously drew the partner isotropically despite its own summary; the correlation raises the joint
  detection at far field by a factor 1.111.
- **True-coincidence sum fraction** (both gammas deposit / detected decays, Studio default geometry, on axis):
  **1.998 × 10⁻⁶ ± 1.0 % at 1000 mm, 2.173 × 10⁻⁵ ± 0.8 % at 300 mm** — matching the review's probe (z −0.4 / +0.6).
  At 500 µCi that is one summed decay per hour at 1 m, about 50× below random pile-up; a sum peak shows only within
  ~100 mm. Correct physics, honestly small for this camera.
- Biased vs analog decays agree (detected per decay z = −0.40, coincident z = +0.30); detected rate unchanged (133.7 vs
  133.6 cps at 1 m); stop / continue stays event-identical with a Co-60 case (139,747 events, 48 summed decays).
- **Na-22 data corrected** from ENSDF (Basunia, Nucl. Data Sheets 127, 69 (2015)): 511 keV = 2 × 0.8996 = 1.7992,
  1274.5 keV = 0.9994 (was 1.798 in `Isotopes`, 1.806 in `DecayScheme`); one table now.
- Not changed: single-photon isotopes keep their draws; Ir-192 stays independent (no scheme). With pile-up off the
  Spectrum axis ends at 1.15 × the top line, so a 2505 keV sum lands in overflow (counted in the total).

## 62. Joint forward-likelihood depth beats the sharpest plane — but is not yet a Studio replacement (2026-10-02)

TODO-25 ([PLAN.Physics.DepthLikelihood](PLAN.Physics.DepthLikelihood.md), research review by a substitute Claude
subagent; [review](PLAN.Physics.DepthLikelihood.Review.md)). Forward model = the engine's own transport replayed with
common random numbers (no template library), exact expected mask transmission, the engine's crystal, analytic 662 keV
window; intensity and flat background profiled; parameters bearing + v = 1/(z − D); no step uses the truth.

- **60 s, default Cs-137 500 µCi, all events (12 seeds, 0 / 30 mrad):** spread 0.7 / 3.0–3.4 / 4.9–5.1 / 16–20 mm at
  300 / 500 / 700 / 1000 mm; mean error at most 0.64 × the spread (Studio's sharpest plane on the same floods: +51 / −19,
  +69 / −40, −70 / −131 mm, and seed SD up to 350 mm at 1 m). Wrong maxima 0 / 190 at 60 s.
- Coverage of 68 / 95 % likelihood-ratio intervals 0.633 / 0.934 at 60 s; at 300 mm with ~46,000 counts the model budget
  (0.9M histories) under-covers — the budget must scale with the data's counts.
- Template-interpolation error measured without MC noise: ≤ 0.45 mm at 20 mm nodes; theme 57's library offsets were
  template noise. Cost: 70–130 s per channel single-threaded per flood — a background job, not a live update.
- **Gate (L-5): not yet.** Open: the 10 s wrong-maximum rate with the final search (v2 gave 11 / 192), count-scaled
  model budget, and model mismatch (e.g. mask–detector distance; a 0.5 mm error ≈ −6 mm at 1 m, derived not measured).
  The sharpest plane stays in Studio until these are measured.

## 63. Evidence quoted over seed ensembles — `samples/evidence` (2026-10-02)

TODO-27 ([PLAN.Physics.EvidenceRefresh](archive/PLAN.Physics.EvidenceRefresh.md), measured review
[PLAN.Physics.EvidenceRefresh.Review](archive/PLAN.Physics.EvidenceRefresh.Review.md), implementation by a substitute Claude
subagent). Every Monte Carlo number in the evidence register was a single realisation at seed 12345. All 33
quotation families were re-measured over 32–128 outer seeds on one engine build (`55af615`; nothing under `src/`,
`samples/` or `rtl/` changed since) and are now quoted as mean ± SD or median [quartiles] with N, picks and gates as
k / N. The author decided every changed interpretation from its measured numbers (ER-1 … ER-12 in the plan; D-19,
D-39, D-40 in `VV.Gcam.Decisions`).

- **Withdrawn or re-interpreted** (decision row → where): calibrated subtraction "matches" the antimask to 0.07 mm →
  the antimask is ~20–30 % better at equal time, rotation still rejected on cost and stability (ER-1, D-19; EV-12,
  LIM-07, theme 5); tungsten "8–10 mm" → ~10 mm from the defined D = 20 mm recipe (21 / 32), the lab command cannot
  pick; 7 mm leaks 28.8 % (ER-2; EV-04, theme 4); PR-MFG-01 "within ~2×" → gate seed-mean RMS ≤ 1.25 × ideal, 40 µm
  1.13× passes, 80 µm 1.43× fails (ER-3, D-39; EV-30, theme 38); "sub-mm from ~50 counts" → collapse below ~25, sub-mm
  from ~250 (ER-4; EV-07, theme 9); rank 11 "≥ 90 %" → passes in 31 / 32, PAPER's ~7× → ~2.5× (ER-5); 16 × 16
  "halves" → about a third (ER-6); shield picks as frequencies, Co-60 25 mm 6.8 kg (83 / 128) or 30 mm 9.8 kg (45),
  "not carriable" against PR-PHY-01, no 6 → 8 mm increment (ER-7; EV-25, theme 22); cascade slope → pooled Poisson fit
  (ER-8, below); depth range < 0.15 m (clamp), 240 mm width censored, z^1.74 empirical, two viewer estimators
  separated (ER-9; EV-33); front end 6.16 ± 0.25 %, the 1.70 % proxy not called electronic noise (ER-10; EV-17);
  Co × 2 spatial limit holds in 126 / 128 (ER-11; EV-15); crystal-gap 9 / 13 / 20 / 69 / 45 % unverified, replaced by
  a labelled experiment 1 / 1.59 / 2.52 / 6.60 / 5.33 relative to zero gap (ER-12; PAPER §4, theme 35).
- **Moved without a change of conclusion** (one realisation, often from the earlier generator): EV-01 centred 0.35 mm,
  ghost −6.40 mm, sweeps 278 / 146 and 361 / 174; EV-02 5.0° with background (56 / 64); EV-05 edge RMS 0.26 mm; EV-09
  floor 0.25 mm; EV-11 MLEM FWHM 1.93 mm at the current 1.5 × 10⁶ photons; EV-15 mixed-field R 3.99, stripped error
  13–18 %; EV-21 plain GAGG 4 ± 2 % at 1 Mcps (not 0.2 %); EV-29 ~0.5 mm; EV-32 0.48 → 0.51 mm. **Stayed** (within
  rounding): efficiencies (EV-09, EV-19, EV-20), dose ratios (EV-23), DOI (EV-13), sub-cell (EV-08), alignment (EV-31),
  dead time (EV-22), the three-source map (EV-10, grid-stable in 64 / 64).
- **ER-8 cascade.** `samples/evidence/cascade_fit.py` fits sum_count ~ Poisson(8 × 10⁶ · exp(a + b · ln single)) over
  all 128 × 7 rows (344 zero-count rows included): **b = 2.001 ± 0.027** (Wald; seed bootstrap SD 0.028, 95 % 1.95–2.06;
  Pearson χ² / dof 1.05; the per-distance pooled fit gives 2.001 ± 0.027). Expected for this geometry: ln P₂ against
  ln P₁ over the CLI's distances, P₁ the solid angle of the ±7 mm face and P₂ both cascade photons hitting it with the
  engine's W(θ) = 1 + cos²θ/8 + cos⁴θ/24 — **1.99** (2.00 with summing-out at its upper bound; the open-fraction test
  and the normal-incidence crystal deposit do not depend on distance). Consistent with ε² (z = 0.4).
- **ER-3 gate.** Seed-mean RMS over 128 seeds ÷ seed-mean ideal (1.346 mm): 0.96 (10 µm), 1.05 (20), **1.13 (40)**,
  **1.43 (80)**, 2.71 (160) — from `maskfab/maskfab.csv/<σ>/rms_mm` in `samples/evidence/results/aggregate.csv`.
- **ER-4 biasing.** Measured with its own recipe (`probe` mode `bias`: same scenario, then a clone with
  `DirectionalBiasing = false` and 10⁸ histories), 32 seeds, rerun through the committed driver bit-identical to the
  review: biased ÷ 4π = **0.999 ± 0.006** (within 1 % in 30 / 32; the spread is the 4π run's Poisson noise); at 10⁶
  photons the biased estimate's own seed spread is 0.11 % against 0.59 % for 10⁸ isotropic histories.
- **Artefacts** (`samples/evidence/`): `seeds.json` (O128, F128 and the RTL lists), `manifest.json` (49 families: recipe,
  N, scenario SHA-256, fixed in-study patterns, engine commit), `run_seeds.py` (driver: complete JSON clone, only Seed
  and declared overrides changed, isolated cwd per run, stdout / stderr / exit / timing captured, refuses changed
  scenarios), `aggregate.py` (parsers ported from the review; refuses missing, failed or schema-mismatched runs),
  `probe/` (headless recipes the CLI lacks: precise, scan, bias, fov, gap, spatial, depthsharp, materials, viewer — not
  in `Gcam.sln`), `rtl_seeds.py` (Python / RTL studies with their own generators and fixtures), `cascade_fit.py`,
  `results/` (`aggregate.csv` per-metric summary, `aggregate_discrete.csv` frequencies, `values.json` per-seed values
  of the quoted keys, `cascade_fit.json`). `aggregate.py` run over the review's raw runs reproduces 3,539 of its 3,540
  metric summaries exactly (the missing one came from rounded CLI output superseded by the materials probe).
- Not done: the full rank × pitch × D grid and an arbitrary-pattern population (mask, defect, gain maps) were not
  re-run over seeds, so no global optimum or all-device tolerance is claimed; no RTL vectors were regenerated.
- Plots: `shield.png`, `masktaper.png`, `handheld_validation.png` read their annotations from `results/` (curves stay
  one labelled run); `cyclic_vs_noncyclic.png` regenerated at seed 12345 with the current engine (362 / 173).

## 64. Absolute ambient background — terrestrial field, calibrated gate, and what it changes (2026-10-04)

TODO-30 ([PLAN.Physics.AmbientBackground](PLAN.Physics.AmbientBackground.md), Codex review
[PLAN.Physics.AmbientBackground.Review](PLAN.Physics.AmbientBackground.Review.md); author decisions AB-1 … AB-14).
Turns 1–5 by Codex, **turns 6–9 by substitute Claude implementers** (Codex allowance), each with a committed report
`samples/evidence/results/ambient-baseline-v1-turn<N>.md` and its JSON. Every earlier MC number came from an ideal
environment or a background set relative to the source (BSR, theme 28). The engine can now add a source-independent
terrestrial field (photon H*(10) → fluence by ICRP 74 → transport → Poisson), bounded by two geometries instead of a
housing model (AB-2): **bare crystal** on all faces (upper) and **front only** through the mask (lower). With the
field off, every legacy and BSR path stays bit-for-bit identical (AB-8). Evidence: EV-34 plus an ambient item in
EV-01 / 02 / 07 / 09 / 12 / 15.

- **Spectrum built by the engine (turn 6, AB-4 / 4d / 10).** K-40, U-238 and Th-232 series at the UNSCEAR 2000 soil
  activities 420 / 33 / 45 Bq/kg in a uniform soil half-space (HASL-258 soil, NIST XCOM, dry air), collided MC
  (`SoilAirTransport`, `montecarlo ambient-terrestrial`), scored at 1 m. Evaluated lines with absolute intensities
  only (IAEA LiveChart / ENSDF, 96 hash-pinned snapshots); 149 "not included" entries listed, never substituted.
  Uncollided fluence vs the analytic half-space kernel: 0.99945 / 1.00001 / 0.99950, every tested cell within 6 SE.
  **Air kerma per Bq/kg, MC / UNSCEAR: 1.01388 ± 0.00032 / 0.99740 ± 0.00032 / 1.02268 ± 0.00026** (N = 16 seeds ×
  10⁷ histories per chain) — many SE from 1, a model-to-model difference; accepted by the author with a **±3 % band
  chosen after seeing the ratios (AB-10), a model comparison, not a statistical test**. 60.76 nGy/h at the UNSCEAR
  activities (UNSCEAR 59.94). Photons above 1332 keV carry 54.1 / 28.9 / 36.1 % of the kerma — the AB-3 exposure
  (tungsten μ clamped above 1332 keV, no pair production in the crystal, dose cut at 2000 keV; TODO-31).
- **Validated file (turn 7).** `samples/ambient/terrestrial-unscear2000-v1.json` issued by `montecarlo ambient-issue`
  (content unchanged, `IsValidated` + an acceptance record read from the hash-pinned turn-6 results); evidence runs
  refuse an unvalidated or mis-pinned spectrum. A CRLF-hashed placeholder sidecar was found and fixed (hash the bytes
  git stores; test `PinnedAmbientFiles_HashTheBytesGitStores`).
- **Detected ambient rate per µSv/h** (turn 7, mean over 128 seeds): lab bare 162.8 cps all deposits / 1.66 cps in the
  662 keV window, front-only 0.572 / 0.0447; hand-held bare 335.2 / 4.28, front-only 1.283 / 0.104. Ambient ÷ a 1 MBq
  Cs-137 source at 0.10 µSv/h: lab 0.203 (bare, open), 1 m **7.04** (bare, open), 0.228 (bare, 662 keV), 0.027
  (front, open). The review's order-of-magnitude estimate (BSR ~2.5–3 at 1 MBq, ~1 m) is exceeded by the bare bound
  and far above the front-only one.
- **Search statistic and gate (turns 7–8, AB-7 / 11).** Z(θ) = the decoder's output studentised against the
  instrument's background-shape model with the acquisition total as the only nuisance (mean 0, variance 1 per grid
  point under a multinomial background — tested); max over the grid, calibrated empirically. 96 configurations
  (4 cases × 2 exposures × 3 fields × 2 bounds × 2 windows). Turn 7 (4,096 selection nulls, α = 0.003): 93 / 96 —
  three front-only configurations with < 1 expected count missed by 0.007 points (13 / 2,048, upper 1.007 %) because Z
  is coarse there. AB-11: re-selected on **65,536 nulls per configuration** (F128[1:65] × 1024) with ties counted
  (thresholds 1.163 … 4.6245, `gate-thresholds-v2.json` pinned before validation), validated on **new seeds
  F256[128:256]** (8,192 nulls per configuration): **96 / 96 pass**, pooled **2,424 / 786,432 = 0.308 %** (one-sided
  95 % 0.298–0.319 %), worst lab 10 s 0.10 front-only 662 keV 40 / 8,192 (upper 0.64 %). The first 64 repeats per
  seed reproduce turn 7's thresholds exactly.
- **Count gates under the field (turn 8, N = 128).** Smallest net S for ≥ 95 % trusted and within one resolution
  element, lab centre: ideal 100, front-only 100, bare open 250 / 250 / 500 (10 s) and 500 / 500 / 1000 (60 s) at
  0.05 / 0.10 / 0.20 µSv/h, bare 662 keV 100 (10 s) and 100 / 250 / 250 (60 s); hand-held centre: 250 ideal, bare open
  500 (10 s) and 1000 / 1000 / > 1000 (60 s). **A significance gate, not a count gate:** in the bare open window S grows
  roughly as √B (lab 250 → 500 → 1000 for B ≈ 160 → 490–980 → 1950). Of 448 gate entries 438 equal turn 7's; the 1 m
  3° source is marginal (edge of the optics, seed-clustered).
- **What it changes (turns 8–9).** EV-01 at the scenarios' 1 MBq × 1 s (N = 64): the count budget dominates (lab
  non-cyclic 180.4 ± 1.5 of 625 ideal vs 278 noiseless), the field costs −10 / −19 / −36 % bare open, ≤ 1.6 % in the
  662 keV window. EV-02 (N = 64): open window at N0 500 the non-cyclic field falls from 5.5° to 3.5–5.5° (10 s) and
  0–4° (60 s) and the flag fails from B/N0 ≈ 0.7; the 662 keV window keeps it within one 0.5° step, unflagged wrong
  spot ≤ 0.28. EV-12 (N = 128): subtraction / antimask reproduce the relative ranking (2.3 / px 0.45 / 0.35 mm; 6.8 / px
  0.92 / 0.61 mm; 13.6 / px 2.24 / 1.69 mm); raw single-mask decode collapses to ~9 mm in the bare bound. EV-15
  (N = 128): the legacy recipe has no live time (1 Bq + 8 Bq and a photon budget → 6.14 Cs counts in a literal 66 h; run
  and reported in turn 8, unmeasurable); AB-14 re-measured it at Cs 1 MBq + Co 8 / 2 MBq, 60 s: **no measurable field
  effect**; stripping −4.5 % [−8.7, −1.0] / −1.1 % [−4.9, +2.8] at 8 : 1 (separated / co-located), −1.2 % / −0.3 % at
  2 : 1; R = 1.1338 ± 0.0092 on total-deposit windows, **not comparable** with the legacy per-pixel R = 3.99. Front-only
  bound and the 662 keV window change nothing measurable anywhere tested.
- **Finding → TODO-33 (AB-12): the raw decoder is pulled; the search statistic is not.** In the bare bound, open window,
  the cross-correlation pull is a switch: ideal floor below B/S ≈ 0.5, jumps between ≈ 0.6 and 1.3, saturation above
  ≈ 2 at ~8 mm lab (2.9°), ~9 mm hand-held (3.4°), ~19 mm at 1 m (1.1°), ~40–50 mm at 5 m (0.5°) — the decoder's answer
  for the non-flat bare background (edge / centre 2.7 lab, 3.3 hand-held; noiseless answer (0.8 ± 1.4, −8.8 ± 0.2) mm lab,
  saturated acquisitions at (0.9, −8.2)). Excess ≤ 0.73 mm in the 662 keV window and front-only. MLEM without a
  background term is pulled more slowly (hand-held edge within-3-mm 0.960 → 0.805 vs 0.772 → 0.184) but is worse at
  80 counts even ideal. Baseline in `ambient-baseline-v1-turn8-bias.json`; fix (background-shape-aware decoding,
  MLEM's b_i term the natural candidate) is TODO-33, not done here.
- **Reproduce.** `montecarlo ambient-terrestrial samples/ambient/terrestrial-generator-v1.json <dir>`;
  `montecarlo ambient-issue samples/ambient/terrestrial-unscear2000-v1-issue.json <dir>`; per seed `ambient-gate` /
  `ambient-evidence <request>`; ensembles `python samples/evidence/run_seeds.py --manifest
  samples/evidence/manifest-ambient-v2.json --family gate_selection_v2 | gate_validation_v2 | ev12_antimask
  ev15_separation ev02_fov ev01_sweep` and `manifest-ambient-v3.json` `ev15_abs_a ev15_abs_b ev02_fov_cs662`, then
  `samples/evidence/ambient/{select_thresholds,aggregate_gate,aggregate_ev,bias_baseline}.py` (commands in each turn
  report's "Reproduce").
- **Artefacts.** `samples/ambient/` (catalog, materials, generator request, NOT-VALIDATED and validated spectra with
  `.sha256`, `source-data/v1/` snapshots, `.gitattributes` `* -text`); `samples/evidence/ambient/` (requests, pinned
  thresholds v1 / v2, scripts); `samples/evidence/manifest-ambient-v{1,2,3}.json`; `seeds.json` gained F256;
  `samples/evidence/results/ambient-baseline-v1-*` (reports, summaries, gate / EV / bias JSON). Tests: the engine
  suite grew 330 → 393 over turns 6–9 (`KleinNishinaTests`, `ExponentialIntegralTests`, `SoilAirMaterialsTests`, `TerrestrialCatalogTests`,
  `SoilAirTransportTests`, `TerrestrialSpectrumTests`, `IncidentSpectrumFileTests`, `AmbientAngularSamplerTests`,
  `AmbientFieldTests`, `GateResponseTests`, `CorrelationSearchTests`, `AmbientGateStudyTests`, `AmbientEvidenceTests`).
- **Caveats.** Bounds, not a housing (realistic head: TODO-32); high-energy transport incomplete (TODO-31); homogeneous
  crystal for source and field, so each family's ideal is re-measured in this pipeline and differs from the legacy
  (EV-02 0.5–1° narrower); Poisson maps from expected maps, not event-by-event; terrestrial soil only (no cosmic,
  airborne radon, room scatter, intrinsic crystal activity); Studio's ambient input stays off by default (AB-5). Turns
  6–9 were substitute Claude implementers, not Codex; turn 8's runs carry `engine_tree_dirty: true` (committed
  afterwards as `aa9fcc4`), turn 9's EV-02 run used a copy of the turn-8 build. Pilot runs (seed 777) are not evidence.
