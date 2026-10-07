# VV.Gcam.Decisions — the author's decisions on the Gcam requirement set, with reasons

Scope: decisions the author took while the V&V document set was built ([URS](VV.Gcam.URS.md),
[PRS](VV.Gcam.PRS.md), [Limitations](VV.Gcam.Limitations.md), [VV.Studio.SRS](VV.Studio.SRS.md) /
[SDS](VV.Studio.SDS.md)). Each entry gives the decision, the **reason as the author gave it** (quoted in Korean
where it was said in Korean), what was rejected, and where it landed. Physics results are not decisions; they are
evidence, in [VV.Gcam.Evidence](VV.Gcam.Evidence.md).

**At a glance**
- 49 decisions (D-01 … D-49), taken with the author on 2026-10-01, 2026-10-02 and 2026-10-06, each with the
  author's own words or a note that none was given.
- The ones that shape the product most: non-cyclic decoding instead of mask rotation (D-17, D-19), no built-in
  radioactive source (D-18), dose rate required at NSS-1 range (D-21), the software path for the narrow field of
  view (D-16), a separate small dose counter (D-37), comparison only with same-class imagers (D-32), Korean law
  only (D-33).
- **Performance-critical (2026-10-06), both measured:** spatial resolution as an angle at field distance (D-41) —
  1.30° pair separation at 1 m with a pixel-area MLEM (D-46 … D-48) and precision restated in angle (D-49); the
  Cs-137 detection limit under Co-60's Compton continuum (D-42) — with the side-window reference (D-43), a stripped
  trust statistic (D-44) and a 10 µSv/h claim ceiling (D-45).
- Still open: the current text of the laws cited.

Rules: IDs are stable (`D-nn`); a reversed decision keeps its row, marked *superseded by D-nn*. A row with no
stated reason says so instead of inventing one.

## 2026-10-01

### Document set

| ID | Decision | Reason (author) | Rejected | Lands in |
|---|---|---|---|---|
| D-01 | SRS and SDS are sub-sections of the VV keyword: `VV.Studio.SRS`, `VV.Studio.SDS`, then `VV.Gcam.*` | the VV keyword already covers requirements and traceability, so no new keyword is needed | new top-level keywords `SRS.*`, `SDS.*` | file names |
| D-02 | No separate RS (abstract requirement spec) layer | "지금 문서 상황에서 SRS가 그걸 대체할 수 있다면 생략해도 상관없어" — the Studio SRS fills it for the viewer; for other subsystems the PRS is the lowest level | an RS between PRS and SRS | PRS §2 |
| D-03 | The PRS covers the **whole Gcam product**, centred on the hand-held concept | the project already holds a "if this were built as a device" specification (the hand-held design study, EV-24 … EV-28) | PRS for the Studio viewer only | PRS |
| D-04 | Known product limitations get their own document, with fixes and their costs | "'Limitation' 문서를 두어서 명확하게 알리는 게 좋을 것 같아. 그를 해결할 방법, 그 대가가 무엇인지도" | burying them in the PRS gap list | [Limitations](VV.Gcam.Limitations.md) |
| D-05 | Ir-192 is handed to the pair session as a TODO document | the pair session takes it over | doing it in this session | Ir-192 in the engine (EV-20), done 2026-10-01 |

### Users and needs

| ID | Decision | Reason (author) | Rejected | Lands in |
|---|---|---|---|---|
| D-06 | Primary user: radiation-safety technician; non-specialists must also be able to use it | "안전 기술자가 아닌 사람도 이걸 사용한다고 가정해야 해" | technician-only product | URS §2 (U1, U2), UN-18 |
| D-07 | Need review accepted; needs rewritten solution-free, split, and dose rate / FOV / record / battery added | review accepted as given | the first draft | URS §3 |
| D-08 | UN-11 confirmed — "no source" is checkable on the energy histogram | "이건 측정 시 에너지 히스토그램으로 확인할 수 있을거야" | — | UN-11, PR-IMG-07 |
| D-09 | UN-12 conditional on ghosting being solved first | "섬광체 카메라의 문제는 Ghosting인데 그게 해결되는 게 선행이야" | unconditional out-of-field cue | UN-12, PR-IMG-08, LIM-07 |
| D-10 | UN-10 (distance) confirmed and raised to high priority | priority raised by the author | priority C | UN-10 |
| D-11 | UN-13: remaining time may be estimated, never asserted | "예측할 순 있어도, 단정지어서는 안 돼" | a definite "wait N s" | UN-13, PR-SENS-02 |
| D-12 | UN-07 conditional: accepted as a target if the field allows | "가능하면 수용인데, 환경이 그렇게 돌아갈까?" — field feasibility (background, motion, narrow field) is unverified | unconditional acceptance | UN-07; PRS §4 |
| D-13 | UN-04 (one-handed, gloved, full search) withdrawn | "굳이 안 넣어도 될 거 같아" | — | UN-04; PR-PHY-01…03 become design targets |
| D-14 | UN-17: battery remaining-capacity indicator only | "배터리 잔량표시만" | a full-shift run-time need | UN-17, PR-PHY-06 |
| D-15 | Display: a portable device with the software, **dockable** on the instrument — docked for aiming, undocked for remote use | "둘 다 가능한 니즈로 바꾸는 게 좋겠는데. sw가 설치된 portable device를 dockable한 방식으로" | phone held in the other hand; screen built into the device | UN-05, UN-19, PR-PHY-04 |

### Product concept

| ID | Decision | Reason (author) | Rejected | Lands in |
|---|---|---|---|---|
| D-16 | Narrow FOV: **software path** — usable field of non-cyclic decoding (~±5°), out-of-field cue, hand sweep with IMU / VIO; rank-up + tapered channels kept as the hardware fallback | adopted after D-17 (non-cyclic decoding) and D-19 (no moving hardware on cost) made it the natural first step | pan-tilt head; mask rotation; redesign of the head now | PR-IMG-10, LIM-01 (the usable field was later simulated: ±6–7.5°, EV-02) |
| D-17 | Ghost suppression by **non-cyclic decoding** | "비순환 디코딩 유지" | — | PR-IMG-01, LIM-07 |
| D-18 | **No built-in radioactive source** | "내장은 안 돼. 비싸기도 하고 취급도 어려워" — it would also make the device a radiation device under 원자력안전법 제60조 | built-in reference source for gain tracking | PR-REG-01, PR-ENV-02, LIM-03 |
| D-19 | No mask-rotation (mask / antimask) hardware | "마스크를 돌리는 하드웨어는 너무 비용이 커". **Basis restated 2026-10-02:** the seed-ensemble measurement withdrew the earlier "a calibrated background subtraction matches the antimask" — at equal total time the antimask has ~20–30 % lower RMS at every background level, both sub-mm (EV-12). The author kept the decision: "어차피 더 좋았어도 기구 제작 측면에서 비용이나 안정성 측면에서 디메리트가 커서 마스크 회전은 기각하려고 했어" — the rotation mechanism's cost and mechanical stability outweigh the gain | iPIX-style rotating mask | LIM-07 |
| D-20 | Gain stability: **peak tracking + temperature-scheduled window widening** | chosen from the LIM-03 options | user check source; thermal control | PR-ENV-02, LIM-03 |
| D-21 | **Dose-rate measurement is required**, range per IAEA NSS-1 (10 mSv/h, ±50 %), and the user's **calibration obligation** is supported | "선량률 기능은 넣어야 하고 이미 기능 자체가 있어야 할걸? 교정 의무는 추가"; "NSS 기준으로" | dose rate left to a separate survey meter | PR-SAFE-01, PR-REG-02 |
| D-22 | **No imaging requirement in high fields** (~10 mSv/h and up) — but the dose reading keeps the NSS-1 over-range indication | "그 정도의 고선량이면 장비를 가동하기 전에 ADR이 먼저 난리치지 않을까?" — the over-range indication stays because a paralysable front end under-reads (EV-22) | faster crystal / attenuator for high-field imaging | PR-SENS-05, LIM-02 |
| D-23 | **No fixed false-positive-rate target** for "no source" | "백그라운드 선량에 따라서 완전히 달라질텐데 이걸 넣는 순간 제품이 너무 엄격해진다" | a numeric false-alarm target | PR-IMG-07 |
| D-24 | Plain category for non-specialists: **"industrial" (산업용)** only | "산업용" | a multi-category scheme | PR-NRG-06 |
| D-25 | Search records in the product's **own file format** | "자체형식" | ANSI N42.42 | PR-SW-03 |
| D-26 | User-interface device: **Windows tablet** | "Windows 태블릿" | Android / iOS | PR-PHY-04 |
| D-37 | **Dose rate from a separate small counter** (e.g. a GM tube or a small energy-compensated scintillator) outside the tungsten shield, read by the main unit over a **data I/O link**; the imaging head is not the dose channel | "소형 계수기 같은 건 싸니까 이걸 데이터 I/O로 받는 게 더 싸고 직관적일 것 같아" — chosen from the LIM-09 options after the simulation showed the collimated imaging head reads only frontally | side-looking crystals in the shield; a frontal-only label | LIM-09, PR-SAFE-01, PR-SAFE-02, PR-SENS-05 |
| D-34 | GCAM Studio is **not** grown into the product UI; it stays the simulator's engineering viewer, and SR-SEC-01 (no file I/O) stands | "아직은 키우지 말자. 애시당초 이 리포 시뮬레이터였고 사실 이건 내 포트폴리오용 데모거든" | Studio as the product-UI prototype | PRS SS-4 |

### Reference conditions and benchmarks

| ID | Decision | Reason (author) | Rejected | Lands in |
|---|---|---|---|---|
| D-27 | Use conditions researched at nuclear-plant level, then **relaxed to iPIX level** | NPP level asked for first ("원자력 발전소에서 요구하는 수준"), then "IPIX 수준으로 완화" — no further reason stated; context: the stricter values exceeded the same-class product on every row | ANSI N42.17AC extreme / NSS-1 as the target | URS §4, PR-DUR-01…04, PR-PHY-01 |
| D-28 | IP65 | accepted ("IP 등급 수용") | IEC 62327 value (not public) | PR-DUR-04 |
| D-29 | Reference sources are **real high-energy industrial sources** (Ir-192 and Co-60 radiography, Cs-137 and Co-60 gauges) | "실제로 사람이 맞닥뜨릴 수 있는 강한 에너지 동위원소를 지정해주는 게 좋겠어" | an IEC 62327 test source | URS §5 RS-1 … RS-4 |
| D-30 | The radionuclide set Cs-137, Co-60, Ir-192 (+ Co-57 for tests) is enough | "지금 핵종들이면 충분해보여" | adding Se-75 etc. | URS §6, PR-NRG-06 |
| D-31 | Sensitivity criteria from **commercial imagers' test specifications** (iPIX, Polaris-H, GeGI) | the IAEA / CERN comparison the author remembered could not be found ("아마 논문을 내린 것 같네") | measurement distances from that paper | URS §5 |
| D-32 | **Only same-class imagers are pass criteria**: Gcam is a scintillator coded-aperture camera, so iPIX counts; Compton (Polaris-H) and HPGe (GeGI) are information only | "이건 섬광체 카메라야. 컴프톤 카메라와 기준을 동일시 할 수 없어" | Polaris-H parity (RC-2, PR-SENS-08 withdrawn) | URS §5, PR-SENS-07 / -08, PR-IMG-09 / -10 |

### Regulation

| ID | Decision | Reason (author) | Rejected | Lands in |
|---|---|---|---|---|
| D-33 | Applicable law: **Korean law only** | "일단 관계 법령은 국내에 집중하는 걸로" | US / EU tables | PRS §6 |

### Document set, continued

| ID | Decision | Reason (author) | Rejected | Lands in |
|---|---|---|---|---|
| D-35 | The V&V documents are **self-contained**: they never send the reader to the repository's working notes; the simulation results they rest on are carried in their own evidence register | "VV 문서가 다시 AGENTS 쪽으로 보내면 안 돼. 그럼 VV 문서 자체가 완결성이 떨어진다는 소리야" | requirement rows citing the working results log by theme number | [Evidence](VV.Gcam.Evidence.md); every VV document |
| D-36 | A one-page [Overview](VV.Gcam.Overview.md) opens the set; every VV document starts with an at-a-glance summary; change history leaves the requirement rows for the evidence register | accepted as proposed ("제안대로 작성해서 나한테 알려줘"); the proposal's reason: requirement documents stay dense for traceability, so a human reader needs a page above them | rewriting the requirement documents in a looser style | [Overview](VV.Gcam.Overview.md); every VV document |
| D-38 | The **Limitations** document stays in the V&V set | the author first asked whether it belonged with the working notes, then accepted the recommendation ("수용"): it is the product's disclosure of what it cannot do, and the PRS gap list points to it | moving it to the working notes | [Limitations](VV.Gcam.Limitations.md) |

## 2026-10-02

### Evidence refresh over seed ensembles

The Monte Carlo quotes were re-measured over 32–128 seeds per study; each change of interpretation was put to the
author with its measured numbers and a recommendation (the numbers are in
[Evidence §9](VV.Gcam.Evidence.md#9-model-history)).

| ID | Decision | Reason (author) | Rejected | Lands in |
|---|---|---|---|---|
| D-39 | PR-MFG-01's 40 µm machining tolerance gets a **defined gate**: seed-mean localisation RMS ≤ 1.25 × the seed-mean RMS of the ideal mask (40 µm: 1.13×, passes; 80 µm: 1.43×, fails). The old "within ~2× the ideal floor" criterion is withdrawn; the result holds for the study's six fixed manufactured patterns, not for an arbitrary mask | accepted as proposed; no further reason stated. The proposal's reason: the ideal RMS spreads too much between seeds for a per-seed 2× test to separate 40 from 80 µm (108 / 128 against 100 / 128 seeds pass) | keeping 40 µm only as an author-chosen conservative target with no gate | PR-MFG-01, EV-30 |
| D-40 | **Every MC number is quoted over a seed ensemble** (mean ± SD or median [quartiles] with N; picks and gates as k / N), and the measured reinterpretations are accepted: thickness ~10 mm from a defined wide-field recipe; two count gates (collapse below ~25, sub-mm from ~250); the rank-11 field as an observed 31 / 32 pass; 16 × 16 "about a third" better; shield picks with frequencies, "not carriable" judged against PR-PHY-01; one pooled cascade fit; depth range < 0.15 m and censored widths; the front-end width not called electronic noise; the Co × 2 spatial limit as 126 / 128; the old crystal-gap series kept as unverified, replaced by a labelled new experiment | for the thickness: "수치는 분명히 하는 게 좋겠지"; for the rest, after reading them: "나머지 9건도 읽어보니 합리적이야. 수용." | single-seed quotes; rewriting conclusions without the author | every EV entry with an MC number; PR-IMG-06, PR-IMG-10, PR-NRG-02 … -05, PR-SENS-03, PR-MFG-03, PR-RNG-01 |

## 2026-10-06

### Performance-critical claims: resolution benchmark and Cs-137 under Co-60

Two headline performance claims were questioned by the author and are marked **performance-critical**: they are not
quoted as product performance until the measurement each decision names exists. Author: "이 둘은 Performance
critical한 부분이니, 명확하게 기록해두는 게 좋겠어."

| ID | Decision | Reason (author) | Rejected | Lands in |
|---|---|---|---|---|
| D-41 | **Spatial resolution is stated as an angle at the use distance, against a declared baseline.** The baseline is the same head's geometric angular resolution, cell / D (lab 1 mm / 60 mm ≈ 0.955°; hand-held 1 mm / 55 mm ≈ 1.04°), with cross-correlation decoding; a reconstruction's gain is reported as two-source angular separation at field distance (e.g. 1 m and 5 m, the EV-02 distances) relative to that baseline, under a stated resolved-pair criterion (valley depth, seed pass rate) and stated counts. Until measured, EV-11's "2–3 mm" stays a **lab near-field result** (source 100 mm from the mask) and is not quoted as field performance: seen from the mask its spacings are 0.86–1.7°, but at 100 mm the projected element is c·(S + D) / D ≈ 2.7 mm, so neither the mm nor the angle transfers to field distance | "이 장비는 원거리에서 방사선을 재도록 만들어져 있는데, '100mm에서 위치 분해능이 높다'라는 얘기가 과연 설득력이 있을까? 애시당초 어느정도 기준점이 되어야지 위치 분해능이 높은지 근본적으로 그 기준부터 세우는 게 맞아보여." — accepted the proposal ("둘 다 수용할게") | quoting mm at 100 mm; converting the near-field result to an angle and quoting that | PR-IMG-04, EV-11 (measured 2026-10-06) |
| D-42 | **Cs-137 under Co-60 is a background-subtraction problem with a statistical cost, quantified before it is claimed.** Physics: Co-60's Compton continuum (edges ≈ 963 and 1118 keV) lies under the 662 keV window, so window counting cannot tell the two apart; Cs-137's photopeak still stands on that continuum, so spectral subtraction (stripping, side windows) recovers the net Cs count, and the price is the continuum's Poisson noise — the Cs detection limit worsens as Co : Cs grows. The claim is stated as recovered count **plus** its cost: a detection limit (critical level / MDA) or localisation success versus Co : Cs and counts, for window counting and for stripping; a literature review of published practice comes first | the author had taken it as unavoidable: "세슘의 Photon peak가 코발트의 컴프톤 피크에 덮어 씌워지는 걸로 알고 있는데 이건 어쩔 수가 없는 걸로 알아. 관련 문헌을 확인해보지 못했거든." — accepted the proposal ("둘 다 수용할게") | "cannot be separated" as a stated limit; quoting the recovered count without its noise cost | PR-NRG-04, EV-15 (measured 2026-10-06) |
| D-43 | **The stripping reference is a side window above the Cs-137 peak (727.87–860.21 keV)**, not a Co-60 photopeak window; the photopeak windows are kept as comparisons | chosen from the measured options; no further reason stated. The proposal's reason: the product holds its gain without a built-in source (D-18), and with the co1332 photopeak window R moves ~13 % per 1 % gain error (its lower edge is 1 σ above the 1173 keV line), against ~0.8 % for the side window, whose R is also flat over the array and direction; the price is a 4–5 % higher detection limit | the co1332 window (EV-15's earlier choice); both Co-60 photopeaks (17 % lower limit, ~3.6 % per 1 % gain) | PR-NRG-04, EV-15, LIM-08, LIM-03 |
| D-44 | **A separate trust statistic and thresholds for stripped images**, selected per configuration on Cs-free Co-60 + ambient acquisitions by the PR-SENS-02 rule (≤ 1 % false trusted) | accepted ("도입") from the proposal; no further reason stated. The proposal's reason: the existing gate was calibrated on ambient-only nulls, and under Co-60 it trusts Co-60's downscatter as a Cs-137 source in nearly every acquisition | claiming no Cs-137 location performance under Co-60 (counts only) | PR-NRG-04, PR-SENS-02, EV-15 |
| D-45 | **PR-NRG-04 claims performance for Co-60 fields up to 10 µSv/h at the head**; 35 µSv/h is measured and reported as beyond the claim | chosen from the proposed ceilings; no further reason stated. The proposal's reason: 10 µSv/h is the band boundary at which the same-class benchmark (iPIX) changes its mask (< 10 / > 10 µSv/h) | 3.5 µSv/h; 35 µSv/h | PR-NRG-04, EV-15 |
| D-46 | **PR-IMG-04 is claimed with an MLEM whose forward model integrates each pixel's area** (analytic, no Monte Carlo; opt-in in the engine, whose default pixel-centre MLEM stays unchanged) | chosen from the measured options; no further reason stated. The proposal's reason: on a head with about one pixel per projected mask cell, the pixel-centre matrix splits single sources after a few tens of iterations, while the pixel-area matrix resolves 1.25 elements at 1 m — the same as a matrix built from transported maps | the engine's pixel-centre MLEM (1.5 elements, only at ≤ ~15 iterations) | PR-IMG-04, EV-11 |
| D-47 | **Spatial resolution is claimed inside the fully coded field (±3.6°)**; the usable non-cyclic field (±7°) is measured and reported, not claimed | chosen from the options; no further reason stated. The proposal's reason: consistent with the narrow-field decision (LIM-01, D-16), and in the partially coded field the decoders' artefacts outrank a true second source (cross-correlation 2 elements: 99 % against 68 %) | claiming over ±7° | PR-IMG-04, EV-11 |
| D-48 | **"Resolves a pair" means:** two peaks found without truth by topographic prominence, each within min(Δ / 2, 1 element) of a different source, valley ≥ 25 % of the lower peak, the second peak above a significance floor calibrated on single-source acquisitions, in ≥ 95 % of acquisitions at Δ and at every larger Δ, with single-source false splits ≤ 5 %. The floor was added after the shape-only test failed every 1 : 4 pair on false splits from a strong source's side maximum | v = 0.25 with the 5 % false-split limit: chosen from the options. The floor: "지금 기준 보강 턴 추가" — the author chose to strengthen the criterion at once rather than defer it or drop unequal pairs | v = 0.5 (kept as a sensitivity); a shape-only test; a two-versus-one likelihood ratio (far costlier, same calibration) | PR-IMG-04, EV-11 |
| D-49 | **PR-IMG-02 is restated in angle at 1 m, averaged over source position** (0.38° RMS at 250 counts, 0.21° at 1000), from TODO-34's measurement; the 0.25 mm near-field floor stays in the evidence only as one on-axis sampling phase | chosen as recommended; no further reason stated. The proposal's reason: the same head at the same near-field distance, averaged over position, gives 1.38 mm at 250 counts (EV-34 1.00 mm), so the old floor was not the head's precision | a separate re-measurement task first | PR-IMG-02, EV-09 |

## Still open (not yet decided)

| Item | Where |
|---|---|
| Current text of 원자력안전법 and the meter-calibration rules (2016 consolidated text used) | PRS §6, PR-REG-02 |
