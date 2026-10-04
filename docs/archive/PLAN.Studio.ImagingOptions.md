# PLAN.Studio.ImagingOptions — detector realism and per-nuclide imaging (TODO-08)

Scope: what `Gcam.Wpf` adds to imaging that Studio lacks — detector realism defaults, gain non-uniformity, decay
cascades, ambient background, per-nuclide energy-window imaging and Compton stripping — and the steps, in two
phases. Builds on [PLAN.Studio.LiveAcquisition](PLAN.Studio.LiveAcquisition.md) (closes its review points R-2, R-5)
and [PLAN.Studio.Spectrum](PLAN.Studio.Spectrum.md).

Status: **done** 2026-10-02 (phase A `d8b833e`, phase B committed with this status). Open: the 480–620 keV
valley (survey P-11) and the Co-60 localisation bias (TODO-17).

## What `Gcam.Wpf` does that Studio does not

`ConfigFromScene` / `PrepareLive` / `RenderImaging` in `src/Gcam.Wpf/MainWindow.xaml.cs`:

| Wpf behaviour | Studio today |
|---|---|
| **Entrance absorber 0.15 mm** steel-equivalent (source capsule + detector window), MC-verified: it leaves the Ba K X-rays as a modest bump and fills the photopeak's low-energy tail | absent — bare vacuum geometry |
| **Backing 2 mm** behind the crystal (SiPM, PCB, housing) → the ~184 keV backscatter peak | absent |
| **Pixel gain σ** (default 3 %, seeded fixed pattern) | absent (R-2) |
| **Reflector gap 100 µm** between crystals | absent (editing it is TODO-11; the default belongs here) |
| No cascades: `Gcam.Wpf` and `EventStreamStudy` also emit singles; true coincidence summing exists only in `CascadeSummingStudy` (`DecayScheme`) | list-mode emits singles — same as `Gcam.Wpf` |
| **Background** at a background-to-signal ratio (uncoded pedestal + crystal response at the background energy) | rejected with `NotSupportedException` (R-5) |
| **One imaging channel per isotope**: events inside that isotope's primary-line window, decoded separately, composited; found peaks labelled by isotope | one flood of all events |
| **Compton strip**: per pixel, subtract R·(higher channel) from a lower channel; R calibrated from an H-only run of the scene | absent |

**Correction to TODO-07 — and to this correction (measured in phase A).** This plan first said the Ba K peak
being the spectrum's tallest bin holds only for the bare geometry. Measured with the realism defaults it still is:
the Ba K / 662 keV peak-bin ratio falls from 3.98 (bare) to 2.32 (0.15 mm steel, unscattered transmission 0.48 vs
0.474 from Beer–Lambert). The original reading was right: the low-energy peak is far narrower (~7 keV FWHM vs
~56 keV for the photopeak once the 3 % pixel gain spread is included), so its bin is taller although it holds
fewer counts. `Gcam.Wpf`'s "~8 % bump" comment does not describe peak height.

## Corrections (2026-10-01, after the implementer stopped)

- **Cascades were a wrong premise.** R-2 said `EventStreamStudy` adds decay cascades; its `cascadeRng` is the
  in-crystal Compton RNG, and `MixedFieldSource` picks each line independently. Coincidence summing needs a
  per-decay emission path (correlated gammas, same arrival time, biasing that keeps the angular correlation) —
  new engine design, moved to **TODO-14**. With it, summing would come out of the existing pile-up stage (zero time
  separation), not a separate model.
- **Gain cannot be "passed through".** The detector's event sink reports the deposit before the per-pixel
  sensitivity, so gain belongs in the measurement stage (I-2) using the `CrystalUniformity` pattern for the
  configured σ and seed — not a `ListModeSource` parameter.

## Decisions (planner, 2026-10-01; the author may override)

| # | Decision | Why |
|---|---|---|
| I-1 | **Detector realism defaults come from `Gcam.Wpf`** (entrance 0.15 mm, backing 2 mm, gain σ 3 %, reflector gap 0.1 mm), passed by Studio explicitly — a Studio `DetectorSettings`, not new defaults inside `SceneConfigBuilder.Build`, so engine tests and studies that call `Build` keep their results. | They were MC-verified for this instrument and silently lost in the move; engine results must not shift. |
| I-2 | **Events keep the true deposit; the detector response is applied once, in one shared "measurement" stage** (pixel gain × deposit, then the chain's `FrontEndModel` smear), used by both the Spectrum and the imaging windows. Gain σ and its seed are detector inputs: changing them marks the acquisition stale (a real detector's gain cannot be changed on recorded data). | One consistent measured energy per event across workspaces; no double gain. |
| I-3 | **Background uses the engine's existing model** (theme 28: BSR × source rate, `BackgroundStudy` spatial profile, crystal response at the background energy, as `EventStreamStudy` builds it) as a second Poisson process merged into the event stream, each event used once. | No new, unverified background physics; R-5 asked for a transported producer, and this one is the engine's. |
| I-4 | **One energy-window setting N** shared by Spectrum and Imaging (owned by the shell, shown in both panels). | The band the user sees on the spectrum must be the window the image uses. |
| I-5 | **Per-nuclide images are chosen with a selector** (All / each isotope in the scene), not a colour composite; found peaks are marked and labelled with the isotope. | Colour-only coding fails SR-A11Y-04; a selector keeps one colormap and the existing measurement tools. |
| I-6 | **Strip ratios R come from H-only calibration acquisitions** of the scene, run on the worker when an acquisition starts (as a real instrument is calibrated with an H-only source), at the same window N. | Same model as `Gcam.Wpf` and the CLI (themes 16–17, 26); its one-pass scalar limit for 3+ overlapping contaminants is stated, not hidden. |

## Phase A — acquisition physics (R-2, R-5, realism defaults)

1. **`DetectorSettings`** in Studio (entrance, backing, gain σ, gain seed, reflector gap) with the I-1 defaults,
   applied by the service when building the acquisition config (clone + mutate, never a hand-written config).
   Shown read-only in the left panel's geometry section except gain σ / seed, which are editable run inputs.
2. **`ListModeSource`**: emit background events per I-3 (and carry the realism defaults through the config).
   Additive; existing engine results unchanged. No cascade (TODO-14), no gain here (I-2).
3. **Measurement stage** per I-2: measured energy = gain[pixel] × deposit (gain pattern from `CrystalUniformity`
   for σ and seed), then the chain's `FrontEndModel` smear; used by `SpectrumService` (replace its own smear) and
   ready for phase B's windows.
4. **Tests (k·σ, real engine):**
   - Ba K band counts with the 0.15 mm absorber vs without: ratio consistent with the MC transmission of that
     absorber at 32–36 keV (state the expectation from an independent narrow-beam calculation and the tolerance);
     replace the TODO-07 "global maximum is Ba K" assertion with this.
   - Backscatter: a peak region near 184 keV appears with the backing and not without.
   - Gain σ = 3 %: the 662 keV in-window fraction drops vs σ = 0, and the measured photopeak FWHM widens
     consistently with σ added in quadrature (within tolerance).
   - Background: at BSR = 1 the event rate doubles within k·σ and the extra events follow the engine's spatial
     profile and energy response; at BSR = 0 the stream is unchanged bit-for-bit for the same seed.
5. **Workspace switch defect (VV.Studio AN-10, found on the desktop):** selecting a workspace button through UI
   Automation (`SelectionItemPattern.Select`, used by screen readers) checks it without changing the workspace.
   Make activation ViewModel-side (setting a workspace active selects it in the shell) with a ViewModel test.
6. **Docs:** VV.Studio.SRS / SDS / matrix / VV.Studio.Acquisition (present behaviour; history only in the SDS and
   evidence notes), DESIGN.Layout (left-panel detector rows), the Spectrum plan's Ba K note.

## Phase B — per-nuclide imaging and Compton strip

Checked in the code before writing (2026-10-01): `MixedFieldStudy.TopPeaks` / `MatchOneToOne` exist; the engine's
stripping model and its evidence (EV-15: R calibrated from a Co-only run, Cs count error 0–11 %; tests
`ComptonTests.Stripping_RecoversCsCount_EvenCoLocated`, `MixedFieldTests.ComptonStripping_RecoversCoLocatedCsCount`);
`MeasurementStage.Amplitude` gives each event's measured energy; `SpectrumSettings.WindowFwhm` is the window N today.

1. **Shared window N (I-4).** Move N from `SpectrumSettings` to the shell (one value; both the Spectrum and the
   Imaging panels show it). Imaging windows use the same S-5 windows (E ± N·FWHM(E) of each isotope's primary line).
2. **Channels.** Per isotope in the scene: the events whose measured energy (`MeasurementStage`) falls in that
   isotope's primary-line window → a flood → decode. "All" = every event (today's image). Built on the worker with
   each snapshot, incrementally where cheap. Re-filtering after an N change needs no new acquisition (view setting).
**Correction (2026-10-02, after the implementer stopped).** The 1.5 mm tolerance borrowed from the source-drag
scenario had no basis here: the default reconstruction grid step is 2.19 mm and `MixedFieldStudy.TopPeaks` returns
the grid argmax without interpolation, so quantisation alone allows ~1.55 mm (measured Co-60 error 2.31 mm, its y
part 2.28 mm — more than half a step, cause not established). Found peaks are now refined with the engine's
`PeakInterpolation.Estimate` at the pipeline-default sub-cell method (EV-08), and the test asserts association, with
precision measured (step 5).

3. **Selector (I-5).** An Imaging-panel selector All / each isotope; the reconstruction, its colour bar and readout
   show the selected channel. Found peaks: `MixedFieldStudy.TopPeaks` per channel (k = sources of that isotope),
   refined with `PeakInterpolation.Estimate` (pipeline-default sub-cell method), drawn as a
   distinct marker shape labelled with the isotope; the true-source markers stay as they are.
4. **Compton strip (I-6).** A view-setting toggle. At acquisition start (and when N changes) the worker runs an
   H-only list-mode calibration per contaminating isotope H (an isotope with a line above the lower channel's
   window): R = H events in the low window ÷ H events in H's own window, with ≥ 20 000 H events. Per pixel
   `max(0, low − Σ R·high)` before decoding. The status / panel shows each R and says it is the one-pass scalar
   model (exact for a pair; approximate for 3+ overlapping contaminants).
5. **Tests (real engine, k·σ).** Co-located Cs-137 + Co-60 (Co ×2 activity): with strip on, the Cs channel's
   in-window count matches a Cs-only acquisition of the same live time within the stated k·σ (EV-15's 0–11 % is the
   reference scale, not the tolerance — derive the tolerance from the counts); with strip off it is biased high by
   the Co downscatter. Off-axis Cs + Co: each channel's top peak is associated with its own source, error below one
   reconstruction-grid diagonal (step × √2) after sub-cell refinement — an association check, not a precision
   claim; the precision is **measured** (mean bias vector, RMS, max per isotope over ≥ 20 seeds, with and without
   refinement, plus a single-isotope Co-60 control at the same position) and recorded as evidence, not asserted. N change re-filters without a new acquisition; R is recomputed.
6. **Docs:** SR rows for channels, selector, found-peak markers and strip; SDS units; DESIGN.Layout (Imaging panel);
   VV matrix.

**Not here:** editable optics (TODO-09), chain selection (TODO-10), editing reflector gap / SiPM / crosstalk and
depth (TODO-11).
