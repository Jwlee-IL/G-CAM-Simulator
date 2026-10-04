# VV.Gcam.Limitations — known limitations of the Gcam locator concept, their fixes and what the fixes cost

Scope: limitations of the hand-held **scintillator coded-aperture** locator concept specified in
[VV.Gcam.PRS](VV.Gcam.PRS.md) (GAGG:Ce,Mg 16 × 16 @ 1 mm, rank-7 MURA, 1 mm cells, 10 mm W, D = 55 mm). Each entry
says what the user experiences, why it happens, the ways to fix it and the price of each. Not covered: limitations of
GCAM Studio as software ([VV.Studio §6](VV.Studio.md#6-known-anomalies-gaps-and-risks)).

**At a glance**
- 9 limitations; 3 rated **high**: the narrow field of view (LIM-01), ghosts before out-of-field cues (LIM-07) and
  limited spatial isotope separation (LIM-08).
- Three fixes are chosen (non-cyclic decoding, the software path for the field of view, a separate dose counter for
  LIM-09); LIM-02 is accepted.
- Every number cites its evidence (`EV-xx`, [VV.Gcam.Evidence](VV.Gcam.Evidence.md)); *(AN)* marks a geometric or
  analytical estimate, *(MC)* a Monte Carlo result.

Decisions referred to as D-xx are recorded with their reasons in [VV.Gcam.Decisions](VV.Gcam.Decisions.md).

**Comparison class.** Gcam is a scintillator coded-aperture camera. It is compared only with coded-aperture
imagers — the closest published one is Mirion iPIX (coded aperture, CdTe pixels). Compton cameras (H3D Polaris-H)
and HPGe imagers (PHDS GeGI) work on different principles — omnidirectional fields of view, different energy
ranges and sensitivities — and are not used as pass criteria ([URS §5](VV.Gcam.URS.md#benchmark-imagers-and-their-test-conditions)).

| ID | Limitation | Affects | Severity |
|---|---|---|---|
| LIM-01 | Narrow field of view — software path adopted (D-16) | UN-01, UN-12 | **high** |
| LIM-07 | Ghosting must be solved before an out-of-field cue can be trusted | UN-11, UN-12 | **high** |
| LIM-08 | Spatial isotope separation is limited (Co : Cs ≈ 2 : 1 in a 662 keV window) | UN-03 | **high** |
| LIM-09 | The imaging head reads dose only frontally — separate dose counter chosen (D-37) | UN-14 | medium |
| LIM-02 | Imaging stops in high fields (~10 mSv/h at the device) — accepted | UN-14 | low |
| LIM-03 | Gain stability over the full temperature range without a built-in reference source | UN-06 | medium |
| LIM-04 | Nuclide labelling only against known lines | UN-02 | medium |
| LIM-05 | Co-60-class background cannot be shielded within the mass budget | UN-03 | low (handled in software) |
| LIM-06 | Limits of the evidence itself | all performance rows | medium |

---

## LIM-01 — Narrow field of view

**What the user sees.** The device images only what lies within about **±3.6°** of where it points (fully-coded
field, 7.3° wide). Non-cyclic decoding stretches the usable field to about **±6–7.5°** along the axes, depending on
the count level *(MC, EV-02)*;
beyond that a source is only partially coded and the decoder answers with a wrong spot inside the field, and past
~14–15° the aperture's shadow leaves the detector and the source is not imaged at all. For scale: iPIX, the same
imaging principle, specifies 41–49°.

**Why** *(AN, from the geometry)*

| Cause | Numbers |
|---|---|
| Field of view ÷ angular resolution = rank (EV-03) | angular resolution ≈ cell / D = 1 mm / 55 mm ≈ **1.04°**; fully-coded field ≈ rank × that ≈ **7.3°**. Good resolution and a narrow field are the same design choice at rank 7. |
| A thick mask collimates | 10 mm tungsten with 1 mm holes is a 10 : 1 channel; rays more than ~atan(1/10) ≈ **5.7°** off its axis are clipped (EV-04: edge / centre efficiency 0.80 at 8 mm, 0.30 at 32 mm in the wide-field test geometry, D = 20 mm; a 25 mm mask's is 0.82, EV-05). The 10 mm is needed to stop 662 keV – 1.3 MeV. |
| Partially-coded field | detector 16 mm + mask 14 mm over D = 55 mm → outer limit ≈ ±15°, where only part of the mask shadow lands on the detector. |

**Ways to widen it, and what each costs**

| Option | How | Field gained | Cost |
|---|---|---|---|
| A. Scan / panorama | sweep by hand with the IMU de-rotation and VIO already planned (PR-ENV-04), or a pan-tilt head, and stitch tiles | any | **time** ≈ number of tiles: a 45° × 45° room view is ~10–14 pointings at the ±6–7.5° usable field; sources near tile edges are partially coded; a pan-tilt head adds mass and setup |
| B. Higher rank, same cell and D | rank 13 or 23 MURA | ×13/7 ≈ 1.9 or ×23/7 ≈ 3.3, resolution unchanged | the detector must hold ~2 pixels per cell shadow over a period (EV-06): ~26 × 26 or ~46 × 46 px at 1 mm instead of 16 × 16. Bigger crystal → bigger **shield** (the master weight: a 12 → 20 mm detector already moves ~2.7 → 3.4 kg, EV-24); 3–8× the readout channels, power and heat (256 pixel channels are already ~12 W, EV-27); collapses if the detector cannot hold one shadow period (EV-03) |
| C. Shorter D or larger cells | e.g. D = 27 mm | ×2 | angular resolution **worse by the same factor** (field ÷ resolution stays 7); sensitivity rises (larger solid angle); depth-from-focus weakens (EV-33) |
| D. Thinner mask | e.g. 4–6 mm W | removes the ~5.7° collimation | more leakage at high energy → lower contrast for Co-60 / Cs-137 (~10 mm is the optimum at 662 keV, EV-04) |
| E. Tapered (bevelled) channels | flare the hole walls toward both faces | thick-mask field close to a thin mask's (edge / centre 0.82 → 0.99 at ~4° bevel, EV-05) | harder machining of tungsten (fabrication σ ≲ 40 µm still required, EV-30); less tungsten near the faces, so not full 10 mm opacity everywhere; the model is an idealised upper bound |
| F. Use the partially-coded field | decode ±15° with a finite-mask model or MLEM (EV-11) | ±15° | lower signal-to-noise and artefacts at the edge; the plain non-cyclic decoder answers wrongly past ~7.5° (EV-02); an MLEM forward model of the edge is **not quantified** |
| G. Out-of-field cue (not imaging) | read the side from the flood centroid: the lit side of the array is opposite the source | none (direction hint only) | meets UN-12, not UN-01; no hardware. Correct side from ~1° to ~14.5°, "outside" flag ≥ 90 % at 4–12.5°; needs a background estimate (EV-02) |

**Combinations that fit the concept.** A + G keeps the hardware and the 1° resolution and pays in search time;
B + E buys a ~25–45° field in hardware and pays in mass, power and cost; C trades resolution for field one-for-one.

**Adopted path (author, 2026-10-01 — D-16).** With non-cyclic decoding chosen (LIM-07) and moving hardware ruled out
on cost, the software-only path:

1. *Usable field from non-cyclic decoding:* **±6–7.5° along the axes** without background (7.0° at 1 m and 7.5° at 5 m with 5000 on-axis counts; 6.0° / 7.0° with 500), the same at 1 m and 5 m; ±5–6.5° with background equal to the signal; on the diagonal at low counts with background it is worse
   than cyclic decoding *(MC, EV-02)*. A usable-field claim needs a count level and a direction. Under a natural
   terrestrial field (0.05–0.20 µSv/h, EV-02 / EV-34) the 662 keV window keeps it within one 0.5° step of its ideal;
   counting every deposit with an unshielded crystal, a source giving 500 counts on axis loses most of it (non-cyclic
   0–4° at 60 s along x).
2. *Option G — out-of-field cue:* the flood centroid tells the user which way to turn and flags the wrong in-field
   answers past ~7° *(MC, EV-02)*.
3. *Option A — hand sweep with IMU / VIO stitching* (no pan-tilt head): ~10–14 pointings for a 45° × 45° view, spaced
   ≲ 28° apart so a source is always within the ~14° where the cue works.

Cost: search time, IMU / VIO software and its validation; no change to mass or power. **B + E stays the hardware
fallback** if field trials show the search takes too long.

**Status.** Software path adopted (PR-IMG-10, grade DEC). Evidence: steps 1 and 2 are simulated at field distance
(EV-02); step 3 — the sweep and its stitching — is not. Options B and E have MC evidence at the lab geometry
(EV-03, EV-05).

---

## LIM-07 — Ghosting comes before out-of-field cues

**Author's condition on UN-12 (2026-10-01).** The characteristic failure of a scintillator coded-aperture camera is
the **ghost**: a source outside the fully-coded field aliases to a false spot on the opposite side (EV-01). Until
ghosts are reliably suppressed, a "source outside the field — turn left" cue (PR-IMG-08) and a "no source" verdict
(PR-IMG-07) cannot be trusted, because a ghost and a real in-field source look alike.

| Option | Cost |
|---|---|
| **Chosen (2026-10-01):** non-cyclic (finite-mask) decoding — the default (PR-IMG-01) | it **moves** the ghost boundary from 3.6° to ~7°, it does not remove it: at 7.5–11° off axis 50–90 % of acquisitions return a wrong spot inside the field (EV-02). The flood centroid flags most of those (≤ 7 % of acquisitions unflagged without background), but with background equal to the signal up to 29–49 % go unflagged. Under an absolute terrestrial field (EV-02, EV-34) the 662 keV window keeps the flag working (unflagged wrong spot ≤ 0.28 at 1 m, N0 500, up to 0.20 µSv/h); counting every deposit in the bare-crystal bound the flag stops working from background ≈ 0.7 × the on-axis source counts — 0.10 µSv/h × 10 s for a source giving 500 counts on axis. Outside the fully coded field the cyclic ghost persists under the field; it is not turned into correct answers |
| *Rejected (cost, mechanical stability):* mask / antimask (rotating the mask, as iPIX does) | a rotation mechanism — its cost and mechanical stability outweigh the gain (D-19). The gain is real: at equal total time the antimask has ~20–30 % lower RMS than a calibrated background subtraction at every background level, both sub-mm through 4 background counts per pixel (EV-12); neither helps against a directional coded interferer |
| MLEM with a full forward model including the partially-coded field (EV-11) | computation; the model must be right at the edges |
| Cross-check with the energy histogram (author, UN-11) | needs a photopeak; does not tell *where* |

**Status.** Non-cyclic decoding chosen, mask rotation rejected (author, 2026-10-01; basis restated 2026-10-02 after the seed-ensemble measurement, D-19). Remaining risk: the centroid
flag that makes non-cyclic answers past ~7° safe to reject needs a background estimate and, under a natural field, an
energy window (measured between a bare and a fully shielded head, EV-34 — the real housing lies between), and
several sources at once are not studied. Under the absolute field the mask / antimask ranking at equal time is
unchanged (EV-12). PR-IMG-08 stays dependent on PR-IMG-01; LIM-01 option G inherits this condition.

## LIM-08 — Spatial isotope separation is limited

**What the user sees.** With a strong Co-60 source in the scene, a weaker Cs-137 source is lost in the 662 keV
image once Co-60 is about **4× stronger** (holds at 2× in 126 / 128 seeds); in an equal-photon-budget demonstration Co-60 downscatter
makes up ~55 % of the 662 keV window in 1 mm GAGG pixels *(MC, EV-15)*.

**What still works.** Per-pixel Compton stripping recovers the co-located Cs-137 count (0–1 % error with an oracle ratio, EV-15) —
the spectral lever; it needs a calibrated downscatter / photopeak ratio and known lines. Re-measured at absolute
activities (Cs-137 1 MBq with Co-60 8 or 2 MBq, 60 s, lab distance) with a natural terrestrial field added, the
picture is the same and the field has no measurable effect (EV-15, EV-34).

| Option | Cost |
|---|---|
| Rely on stripping before decoding (PR-NRG-04) | calibration of the downscatter ratio per geometry; not blind |
| Larger pixels or thicker crystal (more full-energy single-pixel events) | coarser sampling (EV-06) or more mass and depth-of-interaction parallax (EV-13) |
| Better energy resolution (CeBr3 / LaBr3, EV-19, EV-17) | price, hygroscopic crystal |
| Multi-pixel event reconstruction (summing Compton-split events) | already the positioning strategy (PR-NRG-03: ~2× the counts of per-pixel windowing, EV-14); it recovers counts, not the 662 keV window's purity |

This bears directly on UN-03 — the classic Co-60 → Cs-137 downscatter problem of this camera class — which is solved in counts
(stripping) but only partly in position.

## LIM-09 — The dose reading holds only frontally

**What the user sees.** With the source in front, the dose rate read from the detector spectrum is within ±13 % at
every energy 50–1500 keV and for every reference source. Turned away from the source, the reading falls: 10° off
axis it shows 0.11 (60 keV) to 0.66 (1250 keV) of the true dose, and past 20° almost nothing at low energy
*(MC, EV-23)*. A user standing in a field that comes from the side reads far less than the dose where they stand.
NSS-1 tests frontal incidence only, so a frontal type test would pass while the field reading fails.

**Why.** The dose is read through the imaging head, and the imaging head is a collimator: a 10 mm mask with 1 mm
holes accepts ~atan(1/10) ≈ 6°, and the shield blocks the rest. The model has the front plate only, so a real head
with side walls reads lower still.

| Option | Cost |
|---|---|
| A. A small unshielded dose sensor beside the head (e.g. a small scintillator or silicon diode with its own window) | one more channel, its own energy compensation and its own calibration under PR-REG-02; a few grams; the imaging head no longer gives the dose |
| B. Side-looking crystal(s) in the shield | openings in the shield admit background into the imaging channel; more channels; mass |
| C. State the dose as frontal-only and label it | no hardware; but UN-14 ("the dose rate where I stand") is not met for side fields, and the user must point the device to read the dose |

**Status.** **Option A chosen (author, 2026-10-01 — D-37):** a small counter, cheap and outside the shield, read
over a data I/O link (PR-SAFE-02). Remaining risk: the counter is not yet chosen, so its energy and angular
response, dead time and over-range behaviour (PR-SENS-05) are unverified; they come from its data sheet and a type
test, not from Gcam.

## LIM-02 — Imaging stops in high fields

**Accepted (author, 2026-10-01):** at fields high enough to saturate the front end, the user's alarming personal
dosimeter sends them back before imaging matters. What remains required is that the **dose-rate reading never
under-reads**: over-range indication or continuous alarm from 10 mSv/h to 1 Sv/h (NSS-1 §6.4.8; PR-SENS-05). The
simulation shows how: live-time correction holds the reading to ~150 mSv/h, and beyond that the live fraction itself
is the over-range signature *(MC, EV-23)*. The options below are kept for reference.

**What the user sees.** Near a strong source — around 10 mSv/h at the device, 0.3–1 Mcps detected *(AN)* — the
front end saturates (~1 Mcps limit; plain GAGG collapses earlier, EV-21). iPIX images to 10 Sv/h with a small CdTe
pixel detector.

| Option | Cost |
|---|---|
| Report saturation and tell the user to step back (PR-SENS-05) | none in hardware; the user loses imaging close in |
| Faster crystal / narrower shaping (CeBr3, shorter shaping time, EV-21, EV-17) | price, hygroscopic crystal, resolution against rate |
| Smaller active area or an attenuator in front | lower sensitivity for weak sources (RC-1) |

## LIM-03 — Gain stability without a built-in reference source

**Decision (2026-10-01): no built-in radioactive source** — cost and handling, and it would make the device a
*radiation device* under 원자력안전법 제60조 ([PRS §6](VV.Gcam.PRS.md#6-applicable-laws-and-regulations)).

**What the user sees.** Over the operating range (−10 to +45 °C, 55 °C span — iPIX level, decided 2026-10-01) the
energy window can walk: temperature-compensated bias holds a ±10 % window to ΔT ≈ 25 °C, bias + LED pulser to
ΔT ≈ 36 °C *(AN, EV-28)*; the crystal's own light-yield drift (~−0.15 %/°C) is invisible to both and only a spectral
reference catches it.

| Option | Cost |
|---|---|
| **Chosen (2026-10-01), with window widening:** track a known photopeak in the measured spectrum (the scene's own Cs-137 / Co-60 / Ir-192 line) | works only when such a line is present and identified; nothing to track in a background-only search |
| Periodic check with the user's own (licensed) check source | a user step; the site must hold a source |
| **Chosen (2026-10-01), with peak tracking:** temperature-scheduled window widening at the extremes | lower energy selectivity (more downscatter in the window, UN-03) |
| Thermal control of the crystal block | power and mass |

## LIM-04 — Nuclide labelling only against known lines

Separation and stripping need the lines declared in advance (EV-15, EV-16, EV-10). A full library over 30 keV –
3 MeV, as hand-held identifiers do, is not in the concept; GAGG's ~6–7 % resolution at 662 keV (EV-17) also limits
close lines. Fix: a library-based identifier on the summed spectrum — costs software, validation, and is still
limited by resolution.

## LIM-05 — Co-60-class background cannot be shielded

Shielding 1.25 MeV background takes 25–30 mm W, 6.8–9.8 kg of shield *(MC, EV-25: 25 mm in 83 / 128 seeds, 30 mm in 45)*; the concept shields to ~8 mm and handles
high-energy background by coded separation and Compton stripping in software (EV-15). Cost of the alternative: mass
far beyond the 2.5 kg budget.

## LIM-06 — Limits of the evidence itself

- Localisation precision and sensitivity are simulated at the lab distance (source plane 100 mm from the mask);
  at field stand-off only the field of view (Cs-137, 1 m and 5 m, EV-02) and the dose response (1 m, EV-23) are.
- The field-of-view and dose studies model the shield's front plate only, no side or rear walls; oblique readings
  are upper bounds.
- **Ideal environment, and an ambient field measured only between bounds.** Results without a stated background have
  **no ambient (natural) background** — only the simulated sources, an ideal detector and known geometry; they are
  best-case bounds. A source-independent **terrestrial** field (K / U / Th from the soil, 0.05–0.20 µSv/h) has been
  measured for localisation, field of view, the trust gate, mask / antimask and isotope separation *(MC, EV-34;
  EV-01, EV-02, EV-07, EV-12, EV-15)*; the other results state only the expected direction
  ([VV.Gcam.Evidence §1](VV.Gcam.Evidence.md#ideal-conditions-and-background)). What those measurements leave open:
  - *No head housing:* each result is a range between a bare crystal exposed on all faces and a perfectly shielded
    head that admits the field through the mask only. The two ends differ widely — the bare bound pulls the decoder
    and erases the out-of-field cue at counts where the front-only bound changes nothing — so a field claim needs a
    model of the real shield walls and entrance.
  - *High energies:* the tungsten attenuation is clamped above 1332 keV and the crystal has no pair production, while
    29–54 % of the field's air kerma is carried by photons above 1332 keV (K-40 1461, Tl-208 2614 keV); the
    shielding of those lines is therefore not transport-grade.
  - *Decoder pull:* in the bare bound, counting every deposit, the plain decoder's answer jumps toward the
    background's own pattern once background exceeds about half the source counts (saturating at ~8–9 mm at the lab
    distance, ~19 mm at 1 m). The calibrated search statistic and background subtraction do not show it; removing it
    from the reported position needs a decoder that models the background's shape, which does not exist yet.
  - Real plant backgrounds, room scatter, cosmic and airborne-radon components, intrinsic crystal activity and
    shielded sources are not simulated; the older relative (BSR) studies remain stress tests.
- Nothing is compared with recordings of any real instrument, and nothing is measured on hardware.
- Monte Carlo numbers are spreads over seed ensembles of the simulation, not tolerances; results that use the
  studies' fixed manufactured patterns (mask machining errors, defect maps, gain maps) hold for those patterns, not
  for every manufactured part ([VV.Gcam.Evidence §1](VV.Gcam.Evidence.md#1-the-tools-behind-the-grades)).
- The engine's model changes that moved earlier numbers are listed in
  [VV.Gcam.Evidence §9](VV.Gcam.Evidence.md#9-model-history).
