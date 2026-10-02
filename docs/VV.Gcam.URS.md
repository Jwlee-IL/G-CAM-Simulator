# VV.Gcam.URS — user requirements for a Gcam gamma-source locator

Scope: what the users of a **hand-held coded-aperture gamma-source locator** need, and the conditions they use it
in. This is the top of the requirement chain URS → [PRS](VV.Gcam.PRS.md) → [SRS](VV.Studio.SRS.md) →
[SDS](VV.Studio.SDS.md) → [VV](VV.Studio.md). How the needs are met is in the PRS and below; needs here are kept
free of solutions.

**At a glance**
- **19 user needs** of three users — a radiation-safety technician, a non-specialist handed the device, and the
  instrument engineer: 14 confirmed, 4 conditional, 1 withdrawn, all reviewed with the author on 2026-10-01.
- The needs start from known problems of fixed, pixelated-scintillator coded-aperture cameras (§1): moving the
  isotope by hand, high-energy downscatter read as a lower line, temperature drift, ghosts.
- Use conditions come from standards, relaxed to the level of the same-class product, iPIX (§4); reference sources
  are real industrial ones — Ir-192 and Co-60 radiography, Cs-137 and Co-60 gauges (§5).

> **What this is.** A personal design exercise by the author, made for this portfolio repository — not any company's
> product plan or roadmap. The needs, use conditions and decisions come from published standards, published data of
> same-class products and this simulator's results; no former employer's documents, data or plans were used.

> **Status: reviewed — every need decided on 2026-10-01 (confirmed, conditional or withdrawn); the reasons are in
> [VV.Gcam.Decisions](VV.Gcam.Decisions.md).** No user study exists. The needs were reconstructed from the known problems of
> this camera class (§1) and from the simulation results behind the concept
> ([VV.Gcam.Evidence](VV.Gcam.Evidence.md)), then reviewed with the author on 2026-10-01. Rows marked *to confirm*
> were hypotheses until that review. Use-condition values (§4) come from published standards, not from Gcam. Gcam is a
> simulator and a **design-only** product concept ([PRS §2](VV.Gcam.PRS.md#2-product-concept)); no regulatory claim is made.

## 1. Background: the camera class Gcam simulates

Gcam simulates a fixed, lab-mounted coded-aperture camera: a rank-7 tungsten MURA mask over a 12 × 12 pixelated
scintillator array read by SiPMs, with the source position overlaid on an optical image. Cameras of this class share
known problems (coded-aperture and scintillator literature):

| Known problem of the camera class | Needs it leads to |
|---|---|
| Characterising a camera means moving an isotope by hand across the field | UN-08 (the reason Gcam exists) |
| Downscatter of a higher-energy line (e.g. Co-60) lands in a lower line's window (e.g. 662 keV) and reads as that isotope | UN-03 |
| SiPM gain drifts with temperature, so a fixed energy window walks | UN-06 |
| Cyclic decoding aliases a source outside the coded field of view to a ghost on the opposite side | UN-11, UN-12 |
| A fixed mount with an on-board screen limits search use (*inferred*) | UN-04, UN-05 |

## 2. Users

| ID | User | Knows | Status |
|---|---|---|---|
| U1 | **Radiation-safety technician** (primary) — surveys plant areas and searches for lost, unknown or misplaced gamma sources | radiation protection, dose limits, survey meters, common radionuclides; *not* coded-aperture imaging | **confirmed** 2026-10-01 |
| U2 | **Non-specialist operator** — a plant worker, guard or first responder handed the device | how to follow a simple instruction and an alarm; no radiation-protection or imaging training | **confirmed** 2026-10-01 (the device must be usable by people who are not safety technicians) |
| U3 | **Instrument engineer** — designs, characterises and calibrates the device | the physics and the code | **confirmed** (repository purpose) |

U1 and U2 use the same device; anything U2 must do without help is a need of its own (UN-18).

## 3. User needs

Priority: **M** must, **S** should, **C** could. *Users* refers to §2.

### Finding and identifying sources

| ID | Need | Users | Prio | Origin | Status |
|---|---|---|---|---|---|
| UN-01 | See **where** a gamma source is relative to the visible scene, from where I stand, without walking towards it. | U1, U2 | M | camera use; EV-01 | **confirmed** 2026-10-01 |
| UN-11 | Not be shown a source that isn't there, and be told plainly when **no source** is found — checkable on the measured energy histogram (no photopeak above background). | U1, U2 | M | split from UN-01; ghosts (EV-01) | **confirmed** 2026-10-01 |
| UN-12 | Know when a source is **outside what the device currently sees**, and which way to turn to bring it in. | U1, U2 | M | review 2026-10-01; FCFOV limit | **conditional** 2026-10-01: only after ghosting is solved — a scintillator coded-aperture camera must first not produce ghosts, or an "outside the field" cue cannot be trusted |
| UN-02 | Know **which radionuclide** each located source is, at least among the radionuclides declared for the site; U2 needs a plain-language category, not a spectrum. | U1, U2 | M | review 2026-10-01 | **confirmed** 2026-10-01 |
| UN-03 | Get a weaker source's position and strength right even when a stronger, higher-energy source is nearby or at the same place. | U1 | M | Co-60 → Cs-137 downscatter (§1) | **confirmed** 2026-10-01 |
| UN-10 | Know roughly how far away a source is, to plan the approach. | U1 | **M** | depth work (EV-33); author raised the priority 2026-10-01 | **confirmed** 2026-10-01 |

### Safety

| ID | Need | Users | Prio | Origin | Status |
|---|---|---|---|---|---|
| UN-14 | Know the **dose rate** where I stand and be **warned** — audibly, visibly and by vibration — when it passes a level I set (or the site sets). | U1, U2 | M | review 2026-10-01; basic survey-meter function (§4) | **confirmed** 2026-10-01 |
| UN-13 | Be told when a result is **not yet reliable enough to act on**, with an *estimate* of how much longer to wait — shown as an estimate, never as a definite verdict. | U1, U2 | M | split from UN-07; count threshold | **conditional** 2026-10-01: the wait may be predicted, not asserted |

### Time and effort

| ID | Need | Users | Prio | Origin | Status |
|---|---|---|---|---|---|
| UN-07 | Get a location within a stated time from the sources people actually meet — industrial radiography and gauge sources (§5) — at a stated dose rate. | U1, U2 | M | search use | **conditional** 2026-10-01: accepted as a target if the field allows it — feasibility in a real plant (background, hand motion, narrow field) is unverified, see [PRS §4](VV.Gcam.PRS.md#4-what-the-user-needs-that-the-concept-does-not-yet-meet) |
| UN-04 | *Withdrawn* — was: carry and aim the device with one hand, wearing gloves, for a full search without tiring. | U1, U2 | — | hand-held concept | *withdrawn* 2026-10-01 (author: not needed as a need) |
| UN-17 | See **how much battery is left** (a remaining-capacity indicator). Run time is a design target, not a need. | U1, U2 | M | review 2026-10-01; §4 | **conditional** 2026-10-01: indicator only |
| UN-05 | Read the result **while aiming** (in bright light, with gloves), **and** view and control the device **from a distance** when it is set down near a source. | U1, U2 | M | author 2026-10-01 | **confirmed** 2026-10-01 |
| UN-19 | Keep my own distance and dose low while the device measures — set it down, step back, and still see the result. | U1, U2 | S | author 2026-10-01 (dockable display) | **confirmed** 2026-10-01 |
| UN-18 | Do a basic search **with minimal training**: switch on, point, follow a clear "source here / no source / move" cue and the safety alarm, with no settings to change. | U2 | M | author 2026-10-01 | **confirmed** 2026-10-01 |

### Durability and environment

| ID | Need | Users | Prio | Origin | Status |
|---|---|---|---|---|---|
| UN-06 | Keep working across the **temperature and humidity** of nuclear-plant and outdoor use without the user tending it (values §4). | U1, U2 | M | SiPM thermal drift (§1); author 2026-10-01 | **confirmed** (direction) 2026-10-01; values from standards |
| UN-15 | Survive **drops, knocks, rain or spray, dust** and surface decontamination wiping as met in plant use (values §4). | U1, U2 | M | author 2026-10-01 | **confirmed** (direction) 2026-10-01; values from standards |

### Records and engineering

| ID | Need | Users | Prio | Origin | Status |
|---|---|---|---|---|---|
| UN-16 | Keep a **record** of each search — time, place, picture, result, dose rate — for the survey report. | U1 | S | review 2026-10-01 | **confirmed** 2026-10-01 |
| UN-08 | Predict and sweep the instrument's behaviour (geometry, decoder, materials, front end) in software instead of on the bench. | U3 | M | why Gcam exists | **confirmed** (the repository's purpose, [README](../README.md)) |
| UN-09 | Inspect the raw detector image and the reconstruction, and measure on them in physical units. | U3 | M | GCAM Studio | **confirmed** ([VV.Studio §1](VV.Studio.md#1-scope-and-intended-use)) |

**19 needs**, all reviewed with the author on 2026-10-01: **14 confirmed** (UN-01, -02, -03, -05, -06, -08, -09, -10, -11,
-14, -15, -16, -18, -19), **4 conditional** (UN-07, -12, -13, -17 — the condition is in the row), **1 withdrawn** (UN-04).

## 4. Use conditions (from standards)

The author asked for nuclear-plant-level durability. No single public standard covers a hand-held gamma *imager*;
the closest are the portable health-physics instrument standard used at nuclear facilities and the hand-held
radionuclide-identifier specifications. Values below are as published. The **adopted** column was first set to the more demanding of the two, then
**relaxed to iPIX level** — the same-class commercial imager (§5) — by the author on 2026-10-01; where iPIX publishes no
figure, the row is not adopted.

| Condition | ANSI N42.17AC-2022, extreme (Cat. 1) ¹ | IAEA NSS-1, hand-held RID ² | Adopted (iPIX level ⁶) |
|---|---|---|---|
| Operating temperature | −20 °C to +60 °C (Cat. 2: −40 to +70 °C); normal: −10 to +50 °C | −20 °C to +50 °C | **−10 °C to +45 °C** |
| Storage temperature | −60 °C to +70 °C | — | not adopted (no iPIX figure) |
| Temperature shock | (tested) | fully functional within 1 h after 20 ↔ −20 °C and 20 ↔ 50 °C steps reached in < 5 min; must indicate when not fully functional | not adopted (no iPIX figure) |
| Relative humidity | 3 % to 95 % (normal: 14 % to 100 %); fog 6 h exposure | 90 % at 35 °C, non-condensing | **0 % to 93 % at 35 °C** |
| Rain / water | 0.25 inch/h (≈ 6.4 mm/h) rain | count rate unchanged after 30 min water spray | covered by **IP65** (water jets) |
| Dust / ingress (IP code) | — | — | **IP65** — accepted 2026-10-01 (iPIX level) |
| Drop | 1 m onto concrete | 1 m onto concrete, ≥ 10 drops on all sides (in its shipping case) | **60 cm** vertical drop |
| Mechanical shock | 100 g peak (normal 50 g) | shock transients per NSS-1 Table 4 | covered by the 60 cm drop |
| Vibration | 3.5 g (normal 2 g) | 10–500 Hz, 15 min per axis, 3 axes | **2 g, 10–33 Hz, 15 min** |
| Ambient pressure | 55 to 108 kPa | — | not adopted (no iPIX figure) |
| Radiation resistance (of the instrument) | 10,000 rad (100 Gy) | — | not adopted (no iPIX figure; iPIX images up to 10 Sv/h) |
| Battery | 8 h at each operating-temperature extreme | > 8 h without alarm, > 3 h in continuous alarm; remaining-capacity indicator | remaining-capacity indicator (UN-17); run-time design target ~4 h per replaceable battery (iPIX level) |
| Size and mass | — | < 300 × 200 × 150 mm, < 3 kg, single-handed with protective gloves | **≤ 2.5 kg** (iPIX level); hand-held size |
| Dose-rate indication | (radiological tests) | up to 10 mSv/h, ±50 %, 60 keV – 1.33 MeV, frontal incidence; adjustable visual + acoustic safety alarm | **as NSS-1** (UN-14) |

¹ ANSI N42.17AC-2022 Table 2 "Normal and Extreme Environmental Performance Requirements", as reproduced in
D. Walker, *Significance and Use of ANSI Standards for Portable Detectors*, CIRMS 2023
(<https://cirms.org/wp-content/uploads/cirms2023/presentations/Tuesday/Plenary/CIRMS2023_Walker.pdf>) — a
secondary source; verify against the standard before use.
² IAEA Nuclear Security Series No. 1, *Technical and Functional Specifications for Border Monitoring Equipment*
(2006), §6.4.6–6.4.27 (read from the full text).

Not yet consulted because the text is not public: **IEC 60846-1** (portable workplace dose-rate meters) and
**IEC 62327:2017** (hand-held radionuclide identifiers; it has its own *gamma source localization* clause, §6.2,
and the dust / moisture clause, §7.6). Their values should replace or confirm the table above.

## 5. Reference sources

The sources a user can realistically run into are the portable, high-activity sources of industry, not check
sources. They are taken from IAEA RS-G-1.9 ³ Table 2, "typical" activity of each practice, unshielded (a source out
of its container is the case a locator exists for):

| ID | Source | Radionuclide (main lines) | Typical activity ³ | IAEA category ³ | Dose rate at 1 m ⁴ |
|---|---|---|---|---|---|
| RS-1 | Industrial gamma radiography | Ir-192 (316, 468, 308, 296, 604 keV; ~2.3 γ / decay ⁵) | 3.7 TBq (100 Ci) | 2 | ~480 mSv/h |
| RS-2 | Industrial gamma radiography | Co-60 (1173, 1332 keV) | 2.2 TBq (60 Ci) | 2 | ~790 mSv/h |
| RS-3 | Fixed level gauge | Cs-137 (662 keV) | 0.19 TBq (5 Ci) | 3 | ~17 mSv/h |
| RS-4 | Fixed level gauge | Co-60 (1173, 1332 keV) | 0.19 TBq (5 Ci) | 3 | ~68 mSv/h |

All four can be simulated: Ir-192 joined the engine's isotope list (`Isotopes.All`: Cs-137, Co-60, Co-57, Na-22,
Am-241, Ir-192) on 2026-10-01 (EV-20). Co-57 stays in the isotope scope as the low-line test case (EV-16) but is not
a reference source — it is not a high-activity source people meet in the field.

### Reference conditions

A source is tested at the distance where it gives a stated **dose rate at the device**, so the four sources are
compared on the quantity the user experiences. Distances by the inverse-square law, air attenuation neglected
(it shortens the far distances by up to ~half at 300 keV and 70 m):

| Dose rate at the device | RS-1 Ir-192 | RS-2 Co-60 | RS-3 Cs-137 | RS-4 Co-60 | Meaning |
|---|---|---|---|---|---|
| 10 mSv/h | 6.9 m | 8.9 m | 1.3 m | 2.6 m | top of the dose-rate indication range (§4) |
| 100 µSv/h | 69 m | 89 m | 13 m | 26 m | working stand-off |
| 10 µSv/h | 219 m | 280 m | 41 m | 82 m | weak-signal search limit (inverse-square only) |

**Estimated detected count rate** of the hand-held head (16 × 16 mm, 50 % open mask, 15 mm GAGG) at those points —
an analytical estimate (geometry × open fraction × 662 keV stopping), which reproduces the MC lab efficiency
within 2 % (2.44 × 10⁻⁴ vs 2.48 × 10⁻⁴, EV-09) but uses 662 keV stopping for every line — for Ir-192 the MC gives
~7 % more (RS-1 ≈ 1.07 Mcps at 10 mSv/h, 1.07 kcps at 10 µSv/h; EV-20):

| Dose rate at the device | RS-1 | RS-2 | RS-3 | RS-4 |
|---|---|---|---|---|
| 10 mSv/h | 1.0 Mcps | 0.33 Mcps | 0.56 Mcps | 0.33 Mcps |
| 100 µSv/h | 10 kcps | 3.3 kcps | 5.6 kcps | 3.3 kcps |
| 10 µSv/h | 1.0 kcps | 0.33 kcps | 0.56 kcps | 0.33 kcps |

What this says: against realistic sources the camera is **not count-starved**. At the top of the dose-rate range
it reaches the ~1 Mcps rate limit of the front end (EV-21), and the 250–500 counts needed for a sub-mm
location (EV-09) arrive in well under a second even at 10 µSv/h. Time to a location is set by rate handling,
hand motion and background, not by sensitivity.

³ IAEA Safety Standards Series RS-G-1.9, *Categorization of Radioactive Sources* (2005), Table 2
(<https://www-pub.iaea.org/MTCD/Publications/PDF/Pub1227_web.pdf>).
⁴ From the specific gamma-ray constants Γ = 0.48 (Ir-192), 1.32 (Co-60), 0.33 (Cs-137) R·m²/(h·Ci) (US NRC H-117
*Introductory Health Physics*, Ch. 5, <https://www.nrc.gov/docs/ML1121/ML11210B521.pdf>), with 1 R ≈ 10 mSv;
approximate (±~15 %), adequate for setting test distances.
⁵ Sum of γ lines above 50 keV per decay, IAEA LiveChart of Nuclides (ENSDF, Baglin 2012),
<https://www-nds.iaea.org/relnsd/v1/data?fields=decay_rads&nuclides=192ir&rad_types=g>.

### Benchmark imagers and their test conditions

The measurement conditions that count in practice are the ones commercial gamma imagers are specified against. The
author named three; their published figures (manufacturer data unless noted). **Only iPIX is in Gcam's class** — a
coded-aperture camera (CdTe pixels rather than a scintillator). Polaris-H (Compton, CZT) and GeGI (HPGe, Compton +
pinhole + coded aperture) work on other principles — omnidirectional fields, different energy ranges and
efficiencies — so their figures are **information, not pass criteria** (author, 2026-10-01).

| | Mirion **iPIX** ⁶ | H3D **Polaris-H** ⁷ | PHDS **GeGI** ⁸ |
|---|---|---|---|
| Imaging principle | **coded aperture** (MURA rank 7 / 13, rotating mask) — same family as Gcam | Compton (3-D CZT, 20 × 20 × 15 mm) | Compton + pinhole + coded aperture (rank-19 MURA), HPGe |
| **Sensitivity test** | **Cs-137 giving 2 µSv/h** above background at the imager: **detected in < 30 s**; Am-241 giving 25 nSv/h: < 30 s | **10 µCi check source at 1 m: detected in 1 min** | not published as a single figure (Sellafield chose it for "fast results in low dose-rate areas") |
| Angular resolution | 2.5° (rank 13) – 5.0 / 6.0° (rank 7) | locates a point source to ±1°; separates two same-energy sources 20° apart | 1–10° (LLNL survey ⁹) |
| Field of view | 41.4–48.8° | 4π (omnidirectional) | 4π Compton, 60° pinhole |
| Energy range | 30–1332 keV | 50 keV – 3 MeV (imaging from 250 keV) | 30 keV – 3 MeV |
| Energy resolution at 662 keV | (imager: 3 energy bands; NID option < 2.5 %) | < 1.0 % | ~0.3 % |
| Max dose rate | linear to 10 Sv/h (Cs-137) | — | 0.15 mSv/h (LLNL survey ⁹) |
| Mass | 2.5 kg | ~3.9 kg (8.5 lb) | 6.8 kg with tablet and 2 batteries |
| Battery | up to 4 h per battery | 6 h + | ~3 h internal (hot-swap), 6–8 h with external |
| Environment | −10 to 45 °C; 0–93 % RH at 35 °C; **IP65**, decontaminable; 60 cm drop; 2 g at 10–33 Hz | water-proof, washable for decontamination | cooled HPGe: ~4 h cool-down |

### Reference measurement conditions (adopted)

Sensitivity is tested on the **same-class benchmark** (iPIX), so the result reads directly against a product users know;
the high-activity sources RS-1 … RS-4 above set the rate and safety extremes.

| ID | Condition | Pass | From | Gcam estimate (analytical, as above) |
|---|---|---|---|---|
| RC-1 | Cs-137 giving **2 µSv/h** above background at the device | located in **< 30 s** | iPIX parity | ~110 cps → 250 counts in ~2 s |
| RC-2 | *(information only — Compton class)* 10 µCi (370 kBq) Cs-137 at 1 m (~33 nSv/h at the device, below natural background), Polaris-H: detected in ≤ 1 min | — | Polaris-H | ~1.8 cps → ~110 counts / min without background |
| RC-3 | any of RS-1 … RS-4 at **10 mSv/h** at the device | location held, or saturation reported | URS §4 dose-rate range | 0.3–1 Mcps — at the front-end limit |

Not adopted: iPIX's Am-241 at 25 nSv/h (59.5 keV is below the concept's 122 keV imaging range) and iPIX's 10 Sv/h
imaging (the concept saturates around 10 mSv/h; see PRS §4).

⁶ Mirion, *iPIX Ultra Portable Gamma-Ray Imaging System* spec sheet
(<https://mirionprodstorage.blob.core.windows.net/prod-20220822/cms4_mirion/files/pdf/spec-sheets/doc010883en-d_ipix-ultra-portable-gamma-ray-imaging-system.pdf>).
⁷ Y. A. Boucher (H3D), *Polaris-H Imaging Spectrometer Design and Applications*, ISOE ALARA Symposium 2016
(<http://www.nsra.or.jp/isoe/english/alarasymposium/pdf/atc2016-8-1pp.pdf>).
⁸ PHDS Co., GeGI product page and specifications (<https://phdsco.com/products/gegi>).
⁹ S. Varghese et al., *Trade Study of Nondestructive Assay Technologies*, LLNL-TR-851034 (2023)
(<https://www.osti.gov/servlets/purl/1989998>).

A multi-imager comparison by the IAEA exists — the *Technology Demonstration Workshop on Gamma Imaging* (2016,
report 2017), which evaluated nine systems including GeGI — but its report is not public; if obtained, its test
geometry should be checked against RC-1 … RC-3.

## 6. Open decisions

| Item | Needed for | Note |
|---|---|---|
| Pass / fail for UN-07 | UN-07, UN-13 | **set 2026-10-01**: RC-1 (iPIX parity); RC-2 is information only (Compton class) |
| IP code | UN-15 | **IP65 — decided 2026-10-01** |
| Radionuclide set | UN-02 | **decided 2026-10-01: Cs-137, Co-60, Ir-192 (+ Co-57 as the low-line test case) are enough** |
| Which needs stay *to confirm* | all | **done 2026-10-01** — none left |
| Use-condition level | UN-06, UN-15, UN-17 | **decided 2026-10-01: iPIX level** (§4) |

## 7. Validation hooks

| Need | Validation | State |
|---|---|---|
| UN-08 | every evidence entry in [VV.Gcam.Evidence](VV.Gcam.Evidence.md) names the CLI study or script that reproduces it | in place |
| UN-09 | GCAM Studio validation scenarios VAL-01 … VAL-08 ([VV.Studio §5](VV.Studio.md#5-validation)) | partly performed |
| UN-01 … UN-07, UN-10 … UN-13, UN-16 … UN-19 | simulation of the concept where possible (PRS evidence column); otherwise only a real device with users | mostly not possible without hardware |
| UN-06, UN-15 (§4 values) | type tests on hardware | not possible in Gcam — environmental robustness is outside what a Monte Carlo can show |

## 8. Changing this document

Add needs with the next free `UN-` ID; don't renumber (UN-11 … UN-18 were added in the 2026-10-01 review; UN-01
and UN-07 were narrowed then). A need moves from *to confirm* to **confirmed** only when the author says so, with
the date. Every need is traced forward by at least one PRS requirement, or listed as unmet
([VV.Gcam.PRS §5](VV.Gcam.PRS.md#5-traceability-user-need--product-requirement)).
