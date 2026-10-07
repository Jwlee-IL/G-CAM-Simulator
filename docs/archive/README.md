# docs/archive — finished plans and the index of closed tasks

Scope: every `PLAN.*` document whose task is finished, with its review and turn reports, kept as written (links updated
when they moved here), and the **index of closed tasks** below — one entry per closed task, newest first, linking its
plan. Results themselves live in [AGENTS.Findings](../AGENTS.Findings.md) and, for the requirement set, in the V&V
documents; open work is in [AGENTS.Todo](../AGENTS.Todo.md) (ready / in progress) and
[AGENTS.Backlog](../AGENTS.Backlog.md) (deferred / undecided).

Closing a task (procedure in [AGENTS.Planning](../AGENTS.Planning.md) step 8): delete its Todo row, add an entry here,
set the plan's status to done and move the plan and its review / reports into this folder — in one commit.

## Closed tasks

- **TODO-39 Studio strip count** (2026-10-07): with Compton strip on, both reconstruction methods report the signed net
  Σ low − R·Σ high as `N ± σ net counts` (counting + calibration, single-contaminant window overlap; "uncertainty
  unavailable" otherwise) instead of cross-correlation's clipped sum (+50 … +250 counts high); cross-correlation decodes
  the signed difference while the display stays clipped; ROI values carry their pane's unit (SR-IMG-05, SR-IMG-07,
  SR-MEAS-03). Desktop 35 / 35, broken verdict 7 / 7, recovery 7 / 7. Review and implementation by Codex —
  [PLAN.Studio.StripCount](PLAN.Studio.StripCount.md).
- **TODO-37 evidence-run provenance** (2026-10-07): per-seed provenance (schema v1: source commit, tracked-diff and
  untracked hashes; hashes of the executable closure actually run per executor kind; recipe; output hashes), runs
  executed from staged immutable copies of the managed artifacts and of the Python / RTL closure, every downstream
  aggregator / selector (incl. `bias_baseline.py`) embedding provenance or writing hash-bound CSV sidecars and failing
  closed on missing / mixed / conflicting identities; `calibration_record.py` prints it when present (the four existing
  records unchanged); 29 stdlib tests in CI. Review and implementation by Codex — [PLAN.Evidence.RunProvenance](PLAN.Evidence.RunProvenance.md).
- **TODO-36 MLEM as a regular reconstruction path + TODO-38 desktop fix** (2026-10-07): `Decoder.Method` (single run
  and Studio only; studies refuse it), one shared pixel-area MLEM construction, Studio's Reconstruction selector
  (SR-IMG-07; 400 iterations chosen by the pair-resolution rule at the default optics and confirmed on a third seed set;
  MLEM + strip with the downscatter as background; labelled strip counts). TODO-38: the failing channel scenario was
  under-powered (Co-60 20 µCi, 27 % wrong side), not the ambient default; fixed with Co-60 400 µCi by a derived bound.
  Six MLEM desktop scenarios; two product defects found by them and fixed (unit label followed the selector, not the
  displayed image; refreshes cleared the hovered readout). Desktop 34 / 34, broken-verdict 7 / 7 — substitute Claude
  implementer; [PLAN.Studio.MlemReconstruction](PLAN.Studio.MlemReconstruction.md),
  [PLAN.Studio.DesktopChecks](PLAN.Studio.DesktopChecks.md). Follow-up: TODO-39 (clipped strip count).
- **TODO-28 step 1 — calibration records** (2026-10-07): a fixed form (`VV.Gcam.Calibration.Template`) and four
  generated records — CAL-01 gate v1 (superseded, 3 FAIL), CAL-02 gate v2 (96 / 96), CAL-03 stripped Z_s (135 / 135,
  9 not applicable), CAL-04 pair-test floors (410 / 414, 4 FAIL in cells that resolve nothing) — from pinned evidence
  files with seed-disjointness and stored-verdict cross-checks; CI runs `calibration_record.py --release`. Author
  decisions D-50 (form; false alarms judged by the upper limit), D-51 (conservative neighbour between grid points) —
  [PLAN.Docs.CalibrationRecords](PLAN.Docs.CalibrationRecords.md).
- **TODO-34 angular resolution at use distance** (2026-10-06, performance-critical, D-41): blind two-peak test with a
  calibrated significance floor; at 1 m a pixel-area MLEM (new opt-in forward model) resolves two sources 1.30° apart
  (1.25 × cell / D), cross-correlation not within 3 elements; EV-11's near-field "2–3 mm" withdrawn; PR-IMG-02 restated
  in angle (0.38° / 0.21° RMS at 250 / 1000 counts). Author decisions D-46 … D-49. Review, three turns and runs by a
  substitute Claude implementer — [PLAN.Physics.AngularResolution](PLAN.Physics.AngularResolution.md), Findings 66.
- **TODO-35 Cs-137 under Co-60** (2026-10-06, performance-critical, D-42): literature review, then the cost of Compton
  stripping at use distance — exact Currie detection limit validated 864 / 864 (10 µSv/h of Co-60 at 1 m: 181 Cs counts
  = 1.6 MBq in 60 s), a stripped trust statistic with 144 calibrated thresholds (135 / 135 informative configurations ≤ 1 % false), the gain as the
  dominant ratio systematic → side-window reference; author decisions D-43 … D-45. Review, implementation and runs by a
  substitute Claude implementer — [PLAN.Physics.CsUnderCo60](PLAN.Physics.CsUnderCo60.md), Findings 65.
- **TODO-30 absolute ambient background** (2026-10-04): a source-independent terrestrial field (K / U / Th from a
  soil / air transport generator; UNSCEAR 2000 kerma ratios 1.014 / 0.997 / 1.023 accepted as a ±3 % model comparison)
  bounded by a bare-crystal and a front-only geometry; a calibrated background-aware trust gate (96 / 96 configurations
  ≤ 1 % false locations on fresh seeds); EV-01 / 02 / 07 / 09 / 12 / 15 re-measured under the field (EV-34); Studio
  default 0.10 µSv/h front-only with a fixed 0–2000 keV spectrum axis. Turns 1–5 Codex, 6–11 substitute Claude
  implementers; 16 author decisions AB-1 … AB-16 — [PLAN.Physics.AmbientBackground](PLAN.Physics.AmbientBackground.md),
  Findings 64. Follow-ups: TODO-31 (high-energy transport), TODO-32 (housing), TODO-33 (background-aware decoding).
- **TODO-29 clean-room wording** (2026-10-02): text attributing a product description, unsolved defects,
  countermeasures or implementation choices to a former instrument rewritten as known problems / published practice
  of the camera class; design-exercise notice on the VV set; ideal-environment notice on every MC result —
  [PLAN.Docs.CleanRoom](PLAN.Docs.CleanRoom.md).
- **TODO-27 evidence refresh** (2026-10-02): every Monte Carlo quote in the evidence register re-measured over seed
  ensembles (32–128 seeds) and quoted with its spread; 12 author decisions (antimask ~20–30 % better but rotation still
  rejected, tungsten ~10 mm, 40 µm gate, sub-mm from ~250 counts, …); reproducible driver and aggregates in
  `samples/evidence/` — [PLAN.Physics.EvidenceRefresh](PLAN.Physics.EvidenceRefresh.md), Findings 63.
- **TODO-26 RNG replacement** (2026-10-02): legacy seeded `System.Random` (affine in its seed; correlated per-event
  smears in Studio) replaced by xoshiro256**; no published number moved beyond spread except the unquoted shield knee —
  [PLAN.Physics.RngBias](PLAN.Physics.RngBias.md), Findings 60.
- **TODO-14 per-decay cascade emission** (2026-10-02): one event per Co-60 / Na-22 decay with angular correlation and
  unbiased weighting; sum fraction 2.0e-6 at 1 m, 2.2e-5 at 300 mm — [PLAN.Physics.CascadeEmission](PLAN.Physics.CascadeEmission.md),
  Findings 61; found the RNG bias (TODO-26).
- **TODO-12 legacy viewer removed** (2026-10-02): `src/Gcam.Wpf` and its solution entry deleted after every kept
  feature moved into GCAM Studio; last present at `85b2ed1` (theme 34's code is there) —
  [PLAN.Studio.WpfRemoval](PLAN.Studio.WpfRemoval.md).
- **TODO-07 Spectrum, TODO-13 live list-mode acquisition, TODO-15 spectrum graph** (2026-10-01/02): implemented,
  headless-verified and rendered offscreen; their desktop checks passed in TODO-22 —
  [PLAN.Studio.Spectrum](PLAN.Studio.Spectrum.md), [PLAN.Studio.LiveAcquisition](PLAN.Studio.LiveAcquisition.md),
  [PLAN.Studio.SpectrumPlot](PLAN.Studio.SpectrumPlot.md).
- **TODO-20 CR-RC presets** (2026-10-02): whole-code state lost low-energy pulses → Q12 fractional state in C# / Python /
  RTL (48-bit, bit-exact on 452,608 samples), explicit per-preset shaping time (simulation convention) —
  [PLAN.Physics.CrrcPresets](PLAN.Physics.CrrcPresets.md), Findings 58.
- **TODO-21 CsI(Tl) data** (2026-10-02): theme-52 method, NIST like-with-like within 0.011 %, offered in Studio as a what-if —
  [PLAN.Physics.CsIData](PLAN.Physics.CsIData.md), Findings 59.
- **TODO-16 layout batch 4** (2026-10-02): desktop-survey items P-12 … P-16 — [PLAN.Studio.Polish](PLAN.Studio.Polish.md).
- **TODO-22 final desktop pass** (2026-10-02): desktop suite 27/27 (Start / Stop / Continue / Reset, every workspace,
  independent oracles), all 12 scenarios fail under broken verdicts, 16 ms gate (zoom 5.6 ms, resize 4.0 ms), polish
  survey over four workspaces (P-12 … P-16), README capture — Codex in a separate worktree while the desktop was free.
- **TODO-23 depth-from-focus bias** (2026-10-02): reference plan → Codex's measured review: the heuristic score on a
  forward-mismatched decode, no single cause; known-bearing likelihood a few mm — [PLAN.Physics.DepthBias](PLAN.Physics.DepthBias.md),
  Findings 57; research follow-up TODO-25.
- **TODO-24 acquisition control Start / Stop / Reset** (2026-10-02): the author's MCA model (continue after Stop,
  Reset discards, physical inputs locked while data exist — no stale state; source drag withdrawn; a new seed per
  acquisition) → review and implementation by a substitute Claude subagent (Codex out of credits); continuation
  measured event-identical to an uninterrupted run — [PLAN.Studio.AcquisitionControl](PLAN.Studio.AcquisitionControl.md).
- **TODO-16 layout batch 3** (2026-10-02): workspace layout from the renders, review-corrected causes, Ba K X-ray origin
  in the line data — [PLAN.Studio.Polish](PLAN.Studio.Polish.md).
- **TODO-11 Detector workspace, focus sweep, external range** (2026-10-02): reference plan → Codex review (code only;
  sandbox refused processes) → planner's measurements (gap = dead area; crosstalk a no-op on the list-mode path; depth
  from focus near-field and biased — Findings 56) → SiPM pitch and crosstalk moved to TODO-19 (author) → implementation
  by Codex, finished by a substitute Claude subagent when Codex's credits ran out — [PLAN.Studio.Detector](PLAN.Studio.Detector.md);
  follow-up TODO-23 completed (Findings 57); research continuation is TODO-25.
- **TODO-10 Waveform workspace and shared chain** (2026-10-02): reference plan → Codex review (time base 72.8 cps,
  RTL bit-exact for the selected chains, double smearing, CsI fallback) → a conventional four-ADC Anger readout →
  implementation — [PLAN.Studio.Waveform](PLAN.Studio.Waveform.md); follow-up TODO-19 remains; TODO-20/21 completed below with Q12 CR-RC and CsI transport.
- **TODO-09 editable optics, presets, decoder-focus refocus** (2026-10-02): reference plan → Codex's measured review
  (coverage rule false, All not refocused) → revised plan → implementation — [PLAN.Studio.Optics](PLAN.Studio.Optics.md).
- **TODO-18 configuration-scan headline** (2026-10-02): the rank-23 "±64 mm, 96 %, ~7×" result predated the 10 mm
  slab mask and does not reproduce (0.149; collimation at D = 20). Re-scanned: widest ≥ 90 %-usable field is rank 11 /
  1 mm / D 30 → ±21.5 mm (~2.5×). Findings theme 3, EV-03 (and its model-history row), new `samples/plot_scan.py`.
- **TODO-17 Co-60 localisation bias** (2026-10-02): not energy — undersampling of the mask shadow by the default
  optics at 1 m (1.27 samples per cell; RMS 0.95 → 0.24 mm from 0.6 to 0.2 mm pixels). Findings theme 55; feeds TODO-09.
- **TODO-08 detector realism, background, per-nuclide imaging, Compton strip** (2026-10-02): Gcam.Wpf's detector
  defaults restored, gain in one measurement stage, BSR background events, channels per isotope through the shared
  window N, a selector with truth / found markers, Compton strip with an H-only calibration (co-located Cs + Co ×2,
  600 s: stripped Cs 13 409 vs Cs-only 13 297, 4σ = 1 017; R = 0.48). Found peaks refined sub-cell: Cs RMS 0.17 mm;
  Co-60 keeps a systematic −1.4 mm y bias at (−15, −8) → TODO-17 (cause found: undersampling; see its completed entry above) — [PLAN.Studio.ImagingOptions](PLAN.Studio.ImagingOptions.md).
- **TODO-06 Studio workspace shell + `PlotView`** (2026-10-01, `bb3f00e`; desktop-verified the same day): workspaces,
  first-party plot (10 M samples, CPU redraw ≤ 12 ms), polish survey with issues P-01 … P-11 for the author —
  [PLAN.Studio.Shell](PLAN.Studio.Shell.md), `docs/assets/studio-polish-survey/README.md`.
- **1–11**: geometry & localization, FOV÷resolution=rank, cyclic-ghost, directional biasing, noise
  threshold, tungsten leakage, optimal thickness, crystal uniformity, detector array (Nyquist),
  crystal materials, RTL peak-detector, SiPM+ADC front-end.
- **12–17**: front-end energy trust (ballistic deficit), charge integration + pile-up rejection,
  multi-isotope + ADC dynamic range, crystal Compton (Argmax strategy + spatial separation),
  Compton stripping (spectral lever), combined per-pixel stripping inside the coded pipeline.
- **18–21**: source-distance (z) refocusing, depth-under-noise + joint lateral/depth, mask channel
  geometry (holes / focused channels), optimal mask size (cell pitch + open fraction).
- **22**: handheld productization — weight/volume/form, MC validation (2.45× sensitivity),
  camera–mask parallax, DAQ thermal + motion, SiPM gain thermal drift + stabilization ①②③, optimal
  5-sided shield thickness.
- **23**: mask-geometry follow-ups — tapered (hourglass) channels (wide-FOV fix for thick masks,
  edge/center 0.82→0.99 at ~4°), empirical open-fraction with random arrays (ρ≈0.5, MURA ~4× cleaner).
- **24**: full 3D (x,y,S) joint depth search (peak-prominence GLRT) — removes the alternating
  iteration's near-field coupling trap (lateral RMS 3.6→0.37 mm); far field stays physics-limited.
- **25**: trapezoidal shaper (Jordanov-Knoll) in RTL + first cocotb co-sim (bit-exact vs reference,
  TESTS=2 PASS=2); flat-top energy, pole-zero baseline restoration, pile-up separation.
- **51** (2026-10-01): Ir-192 reference source (RS-1) — ENSDF lines, NIST tungsten points 200–600 keV, cascade
  explicitly not modelled; RS-1 per-photon efficiency vs the URS estimate in theme 52.
- **52** (2026-10-01): crystal attenuation from tabulated cross sections (`CrystalMaterial`, 7 scintillators) —
  replaced fitted curves that made the crystal over-absorbing; themes 6, 15, 17, 26, 28, 37, 44, 46 re-run
  (spatial Cs/Co separation now holds only to Co:Cs ≈ 2:1); single `source` with `lines` now emits all lines.
  TODO-03 addendum: the PRS † rows re-measured (handheld floor 0.24 mm, mixed field unchanged, Argmax 2.0× per-pixel).
- **53** (2026-10-01): field of view at field distance (`montecarlo fov`, TODO-04) — non-cyclic usable field ≈ ±7° along
  x (±4–6.5° with background), wrong in-field answers past ~7.5° caught by the flood-centroid "outside" flag, side cue
  1–14.5°, no direction information past ~14°.
- **54** (2026-10-01): dose rate from the detector spectrum (`montecarlo dose`, TODO-05) — ICRP 74 truth, fitted G(E)
  within ±13 % frontally (all reference sources), but the collimating mask reads 0.1–0.7 of the dose 10° off axis;
  paralyzable over-range with live-time correction to ~150 mSv/h and the live fraction as the over-range signature.
- **Test harness** (2026-10-01): closed-form invariants for every transport stage (sampler moments, config
  round-trip and every shipped scenario, decoder vs an analytic shadow, biased-source solid angle, crystal slant
  stopping, mask open fraction + leak, Compton energy conservation, determinism / progress / cancellation), k·σ
  statistical assertions, shared rigs from `samples/`, and `tests/Gcam.Studio.Services.Tests` for the service layer
  against the real engine. A mutation check (Poisson off-by-one, half-pixel flood origin, crystal slant path ignored)
  fails each — the last one was not caught by the earlier suite.
- **V&V fold-in** (2026-10-01): the TODO-04 / TODO-05 hand-overs moved into the V&V set — evidence register
  `VV.Gcam.Evidence` (EV-01 … EV-33, every theme a VV row cites), PR-IMG-08 / -10, PR-SAFE-01, PR-SENS-05 re-graded
  to MC, LIM-01 / -07 updated, LIM-09 (frontal-only dose reading, decision open) added; one-page `VV.Gcam.Overview`;
  the VV documents no longer link to `AGENTS.*` (D-35).
