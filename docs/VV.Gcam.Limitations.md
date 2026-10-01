# VV.Gcam.Limitations — known limitations of the Gcam locator concept, their fixes and what the fixes cost

Scope: limitations of the hand-held **scintillator coded-aperture** locator concept specified in
[VV.Gcam.PRS](VV.Gcam.PRS.md) (theme-22 head: GAGG:Ce,Mg 16 × 16 @ 1 mm, rank-7 MURA, 1 mm cells, 10 mm W,
D = 55 mm). Each entry says what the user experiences, why it happens, the ways to fix it and the price of each.
Not covered: limitations of GCAM Studio as software ([VV.Studio §6](VV.Studio.md#6-known-anomalies-gaps-and-risks)).

Decisions referred to as D-xx are recorded with their reasons in [VV.Gcam.Decisions](VV.Gcam.Decisions.md).
Numbers marked *(AN)* are geometric or analytical estimates; *(MC)* are Gcam Monte Carlo results; theme numbers
refer to [AGENTS.Findings](AGENTS.Findings.md).

**Comparison class.** Gcam is a scintillator coded-aperture camera. It is compared only with coded-aperture
imagers — the closest published one is Mirion iPIX (coded aperture, CdTe pixels). Compton cameras (H3D Polaris-H)
and HPGe imagers (PHDS GeGI) work on different principles — omnidirectional fields of view, different energy
ranges and sensitivities — and are not used as pass criteria ([URS §5](VV.Gcam.URS.md#benchmark-imagers-and-their-test-conditions)).

| ID | Limitation | Affects | Severity |
|---|---|---|---|
| LIM-01 | Narrow field of view — software path adopted (D-16) | UN-01, UN-12 | **high** |
| LIM-07 | Ghosting must be solved before an out-of-field cue can be trusted | UN-11, UN-12 | **high** |
| LIM-02 | Imaging stops in high fields (~10 mSv/h at the device) — accepted | UN-14 | low |
| LIM-03 | Gain stability over the full temperature range without a built-in reference source | UN-06 | medium |
| LIM-04 | Nuclide labelling only against known lines | UN-02 | medium |
| LIM-05 | Co-60-class background cannot be shielded within the mass budget | UN-03 | low (handled in software) |
| LIM-06 | Evidence is from the lab geometry; some rows still quote the pre-theme-52 crystal model | all performance rows | medium |
| LIM-08 | Spatial isotope separation is limited (Co : Cs ≈ 2 : 1 in a 662 keV window) | UN-03 | **high** |

---

## LIM-01 — Narrow field of view

**What the user sees.** The device images only what lies within about **±3.6°** of where it points (fully-coded
field, 7.3° wide). Between ~4° and ~15° off-axis a source is only partially coded — weaker, with artefacts — and
beyond ~±15° it is not imaged at all. Because the decoder is non-cyclic (to suppress ghosts, PR-IMG-01), a source
outside the field does not show up as a false spot — it simply **is not there**, which a user can mistake for
"no source" (UN-11, UN-12). For scale: iPIX, the same imaging principle, specifies 41–49°.

**Why** *(AN, from the geometry)*

| Cause | Numbers |
|---|---|
| Field of view ÷ angular resolution = rank (theme 3) | angular resolution ≈ cell / D = 1 mm / 55 mm ≈ **1.04°**; fully-coded field ≈ rank × that ≈ **7.3°**. Good resolution and a narrow field are the same design choice at rank 7. |
| A thick mask collimates | 10 mm tungsten with 1 mm holes is a 10 : 1 channel; rays more than ~atan(1/10) ≈ **5.7°** off its axis are clipped (theme 4; theme 23 measured edge/centre efficiency 0.82 on a thick mask). The 10 mm is needed to stop 662 keV – 1.3 MeV. |
| Partially-coded field | detector 16 mm + mask 14 mm over D = 55 mm → outer limit ≈ ±15°, where only part of the mask shadow lands on the detector. |

**Ways to widen it, and what each costs**

| Option | How | Field gained | Cost |
|---|---|---|---|
| A. Scan / panorama | sweep by hand with the IMU de-rotation and VIO already planned (PR-ENV-04), or a pan-tilt head, and stitch tiles | any | **time** ≈ number of tiles: a 45° × 45° room view is ~(45 / 7)² ≈ 40 pointings; sources near tile edges are partially coded; a pan-tilt head adds mass and setup |
| B. Higher rank, same cell and D | rank 13 or 23 MURA | ×13/7 ≈ 1.9 or ×23/7 ≈ 3.3, resolution unchanged | the detector must hold ~2 pixels per cell shadow over a period (theme 7): ~26 × 26 or ~46 × 46 px at 1 mm instead of 16 × 16. Bigger crystal → bigger **shield** (the master weight, theme 22: a 12 → 20 mm detector already moves ~2.7 → 3.4 kg); 3–8× the readout channels, power and heat (256 pixel channels are already ~12 W); collapses if the detector cannot hold one shadow period (theme 3) |
| C. Shorter D or larger cells | e.g. D = 27 mm | ×2 | angular resolution **worse by the same factor** (FOV ÷ resolution stays 7); sensitivity rises (larger solid angle); depth-from-focus weakens |
| D. Thinner mask | e.g. 4–6 mm W | removes the ~5.7° collimation | more leakage at high energy → lower contrast for Co-60 / Cs-137 (theme 4: 8–10 mm optimum at 662 keV) |
| E. Tapered (bevelled) channels | flare the hole walls toward both faces | thick-mask field close to a thin mask's (theme 23: edge/centre 0.82 → 0.99 at ~4° bevel) | harder machining of tungsten (fabrication σ ≲ 40 µm still required, theme 38); less tungsten near the faces, so not full 10 mm opacity everywhere; the model is an idealised upper bound |
| F. Use the partially-coded field | decode ±15° with a finite-mask model or MLEM (theme 48) | ±15° | lower signal-to-noise and artefacts at the edge; **not quantified** in Gcam yet |
| G. Out-of-field cue (not imaging) | compare counts that the coded field cannot explain, or side-shield counts, to say "source to the left / right" | none (direction hint only) | meets UN-12, not UN-01; cheap in hardware; detection threshold not studied |

**Combinations that fit the concept.** A + G keeps the hardware and the 1° resolution and pays in search time;
B + E buys a ~25–45° field in hardware and pays in mass, power and cost; C trades resolution for field one-for-one.

**Adopted path (author, 2026-10-01 — D-16).** With non-cyclic decoding chosen
(LIM-07) and moving hardware ruled out on cost, the software-only path is the natural first step:

1. *Usable field from non-cyclic decoding.* At the theme-22 geometry, non-cyclic decoding localised 360 / 625 grid
   positions against 171 / 625 cyclic (≈ the fully-coded area) — about **2.1× the area, ~1.45× the width**, i.e. a
   usable field of roughly **±5°** instead of ±3.6° *(MC area ratio at the lab geometry → AN angle)*.
2. *Option G — out-of-field cue*, now possible because non-cyclic decoding does not fold outside sources into
   ghosts: tells the user which way to turn, so the sweep is directed instead of a raster.
3. *Option A — hand sweep with IMU / VIO stitching* (no pan-tilt head): a 45° × 45° view is ~(45 / 10.5)² ≈ **18
   pointings** instead of ~40, fewer when G points the way.

Cost: search time, IMU / VIO software and its validation; no change to mass or power. **B + E stays the
hardware fallback** if field trials show the search takes too long. Evidence still missing: MC of the usable
field at field distance with background (the ±5° is an estimate), and any study of G.

**Status.** Software path adopted (PR-IMG-10, grade DEC); B + E is the documented hardware fallback. Options B and E
have MC evidence at the lab geometry (themes 3, 23); A, F and G have none yet — the ±5° usable field and the
out-of-field cue are the first things to simulate.

---

## LIM-07 — Ghosting comes before out-of-field cues

**Author's condition on UN-12 (2026-10-01).** The characteristic failure of a scintillator coded-aperture camera is
the **ghost**: a source outside the fully-coded field aliases to a false spot on the opposite side (theme 2). Until
ghosts are reliably suppressed, a "source outside the field — turn left" cue (PR-IMG-08) and a "no source" verdict
(PR-IMG-07) cannot be trusted, because a ghost and a real in-field source look alike.

| Option | Cost |
|---|---|
| **Chosen (2026-10-01):** non-cyclic (finite-mask) decoding — the default (PR-IMG-01) | evidence is at the lab geometry only (theme 22: 360 / 625 vs 171 / 625 positions localised); not verified at field distance, with background, or with several sources |
| *Rejected (cost):* mask / antimask (rotating the mask, as iPIX does) | a second exposure (time) and a rotation mechanism; helps against common-mode background, not against a directional coded interferer (theme 5, 28) |
| MLEM with a full forward model including the partially-coded field (theme 48) | computation; the model must be right at the edges |
| Cross-check with the energy histogram (author, UN-11) | needs a photopeak; does not tell *where* |

**Status.** Non-cyclic decoding chosen, mask rotation rejected (author, 2026-10-01). Remaining risk: it is verified
only at the lab geometry, so PR-IMG-08 stays dependent on PR-IMG-01 holding at field conditions; LIM-01 option G
inherits this condition.

## LIM-08 — Spatial isotope separation is limited

**What the user sees.** With a strong Co-60 source in the scene, a weaker Cs-137 source is lost in the 662 keV
image once Co-60 is about **4× stronger** (holds at 2×); Co-60 downscatter makes up ~55 % of the 662 keV window in
1 mm GAGG pixels *(MC, theme 52)*. The earlier, stronger result (Cs found even at Co × 8) rested on a crystal model
that absorbed too much and too photoelectrically.

**What still works.** Per-pixel Compton stripping recovers the co-located Cs-137 count (0–4 % error, theme 52) —
the spectral lever survives; it needs a calibrated downscatter / photopeak ratio and known lines.

| Option | Cost |
|---|---|
| Rely on stripping before decoding (PR-NRG-04) | calibration of the downscatter ratio per geometry; not blind |
| Larger pixels or thicker crystal (more full-energy single-pixel events) | coarser sampling (theme 7) or more mass and parallax (theme 50) |
| Better energy resolution (CeBr3 / LaBr3, themes 6, 32) | price, hygroscopic crystal |
| Multi-pixel event reconstruction (summing Compton-split events, theme 15 Argmax) | its numbers must be re-run on the new crystal model (PR-NRG-03 †) |

This bears directly on UN-03 — the original rig's unsolved Co-60 → Cs-137 problem — which is solved in counts
(stripping) but only partly in position.

## LIM-02 — Imaging stops in high fields

**Accepted (author, 2026-10-01):** at fields high enough to saturate the front end, the user's alarming personal
dosimeter sends them back before imaging matters. What remains required is that the **dose-rate reading never
under-reads**: over-range indication or continuous alarm from 10 mSv/h to 1 Sv/h (NSS-1 §6.4.8; PR-SENS-05).
The options below are kept for reference.

**What the user sees.** Near a strong source — around 10 mSv/h at the device, 0.3–1 Mcps detected *(AN)* — the
front end saturates (theme 10: ~1 Mcps limit; plain GAGG collapses earlier). iPIX images to 10 Sv/h with a small
CdTe pixel detector.

| Option | Cost |
|---|---|
| Report saturation and tell the user to step back (PR-SENS-05) | none in hardware; the user loses imaging close in |
| Faster crystal / shaping (CeBr3, shorter shaping time, theme 10, 31) | price, hygroscopic crystal, resolution vs rate trade |
| Smaller active area or an attenuator in front | lower sensitivity for weak sources (RC-1) |

## LIM-03 — Gain stability without a built-in reference source

**Decision (2026-10-01): no built-in radioactive source** — cost and handling, and it would make the device a
*radiation device* under 원자력안전법 제60조 ([PRS §6](VV.Gcam.PRS.md#6-applicable-laws-and-regulations)).

**What the user sees.** Over the operating range (−10 to +45 °C, 55 °C span — iPIX level, decided 2026-10-01) the
energy window can walk:
temperature-compensated bias holds a ±10 % window to ΔT ≈ 25 °C, bias + LED pulser to ΔT ≈ 36 °C (theme 22,
*AN*); the crystal's own light-yield drift (~−0.15 %/°C) is invisible to both and only a spectral reference
catches it.

| Option | Cost |
|---|---|
| **Chosen (2026-10-01), with window widening:** track a known photopeak in the measured spectrum (the scene's own Cs-137 / Co-60 / Ir-192 line) | works only when such a line is present and identified; nothing to track in a background-only search |
| Periodic check with the user's own (licensed) check source | a user step; the site must hold a source |
| **Chosen (2026-10-01), with peak tracking:** temperature-scheduled window widening at the extremes | lower energy selectivity (more downscatter in the window, UN-03) |
| Thermal control of the crystal block | power and mass |

## LIM-04 — Nuclide labelling only against known lines

Separation and stripping need the lines declared in advance (themes 14–17, 26). A full library over 30 keV – 3 MeV,
as hand-held identifiers do, is not in the concept; GAGG's ~6–7 % resolution at 662 keV also limits close lines.
Fix: a library-based identifier on the summed spectrum — costs software, validation, and still limited by
resolution.

## LIM-05 — Co-60-class background cannot be shielded

Shielding 1.25 MeV background takes ~30 mm W, ~11 kg (theme 22, *MC*); the concept shields to ~8 mm and handles
high-energy background by coded separation and Compton stripping in software (themes 15–17). Cost of the
alternative: mass far beyond the 2.5 kg budget.

## LIM-06 — Evidence limits

- All MC localisation numbers are at the lab geometry (S = 100 mm); field stand-offs are not simulated.
- The crystal model was replaced by tabulated GAGG cross sections on 2026-10-01 (theme 52, TODO-02 done). The URS
  §5 estimates now agree with MC within 2 % for Cs-137 and ~7 % low for Ir-192. PRS rows marked † (PR-IMG-02,
  PR-IMG-03, PR-NRG-03) still quote pre-theme-52 numbers and need re-running.
- No dose model exists, so PR-SAFE-01 (dose rate) has no simulation evidence.
