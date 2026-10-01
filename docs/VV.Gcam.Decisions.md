# VV.Gcam.Decisions — the author's decisions on the Gcam requirement set, with reasons

Scope: decisions the author took while the V&V document set was built ([URS](VV.Gcam.URS.md),
[PRS](VV.Gcam.PRS.md), [Limitations](VV.Gcam.Limitations.md), [VV.Studio.SRS](VV.Studio.SRS.md) /
[SDS](VV.Studio.SDS.md)). Each entry gives the decision, the **reason as the author gave it** (quoted in Korean
where it was said in Korean), what was rejected, and where it landed. Physics results are not decisions; they are
evidence, in [VV.Gcam.Evidence](VV.Gcam.Evidence.md).

**At a glance**
- 38 decisions (D-01 … D-38), all taken with the author on 2026-10-01, each with the author's own words.
- The ones that shape the product most: non-cyclic decoding instead of mask rotation (D-17, D-19), no built-in
  radioactive source (D-18), dose rate required at NSS-1 range (D-21), the software path for the narrow field of
  view (D-16), a separate small dose counter (D-37), comparison only with same-class imagers (D-32), Korean law
  only (D-33).
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
| D-19 | No mask-rotation (mask / antimask) hardware | "마스크를 돌리는 하드웨어는 너무 비용이 커" | iPIX-style rotating mask | LIM-07 |
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

## Still open (not yet decided)

| Item | Where |
|---|---|
| Current text of 원자력안전법 and the meter-calibration rules (2016 consolidated text used) | PRS §6, PR-REG-02 |
