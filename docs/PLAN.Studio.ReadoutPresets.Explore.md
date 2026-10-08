# TODO-41: imaging qualification exploration

Status: development exploration, 2026-10-08. No qualification published, rule pinned or preset tuned. Existing uncommitted turns 3-5 and pinned v1-v3 artifacts are preserved. The read-only main-tree plan, its five corrections, the earlier review and all turn reports were read before measurement.

## Finding and proposed decision

Use a source-localisation accuracy-versus-exposure curve as the primary yardstick. Keep calibration success as a hard condition. Do not use the proposed squared RMS ratio as an unconditional measurement-time factor: the measured curves have bias-dominated regimes and remote-peak selections, and their ratio changes with count level. A scalar relative gate also permits two inaccurate paths to pass together. An author-selected absolute bearing-accuracy target is needed.

Uniform flood correction is appropriate, but equalising mean pixel counts cannot undo multi-crystal spatial migration or restore lost coded-shadow contrast. The existing weakest/median sensitivity gate penalises an effect that is routinely corrected; the corrected image still needs independent localisation testing. The measured large penalties below are outcomes of the declared decoder/readout combination, not a reason to adjust the LUT or discard histories.

## Measurement contract and declared count-law correction

Reference: ideal direct largest-summed-deposit crystal, exactly as `ReadoutStudy.Localise`, with total true deposited energy in [0.85 E, 1.15 E]. No optical fluctuation, ADC loss or pile-up. This is a sound ideal-readout benchmark for the same transported detector, not a claim of a realizable direct electronic instrument. The window itself lies above the physical 50 keV trigger, so no reference trigger cut is needed. All Compton histories are eligible; no first-interaction or single-crystal selection improves the reference.

Anger: same photon histories; four-output DPC 10/1000 ohm, matched virtual sensors with unlimited microcells, specular 0.98, eight DOI bins, 100,000 optical photons/bin, 2,000 scored calibration histories per crystal, 32 histogram bins/crystal. Baseline keeps Legacy solve/LUT; fine heads use Adjoint/LatticeGrowth. LUT position plus per-crystal calibrated energy, with the same [0.85 E, 1.15 E] window in keV, as the imaging comparison in `ReadoutStudy`. This deliberately differs from v3's global-code-sum window, which is retained for the crystal diagnostics. The two energy windows have identical physical widths, but cannot select identical events: energy noise and continuum admission are part of readout performance. Anger can admit more events at 1332 keV; extra window counts need not be extra source information.

Each line has its own blind calibration and correction flood. This follows v3's line matrix; it is not evidence for a single 662 keV calibration reused at 1332 keV. Current Studio does not apply the new sensitivity correction in its physical projection, so these results describe the proposed corrected workflow, not current shipped Studio performance.

Geometry: Studio presets, 10 mm GAGG, gap 0.1 mm, entrance absorber 0.15 mm, backing 2 mm; finite 10 mm tungsten MURA 2x2 mosaic. Source plane is 1,000 mm from detector (S=1,000-D), signal-only monoenergetic photons, not a Co-60 cascade. Tungsten attenuation uses the engine's energy-dependent law. There is no background, saturation, network RC or measured reflector data.

| Head | N / pitch mm | Rank / cell mm / D mm | Nominal FCFOV half mm | Source offsets a mm | Element angle degrees |
|---|---|---|---|---|---|
| Baseline | 12 / 1 | 7 / 1 / 60 | 58.333 | 26.250 | 0.955 |
| Sharp | 30 / 0.6 | 13 / 0.7 / 80 | 56.875 | 25.594 | 0.501 |
| Wide | 34 / 0.75 | 17 / 1 / 50 | 170.000 | 76.500 | 1.146 |
| High-res | 44 / 0.4 | 13 / 0.5 / 100 | 32.500 | 14.625 | 0.286 |

Nine positions, equally weighted: (0,0), (+/-a,0), (0,+/-a), and all four (+/-a,+/-a) combinations. Here a=0.45 times the nominal FCFOV half-width; all positions are inside the FCFOV and inside the reconstruction grid. This tests the central field; it does not qualify the whole nominal FCFOV, arbitrary distances or extended/multiple sources.

Decoder: the actual Studio `SceneConfigBuilder` finite-mask cross-correlation geometry, half extent 0.95 times nominal FCFOV half and step (cell/D)*1,000/4, with the engine's default Tent interpolation. Neither grid nor interpolation is adjusted after results. Scratch caches the exact engine `PointResponse` coefficients and uses double precision dot products and engine interpolation. An ordinary engine decode checks the first bootstrap image of every head/line/seed against the cached implementation to 1e-9 mm. MLEM is not used.

Flood correction: replay the line's actual uniform calibration flood with its original response/ADC streams, now assigned through the finished LUT and per-crystal energy window. For each path independently, histogram accepted assigned crystals and weight image pixel k by mean(flood)/flood[k]. Direct has its own total-deposit window and histogram. There is no truth denominator, clipping, smoothing, interpolation, gain retuning or validation-data fit. An empty correction bin is a hard failure. Multiplicative weighting equalises mean sensitivity while retaining its noise cost; raw accepted counts, not corrected sums, define exposure levels.

Exposure and pairing: each position has 120,000 detector-biased emission proposals, retaining null/rejected outcomes and the complete joint direct/Anger outcome. `ListModeSource`'s weight-bound rejection makes retained events unit-count physical samples. At a fixed position, mean H proposals represents mean H/WeightBound physical emissions; the bound is identical for both readouts. H is chosen from the pool's direct efficiency to give expected direct accepted counts 250, 1,000 or 4,000. Both images in a pair receive exactly the same sampled proposals, so efficiency changes are included. There is no truncation to equal accepted counts. Different positions have separately specified exposures to the same nominal direct count level. There are 64 paired trials per position and outer seed. Pool estimation error and finite-pool optical fluctuations remain limitations; no trial is a new outer calibration seed.

The original pilot and supplementary Wide/High measurements bootstrap exactly H complete proposals. This preserves conditional spatial distributions, but fixed detector-biased proposal counts give sub-Poisson total accepted counts: if a=p/WeightBound is a marked proposal's acceptance, count variance is H*a*(1-a), whereas physical low-efficiency photon counts are approximately Poisson. This count-law issue was identified during analysis. Baseline/Sharp (including the rate cell) were therefore remeasured with a Poisson(H) proposal count, drawing the same complete joint history marks. Poisson thinning gives the correct Poisson marked-event count law at fixed expected exposure. The sampler sums exact bounded-mean Knuth Poisson variates (mean <=20), without the engine's large-lambda Gaussian approximation. Calibration, optical budgets, source positions, decoder, windows, streams and 64-trial budget stay unchanged; the existing isolated diagnostics are reused. Original fixed-H measurements are retained, not overwritten or chosen per cell. This is a measurement correction, not a per-preset tolerance change. Wide/High remain explicitly supplementary fixed-H results; their rare-peak/count uncertainty cannot be presented as an exact physical time-factor measurement. No qualification is inferred from either version.

Rate: Sharp 662 keV, target 1,000 direct counts only. Use the exact same sampled histories as the isolated case, but all scored deposits (including continuum and subthreshold hits) become four-channel pulses with Poisson gaps at 1e5 scored input hits/s across the array. Process with `ReadoutPulseProcessor.Process`, including summation, hold, dead time and rearm. Direct retains one ideal event per history; no dominant-hit approximation substitutes for the rate image. The isolated correction remains fixed. The rate denotes scored crystal inputs, not emitted source photons. Resampling realized channel charges is a conditional history bootstrap; rate arrivals and ADC noise are fresh.

## Statistics and uncertainty

Angular errors are signed bearing-coordinate differences: ex=atan(x_est/1000)-atan(x_true/1000), likewise y. Radial-coordinate RMS R=sqrt(mean(ex^2+ey^2)), over trials and positions, includes bias and every wrong peak. This is the tangent bearing coordinate norm, not a separately fitted geodesic angle. F=R_A^2/R_D^2 is a ratio of equally weighted outer-seed mean squared errors, not a mean of seed ratios. Mean bias B=sqrt(mean_position[(mean_trial ex)^2+(mean_trial ey)^2]); opposing position biases cannot cancel. B is finite-exposure mean error, not a proved infinite-count bias. Within-position variance is also reported in scratch.

Only T41_DEV4 was used: 41000001, 41104730, 41209459, 41314188. Outer seed randomises optics, calibration, held-out transport and response. Purpose streams for calibration and imaging are separate. There are no selection, confirmation or repair observations.

Intervals are exploratory two-sided 95% Student cluster intervals (df=3). For F, retain pairing: with per-seed MSEs a_s,d_s, a=mean(a_s), d=mean(d_s), F=a/d, u_s=(a_s-F*d_s)/a; log-F half-width is 3.182446*sd(u_s)/sqrt(4), exponentiated about F. RMS and B intervals apply Student intervals to their squared statistics before square root. Diagnostics use per-seed Student intervals. Four clusters and multimodal errors do not justify exact 95% coverage or publication-grade upper bounds. They are neither 2,304 independent calibration trials nor a confirmation ensemble. No failed calibration is removed to compute a surviving-seed interval.

For zero or very rare diagnostic errors, a degenerate/negative raw Student interval is not a population probability bound; confirmation requires appropriate rare-event/cluster treatment. The tables do not establish zero population error or a guaranteed infinite-count bias. Finite empirical source pools also contribute to the high-count mean error and require refinement before a numerical gate is frozen.

## Measured localisation

All angles are degrees. Square brackets are the exploratory 95% cluster interval for F. Baseline/Sharp use the corrected Poisson proposal ensemble; Wide/High are supplementary fixed-proposal ensembles. The localisation table includes every measured count level; uncertainty on RMS and bias is in the following table and `summary.json`.

| Head | keV | Direct target | Rate cps | F [95%] | RMS direct / Anger | Bias direct / Anger | Mean Anger count |
|---|---:|---:|---:|---|---|---|---:|
| Baseline | 661.7 | 250 | 0 | 2.968 [2.608, 3.378] | 0.9021 / 1.5542 | 0.3381 / 0.8763 | 250.4 |
| Baseline | 661.7 | 1000 | 0 | 21.361 [13.868, 32.904] | 0.1871 / 0.8649 | 0.1602 / 0.4039 | 1000.9 |
| Baseline | 661.7 | 4000 | 0 | 4.284 [2.421, 7.578] | 0.1632 / 0.3379 | 0.1616 / 0.1788 | 4002.1 |
| Baseline | 1332.5 | 250 | 0 | 1.228 [1.070, 1.409] | 2.0964 / 2.3233 | 1.5556 / 1.7969 | 298.5 |
| Baseline | 1332.5 | 1000 | 0 | 1.533 [1.392, 1.688] | 1.7347 / 2.1476 | 1.4094 / 1.8323 | 1195.8 |
| Baseline | 1332.5 | 4000 | 0 | 1.830 [1.646, 2.036] | 1.5433 / 2.0880 | 1.4625 / 1.9172 | 4781.3 |
| High | 661.7 | all | all | calibration failed | undefined | undefined | undefined |
| High | 1332.5 | all | all | calibration failed | undefined | undefined | undefined |
| Sharp | 661.7 | 250 | 0 | 32.668 [9.835, 108.517] | 0.1669 / 0.9542 | 0.0634 / 0.3212 | 250.2 |
| Sharp | 661.7 | 1000 | 0 | 4.534 [1.302, 15.792] | 0.0738 / 0.1571 | 0.0605 / 0.0796 | 1000.8 |
| Sharp | 661.7 | 1000 | 100000 | 3.802 [1.891, 7.644] | 0.0738 / 0.1439 | 0.0605 / 0.0806 | 916.0 |
| Sharp | 661.7 | 4000 | 0 | 2.754 [2.282, 3.323] | 0.0595 / 0.0987 | 0.0561 / 0.0836 | 4002.9 |
| Sharp | 1332.5 | 250 | 0 | 3.544 [3.274, 3.835] | 1.0953 / 2.0619 | 0.4273 / 1.3943 | 275.8 |
| Sharp | 1332.5 | 1000 | 0 | 91.139 [35.332, 235.091] | 0.1491 / 1.4236 | 0.0791 / 1.0558 | 1099.7 |
| Sharp | 1332.5 | 4000 | 0 | 119.640 [66.019, 216.813] | 0.0955 / 1.0449 | 0.0828 / 0.7625 | 4400.5 |
| Wide | 661.7 | all | all | calibration failed | undefined | undefined | undefined |
| Wide | 1332.5 | 250 | 0 | 2.314 [2.086, 2.568] | 5.2417 / 7.9740 | 2.5777 / 5.5517 | 268.6 |
| Wide | 1332.5 | 1000 | 0 | 16.043 [11.889, 21.648] | 1.5746 / 6.3071 | 0.4873 / 4.2699 | 1073.4 |
| Wide | 1332.5 | 4000 | 0 | 51.171 [31.402, 83.385] | 0.5154 / 3.6870 | 0.5041 / 2.0781 | 4298.1 |

At the 1,000-count exposure (each entry is estimate [95%]):

| Head / line / rate | RMS direct | RMS Anger | Bias direct | Bias Anger |
|---|---|---|---|---|
| Baseline / 661.7 / 0 | 0.187 [0.160, 0.211] | 0.865 [0.737, 0.977] | 0.160 [0.157, 0.163] | 0.404 [0.340, 0.459] |
| Baseline / 1332.5 / 0 | 1.735 [1.661, 1.806] | 2.148 [2.063, 2.229] | 1.409 [1.339, 1.476] | 1.832 [1.744, 1.917] |
| Sharp / 661.7 / 0 | 0.074 [0.072, 0.076] | 0.157 [0.000, 0.238] | 0.061 [0.060, 0.061] | 0.080 [0.069, 0.089] |
| Sharp / 661.7 / 100000 | 0.074 [0.072, 0.076] | 0.144 [0.082, 0.186] | 0.061 [0.060, 0.061] | 0.081 [0.073, 0.087] |
| Sharp / 1332.5 / 0 | 0.149 [0.046, 0.206] | 1.424 [1.390, 1.456] | 0.079 [0.074, 0.083] | 1.056 [0.965, 1.139] |
| Wide / 1332.5 / 0 | 1.575 [1.336, 1.781] | 6.307 [6.000, 6.600] | 0.487 [0.398, 0.563] | 4.270 [3.855, 4.648] |

## Crystal diagnostics beside the imaging metric

Independent isolated held-out floods, 3,000 scored histories per crystal/line, exactly the v3 transport/response/ADC keys and global-code-sum window. G is per-axis (RMS_Anger^2-RMS_direct^2)/allocation^2 against the first-interaction crystal on the same Anger-accepted events; allocation=pixelPitch/(D*sqrt(12)) radians, the per-axis uniform-crystal quantisation allocation. W/M is the per-seed weakest/median sensitivity (assigned-to-k / full-energy-first-in-k), not a correction factor or a pooled v3 qualification verdict. Multi-crystal share is among all Anger-accepted window events; single mis-ID is wrong/single-assigned. These numbers repeat the existing recipe semantics, not the imaging energy-window variant.

| Head / line | G X [95%] | G Y [95%] | Single mis-ID % [95%] | W/M [95%] | Multi % [95%] |
|---|---|---|---|---|---|
| Baseline / 661.7 | -1.478 [-1.534, -1.422] | -1.609 [-1.652, -1.567] | 0.022 [-0.004, 0.048] | 0.657 [0.598, 0.716] | 50.799 [50.795, 50.803] |
| Baseline / 1332.5 | 3.044 [2.970, 3.118] | 3.028 [2.983, 3.073] | 0.000 [0.000, 0.000] | 0.667 [0.606, 0.728] | 61.761 [61.494, 62.029] |
| Sharp / 661.7 | -5.613 [-5.651, -5.576] | -5.730 [-5.760, -5.701] | 0.642 [0.633, 0.652] | 0.623 [0.613, 0.633] | 58.990 [58.988, 58.992] |
| Sharp / 1332.5 | 18.141 [18.063, 18.219] | 18.274 [18.249, 18.299] | 0.000 [-0.000, 0.001] | 0.567 [0.554, 0.579] | 71.927 [71.884, 71.970] |
| Wide / 1332.5 | 21.158 [21.138, 21.178] | 21.440 [21.413, 21.467] | 0.002 [0.001, 0.004] | 0.545 [0.533, 0.557] | 74.284 [74.250, 74.319] |
| Sharp / 661.7 / 1e5 cps | -2.728 [-3.734, -1.722] | -0.956 [-3.376, 1.464] | 3.137 [2.619, 3.655] | 0.623 [0.613, 0.633] | 59.937 [59.377, 60.497] |

Rate diagnostics use v3's first 10,000 held-out histories and its response/noise/arrival keys, dominant-hit truth and one acceptance per original history. Rate-row W/M comes from the isolated rows, as in v3; the rate sample cannot resolve per-crystal sensitivity and its scratch diagnostic is null when a denominator is empty. Single-crystal status here refers to the dominant original history, not proof that an output pulse has no piled contributors.

The Sharp seed-41000001 diagnostics reproduce turn 5: G=-5.647/-5.750 and +18.074/+18.258 at 662/1332 keV; W/M=0.623679 and 0.559598. This is a useful independent check that the preserved engine/recipe behaviour was measured.

## Calibration failures

| Head | keV | Successful / expected calibrations |
|---|---:|---:|
| Baseline | 661.7 | 4 / 4 |
| Baseline | 1332.5 | 4 / 4 |
| High | 661.7 | 0 / 4 |
| High | 1332.5 | 3 / 4 |
| Sharp | 661.7 | 4 / 4 |
| Sharp | 1332.5 | 4 / 4 |
| Wide | 661.7 | 2 / 4 |
| Wide | 1332.5 | 4 / 4 |

A population F is undefined when any calibration fails; this does not mean every calibration failed. Repeated Baseline/Sharp calibrations in the count-law correction are identical realizations and do not enlarge the calibration population.

| Head | keV | Seed | Failure |
|---|---|---|---|
| High | 661.7 | 41000001 | measured lattice spans 44 × 44 with 1439 maxima, expected 44 × 44; unsupported slots (0, 0), (4, 0), (7, 0) |
| High | 661.7 | 41104730 | measured lattice spans 44 × 44 with 1441 maxima, expected 44 × 44; unsupported slots (0, 0), (2, 0), (3, 0) |
| High | 661.7 | 41209459 | measured lattice spans 44 × 44 with 1405 maxima, expected 44 × 44; unsupported slots (0, 0), (6, 0), (8, 0) |
| High | 661.7 | 41314188 | measured lattice spans 44 × 44 with 1455 maxima, expected 44 × 44; unsupported slots (0, 0), (2, 0), (6, 0) |
| High | 1332.5 | 41209459 | measured lattice spans 44 × 44 with 1935 maxima, expected 44 × 44; unsupported slots (32, 27) |
| Wide | 661.7 | 41209459 | measured lattice spans 34 × 34 with 1155 maxima, expected 34 × 34; unsupported slots (33, 16) |
| Wide | 661.7 | 41314188 | measured lattice spans 34 × 34 with 1154 maxima, expected 34 × 34; unsupported slots (33, 13), (31, 25) |

Successful optional-head individual runs at 1,000 direct-reference counts (fixed-proposal supplementary observations, no outer-seed interval; not a survivor-population qualification):

| Head / line | Seed | F | RMS direct / Anger degrees | Bias direct / Anger degrees |
|---|---:|---:|---|---|
| High / 1332.5 | 41000001 | 134.452 | 0.050 / 0.582 | 0.037 / 0.233 |
| High / 1332.5 | 41104730 | 116.679 | 0.050 / 0.542 | 0.039 / 0.264 |
| High / 1332.5 | 41314188 | 152.920 | 0.051 / 0.631 | 0.038 / 0.295 |
| Wide / 661.7 | 41000001 | 1.903 | 0.503 / 0.694 | 0.482 / 0.382 |
| Wide / 1332.5 | 41000001 | 20.959 | 1.362 / 6.236 | 0.423 / 4.027 |
| Wide / 661.7 | 41104730 | 3.858 | 0.502 / 0.987 | 0.485 / 0.365 |
| Wide / 1332.5 | 41104730 | 15.533 | 1.629 / 6.422 | 0.464 / 4.337 |
| Wide / 1332.5 | 41209459 | 16.542 | 1.596 / 6.491 | 0.537 / 4.578 |
| Wide / 1332.5 | 41314188 | 12.884 | 1.691 / 6.071 | 0.517 / 4.116 |

## Sanity checks and interpretation

Identity: each trial actually decodes the same corrected direct image twice and asserts exact equality. Replacing Anger by that identical direct image gives F=1.000 exactly, zero paired bias change and identical acceptance at every exposure. This checks the statistic and decoder; it does not claim physical Anger is identity-like.

Count scaling:

| Head / line | F250 | F1000 | F4000 | RMS direct 1000/4000 | RMS Anger 1000/4000 |
|---|---:|---:|---:|---:|---:|
| Baseline / 661.7 | 2.968 | 21.361 | 4.284 | 1.146 | 2.560 |
| Baseline / 1332.5 | 1.228 | 1.533 | 1.830 | 1.124 | 1.029 |
| Sharp / 661.7 | 32.668 | 4.534 | 2.754 | 1.240 | 1.591 |
| Sharp / 1332.5 | 3.544 | 91.139 | 119.640 | 1.561 | 1.362 |
| Wide / 1332.5 | 2.314 | 16.043 | 51.171 | 3.055 | 1.711 |

Pure counting-limited localisation would give an RMS ratio of 2 when exposure quadruples, and constant F. That premise is not generally satisfied. A low-count wrong-peak tail can disappear abruptly, while finite-grid, imperfect forward-model, flood-map and readout-migration biases do not vanish with longer observation. The actual curves, not the 1/sqrt(counts) assumption, must decide the time interpretation. The 250-count threshold in earlier single-head evidence cannot be transferred to every head/energy/decoder.

Remote error diagnostic: an error larger than one mask element (cell/D radians), at the 1,000-count exposure. Mean probability in percent, over all trials/positions/seeds. This secondary diagnostic was added after the pilot; the same one-element definition is used for every head. It is not a qualification rule and was not adjusted per preset.

| Head / line / rate | Direct % | Anger % |
|---|---:|---:|
| Baseline / 661.7 / 0 | 0.09 | 8.77 |
| Baseline / 1332.5 / 0 | 26.78 | 48.13 |
| Sharp / 661.7 / 0 | 0.00 | 0.09 |
| Sharp / 661.7 / 100000 | 0.00 | 0.09 |
| Sharp / 1332.5 / 0 | 0.17 | 22.53 |
| Wide / 1332.5 / 0 | 2.17 | 36.07 |

Worst of the nine positions at the same exposure, pooled over seeds (degrees; these are descriptive maxima, not simultaneous confidence limits):

| Head / line / rate | Worst direct RMS | Worst Anger RMS |
|---|---:|---:|
| Baseline / 661.7 / 0 | 0.265 | 1.580 |
| Baseline / 1332.5 / 0 | 3.810 | 3.859 |
| Sharp / 661.7 / 0 | 0.103 | 0.312 |
| Sharp / 661.7 / 100000 | 0.103 | 0.227 |
| Sharp / 1332.5 / 0 | 0.269 | 3.202 |
| Wide / 1332.5 / 0 | 3.636 | 11.290 |

Pile-up direction check: NOT ESTABLISHED. Sharp 662 keV at 1e5 cps uses the same proposal draws as isolated processing. Anger accepted throughput falls from 1,000.8 to 916.0 events (8.5%), and single-crystal mis-ID increases to 3.14%. However, source RMS falls from 0.1571 to 0.1439 degrees, and F from 4.534 to 3.802. The point-estimate direction check therefore fails; it must not be reported as a pass. The paired rate-minus-isolated source MSE difference is -0.00399 degrees squared, with exploratory four-seed Student interval [-0.04433, 0.03636]. No direction is resolved. A retained 4.898-degree isolated error at seed 41104730, position (-25.594,+25.594), versus a correct rate estimate on that trial dominates the ordering; other rate trials acquire remote errors (3.094 and 2.885 degrees). Neither favourable nor unfavourable outlier is removed. This is why throughput and a sparse wrong-peak MSE cannot establish a monotone time factor from 64 trials. Rate-dependent window selection can also change the event mixture; monotonicity of this estimator is not a physical theorem. Uniform isolated calibration does not remove rate-dependent mixing. This is a scored-input-rate, ideal-direct comparison, not an assertion that a real direct system has unlimited rate.

Poisson count-law control: 20,000 independent exact-sampler draws at each declared mean. Mean and variance respectively were 249.900/247.143, 999.364/1,013.890 and 3,999.705/4,027.168 for means 250, 1,000 and 4,000. Both moments satisfy predeclared five-standard-error bounds using exact Poisson moment formulas. This checks the sampler, not the finite source pool or rare source-error convergence. Original fixed-proposal results remain in scratch; the corrected ensemble is used uniformly for all required Baseline/Sharp cells, never selected by outcome.

## What the yardsticks reward and penalise

| Yardstick | Rewards / penalises | Qualification use |
|---|---|---|
| Disagreement with largest deposit | Agreement with one direct assignment convention; penalises legitimate light centroids | Diagnostic only; not truth error |
| Single-crystal mis-ID | Resolving isolated crystal spots and good LUT labelling | Useful detector diagnostic; misses most multi-crystal window events |
| Paired first-interaction G | Average squared proximity to first interaction on Anger-selected events; sensitive to Compton tails | Diagnostic; excludes losses and does not measure source bearing information |
| Weakest/median assigned sensitivity | Uniform uncorrected assigned occupancy; penalises edge centroid flow and continuum/window effects | Report with correction amplification/noise; raw minimum is not an imaging gate |
| Equal accepted-count source RMS | Preserving localisation information per accepted event | Useful companion; can conceal time/efficiency cost |
| Equal-fluence source F | Accuracy of the specified source estimator, including throughput, migration and wrong peaks | A finite-exposure MSE penalty. A time factor only after counting scaling and small bias are demonstrated |
| Bias plus absolute source RMS/tail | Accurate bearing and avoiding confident remote peaks, even if both readouts perform poorly | Necessary system condition beside any relative gate |
| Time to a stated absolute accuracy | Finding the source accurately and quickly, with failures/floors retained | Recommended operational primary yardstick |

Physics: for the regular counting-limited regime, MSE_j(T)=b_j^2+v_j/T at fixed scene flux. If b is negligible, F=v_A/v_D equals T_A/T_D for equal target MSE. With bias, time to RMS epsilon is T_j(epsilon)=v_j/(epsilon^2-b_j^2), when epsilon>b_j, and unattainable otherwise. Therefore F measured at one common time is generally not T_A/T_D. Remote-peak switching also violates this simple asymptotic form. A uniform multiplicative correction changes row sensitivity, not the migration kernel or modulation transfer; its larger weights amplify noise. These are derivations/interpretations of the declared estimator, not borrowed instrument acceptance limits.

More explicitly, if R maps crystal interaction intensity to assigned pixels, normalising a uniform flood produces H=diag(1/(R*1))*R. It ensures H*1=1, but does not ensure H=I or preserve a coded shadow R*m. This explains why a low uncorrected W/M need not be a qualification failure, yet successful uniformity correction cannot establish source accuracy. The engine's direct map also includes gamma transport/migration; neither reference is a first-interaction oracle.

## Recommendation and author-selectable threshold options

Recommended small set: (1) every blind calibration and every correction bin succeeds; (2) an absolute source accuracy target is achieved, including wrong peaks, across the declared source positions at the declared rate/line; (3) the upper paired uncertainty bound of the extra exposure needed for that accuracy is at most the author's chosen time factor. Measure the curve with independent histories at additional exposures where it crosses the target. If it does not cross, retain an unattained/right-censored outcome rather than extrapolating or reporting F as measurement time. Also require an absolute bias/tail condition, or use a sufficiently strict RMS curve which includes those errors. Qualify the decoder, calibration policy, geometry and readout together; MLEM or a migration-aware forward response is a new declared estimator with its own development/confirmation data.

If the author prefers F as a simpler initial gate, call it an equal-fluence MSE penalty at specified exposures (e.g. 1,000 and 4,000 direct-reference counts), require the absolute target separately, and restrict any time wording to cells whose RMS-versus-exposure scaling actually holds. Do not pin one F on a bias-dominated line or infer a hardware gate from these four development seeds.

| Time-factor option K | Meaning after a time-to-target measurement, or verified counting scaling |
|---|---|
| 1.10 | At most 10% longer; 4.9% greater counting-limited RMS at equal time |
| 1.25 | At most 25% longer; 11.8% greater counting-limited RMS at equal time |
| 1.50 | At most 50% longer; 22.5% greater counting-limited RMS at equal time |
| 2.00 | At most twice as long; 41.4% greater counting-limited RMS at equal time |

Possible absolute targets, chosen from physics rather than the best measured row: radial RMS <= 0.25 mask element (precise bearing) or <= 0.5 element (coarser finding). For Baseline these are about 0.239/0.477 degrees; Sharp 0.125/0.251 degrees. A separate mean-bias allocation could be 0.1 or 0.25 element, and a wrong-bearing probability could be <=1% or <=5% beyond one element. The author must choose the combination and intended field/time range before confirmation. These are engineering allocations, not standards. Use simultaneous field-position upper bounds in confirmation, not only the nine-position average printed here. A direct reference that misses the absolute target makes that operating point an unqualified imaging combination, not an automatic physical-readout defect.

## Corrections to earlier premises

The plan's current v3 G and sensitivity gates remain preserved, but they cannot be reinterpreted as an imaging/time gate. Its sensitivity numerator is not just full-energy events; escape cancellation is partial, especially at 1332 keV. Its first-interaction statistic uses only Anger-accepted histories and therefore cannot penalise all lost fluence. The older equal-count localisation result in EV-36 is one near-field position and an older lab head; it does not validate these Studio positions, line energies or decoder curves. The main-tree text saying the physical readout is study-only is stale after TODO-40, as the earlier review already identified. No existing result or calibration failure was overwritten to reconcile these scopes.

The additional premise that every source above 250 counts is in a 1/sqrt(counts) regime is false for the measured combinations. The imaging-level exploration is still useful: it exposes the information/bias cost actually relevant to a user, rather than asserting that every calibrated crystal map must localise well.

With an imaging-level gate, PD-4's detector-module-only qualification inheritance is insufficient. A changed mask rank/cell size, D, focus, decoder or window can change source accuracy without changing a uniform readout calibration. The module fingerprint can reuse preparation/calibration, but performance-matrix inheritance must also match the qualified imaging geometry/estimator or an explicitly tested envelope. This is a consequence of the proposed new yardstick; no inheritance policy was changed here.

## Published practice consulted

Energy-specific uniformity correction, separate spatial linearity/resolution tests and high-rate testing are documented in [IAEA, Quality Assurance for SPECT Systems, sections 2.1.2.5, 2.3.5-2.3.6 and 4.2](https://www-pub.iaea.org/MTCD/Publications/PDF/Pub1394_web.pdf). This supports using independently measured corrections and testing corrected imaging; its clinical SPECT tolerances are not transplanted to coded-aperture bearing accuracy. [Rogers et al., Field-flood requirements for emission computed tomography with an Anger camera](https://pubmed.ncbi.nlm.nih.gov/6977019/) experimentally addresses statistical accuracy, energy-window dependence and limits of flood correction. Neither source establishes a universal F threshold. No former-employer instrument information is used.

## Runtime, reproducibility and limits

Completed 16 original seed/head runs, 8 corrected Poisson seed/head runs and 4 auxiliary rate-diagnostic runs; summed probe wall time 67.22 minutes (serial, approximately one core), excluding short compilations, count-sampler control and aggregation. Sharp pilot: 182.9 seconds, full optical/calibration budgets, both lines and all nine positions/count levels.

Scratch artifacts: `%TEMP%/gcam-todo41/explore/` contains `Program.cs`, `RateMetrics.cs`, `Explore.csproj`, `NuGet.Config`, `run.ps1`, `optional.ps1`, `poisson.ps1`, `aggregate.py`, `make_report.py`, source/binary closures, per-head/seed JSON, `rate/` diagnostics, `poisson/` corrected results, `Poisson-moments.json` and `summary.json`. `closure.json` hashes all 188 engine source files and the original Baseline/Sharp probe source/binary; `closure-extended.json` records the separate rate-diagnostics extension. Original hashes remain as records; later builds replaced only the scratch binary. `closure-final.json` records the current executable/scripts and verifies every original engine-source hash unchanged. No pinned recipe was modified. All three scratch compilations succeeded with zero warnings/errors; decoder oracle and identity checks passed for every completed line. Product tests were not rerun because product code was unchanged; the planner's preserved 912-pass verification is inherited, not a new test claim.

Not measured: independent-history confirmation; convergence of the imaging metric with optical/DOI/calibration/flood budgets; single-662-calibration cross-energy operation; MLEM; rates other than the one Sharp cell; field edges/distances/multiple sources/background; time-to-target crossings outside the three exposures; physical sensor saturation. Bootstrap empirical pools and four outer clusters are adequate for exploration, not for pinning a rule. No numerical bound on v3 G can be transferred to F without measuring the new observable. Future numerical studies must refine corrected source-MSE/bias or target crossing times, preserving calibration failures.

No longer measurement was necessary for the requested development exploration; no unfinished background command remains. The scratch `run.ps1` is resumable by complete head/seed JSON and rejects mismatched pool/replicate identities. The author/planner should prepare a new declared output root and source/binary closure before any larger or confirmation study; this development runner rejects every non-development seed.

## Complete write-command inventory

All complete command text and patch bodies are retained in the conversation tool log. Read-only inspections, git status and public web reads are excluded below.

1. `New-Item -ItemType Directory -Force (Join-Path $env:TEMP 'gcam-todo41\explore') | Out-Null`.
2. `apply_patch` creates scratch `Explore.csproj`, `NuGet.Config`, `Program.cs`; a second patch creates `run.ps1` and `aggregate.py`; subsequent patches add remote-error analysis, `make_report.py`, `RateMetrics.cs`, `optional.ps1`, the separate rate-only branch, rate-summary/null-denominator handling, `poisson.ps1`, exact Poisson sampling/count-check mode and diagnostic reuse, and report wording/tables. No product source or pinned artifact is edited.
3. Three invocations of `dotnet build (Join-Path $env:TEMP 'gcam-todo41\explore\Explore.csproj') -c Release --configfile (Join-Path $env:TEMP 'gcam-todo41\explore\NuGet.Config')`: scratch bin/obj/restore files only; repository engine sources are linked read-only. The second compiled the additive rate-only mode after the first background family finished; the third compiled the uniform count-law correction and sampler control after the optional family finished.
4. `dotnet (Join-Path $env:TEMP 'gcam-todo41\explore\bin\Release\net9.0\Explore.dll') 41000001 Sharp (Join-Path $env:TEMP 'gcam-todo41\explore\Sharp-41000001.json') 120000 64`: completed full-budget timing pilot, writes that JSON.
5. `& (Join-Path $env:TEMP 'gcam-todo41\explore\run.ps1')`: Baseline/Sharp four-seed serial command, keeps the completed pilot and writes the other seven JSON files.
6. `& (Join-Path $env:TEMP 'gcam-todo41\explore\optional.ps1')`: one serial background command. Creates scratch `rate/`, invokes the same scratch DLL four times with arguments `<development seed> Sharp <rate/Sharp-seed.json> 120000 64 rate-only`, then calls `run.ps1 -Heads @('Wide','High')`. Writes rate diagnostics and optional head results, retaining every calibration failure.
7. `dotnet (Join-Path $env:TEMP 'gcam-todo41\explore\bin\Release\net9.0\Explore.dll') count-check (Join-Path $env:TEMP 'gcam-todo41\explore\Poisson-moments.json')`: writes the exact-sampler moment control, all checks passed.
8. `& (Join-Path $env:TEMP 'gcam-todo41\explore\poisson.ps1')`: one serial resumable background command; creates scratch `poisson/` and invokes the scratch DLL eight times as `<development seed> <Baseline|Sharp> <poisson/Head-seed.json> 120000 64 poisson <original matching JSON>`, keeping original files and reusing their isolated diagnostic objects.
9. Repeated `python -B (Join-Path $env:TEMP 'gcam-todo41\explore\aggregate.py')`: writes/replaces scratch `summary.json`, including early incomplete summaries which were explicitly provisional.
10. The logged Python closure commands hash allowed repository engine sources and scratch probe source/binary, writing `closure.json`, `closure-extended.json` and `closure-final.json` under scratch. Other Python inspection commands write no files (`-B` prevents bytecode).
11. `python -B (Join-Path $env:TEMP 'gcam-todo41\explore\make_report.py')`: writes only this newly authorized `docs/PLAN.Studio.ReadoutPresets.Explore.md` (UTF-8, LF). Subsequent documentation regeneration is limited to this file.

No installation, deletion, process termination, git mutation, network upload, GUI or forbidden-path access. No approval requests.

## Discrepancy check

2026-10-08. Conclusion (ii): the large penalties are reproduced behaviour of this simulated readout/decoder combination, not an index, orientation, history-pairing or cached-decoder error. Multi-crystal charge-centroid reassignment changes the coded shadow, and remote competing correlation peaks dominate the finite-exposure MSE and mean bias. Flood correction equalises uniform occupancy but cannot invert that migration. This is a finding about the declared model, not hardware validation. No yardstick decision is made in this turn.

The earlier tables are preserved byte-for-byte. Every original 1,000-count trial of Baseline/662 and Sharp/1332 was reproduced exactly (including counts and both signed errors). Those two original rows are conditional empirical-pool estimates; the independent fresh-history rows below supersede them **for subsequent population-performance quotations at that exposure**. This is a measurement refinement, not a repaired index/decoder implementation or evidence that the large penalty disappears. Other earlier rows remain explicitly conditional approximations, not confirmation evidence. The assignment/window controls are diagnostic interventions, not operational replacements or evidence for rejecting selected histories in operation.

### Theme-68 reproduction and direct study/probe comparison

Use the actual `ReadoutStudy.Run`, restricted to the selected p1.0/g0 DPC-r0.01 Sum50 variant, 662 keV, with the full 100,000-photons/bin optics, 2,000-calibration-histories/crystal, 3,000-validation-histories/crystal and 200,000-localisation-proposal budgets. The irrelevant rate/other-line axes are omitted. Restricting the request renumbers the variant stream to zero; it therefore reproduces the physical contract on the four development seeds, not the original selection/confirmation realizations. The reference remains the study's true-total-deposit window, and equal accepted counts use each path's first min(N_D,N_A) accepted events, without sensitivity correction.

An independent scratch path rebuilds the same optical table/calibration and replays transport, response, noise, energy window and image construction with the study's exact stream keys. Its accepted counts and decoded radial errors must match `ReadoutStudy` to 1e-9 mm on every seed; they do. The exploration's cached coefficients also match ordinary engine decoding here.

Important statistic distinction: theme 68 aggregates the **mean radial error** of one acquisition at a centred source per seed, not an RMS over positions/repeated acquisitions. The lab source is at z=160 mm (D=60, S=100), its decoder is cyclic with the auto grid (step 0.3889 mm), and there is no sensitivity correction. The exploration uses z=1,000 mm, nine positions, a finite decoder, step 4.1667 mm on Baseline, and nominal 1,000 rather than approximately 13,640 accepted counts. The comparison must keep these contracts separate.

Four-seed mean radial error in mm [exploratory two-sided 95% Student interval, df=3]:

| Contract | Direct error | Anger error | Mean equal count |
|---|---|---|---:|
| Lab 662, centre | 0.3620 [0.3456, 0.3784] | 0.3938 [0.3685, 0.4192] | 13640 |
| Studio Baseline 662, centre | 1.1785 [1.1785, 1.1785] | 1.1785 [1.1785, 1.1785] | 12130 |
| Studio Baseline 662, (-26.25,0) | 0.8865 [0.8481, 0.9248] | 1.1225 [0.6471, 1.5980] | 10789 |
| Studio Sharp 1332, (-25.594,+25.594) | 0.9619 [0.7855, 1.1383] | 1.0379 [0.7401, 1.3357] | 5064 |

The published 0.406 +/- 0.006 mm Anger and 0.367 +/- 0.001 mm direct values lie inside the new lab intervals. Published +/- values are standard errors from their larger, independent ensemble, not four-seed acceptance tolerances. No forbidden selection/confirmation/repair seed was read as an observation or run. There is no numerical evidence here that EV-36 is wrong. At the centred Studio point both paths estimate (0.8333,0.8333) mm, giving 1.1785 mm error: this is a shared reconstruction-grid/plateau effect, not a physical 1.1785 mm readout shift.

### Mapping and measurement checks

| Check | Direct test | Outcome |
|---|---|---|
| Crystal index | Compare every recorded `DetectedEvent.PixelY*n+PixelX` with independently summed `ReadoutDevice.DirectCrystal(sites,n)` | Zero mismatches in both heads |
| LUT label and raw orientation | Noiseless full-energy deposit in every crystal at entrance, midpoint and exit; compare raw LUT lookup with y*n+x | Zero mismatches; all crystals at all three depths agree |
| Transpose/mirrors | Check raw x strictly increases along each physical row and raw y along each column at all three depths, and verify every recovered label | No transpose, x flip or y flip |
| Image/decoder orientation | Construct image by [k%n,k/n]; compare cached and ordinary decoder for all Baseline All/Own trial images, all first group/correction trials, and remote Sharp estimates | Agreement at floating-point roundoff (details below) |
| Single-crystal identity | Same histories, jointly accepted window events, same image weights; compare raw per-pixel counts and decoded position | Sharp exact identity; rare Baseline single-crystal label errors are quantified below |
| Pairing | One `ListModeSource.Advance` supplies direct record and Anger sites before either image is built; reproduce every original trial's counts and signed errors | Exact replay on all four seeds |
| Flood path/window | Replay each actual calibration flood with its original response/ADC keys; D uses true total deposit, A uses per-crystal calibrated energy, both +/-15%; separate mean/bin weights | Original arrays reproduced; no cross-path or wrong-line weights |
| Small denominators | Record all calibration-bin counts and weight extrema, refuse empty bins; no clipping/smoothing/tolerance changes | Every bin occupied; maximum weights of order two, not a numerical blow-up |
| Energy calibration/window | Test noiseless full-energy spots and transported full-energy histories; compare native window to a truth-window counterfactual, holding labels fixed | Correct line and per-crystal keV gain; residual energy-response bias/window migration quantified below |

Baseline/662: 10,066 engine-oracle comparisons, maximum coordinate discrepancy 1.35e-13 mm; 1,728 noiseless crystal/depth spot checks, zero label failures. Joint-window single-crystal mis-ID 0.01841%. Raw matched single images identical in 2101/2304 trials; maximum per-pixel L1 difference 8 counts, maximum decoded difference 0.152439 mm. Multi-crystal joint-window assignment disagreement 70.48%.

Calibration assigned-count minima D/A: 457/326. Correction ranges D 0.815-1.383, A 0.462-1.940. Mean calibrated energy of true-full-energy source histories, averaged across positions: 661.8538 [661.7329, 661.9747] keV.

Sharp/1332: 8,125 engine-oracle comparisons, maximum coordinate discrepancy 4.26e-14 mm; 10,800 noiseless crystal/depth spot checks, zero label failures. Joint-window single-crystal mis-ID 0.00000%. Raw matched single images identical in 2304/2304 trials; maximum per-pixel L1 difference 0 counts, maximum decoded difference 0.000000 mm. Multi-crystal joint-window assignment disagreement 71.77%.

Calibration assigned-count minima D/A: 214/191. Correction ranges D 0.779-1.921, A 0.552-2.379. Mean calibrated energy of true-full-energy source histories, averaged across positions: 1357.1889 [1356.6839, 1357.6938] keV.


A single-crystal identity test must use the same accepted histories and weights. Native windows can accept different histories, particularly at 1332 keV; and correction maps derived from all histories differ because of multi-crystal migration. Applying those different maps to a single-only subset is not an identity experiment. The raw and common-direct-weight tests below explicitly separate this effect. The native full-energy mean at Sharp/1332 is approximately 1.85% above the line; this is a calibration/DOI/event-mixture response limitation, not an accidental use of 662 keV gain. The truth-window intervention measures how much that acceptance difference matters.

### Per-position bias pattern

All angles in degrees, vector = (mean x bearing error, mean y bearing error), pooled equally across outer seeds. These vectors are mean errors of the estimator including remote peaks, not a fitted light-plane distortion. Position units are mm.

| Position x,y | Baseline direct vector | Baseline Anger vector | Sharp direct vector | Sharp Anger vector |
|---|---|---|---|---|
| Baseline (0.00,0.00); Sharp (0.00,0.00) | (+0.0466,+0.0467) | (-0.0097,-0.1916) | (+0.0300,-0.0175) | (+0.0410,-0.1493) |
| Baseline (26.25,0.00); Sharp (25.59,0.00) | (+0.1491,+0.0468) | (-0.2262,-0.3995) | (+0.1254,+0.0173) | (+0.0364,-0.2632) |
| Baseline (-26.25,0.00); Sharp (-25.59,0.00) | (+0.0156,+0.0219) | (+0.4017,-0.7647) | (-0.0443,-0.0055) | (+1.2646,-1.0147) |
| Baseline (0.00,26.25); Sharp (0.00,25.59) | (+0.0477,-0.0536) | (+0.0794,-0.0905) | (-0.0004,+0.0135) | (+0.0498,-0.1679) |
| Baseline (0.00,-26.25); Sharp (0.00,-25.59) | (+0.0447,-0.2181) | (+0.0218,-0.2237) | (+0.0175,-0.0612) | (-0.0194,-0.0742) |
| Baseline (26.25,26.25); Sharp (25.59,25.59) | (+0.1550,-0.0635) | (+0.1050,-0.1379) | (+0.0795,-0.0129) | (+0.0212,-0.2229) |
| Baseline (26.25,-26.25); Sharp (25.59,-25.59) | (+0.1396,-0.2217) | (+0.1343,-0.2196) | (+0.0718,-0.0957) | (-0.0241,-0.0512) |
| Baseline (-26.25,26.25); Sharp (-25.59,25.59) | (-0.0298,-0.0507) | (+0.1629,-0.3467) | (-0.0559,+0.0316) | (+1.4471,-2.2323) |
| Baseline (-26.25,-26.25); Sharp (-25.59,-25.59) | (-0.0117,-0.2138) | (+0.0505,-0.2185) | (-0.0605,-0.0574) | (+0.0118,-0.1064) |

The pattern is not a universal flip, constant shift or radial scale. Baseline's Anger vector at (-26.25,0) is much larger than at (0,-26.25); negative-x positions disproportionately select peaks around y=-40 mm. A simple affine error fit has only R-squared 0.29 in x and 0.17 in y (x error intercept +0.080 degrees, slopes -0.0667/+0.0156 versus source x/y bearings; y intercept -0.288, slopes +0.0635/+0.0096). These slopes cannot describe a sign reversal, and do not account for most of the position pattern. Small global assigned-flood centroid shifts also cannot explain the much larger decoded errors.

### Bias/error decomposition

Same 1,000-direct-count exposure and exact paired proposal draws for every row. Selecting single/multi subsets reduces their counts; no subset is replenished or equal-count-normalised. Own correction means each path's original all-event flood; Common means use the direct flood for both paths, solely to isolate the weighting effect. RMS and finite-exposure bias B include all subset wrong peaks. Angles in degrees; the scratch summary contains per-seed squared-statistic intervals. Subset errors cannot be added to predict total error: correlation scores add, but peak selection/interpolation is nonlinear.

| Head | Histories | Correction | Counts D/A | RMS D/A | Bias B D/A |
|---|---|---|---|---|---|
| Baseline/662 | All | None | 1000.4/1000.9 | 0.1846/0.6954 | 0.1718/0.3322 |
| Baseline/662 | All | Own | 1000.4/1000.9 | 0.1871/0.8649 | 0.1602/0.4039 |
| Baseline/662 | All | Common direct | 1000.4/1000.9 | 0.1871/0.7574 | 0.1602/0.3580 |
| Baseline/662 | Single | None | 488.2/488.4 | 0.2067/0.2065 | 0.1565/0.1565 |
| Baseline/662 | Single | Own | 488.2/488.4 | 0.2576/0.4365 | 0.1447/0.1757 |
| Baseline/662 | Single | Common direct | 488.2/488.4 | 0.2576/0.2574 | 0.1447/0.1448 |
| Baseline/662 | Multi | None | 512.2/512.6 | 0.7114/2.3284 | 0.2663/1.5602 |
| Baseline/662 | Multi | Own | 512.2/512.6 | 0.8986/2.5755 | 0.3638/2.0442 |
| Baseline/662 | Multi | Common direct | 512.2/512.6 | 0.8986/2.4400 | 0.3638/1.7801 |
| Sharp/1332 | All | None | 999.2/1099.7 | 0.1831/1.5756 | 0.0841/1.1478 |
| Sharp/1332 | All | Own | 999.2/1099.7 | 0.1491/1.4236 | 0.0791/1.0558 |
| Sharp/1332 | All | Common direct | 999.2/1099.7 | 0.1491/1.5151 | 0.0791/1.1190 |
| Sharp/1332 | Single | None | 244.1/310.4 | 1.2193/0.9349 | 0.6127/0.4286 |
| Sharp/1332 | Single | Own | 244.1/310.4 | 1.2011/1.0773 | 0.6869/0.6713 |
| Sharp/1332 | Single | Common direct | 244.1/310.4 | 1.2011/0.9935 | 0.6869/0.5332 |
| Sharp/1332 | Multi | None | 755.1/789.4 | 0.2961/2.4661 | 0.0951/1.8952 |
| Sharp/1332 | Multi | Own | 755.1/789.4 | 0.2776/2.3395 | 0.0863/1.8178 |
| Sharp/1332 | Multi | Common direct | 755.1/789.4 | 0.2776/2.4021 | 0.0863/1.8741 |

Controlled interventions on the same histories, with a separately recomputed uniform flood for each intervention (no validation fit, no new RNG draws): Native = normal Anger assignment/window; Direct-on-A = ideal largest-deposit assignment but retain Anger acceptance; A-on-truth = retain Anger label but replace its energy window by the direct total-deposit window; Direct-on-truth = direct label/window retaining valid Anger trigger/label. These are truth-aided diagnostics unavailable to a Studio user, not proposed corrections or estimators. Each row uses its own intervention flood when corrected.

| Head | Intervention | Correction | RMS [exploratory 95%] | Bias B |
|---|---|---|---|---:|
| Baseline/662 | Native | None | 0.6954 [0.2839, 0.9416] | 0.3322 |
| Baseline/662 | Native | Own intervention flood | 0.8649 [0.7366, 0.9766] | 0.4039 |
| Baseline/662 | Direct-on-A | None | 0.1847 [0.1762, 0.1928] | 0.1718 |
| Baseline/662 | Direct-on-A | Own intervention flood | 0.1872 [0.1598, 0.2111] | 0.1602 |
| Baseline/662 | A-on-truth | None | 0.7079 [0.3480, 0.9387] | 0.3354 |
| Baseline/662 | A-on-truth | Own intervention flood | 0.8770 [0.7648, 0.9763] | 0.4065 |
| Baseline/662 | Direct-on-truth | None | 0.1846 [0.1767, 0.1922] | 0.1718 |
| Baseline/662 | Direct-on-truth | Own intervention flood | 0.1871 [0.1598, 0.2110] | 0.1602 |
| Sharp/1332 | Native | None | 1.5756 [1.4263, 1.7120] | 1.1478 |
| Sharp/1332 | Native | Own intervention flood | 1.4236 [1.3901, 1.4562] | 1.0558 |
| Sharp/1332 | Direct-on-A | None | 0.1164 [0.1040, 0.1276] | 0.0830 |
| Sharp/1332 | Direct-on-A | Own intervention flood | 0.1176 [0.0971, 0.1350] | 0.0793 |
| Sharp/1332 | A-on-truth | None | 1.8173 [1.7433, 1.8884] | 1.3368 |
| Sharp/1332 | A-on-truth | Own intervention flood | 1.5616 [1.5087, 1.6128] | 1.1350 |
| Sharp/1332 | Direct-on-truth | None | 0.1831 [0.0000, 0.2661] | 0.0841 |
| Sharp/1332 | Direct-on-truth | Own intervention flood | 0.1491 [0.0460, 0.2058] | 0.0791 |

The interventions distinguish placement migration from energy-window admission. Their measured changes are conditional effects; they are not additive bias allocations. In particular, removing the crystal-assignment migration while keeping Anger acceptance tests whether the poor source result survives purely as an efficiency/energy-selection effect.

Baseline/662: retaining native Anger acceptance but replacing placement by largest deposit changes corrected RMS 0.8649 -> 0.1872 degrees and B 0.4039 -> 0.1602. Keeping Anger placement but substituting the true-energy window gives RMS 0.8770 and B 0.4065. Thus energy-window admission is not the cause of the large excess error; the placement intervention removes it.

Sharp/1332: retaining native Anger acceptance but replacing placement by largest deposit changes corrected RMS 1.4236 -> 0.1176 degrees and B 1.0558 -> 0.0793. Keeping Anger placement but substituting the true-energy window gives RMS 1.5616 and B 1.1350. Thus energy-window admission is not the cause of the large excess error; the placement intervention removes it.



### Fresh-history check of the empirical-pool approximation

Repeat the native 1,000-count cell with 64 completely new transported acquisitions per position and seed. Each acquisition has an exact Poisson(H) emission-proposal count at the original exposure, using new development-purpose transport/response/noise streams. No source history or realised channel fluctuation is resampled. Optical table, blind calibration, correction flood, positions, decoder and windows are unchanged. The original 120,000-proposal direct-efficiency estimate still sets H, preserving the declared exposure; achieved mean counts are reported instead of forcing equal accepted counts. This checks the concern that the empirical source pool manufactured the large penalty. It is additional development data, not a calibration/confirmation seed ensemble.

| Head | Mean counts D/A | Fresh RMS D/A | Fresh bias B D/A | Fresh F [exploratory 95%] |
|---|---|---|---|---|
| Baseline/662 | 1002.9/1003.3 | 0.1634/0.6529 | 0.1524/0.2917 | 15.9681 [13.1721, 19.3575] |
| Sharp/1332 | 993.2/1092.8 | 0.1034/1.1309 | 0.0732/0.5558 | 119.5138 [102.4103, 139.4737] |

Independent-history point estimates differ from the conditional bootstrap estimates because a finite empirical pool has a noisy mean shadow and repeated realised event marks. With a multimodal decoder, that approximation can materially alter remote-peak probabilities and finite-exposure mean bias, beyond smooth variance inflation. It must not be treated as a converged high-count population response. The large relative source penalty nevertheless persists without any history reuse; the extreme effect is not manufactured by the pool. These fresh four-cluster intervals remain development estimates, not a qualification bound.

Placement/window controls also applied to the fresh acquisitions (own intervention flood; values are RMS/B in degrees):

| Head | Direct placement, Anger acceptance | Anger placement, true-energy window |
|---|---|---|
| Baseline/662 | 0.1633/0.1525 | 0.6567/0.2962 |
| Sharp/1332 | 0.1029/0.0756 | 1.3451/0.7539 |

Fresh matched-single control, Baseline/662: 2105/2304 raw images exactly equal, maximum decoded difference 5.0073 mm. Rare single-crystal label errors can switch a nearly tied subset peak; maximum difference is not a population tolerance. The raw single-only aggregate and common-weight comparisons remain in scratch.

Fresh matched-single control, Sharp/1332: 2304/2304 raw images exactly equal, maximum decoded difference 0.0000 mm. Rare single-crystal label errors can switch a nearly tied subset peak; maximum difference is not a population tolerance. The raw single-only aggregate and common-weight comparisons remain in scratch.



### Assigned floods and competing peaks

Every crystal's assigned counts, uniform calibration D/A counts, correction weights, point-source D/A counts and joint-window migration matrix are retained in the per-seed JSON. TV below is half the L1 distance between separately normalised assigned images; it reports shape difference, not sensitivity loss. Corrected uniform calibration images are flat and identical by construction; corrected point-source images need not be.

Baseline uniform calibration edge share: direct 26.00%, Anger 19.74%; after their own corrections both equal 44/144 = 30.56%. The complete two-dimensional normalised A/D assigned-density ratio, averaged over the four calibration floods, is below (y increasing down the table; x increasing left-to-right).

| y\x | 0 | 1 | 2 | 3 | 4 | 5 | 6 | 7 | 8 | 9 | 10 | 11 |
|---|---|---|---|---|---|---|---|---|---|---|---|---|
| 0 | 0.76 | 0.73 | 0.76 | 0.72 | 0.71 | 0.70 | 0.72 | 0.75 | 0.75 | 0.72 | 0.80 | 0.77 |
| 1 | 0.85 | 1.00 | 1.06 | 0.96 | 1.09 | 1.07 | 1.06 | 0.93 | 1.12 | 1.15 | 0.93 | 0.90 |
| 2 | 0.77 | 0.89 | 1.31 | 0.83 | 0.97 | 1.19 | 1.05 | 1.30 | 0.83 | 1.09 | 0.99 | 0.75 |
| 3 | 0.78 | 0.95 | 1.19 | 1.23 | 1.18 | 1.14 | 1.09 | 1.18 | 0.88 | 1.13 | 0.92 | 0.74 |
| 4 | 0.64 | 1.05 | 1.40 | 1.02 | 1.12 | 1.38 | 0.97 | 1.12 | 1.17 | 0.97 | 1.15 | 0.76 |
| 5 | 0.75 | 1.14 | 1.11 | 1.15 | 0.92 | 1.11 | 1.16 | 1.37 | 0.94 | 1.25 | 0.96 | 0.78 |
| 6 | 0.72 | 1.11 | 1.06 | 1.19 | 1.27 | 0.99 | 1.18 | 1.55 | 0.96 | 1.21 | 1.02 | 0.78 |
| 7 | 0.81 | 0.97 | 1.06 | 1.01 | 1.20 | 1.36 | 1.19 | 1.07 | 0.89 | 1.23 | 0.98 | 0.70 |
| 8 | 0.76 | 0.93 | 1.10 | 0.94 | 0.97 | 1.11 | 1.03 | 1.23 | 1.06 | 1.03 | 0.91 | 0.71 |
| 9 | 0.77 | 1.00 | 1.11 | 1.29 | 0.89 | 1.11 | 1.14 | 0.98 | 1.03 | 0.99 | 1.23 | 0.76 |
| 10 | 0.87 | 1.05 | 1.15 | 0.80 | 1.08 | 1.12 | 0.94 | 1.12 | 1.06 | 1.00 | 1.18 | 0.73 |
| 11 | 0.73 | 0.79 | 0.73 | 0.88 | 0.81 | 0.79 | 0.68 | 0.77 | 0.73 | 0.78 | 0.73 | 0.76 |

Point-source assigned-image comparison on Baseline (mean over seeds):

| Position mm | Raw TV % | Corrected TV % | A-minus-D assigned centroid x/y mm |
|---|---:|---:|---|
| (0.00,0.00) | 11.64 | 7.25 | -0.0079/-0.0122 |
| (26.25,0.00) | 11.51 | 6.90 | +0.0067/+0.0049 |
| (-26.25,0.00) | 11.93 | 7.24 | -0.0101/-0.0012 |
| (0.00,26.25) | 11.32 | 7.16 | -0.0162/+0.0201 |
| (0.00,-26.25) | 11.20 | 6.61 | +0.0154/-0.0148 |
| (26.25,26.25) | 11.80 | 6.10 | -0.0095/+0.0098 |
| (26.25,-26.25) | 10.92 | 6.02 | -0.0184/-0.0030 |
| (-26.25,26.25) | 11.25 | 6.74 | -0.0241/+0.0123 |
| (-26.25,-26.25) | 11.27 | 6.85 | -0.0260/+0.0063 |

Remote-error contribution at the original 1,000-count exposure (one-element radius = cell/D radians, same diagnostic definition as the earlier exploration; all observations remain in qualification statistics):

| Head | Remote share | Share of Anger MSE from remote errors | RMS within one element |
|---|---:|---:|---:|
| Baseline/661.7 | 8.77% | 95.53% | 0.1915 degrees |
| Sharp/1332.5 | 22.53% | 99.06% | 0.1567 degrees |

This diagnostic explains why quoting the overall Anger/direct RMS ratio as a smooth spatial-resolution degradation is misleading. Many near-peak estimates have comparable precision, but lost coded contrast lets competing remote maxima win. The directionally asymmetric peak mixture generates a large mean bias; it is not a global x/y convention error. The mask/slab, pixel sampling, crystal transport and centroid response together define that contrast. Uniform sensitivity correction reduces assigned-density differences but does not restore the direct coded shadow. Baseline's raw all-event Anger RMS is already 0.6954 degrees versus 0.1846 direct; its own correction raises Anger RMS to 0.8649 while direct stays 0.1871. At Sharp/1332 the correction helps (1.5756 to 1.4236), but cannot remove the multi-crystal problem.

Theme 68 did not sample this acquisition/field regime. It used a centred near-field source, roughly an order of magnitude more accepted events, a cyclic finer physical grid, no sensitivity correction, no Studio entrance/backing/gap, and reported mean error over seeds rather than remote-peak RMS across a field. The direct study/probe comparisons and source-assignment interventions make that explanation testable; a generic claim that all calibrated four-output heads localise only 10% worse is not supported by EV-36.

### Conclusion, preserved rows and limits

Conclusion (ii), within the model and declared decoder: reproduced multi-crystal centroid migration and competing-peak selection, compounded by class-dependent flood weighting and energy-window admission. No mapping/decoder fix was required. Baseline/662 and Sharp/1332 1,000-count conditional rows were reproduced exactly, then remeasured with fresh independent acquisitions; use the new rows for population-performance quotations and retain the originals as superseded conditional records. The other exploration rows are unchanged conditional estimates. No calibration failure, remote estimate or seed was discarded. Nothing here validates the absolute realism of the optical/gamma model or licenses a yardstick threshold. Independent confirmation, forward-model/migration-aware reconstruction and convergence remain separate work; no product change is proposed or made in this turn.

The single-only comparison and direct-assignment intervention are diagnoses, not permissible replacements for the all-event operational observable. The off-axis Sharp study test also asks the actual `ReadoutStudy` to decode that geometry; agreement with scratch establishes implementation consistency, not an independent gamma-transport oracle.

### Compute and complete write-command inventory for this turn

Completed all 40 development seed/contract runs, summed probe wall time 35.63 minutes, plus short scratch builds/analysis. All commands finished. No long job remains and none requiring more than the compute allowance was needed.


All command/patch bodies are retained in the conversation log. With `R = Join-Path $env:TEMP 'gcam-todo41\explore\discrepancy'`, the write operations were:

1. `New-Item -ItemType Directory -Force R | Out-Null` (the actual command uses the full Join-Path expression).
2. Logged `python -B -` command creates scratch `Check.csproj` by linking the six engine source projects read-only and extracts the original cached decoder/count sampler into `Helpers.cs`; no product projects are built.
3. `apply_patch` creates/updates `Main.cs`, `run.ps1`, `controls.ps1`, `fresh.ps1`, `analyse.py` and `report.py`, and adds the fresh-control table within this new discrepancy section. Earlier document content is untouched. Two initial compilation errors (C# decimal literal syntax and the `double.Hypot` API spelling) were fixed in scratch before any measurement.
4. Five invocations of `dotnet build (Join-Path R 'Check.csproj') -c Release --configfile (Join-Path $env:TEMP 'gcam-todo41\explore\NuGet.Config')`. The first two failed; the next three succeeded with zero warnings/errors. Writes only scratch bin/obj/restore outputs. Rebuilds occurred only after the preceding serial runner exited.
5. `dotnet (Join-Path R 'bin\Release\net9.0\Check.dll') 41000001 lab (Join-Path R 'lab-41000001.json')`, then the analogous `41000001 baseline ...baseline-41000001.json`: complete timing pilots, no reduced budgets.
6. `& (Join-Path R 'run.ps1')`: one serial background command; runs the four development seeds in modes lab, studio-study, baseline and sharp, retaining the two complete pilots. Every DLL result target is under R.
7. Logged `python -B -` commands preserve main sources as `MainInitial.cs` and `MainControls.cs`, write `closure-initial.json`, `closure-controls.json`, `repository-before.json` and `closure-final.json`; a later such command updates the final script hashes. No file is moved or deleted.
8. `& (Join-Path R 'controls.ps1')`: one serial background command after the first runner finished; four development seeds in baseline-controls, sharp-controls, studio-study-offaxis and sharp-study, each writing a separate JSON. Existing observation files are kept.
9. `& (Join-Path R 'fresh.ps1')`: one serial background command after the controls runner finished; four development seeds each in baseline-fresh and sharp-fresh modes, each writing a separate JSON. No old observations are overwritten.
10. Repeated `python -B (Join-Path R 'analyse.py')`: writes scratch `summary-check.json`; intermediate incomplete summaries were provisional and are not used in this report.
11. `python -B (Join-Path R 'report.py')`: appends only this section to the authorized Explore file, verifies the entire earlier file prefix byte-for-byte against its recorded hash, and writes a scratch report closure. UTF-8, LF; no local user names/paths in the document.

The final source audit verifies the original 188 engine-source hashes unchanged and the pinned v3 recipe/selector/rule hashes unchanged. No git mutation, deletion, install, process stop, GUI or upload. No approval requests.
