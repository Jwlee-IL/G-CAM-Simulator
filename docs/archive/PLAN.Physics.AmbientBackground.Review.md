# PLAN.Physics.AmbientBackground.Review — TODO-30 premises, public data and rate measurements

Scope: Turn 1 review of [the reference plan](PLAN.Physics.AmbientBackground.md). No implementation or field-performance claim. Measurements use existing engine code and temporary headless probes; the only repository write is this file.

Status: **review complete; implementation blocked on the corrections and decisions below**, 2026-10-02. Reviewed HEAD `6e44d8b01fac923ff7671fd16257e6e62f6d4562`; working tree initially clean. No desktop, UI tests, process inspection/control, installations or git mutations. The source tree's build folders were not used. No approval-requiring command was executed.

## 1. Findings that stop the affected plan items

The purpose is sound: detected BSR is not an absolute incident radiation field. The plan is not ready to implement as written. Stop these items at their premises; the proposals below require adoption, not silent substitutions.

| Item | Finding and required correction |
|---|---|
| A-1, A-2 | A dose rate does not determine a spectrum or its angular distribution. Published K/U/Th dose contributions and decay branching fractions are not transported line fluence fractions. A numerical, versioned **incident fluence** spectrum including continuum has not been established here. Do not invent the proposed “terrestrial, typical” preset. Section 3 identifies accessible data and explicit remaining source needs. |
| A-3 | There is no reusable five-sided ray-transport shield. `ShieldStudy` attenuates a prescribed detected pedestal by `exp(-mu*t)`; the FOV model is a front plate. The crystal transport requires a crossing of the top plane and cannot model arbitrary side/rear entry. A full-head field needs new geometry/transport, not just a call to these models. |
| A-1, A-3, A-10 | The 2614.5 keV line exposes unsupported high-energy physics: tungsten attenuation clamps above 1332 keV; crystal transport omits pair production; `DoseStudy` discards deposits at or above 2000 keV. Do not claim realistic Tl-208 transport or the EV-23 accuracy without correcting and validating those items. |
| A-4 | Source removal is currently impossible in list mode: zero activity is rejected, empty `Sources` falls back to `Source`, and background is scheduled only after a pending source event exists. Studio also rejects an empty scene. A background-only acquisition and a fixed-time event API are necessary. |
| A-5 | “GAGG … low” must not mean zero intrinsic activity. GAGG contains natural alpha-emitting Gd-152 and can have U/Th/Ac/Sm contamination; BGO, CsI and NaI also have sample-dependent contaminants. Activity in Bq is not a gamma-equivalent pulse spectrum. Internal beta/alpha deposition and quenching are absent from the proposed external-photon route. Keep intrinsic modelling a separately decided scope. |
| A-8 | Net source counts alone cannot maintain the EV-07 precision gate: identical net counts with different background variance have different precision. The Evidence §1 statement “the gates hold for net source counts” needs qualification. A search-calibrated significance and a localisation-quality gate are separate requirements. |
| A-9 | An acquisition duration is missing for many evidence recipes. A photon proposal budget or a weighted mean map is not elapsed physical time. Merely adding ambient config and rerunning the driver will not add background to studies that ignore/clear it or build synthetic floods. Each recipe needs a physical normalisation and explicit injection route. |
| A-10 | EV-23 already demonstrates a large angular dose bias and led to the separate-counter decision D-37. Its frontal ±13% result cannot be used as an isotropic ambient-dose round-trip guarantee. Algebraic dose↔fluence closure and an instrument dose estimate are different oracles. |

Evidence §1's “every error grows” is also too strong: fixed geometry/grid bias need not grow monotonically in one run, and source transport efficiency is unchanged by an independent field in a linear detector. Report distributions, false alarms, precision and recorded rates separately. The “background is not coded/uniform” premise is an approximation; a finite aperture, directional field and edge/rear leaks produce structure. A source-free reconstruction may have peaks.

## 2. Every “What exists” row checked against current code

Line numbers below refer to the reviewed tree, not to historical memory. Paths are repository-relative.

| Plan row | Verified member and line | Correction / detail |
|---|---|---|
| Config | `src/Gcam.Configuration/SimulationConfig.cs:133`, `BackgroundConfig`; properties at 137, 143, 148 | Correct: BSR defaults to 0, energy to 200 keV, nullable dark rate. No absolute field or spectrum. The comments understate usage: dark counts and energy are used in list mode too, not only RTL. |
| Flood-map helper | `src/Gcam.Simulation/BackgroundStudy.cs:15`, `Background.PedestalPerPixel`; `:31`, `RealizePixel`; `:35`, `Realize`; `:140`, `BackgroundStudy.RunSweep` | The member is **`RealizePixel`, not `PoissonPixel`**. `RunSweep` clears `Background` at 148, transports the source, scales to a detected-count budget, and adds the prescribed pedestal at 167/187. `GradientProfile` and `SideLeakProfile` are prescribed spatial shapes, not transport. |
| Flood callers | `src/Gcam.Simulation/MaskAntimaskStudy.cs:75–80`; `ShieldStudy.cs:68/79`; `FieldOfViewStudy.cs:114/201` | `antimask` uses counts/pixel, not directly the config BSR; `shield` uses `bg0PerPixel*exp(-mu*t)`. FOV BSR is relative to **on-axis N0**, not the reduced off-axis signal. |
| `antimask-scene` | `src/Gcam.Cli/Commands/DecodingCommands.cs:162`, scene command; mean-map combinations starting at 190 | **Not a pedestal/Poisson caller.** It subtracts mask/antimask source mean maps and separately adds another directional source's mean maps. Its “diffuse cancels” example contains no explicit diffuse photon field or noisy background acquisition. Stop the claim that this command already measures that noise. |
| List-mode rate | `src/Gcam.Simulation/ListModeSource.cs:47–48`, `SourceRateCps`/`RateCps`; `Advance` at 125, scheduling at 135–157 | Correct BSR law, but the source rate is a running importance-weighted estimator. Exponential gaps use `BSR*SourceRateCps`, so the implemented process has an adaptive rate during estimation, not a stationary known-rate Poisson oracle. Source positivity at 75 and pending-signal dependency at 135 block source-free operation. |
| List-mode deposits | `src/Gcam.Simulation/ListModeBackground.cs:17`, constructor; `Advance` at 34; `Place` at 48 | Correct: cosine incidence into the top face, monoenergy, unmasked/unshielded Compton deposition. Retries at the **same scheduled time** until a deposit exists make BSR a detected-rate knob; the crystal efficiency does not thin that rate. `Place` independently draws a uniform pixel. Unlike the source detector, this path omits entrance absorber, backing, reflector gaps and optical crosstalk. |
| Event-stream spectrum | `src/Gcam.Simulation/EventStreamStudy.cs:199`, `BackgroundDepositSpectrum`; event insertion at 96–122 | Spectrum-only helper repeatedly tries until `count` deposits or `count*50` attempts. It returns a conditional deposit pool, not efficiency or absolute rate. Cosine top-face model, no mask/shield. RTL background reuses a 4096-deposit pool. `events.Count>0` and last source event as the span also block a source-free fixed-duration stream. |
| Dose conversion | `src/Gcam.Simulation/DoseStudy.cs:10`, **`AmbientDose.PerFluence`** at 18 | Correct table declaration: ICRP 74 Table A.21, log-log interpolation, 10 keV–10 MeV, pSv·cm². This review uses the repository coefficients; it does not independently certify every original table value. `DoseStudy.Response` at 67 is a frontal/oblique point-source response, clears background at 73, limits pulse heights at 60/96. |
| Studio | `src/Gcam.Studio.Core/ViewModels/MainViewModel.cs:210`, `_backgroundToSignalRatio`; change validation at 224; start at 405; `src/Gcam.Studio.Services/SimulationService.cs:24`, `BuildConfig`, background assignment at 47 | Correct: editable BSR starts at 0; no ambient control. It is passed separately from scene and detector settings. `Start` at 15 rejects an empty scene. |
| Ideal studies / single runs | `src/Gcam.Simulation/SimulationRunner.cs:33–64`; `NoiseStudy.Run` at 36, realisation at 62; `FieldOfViewStudy.cs:65`; `DoseStudy.cs:73`; `MlemStudy.Run` at 41, `TwoSourceFlood` at 98 | `SimulationRunner` does not inspect background at all, so a single run with nonzero config BSR remains source-only. Noise uses Poisson(source mean) only. Most transport studies inherit the runner's behaviour, while FOV/dose explicitly clear background. **MLEM uses a synthetic binary forward model matching its inversion, not the physical transport chain** (66/81); it needs a different recipe to establish absolute ambient performance. Mixed-field/Compton/depth evidence has no absolute ambient injection. |

Additional transport boundaries: `ComptonCrystalDetector.Score` (`src/Gcam.Detector/ComptonCrystalDetector.cs:119`) intersects `PlaneZ` at 123, requires a positive crossing time and in-face entry at 125–129, then starts at z=0 and tracks into negative z (145–159). Existing `pixelEventSink` at 223–231 chooses the largest local deposit; `pixelSitesSink` at 236 exposes the sites. Use those for deposition-associated pixels. Source-path use is already at `ListModeSource.cs:100–108`.

`ShieldStudy.Run` (`src/Gcam.Simulation/ShieldStudy.cs:37`) is a five-wall attenuation/mass surrogate (67–93), not front-plate-only, but **neither five-wall ray transport nor scattering/buildup**. `CodedApertureMask.Transmit` (`src/Gcam.Masks/CodedApertureMask.cs:64`) counts out-of-pattern slab samples as tungsten at 117–120; this behaves like an infinite surrounding front plate. Energy-dependent attenuation is at 165–168; `TungstenMuRel` clamps above its final 1332 keV grid point at 188–196. `CrystalMaterial` documents missing pair production at `src/Gcam.Detector/CrystalMaterial.cs:17–20`, including sizeable errors at 2–3 MeV.

## 3. Public data found, and what is still “needs source”

### 3.1 Terrestrial composition and angular distribution (A-1/A-2)

**No numerical “typical” incident spectrum is approved by this review.** There is no unique universal mixture: geology, height, building materials and scattering change it. A measured scintillator pulse-height continuum includes the detector's own Compton redistribution; feeding that as incident fluence would count detector scattering twice.

The accessible primary reference is **IAEA, *Guidelines for radioelement mapping using gamma ray spectrometry data*, IAEA-TECDOC-1363, Vienna (2003)**, §§3.2–3.4, Figs. 3.1–3.4 and Table 3.1 ([full report](https://www-pub.iaea.org/MTCD/Publications/PDF/te_1363_web/PDF/Contents.pdf)). It supplies K/U/Th line spectra, transported flux illustrations with continua at 300 m, and measured survey spectra. Principal mapping lines are K-40 1.460 MeV, Bi-214 1.765 MeV and Tl-208 2.614 MeV. Its explanation explicitly ties scattered/unscattered shares to geometry and intervening matter. The 300 m illustration is **not** a ground-level indoor preset. No integrated continuum fraction was extracted from it.

**UNSCEAR, *Sources and Effects of Ionizing Radiation*, 2000 Report, Volume I, Annex B, Table 6 and §§46–56** ([report](https://www.unscear.org/unscear/uploads/documents/publications/UNSCEAR_2000_Annex-B.pdf)) gives population-weighted soil activities K/U/Th = 420/33/45 Bq/kg, with air-dose coefficients 0.0417/0.462/0.604 nGy/h per Bq/kg. Multiplication gives 17.5/15.2/27.2 nGy/h, approximately **29/25/45% of air kerma**, not of H*(10) or photon fluence. Outdoor/indoor terrestrial averages are 59/84 nGy/h. These are useful composition/scale anchors, but cannot supply the requested line and continuum fluence fractions. This report's combined directly ionizing + photon cosmic dose must not be converted wholesale into photon fluence.

| Requested component | Data status / action before a preset |
|---|---|
| K-40, Bi-214, Tl-208 principal energies | Supported by the IAEA reference above. |
| Pb-214 295/352; Bi-214 609/1120/1764; Tl-208 583/2614; Ac-228 911/969 keV | These are the plan's candidate energies. Several appear in the IAEA spectra; a complete evaluated numerical emission table for all of them **needs source**. LNHB/DDEP PDF access attempts failed (502/timeouts); do not assign intensities from recollection. The IAEA figure's extracted 352-keV label is inconsistent with the plan's Pb-214 assignment, so its plot labels are not a substitute for evaluated decay data. |
| Individual line relative contributions to H*(10)/fluence | **Needs source**: a specified observation geometry and numerical transported field/unfolded dataset, not source branching fractions. |
| Incident continuum fraction and bins | **Needs source**: energy limits, normalisation and site must accompany the fraction. No arbitrary flat/exponential continuum is proposed as “published terrestrial”. |
| Ground/sky angular mixture | Isotropy is an explicitly ideal baseline. A fixed 50:50 ground/sky split is not measured here. Ground normal must be a world-space vector distinct from the optical axis. |

Relevant public references located but not used to invent weights:

- **H. L. Beck and G. de Planque, *The Radiation Field in Air Due to Distributed Gamma-Ray Sources in the Ground*, HASL-195, US Atomic Energy Commission Health and Safety Laboratory (1968).** [Public record](https://hero.epa.gov/reference/3794511/). A second archive viewer was accessible but did not expose usable tables. **Full numerical tables not accessed; needs source.**
- **ICRU, *Gamma-Ray Spectrometry in the Environment*, Report 53 (1994).** [Publisher contents](https://journals.sagepub.com/toc/crub/os-27/2). Chapters were listed as restricted access. **Full report not accessed; needs source.**
- **“Terrestrial-origin skyshine at sea level”, *Radiation Physics and Chemistry*, publisher PII S0969806X22007794** ([publisher](https://www.sciencedirect.com/science/article/pii/S0969806X22007794)). The indexed abstract reports a skyshine/emission flux ratio around 45%. It is a candidate angular-model reference; **full conditions and complete bibliographic metadata not accessed**, so that ratio is not adopted as a hemisphere probability.

### 3.2 Dose-rate scale and cosmic photons (A-1/A-2)

**K. A. Aleissa and A. M. Enany, “Measurements of environmental radiation doses due to natural radiation sources at Riyadh region, Saudi Arabia”, *Radiation Protection Dosimetry* 152(4), 264–272 (2012), DOI 10.1093/rpd/ncs048** ([primary abstract](https://pubmed.ncbi.nlm.nih.gov/22504311/)). Direct H*(10) measurements span **61–135 nSv/h indoors and 57–105 nSv/h outdoors**. A 0.1 µSv/h UI example is within these ranges; it is not a worldwide average or a separately measured photon-only component. The abstract is accessible; full text was not used.

**D. Mrdja, I. Bikit, K. Bikit, J. Slivka and I. Anicin, “Study of radiation dose induced by cosmic-ray origin low-energy gamma rays and electrons near sea level”, *Journal of Atmospheric and Solar-Terrestrial Physics* 123, 55–62 (2015), DOI 10.1016/j.jastp.2014.12.007** ([publisher abstract/snippets](https://www.sciencedirect.com/science/article/abs/pii/S1364682614002892)). It reports a 30–300 keV photon component peaking around 90 keV and flux around **3000 m⁻² s⁻¹ = 0.30 cm⁻² s⁻¹**, using an upper-hemisphere measurement. It estimates photon-only low-energy effective dose around 5% of the combined sea-level cosmic 0.270 mSv/year. **Not H*(10), not a full cosmic spectrum**. Full spectral tables and the precise flux convention/uncertainty were not accessed; do not add this as an isotropic scalar fluence or choose a cosmic dose fraction yet.

Keep measured total environmental H*(10), terrestrial photon H*(10), cosmic photons, muons/electrons and neutrons distinct. An ambient photon preset already including cosmic photons must not receive a second cosmic addition. Muons are outside this photon engine's scope.

### 3.3 Intrinsic material backgrounds (A-5)

These are **sample examples, not interchangeable material constants**. Bq/kg measures radionuclide activity; counts/s/cm³ additionally depends on window, escape, quenching and readout. Contamination need not be in secular equilibrium.

| Material | Published quantitative anchor and meaning | Reference |
|---|---|---|
| LYSO | A published GATE study uses **300 decays/s/cm³** uniformly distributed Lu-176, with comparison to measured spectra; 88/202/307 keV gammas accompany beta decay. This is a representative activity input, not a universal certified LYSO batch assay. A 3.24 cm³ solid Studio volume would contain about 972 decays/s before gap/threshold losses, far above the external-photon examples. | F. E. Enríquez-Mier-y-Terán et al., “GATE simulation of the intrinsic radioactivity in LYSO scintillation crystals”, *NIM B* 454, 1–5 (2019), [DOI 10.1016/j.nimb.2019.06.001](https://doi.org/10.1016/j.nimb.2019.06.001). Publisher abstract and indexed methods accessible; full paper not accessed. |
| LaBr3 | Final published abstract reports **1.523(34) Bq/cm³** in a 3-inch × 3-inch B380. Earlier author manuscript reports 1.480(69), chiefly La-138 1.425(59) Bq/cm³ plus Ac-chain daughters. **Do not combine the two versions as one measurement.** | H. Cheng et al., “Intrinsic background radiation of LaBr3(Ce) detector via coincidence measurements and simulations”, *Nuclear Science and Techniques* (2020), [DOI 10.1007/s41365-020-00812-8](https://doi.org/10.1007/s41365-020-00812-8); [accessible author manuscript](https://arxiv.org/abs/2007.07552). Final full text unavailable. |
| CeBr3 | A 38.1-mm diameter × 38.1-mm crystal assayed underground had Ac-227 **0.30±0.02 Bq/kg** and La-138 **7.4±1.0 mBq/kg**, with activation products also detected. It is not activity-free. | G. Lutter et al., “Radiopurity of a CeBr3 crystal used as scintillation detector”, *NIM A* (2013), [DOI 10.1016/j.nima.2012.11.174](https://doi.org/10.1016/j.nima.2012.11.174); [JRC primary abstract](https://publications.jrc.ec.europa.eu/repository/handle/JRC74411). |
| CeBr3 / LaBr3 comparison | Shielded, 20 keV–3 MeV **integral count rates**, Table 2: 2-inch CeBr3 samples 0.019±0.001 and 0.043±0.001 counts/s/cm³; LaBr3 1.242±0.008. These are count rates, not Bq. Batch impurity matters. | F. G. A. Quarati et al., “Scintillation and detection characteristics of high-sensitivity CeBr3 gamma-ray spectrometers”, *NIM A* 729, 596–604 (2013), [DOI 10.1016/j.nima.2013.08.005](https://doi.org/10.1016/j.nima.2013.08.005); [accessible paper](https://www.berkeleynucleonics.com/sites/default/files/products/resources/cebr3_characteristics_white_paper.pdf). |
| GAGG | Natural Gd-152 alone gives about **0.5 events/s in 650 g** in a recent study, derived from its natural abundance/half-life. A measured ~240 keV electron-equivalent alpha peak has ~1 event/s including suspected Sm-147; alpha light is strongly quenched. The same study identifies U/Th/Ac contamination. | L. Ascenzo et al., “Characterization of a GAGG detector for neutron measurements in underground laboratories”, *EPJ C* 85, 1057 (2025), §4, [DOI/full text](https://link.springer.com/article/10.1140/epjc/s10052-025-14807-5). |
| GAGG batch range | Table 2: high-purity sample Th-232 **10.3±0.8 mBq/kg**, upper U-238 chain **125.2±1.6 mBq/kg**; a 4N sample has **288.8±19.6** and **911.3±10.1 mBq/kg** respectively. Do not equate a chain-segment assay with its total counted decays. | T. Omori et al., “First Study of the PIKACHU Project: Development and Evaluation of High-Purity Gd3Ga3Al2O12:Ce Crystals for 160Gd Double Beta Decay Search”, *PTEP* 2024, 033D01, [DOI 10.1093/ptep/ptae026](https://doi.org/10.1093/ptep/ptae026); [accessible author text](https://arxiv.org/html/2402.06830v1). |
| NaI(Tl) | Eleven older BICRON crystals have K-40 bulk activities **13.7±0.3 to 21.2±0.4 mBq/kg** (Table 4). Purification changes this. A shielded NaI count rate in Quarati Table 2 is 0.012±0.001 counts/s/cm³, including residual external/cosmic radiation; it is **not** a NaI intrinsic activity. | C. Cuesta et al., “Analysis of the 40K contamination in NaI(Tl) crystals from different providers in the frame of the ANAIS project”, *IJMP A* 29, 1443010 (2014), [DOI 10.1142/S0217751X14430106](https://doi.org/10.1142/S0217751X14430106); [author paper](https://arxiv.org/pdf/1403.3580). |
| CsI(Tl) | Indexed primary text reports CsI **powder** Cs-137 contamination **25.6±5.0 mBq/kg**. This is precursor material, not a finished-crystal activity or a universal low-background number. Finished-crystal radionuclide-specific activities **need source** before a preset. | B. J. Kim et al., “Study of the internal background of CsI(Tl) crystal detectors for dark matter search”, *NIM A* 500, 337–344 (2003), [DOI 10.1016/S0168-9002(03)00346-2](https://doi.org/10.1016/S0168-9002(03)00346-2). Indexed paper excerpt available; full PDF fetch failed. |
| BGO | Some batches have internal alpha activity **up to 10 Bq/kg**, attributed to Po-210. The publisher introduction reports typical Bi-207 contamination at 1–3 Bq/kg, but extracted unit text is malformed (“Bk/kg”); **verify that unit from the PDF before adopting it**. No default zero is warranted. | D. N. Grigoriev et al., “Alpha radioactive background in BGO crystals”, *NIM A* 623(3), 999–1001 (2010), [DOI 10.1016/j.nima.2010.07.067](https://doi.org/10.1016/j.nima.2010.07.067); accessible publisher abstract/snippets. |

Recommendation: implement external ambient photons first, with intrinsic defaults off and an explicit “not modelled” material limitation. A subsequent intrinsic model should sample uniform active-volume decay sites, include local beta/conversion-electron energy, correlated gammas and alpha quenching, and use cited batch profiles with uncertainty. It must not masquerade as external H*(10).

### 3.4 Small-crystal sanity anchor (A-10)

**M. Ripani, F. Rossi, L. Cosentino, F. Longhitano, P. Musico, M. Osipenko, G. E. Poma and P. Finocchiaro, “Field Test of the MiniRadMeter Gamma and Neutron Detector for the EU Project CLEANDEM”, *Sensors* 24(18), 5905 (2024), DOI 10.3390/s24185905** ([published reference](https://doi.org/10.3390/s24185905); [accessible author preprint](https://www.preprints.org/manuscript/202409.0028), §§2.1–2.2/4, Figs. 4/8f, Table 1).

A **1 cm³ CsI(Tl)/SiPM** reports **4.04 cps over 80 s** in background; Table 1 gives **0.15±0.008 µSv/h** by Cs-137 count conversion and **0.07±0.004 µSv/h** by deposited energy. It also experimentally checks approximately **2.7 cps at a stated 0.1 µSv/h Cs-137 field**. Its two ambient dose methods disagree; neither is independent traceable H*(10) truth. Use rate/shape as a sanity anchor with matching housing/thresholds. Published-site access returned 429; final-version agreement with the accessible preprint needs checking.

## 4. Temporary measurements and physical interpretation

### 4.1 Method, reproducibility and limits

Headless C# probe under `%TEMP%/gcam-ambient-review`, project referencing `Gcam.Studio.Services` and its engine dependencies. Builds used `--artifacts-path` under that directory. Retained files: `AmbientReview.csproj`, `Program.cs`, `review-seeds.json`, `ensemble.jsonl`, build logs, and `driver-smoke/`. No GUI was loaded.

Eight outer seeds are the first eight `O128` entries: **12345, 1000003, 1007922, 1015841, 1023760, 1031679, 1039598, 1047517**. Each source-rate estimate advances `ListModeSource` for 200,000 histories. Each (geometry, energy, seed) samples 200,000 incident top-face rays and transports them through the actual `ComptonCrystalDetector`, using the same cosine-flux entry construction as `ListModeBackground`. Entry x/y use the existing 0.999 edge factor; entry/transport RNG offsets are seed+200/+201. For an additional front-plate diagnostic the ray starts at `D+thickness/2+1 mm`, is traced through the existing factory mask with seed+202, then through a second crystal with seed+203. Misses count as misses, not retries until detection.

Crystals use configured material, pitch, depth and attenuation anchor. These diagnostic ambient detectors intentionally match the **legacy BSR bare response**, omitting gaps, entrance/backing and readout cuts. Source rates use the actual list-mode source settings, including Studio's gaps/entrance/backing. Thus BSR below is a **legacy-equivalent diagnostic ratio**, not a prediction of a finished head. The 1461/2614.5 keV rows expose the current high-energy approximations and are not accepted physical validation.

For each monoenergetic diagnostic, the entire 0.1 µSv/h is assigned to that energy. Scalar full-sphere fluence rate is

`Phi(E) = 100000 pSv/h / (3600 * AmbientDose.PerFluence(E))` in cm⁻² s⁻¹.

For isotropic intensity, **one face's incoming current is Phi/4**, not Phi or Phi/2. With face area A and measured deposit probability p, `B_top = Phi*A*p/4`. Coefficients at 200/661.7/1461/2614.5 keV are 1.200/3.7347/6.7743/10.1796 pSv·cm², giving Phi 23.148/7.438/4.100/2.729 cm⁻² s⁻¹. These diagnostics deliberately avoid inventing spectrum weights. A mixture with dose fractions q would have `B = sum(q_E*B_E)`; fluence fractions require a different normalisation (Section 5).

All “±” below are **sample SD across eight outer seeds**, not uncertainty bounds or gate tolerances. MC sampling uncertainty is much smaller than spectral/geometry model uncertainty. Example binomial standard error for a 200,000-ray efficiency estimate is `sqrt(p*(1-p)/200000)`; front-mask low-probability sampling has larger relative error. No new production test was run.

The probe built successfully with zero warnings/errors. An intermediate rebuild accidentally included C# files from a second nested temporary artifacts directory, producing duplicate assembly attributes. The temporary project was corrected to compile only `Program.cs`; the final build and ensemble succeeded. The failed output (`measurement3`) was not used. No repository build setting was changed.

### 4.2 Source settings and measured rates

| Scene | Actual settings | List-mode detected source rate, cps (N=8) | Conventional runner rate, cps (N=8) |
|---|---|---|---|
| `samples/scenario.json` | Defaults: 1 MBq Cs-137, branching 0.851, source–detector 160 mm; 12×12, pitch 1 mm, depth 10 mm; “ideal” material resolves to GAGG in Compton transport | **80.217±0.300** | **212.030±0.230**, ideal detector stopping in the conventional factory |
| `samples/scenario_handheld.json` | 1 MBq, branching 0.851, source–detector 155 mm; GAGG_Mg 16×16, pitch 1 mm, depth 15 mm, mu662=0.0513/mm | **194.805±0.712** | **210.919±0.282**, deposited weighting/readout differs from list-mode event counting |
| Studio startup | One centred Cs-137 at **500 µCi = 18.5 MBq**, 1000 mm; rank 13, cell 0.7 mm, D=80 mm; GAGG 30×30 at 0.6 mm, depth 10 mm, gap 0.1 mm, entrance 0.15 mm, backing 2 mm | **72.820±0.351** | Not used: `BuildConfig` sets `PhotonCount=1`; that runner output is not a statistically useful Studio rate |

Defaults verified at `SceneConfigBuilder.cs:6–15`, `Scene.cs:58–82`, `SourceItemViewModel.cs:12–16`, `MainViewModel.cs:55` (startup `AddSource`), `DetectorSettings.cs:6–12`, and `SimulationService.BuildConfig`. Studio includes isotope emission lines, including soft X-rays; it is not the lab mono-line input. These differences forbid a single BSR conversion for all paths.

### 4.3 Bare top-face detected rate and corresponding BSR

| Geometry | Energy keV | B_top cps, mean±SD (N=8) | B_top / list-mode source rate, mean±SD (N=8) |
|---|---:|---:|---:|
| Lab | 200 | 5.8952±0.0126 | 0.07349±0.00028 |
| Lab | 661.7 | 0.81336±0.00082 | 0.010140±0.000036 |
| Lab | 1461 | 0.30160±0.00144 | 0.003760±0.000026 |
| Lab | 2614.5 | 0.14708±0.00068 | 0.001834±0.000011 |
| Handheld | 200 | 11.5306±0.0186 | 0.05919±0.00024 |
| Handheld | 661.7 | 1.83794±0.00775 | 0.009435±0.000054 |
| Handheld | 1461 | 0.70358±0.00251 | 0.003612±0.000017 |
| Handheld | 2614.5 | 0.34979±0.00130 | 0.001796±0.000007 |
| Studio | 200 | 14.6258±0.0300 | 0.20085±0.00082 |
| Studio | 661.7 | 2.19496±0.00774 | 0.030143±0.000174 |
| Studio | 1461 | 0.82818±0.00216 | 0.011373±0.000051 |
| Studio | 2614.5 | 0.40780±0.00178 | 0.005600±0.000033 |

For the ideal lab flood-map path, dividing the same 200/661.7-keV diagnostic rates by 212.03 cps instead gives about **BSR 0.0278/0.00384**. That difference is detector modelling, not uncertainty in a common source rate.

The ±10% **total-deposit** Cs window [595.53,727.87] keV in the bare probe has 0 counts in all eight 200-keV runs by energy conservation. At 1461 keV its background rates are **0.02212±0.00039 / 0.04958±0.00047 / 0.05963±0.00041 cps** (lab/head/Studio, N=8). This is before smearing and uses total event deposit, not the EV-15 per-pixel window. It illustrates why all-energy BSR cannot predict isotope-window contamination.

### 4.4 Front-mask diagnostics and a whole-crystal geometric scale

| Energy keV | Lab mask+top cps (N=8) | Handheld mask+top cps (N=8) | Studio mask+top cps (N=8) |
|---:|---:|---:|---:|
| 200 | 0.02658±0.00080 | 0.05044±0.00168 | 0.04637±0.00101 |
| 661.7 | 0.07537±0.00086 | 0.17401±0.00209 | 0.19017±0.00202 |
| 1461 | 0.07420±0.00093 | 0.17532±0.00157 | 0.19270±0.00213 |
| 2614.5 | 0.03611±0.00049 | 0.08702±0.00077 | 0.09440±0.00163 |

These use the existing **infinite front-plate** approximation only; they omit other faces and all secondary radiation. In particular the two high-energy rows inherit the tungsten clamp and missing pair production. They show how wrong it would be to use the bare BSR response as front-head efficiency, not a validated ambient shield design.

For a homogeneous convex rectangular bare crystal, isotropic geometric interception is `Phi*S_surface/4`, with mean chord `4V/S_surface`. A first-interaction upper estimate is

`B_interaction <= Phi*S_surface/4 * (1-exp(-mu*4V/S_surface))`.

The inequality follows from the concavity of `1-exp(-mu*l)` and the isotropic chord mean. It assumes a solid crystal and the current photo+Compton mu, with no gaps, walls, thresholds or escaping-secondary event multiplication. Surface areas/mean chords are lab 7.68 cm²/7.50 mm, head 14.72 cm²/10.435 mm, Studio 13.68 cm²/9.474 mm. At 200 keV the analytic upper scales are **35.68 / 76.29 / 68.98 cps**; at 1461 keV **1.66 / 4.24 / 3.63 cps**. These are deterministic model bounds, not measured all-face rates. They explain why side/rear transport matters. They are not bounds on a head with shielding-generated secondary photons.

### 4.5 How far ideal conclusions could move

For rough counting significance with perfectly known uniform background, `Z ~ S/sqrt(S+B)`; obtaining the same Z as an ideal net-count budget S0 needs roughly `S ~ S0*(1+B/S)`. Independent finite-time background calibration adds variance `B*t/t_bg`. This scaling does **not** reproduce a coded-image precision distribution or guarantee sub-mm performance.

- **EV-07:** At the default 1 MBq lab distance the bare 200-keV diagnostic is BSR ~0.028 in the ideal flood path, suggesting only a few-percent statistical budget increase; front-only transport suggests much less. The 25-count collapse and 250-count sub-mm gates remain ideal references, not new guarantees. At **1 MBq and approximately 1 m**, inverse-square extrapolation of the list-mode lab/head rates gives ~2.05/~4.68 cps (near-field mask acceptance changes, so only an order estimate). The 200/1461-keV bare top-face diagnostics then imply BSR roughly **2.9/0.15 (lab)** and **2.5/0.15 (head)**. A 250-source-count significance-equivalent budget can become several hundred to around **1000 source counts**. Side/rear exposure may increase it; shielding/windows may decrease it.
- **Studio:** At startup the same bare top diagnostics imply ~1–20% rate ratios, but at **1 MBq/1 m**, the source-rate scaling is ~3.94 cps and BSR spans ~0.21–3.7 for 1461 versus 200 keV. At **10 kBq/1 m**, ~0.0394 cps, these ratios become ~21–371. Even the front-only diagnostics are then consequential (BSR ~1–5). Long acquisition integrates background as well as source; total counts alone become misleading.
- **EV-01/09 floors:** A high-count, deterministic geometry/grid floor is not automatically raised by a fixed ambient mean. Its Poisson tail, wrong-peak probability and time to approach the floor change. A heuristic statistical error component scales like `sqrt(1+B/S)`: roughly 1.1–2 at BSR 0.2–3, not “every floor doubles”. A structured unknown background can create a persistent bias. No new localisation floor is measured here.
- **EV-15:** Ambient adds unrelated line/continuum counts to Cs and Co windows. Existing Co-only stripping cannot remove that nuisance term. Some quotes are weighted mean-map counts (~3 true Cs counts), not a stated exposure; their absolute ambient contamination is undefined until time/rate normalisation is specified. The bare total-deposit 1461-keV example would contribute about **1.33/2.97/3.58 counts in 60 s** for lab/head/Studio, compared with just 3 source counts in the cited weak-count example. Those are scale comparisons, not a remeasurement of per-pixel stripping. New window-response maps and finite-statistics background calibration are required.
- **EV-17/21/22:** Tens of external cps add little dead time at microsecond resolving times (rate×tau ~10⁻⁵–10⁻⁴), but this inference does not cover LYSO's much larger intrinsic activity or very soft fields. Keep trigger thresholds and material scope explicit.

## 5. Proposed config, API and transport (A-3/A-4/A-6)

Retain `BackgroundConfig.BackgroundToSignalRatio`, `EnergyKeV` and `DarkCountRateKcps` with their current semantics. Add an optional **sibling** `SimulationConfig.Ambient` (null = off); do not reinterpret BSR or infer incident fluence from its 200-keV deposit spectrum. Proposed conceptual shape:

```text
AmbientFieldConfig
  DoseRateMicroSvPerHour       // photon H*(10), free field, finite >= 0
  SpectrumId / Spectrum       // versioned published incident spectrum; one selected route
  AngularModel                // Isotropic; tabulated world-space distribution later
  GroundNormal                // only for a documented ground/sky model

IncidentSpectrum
  Lines: EnergyKeV, RelativePhotonWeight
  ContinuumBins: LowKeV, HighKeV, IntegratedPhotonWeight, density rule
  EnergyLimits, Reference, Geometry, Version, ContentHash

HeadShieldConfig              // separate from the field and detector response
  Front geometry + four side walls + rear wall; material, dimensions, thickness
  Explicit response approximation / supported energy range
```

Reject negative/nonfinite weights, zero total weight, overlapping/ambiguous bins, energies outside validated transport/dose ranges, conflicting preset/custom inputs and a missing continuum density convention. Use a normalised **fluence-weight** distribution f: `Phi_total = Hdot_pSvPerS / sum(f_i*h_i)`, `Phi_i=f_i*Phi_total`. For finite bins integrate `h(E)` against the declared density. Dose fractions q instead imply `Phi_i=Hdot*q_i/h_i`; never mix those definitions.

An `AmbientResponse` should return unweighted-event effective areas by energy/direction and pixel/deposit kernels, thresholds separately, plus variance/effective sample size for MC estimates. Detector count rate is `sum(Phi_i*A_eff_i)`, **not** incident rate times the source's efficiency. Cache key includes spectrum/geometry/material/readout inputs, seed and response version. A mean-map acquisition needs an explicit duration in seconds. Keep expected source, ambient, relative stress and dark contributions separate internally; expose combined observed data and rate estimates, not ground-truth provenance as a confidence oracle.

For full-head isotropy, generate incoming boundary rays on an enclosing convex surface: select faces proportional to area, position uniformly, inward direction cosine-weighted. Total proposals per second are `Phi*S_enclosure/4`. Rays must traverse the actual wall segments before reaching the active crystal; this avoids treating optical front/rear as ground/sky and avoids double-counting photons crossing multiple surfaces. Front aperture uses the existing mask slab only after its energy range and finite front-wall geometry are made adequate. Side/rear wall transmission must use actual oblique path lengths. If the author chooses uncollided attenuation only, label missing buildup/fluorescence and secondary scatter; do not cite EV-25 as validation of that geometry.

The crystal needs a ray-box entry for all faces and an internal starting point just inside that face. Entrance window applies to the front housing, backing to the actual rear material, not every face. Preserve local site accumulation, total event deposit and largest-deposit pixel via the existing callback contract. No random reassignment of a transported ambient deposit to a different crystal. A finite source capsule should not accidentally attenuate unrelated ambient photons: the current lumped entrance absorber is not a resolved capsule/window geometry.

For a stationary field, either (a) sample incident exponential gaps at a fixed boundary rate and thin by transport, or (b) calibrate a fixed detected rate/kernel, then sample fresh conditional detected events. Route (a) gives transparent efficiency/rate oracles; route (b) needs response-MC uncertainty and must not repeatedly use a finite deposit pool. Merge independent fixed-rate processes by event time, retain pending events and each RNG stream through Stop/Continue, and allow all source rates to be zero. Bound work per advance and expose progress through empty intervals, so background-only or zero-detection transport cannot freeze acquisition time. RNG streams for ambient must leave the disabled legacy draw order unchanged.

Legacy BSR stays a deliberately post-detection stress component. Keeping its exact old event sequence conflicts with changing its arbitrary pixel-placement law. Recommendation: retain that path unchanged for compatibility, and use correct deposit-site placement for the new ambient path. If the author wants legacy BSR corrected too, explicitly accept changed seeded results and remeasure that compatibility scope. Ambient-only, BSR-only, both additive, and both disabled are separate supported cases. Derived ambient BSR is a nullable readout `ambientDetectedRate/sourceDetectedRate`, undefined at zero source; label estimated rates and the active energy window.

## 6. Studio default decision (A-7)

| Option for the author | Consequence |
|---|---|
| **Recommended after spectrum/transport validation:** ambient enabled at **0.10 µSv/h**, with a named cited spectrum/site/version and an editable 0/off value | Makes absolute background visible in normal use. 0.10 is a representative example supported by §3.2, not “the” indoor/outdoor truth. UI label should say **ambient photon H*(10)** and identify the preset. |
| Start at 0/off until a validated public preset exists | Preserves existing demonstrations; visibly states ideal environment and requires an explicit ambient choice. Recommended interim position if A-1 remains unresolved. |
| Offer separately cited indoor/outdoor presets | Useful only after obtaining numerical spectra/angular models for both; dose-rate ranges alone do not justify two spectrum shapes. |

Keep BSR editable as a separate stress input (for authorised compatibility), with an additional derived ambient BSR readout; making the only BSR field read-only would remove a current capability. Inputs lock with acquisition as current scene/detector inputs do. Show predicted/recorded background cps and the field definition; do not show a universal confidence verdict from raw total counts. Add an empty-scene/background-only acquisition option. No UI implementation or desktop validation was performed this turn.

## 7. Significance and PR-SENS-02 protocol (A-8)

`BackgroundStudy.PeakSnr` (`BackgroundStudy.cs:203–217`) uses the **entire reconstruction**, including the chosen peak and its sidelobes, to compute `(peak-mean)/std`. Its comment calling this a “source-free” reconstruction spread is inaccurate. It is a contrast diagnostic, not a calibrated Gaussian significance or a false-alarm probability.

Recommendation: a Poisson likelihood search using a transported background expectation `b_i`, uncertainty from a finite background-only calibration, and source templates `a_i(theta)` consistent with mask/grid/readout. Fit `n_i ~ Poisson(t*(b_i+s*a_i(theta)))`, `s>=0`; allow only documented background nuisance parameters. Use the maximised improvement `TS=2*(logL_source+background-logL_background)` over the **entire configured search grid**. Calibrate its maximum distribution on independent background-only acquisitions; do not call `sqrt(TS)` a universal sigma, because the nonnegative boundary, low counts and spatial search invalidate that shortcut. Correlation-based studentised peaks can be a lower-cost alternative, but require the same empirical search calibration.

Predeclare exposure, source activity/distance, window, search grid/decoder and calibration time. Evaluate at least 0/0.05/0.10/0.20 µSv/h; source activities spanning strong through background-dominated cases (including zero); lab and 1/5 m geometries; centre/edge/outside field; Cs alone and a Co/Cs nuisance case. Pair ideal/ambient outer seeds for comparisons without using simulated source labels in the inference. Split calibration/training and held-out evaluation seeds; include background spectrum and spatial mismatch, not just perfectly known `b_i`. Calibrate the out-of-field cue independently of the same series used to evaluate it.

Proposed author-set risk target: **<=1% false trusted location per acquisition** on the defined background-only domain, plus a separately reported conditional localisation-quality target (e.g. >=95% within one declared angular resolution element). “Sub-mm” is meaningful for the lab distance, not as the same angular criterion at 5 m. A detection gate and a localisation interval/quality gate can both be required. Test confusion with other isotopes and wrong peaks; strong nuisance sources can have high TS without the desired isotope being localised.

For zero false positives in m independent held-out null acquisitions, the one-sided 95% binomial upper limit is `1-0.05^(1/m)`: **m=299** is the minimum to bound it below 1%. Use at least 1000 null acquisitions per declared configuration (zero-failure upper ~0.299%); with failures use an exact binomial interval. 128 zero-failure seeds alone only bound the rate around 2.31%, and repeated acquisitions sharing an MC-estimated map are clustered, not wholly independent evidence of map uncertainty. Use 128 outer seeds with explicit inner repeats and cluster-aware reporting. Never choose the gate and quote validation on the same acquisitions. Time-remaining estimates use fitted signal/background rates and uncertainty, not a definite verdict.

## 8. Remeasurement scope and cost (A-9)

The driver (`samples/evidence/run_seeds.py:38–160`) already provides complete config cloning for **CLI families**, dotted overrides, isolated cwd, binary overrides, exit/timing capture and config-hash checks. Its **probe** route passes only mode/seed/samplesDir: it does **not** apply CLI config overrides to probes. RTL modes have their own synthetic recipes. `aggregate.py` assumes the current keys/schema. A future ambient run needs a versioned experiment manifest and parsers/keys including field, activity, distance, exposure, window and shield. A distinct output root and recipe identity must prevent mixing ideal/ambient records or silently skipping previous successful runs. No existing manifest/result is changed in this turn.

| Evidence §1 row | Existing families and N to retain | Work before a meaningful absolute-background rerun |
|---|---|---|
| EV-01/03/05/06/08/09/13/31/32 | `sweep`, `sweep_head` 64 each; `precise`, `scan` 32 each; `masktaper` 64; `array` 128; `subcell` 64; `noise_head`, `noise_orig`, `doi`, `align`, `defects` 64 each | Add ambient mean/event response and exposure normalisation. Separate geometric floors/efficiency from finite-count performance; keep manufactured patterns fixed. |
| EV-07 / PR-SENS-02 | `noise` 128 plus new null/gate validation recipes | Add calibrated significance protocol and independent calibration acquisition; existing `bias` need not be rerun solely for additive background. |
| EV-10/11/14/15 | `mixedfield`, `mlem`, `compton`, `compton-strip`, `mixediso`, `mixedstrip` 64 each; `spatial` 128 | Define physical time for weighted studies, new spectral response/calibration maps; background term in MLEM likelihood. Decide whether the EV-11 synthetic demonstration stays as its own ideal bound. |
| EV-02 | `fov` 64 | Replace N0-relative pedestal with a fixed field and a source-activity/time mapping; keep original BSR comparison and independently validate centroid flags. |
| EV-12 | `background` 64, `antimask` 128 | Keep stress curves; add cited field response with explicit exposure and finite background-calibration time. Same ambient dose in both mask positions, transport each position. |
| EV-25 | `shield` 128 | New ray geometry, spectrum-weighted wall response, stated uncollided/secondary scope; preserve old pedestal sweep as a separate controlled experiment. |
| EV-17/21/22 | `rtl_frontend` 32, `rtl_material` 128, `rtl_peak` 32, `deadtime` 64 | Inject independently timed ambient deposits into these source-only recipes; threshold/material scope explicit. Do not imply rerunning unchanged synthetic RTL fixtures adds ambient radiation. |
| EV-23 | `dose` 64 | Hold original frontal calibration fixed for comparison, test isotropic field response separately; extend energy/transport as adopted, report bias rather than enforcing ±13%. Separate-counter model is a further scope decision. |

These listed existing families total **2112 family-seed runs for one new condition**, before gate/null acquisitions or multiple dose/activity/distance settings. Not all entries require all metrics rerun; the author can approve a justified analytic “unchanged” metric while measuring acquisition-dependent metrics. Do not describe a partial set as filling every row.

Measured baseline smoke via the **actual seed driver**, one first seed and one worker: `noise` **11.4 s**, `background` **10.0 s**, `dose` **1.0 s**, all exit 0. Baseline full ensembles for those three cost approximately **24.3 + 10.7 + 1.1 = 36.1 worker-minutes** per condition (linear estimate, excluding new ambient response/gate work). At four dose levels this portion alone is ~2.4 worker-hours. The driver README says some FOV runs exceed ten minutes/seed: **budget at least roughly 10.7 worker-hours for 64 seeds if that runtime applies**, with substantial internal parallelism. This is a planning allowance, not a measured lower bound for this machine. EV-02 also lists a shorter CLI run, which is not the full probe recipe. Running many driver jobs alongside internal parallel loops risks oversubscription and competing with ongoing measurements. No FOV or full rerun was started. Other families' timing and future ambient response cost are **unmeasured**; perform one-seed smoke timings before promising a total completion time.

Executed smoke recipe (outputs all temporary):

```powershell
$reviewDir = Join-Path $env:TEMP 'gcam-ambient-review'
dotnet build src/Gcam.Cli/Gcam.Cli.csproj -c Release --artifacts-path (Join-Path $reviewDir 'cli-artifacts')
python -B samples/evidence/run_seeds.py --out (Join-Path $reviewDir 'driver-smoke') --family noise background dose --n 1 --jobs 1 --cli (Join-Path $reviewDir 'cli-artifacts/bin/Gcam.Cli/release/Gcam.Cli.dll')
```

The small C# probe was built with `dotnet build <temporary AmbientReview.csproj> -c Release --artifacts-path <temporary artifacts>`, then its DLL was run with repo root and the temporary seed-list path. The retained `Program.cs` contains the complete sampling recipe; `ensemble.jsonl` contains per-seed counts/configs/rates. Temp outputs are review aids, not committed evidence. Before implementation evidence is published, move the adopted recipes into the authorised seed workflow with source hashes and provenance, not this turn.

## 9. Proposed test oracles and sample-derived tolerances

| Oracle | Protocol and tolerance derivation |
|---|---|
| Dose/fluence units and mixture closure | Analytic cases at tabulated energies, pure line and two-line mixture; compute `sum(Phi_i*h_i)` independently. Double-precision arithmetic tolerance follows rounding error/conditioning (e.g. ~100 machine epsilons for a short positive sum), not EV-23's percent accuracy. Finite-bin integration uses convergence/quadrature error separately. Catch 3600, 10⁶ and cm²/mm² errors explicitly. |
| Isotropic current / area | No-attenuation face toy geometry: current Phi*A/4; surface total Phi*S/4. For M directional trials with known hit probability p, use binomial quantiles or `k*sqrt(p*(1-p)/M)` where normal approximation is valid. Example M=200000, p=0.25 gives 4-sigma absolute probability tolerance **0.003873**, derived from this sample size. Test rotated geometry and separately rear/side hits. |
| Simple absorption | Monoenergetic parallel rays through a uniform thickness: p=1-exp(-mu*l), same binomial tolerance; wall obliquity uses l=t/abs(cos theta). Choose supported energies and disabled scatter for this closed-form oracle. Do not borrow a GAGG/NIST deviation for another material. |
| Source independence | Fixed ambient seed and fixed transport inputs: ambient response/rate and ambient component event sequence identical with zero/weak/strong sources. With independently sampled acquisitions, count difference has variance **B*(t1+t2)** (Skellam law), or include response-estimation variance. A “within Poisson” comparison without count/time/sample sizes is not a test specification. |
| Counts and exponential timing | Predetermined fixed-rate null toy, M=10000 equal-duration bins, lambda=1: mean-count SE=0.01; a 4-SE mean band is **±0.04**. Estimated Fano ratio has asymptotic SE sqrt(2/M), giving **±0.0566** at 4 SE; verify exact/simulation quantiles for selected low means. For n exponential gaps, the sum is Gamma(n,rate), enabling exact quantile limits. Include zero-count intervals and pending events after Stop/Continue. |
| Pixel/spectrum probabilities | Analytic symmetric toy pixel probabilities and declared incident mixture weights: multinomial/binomial intervals from each test's N and p, corrected for the number of tested bins. Full spectral lines are tested in incident sampling; transported pulse heights need not reproduce incident peak proportions. Test energy conservation, full absorption in a toy limit and correct deposit-site/argmax association. No universal 1-keV line tolerance: measured peak-centroid SE is fitted resolution/sqrt(peak counts), with calibration/binning biases reported. |
| Event vs flood agreement | Independent fixed-time toy processes: compare per-pixel mean/variance and window totals with Poisson/binomial errors. If mean maps are importance-weighted MC, add sample variance of the weights; do not treat proposal histories as Poisson counts. Keep source and response MC streams independent. |
| Exact compatibility | Null/zero ambient leaves legacy BSR-only and disabled records/RNG sequences bit-for-bit identical, including Stop/Continue; deterministic exact equality. If legacy placement changes, this oracle is explicitly withdrawn by author decision rather than weakened. |
| Sanity anchor | Match housing/thresholds and include field/calibration/model uncertainty. The cited 80-s, 4.04-cps anchor implies about 323 counts: rate SE sqrt(323)/80=0.225 cps. Its dose-method discrepancy prevents a narrow H*(10) tolerance. Obtain raw spectrum/counts and an independent field reference before setting a validation bound. |

The existing `Sampling.Poisson` (`src/Gcam.Core/Sampling.cs:21`) uses Knuth below lambda 30 and rounded/clipped Gaussian above it. A plan promising exact Poisson tails in flood maps must decide whether to introduce an exact sampler for the new path; normal-approximation mean/variance tests do not establish low-tail false-alarm accuracy. Continuous exponential-time transport/thinning gives exact Poisson counts in the fixed-rate toy limit without borrowing that approximation. Multiple-site/cascade/intrinsic pulses and post-readout pile-up can violate independent pixel-Poisson assumptions; test the declared pre-readout event definition first.

## 10. Decisions and unavailable work

Author/planner decisions needed before implementation:

1. Adopt full six-face transport and the high-energy prerequisites, or explicitly narrow the first implementation and its evidence claims. A front-only result cannot satisfy the current A-3/done-when wording.
2. Obtain/choose a **numerical public incident spectrum with continuum and geometry**, and a validated angular convention. Until then the “terrestrial typical” weights and continuum fraction remain **needs source**. Diagnostic monoenergy fields may test APIs but cannot fill the ambient evidence column as natural-background performance.
3. Choose A-7 default and retain a separate editable BSR stress knob; approve background-only Studio/engine semantics.
4. Choose false-alarm/localisation risk targets, calibration exposure, source activity/distance/time grid and whether repeated live confidence queries require a sequential false-alarm protocol.
5. Decide legacy BSR bit-compatibility versus corrected placement, intrinsic-material scope, and the dose-channel/separate-counter scope.
6. Adopt the per-family physical-time/injection changes and budget the A-9 ensemble work. The original ideal results remain labelled best-case references.

Not completed: inaccessible reference tables and finished-crystal CsI intrinsic assay; an adopted terrestrial fluence spectrum/continuum fraction; a full cosmic spectral/angular dataset; independent certification of ICRP table values; all-face/head transport prediction; ambient localisation/stripping/false-alarm ensembles; UI validation; a full implementation or updated existing evidence documents. These are explicitly outside this turn or blocked on the stated premises. No number has been filled in the existing Evidence table.

## APPROVAL REQUESTS

**None.** No destructive, state-changing, installation, process-control or upload command is needed for this review. The open items above are scientific/product/scope decisions for the next turn, not requests to execute an irreversible command.
