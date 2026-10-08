# VV.Tests — automated test catalog

Scope: every automated test unit in the repository — the xUnit classes of the engine and GCAM Studio, the Python
unittest classes of the evidence tools, the cocotb RTL configurations and the standalone checkers — with what each
verifies, where its expected value comes from and how its tolerance is justified. Results of a run are in
[VV.Tests.Results](VV.Tests.Results.md); requirement status in [VV.Studio](VV.Studio.md).

**At a glance**
- 161 units: 125 xUnit classes, 3 unittest classes, 29 cocotb configurations, 4 checkers.
- Every entry states purpose, oracle, tolerance (its derivation, or "exact", "structural", "measurement only"), gates
  and requirement / evidence selectors in a fenced JSON block.
- 70 units carry a margin the code states without a derivation — recorded as a finding, not filled in.
- `check-catalog --discover` fails when a discovered class has no entry, an entry names no class, or a selector does
  not resolve; a changed source hash flags the entry for review.

One entry describes each xUnit class, unittest class, cocotb module/configuration or checker. Source links locate the implementation; metadata pins the reviewed source hashes. Method names and trace selectors in metadata are relative to the entry id and expand to exact assembly-qualified selectors. Theory case arguments are discovered separately.

Tolerance statements describe existing assertions; they introduce no acceptance margin. An unexplained margin is an established finding, not an incomplete entry. Operational deadlines and product targets are distinguished from numerical accuracy. A reused production helper limits oracle independence.

Run `python samples/testing/test_records.py check-catalog --discover` after a Release build to check membership, methods, trace selectors, source fingerprints and expanded cases. `--update-discovery` owns the discovery block. Run commands and profile switches are recorded in each execution bundle; archives pin this catalog. Normal collection explicitly disables the three GCAM opt-ins. Level follows runner family: xUnit engine/unit/service or gated desktop/render, Python algorithm/provenance, RTL simulation, and checker replay.


<!-- BEGIN GENERATED: TEST DISCOVERY -->
| Unit | Methods | Discovered cases |
|---|---:|---:|
| `Gcam.Studio.RenderTests::Gcam.Studio.RenderTests.PlotViewRenderTests` | 1 | 1 |
| `Gcam.Studio.RenderTests::Gcam.Studio.RenderTests.ReadoutRenderTests` | 2 | 2 |
| `Gcam.Studio.Services.Tests::Gcam.Studio.Services.Tests.AcquisitionContinuationTests` | 4 | 6 |
| `Gcam.Studio.Services.Tests::Gcam.Studio.Services.Tests.AcquisitionServiceTests` | 6 | 9 |
| `Gcam.Studio.Services.Tests::Gcam.Studio.Services.Tests.AmbientAcquisitionTests` | 3 | 4 |
| `Gcam.Studio.Services.Tests::Gcam.Studio.Services.Tests.AmbientPresetTests` | 4 | 4 |
| `Gcam.Studio.Services.Tests::Gcam.Studio.Services.Tests.DetectorGapEvidenceTests` | 1 | 1 |
| `Gcam.Studio.Services.Tests::Gcam.Studio.Services.Tests.DetectorRealismTests` | 6 | 6 |
| `Gcam.Studio.Services.Tests::Gcam.Studio.Services.Tests.FocusSweepServiceTests` | 5 | 7 |
| `Gcam.Studio.Services.Tests::Gcam.Studio.Services.Tests.ImagingServiceTests` | 11 | 11 |
| `Gcam.Studio.Services.Tests::Gcam.Studio.Services.Tests.MeasurementRandomnessTests` | 1 | 1 |
| `Gcam.Studio.Services.Tests::Gcam.Studio.Services.Tests.OpticsProjectionTests` | 6 | 14 |
| `Gcam.Studio.Services.Tests::Gcam.Studio.Services.Tests.PhysicalReadoutTests` | 6 | 6 |
| `Gcam.Studio.Services.Tests::Gcam.Studio.Services.Tests.SpectrumServiceTests` | 8 | 10 |
| `Gcam.Studio.Services.Tests::Gcam.Studio.Services.Tests.StripCountEstimatorTests` | 4 | 6 |
| `Gcam.Studio.Services.Tests::Gcam.Studio.Services.Tests.StripProjectionTests` | 3 | 4 |
| `Gcam.Studio.Services.Tests::Gcam.Studio.Services.Tests.WaveformServiceTests` | 16 | 32 |
| `Gcam.Studio.Tests::Gcam.Studio.Tests.AcquisitionViewModelTests` | 8 | 8 |
| `Gcam.Studio.Tests::Gcam.Studio.Tests.AmbientViewModelTests` | 8 | 25 |
| `Gcam.Studio.Tests::Gcam.Studio.Tests.DetectorFaceTests` | 2 | 7 |
| `Gcam.Studio.Tests::Gcam.Studio.Tests.DetectorWorkspaceTests` | 4 | 7 |
| `Gcam.Studio.Tests::Gcam.Studio.Tests.FocusSweepMathTests` | 5 | 7 |
| `Gcam.Studio.Tests::Gcam.Studio.Tests.FocusSweepViewModelTests` | 4 | 9 |
| `Gcam.Studio.Tests::Gcam.Studio.Tests.HeatmapViewportTests` | 9 | 11 |
| `Gcam.Studio.Tests::Gcam.Studio.Tests.HistogramPlotTests` | 10 | 10 |
| `Gcam.Studio.Tests::Gcam.Studio.Tests.ImagingWorkspaceTests` | 10 | 10 |
| `Gcam.Studio.Tests::Gcam.Studio.Tests.MainViewModelTests` | 11 | 11 |
| `Gcam.Studio.Tests::Gcam.Studio.Tests.MeasurementMathTests` | 5 | 5 |
| `Gcam.Studio.Tests::Gcam.Studio.Tests.MeasurementsViewModelTests` | 7 | 7 |
| `Gcam.Studio.Tests::Gcam.Studio.Tests.MinMaxPyramidTests` | 4 | 19 |
| `Gcam.Studio.Tests::Gcam.Studio.Tests.NiceTicksTests` | 5 | 5 |
| `Gcam.Studio.Tests::Gcam.Studio.Tests.OpticsPolicyTests` | 5 | 12 |
| `Gcam.Studio.Tests::Gcam.Studio.Tests.OpticsViewModelTests` | 4 | 4 |
| `Gcam.Studio.Tests::Gcam.Studio.Tests.OverlayLabelLayoutTests` | 5 | 6 |
| `Gcam.Studio.Tests::Gcam.Studio.Tests.PlotSeriesTests` | 1 | 1 |
| `Gcam.Studio.Tests::Gcam.Studio.Tests.PlotViewportTests` | 4 | 5 |
| `Gcam.Studio.Tests::Gcam.Studio.Tests.ReadoutViewModelTests` | 3 | 3 |
| `Gcam.Studio.Tests::Gcam.Studio.Tests.SpectrumBandTests` | 1 | 1 |
| `Gcam.Studio.Tests::Gcam.Studio.Tests.ThemeContrastTests` | 2 | 8 |
| `Gcam.Studio.Tests::Gcam.Studio.Tests.TickFormatterTests` | 7 | 18 |
| `Gcam.Studio.Tests::Gcam.Studio.Tests.WaveformWorkspaceTests` | 10 | 15 |
| `Gcam.Studio.UiTests::Gcam.Studio.UiTests.FloodOracleTests` | 12 | 12 |
| `Gcam.Studio.UiTests::Gcam.Studio.UiTests.MlemScenarioTests` | 6 | 6 |
| `Gcam.Studio.UiTests::Gcam.Studio.UiTests.PilotTests` | 1 | 1 |
| `Gcam.Studio.UiTests::Gcam.Studio.UiTests.PlotViewTests` | 1 | 1 |
| `Gcam.Studio.UiTests::Gcam.Studio.UiTests.PolishSurveyTests` | 1 | 1 |
| `Gcam.Studio.UiTests::Gcam.Studio.UiTests.ReadoutScenarioTests` | 4 | 4 |
| `Gcam.Studio.UiTests::Gcam.Studio.UiTests.ScenarioTests` | 6 | 6 |
| `Gcam.Studio.UiTests::Gcam.Studio.UiTests.WorkspaceOracleTests` | 3 | 3 |
| `Gcam.Studio.UiTests::Gcam.Studio.UiTests.WorkspaceScenarioTests` | 5 | 5 |
| `Gcam.Tests::Gcam.Tests.AlignmentTests` | 4 | 4 |
| `Gcam.Tests::Gcam.Tests.AmbientAngularSamplerTests` | 4 | 7 |
| `Gcam.Tests::Gcam.Tests.AmbientEvidenceTests` | 6 | 6 |
| `Gcam.Tests::Gcam.Tests.AmbientFieldTests` | 14 | 21 |
| `Gcam.Tests::Gcam.Tests.AmbientGateStudyTests` | 3 | 3 |
| `Gcam.Tests::Gcam.Tests.AngularResolutionStudyTests` | 4 | 4 |
| `Gcam.Tests::Gcam.Tests.AutoFocusTests` | 1 | 2 |
| `Gcam.Tests::Gcam.Tests.BackgroundDecodingTests` | 10 | 16 |
| `Gcam.Tests::Gcam.Tests.BackgroundTests` | 6 | 6 |
| `Gcam.Tests::Gcam.Tests.CascadeEmissionTests` | 3 | 3 |
| `Gcam.Tests::Gcam.Tests.CascadeSummingTests` | 9 | 9 |
| `Gcam.Tests::Gcam.Tests.ChargeDivisionNetworkTests` | 7 | 13 |
| `Gcam.Tests::Gcam.Tests.ComptonTests` | 5 | 5 |
| `Gcam.Tests::Gcam.Tests.ConfigLoaderTests` | 4 | 19 |
| `Gcam.Tests::Gcam.Tests.CorrelationSearchTests` | 6 | 10 |
| `Gcam.Tests::Gcam.Tests.CrrcContractTests` | 5 | 10 |
| `Gcam.Tests::Gcam.Tests.CrystalMaterialTests` | 5 | 8 |
| `Gcam.Tests::Gcam.Tests.CsIMaterialTests` | 6 | 12 |
| `Gcam.Tests::Gcam.Tests.CsUnderCoTests` | 12 | 20 |
| `Gcam.Tests::Gcam.Tests.DeadTimeTests` | 5 | 5 |
| `Gcam.Tests::Gcam.Tests.DecoderInvariantTests` | 3 | 7 |
| `Gcam.Tests::Gcam.Tests.DepthDesignTests` | 2 | 2 |
| `Gcam.Tests::Gcam.Tests.DepthLocalizationTests` | 1 | 1 |
| `Gcam.Tests::Gcam.Tests.DepthTests` | 5 | 5 |
| `Gcam.Tests::Gcam.Tests.DetectorDefectTests` | 4 | 4 |
| `Gcam.Tests::Gcam.Tests.DoiParallaxTests` | 1 | 1 |
| `Gcam.Tests::Gcam.Tests.DoseTests` | 4 | 6 |
| `Gcam.Tests::Gcam.Tests.EmissionKindTests` | 3 | 3 |
| `Gcam.Tests::Gcam.Tests.EntranceAbsorberTests` | 4 | 4 |
| `Gcam.Tests::Gcam.Tests.EventStreamTests` | 7 | 8 |
| `Gcam.Tests::Gcam.Tests.ExponentialIntegralTests` | 2 | 8 |
| `Gcam.Tests::Gcam.Tests.FieldOfViewTests` | 4 | 4 |
| `Gcam.Tests::Gcam.Tests.FiniteSourceTests` | 2 | 2 |
| `Gcam.Tests::Gcam.Tests.FloodLutTests` | 6 | 7 |
| `Gcam.Tests::Gcam.Tests.FocusFusionTests` | 2 | 2 |
| `Gcam.Tests::Gcam.Tests.FrontEndPartsTests` | 2 | 2 |
| `Gcam.Tests::Gcam.Tests.FrontEndTests` | 6 | 6 |
| `Gcam.Tests::Gcam.Tests.GateResponseTests` | 5 | 9 |
| `Gcam.Tests::Gcam.Tests.HalfSpaceUncollidedTests` | 2 | 4 |
| `Gcam.Tests::Gcam.Tests.IncidentSpectrumFileTests` | 4 | 4 |
| `Gcam.Tests::Gcam.Tests.Ir192Tests` | 8 | 20 |
| `Gcam.Tests::Gcam.Tests.KleinNishinaTests` | 2 | 4 |
| `Gcam.Tests::Gcam.Tests.ListModeBackgroundTests` | 3 | 3 |
| `Gcam.Tests::Gcam.Tests.ListModeSourceTests` | 7 | 8 |
| `Gcam.Tests::Gcam.Tests.MaskAttenuationTests` | 2 | 2 |
| `Gcam.Tests::Gcam.Tests.MaskFabricationTests` | 5 | 5 |
| `Gcam.Tests::Gcam.Tests.MaskGeometryTests` | 4 | 4 |
| `Gcam.Tests::Gcam.Tests.MaskScatterTests` | 3 | 3 |
| `Gcam.Tests::Gcam.Tests.MaskSecondaryTests` | 5 | 5 |
| `Gcam.Tests::Gcam.Tests.MixedFieldTests` | 9 | 9 |
| `Gcam.Tests::Gcam.Tests.MlemOptionTests` | 9 | 16 |
| `Gcam.Tests::Gcam.Tests.MlemReconstructionTests` | 11 | 17 |
| `Gcam.Tests::Gcam.Tests.MlemTests` | 2 | 2 |
| `Gcam.Tests::Gcam.Tests.MuraGeneratorTests` | 5 | 5 |
| `Gcam.Tests::Gcam.Tests.NonProportionalityTests` | 4 | 4 |
| `Gcam.Tests::Gcam.Tests.OpticalResponseTests` | 7 | 8 |
| `Gcam.Tests::Gcam.Tests.PileUpTests` | 6 | 6 |
| `Gcam.Tests::Gcam.Tests.PipelineTests` | 7 | 7 |
| `Gcam.Tests::Gcam.Tests.RandomQualityTests` | 5 | 7 |
| `Gcam.Tests::Gcam.Tests.RangeLocalizationTests` | 1 | 3 |
| `Gcam.Tests::Gcam.Tests.ReadoutDefaultPathTests` | 7 | 7 |
| `Gcam.Tests::Gcam.Tests.ReadoutDeviceTests` | 5 | 8 |
| `Gcam.Tests::Gcam.Tests.ReadoutPulseProcessorTests` | 7 | 7 |
| `Gcam.Tests::Gcam.Tests.ReadoutStreamTests` | 2 | 4 |
| `Gcam.Tests::Gcam.Tests.ReadoutStudyTests` | 3 | 3 |
| `Gcam.Tests::Gcam.Tests.ResolvedPairTests` | 10 | 15 |
| `Gcam.Tests::Gcam.Tests.SamplingTests` | 6 | 14 |
| `Gcam.Tests::Gcam.Tests.ScattererTests` | 3 | 3 |
| `Gcam.Tests::Gcam.Tests.SceneConfigBuilderTests` | 4 | 7 |
| `Gcam.Tests::Gcam.Tests.SceneSimTests` | 2 | 2 |
| `Gcam.Tests::Gcam.Tests.SoilAirMaterialsTests` | 3 | 3 |
| `Gcam.Tests::Gcam.Tests.SoilAirTransportTests` | 4 | 4 |
| `Gcam.Tests::Gcam.Tests.SourceDistanceTests` | 2 | 2 |
| `Gcam.Tests::Gcam.Tests.SubCellTests` | 6 | 10 |
| `Gcam.Tests::Gcam.Tests.TerrestrialCatalogTests` | 3 | 3 |
| `Gcam.Tests::Gcam.Tests.TerrestrialSpectrumTests` | 5 | 5 |
| `Gcam.Tests::Gcam.Tests.ThermalDriftTests` | 6 | 6 |
| `Gcam.Tests::Gcam.Tests.ThermalReadoutTests` | 2 | 2 |
| `Gcam.Tests::Gcam.Tests.TransportInvariantTests` | 7 | 14 |
| `Gcam.Tests::Gcam.Tests.WaveformTests` | 9 | 9 |
| `checker::archived-replay` | 1 | 1 |
| `checker::calibration` | 1 | 1 |
| `checker::test-catalog` | 1 | 1 |
| `checker::test-record-self-test` | 1 | 1 |
| `cocotb::test_blr@baseline_restorer` | 1 | 1 |
| `cocotb::test_crrc@fast-bgo-f0` | 5 | 5 |
| `cocotb::test_crrc@fast-bgo-f12` | 5 | 5 |
| `cocotb::test_crrc@fast-gagg-f0` | 5 | 5 |
| `cocotb::test_crrc@fast-gagg-f12` | 5 | 5 |
| `cocotb::test_crrc@fast-lyso-f0` | 5 | 5 |
| `cocotb::test_crrc@fast-lyso-f12` | 5 | 5 |
| `cocotb::test_crrc@fast-nai-f0` | 5 | 5 |
| `cocotb::test_crrc@fast-nai-f12` | 5 | 5 |
| `cocotb::test_crrc@legacy-f0` | 5 | 5 |
| `cocotb::test_crrc@legacy-f12` | 5 | 5 |
| `cocotb::test_crrc@original-bgo-f0` | 5 | 5 |
| `cocotb::test_crrc@original-bgo-f12` | 5 | 5 |
| `cocotb::test_crrc@original-gagg-f0` | 5 | 5 |
| `cocotb::test_crrc@original-gagg-f12` | 5 | 5 |
| `cocotb::test_crrc@original-lyso-f0` | 5 | 5 |
| `cocotb::test_crrc@original-lyso-f12` | 5 | 5 |
| `cocotb::test_crrc@original-nai-f0` | 5 | 5 |
| `cocotb::test_crrc@original-nai-f12` | 5 | 5 |
| `cocotb::test_crrc@slow-bgo-f0` | 5 | 5 |
| `cocotb::test_crrc@slow-bgo-f12` | 5 | 5 |
| `cocotb::test_crrc@slow-gagg-f0` | 5 | 5 |
| `cocotb::test_crrc@slow-gagg-f12` | 5 | 5 |
| `cocotb::test_crrc@slow-lyso-f0` | 5 | 5 |
| `cocotb::test_crrc@slow-lyso-f12` | 5 | 5 |
| `cocotb::test_crrc@slow-nai-f0` | 5 | 5 |
| `cocotb::test_crrc@slow-nai-f12` | 5 | 5 |
| `cocotb::test_trap_shaper@trapezoidal_shaper` | 3 | 3 |
| `cocotb::test_trap_shaper@trapezoidal_shaper_pl` | 3 | 3 |
| `unittest::test_background_shape.BackgroundShapeTests` | 10 | 10 |
| `unittest::test_provenance.ProvenanceTests` | 29 | 29 |
| `unittest::test_readout_netlist.ReadoutNetlistTests` | 4 | 4 |
<!-- END GENERATED: TEST DISCOVERY -->

## PlotViewRenderTests — Gcam.Studio.RenderTests

Checks detached Studio views in both themes and at different plot scales, including bindings, layout, hover readouts and workspace controls. Analytic fixtures exercise rendering without starting Studio or creating a visible window.

Oracle: Analytic spectrum, waveform and focus curves, fake acquisition snapshots and direct production control/viewmodel state supply binding, readout and layout expectations. Saved PNGs have no golden-pixel oracle.

Tolerance: Fixed ±5% focus and .01 layout/decimal margins are not derived. The 60 s join is a deadlock deadline. Unexplained margins: tolerance stated without a derivation in the code.

Gates: `GCAM_RENDER_SNAPSHOTS`.

Source: [tests/Gcam.Studio.RenderTests/PlotViewRenderTests.cs](../tests/Gcam.Studio.RenderTests/PlotViewRenderTests.cs), [tests/Gcam.Studio.RenderTests/AmbientRenderTests.cs](../tests/Gcam.Studio.RenderTests/AmbientRenderTests.cs), [tests/Gcam.Studio.RenderTests/DetectorFocusRenderTests.cs](../tests/Gcam.Studio.RenderTests/DetectorFocusRenderTests.cs), [tests/Gcam.Studio.RenderTests/HeatmapReadoutChecks.cs](../tests/Gcam.Studio.RenderTests/HeatmapReadoutChecks.cs), [tests/Gcam.Studio.RenderTests/MainWindowRenderTests.cs](../tests/Gcam.Studio.RenderTests/MainWindowRenderTests.cs), [tests/Gcam.Studio.RenderTests/WaveformRenderTests.cs](../tests/Gcam.Studio.RenderTests/WaveformRenderTests.cs).

```json
{"id":"Gcam.Studio.RenderTests::Gcam.Studio.RenderTests.PlotViewRenderTests","runner":"dotnet","methods":["Spectrum_BothThemesFullAndZoom_RenderWithoutWindow"],"sources":{"tests/Gcam.Studio.RenderTests/PlotViewRenderTests.cs":"1622cbb97c42700a8d2ceb8c36a903bf62dbcb5542fd3b3e98aeebaf9a817460","tests/Gcam.Studio.RenderTests/AmbientRenderTests.cs":"fc2278723299bfe5d048c12256b3727cb3686024019a00404cc31d39e4593731","tests/Gcam.Studio.RenderTests/DetectorFocusRenderTests.cs":"202e59c552c9f5fd8826e7df7689080e29edd985370ce016d394c4da0e0aac36","tests/Gcam.Studio.RenderTests/HeatmapReadoutChecks.cs":"213a8cd5030d7cc186ed144c4459cb38330c0a1e4174720ad0abe67145be6471","tests/Gcam.Studio.RenderTests/MainWindowRenderTests.cs":"fa1439c011a9fb44c383159a27ea79af171a7804fe0787fa1e87b9adaae37315","tests/Gcam.Studio.RenderTests/WaveformRenderTests.cs":"1cf71c0357b2ff9317591b4967fd641d138bc2a2b74c758bf150fe8b3dad9627","tests/Gcam.Studio.RenderTests/RenderSnapshotFactAttribute.cs":"95dd1088eee764d18a539ff37399a3c518aa75056d29d2de60286d2173ba1edd"},"trace":[{"requirement":"SR-OPT-06","selectors":["Spectrum_BothThemesFullAndZoom_RenderWithoutWindow"]},{"requirement":"SR-RUN-29","selectors":["Spectrum_BothThemesFullAndZoom_RenderWithoutWindow"]},{"requirement":"SR-PLOT-06","selectors":["Spectrum_BothThemesFullAndZoom_RenderWithoutWindow"]},{"requirement":"SR-PLOT-11","selectors":["Spectrum_BothThemesFullAndZoom_RenderWithoutWindow"]}]}
```


## AcquisitionContinuationTests — Gcam.Studio.Services.Tests

Verifies that Stop / Continue preserves the uninterrupted acquisition event stream and immutable prefixes.

Oracle: An uninterrupted session supplies the expected pixel, true deposit and arrival-time records; retained prefixes and configured state transitions supply the other expectations.

Tolerance: exact

Gates: none.

Source: [tests/Gcam.Studio.Services.Tests/AcquisitionContinuationTests.cs](../tests/Gcam.Studio.Services.Tests/AcquisitionContinuationTests.cs).

```json
{"id":"Gcam.Studio.Services.Tests::Gcam.Studio.Services.Tests.AcquisitionContinuationTests","runner":"dotnet","methods":["StopAndContinue_ReproducesTheUninterruptedEventStream","Completed_RaisedPreset_ContinuesExactly","Continue_IsRejectedWhileRunning","Seed_FixedReproduces_OtherSeedIsAnIndependentAcquisition"],"sources":{"tests/Gcam.Studio.Services.Tests/AcquisitionContinuationTests.cs":"d77390ab37a9dccf9ad27cba0cfa01ad76fd24501dfb3d226dbfdae8afd8614a","tests/Gcam.Studio.Services.Tests/EvidenceFactAttribute.cs":"898301fdfe0438aa4ea3503809ee81dc90b225dff52defb3e2cee6277085cd60"},"trace":[{"requirement":"SR-RUN-23","selectors":["Completed_RaisedPreset_ContinuesExactly","Continue_IsRejectedWhileRunning","StopAndContinue_ReproducesTheUninterruptedEventStream"]},{"requirement":"SR-RUN-25","selectors":["Seed_FixedReproduces_OtherSeedIsAnIndependentAcquisition"]}]}
```


## AcquisitionServiceTests — Gcam.Studio.Services.Tests

Checks that the real acquisition service captures detector settings, publishes immutable snapshots and preserves the consumed event prefix on Stop. It also checks source localization, detector pixel coordinates, input rejection and live-time progress under an injected clock.

Oracle: Configured CsI detector settings, the source coordinate and projected resolution element supply physical expectations; retained snapshot copies, consumed event prefixes, decoder pixel centres and an injected clock supply session and timing references.

Tolerance: Localization uses the projected resolution element derived from optics; clock/deadlock timeouts are operational bounds, not physics tolerances.

Gates: none.

Source: [tests/Gcam.Studio.Services.Tests/AcquisitionServiceTests.cs](../tests/Gcam.Studio.Services.Tests/AcquisitionServiceTests.cs).

Method groups:

- `CsISelection_IsCapturedByTheRealAcquisition`, `ShortAcquisition_LocalizesAfterCountThreshold_SnapshotsAreImmutable`, `Stop_KeepsConsumedPrefix_AtExtremeMcLimitedSpeed`, `InvalidInputs_FailBeforeStarting`, `InjectedClock_AdvancesLiveTimeAndPresetWithoutWallDelay`: exact / structural fixture comparisons; gate None.
- `FloodAxis_MatchesTheDecodersPixelCentres`: numerical comparisons described above; gate None.

```json
{"id":"Gcam.Studio.Services.Tests::Gcam.Studio.Services.Tests.AcquisitionServiceTests","runner":"dotnet","methods":["CsISelection_IsCapturedByTheRealAcquisition","ShortAcquisition_LocalizesAfterCountThreshold_SnapshotsAreImmutable","FloodAxis_MatchesTheDecodersPixelCentres","Stop_KeepsConsumedPrefix_AtExtremeMcLimitedSpeed","InvalidInputs_FailBeforeStarting","InjectedClock_AdvancesLiveTimeAndPresetWithoutWallDelay"],"sources":{"tests/Gcam.Studio.Services.Tests/AcquisitionServiceTests.cs":"ab2df2744127a912ddd910ba7544420009a92e09a47e13a2244c00d28484047d","tests/Gcam.Studio.Services.Tests/EvidenceFactAttribute.cs":"898301fdfe0438aa4ea3503809ee81dc90b225dff52defb3e2cee6277085cd60"},"trace":[{"requirement":"SR-RUN-09","selectors":["ShortAcquisition_LocalizesAfterCountThreshold_SnapshotsAreImmutable"]},{"requirement":"SR-RUN-10","selectors":["Stop_KeepsConsumedPrefix_AtExtremeMcLimitedSpeed"]},{"requirement":"SR-RUN-11","selectors":["InjectedClock_AdvancesLiveTimeAndPresetWithoutWallDelay"]},{"requirement":"SR-RUN-15","selectors":["InvalidInputs_FailBeforeStarting"]},{"requirement":"SR-VIEW-05","selectors":["FloodAxis_MatchesTheDecodersPixelCentres"]}]}
```


## AmbientAcquisitionTests — Gcam.Studio.Services.Tests

Checks that background-only Stop / Continue reproduces a fixed-time acquisition event for event. It also checks frozen configuration, completion with a zero field and workspace counts without invented isotope lines.

Oracle: Fixed-time background-only events are compared exactly with partitioned Stop/Continue sessions. Frozen config, zero-field completion and absence of invented isotope lines are structural expectations.

Tolerance: exact

Gates: none.

Source: [tests/Gcam.Studio.Services.Tests/AmbientAcquisitionTests.cs](../tests/Gcam.Studio.Services.Tests/AmbientAcquisitionTests.cs).

```json
{"id":"Gcam.Studio.Services.Tests::Gcam.Studio.Services.Tests.AmbientAcquisitionTests","runner":"dotnet","methods":["BackgroundOnly_StopContinueRetainsExactlyTheFixedTimeStream","EmptyZeroField_CompletesAndBuildConfigFreezesInputs","BackgroundOnly_WorkspacesShowCountsWithoutInventingIsotopeLines"],"sources":{"tests/Gcam.Studio.Services.Tests/AmbientAcquisitionTests.cs":"6b21c87bd4ed837ccb654b2911bbbcee0dc6e85359fd7de28cb48a1499379384","tests/Gcam.Studio.Services.Tests/EvidenceFactAttribute.cs":"898301fdfe0438aa4ea3503809ee81dc90b225dff52defb3e2cee6277085cd60"},"trace":[{"requirement":"SR-RUN-29","selectors":["BackgroundOnly_StopContinueRetainsExactlyTheFixedTimeStream","BackgroundOnly_WorkspacesShowCountsWithoutInventingIsotopeLines","EmptyZeroField_CompletesAndBuildConfigFreezesInputs"]}]}
```


## AmbientPresetTests — Gcam.Studio.Services.Tests

Studio's ambient preset (AB-15): the validated terrestrial spectrum is deployed beside the assemblies, pinned in code to the repository's sidecar hash, and resolved by the engine loader on the frozen config.

Oracle: Repository sidecar hash and deployed bytes are exact references.

Tolerance: Frozen-copy resolution, wrong/missing pin rejection and default acquisition presence are structural checks, not rate-precision evidence.

Gates: none.

Source: [tests/Gcam.Studio.Services.Tests/AmbientPresetTests.cs](../tests/Gcam.Studio.Services.Tests/AmbientPresetTests.cs).

```json
{"id":"Gcam.Studio.Services.Tests::Gcam.Studio.Services.Tests.AmbientPresetTests","runner":"dotnet","methods":["Pin_EqualsRepositorySidecar_AndDeployedBytesAreTheRepositoryFile","BuildConfig_ResolvesTheValidatedSpectrum_OnTheFrozenCopyOnly","WrongPinOrMissingFile_IsRefusedAtStart","DefaultField_AcquiresWithTheValidatedSpectrum"],"sources":{"tests/Gcam.Studio.Services.Tests/AmbientPresetTests.cs":"36f110f477e5af12a932684ed2a9c5dba3fce8a5f795b2a9063ecd73d452d57e","tests/Gcam.Studio.Services.Tests/EvidenceFactAttribute.cs":"898301fdfe0438aa4ea3503809ee81dc90b225dff52defb3e2cee6277085cd60"},"trace":[{"requirement":"SR-RUN-29","selectors":["BuildConfig_ResolvesTheValidatedSpectrum_OnTheFrozenCopyOnly","DefaultField_AcquiresWithTheValidatedSpectrum","Pin_EqualsRepositorySidecar_AndDeployedBytesAreTheRepositoryFile","WrongPinOrMissingFile_IsRefusedAtStart"]}]}
```


## DetectorGapEvidenceTests — Gcam.Studio.Services.Tests

Opt-in Studio-path MC evidence, with independent spread and validation seeds.

Oracle: Expected rate ratio is active geometric area.

Tolerance: Independent spread seeds give 4*s*sqrt(1+1/n), a per-seed predictive bound including calibration-mean uncertainty, not estimator precision.

Gates: `GCAM_EVIDENCE_TESTS`.

Source: [tests/Gcam.Studio.Services.Tests/DetectorGapEvidenceTests.cs](../tests/Gcam.Studio.Services.Tests/DetectorGapEvidenceTests.cs).

```json
{"id":"Gcam.Studio.Services.Tests::Gcam.Studio.Services.Tests.DetectorGapEvidenceTests","runner":"dotnet","methods":["RateRatio_MatchesGeometricArea_WithinMeasuredSeedSpread"],"sources":{"tests/Gcam.Studio.Services.Tests/DetectorGapEvidenceTests.cs":"2870645982cc60502a8b8201013de023e63a76ce970e1e338abd1c48dc225601","tests/Gcam.Studio.Services.Tests/EvidenceFactAttribute.cs":"898301fdfe0438aa4ea3503809ee81dc90b225dff52defb3e2cee6277085cd60"},"trace":[]}
```


## DetectorRealismTests — Gcam.Studio.Services.Tests

Checks the spectral effects of an entrance absorber, detector backing and per-crystal gain variation in real Studio acquisitions. It also checks explicit defaults, frozen-input replay and reports bare versus realistic throughput.

Oracle: Independent Beer-Lambert calculations from iron attenuation anchors supply narrow-beam transmission; a separately weighted transport reference supplies broad-band counts. Compton return energy and independently computed gain-mixture moments supply backing and photopeak expectations, while retained inputs/events supply replay identity.

Tolerance: Four Poisson/delta-method standard errors combine all reference variances. Gain uses quadrature variance and fourth-moment SE; FWHM uses two bins plus five width-estimator SE. Throughput is measurement only.

Gates: none.

Source: [tests/Gcam.Studio.Services.Tests/DetectorRealismTests.cs](../tests/Gcam.Studio.Services.Tests/DetectorRealismTests.cs).

```json
{"id":"Gcam.Studio.Services.Tests::Gcam.Studio.Services.Tests.DetectorRealismTests","runner":"dotnet","methods":["StudioDefaults_AreExplicit_AndSceneBuilderRemainsBare","BaKAbsorber_UncollidedBandMatchesIndependentNarrowBeam_ReportsMeasuredSpectrum","Backing_AddsBackscatterRegion_FromTransport","Gain_WidensPhotopeakInQuadrature_AndReducesTightWindowAcceptance","Throughput_RecordsBareAndRealisticAcquisition","Measurement_UsesFrozenInputs_AndReplaysAcrossSnapshotsAndPileUp"],"sources":{"tests/Gcam.Studio.Services.Tests/DetectorRealismTests.cs":"591e6aa566858176b5555b7039ffdb939a8907695411d1719cfd839c62cedf7f","tests/Gcam.Studio.Services.Tests/EvidenceFactAttribute.cs":"898301fdfe0438aa4ea3503809ee81dc90b225dff52defb3e2cee6277085cd60"},"trace":[{"requirement":"SR-RUN-20","selectors":["StudioDefaults_AreExplicit_AndSceneBuilderRemainsBare"]},{"requirement":"SR-RUN-21","selectors":["Gain_WidensPhotopeakInQuadrature_AndReducesTightWindowAcceptance","Measurement_UsesFrozenInputs_AndReplaysAcrossSnapshotsAndPileUp"]}]}
```


## FocusSweepServiceTests — Gcam.Studio.Services.Tests

Checks that a focus sweep uses the retained detector flood without events or scene truth, leaves that flood unchanged and handles cancellation or empty data. It also checks detector-face gain consistency and rejection of invalid reflector gaps.

Oracle: A synthetic retained flood with no scene truth or event list supplies sweep input; FocusSweepMath.Planes supplies expected planes, so that check reuses production math. The original flood bytes, empty/cancelled fixtures and CrystalUniformity gain array supply preservation, refusal and detector-face references.

Tolerance: Detector gains compare exactly with the measurement-pattern array and invalid gap rejection is structural. No approximate numerical tolerance.

Gates: none.

Source: [tests/Gcam.Studio.Services.Tests/FocusSweepServiceTests.cs](../tests/Gcam.Studio.Services.Tests/FocusSweepServiceTests.cs).

```json
{"id":"Gcam.Studio.Services.Tests::Gcam.Studio.Services.Tests.FocusSweepServiceTests","runner":"dotnet","methods":["Sweep_UsesRetainedFlood_WithoutEventsOrSceneTruth","Sweep_PreCancelledWorkerCannotPublish","Sweep_EmptyFloodIsUnresolved","Face_GainsMatchMeasurementPattern","Config_RejectsGapAtServiceBoundary"],"sources":{"tests/Gcam.Studio.Services.Tests/FocusSweepServiceTests.cs":"287ce16c1a0663b39606eb476a811e11c291ca78eb698965fd843aaf05067e29","tests/Gcam.Studio.Services.Tests/EvidenceFactAttribute.cs":"898301fdfe0438aa4ea3503809ee81dc90b225dff52defb3e2cee6277085cd60"},"trace":[]}
```


## ImagingServiceTests — Gcam.Studio.Services.Tests

Checks isotope-channel localization and signed spectral stripping, including net counts, calibration uncertainty and overlapping-window covariance. It also checks retained-event replay, incremental-prefix equivalence and MLEM matrix reuse across refreshes.

Oracle: Pure-Cs acquisition, independently recomputed strip variance/covariance, retained event replay, constructed decoder and known source sides/coordinates provide the oracles.

Tolerance: Counting/calibration variances use stated standard-error budgets; expanded covariance allows 32 machine epsilons times the absolute-term sum. Localization uses the geometric cell diagonal; precision measurements do not assert a numerical precision ceiling.

Gates: `GCAM_EVIDENCE_TESTS`.

Source: [tests/Gcam.Studio.Services.Tests/ImagingServiceTests.cs](../tests/Gcam.Studio.Services.Tests/ImagingServiceTests.cs).

Method groups:

- `Strip_CoLocatedCsAndDoubleActivityCo_RecoversSameLiveTimeCsCount`, `Strip_UsesSignedCcInput_AndMatchesMlemCountAndUncertainty`, `Strip_OverlapCovariance_ReplaysWithWindowChangesAndIncrementalPrefixes`, `Strip_MultipleContaminants_ReportNetWithUnavailableUncertainty`, `Channels_OffAxisCsAndCo_LocalizeAtTheirOwnSource`, `WindowChange_ReplaysRetainedEvents_RecalibratesAndMatchesFreshProcessing`, `IncrementalChannels_MatchWholePrefixAndSharedMeasuredEnergyWindows`, `Mlem_ChannelsLocalizeAtTheirOwnSource_NonNegative`, `Mlem_BuildsOneMatrixPerGeometryAndLine_AndReusesItAcrossRefreshes`, `CrossCorrelation_IsUnchangedByTheMethodField`: exact / structural fixture comparisons; gate None.
- `Precision_TwentySeeds_MeasuresMixedIsotopesAndCoOnlyControl`: exact / structural fixture comparisons; gate GCAM_EVIDENCE_TESTS.

```json
{"id":"Gcam.Studio.Services.Tests::Gcam.Studio.Services.Tests.ImagingServiceTests","runner":"dotnet","methods":["Strip_CoLocatedCsAndDoubleActivityCo_RecoversSameLiveTimeCsCount","Strip_UsesSignedCcInput_AndMatchesMlemCountAndUncertainty","Strip_OverlapCovariance_ReplaysWithWindowChangesAndIncrementalPrefixes","Strip_MultipleContaminants_ReportNetWithUnavailableUncertainty","Channels_OffAxisCsAndCo_LocalizeAtTheirOwnSource","WindowChange_ReplaysRetainedEvents_RecalibratesAndMatchesFreshProcessing","Precision_TwentySeeds_MeasuresMixedIsotopesAndCoOnlyControl","IncrementalChannels_MatchWholePrefixAndSharedMeasuredEnergyWindows","Mlem_ChannelsLocalizeAtTheirOwnSource_NonNegative","Mlem_BuildsOneMatrixPerGeometryAndLine_AndReusesItAcrossRefreshes","CrossCorrelation_IsUnchangedByTheMethodField"],"sources":{"tests/Gcam.Studio.Services.Tests/ImagingServiceTests.cs":"2f327ee044a798baed14c2b26a3cf1117fd22369529d9d929e5b194e9a904e02","tests/Gcam.Studio.Services.Tests/EvidenceFactAttribute.cs":"898301fdfe0438aa4ea3503809ee81dc90b225dff52defb3e2cee6277085cd60"},"trace":[{"requirement":"SR-IMG-01","selectors":["WindowChange_ReplaysRetainedEvents_RecalibratesAndMatchesFreshProcessing"]},{"requirement":"SR-IMG-02","selectors":["Channels_OffAxisCsAndCo_LocalizeAtTheirOwnSource","CrossCorrelation_IsUnchangedByTheMethodField","IncrementalChannels_MatchWholePrefixAndSharedMeasuredEnergyWindows","Mlem_BuildsOneMatrixPerGeometryAndLine_AndReusesItAcrossRefreshes","Mlem_ChannelsLocalizeAtTheirOwnSource_NonNegative","Precision_TwentySeeds_MeasuresMixedIsotopesAndCoOnlyControl","Strip_CoLocatedCsAndDoubleActivityCo_RecoversSameLiveTimeCsCount","Strip_MultipleContaminants_ReportNetWithUnavailableUncertainty","Strip_OverlapCovariance_ReplaysWithWindowChangesAndIncrementalPrefixes","Strip_UsesSignedCcInput_AndMatchesMlemCountAndUncertainty","WindowChange_ReplaysRetainedEvents_RecalibratesAndMatchesFreshProcessing"]},{"requirement":"SR-IMG-04","selectors":["Channels_OffAxisCsAndCo_LocalizeAtTheirOwnSource","Precision_TwentySeeds_MeasuresMixedIsotopesAndCoOnlyControl"]},{"requirement":"SR-IMG-05","selectors":["Strip_CoLocatedCsAndDoubleActivityCo_RecoversSameLiveTimeCsCount","Strip_MultipleContaminants_ReportNetWithUnavailableUncertainty","Strip_OverlapCovariance_ReplaysWithWindowChangesAndIncrementalPrefixes","Strip_UsesSignedCcInput_AndMatchesMlemCountAndUncertainty","WindowChange_ReplaysRetainedEvents_RecalibratesAndMatchesFreshProcessing"]},{"requirement":"SR-IMG-07","selectors":["CrossCorrelation_IsUnchangedByTheMethodField","Mlem_BuildsOneMatrixPerGeometryAndLine_AndReusesItAcrossRefreshes","Mlem_ChannelsLocalizeAtTheirOwnSource_NonNegative"]}]}
```


## MeasurementRandomnessTests — Gcam.Studio.Services.Tests

The per-event energy smear of <see cref="MeasurementStage"/> behaves like independent draws (TODO-26).

Oracle: Independent draws have lag-one correlation SE=1/sqrt(N); block-binomial variance ratio has SE=sqrt(2/(B−1)).

Tolerance: Both use four SE as derived in comments.

Gates: none.

Source: [tests/Gcam.Studio.Services.Tests/MeasurementRandomnessTests.cs](../tests/Gcam.Studio.Services.Tests/MeasurementRandomnessTests.cs).

```json
{"id":"Gcam.Studio.Services.Tests::Gcam.Studio.Services.Tests.MeasurementRandomnessTests","runner":"dotnet","methods":["Smears_AreUncorrelated_AndWindowCountsAreBinomial"],"sources":{"tests/Gcam.Studio.Services.Tests/MeasurementRandomnessTests.cs":"92bfe4723267ecf9f442d93095ebc489d8f057cdc27918355f09a27ac13b3429","tests/Gcam.Studio.Services.Tests/EvidenceFactAttribute.cs":"898301fdfe0438aa4ea3503809ee81dc90b225dff52defb3e2cee6277085cd60"},"trace":[]}
```


## OpticsProjectionTests — Gcam.Studio.Services.Tests

Checks that refocusing reprojects all channels without new events or measurement calibration. It also checks localization under optics presets and the effect of finer detector sampling at fixed detector size.

Oracle: The original retained events, measurement settings and calibration identity supply refocus-preservation references. Configured isotope coordinates and the projected resolution element supply localization expectations; fine/coarse reconstructions of the same configured source positions supply sampling comparisons.

Tolerance: Small-sample RMS ratio .85 comes from the quoted five-seed maximum plus twice the observed range, rounded upward; it protects only those pinned regressions. The full-sweep .75 improvement thresholds are stated without derivation. Unexplained margins: tolerance stated without a derivation in the code.

Gates: `GCAM_EVIDENCE_TESTS`.

Source: [tests/Gcam.Studio.Services.Tests/OpticsProjectionTests.cs](../tests/Gcam.Studio.Services.Tests/OpticsProjectionTests.cs).

Method groups:

- `Refocus_ReprojectsAllChannels_WithoutMeasurementCalibrationOrNewEvents`, `EmptySnapshot_RefocusKeepsEmptyImagesAndDoesNotCalibrate`, `Presets_NormalizedMixedScene_LocalizeWindowedChannelsWithinOneElement`, `Sampling_SmallOffAxisSample_FinerPixelsReduceLocalizationError`: exact / structural fixture comparisons; gate None.
- `Sampling_PositionSweepAtConstantDetectorSize_FinerPixelsReduceLocalizationError`: numerical comparisons described above; gate GCAM_EVIDENCE_TESTS.
- `Sampling_SmallBudgetSeedSpread_ReportsRegressionMargin`: exact / structural fixture comparisons; gate GCAM_EVIDENCE_TESTS.

```json
{"id":"Gcam.Studio.Services.Tests::Gcam.Studio.Services.Tests.OpticsProjectionTests","runner":"dotnet","methods":["Refocus_ReprojectsAllChannels_WithoutMeasurementCalibrationOrNewEvents","EmptySnapshot_RefocusKeepsEmptyImagesAndDoesNotCalibrate","Presets_NormalizedMixedScene_LocalizeWindowedChannelsWithinOneElement","Sampling_PositionSweepAtConstantDetectorSize_FinerPixelsReduceLocalizationError","Sampling_SmallOffAxisSample_FinerPixelsReduceLocalizationError","Sampling_SmallBudgetSeedSpread_ReportsRegressionMargin"],"sources":{"tests/Gcam.Studio.Services.Tests/OpticsProjectionTests.cs":"a70b022dd0c241c32d86418cc3f5da2173f2608003020f0e3e17b6fa0c6eecb3","tests/Gcam.Studio.Services.Tests/EvidenceFactAttribute.cs":"898301fdfe0438aa4ea3503809ee81dc90b225dff52defb3e2cee6277085cd60"},"trace":[{"requirement":"SR-OPT-04","selectors":["EmptySnapshot_RefocusKeepsEmptyImagesAndDoesNotCalibrate","Refocus_ReprojectsAllChannels_WithoutMeasurementCalibrationOrNewEvents"]},{"requirement":"SR-OPT-05","selectors":["Sampling_SmallOffAxisSample_FinerPixelsReduceLocalizationError"]}]}
```


## SpectrumServiceTests — Gcam.Studio.Services.Tests

Checks photopeak position and width, pile-up redistribution, resolution-based line grouping and deterministic replay of retained events. It also checks a fixed 0-2000 keV axis with explicit overflow retention and reports full versus incremental processing cost.

Oracle: Retained event/histogram counts and the configured Gaussian chain width supply count and FWHM expectations. A separate replay of the engine pile-up operation supplies pulse counts, explicit line-separation fixtures define merging and injected above-axis deposits define overflow retention.

Tolerance: Width tolerance is two bins plus five FWHM/sqrt(2(N−1)) standard errors, as commented. Ordering, replay and sample caps are structural; timings are measurement only.

Gates: none.

Source: [tests/Gcam.Studio.Services.Tests/SpectrumServiceTests.cs](../tests/Gcam.Studio.Services.Tests/SpectrumServiceTests.cs).

Method groups:

- `CsAcquisition_PhotopeakBinAndFwhmMatchChain`, `Axis_IsFixedAt2000keV_ForAnyLinesFieldOrPileUp_AndOverflowKeepsEveryPulse`: numerical comparisons described above; gate None.
- `HighRate_PileUpLosesPulsesAndMovesCountsAbovePhotopeak`, `MixedCsCo_HasFourBandsAndUnionCountsEachBinOnce`, `Merge_UsesResolutionRatherThanWindowOverlap`, `Merge_SingleLineGivesOneBand`, `SeedAndSnapshotPartition_AreDeterministic_ToggleReplaysExactly`, `Processing_MeasuresFullAndIncrementalWorkAt100000Events`: exact / structural fixture comparisons; gate None.

```json
{"id":"Gcam.Studio.Services.Tests::Gcam.Studio.Services.Tests.SpectrumServiceTests","runner":"dotnet","methods":["CsAcquisition_PhotopeakBinAndFwhmMatchChain","HighRate_PileUpLosesPulsesAndMovesCountsAbovePhotopeak","MixedCsCo_HasFourBandsAndUnionCountsEachBinOnce","Merge_UsesResolutionRatherThanWindowOverlap","Merge_SingleLineGivesOneBand","SeedAndSnapshotPartition_AreDeterministic_ToggleReplaysExactly","Processing_MeasuresFullAndIncrementalWorkAt100000Events","Axis_IsFixedAt2000keV_ForAnyLinesFieldOrPileUp_AndOverflowKeepsEveryPulse"],"sources":{"tests/Gcam.Studio.Services.Tests/SpectrumServiceTests.cs":"8a4a96c47c6f4e5619035596ac1828f4fe426655322e1aa20599f13940ee96bb","tests/Gcam.Studio.Services.Tests/EvidenceFactAttribute.cs":"898301fdfe0438aa4ea3503809ee81dc90b225dff52defb3e2cee6277085cd60"},"trace":[{"requirement":"SR-SPEC-01","selectors":["Axis_IsFixedAt2000keV_ForAnyLinesFieldOrPileUp_AndOverflowKeepsEveryPulse","CsAcquisition_PhotopeakBinAndFwhmMatchChain"]},{"requirement":"SR-SPEC-02","selectors":["CsAcquisition_PhotopeakBinAndFwhmMatchChain"]},{"requirement":"SR-SPEC-03","selectors":["HighRate_PileUpLosesPulsesAndMovesCountsAbovePhotopeak"]},{"requirement":"SR-SPEC-04","selectors":["Merge_SingleLineGivesOneBand","Merge_UsesResolutionRatherThanWindowOverlap","MixedCsCo_HasFourBandsAndUnionCountsEachBinOnce"]},{"requirement":"SR-SPEC-05","selectors":["MixedCsCo_HasFourBandsAndUnionCountsEachBinOnce"]},{"requirement":"SR-SPEC-07","selectors":["Processing_MeasuresFullAndIncrementalWorkAt100000Events","SeedAndSnapshotPartition_AreDeterministic_ToggleReplaysExactly"]}]}
```


## StripCountEstimatorTests — Gcam.Studio.Services.Tests

Checks the signed low-minus-scaled-high count estimator and its reported uncertainty for overlapping windows and finite calibration samples. Identical windows must cancel exactly; unsupported covariance or calibration inputs must report unavailable uncertainty.

Oracle: Independent fixed-total calibration moments, ratio bias and mixture moments are computed before sampling.

Tolerance: Four-standard-error Chebyshev bounds give failure probability<=1/16; identical-window net/variance and unsupported cases are exact/structural.

Gates: none.

Source: [tests/Gcam.Studio.Services.Tests/StripCountEstimatorTests.cs](../tests/Gcam.Studio.Services.Tests/StripCountEstimatorTests.cs).

```json
{"id":"Gcam.Studio.Services.Tests::Gcam.Studio.Services.Tests.StripCountEstimatorTests","runner":"dotnet","methods":["Overlap_IdenticalWindows_HaveExactlyZeroNetAndVariance","ZeroCalibrationLow_OrMissingCovariance_ReportsUnavailableUncertainty","NetSign_IsIndependentOfAcquisitionSupport","IndependentFixedTotalCalibration_ReportedVarianceMatchesDerivedSamplingBounds"],"sources":{"tests/Gcam.Studio.Services.Tests/StripCountEstimatorTests.cs":"35085f9454b7626ac2bb2b9c25ae64448cc0fc20050c33a1fce797e455c05792","tests/Gcam.Studio.Services.Tests/EvidenceFactAttribute.cs":"898301fdfe0438aa4ea3503809ee81dc90b225dff52defb3e2cee6277085cd60"},"trace":[{"requirement":"SR-IMG-05","selectors":["IndependentFixedTotalCalibration_ReportedVarianceMatchesDerivedSamplingBounds","NetSign_IsIndependentOfAcquisitionSupport","Overlap_IdenticalWindows_HaveExactlyZeroNetAndVariance","ZeroCalibrationLow_OrMissingCovariance_ReportsUnavailableUncertainty"]}]}
```


## StripProjectionTests — Gcam.Studio.Services.Tests

Checks that reconstruction depends on raw acquisition support rather than the sign of the stripped net count. Empty raw windows must yield no reconstruction, while supported negative nets remain available to cross-correlation and MLEM.

Oracle: Raw acquisition support, signed low−R*high inputs and explicit decoder construction are the references. Empty support yields no reconstruction; a negative net does not imply absent data. Comparisons are exact/structural.

Tolerance: exact

Gates: none.

Source: [tests/Gcam.Studio.Services.Tests/StripProjectionTests.cs](../tests/Gcam.Studio.Services.Tests/StripProjectionTests.cs).

```json
{"id":"Gcam.Studio.Services.Tests::Gcam.Studio.Services.Tests.StripProjectionTests","runner":"dotnet","methods":["SignedZeroOrNegativeNet_DecodesWithRawSupport","EmptyRawWindows_DoNotCreateAReconstruction","Mlem_HighOnlyRawSupport_KeepsNegativeNetWithoutADetectionGate"],"sources":{"tests/Gcam.Studio.Services.Tests/StripProjectionTests.cs":"034d65d611874407e0c3a52a57e6a9940a7310e4a293804b7d2dcebbb415b82d","tests/Gcam.Studio.Services.Tests/EvidenceFactAttribute.cs":"898301fdfe0438aa4ea3503809ee81dc90b225dff52defb3e2cee6277085cd60"},"trace":[{"requirement":"SR-IMG-05","selectors":["EmptyRawWindows_DoNotCreateAReconstruction","Mlem_HighOnlyRawSupport_KeepsNegativeNetWithoutADetectionGate","SignedZeroOrNegativeNet_DecodesWithRawSupport"]}]}
```


## WaveformServiceTests — Gcam.Studio.Services.Tests

Checks conversion of retained acquisition events into ADC and shaped waveforms, including preceding-pulse tails, time origins, acquired chain settings and readout suppression. It also checks legacy rasterizer equivalence, cancellation, deterministic rate studies and bounded plot work.

Oracle: Legacy rasterization, retained pulse/event identity, chain/settings consistency and bounded plot work are the oracles.

Tolerance: Performance reports are measurement only; pulse-readout numerical comparisons retain their coded precision, without a general derived rounding budget. Unexplained margins: tolerance stated without a derivation in the code.

Gates: `GCAM_EVIDENCE_TESTS`.

Source: [tests/Gcam.Studio.Services.Tests/WaveformServiceTests.cs](../tests/Gcam.Studio.Services.Tests/WaveformServiceTests.cs).

Method groups:

- `Crrc_PlotsFractionalCodesAndLabelsSimulationTime`, `Chain_SetsTransportMaterialWithoutChangingEngineDefaults`, `Chain_RejectsUnsupportedMaterial`, `Response_AgreesAcrossWaveformSpectrumAndImaging`, `Rasterizer_MatchesLegacyWithoutSecondIntrinsicSmear`, `Window_PreservesRequestedLengthAndPrecedingPulseAcrossPixels`, `TimeBase_IsOriginRelativeAtLateAcquisitionTime`, `RateStudy_IsDeterministicAndLeavesAcquisitionUntouched`, `AcquiredChain_IsUsedAfterPendingSelectionChanges`, `Readout_SuppressesPartialOverlapAndSaturation`, `InvalidRequestsAndCancellation_FailWithoutPublishing`, `RetainedMonteCarloEvents_PreserveAssociationAndTime`: exact / structural fixture comparisons; gate None.
- `Ideal_HasNoNoiseOrSmearAndKeepsTail`: numerical comparisons described above; gate None.
- `MaximumWindow_ReportsWorkerCostAndBoundedPlotSamples`, `RetainedMonteCarlo_IsolatedPulseReadoutsAcrossParts`, `MixedFieldCalibration_UsesAcquiredChainAndInvalidatesRatios`: exact / structural fixture comparisons; gate GCAM_EVIDENCE_TESTS.

```json
{"id":"Gcam.Studio.Services.Tests::Gcam.Studio.Services.Tests.WaveformServiceTests","runner":"dotnet","methods":["Crrc_PlotsFractionalCodesAndLabelsSimulationTime","Chain_SetsTransportMaterialWithoutChangingEngineDefaults","Chain_RejectsUnsupportedMaterial","Response_AgreesAcrossWaveformSpectrumAndImaging","Rasterizer_MatchesLegacyWithoutSecondIntrinsicSmear","Window_PreservesRequestedLengthAndPrecedingPulseAcrossPixels","Ideal_HasNoNoiseOrSmearAndKeepsTail","TimeBase_IsOriginRelativeAtLateAcquisitionTime","RateStudy_IsDeterministicAndLeavesAcquisitionUntouched","AcquiredChain_IsUsedAfterPendingSelectionChanges","Readout_SuppressesPartialOverlapAndSaturation","InvalidRequestsAndCancellation_FailWithoutPublishing","RetainedMonteCarloEvents_PreserveAssociationAndTime","MaximumWindow_ReportsWorkerCostAndBoundedPlotSamples","RetainedMonteCarlo_IsolatedPulseReadoutsAcrossParts","MixedFieldCalibration_UsesAcquiredChainAndInvalidatesRatios"],"sources":{"tests/Gcam.Studio.Services.Tests/WaveformServiceTests.cs":"48efc865b032f8ab17aab1f8bdb13b90714b72bfce340f8ee2f1072b98302257","tests/Gcam.Studio.Services.Tests/EvidenceFactAttribute.cs":"898301fdfe0438aa4ea3503809ee81dc90b225dff52defb3e2cee6277085cd60"},"trace":[]}
```


## AcquisitionViewModelTests — Gcam.Studio.Tests

Checks Start / Stop / Continue / Reset state transitions, physical-input locking and preset progress as snapshots arrive. It also checks failure handling, retained spectrum selection and new acquisition identity after Reset.

Oracle: The fake session publishes controlled snapshots/errors/completion. Expected locks, commands, preset progress, retained selection and reset identity are explicit state-machine fixtures; comparisons are exact/structural.

Tolerance: exact

Gates: none.

Source: [tests/Gcam.Studio.Tests/AcquisitionViewModelTests.cs](../tests/Gcam.Studio.Tests/AcquisitionViewModelTests.cs).

```json
{"id":"Gcam.Studio.Tests::Gcam.Studio.Tests.AcquisitionViewModelTests","runner":"dotnet","methods":["Start_CapturesDetectorInputs_AndLocksThemWhileDataExist","Start_SnapshotsGrow_StopKeepsDataAndLocks_ContinueAccumulates","Completed_StartDisabledUntilPresetRaised_LowerPresetRejected_ProgressFollowsPreset","Reset_DiscardsData_UnlocksInputs_NextStartIsANewAcquisitionWithANewSeed","Failure_WithData_KeepsItLocked_ResetOnly","Failure_WithoutData_BehavesAsEmpty","Spectrum_SnapshotsGrow_ViewSettingsReuseAcquisition_SelectionSurvives","LiveTimeAndSpeed_LockedWhileAcquiring_InvalidValuesFallBack"],"sources":{"tests/Gcam.Studio.Tests/AcquisitionViewModelTests.cs":"1252d3536bdfe38bd3b56c568619ef6c435cad6f2db52b1ce5e932827b0cc1bb"},"trace":[{"requirement":"SR-RUN-03","selectors":["Failure_WithData_KeepsItLocked_ResetOnly","Failure_WithoutData_BehavesAsEmpty"]},{"requirement":"SR-RUN-09","selectors":["Reset_DiscardsData_UnlocksInputs_NextStartIsANewAcquisitionWithANewSeed"]},{"requirement":"SR-RUN-10","selectors":["Start_SnapshotsGrow_StopKeepsDataAndLocks_ContinueAccumulates"]},{"requirement":"SR-RUN-11","selectors":["Completed_StartDisabledUntilPresetRaised_LowerPresetRejected_ProgressFollowsPreset"]},{"requirement":"SR-RUN-12","selectors":["LiveTimeAndSpeed_LockedWhileAcquiring_InvalidValuesFallBack","Start_CapturesDetectorInputs_AndLocksThemWhileDataExist"]},{"requirement":"SR-RUN-13","selectors":["Completed_StartDisabledUntilPresetRaised_LowerPresetRejected_ProgressFollowsPreset","Start_SnapshotsGrow_StopKeepsDataAndLocks_ContinueAccumulates"]},{"requirement":"SR-RUN-21","selectors":["Start_CapturesDetectorInputs_AndLocksThemWhileDataExist"]},{"requirement":"SR-RUN-24","selectors":["Completed_StartDisabledUntilPresetRaised_LowerPresetRejected_ProgressFollowsPreset","LiveTimeAndSpeed_LockedWhileAcquiring_InvalidValuesFallBack"]},{"requirement":"SR-RUN-25","selectors":["Reset_DiscardsData_UnlocksInputs_NextStartIsANewAcquisitionWithANewSeed"]},{"requirement":"SR-RUN-26","selectors":["Reset_DiscardsData_UnlocksInputs_NextStartIsANewAcquisitionWithANewSeed"]},{"requirement":"SR-RUN-27","selectors":["Failure_WithData_KeepsItLocked_ResetOnly","Failure_WithoutData_BehavesAsEmpty"]},{"requirement":"SR-RUN-28","selectors":["Start_SnapshotsGrow_StopKeepsDataAndLocks_ContinueAccumulates"]},{"requirement":"SR-NAV-02","selectors":["Spectrum_SnapshotsGrow_ViewSettingsReuseAcquisition_SelectionSurvives"]},{"requirement":"SR-NAV-03","selectors":["Start_CapturesDetectorInputs_AndLocksThemWhileDataExist"]},{"requirement":"SR-SPEC-06","selectors":["Spectrum_SnapshotsGrow_ViewSettingsReuseAcquisition_SelectionSurvives"]}]}
```


## AmbientViewModelTests — Gcam.Studio.Tests

Checks the validated ambient preset and dose-text policy, including the zero-dose legacy path and source-free Start rules. Invalid edits must preserve the previous value, and physical ambient settings must lock while data exist.

Oracle: The pinned validated front-only preset, explicit dose-text formats and fake start-captured settings supply exact fixture expectations.

Tolerance: Unsupported/non-finite edits and locks are structural; no sampled physics tolerance.

Gates: none.

Source: [tests/Gcam.Studio.Tests/AmbientViewModelTests.cs](../tests/Gcam.Studio.Tests/AmbientViewModelTests.cs).

```json
{"id":"Gcam.Studio.Tests::Gcam.Studio.Tests.AmbientViewModelTests","runner":"dotnet","methods":["DefaultIsValidatedFrontOnlyField_ZeroIsIdeal_SourceFreeStartRequiresAbsoluteField","PresetListOffersOnlyTheValidatedSpectrum_ByPinnedReference","Start_DefaultPassesValidatedPresetReference_ZeroTakesTheLegacyPath","AmbientPhysicalInputsLockAndDerivedBsrHasNoSetter","InvalidAmbientDoseIsRefused_PreviousValueStays","DoseTextOutsideThePattern_IsRefused_PreviousValueStays_StartBlocked","DoseTextMatchingThePattern_SetsTheDoseRate","DoseText_FollowsProgrammaticValue_AndLocksWithData"],"sources":{"tests/Gcam.Studio.Tests/AmbientViewModelTests.cs":"a0dcb85cb498df04b2cb935b7bad38e8ad73758da0e5b8cf47a8ae17ad06ac78"},"trace":[{"requirement":"SR-RUN-12","selectors":["AmbientPhysicalInputsLockAndDerivedBsrHasNoSetter"]},{"requirement":"SR-RUN-13","selectors":["DefaultIsValidatedFrontOnlyField_ZeroIsIdeal_SourceFreeStartRequiresAbsoluteField"]},{"requirement":"SR-RUN-29","selectors":["DefaultIsValidatedFrontOnlyField_ZeroIsIdeal_SourceFreeStartRequiresAbsoluteField","DoseTextMatchingThePattern_SetsTheDoseRate","DoseTextOutsideThePattern_IsRefused_PreviousValueStays_StartBlocked","DoseText_FollowsProgrammaticValue_AndLocksWithData","InvalidAmbientDoseIsRefused_PreviousValueStays","PresetListOffersOnlyTheValidatedSpectrum_ByPinnedReference","Start_DefaultPassesValidatedPresetReference_ZeroTakesTheLegacyPath"]}]}
```


## DetectorFaceTests — Gcam.Studio.Tests

Checks the crystal-and-reflector tiling of a detector face, including active area, edge half-gaps and absence of crystal-gap overlap. Invalid reflector gaps must be rejected.

Oracle: Active area ((pitch−gap)/pitch)², tiled areas and crystal/gap positions are geometric oracles.

Tolerance: Code labels 1e-12 edge arithmetic as roundoff; the fixed 12-decimal and absolute budgets have no operation-count derivation. Unexplained margins: tolerance stated without a derivation in the code.

Gates: none.

Source: [tests/Gcam.Studio.Tests/DetectorFaceTests.cs](../tests/Gcam.Studio.Tests/DetectorFaceTests.cs).

Method groups:

- `Geometry_ExactGapCoverageAndArea`: numerical comparisons described above; gate None.
- `Geometry_RejectsInvalidGap`: exact / structural fixture comparisons; gate None.

```json
{"id":"Gcam.Studio.Tests::Gcam.Studio.Tests.DetectorFaceTests","runner":"dotnet","methods":["Geometry_ExactGapCoverageAndArea","Geometry_RejectsInvalidGap"],"sources":{"tests/Gcam.Studio.Tests/DetectorFaceTests.cs":"2c47de9fb9f1561c285b4ca7ac5ac02af454e8ebca7df96a979c8a85919eb65f"},"trace":[]}
```


## DetectorWorkspaceTests — Gcam.Studio.Tests

Checks reflector-gap validation as pitch and units change, and whether edits can permit Start. The displayed detector face must follow pending settings before acquisition, acquired settings while data exist and pending settings again after Reset.

Oracle: Controlled pending/acquired settings and reflector-gap validation/unit conversion are exact fixtures. Start rejection, reset unlock and displayed face identity are structural.

Tolerance: exact

Gates: none.

Source: [tests/Gcam.Studio.Tests/DetectorWorkspaceTests.cs](../tests/Gcam.Studio.Tests/DetectorWorkspaceTests.cs).

```json
{"id":"Gcam.Studio.Tests::Gcam.Studio.Tests.DetectorWorkspaceTests","runner":"dotnet","methods":["Gap_InvalidEditPreventsStart","Gap_RevalidatesAfterPitchChange_AndConvertsUnits","Gap_ZeroAllowsPitchBelowOldDefaultGap","Face_FollowsPendingBeforeStart_AcquiredAndLockedAfter_PendingAgainAfterReset"],"sources":{"tests/Gcam.Studio.Tests/DetectorWorkspaceTests.cs":"5193220ce5bb0f6c5bb4e8c06916ce5b9ab7d6dc1119a8f8b7c47347234b4c36"},"trace":[{"requirement":"SR-RUN-26","selectors":["Face_FollowsPendingBeforeStart_AcquiredAndLockedAfter_PendingAgainAfterReset"]}]}
```


## FocusSweepMathTests — Gcam.Studio.Tests

Checks inverse-distance sweep planes and raw half-maximum interval interpolation, including censored edges and disjoint modes. Track linkage must use angle and preserve one-to-one associations.

Oracle: Uniform inverse-distance endpoints, synthetic half-height interpolation/censoring/modes and one-to-one angle linkage supply analytic fixtures.

Tolerance: Decimal geometry comparisons are tolerance stated without a derivation in the code.

Gates: none.

Source: [tests/Gcam.Studio.Tests/FocusSweepMathTests.cs](../tests/Gcam.Studio.Tests/FocusSweepMathTests.cs).

Method groups:

- `Planes_UniformInverseDistance_ExactBounds`: numerical comparisons described above; gate None.
- `Interval_InterpolatesRawHalfMaximum_WithoutNoiseInterpretation`, `Interval_CensorsSweepEdges`, `Interval_FlagsDisjointModes_AndBoundaryMaximum`, `Linking_UsesAngle_NotMillimetres_AndIsOneToOne`: exact / structural fixture comparisons; gate None.

```json
{"id":"Gcam.Studio.Tests::Gcam.Studio.Tests.FocusSweepMathTests","runner":"dotnet","methods":["Planes_UniformInverseDistance_ExactBounds","Interval_InterpolatesRawHalfMaximum_WithoutNoiseInterpretation","Interval_CensorsSweepEdges","Interval_FlagsDisjointModes_AndBoundaryMaximum","Linking_UsesAngle_NotMillimetres_AndIsOneToOne"],"sources":{"tests/Gcam.Studio.Tests/FocusSweepMathTests.cs":"caf85ab3095a01d04555ef73655f58509568dbf75c9229a9caf2e4a28416e3cc"},"trace":[]}
```


## FocusSweepViewModelTests — Gcam.Studio.Tests

Checks that asynchronous sweeps freeze acquisition identity and settings without delaying snapshot consumption, and reject results after identity changes. An external range must change focus only through an explicit action.

Oracle: Controlled pending sweep tasks and frozen identities establish cancellation/latest-result/state expectations. The code labels the deadline as deadlock protection, not physical/publisher-frequency accuracy. Exact/structural.

Tolerance: exact

Gates: none.

Source: [tests/Gcam.Studio.Tests/FocusSweepViewModelTests.cs](../tests/Gcam.Studio.Tests/FocusSweepViewModelTests.cs).

```json
{"id":"Gcam.Studio.Tests::Gcam.Studio.Tests.FocusSweepViewModelTests","runner":"dotnet","methods":["Sweep_DoesNotHoldSnapshotConsumption_AndLabelsFrozenPrefix","Sweep_FreezesIdentity_UserK_AndNeverDelaysAcquisition","Sweep_RejectsLateResult_WhenIdentityChanges","ExternalRange_OnlyExplicitActionChangesFocus"],"sources":{"tests/Gcam.Studio.Tests/FocusSweepViewModelTests.cs":"7d62866b2a437e4c6c5f2742a35d6902157dc26baa4f04dabd4adc7aa017b549"},"trace":[{"requirement":"SR-RUN-26","selectors":["Sweep_RejectsLateResult_WhenIdentityChanges"]}]}
```


## HeatmapViewportTests — Gcam.Studio.Tests

Checks aspect-preserving heatmap fit, screen/image coordinate conversion and pixel lookup with an upward image Y axis. Zoom must preserve its anchor, pan must retain visible image support, and resize or image-size changes must obey the documented fit policy.

Oracle: Known fit dimensions, round-trip/anchor coordinates, clipping and pixel-centre conventions are geometric oracles.

Tolerance: Six/nine-decimal equality margins are stated without a derived rounding-error budget; integer cells and zoom limits are exact/structural. Unexplained margins: tolerance stated without a derivation in the code.

Gates: none.

Source: [tests/Gcam.Studio.Tests/HeatmapViewportTests.cs](../tests/Gcam.Studio.Tests/HeatmapViewportTests.cs).

```json
{"id":"Gcam.Studio.Tests::Gcam.Studio.Tests.HeatmapViewportTests","runner":"dotnet","methods":["Fit_PreservesAspectAndCentres","ScreenImage_RoundTripWithYUp","PixelAt_RespectsBoundsAndOrientation","ZoomAt_KeepsAnchorPointFixed","ZoomAt_IsClamped_FullZoomOutRefits","PanBy_CannotDragImageOutOfView","MmMapping_PutsPixelCentresOnGrid","Snapping_FitsWholeDevicePixelsPerCell","Configure_ResizeKeepsZoom_NewImageSizeRefits"],"sources":{"tests/Gcam.Studio.Tests/HeatmapViewportTests.cs":"650c1de0a834c8a218780838156d7681129f2740c538a5b0cd037adf6b84906f"},"trace":[{"requirement":"SR-VIEW-01","selectors":["Fit_PreservesAspectAndCentres"]},{"requirement":"SR-VIEW-02","selectors":["Snapping_FitsWholeDevicePixelsPerCell"]},{"requirement":"SR-VIEW-03","selectors":["ZoomAt_IsClamped_FullZoomOutRefits","ZoomAt_KeepsAnchorPointFixed"]},{"requirement":"SR-VIEW-04","selectors":["PanBy_CannotDragImageOutOfView"]},{"requirement":"SR-VIEW-05","selectors":["MmMapping_PutsPixelCentresOnGrid","PixelAt_RespectsBoundsAndOrientation","ScreenImage_RoundTripWithYUp"]},{"requirement":"SR-VIEW-06","selectors":["Configure_ResizeKeepsZoom_NewImageSizeRefits"]}]}
```


## HistogramPlotTests — Gcam.Studio.Tests

Checks bin-step and column-envelope geometry, half-open bin lookup, visible extrema and collision-free label placement. It also checks zoom preservation, line endpoints and logarithmic sampling, and reports geometry cost for ten million samples.

Oracle: Bin geometry, independent extrema/boundary counts and layout exclusion are structural or exact references.

Tolerance: Timings are measurement only; decimal geometric margins are stated without an error-budget derivation. Unexplained margins: tolerance stated without a derivation in the code.

Gates: none.

Source: [tests/Gcam.Studio.Tests/HistogramPlotTests.cs](../tests/Gcam.Studio.Tests/HistogramPlotTests.cs).

Method groups:

- `Steps_IncludePartialBins_AndEmptyBinHasFullWidth`, `Envelope_RetainsExtremaOfBinsCrossingColumns`, `VisibleExtent_ExcludesDistantPeak_IncludesIntersectingBins`, `LineGeometry_PreservesSingletonsAndRightEndpoint`: numerical comparisons described above; gate None.
- `BinLookup_UsesHalfOpenEdges_AndReadoutUsesCountsAndUnits`, `HistogramValidation_RejectsMissingEdges_NegativeCounts_AndNonIncreasingEdges`, `Configure_PreservesZoomAndPanUntilRangeChanges_AndResetStillFits`, `Labels_ClampBothEdges_AndUseAdditionalRowsWithoutCollisions`, `LineGeometry_LogColumns_KeepNearFieldSamplesSeparate`, `GeometryTiming_TenMillionLineAndHistogram_Headless`: exact / structural fixture comparisons; gate None.

```json
{"id":"Gcam.Studio.Tests::Gcam.Studio.Tests.HistogramPlotTests","runner":"dotnet","methods":["Steps_IncludePartialBins_AndEmptyBinHasFullWidth","Envelope_RetainsExtremaOfBinsCrossingColumns","BinLookup_UsesHalfOpenEdges_AndReadoutUsesCountsAndUnits","HistogramValidation_RejectsMissingEdges_NegativeCounts_AndNonIncreasingEdges","VisibleExtent_ExcludesDistantPeak_IncludesIntersectingBins","Configure_PreservesZoomAndPanUntilRangeChanges_AndResetStillFits","Labels_ClampBothEdges_AndUseAdditionalRowsWithoutCollisions","LineGeometry_PreservesSingletonsAndRightEndpoint","LineGeometry_LogColumns_KeepNearFieldSamplesSeparate","GeometryTiming_TenMillionLineAndHistogram_Headless"],"sources":{"tests/Gcam.Studio.Tests/HistogramPlotTests.cs":"4e55ddd8f72bfe925b91dd9c4155d4802b0f43b9594ebcc6dabc8e14331f59b8"},"trace":[{"requirement":"SR-PLOT-06","selectors":["Envelope_RetainsExtremaOfBinsCrossingColumns","Steps_IncludePartialBins_AndEmptyBinHasFullWidth"]},{"requirement":"SR-PLOT-07","selectors":["Configure_PreservesZoomAndPanUntilRangeChanges_AndResetStillFits"]},{"requirement":"SR-PLOT-08","selectors":["VisibleExtent_ExcludesDistantPeak_IncludesIntersectingBins"]},{"requirement":"SR-PLOT-09","selectors":["BinLookup_UsesHalfOpenEdges_AndReadoutUsesCountsAndUnits"]},{"requirement":"SR-PLOT-10","selectors":["Labels_ClampBothEdges_AndUseAdditionalRowsWithoutCollisions"]}]}
```


## ImagingWorkspaceTests — Gcam.Studio.Tests

Checks that channel, window, stripping and reconstruction changes reuse frozen acquisition data and preserve measurements. Published labels, units and ROI values must describe the displayed result, while late worker results must not replace newer settings.

Oracle: Controlled responses/snapshots/late completions supply expected channel/strip/reconstruction labels, ROI units, measurement retention and stale-result rejection.

Tolerance: Exact/structural; no MC acceptance margin.

Gates: none.

Source: [tests/Gcam.Studio.Tests/ImagingWorkspaceTests.cs](../tests/Gcam.Studio.Tests/ImagingWorkspaceTests.cs).

```json
{"id":"Gcam.Studio.Tests::Gcam.Studio.Tests.ImagingWorkspaceTests","runner":"dotnet","methods":["FocusSweep_ChannelChangeCancelsAndRejectsLateResult","SharedWindow_SelectorStripAndRoi_ReuseFrozenAcquisition","PeakChip_CountsSeveralFoundPeaks_AndNamesASingleOne","LateResponse_CannotReplaceNewerWindowResult","Reconstruction_IsAReprojectionSetting_KeepsMeasurementsAndSweep","ReconstructionNote_FlagsUnmeasuredOptics_AndTheAcquisitionImageIsNotShownAsMlem","StripCount_IsLabelledNetWithUncertainty_ForBothMethods","ReconstructionUnit_DescribesTheDisplayedImage_UntilTheReDecodedViewArrives","StripMetadata_AndRoiUnits_FollowPublishedViewWhileStripIsPending","NegativeNetAndUnavailableUncertainty_KeepSweepEnabledAndDisplayedRoiValues"],"sources":{"tests/Gcam.Studio.Tests/ImagingWorkspaceTests.cs":"a22d122aba82d15a31f287c54a029cab179f024b9493406e54c033129493d917"},"trace":[{"requirement":"SR-IMG-01","selectors":["SharedWindow_SelectorStripAndRoi_ReuseFrozenAcquisition"]},{"requirement":"SR-IMG-03","selectors":["SharedWindow_SelectorStripAndRoi_ReuseFrozenAcquisition"]},{"requirement":"SR-IMG-06","selectors":["LateResponse_CannotReplaceNewerWindowResult"]},{"requirement":"SR-IMG-07","selectors":["ReconstructionNote_FlagsUnmeasuredOptics_AndTheAcquisitionImageIsNotShownAsMlem","Reconstruction_IsAReprojectionSetting_KeepsMeasurementsAndSweep","StripCount_IsLabelledNetWithUncertainty_ForBothMethods","StripMetadata_AndRoiUnits_FollowPublishedViewWhileStripIsPending"]},{"requirement":"SR-VIEW-08","selectors":["PeakChip_CountsSeveralFoundPeaks_AndNamesASingleOne"]}]}
```


## MainViewModelTests — Gcam.Studio.Tests

Checks workspace activation, source selection and editing, theme relabeling and retained shared results across snapshots. It also checks detector-input defaults and validation, measurement refresh and fixed-axis overflow summary text.

Oracle: Explicit defaults, fake acquisition results and selection/theme/edit actions establish the state/label/command oracle.

Tolerance: Exact fixtures and structural validation; no physical accuracy tolerance.

Gates: none.

Source: [tests/Gcam.Studio.Tests/MainViewModelTests.cs](../tests/Gcam.Studio.Tests/MainViewModelTests.cs).

```json
{"id":"Gcam.Studio.Tests::Gcam.Studio.Tests.MainViewModelTests","runner":"dotnet","methods":["WorkspaceActivation_SelectsShell_WhenActiveIsSetWithoutCommand","DetectorInputs_DefaultsAndValidationFallbacks_EditableWithoutData","Workspace_SharedResultAndSelectionSurviveSnapshot_ViewSettingsKeepState","ThemeToggle_FlipsThemeAndRelabels","Startup_HasOneSelectedSource","AddRemove_SelectsNewSourceThenNeighbour","RemoveAll_DisablesRemoveAndStart","SourceItem_ClampsValues_LabelFollowsEdits","NewResult_RefreshesMeasurements","IsotopePicker_OffersIr192_AndKeepsCs137AsTheDefault","SpectrumSummary_NamesTheOverflowAndTheFixedAxisEnd"],"sources":{"tests/Gcam.Studio.Tests/MainViewModelTests.cs":"b081f49b4f3558e24a67222254636795b4d372d853f37a7e4b7a3d0e9d1812f5"},"trace":[{"requirement":"SR-RUN-20","selectors":["DetectorInputs_DefaultsAndValidationFallbacks_EditableWithoutData"]},{"requirement":"SR-SCENE-01","selectors":["IsotopePicker_OffersIr192_AndKeepsCs137AsTheDefault","SourceItem_ClampsValues_LabelFollowsEdits"]},{"requirement":"SR-SCENE-02","selectors":["AddRemove_SelectsNewSourceThenNeighbour","Startup_HasOneSelectedSource"]},{"requirement":"SR-VIEW-08","selectors":["Workspace_SharedResultAndSelectionSurviveSnapshot_ViewSettingsKeepState"]},{"requirement":"SR-MEAS-04","selectors":["NewResult_RefreshesMeasurements"]},{"requirement":"SR-THEME-01","selectors":["ThemeToggle_FlipsThemeAndRelabels"]},{"requirement":"SR-NAV-01","selectors":["Workspace_SharedResultAndSelectionSurviveSnapshot_ViewSettingsKeepState"]},{"requirement":"SR-NAV-02","selectors":["WorkspaceActivation_SelectsShell_WhenActiveIsSetWithoutCommand"]},{"requirement":"SR-NAV-03","selectors":["Workspace_SharedResultAndSelectionSurviveSnapshot_ViewSettingsKeepState"]},{"requirement":"SR-SPEC-01","selectors":["SpectrumSummary_NamesTheOverflowAndTheFixedAxisEnd"]}]}
```


## MeasurementMathTests — Gcam.Studio.Tests

Checks Euclidean distance and vertex-angle calculations, including degenerate arms. ROI sums must include pixels by their centres, accept either corner order and clip to the image.

Oracle: Euclidean distance, vertex-angle and pixel-centre ROI sums are recomputed on synthetic fixtures.

Tolerance: Decimal geometric equality margins are tolerance stated without a derivation in the code; ROI counts/clip domains are exact.

Gates: none.

Source: [tests/Gcam.Studio.Tests/MeasurementMathTests.cs](../tests/Gcam.Studio.Tests/MeasurementMathTests.cs).

Method groups:

- `Distance_IsEuclidean`, `AngleDeg_MeasuresAtTheVertex`, `AngleDeg_DegenerateArm_IsZero`: numerical comparisons described above; gate None.
- `Roi_CountsPixelsByCentre_CornersInAnyOrder`, `Roi_ClipsToTheImage_AndIsEmptyBetweenCentres`: exact / structural fixture comparisons; gate None.

```json
{"id":"Gcam.Studio.Tests::Gcam.Studio.Tests.MeasurementMathTests","runner":"dotnet","methods":["Distance_IsEuclidean","AngleDeg_MeasuresAtTheVertex","AngleDeg_DegenerateArm_IsZero","Roi_CountsPixelsByCentre_CornersInAnyOrder","Roi_ClipsToTheImage_AndIsEmptyBetweenCentres"],"sources":{"tests/Gcam.Studio.Tests/MeasurementMathTests.cs":"b4a338ea8d8bde1227d2c3bb0d2c7a23c816f399d392a3c8c97866f1909fddbf"},"trace":[{"requirement":"SR-MEAS-01","selectors":["Distance_IsEuclidean"]},{"requirement":"VAL-03","selectors":["AngleDeg_DegenerateArm_IsZero","AngleDeg_MeasuresAtTheVertex","Distance_IsEuclidean","Roi_ClipsToTheImage_AndIsEmptyBetweenCentres","Roi_CountsPixelsByCentre_CornersInAnyOrder"]},{"requirement":"SR-MEAS-02","selectors":["AngleDeg_DegenerateArm_IsZero","AngleDeg_MeasuresAtTheVertex"]},{"requirement":"SR-MEAS-03","selectors":["Roi_ClipsToTheImage_AndIsEmptyBetweenCentres","Roi_CountsPixelsByCentre_CornersInAnyOrder"]}]}
```


## MeasurementsViewModelTests — Gcam.Studio.Tests

Checks measurement creation, sequential numbering, selection, deletion and tool hints. ROI values must follow the current pane image and remain unavailable without data or enclosed pixel centres.

Oracle: Explicit drawn points, synthetic pane images and selected rows define numbering, ROI availability/sums and deletion/clear/tool-hint expectations.

Tolerance: Structural/exact; decimal fixture equality, where coded, has no derived operation-count budget. Unexplained margins: tolerance stated without a derivation in the code.

Gates: none.

Source: [tests/Gcam.Studio.Tests/MeasurementsViewModelTests.cs](../tests/Gcam.Studio.Tests/MeasurementsViewModelTests.cs).

```json
{"id":"Gcam.Studio.Tests::Gcam.Studio.Tests.MeasurementsViewModelTests","runner":"dotnet","methods":["Add_NumbersSequentially_AndSelectsTheNewOne","Add_WrongNumberOfPoints_Throws","Roi_HasNoValueBeforeARun_AndFollowsEachNewResult","Roi_OnAPaneWithoutData_StaysEmpty","Roi_WithNoPixelCentreInside_HasNoValue","Delete_SelectsTheNeighbour_ClearRestartsNumbering","ToolHint_FollowsTheActiveTool"],"sources":{"tests/Gcam.Studio.Tests/MeasurementsViewModelTests.cs":"ec61490e5797c6ac5570288f9227593096aa0a36b9c20bde5c6707b193f43b88"},"trace":[{"requirement":"SR-MEAS-01","selectors":["Add_NumbersSequentially_AndSelectsTheNewOne"]},{"requirement":"VAL-03","selectors":["Add_NumbersSequentially_AndSelectsTheNewOne"]},{"requirement":"SR-MEAS-04","selectors":["Roi_HasNoValueBeforeARun_AndFollowsEachNewResult","Roi_OnAPaneWithoutData_StaysEmpty","Roi_WithNoPixelCentreInside_HasNoValue"]},{"requirement":"SR-MEAS-05","selectors":["Add_NumbersSequentially_AndSelectsTheNewOne","Add_WrongNumberOfPoints_Throws","Delete_SelectsTheNeighbour_ClearRestartsNumbering"]},{"requirement":"SR-MEAS-06","selectors":["ToolHint_FollowsTheActiveTool"]}]}
```


## MinMaxPyramidTests — Gcam.Studio.Tests

Checks that plot-envelope queries return the same extrema as a direct scan, including endpoints and block boundaries. It also checks clipped ranges, rejection of non-finite samples and the storage bound.

Oracle: Brute-force extrema over the same sample arrays and inclusive endpoints are independent algorithmic references.

Tolerance: Storage<=count/16 is a structural design bound, not approximate accuracy. Exact comparisons and invalid-sample refusal.

Gates: none.

Source: [tests/Gcam.Studio.Tests/MinMaxPyramidTests.cs](../tests/Gcam.Studio.Tests/MinMaxPyramidTests.cs).

Method groups:

- `Query_EqualsBruteForce_IncludingBothEnds`, `Range_EqualsBruteForce_AcrossBlockBoundaries`, `Storage_UsesAtMostOneSixteenthOfSampleCount`: exact / structural fixture comparisons; gate None.
- `Range_ClipsOutsideData_RejectsNonFiniteSamples`: numerical comparisons described above; gate None.

```json
{"id":"Gcam.Studio.Tests::Gcam.Studio.Tests.MinMaxPyramidTests","runner":"dotnet","methods":["Query_EqualsBruteForce_IncludingBothEnds","Range_EqualsBruteForce_AcrossBlockBoundaries","Storage_UsesAtMostOneSixteenthOfSampleCount","Range_ClipsOutsideData_RejectsNonFiniteSamples"],"sources":{"tests/Gcam.Studio.Tests/MinMaxPyramidTests.cs":"8f4bd2728c3a573d5973e02a25d05466d3d726762fedb6450fae668ab49ceecd"},"trace":[{"requirement":"SR-PLOT-03","selectors":["Query_EqualsBruteForce_IncludingBothEnds","Range_ClipsOutsideData_RejectsNonFiniteSamples"]}]}
```


## NiceTicksTests — Gcam.Studio.Tests

Checks linear 1/2/5 steps and logarithmic decade/minor ticks, including a linear fallback for narrow log ranges. Labels must use the expected engineering or grouped-number notation, avoid negative zero and remain within the data range.

Oracle: Explicit 1/2/5 steps, decade/minor tick sets and formatted labels are fixture oracles.

Tolerance: Tick membership is structural; decimal step comparisons retain their stated precision without a rounding-error derivation. Unexplained margins: tolerance stated without a derivation in the code.

Gates: none.

Source: [tests/Gcam.Studio.Tests/NiceTicksTests.cs](../tests/Gcam.Studio.Tests/NiceTicksTests.cs).

Method groups:

- `Linear_Uses125Steps_EngineeringLabels_NoNegativeZero`, `Logarithmic_LabelsDecades_WithEightMinorTicks`, `LogarithmicAxis_Labels125_InsideRange_FallsBackToLinearWhenNarrow`, `Logarithmic_UsesGroupedNumbersThenSuperscriptPowers`: exact / structural fixture comparisons; gate None.
- `Linear_ColourScaleTicksStayInsideDataRange`: numerical comparisons described above; gate None.

```json
{"id":"Gcam.Studio.Tests::Gcam.Studio.Tests.NiceTicksTests","runner":"dotnet","methods":["Linear_Uses125Steps_EngineeringLabels_NoNegativeZero","Logarithmic_LabelsDecades_WithEightMinorTicks","LogarithmicAxis_Labels125_InsideRange_FallsBackToLinearWhenNarrow","Logarithmic_UsesGroupedNumbersThenSuperscriptPowers","Linear_ColourScaleTicksStayInsideDataRange"],"sources":{"tests/Gcam.Studio.Tests/NiceTicksTests.cs":"8db43ab0de49c2fc12a7aa4a3cbe8f024c6013c254cd04a5b214d4805c680ff2"},"trace":[{"requirement":"SR-PLOT-02","selectors":["Linear_Uses125Steps_EngineeringLabels_NoNegativeZero","Logarithmic_LabelsDecades_WithEightMinorTicks"]}]}
```


## OpticsPolicyTests — Gcam.Studio.Tests

Checks atomic application of optics presets and geometry derived from effective input fields. Invalid text, unsupported pixel counts and invalid rank, gap, source, focus or overflow values must remain ineffective and prevent acquisition.

Oracle: Preset field tuples, geometry formulas and invalid effective-text/intensity/rank/gap/overflow inputs provide the oracle.

Tolerance: Exact/structural plus coded rounded geometry equality; those decimal margins have no derived error budget. Unexplained margins: tolerance stated without a derivation in the code.

Gates: none.

Source: [tests/Gcam.Studio.Tests/OpticsPolicyTests.cs](../tests/Gcam.Studio.Tests/OpticsPolicyTests.cs).

Method groups:

- `Presets_ApplyAtomically_MatchPhysicalFieldsAndShowCustom`, `InvalidText_IsRetainedAndCannotBecomeEffective`, `PixelCount_RequiresSupportedInteger`, `Validation_ChecksRankGapSourceFaceFocusAndOverflow`: exact / structural fixture comparisons; gate None.
- `Geometry_UsesEffectiveFieldsAndHasNoPerformanceBand`: numerical comparisons described above; gate None.

```json
{"id":"Gcam.Studio.Tests::Gcam.Studio.Tests.OpticsPolicyTests","runner":"dotnet","methods":["Presets_ApplyAtomically_MatchPhysicalFieldsAndShowCustom","Geometry_UsesEffectiveFieldsAndHasNoPerformanceBand","InvalidText_IsRetainedAndCannotBecomeEffective","PixelCount_RequiresSupportedInteger","Validation_ChecksRankGapSourceFaceFocusAndOverflow"],"sources":{"tests/Gcam.Studio.Tests/OpticsPolicyTests.cs":"a23a5774589813016b0e0c47f38b53a30453c6312a6b2310eaf82c276383f7eb"},"trace":[{"requirement":"SR-OPT-01","selectors":["Presets_ApplyAtomically_MatchPhysicalFieldsAndShowCustom"]},{"requirement":"SR-OPT-02","selectors":["Presets_ApplyAtomically_MatchPhysicalFieldsAndShowCustom"]},{"requirement":"SR-OPT-03","selectors":["Geometry_UsesEffectiveFieldsAndHasNoPerformanceBand","InvalidText_IsRetainedAndCannotBecomeEffective","PixelCount_RequiresSupportedInteger","Presets_ApplyAtomically_MatchPhysicalFieldsAndShowCustom","Validation_ChecksRankGapSourceFaceFocusAndOverflow"]},{"requirement":"SR-OPT-05","selectors":["Geometry_UsesEffectiveFieldsAndHasNoPerformanceBand"]}]}
```


## OpticsViewModelTests — Gcam.Studio.Tests

Checks that focus changes preserve acquired events and spectrum while physical optics remain locked with data. Projection requests must coalesce, reject stale workers across acquisition identities and refuse Start when inputs are invalid.

Oracle: Fake session/projection workers and controlled completion order supply the oracle for pending/acquired locks, event preservation and latest-focus identity. Exact/structural; timeouts protect completion, not imaging accuracy.

Tolerance: exact

Gates: none.

Source: [tests/Gcam.Studio.Tests/OpticsViewModelTests.cs](../tests/Gcam.Studio.Tests/OpticsViewModelTests.cs).

```json
{"id":"Gcam.Studio.Tests::Gcam.Studio.Tests.OpticsViewModelTests","runner":"dotnet","methods":["Focus_AcquiringStoppedCompleted_KeepsEventsAndSpectrum_OpticsLockedWithData","LatestFocus_CoalescesChangesAndRejectsLateSnapshotProjection","OldAcquisitionWorker_CannotPublishIntoNewAcquisition","InvalidInput_StartExpandsSectionsAndDoesNotStartTransport"],"sources":{"tests/Gcam.Studio.Tests/OpticsViewModelTests.cs":"450a9a070e53e3bf35f7abd43e4037ea2d96f9022e6dd10e4624d01a87408fdb"},"trace":[{"requirement":"SR-OPT-01","selectors":["Focus_AcquiringStoppedCompleted_KeepsEventsAndSpectrum_OpticsLockedWithData"]},{"requirement":"SR-OPT-02","selectors":["Focus_AcquiringStoppedCompleted_KeepsEventsAndSpectrum_OpticsLockedWithData"]},{"requirement":"SR-OPT-03","selectors":["InvalidInput_StartExpandsSectionsAndDoesNotStartTransport"]},{"requirement":"SR-OPT-04","selectors":["Focus_AcquiringStoppedCompleted_KeepsEventsAndSpectrum_OpticsLockedWithData","InvalidInput_StartExpandsSectionsAndDoesNotStartTransport","LatestFocus_CoalescesChangesAndRejectsLateSnapshotProjection","OldAcquisitionWorker_CannotPublishIntoNewAcquisition"]},{"requirement":"SR-RUN-23","selectors":["Focus_AcquiringStoppedCompleted_KeepsEventsAndSpectrum_OpticsLockedWithData"]}]}
```


## OverlayLabelLayoutTests — Gcam.Studio.Tests

Checks that overlay labels retain preferred positions when possible and otherwise avoid one another, truth markers and measurements within pane bounds. Labels must be omitted when no space exists, and non-finite geometry must be rejected.

Oracle: Preferred positions and explicitly occupied rectangles test containment, separation and omission.

Tolerance: Exact/structural geometry; any decimal coordinate margin is stated without a derivation in the code. Unexplained margins: tolerance stated without a derivation in the code.

Gates: none.

Source: [tests/Gcam.Studio.Tests/OverlayLabelLayoutTests.cs](../tests/Gcam.Studio.Tests/OverlayLabelLayoutTests.cs).

Method groups:

- `Arrange_PreservesUncrowdedPreferredPositions`, `Arrange_ClampsEdgeLabel_AndRecalculatesForChangedBounds`, `Arrange_OmitsChip_WhenBoundsAreTooSmallOrFullyOccupied`, `Arrange_RejectsNonFiniteGeometry`: exact / structural fixture comparisons; gate None.
- `Arrange_SeparatesEqualYLanes_AndAvoidsTruthMarkersAndMeasurement`: numerical comparisons described above; gate None.

```json
{"id":"Gcam.Studio.Tests::Gcam.Studio.Tests.OverlayLabelLayoutTests","runner":"dotnet","methods":["Arrange_PreservesUncrowdedPreferredPositions","Arrange_SeparatesEqualYLanes_AndAvoidsTruthMarkersAndMeasurement","Arrange_ClampsEdgeLabel_AndRecalculatesForChangedBounds","Arrange_OmitsChip_WhenBoundsAreTooSmallOrFullyOccupied","Arrange_RejectsNonFiniteGeometry"],"sources":{"tests/Gcam.Studio.Tests/OverlayLabelLayoutTests.cs":"e0918ad8cfd5c1f207a50d3cccae64acb0f3433c7e8ced3db2fe2cc2e233ac22"},"trace":[{"requirement":"SR-IMG-04","selectors":["Arrange_ClampsEdgeLabel_AndRecalculatesForChangedBounds","Arrange_OmitsChip_WhenBoundsAreTooSmallOrFullyOccupied","Arrange_PreservesUncrowdedPreferredPositions","Arrange_RejectsNonFiniteGeometry","Arrange_SeparatesEqualYLanes_AndAvoidsTruthMarkersAndMeasurement"]}]}
```


## PlotSeriesTests — Gcam.Studio.Tests

Checks lower-bound lookup on uniformly spaced and explicit irregular X coordinates, including queries outside the series. Explicit X arrays must match the sample count and increase strictly.

Oracle: Explicit uniform/irregular X arrays, query positions and endpoint indices supply exact lower-bound fixtures. No approximate numerical tolerance.

Tolerance: exact

Gates: none.

Source: [tests/Gcam.Studio.Tests/PlotSeriesTests.cs](../tests/Gcam.Studio.Tests/PlotSeriesTests.cs).

```json
{"id":"Gcam.Studio.Tests::Gcam.Studio.Tests.PlotSeriesTests","runner":"dotnet","methods":["LowerBound_HandlesUniformAndIrregularX_AndEnds"],"sources":{"tests/Gcam.Studio.Tests/PlotSeriesTests.cs":"688fdb0a85e881df40c771460b340cab16ab9ee22d690f769dcd9609766a10bf"},"trace":[{"requirement":"SR-PLOT-01","selectors":["LowerBound_HandlesUniformAndIrregularX_AndEnds"]}]}
```


## PlotViewportTests — Gcam.Studio.Tests

Checks linear and logarithmic plot coordinate mappings and their inverse, including equal screen spacing for decades. Zoom must preserve its anchor, pan must use the appropriate axis space and Reset must restore bounded extents.

Oracle: Analytic linear/log mappings, equal decade distances, coordinate round trips and anchor-preserving zoom/pan are geometric references.

Tolerance: Decimal comparison margins are tolerance stated without a derivation in the code.

Gates: none.

Source: [tests/Gcam.Studio.Tests/PlotViewportTests.cs](../tests/Gcam.Studio.Tests/PlotViewportTests.cs).

```json
{"id":"Gcam.Studio.Tests::Gcam.Studio.Tests.PlotViewportTests","runner":"dotnet","methods":["Mapping_RoundTrips_AndClampsLogFloor","LogX_MapsDecadesEqually_RoundTrips_AndZoomsPansInLogSpace","PanFraction_OnLinearAxis_EqualsDataDeltaPan","ZoomPanReset_PreservesAnchor_AndBounds"],"sources":{"tests/Gcam.Studio.Tests/PlotViewportTests.cs":"b5dba8165ef731924b37a82da36de761fe9e6088118a5a926d9489de91dcd24f"},"trace":[{"requirement":"SR-PLOT-02","selectors":["Mapping_RoundTrips_AndClampsLogFloor"]},{"requirement":"SR-PLOT-04","selectors":["ZoomPanReset_PreservesAnchor_AndBounds"]}]}
```


## SpectrumBandTests — Gcam.Studio.Tests

Checks that X-ray bands are named by their emitting element and parent isotope, while gamma bands retain the isotope name. Descriptions must preserve the line energies supplied by the engine isotope table.

Oracle: Explicit X-ray/gamma line kinds and isotope/emitter names define exact expected band labels. No numerical tolerance.

Tolerance: exact

Gates: none.

Source: [tests/Gcam.Studio.Tests/SpectrumBandTests.cs](../tests/Gcam.Studio.Tests/SpectrumBandTests.cs).

```json
{"id":"Gcam.Studio.Tests::Gcam.Studio.Tests.SpectrumBandTests","runner":"dotnet","methods":["BandOfXRayLines_IsNamedByEmitter_GammaBandByIsotope"],"sources":{"tests/Gcam.Studio.Tests/SpectrumBandTests.cs":"d43b24d241c018ace487605d10fc7478a41cdab9282a7afa394192e1213efeea"},"trace":[{"requirement":"SR-SPEC-05","selectors":["BandOfXRayLines_IsNamedByEmitter_GammaBandByIsotope"]}]}
```


## ThemeContrastTests — Gcam.Studio.Tests

Checks that disabled retained-value text meets the stated contrast target against each theme background. It must also remain dimmer than enabled text.

Oracle: Independent sRGB relative luminance/contrast on resource tokens checks the product target 4.5:1, explicitly stated in code; dimmer-than-enabled is an ordering check.

Tolerance: This is a target, not a statistical tolerance.

Gates: none.

Source: [tests/Gcam.Studio.Tests/ThemeContrastTests.cs](../tests/Gcam.Studio.Tests/ThemeContrastTests.cs).

Method groups:

- `DisabledText_MeetsRetainedValueContrastTarget`: numerical comparisons described above; gate None.
- `DisabledText_RemainsDimmerThanEnabledText`: exact / structural fixture comparisons; gate None.

```json
{"id":"Gcam.Studio.Tests::Gcam.Studio.Tests.ThemeContrastTests","runner":"dotnet","methods":["DisabledText_MeetsRetainedValueContrastTarget","DisabledText_RemainsDimmerThanEnabledText"],"sources":{"tests/Gcam.Studio.Tests/ThemeContrastTests.cs":"85c1942c22d4a4d42ef81d02e12d7f59980acd98af446730b3f485966da02975"},"trace":[]}
```


## TickFormatterTests — Gcam.Studio.Tests

Checks shared engineering exponents, decimal counts and grouped numeric labels across small, ordinary and flat ranges. Formatting must preserve significant digits and grouped fractional resolution without trailing zeros or negative zero.

Oracle: Explicit exponent/decimal/significant/grouped-label strings and expected engineering exponent integers are exact formatting fixtures. No approximate numerical acceptance.

Tolerance: exact

Gates: none.

Source: [tests/Gcam.Studio.Tests/TickFormatterTests.cs](../tests/Gcam.Studio.Tests/TickFormatterTests.cs).

Method groups:

- `SmallValues_ShareOneMultiplier_AndOneDecimalCount`, `OrdinaryRange_HasNoMultiplier_AndNoNegativeZero`, `SharedExponent_IsZeroForReadableValues_OtherwiseAMultipleOfThree`, `Significant_FourDigitsGroupedWithoutTrailingZeros`: exact / structural fixture comparisons; gate None.
- `FlatImage_GivesZeroDecimals_WithoutThrowing`, `StepLabels_TakeDecimalsFromTheStep_AndGroupThousands`, `GroupedFraction_KeepsResolution_InGroupsOfThree`: numerical comparisons described above; gate None.

```json
{"id":"Gcam.Studio.Tests::Gcam.Studio.Tests.TickFormatterTests","runner":"dotnet","methods":["SmallValues_ShareOneMultiplier_AndOneDecimalCount","OrdinaryRange_HasNoMultiplier_AndNoNegativeZero","SharedExponent_IsZeroForReadableValues_OtherwiseAMultipleOfThree","FlatImage_GivesZeroDecimals_WithoutThrowing","StepLabels_TakeDecimalsFromTheStep_AndGroupThousands","Significant_FourDigitsGroupedWithoutTrailingZeros","GroupedFraction_KeepsResolution_InGroupsOfThree"],"sources":{"tests/Gcam.Studio.Tests/TickFormatterTests.cs":"f367d147f98c416dee8d81a359251bf1506f00c33baec0a8dd13f1af407dfd7f"},"trace":[{"requirement":"SR-VIEW-07","selectors":["Significant_FourDigitsGroupedWithoutTrailingZeros"]},{"requirement":"SR-PLOT-02","selectors":["StepLabels_TakeDecimalsFromTheStep_AndGroupThousands"]}]}
```


## WaveformWorkspaceTests — Gcam.Studio.Tests

Checks waveform-window limits, relative time conversion and compatibility of prepared plot data with its extrema pyramid. Acquired chain locks, hidden/held scope behavior and asynchronous selection changes must preserve the acquisition and reject stale results.

Oracle: Fake acquired chain/events and controlled async completion establish locks/latest-result/selection/error expectations.

Tolerance: Warmup+samples<=MaximumSamples and overflow/time conversion are structural/exact, not timing tolerances.

Gates: none.

Source: [tests/Gcam.Studio.Tests/WaveformWorkspaceTests.cs](../tests/Gcam.Studio.Tests/WaveformWorkspaceTests.cs).

Method groups:

- `CsI_IsOfferedAndRequiresResetForANewAcquisition`, `Window_CapsTotalWorkAndKeepsPretrigger`, `Window_RejectsInvalidLength`, `PreparedPlot_RejectsAnotherArraysPyramid`, `Chain_LockedWithData_ScopeUsesAcquiredChain`, `ActiveAcquisition_DisablesPhysicalChainChanges`, `HiddenScope_DoesNotGenerateAndHeldTriggerReusesWindow`, `LatestSelectionWinsAndNewAcquisitionCancelsOldScope`, `ScopeFailure_IsVisibleAndLocalControlsKeepTheAcquisition`: exact / structural fixture comparisons; gate None.
- `TimeConversion_IsCheckedAndRelative`: numerical comparisons described above; gate None.

```json
{"id":"Gcam.Studio.Tests::Gcam.Studio.Tests.WaveformWorkspaceTests","runner":"dotnet","methods":["CsI_IsOfferedAndRequiresResetForANewAcquisition","Window_CapsTotalWorkAndKeepsPretrigger","Window_RejectsInvalidLength","TimeConversion_IsCheckedAndRelative","PreparedPlot_RejectsAnotherArraysPyramid","Chain_LockedWithData_ScopeUsesAcquiredChain","ActiveAcquisition_DisablesPhysicalChainChanges","HiddenScope_DoesNotGenerateAndHeldTriggerReusesWindow","LatestSelectionWinsAndNewAcquisitionCancelsOldScope","ScopeFailure_IsVisibleAndLocalControlsKeepTheAcquisition"],"sources":{"tests/Gcam.Studio.Tests/WaveformWorkspaceTests.cs":"873d34745054484745b73741fd62acf863511d373b28fe3b67f51376a8d04f56"},"trace":[]}
```


## FloodOracleTests — Gcam.Studio.UiTests

The pilot's oracle and verdict are test code too — these run everywhere, no desktop needed.

Oracle: Known view/image coordinates, Euclidean/vertex geometry, whole-pixel centre counts and parser fixtures are the oracles.

Tolerance: The /5 long-arm comparison is geometric scaling, not sampled accuracy; selected decimal margins have no rounding-budget derivation. Unexplained margins: tolerance stated without a derivation in the code.

Gates: none.

Source: [tests/Gcam.Studio.UiTests/FloodOracleTests.cs](../tests/Gcam.Studio.UiTests/FloodOracleTests.cs).

Method groups:

- `ImageBounds_UseWholePixelsPerCell_Centred`, `ScreenToMm_MapsImageCornersToDetectorEdges_YUp`, `DistanceMm_FullDiagonal_IsSpanTimesRootTwo`, `Verdict_RejectsAValueOutsideTheTolerance`, `PixelsInside_CountsCentres_RobustToAPixelAtTheCorners`, `AngleDeg_IsTheVertexAngle_ToleranceShrinksWithLongerArms`, `Parsers_ReadPeakAndRoiDetail_RejectOtherText`, `CellCentreMm_IsAbsolute_WithYUp`, `ReadoutAndSumParsers_ReadTheAppsFormats`, `MeasurementRow_ParsesTheAutomationName`: numerical comparisons described above; gate None.
- `ReadoutUnit_NamesTheImageKind_IncludingMlem`, `NetCountParser_ReadsSignedUncertainty_AndRejectsMissingOrWrongUnits`: exact / structural fixture comparisons; gate None.

```json
{"id":"Gcam.Studio.UiTests::Gcam.Studio.UiTests.FloodOracleTests","runner":"dotnet","methods":["ImageBounds_UseWholePixelsPerCell_Centred","ScreenToMm_MapsImageCornersToDetectorEdges_YUp","DistanceMm_FullDiagonal_IsSpanTimesRootTwo","Verdict_RejectsAValueOutsideTheTolerance","PixelsInside_CountsCentres_RobustToAPixelAtTheCorners","AngleDeg_IsTheVertexAngle_ToleranceShrinksWithLongerArms","Parsers_ReadPeakAndRoiDetail_RejectOtherText","CellCentreMm_IsAbsolute_WithYUp","ReadoutAndSumParsers_ReadTheAppsFormats","ReadoutUnit_NamesTheImageKind_IncludingMlem","NetCountParser_ReadsSignedUncertainty_AndRejectsMissingOrWrongUnits","MeasurementRow_ParsesTheAutomationName"],"sources":{"tests/Gcam.Studio.UiTests/FloodOracleTests.cs":"adb568f7478411b222f31808f6904afe2cc68fa8356de5d72f0f9a1e7c72d20e","tests/Gcam.Studio.UiTests/Harness/FloodOracle.cs":"22db35cb2771a0460e437e47308b5792bf1c22d2dc077af54106223a29cd75be","tests/Gcam.Studio.UiTests/Harness/Pointer.cs":"e56d09c72e552bf5619c088150a0d6ceba0ec2eb75ee06c2744c4677b9db896c","tests/Gcam.Studio.UiTests/Harness/RepoPaths.cs":"2bdc3c25db3c74cfe32ea7f64352f36cc5fb3b8fbf7be92ce03b7184648cc655","tests/Gcam.Studio.UiTests/Harness/RunRecord.cs":"0db3b6cf367fed06ba506db2973593b100ebed4a6ca793552a77e490d0fce377","tests/Gcam.Studio.UiTests/Harness/Scenario.cs":"dac922df393236562f138489ab7a54d4ff09bb4003f7694da421564e0647b085","tests/Gcam.Studio.UiTests/Harness/StudioProcess.cs":"973777303020b10c38bcd701e991764b095cb2f149ed31da6a5d554bf73d87c2","tests/Gcam.Studio.UiTests/Harness/StudioWindow.cs":"59fca517024d117593d9056ed1c5122d2b8632b7ae179b36a8e091aea06b5bf8","tests/Gcam.Studio.UiTests/Harness/WorkspaceOracle.cs":"153e5f5ec778401b877467e405295a940d663d4fea1d32f9101139e9c0b325e5","tests/Gcam.Studio.UiTests/DesktopFactAttribute.cs":"1dcac8c38143a4d38635267e17af2acdbb5f2ee84eca07e2968a2a1e7f073bfc"},"trace":[]}
```


## MlemScenarioTests — Gcam.Studio.UiTests

TODO-36's desktop checks as scenarios (TODO-38 / DC-3; SR-IMG-07).

Oracle: Specification items/unit/400 iterations and independently recomputed retained counts/floods/variance provide the oracle.

Tolerance: Non-negativity is exact; roundoff and count formatting are described in code. Cs side is justified by the quoted measured default-geometry success probability, not a universal localization tolerance.

Gates: `GCAM_UI_TESTS`.

Source: [tests/Gcam.Studio.UiTests/MlemScenarioTests.cs](../tests/Gcam.Studio.UiTests/MlemScenarioTests.cs).

Method groups:

- `Mlem_SelectedDuringAcquisition_NonNegativeWithUnitNoteAndAdvancingStatus`, `Mlem_FocalPlane1500_FlagsTheUnmeasuredIterationCount`, `MethodSwitch_KeepsMeasurementsOnTheSameGrid`, `MlemSelection_SurvivesStopContinueAndReset_WithoutAStaleImage`: exact / structural fixture comparisons; gate GCAM_UI_TESTS.
- `Mlem_WithStrip_ReportsNetCountsAndFindsCsOnItsSide`, `FocusSweep_UnderMlem_IsTheCrossCorrelationSweep`: numerical comparisons described above; gate GCAM_UI_TESTS.

```json
{"id":"Gcam.Studio.UiTests::Gcam.Studio.UiTests.MlemScenarioTests","runner":"dotnet","methods":["Mlem_SelectedDuringAcquisition_NonNegativeWithUnitNoteAndAdvancingStatus","Mlem_WithStrip_ReportsNetCountsAndFindsCsOnItsSide","Mlem_FocalPlane1500_FlagsTheUnmeasuredIterationCount","MethodSwitch_KeepsMeasurementsOnTheSameGrid","FocusSweep_UnderMlem_IsTheCrossCorrelationSweep","MlemSelection_SurvivesStopContinueAndReset_WithoutAStaleImage"],"sources":{"tests/Gcam.Studio.UiTests/MlemScenarioTests.cs":"3c2c52587a2f71b0e7d72cb3bb74a4a28bba18a2f97852f65df66cced7786c28","tests/Gcam.Studio.UiTests/Harness/FloodOracle.cs":"22db35cb2771a0460e437e47308b5792bf1c22d2dc077af54106223a29cd75be","tests/Gcam.Studio.UiTests/Harness/Pointer.cs":"e56d09c72e552bf5619c088150a0d6ceba0ec2eb75ee06c2744c4677b9db896c","tests/Gcam.Studio.UiTests/Harness/RepoPaths.cs":"2bdc3c25db3c74cfe32ea7f64352f36cc5fb3b8fbf7be92ce03b7184648cc655","tests/Gcam.Studio.UiTests/Harness/RunRecord.cs":"0db3b6cf367fed06ba506db2973593b100ebed4a6ca793552a77e490d0fce377","tests/Gcam.Studio.UiTests/Harness/Scenario.cs":"dac922df393236562f138489ab7a54d4ff09bb4003f7694da421564e0647b085","tests/Gcam.Studio.UiTests/Harness/StudioProcess.cs":"973777303020b10c38bcd701e991764b095cb2f149ed31da6a5d554bf73d87c2","tests/Gcam.Studio.UiTests/Harness/StudioWindow.cs":"59fca517024d117593d9056ed1c5122d2b8632b7ae179b36a8e091aea06b5bf8","tests/Gcam.Studio.UiTests/Harness/WorkspaceOracle.cs":"153e5f5ec778401b877467e405295a940d663d4fea1d32f9101139e9c0b325e5","tests/Gcam.Studio.UiTests/DesktopFactAttribute.cs":"1dcac8c38143a4d38635267e17af2acdbb5f2ee84eca07e2968a2a1e7f073bfc"},"trace":[]}
```


## PilotTests — Gcam.Studio.UiTests

The pilot flow: simulate → pick the distance tool → drag on the flood map → judge the product's result.

Oracle: The expected drag length comes from FloodOracle image coordinates and Verdict geometry; acceptance uses its pixel/rounding tolerance. Test success requires the product-created row and value, not merely successful automation.

Tolerance: structural

Gates: `GCAM_UI_TESTS`.

Source: [tests/Gcam.Studio.UiTests/PilotTests.cs](../tests/Gcam.Studio.UiTests/PilotTests.cs).

```json
{"id":"Gcam.Studio.UiTests::Gcam.Studio.UiTests.PilotTests","runner":"dotnet","methods":["Pilot_SimulateThenMeasureDistance_AddsRowWithExpectedLength"],"sources":{"tests/Gcam.Studio.UiTests/PilotTests.cs":"404762bb49012f40547f7405f6ab4b5bd448401629be1045152d755146076b1c","tests/Gcam.Studio.UiTests/Harness/FloodOracle.cs":"22db35cb2771a0460e437e47308b5792bf1c22d2dc077af54106223a29cd75be","tests/Gcam.Studio.UiTests/Harness/Pointer.cs":"e56d09c72e552bf5619c088150a0d6ceba0ec2eb75ee06c2744c4677b9db896c","tests/Gcam.Studio.UiTests/Harness/RepoPaths.cs":"2bdc3c25db3c74cfe32ea7f64352f36cc5fb3b8fbf7be92ce03b7184648cc655","tests/Gcam.Studio.UiTests/Harness/RunRecord.cs":"0db3b6cf367fed06ba506db2973593b100ebed4a6ca793552a77e490d0fce377","tests/Gcam.Studio.UiTests/Harness/Scenario.cs":"dac922df393236562f138489ab7a54d4ff09bb4003f7694da421564e0647b085","tests/Gcam.Studio.UiTests/Harness/StudioProcess.cs":"973777303020b10c38bcd701e991764b095cb2f149ed31da6a5d554bf73d87c2","tests/Gcam.Studio.UiTests/Harness/StudioWindow.cs":"59fca517024d117593d9056ed1c5122d2b8632b7ae179b36a8e091aea06b5bf8","tests/Gcam.Studio.UiTests/Harness/WorkspaceOracle.cs":"153e5f5ec778401b877467e405295a940d663d4fea1d32f9101139e9c0b325e5","tests/Gcam.Studio.UiTests/DesktopFactAttribute.cs":"1dcac8c38143a4d38635267e17af2acdbb5f2ee84eca07e2968a2a1e7f073bfc"},"trace":[{"requirement":"SR-MEAS-01","selectors":["Pilot_SimulateThenMeasureDistance_AddsRowWithExpectedLength"]},{"requirement":"SR-RUN-09","selectors":["Pilot_SimulateThenMeasureDistance_AddsRowWithExpectedLength"]},{"requirement":"VAL-01","selectors":["Pilot_SimulateThenMeasureDistance_AddsRowWithExpectedLength"]},{"requirement":"VAL-03","selectors":["Pilot_SimulateThenMeasureDistance_AddsRowWithExpectedLength"]}]}
```


## PlotViewTests — Gcam.Studio.UiTests

Measures production plot redraws in a visible WPF host after warmup. Every zoom and resize sample for a ten-million-sample series must meet the stated redraw-time target.

Oracle: Product redraw timing is checked against the stated <=16 ms target for every zoom/resize sample.

Tolerance: The target is a performance requirement, not a derived timing uncertainty; redraw/window deadlines are operational. Unexplained margins: tolerance stated without a derivation in the code.

Gates: `GCAM_UI_TESTS`.

Source: [tests/Gcam.Studio.UiTests/PlotViewTests.cs](../tests/Gcam.Studio.UiTests/PlotViewTests.cs).

```json
{"id":"Gcam.Studio.UiTests::Gcam.Studio.UiTests.PlotViewTests","runner":"dotnet","methods":["TenMillionSamples_ZoomAndResizeRedraw_Within16Milliseconds"],"sources":{"tests/Gcam.Studio.UiTests/PlotViewTests.cs":"c6304d378ffff12fc12ba8dd84649231b085b105a7ef12a810ec3ea40a63d36c","tests/Gcam.Studio.UiTests/Harness/FloodOracle.cs":"22db35cb2771a0460e437e47308b5792bf1c22d2dc077af54106223a29cd75be","tests/Gcam.Studio.UiTests/Harness/Pointer.cs":"e56d09c72e552bf5619c088150a0d6ceba0ec2eb75ee06c2744c4677b9db896c","tests/Gcam.Studio.UiTests/Harness/RepoPaths.cs":"2bdc3c25db3c74cfe32ea7f64352f36cc5fb3b8fbf7be92ce03b7184648cc655","tests/Gcam.Studio.UiTests/Harness/RunRecord.cs":"0db3b6cf367fed06ba506db2973593b100ebed4a6ca793552a77e490d0fce377","tests/Gcam.Studio.UiTests/Harness/Scenario.cs":"dac922df393236562f138489ab7a54d4ff09bb4003f7694da421564e0647b085","tests/Gcam.Studio.UiTests/Harness/StudioProcess.cs":"973777303020b10c38bcd701e991764b095cb2f149ed31da6a5d554bf73d87c2","tests/Gcam.Studio.UiTests/Harness/StudioWindow.cs":"59fca517024d117593d9056ed1c5122d2b8632b7ae179b36a8e091aea06b5bf8","tests/Gcam.Studio.UiTests/Harness/WorkspaceOracle.cs":"153e5f5ec778401b877467e405295a940d663d4fea1d32f9101139e9c0b325e5","tests/Gcam.Studio.UiTests/DesktopFactAttribute.cs":"1dcac8c38143a4d38635267e17af2acdbb5f2ee84eca07e2968a2a1e7f073bfc"},"trace":[{"requirement":"SR-PLOT-04","selectors":["TenMillionSamples_ZoomAndResizeRedraw_Within16Milliseconds"]},{"requirement":"SR-PLOT-05","selectors":["TenMillionSamples_ZoomAndResizeRedraw_Within16Milliseconds"]}]}
```


## PolishSurveyTests — Gcam.Studio.UiTests

Diagnostic desktop frames, never automatic pixel baselines.

Oracle: Saved frames are diagnostic measurement only and have no automatic pixel baseline.

Tolerance: The scenario also checks model state and source error within the projected element; screenshot capture is not its verdict.

Gates: `GCAM_UI_TESTS`.

Source: [tests/Gcam.Studio.UiTests/PolishSurveyTests.cs](../tests/Gcam.Studio.UiTests/PolishSurveyTests.cs).

```json
{"id":"Gcam.Studio.UiTests::Gcam.Studio.UiTests.PolishSurveyTests","runner":"dotnet","methods":["AllWorkspaces_BothThemesAndWindowSizes_CapturesPolishSurvey"],"sources":{"tests/Gcam.Studio.UiTests/PolishSurveyTests.cs":"66935216a51fb5cb63c0220ea006991cefdee407031ce232968a7464c666b736","tests/Gcam.Studio.UiTests/Harness/FloodOracle.cs":"22db35cb2771a0460e437e47308b5792bf1c22d2dc077af54106223a29cd75be","tests/Gcam.Studio.UiTests/Harness/Pointer.cs":"e56d09c72e552bf5619c088150a0d6ceba0ec2eb75ee06c2744c4677b9db896c","tests/Gcam.Studio.UiTests/Harness/RepoPaths.cs":"2bdc3c25db3c74cfe32ea7f64352f36cc5fb3b8fbf7be92ce03b7184648cc655","tests/Gcam.Studio.UiTests/Harness/RunRecord.cs":"0db3b6cf367fed06ba506db2973593b100ebed4a6ca793552a77e490d0fce377","tests/Gcam.Studio.UiTests/Harness/Scenario.cs":"dac922df393236562f138489ab7a54d4ff09bb4003f7694da421564e0647b085","tests/Gcam.Studio.UiTests/Harness/StudioProcess.cs":"973777303020b10c38bcd701e991764b095cb2f149ed31da6a5d554bf73d87c2","tests/Gcam.Studio.UiTests/Harness/StudioWindow.cs":"59fca517024d117593d9056ed1c5122d2b8632b7ae179b36a8e091aea06b5bf8","tests/Gcam.Studio.UiTests/Harness/WorkspaceOracle.cs":"153e5f5ec778401b877467e405295a940d663d4fea1d32f9101139e9c0b325e5","tests/Gcam.Studio.UiTests/DesktopFactAttribute.cs":"1dcac8c38143a4d38635267e17af2acdbb5f2ee84eca07e2968a2a1e7f073bfc"},"trace":[]}
```


## ScenarioTests — Gcam.Studio.UiTests

Stage-4 scenarios, one fresh app each, traced to the validation scenarios (VAL-xx) and requirements (SR-xx) in docs/VV.Studio.md.

Oracle: Product lifecycle, independently computed pixel-centre ROI/distance/angle and configured source position provide the oracle.

Tolerance: FloodOracle derives distance tolerance as 2*sqrt(2) device pixels plus .05 mm display rounding; Verdict derives angle tolerance from arm length and two-pixel endpoint/vertex errors. The additional .006+1e-12 one-cell comparison and 1.5 mm localization allowance are tolerance stated without a derivation in the code; the localization comment only says it allows acquisition photon noise.

Gates: `GCAM_UI_TESTS`.

Source: [tests/Gcam.Studio.UiTests/ScenarioTests.cs](../tests/Gcam.Studio.UiTests/ScenarioTests.cs).

Method groups:

- `StopContinueReset_KeepsAccumulatesAndDiscards`, `Roi_OnFloodMap_CountsWholePixelsByCentre`, `Angle_EscAbandonsDraft_DeleteRemovesSelected`, `MoveSource_ResetEditStart_PutsPeakOnTheSource`, `ThemeToggle_RelabelsAndSwitchesBack`: exact / structural fixture comparisons; gate GCAM_UI_TESTS.
- `Readout_AndOneCellRoi_MatchAbsolutePositionAndValue`: numerical comparisons described above; gate GCAM_UI_TESTS.

```json
{"id":"Gcam.Studio.UiTests::Gcam.Studio.UiTests.ScenarioTests","runner":"dotnet","methods":["StopContinueReset_KeepsAccumulatesAndDiscards","Roi_OnFloodMap_CountsWholePixelsByCentre","Readout_AndOneCellRoi_MatchAbsolutePositionAndValue","Angle_EscAbandonsDraft_DeleteRemovesSelected","MoveSource_ResetEditStart_PutsPeakOnTheSource","ThemeToggle_RelabelsAndSwitchesBack"],"sources":{"tests/Gcam.Studio.UiTests/ScenarioTests.cs":"b1f5ba72b6b3e6f826e9f6e0b675006d4b4d2072e5516515ab172c5142620450","tests/Gcam.Studio.UiTests/Harness/FloodOracle.cs":"22db35cb2771a0460e437e47308b5792bf1c22d2dc077af54106223a29cd75be","tests/Gcam.Studio.UiTests/Harness/Pointer.cs":"e56d09c72e552bf5619c088150a0d6ceba0ec2eb75ee06c2744c4677b9db896c","tests/Gcam.Studio.UiTests/Harness/RepoPaths.cs":"2bdc3c25db3c74cfe32ea7f64352f36cc5fb3b8fbf7be92ce03b7184648cc655","tests/Gcam.Studio.UiTests/Harness/RunRecord.cs":"0db3b6cf367fed06ba506db2973593b100ebed4a6ca793552a77e490d0fce377","tests/Gcam.Studio.UiTests/Harness/Scenario.cs":"dac922df393236562f138489ab7a54d4ff09bb4003f7694da421564e0647b085","tests/Gcam.Studio.UiTests/Harness/StudioProcess.cs":"973777303020b10c38bcd701e991764b095cb2f149ed31da6a5d554bf73d87c2","tests/Gcam.Studio.UiTests/Harness/StudioWindow.cs":"59fca517024d117593d9056ed1c5122d2b8632b7ae179b36a8e091aea06b5bf8","tests/Gcam.Studio.UiTests/Harness/WorkspaceOracle.cs":"153e5f5ec778401b877467e405295a940d663d4fea1d32f9101139e9c0b325e5","tests/Gcam.Studio.UiTests/DesktopFactAttribute.cs":"1dcac8c38143a4d38635267e17af2acdbb5f2ee84eca07e2968a2a1e7f073bfc"},"trace":[{"requirement":"SR-VIEW-07","selectors":["Readout_AndOneCellRoi_MatchAbsolutePositionAndValue"]},{"requirement":"SR-RUN-10","selectors":["StopContinueReset_KeepsAccumulatesAndDiscards"]},{"requirement":"SR-RUN-12","selectors":["MoveSource_ResetEditStart_PutsPeakOnTheSource","StopContinueReset_KeepsAccumulatesAndDiscards"]},{"requirement":"SR-RUN-13","selectors":["StopContinueReset_KeepsAccumulatesAndDiscards"]},{"requirement":"SR-RUN-23","selectors":["StopContinueReset_KeepsAccumulatesAndDiscards"]},{"requirement":"SR-RUN-26","selectors":["MoveSource_ResetEditStart_PutsPeakOnTheSource","StopContinueReset_KeepsAccumulatesAndDiscards"]},{"requirement":"VAL-02","selectors":["StopContinueReset_KeepsAccumulatesAndDiscards"]},{"requirement":"SR-MEAS-03","selectors":["Readout_AndOneCellRoi_MatchAbsolutePositionAndValue","Roi_OnFloodMap_CountsWholePixelsByCentre"]},{"requirement":"VAL-03","selectors":["Angle_EscAbandonsDraft_DeleteRemovesSelected","Roi_OnFloodMap_CountsWholePixelsByCentre"]},{"requirement":"SR-MEAS-02","selectors":["Angle_EscAbandonsDraft_DeleteRemovesSelected"]},{"requirement":"SR-MEAS-07","selectors":["Angle_EscAbandonsDraft_DeleteRemovesSelected"]},{"requirement":"SR-VIEW-08","selectors":["MoveSource_ResetEditStart_PutsPeakOnTheSource"]},{"requirement":"VAL-04","selectors":["MoveSource_ResetEditStart_PutsPeakOnTheSource"]},{"requirement":"VAL-05","selectors":["MoveSource_ResetEditStart_PutsPeakOnTheSource"]},{"requirement":"SR-VIEW-05","selectors":["Readout_AndOneCellRoi_MatchAbsolutePositionAndValue"]},{"requirement":"SR-THEME-01","selectors":["ThemeToggle_RelabelsAndSwitchesBack"]}]}
```


## WorkspaceOracleTests — Gcam.Studio.UiTests

Checks the desktop harness calculations for inclusive spectrum-band centres, half-open event arrival windows and interpolated half-maximum intervals with edge censoring.

Oracle: Hand-specified histogram centres/counts, half-open arrival windows and synthetic half-max curves provide the oracle.

Tolerance: Counts/index sets are exact; decimal interpolated endpoints retain the coded precision without an operation-count derivation. Unexplained margins: tolerance stated without a derivation in the code.

Gates: none.

Source: [tests/Gcam.Studio.UiTests/WorkspaceOracleTests.cs](../tests/Gcam.Studio.UiTests/WorkspaceOracleTests.cs).

Method groups:

- `BandCount_IncludesBoundaryCentres`, `EventsInWindow_IncludesStartExcludesEnd`: numerical comparisons described above; gate None.
- `HalfMax_InterpolatesAndCensors`: exact / structural fixture comparisons; gate None.

```json
{"id":"Gcam.Studio.UiTests::Gcam.Studio.UiTests.WorkspaceOracleTests","runner":"dotnet","methods":["BandCount_IncludesBoundaryCentres","EventsInWindow_IncludesStartExcludesEnd","HalfMax_InterpolatesAndCensors"],"sources":{"tests/Gcam.Studio.UiTests/WorkspaceOracleTests.cs":"fab3e89445eaae06aa2b82c7939d1f8d051a2a550282138e9b42b2710ccf1521","tests/Gcam.Studio.UiTests/Harness/FloodOracle.cs":"22db35cb2771a0460e437e47308b5792bf1c22d2dc077af54106223a29cd75be","tests/Gcam.Studio.UiTests/Harness/Pointer.cs":"e56d09c72e552bf5619c088150a0d6ceba0ec2eb75ee06c2744c4677b9db896c","tests/Gcam.Studio.UiTests/Harness/RepoPaths.cs":"2bdc3c25db3c74cfe32ea7f64352f36cc5fb3b8fbf7be92ce03b7184648cc655","tests/Gcam.Studio.UiTests/Harness/RunRecord.cs":"0db3b6cf367fed06ba506db2973593b100ebed4a6ca793552a77e490d0fce377","tests/Gcam.Studio.UiTests/Harness/Scenario.cs":"dac922df393236562f138489ab7a54d4ff09bb4003f7694da421564e0647b085","tests/Gcam.Studio.UiTests/Harness/StudioProcess.cs":"973777303020b10c38bcd701e991764b095cb2f149ed31da6a5d554bf73d87c2","tests/Gcam.Studio.UiTests/Harness/StudioWindow.cs":"59fca517024d117593d9056ed1c5122d2b8632b7ae179b36a8e091aea06b5bf8","tests/Gcam.Studio.UiTests/Harness/WorkspaceOracle.cs":"153e5f5ec778401b877467e405295a940d663d4fea1d32f9101139e9c0b325e5","tests/Gcam.Studio.UiTests/DesktopFactAttribute.cs":"1dcac8c38143a4d38635267e17af2acdbb5f2ee84eca07e2968a2a1e7f073bfc"},"trace":[]}
```


## WorkspaceScenarioTests — Gcam.Studio.UiTests

Checks the live Studio workspaces against retained histogram, event and flood data while changing windows, selected events, stripping and focus. It also checks detector-input locking and that displayed focus intervals agree with the sweep curve.

Oracle: Retained histogram/list-mode events and independent flood/strip/variance/half-height recomputation are the oracles.

Tolerance: Channel-side acquisition budget uses the quoted Gaussian-tail Bonferroni bound <=1e-3. Grid/label margins combine half-step and display rounding; covariance uses its coded roundoff budget.

Gates: `GCAM_UI_TESTS`.

Source: [tests/Gcam.Studio.UiTests/WorkspaceScenarioTests.cs](../tests/Gcam.Studio.UiTests/WorkspaceScenarioTests.cs).

Method groups:

- `Spectrum_BandCountAndWindowChange_MatchRetainedHistogram`: exact / structural fixture comparisons; gate GCAM_UI_TESTS.
- `Waveform_SelectedEventListAndMarkers_MatchArrivalWindow`, `Detector_FaceBeforeStart_LockedUntilReset`, `Imaging_ChannelAndStrip_MatchRetainedFloodAndFoundPeaks`, `Imaging_RefocusAndSweep_MatchProjectionAndHalfMaxInterval`: numerical comparisons described above; gate GCAM_UI_TESTS.

```json
{"id":"Gcam.Studio.UiTests::Gcam.Studio.UiTests.WorkspaceScenarioTests","runner":"dotnet","methods":["Spectrum_BandCountAndWindowChange_MatchRetainedHistogram","Waveform_SelectedEventListAndMarkers_MatchArrivalWindow","Detector_FaceBeforeStart_LockedUntilReset","Imaging_ChannelAndStrip_MatchRetainedFloodAndFoundPeaks","Imaging_RefocusAndSweep_MatchProjectionAndHalfMaxInterval"],"sources":{"tests/Gcam.Studio.UiTests/WorkspaceScenarioTests.cs":"c023e759ae2a4580b80a836d6bf0a99bac92030dabc8406f2cc0048b4fc39d81","tests/Gcam.Studio.UiTests/Harness/FloodOracle.cs":"22db35cb2771a0460e437e47308b5792bf1c22d2dc077af54106223a29cd75be","tests/Gcam.Studio.UiTests/Harness/Pointer.cs":"e56d09c72e552bf5619c088150a0d6ceba0ec2eb75ee06c2744c4677b9db896c","tests/Gcam.Studio.UiTests/Harness/RepoPaths.cs":"2bdc3c25db3c74cfe32ea7f64352f36cc5fb3b8fbf7be92ce03b7184648cc655","tests/Gcam.Studio.UiTests/Harness/RunRecord.cs":"0db3b6cf367fed06ba506db2973593b100ebed4a6ca793552a77e490d0fce377","tests/Gcam.Studio.UiTests/Harness/Scenario.cs":"dac922df393236562f138489ab7a54d4ff09bb4003f7694da421564e0647b085","tests/Gcam.Studio.UiTests/Harness/StudioProcess.cs":"973777303020b10c38bcd701e991764b095cb2f149ed31da6a5d554bf73d87c2","tests/Gcam.Studio.UiTests/Harness/StudioWindow.cs":"59fca517024d117593d9056ed1c5122d2b8632b7ae179b36a8e091aea06b5bf8","tests/Gcam.Studio.UiTests/Harness/WorkspaceOracle.cs":"153e5f5ec778401b877467e405295a940d663d4fea1d32f9101139e9c0b325e5","tests/Gcam.Studio.UiTests/DesktopFactAttribute.cs":"1dcac8c38143a4d38635267e17af2acdbb5f2ee84eca07e2968a2a1e7f073bfc"},"trace":[{"requirement":"SR-IMG-02","selectors":["Imaging_ChannelAndStrip_MatchRetainedFloodAndFoundPeaks"]}]}
```


## AlignmentTests — Gcam.Tests

Mask–detector alignment / pose error: the physical mask is displaced/rolled from the ideal pose the decoder back-projects with, so a mis-registered mask casts a systematically shifted/rotated coded shadow → a SYSTEMATIC localization bias.

Oracle: Pose equivalence uses the same seeded ray decisions after inverse translation/rotation.

Tolerance: Localization bias is compared with source truth and mask-offset magnification (D+S)/D; the 1 mm floor and 1.8–3.4 interval have no derived margins. Unexplained margins: tolerance stated without a derivation in the code.

Gates: none.

Source: [tests/Gcam.Tests/AlignmentTests.cs](../tests/Gcam.Tests/AlignmentTests.cs).

Method groups:

- `InPlaneOffset_EquivalentToShiftingTheRay`, `Roll_EquivalentToRotatingTheRay`, `PerfectAlignment_IsUnchangedFromNoPose`: exact / structural fixture comparisons; gate None.
- `InPlaneOffset_BiasesLocalization_AmplifiedByMagnification`: numerical comparisons described above; gate None.

```json
{"id":"Gcam.Tests::Gcam.Tests.AlignmentTests","runner":"dotnet","methods":["InPlaneOffset_EquivalentToShiftingTheRay","Roll_EquivalentToRotatingTheRay","PerfectAlignment_IsUnchangedFromNoPose","InPlaneOffset_BiasesLocalization_AmplifiedByMagnification"],"sources":{"tests/Gcam.Tests/AlignmentTests.cs":"24db88afa3d68efc06ec45404a8e9eb3a6f59309bb72fbee748481f7c18554c8","tests/Gcam.Tests/Harness/Rigs.cs":"9b89ce04cdcb302fcefc6636d45a0d1640a48c288d1e0a3638b005c44028c3b2","tests/Gcam.Tests/Harness/Stat.cs":"4502dddc6276f9d1de0c41b2ed7246ed7f00c9266daa12a34d6174ae1cb19029"},"trace":[{"requirement":"EV-31","selectors":["InPlaneOffset_BiasesLocalization_AmplifiedByMagnification","InPlaneOffset_EquivalentToShiftingTheRay","PerfectAlignment_IsUnchangedFromNoPose","Roll_EquivalentToRotatingTheRay"]}]}
```


## AmbientAngularSamplerTests — Gcam.Tests

Checks projected incident current for isotropic illumination and preservation of joint energy-angle correlations at crystal faces. It also checks malformed-table rejection and event-for-event replay across Stop / Continue partitions.

Oracle: Analytic isotropic projected surface areas supply total-current expectations. MeanProjectedArea from the production sampler supplies current-weighted energy and conditional zenith probabilities, limiting independence; explicit angular bins and ray-box intersection checks supply correlation and crystal-hit expectations. The isotropic area check retains its 64-machine-epsilon budget.

Tolerance: Joint energy/angle probabilities use six binomial standard errors with the conditional sample count. Invalid-table and partition-replay checks are structural/exact.

Gates: none.

Source: [tests/Gcam.Tests/AmbientAngularSamplerTests.cs](../tests/Gcam.Tests/AmbientAngularSamplerTests.cs).

Method groups:

- `IsotropicTable_CurrentMatchesAnalyticSurfaceArea`, `InvalidTable_RejectsOverlappingBinsMarginalMismatchAndStaleHash`, `AngularField_StopContinuePartitionKeepsEveryRecord`: exact / structural fixture comparisons; gate None.
- `JointDistribution_PreservesEnergyAngleCorrelationAndProjectedCurrent`: numerical comparisons described above; gate None.

```json
{"id":"Gcam.Tests::Gcam.Tests.AmbientAngularSamplerTests","runner":"dotnet","methods":["IsotropicTable_CurrentMatchesAnalyticSurfaceArea","JointDistribution_PreservesEnergyAngleCorrelationAndProjectedCurrent","InvalidTable_RejectsOverlappingBinsMarginalMismatchAndStaleHash","AngularField_StopContinuePartitionKeepsEveryRecord"],"sources":{"tests/Gcam.Tests/AmbientAngularSamplerTests.cs":"08c924437393ed66c516feb8acee5122d662b55a0a27b157c037154ab1680534","tests/Gcam.Tests/Harness/Rigs.cs":"9b89ce04cdcb302fcefc6636d45a0d1640a48c288d1e0a3638b005c44028c3b2","tests/Gcam.Tests/Harness/Stat.cs":"4502dddc6276f9d1de0c41b2ed7246ed7f00c9266daa12a34d6174ae1cb19029"},"trace":[{"requirement":"EV-34","selectors":["AngularField_StopContinuePartitionKeepsEveryRecord","InvalidTable_RejectsOverlappingBinsMarginalMismatchAndStaleHash","IsotropicTable_CurrentMatchesAnalyticSurfaceArea","JointDistribution_PreservesEnergyAngleCorrelationAndProjectedCurrent"]}]}
```


## AmbientEvidenceTests — Gcam.Tests

Checks line-map superposition, intensity scaling and the exposure budgets used by ambient evidence recipes. Ideal no-field subtraction, field-of-view fractions and spatial separation outputs must retain their declared meanings.

Oracle: Line-map superposition and power-of-two intensity scaling are exact; ideal subtraction is compared with the same decoder without a field.

Tolerance: Count/window/position tallies and probability ranges are structural. Decimal precisions are stated without a rounding-error derivation. Unexplained margins: tolerance stated without a derivation in the code.

Gates: none.

Source: [tests/Gcam.Tests/AmbientEvidenceTests.cs](../tests/Gcam.Tests/AmbientEvidenceTests.cs).

Method groups:

- `SourceLines_IsTheSumOfItsLinesEachOnItsOwnStream`, `SourceLines_IsLinearInTheLineIntensity`, `Separation_IdealHasNothingToSubtract_AndCoAddsToThe662Window`: exact / structural fixture comparisons; gate None.
- `Antimask_IdealCalibratedIsSingle_AndTheBudgetsAreTheDeclaredOnes`, `FieldOfView_OnAxisSourceGivesN0_AndFractionsAreProbabilities`, `Sweep_CountsArePositionTalliesAndThePullIsReportedPerBoundAndWindow`: numerical comparisons described above; gate None.

```json
{"id":"Gcam.Tests::Gcam.Tests.AmbientEvidenceTests","runner":"dotnet","methods":["SourceLines_IsTheSumOfItsLinesEachOnItsOwnStream","SourceLines_IsLinearInTheLineIntensity","Antimask_IdealCalibratedIsSingle_AndTheBudgetsAreTheDeclaredOnes","FieldOfView_OnAxisSourceGivesN0_AndFractionsAreProbabilities","Separation_IdealHasNothingToSubtract_AndCoAddsToThe662Window","Sweep_CountsArePositionTalliesAndThePullIsReportedPerBoundAndWindow"],"sources":{"tests/Gcam.Tests/AmbientEvidenceTests.cs":"b73b903fcff7fa2daed6d53d6754461e671a6b0b1ee6a27431ac1c9524f8260e","tests/Gcam.Tests/Harness/Rigs.cs":"9b89ce04cdcb302fcefc6636d45a0d1640a48c288d1e0a3638b005c44028c3b2","tests/Gcam.Tests/Harness/Stat.cs":"4502dddc6276f9d1de0c41b2ed7246ed7f00c9266daa12a34d6174ae1cb19029"},"trace":[{"requirement":"EV-34","selectors":["Antimask_IdealCalibratedIsSingle_AndTheBudgetsAreTheDeclaredOnes","FieldOfView_OnAxisSourceGivesN0_AndFractionsAreProbabilities","Separation_IdealHasNothingToSubtract_AndCoAddsToThe662Window","SourceLines_IsLinearInTheLineIntensity","SourceLines_IsTheSumOfItsLinesEachOnItsOwnStream","Sweep_CountsArePositionTalliesAndThePullIsReportedPerBoundAndWindow"]}]}
```


## AmbientFieldTests — Gcam.Tests

Checks absolute-field Poisson arrivals and detector counts, dose closure, crystal-face scoring and agreement between fixed-time floods and event streams. Null/zero-field paths must preserve legacy events and RNG state, while invalid or unvalidated evidence inputs must be rejected.

Oracle: Captured preimplementation record/RNG hashes and partitioned event replay supply legacy identities. Analytic Poisson and binomial moments, independent midpoint dose integration against ICRP 74, actual deposit-pixel indices and common-history flood/event tallies supply field and detector expectations.

Tolerance: Poisson mean/sample-variance, dark-count, efficiency and per-pixel variance bounds use six standard errors; comments give their formulas. Dose closure uses 32 machine epsilons and the composite-midpoint curvature bound.

Gates: none.

Source: [tests/Gcam.Tests/AmbientFieldTests.cs](../tests/Gcam.Tests/AmbientFieldTests.cs).

Method groups:

- `AmbientNull_MatchesCapturedPreimplementationRecordsAndRng`, `SourceFreeZeroField_ProgressesAndRejectsCancellationBeforeRandomDraws`, `AbsoluteField_IsExactlyIndependentOfConfiguredSourceActivity`, `IncidentPoissonBins_MeanAndVarianceUseSampleSizeDerivedTolerances`, `DoseClosure_IntegratesContinuumAgainstTheSameIcrpTable`, `EveryCrystalFace_ScoresAtItsActualDepositPixel`, `DevelopmentPlaceholder_CannotBeUsedAsValidatedEvidence`, `FixedTimeFloodAndEventStream_AgreeWithoutResampling`, `SourceFreeRelativeBsrAddsZero_AndElectronicDarkCountsRemainIndependent`, `ParallelBeam_InteractionEfficiencyHasBinomialSampleSizeTolerance`, `DetectedCountsPerPixel_ArePoissonOverFixedTimeBins`, `EvidenceHashAndInvalidSpectrumAreRejectedWithoutTransport`: exact / structural fixture comparisons; gate None.
- `EmptyIntervalsAndSegmentBoundaries_PreserveEvents`, `ZeroAbsoluteField_WithSourceAndLegacyBsr_ReplaysTheLegacyStreamExactly`: numerical comparisons described above; gate None.

```json
{"id":"Gcam.Tests::Gcam.Tests.AmbientFieldTests","runner":"dotnet","methods":["AmbientNull_MatchesCapturedPreimplementationRecordsAndRng","EmptyIntervalsAndSegmentBoundaries_PreserveEvents","SourceFreeZeroField_ProgressesAndRejectsCancellationBeforeRandomDraws","AbsoluteField_IsExactlyIndependentOfConfiguredSourceActivity","IncidentPoissonBins_MeanAndVarianceUseSampleSizeDerivedTolerances","DoseClosure_IntegratesContinuumAgainstTheSameIcrpTable","EveryCrystalFace_ScoresAtItsActualDepositPixel","DevelopmentPlaceholder_CannotBeUsedAsValidatedEvidence","FixedTimeFloodAndEventStream_AgreeWithoutResampling","ZeroAbsoluteField_WithSourceAndLegacyBsr_ReplaysTheLegacyStreamExactly","SourceFreeRelativeBsrAddsZero_AndElectronicDarkCountsRemainIndependent","ParallelBeam_InteractionEfficiencyHasBinomialSampleSizeTolerance","DetectedCountsPerPixel_ArePoissonOverFixedTimeBins","EvidenceHashAndInvalidSpectrumAreRejectedWithoutTransport"],"sources":{"tests/Gcam.Tests/AmbientFieldTests.cs":"90f59d1bf35e778e4cdd1bae93c06591a144b776451c916a8d4b53f8a233897b","tests/Gcam.Tests/Harness/Rigs.cs":"9b89ce04cdcb302fcefc6636d45a0d1640a48c288d1e0a3638b005c44028c3b2","tests/Gcam.Tests/Harness/Stat.cs":"4502dddc6276f9d1de0c41b2ed7246ed7f00c9266daa12a34d6174ae1cb19029"},"trace":[{"requirement":"EV-34","selectors":["AbsoluteField_IsExactlyIndependentOfConfiguredSourceActivity","AmbientNull_MatchesCapturedPreimplementationRecordsAndRng","DetectedCountsPerPixel_ArePoissonOverFixedTimeBins","DevelopmentPlaceholder_CannotBeUsedAsValidatedEvidence","DoseClosure_IntegratesContinuumAgainstTheSameIcrpTable","EmptyIntervalsAndSegmentBoundaries_PreserveEvents","EveryCrystalFace_ScoresAtItsActualDepositPixel","EvidenceHashAndInvalidSpectrumAreRejectedWithoutTransport","FixedTimeFloodAndEventStream_AgreeWithoutResampling","IncidentPoissonBins_MeanAndVarianceUseSampleSizeDerivedTolerances","ParallelBeam_InteractionEfficiencyHasBinomialSampleSizeTolerance","SourceFreeRelativeBsrAddsZero_AndElectronicDarkCountsRemainIndependent","SourceFreeZeroField_ProgressesAndRejectsCancellationBeforeRandomDraws","ZeroAbsoluteField_WithSourceAndLegacyBsr_ReplaysTheLegacyStreamExactly"]}]}
```


## AmbientGateStudyTests — Gcam.Tests

Checks the threshold file comparison policy: strict exceedance rejects ties, while four-decimal rounded comparison accepts them. Unknown comparison policies must be refused.

Oracle: Explicit threshold comparison policy defines strict ties, four-decimal rounded ties and the no-candidate floor.

Tolerance: Exact decision fixtures; four decimals is the recorded selection precision stated in code, not a sampled uncertainty.

Gates: none.

Source: [tests/Gcam.Tests/AmbientGateStudyTests.cs](../tests/Gcam.Tests/AmbientGateStudyTests.cs).

Method groups:

- `Thresholds_GreaterThan_IsTheTurn7RuleAndTheDefault`, `Thresholds_RoundedAtLeast_CountsATieAsTrusted`: numerical comparisons described above; gate None.
- `Thresholds_UnknownComparison_IsRefused`: exact / structural fixture comparisons; gate None.

```json
{"id":"Gcam.Tests::Gcam.Tests.AmbientGateStudyTests","runner":"dotnet","methods":["Thresholds_GreaterThan_IsTheTurn7RuleAndTheDefault","Thresholds_RoundedAtLeast_CountsATieAsTrusted","Thresholds_UnknownComparison_IsRefused"],"sources":{"tests/Gcam.Tests/AmbientGateStudyTests.cs":"b7cd218d024c3dfb941b447be2ebdffda7e0262932617e9a59c1a9e3ec307e2a","tests/Gcam.Tests/Harness/Rigs.cs":"9b89ce04cdcb302fcefc6636d45a0d1640a48c288d1e0a3638b005c44028c3b2","tests/Gcam.Tests/Harness/Stat.cs":"4502dddc6276f9d1de0c41b2ed7246ed7f00c9266daa12a34d6174ae1cb19029"},"trace":[{"requirement":"EV-34","selectors":["Thresholds_GreaterThan_IsTheTurn7RuleAndTheDefault","Thresholds_RoundedAtLeast_CountsATieAsTrusted","Thresholds_UnknownComparison_IsRefused"]}]}
```


## AngularResolutionStudyTests — Gcam.Tests

TODO-34: the angres evidence family runs end to end on a tiny budget and keeps its geometry: the one-period grid is the fully coded field, positions sit at z·tan(elements·atan(c/D)), counts never exceed the repeats, and the ladder's shadow sampling is (S + D)/S · c / pixel.

Oracle: Geometry follows one-period FCFOV and shadow magnification formulas; smoke counts are structural, not MC precision evidence.

Tolerance: Landed share integrates the open-area fraction cell by cell and uses four binomial standard errors. Geometry decimal precisions have no stated error-budget derivation. Unexplained margins: tolerance stated without a derivation in the code.

Gates: none.

Source: [tests/Gcam.Tests/AngularResolutionStudyTests.cs](../tests/Gcam.Tests/AngularResolutionStudyTests.cs).

Method groups:

- `Main_RunsAndKeepsItsGeometry`, `Ladder_RunsEveryStep`, `SelfConsistentFlood_LandsTheOpenShareOfAimedPhotons`: numerical comparisons described above; gate None.
- `Main_RefusesASourceOutsideTheSearchGrid`: exact / structural fixture comparisons; gate None.

```json
{"id":"Gcam.Tests::Gcam.Tests.AngularResolutionStudyTests","runner":"dotnet","methods":["Main_RunsAndKeepsItsGeometry","Main_RefusesASourceOutsideTheSearchGrid","Ladder_RunsEveryStep","SelfConsistentFlood_LandsTheOpenShareOfAimedPhotons"],"sources":{"tests/Gcam.Tests/AngularResolutionStudyTests.cs":"8a2173c84668a13c2d44fe05c2c183bc2886a64401cc1f6752ae5e181cb94d11","tests/Gcam.Tests/Harness/Rigs.cs":"9b89ce04cdcb302fcefc6636d45a0d1640a48c288d1e0a3638b005c44028c3b2","tests/Gcam.Tests/Harness/Stat.cs":"4502dddc6276f9d1de0c41b2ed7246ed7f00c9266daa12a34d6174ae1cb19029"},"trace":[{"requirement":"EV-11","selectors":["Ladder_RunsEveryStep","Main_RefusesASourceOutsideTheSearchGrid","Main_RunsAndKeepsItsGeometry","SelfConsistentFlood_LandsTheOpenShareOfAimedPhotons"]}]}
```


## AutoFocusTests — Gcam.Tests

Auto-focus: MixedFieldStudy.BestFocalMm must find the focal plane that focuses a source at an UNKNOWN distance — the fix for "a far source isn't caught" (the app used to decode at a fixed near plane).

Oracle: The oracle is the configured source plane.

Tolerance: The comment identifies a roughly 18 mm sweep step, but does not derive the chosen ±25 mm margin. Unexplained margins: tolerance stated without a derivation in the code.

Gates: none.

Source: [tests/Gcam.Tests/AutoFocusTests.cs](../tests/Gcam.Tests/AutoFocusTests.cs).

```json
{"id":"Gcam.Tests::Gcam.Tests.AutoFocusTests","runner":"dotnet","methods":["BestFocal_FindsTheSourcePlane"],"sources":{"tests/Gcam.Tests/AutoFocusTests.cs":"1a8c864657858f7254b701d82bc5935fbbdf2aa38bbfdcefdad4db475f48c771","tests/Gcam.Tests/Harness/Rigs.cs":"9b89ce04cdcb302fcefc6636d45a0d1640a48c288d1e0a3638b005c44028c3b2","tests/Gcam.Tests/Harness/Stat.cs":"4502dddc6276f9d1de0c41b2ed7246ed7f00c9266daa12a34d6174ae1cb19029"},"trace":[]}
```


## BackgroundDecodingTests — Gcam.Tests

Checks joint source/background EM updates, count conservation and likelihood behavior, including the zero-background boundary and scale invariance. Calibration must be detached, correctly bound and explicitly enabled; null and known-scale paths must preserve the retained decoder behavior.

Oracle: An independent double-precision joint EM step supplies the update reference; real-arithmetic count conservation and Poisson log likelihood supply invariants. Retained/direct decoder calls and deliberately mismatched calibration bindings supply compatibility and rejection fixtures. Forward-error checks retain the 22-float-operation update bound, four count-sum operations and the positive-projection likelihood perturbation bound.

Tolerance: Boundary, invalid-input, exact power-of-two scaling and retained-decoder checks are exact/structural.

Gates: none.

Source: [tests/Gcam.Tests/BackgroundDecodingTests.cs](../tests/Gcam.Tests/BackgroundDecodingTests.cs).

Method groups:

- `JointStep_MatchesIndependentDoubleReference`, `JointEm_ConservesCountsAndDoesNotDecreaseLikelihoodWithinRoundoff`, `JointBetaZero_IsAnExactBoundary`, `EmptyImage_DoesNotCreateCounts`, `InvalidShapeAndCounts_AreRejected`, `Calibration_IsDetachedAndRefusesWrongBoundWindowGeometryAndHead`, `NullFactoryPath_IsExactlyTheRetainedDecoder`, `KnownScaleModes_AreExactlyDirectFixedOrSignedDecoding`, `ConfigCloneAndExplicitOptIn_RequireCalibration`: exact / structural fixture comparisons; gate None.
- `Normalization_IsInvariantUnderExactPowerOfTwoScaling`: numerical comparisons described above; gate None.

```json
{"id":"Gcam.Tests::Gcam.Tests.BackgroundDecodingTests","runner":"dotnet","methods":["JointStep_MatchesIndependentDoubleReference","JointEm_ConservesCountsAndDoesNotDecreaseLikelihoodWithinRoundoff","JointBetaZero_IsAnExactBoundary","EmptyImage_DoesNotCreateCounts","Normalization_IsInvariantUnderExactPowerOfTwoScaling","InvalidShapeAndCounts_AreRejected","Calibration_IsDetachedAndRefusesWrongBoundWindowGeometryAndHead","NullFactoryPath_IsExactlyTheRetainedDecoder","KnownScaleModes_AreExactlyDirectFixedOrSignedDecoding","ConfigCloneAndExplicitOptIn_RequireCalibration"],"sources":{"tests/Gcam.Tests/BackgroundDecodingTests.cs":"b3861d846a486bba01c27dedf52a8957005357e993bad1bc60f955f139b04f09","tests/Gcam.Tests/Harness/Rigs.cs":"9b89ce04cdcb302fcefc6636d45a0d1640a48c288d1e0a3638b005c44028c3b2","tests/Gcam.Tests/Harness/Stat.cs":"4502dddc6276f9d1de0c41b2ed7246ed7f00c9266daa12a34d6174ae1cb19029"},"trace":[{"requirement":"EV-35","selectors":["Calibration_IsDetachedAndRefusesWrongBoundWindowGeometryAndHead","ConfigCloneAndExplicitOptIn_RequireCalibration","EmptyImage_DoesNotCreateCounts","InvalidShapeAndCounts_AreRejected","JointBetaZero_IsAnExactBoundary","JointEm_ConservesCountsAndDoesNotDecreaseLikelihoodWithinRoundoff","JointStep_MatchesIndependentDoubleReference","KnownScaleModes_AreExactlyDirectFixedOrSignedDecoding","Normalization_IsInvariantUnderExactPowerOfTwoScaling","NullFactoryPath_IsExactlyTheRetainedDecoder"]}]}
```


## BackgroundTests — Gcam.Tests

Ambient background: a diffuse, uncoded field modelled as a uniform pedestal on the flood map.

Oracle: Pedestal and gradient normalization use their stated formulas.

Tolerance: Study checks compare clean/heavy BSR and graded/flat mean maps. The +1 mm knee margin is supported by the 64-seed measurements quoted in code; the remaining decimal/fixed RMS, failure-rate, edge-share and near-identity margins are not derived. Unexplained margins: tolerance stated without a derivation in the code.

Gates: none.

Source: [tests/Gcam.Tests/BackgroundTests.cs](../tests/Gcam.Tests/BackgroundTests.cs).

```json
{"id":"Gcam.Tests::Gcam.Tests.BackgroundTests","runner":"dotnet","methods":["Sweep_DegradesMonotonically_AndCollapsesAtHighBackground","ZeroBackground_MatchesCleanRun","PedestalPerPixel_IsBsrTimesCountsOverPixels","SideLeakProfile_IsMeanOneAndEdgeWeighted","GradientProfile_IsMeanOneAndRampsAlongDirection","GradedBackground_IsHarmlessWhileSourceWins_ThenBiasesAtTheKnee"],"sources":{"tests/Gcam.Tests/BackgroundTests.cs":"25ea2454bfde307fb568687527c4d97befd18c807f6376cb23778fabc2f19bea","tests/Gcam.Tests/Harness/Rigs.cs":"9b89ce04cdcb302fcefc6636d45a0d1640a48c288d1e0a3638b005c44028c3b2","tests/Gcam.Tests/Harness/Stat.cs":"4502dddc6276f9d1de0c41b2ed7246ed7f00c9266daa12a34d6174ae1cb19029"},"trace":[{"requirement":"EV-12","selectors":["GradedBackground_IsHarmlessWhileSourceWins_ThenBiasesAtTheKnee","GradientProfile_IsMeanOneAndRampsAlongDirection","PedestalPerPixel_IsBsrTimesCountsOverPixels","SideLeakProfile_IsMeanOneAndEdgeWeighted","Sweep_DegradesMonotonically_AndCollapsesAtHighBackground","ZeroBackground_MatchesCleanRun"]},{"requirement":"EV-25","selectors":["GradedBackground_IsHarmlessWhileSourceWins_ThenBiasesAtTheKnee","GradientProfile_IsMeanOneAndRampsAlongDirection","PedestalPerPixel_IsBsrTimesCountsOverPixels","SideLeakProfile_IsMeanOneAndEdgeWeighted","Sweep_DegradesMonotonically_AndCollapsesAtHighBackground","ZeroBackground_MatchesCleanRun"]}]}
```


## CascadeEmissionTests — Gcam.Tests

Per-decay list-mode emission of cascade isotopes (TODO-14): one history per Co-60 decay, both gammas transported, the detected ones summed into ONE event.

Oracle: Biased and analog per-decay rates use binomial and weight-bound variances combined in quadrature, four standard errors.

Tolerance: Summed-energy and scheme-selection checks are structural; the added 1e-9 energy ceilings have no error-budget derivation. Unexplained margins: tolerance stated without a derivation in the code.

Gates: none.

Source: [tests/Gcam.Tests/CascadeEmissionTests.cs](../tests/Gcam.Tests/CascadeEmissionTests.cs).

```json
{"id":"Gcam.Tests::Gcam.Tests.CascadeEmissionTests","runner":"dotnet","methods":["BiasedDecays_ReproduceAnalogDecays_DetectedAndCoincident","OneEventPerDecay_SumsTheGammas_WithOneArrivalTime","CascadePath_OnlyForTheSchemesOwnLines"],"sources":{"tests/Gcam.Tests/CascadeEmissionTests.cs":"399fa676757a7e19f0d807bb031fd580041163f361bb86c147f5123d67a188cd","tests/Gcam.Tests/Harness/Rigs.cs":"9b89ce04cdcb302fcefc6636d45a0d1640a48c288d1e0a3638b005c44028c3b2","tests/Gcam.Tests/Harness/Stat.cs":"4502dddc6276f9d1de0c41b2ed7246ed7f00c9266daa12a34d6174ae1cb19029"},"trace":[{"requirement":"SR-RUN-16","selectors":["BiasedDecays_ReproduceAnalogDecays_DetectedAndCoincident","CascadePath_OnlyForTheSchemesOwnLines","OneEventPerDecay_SumsTheGammas_WithOneArrivalTime"]}]}
```


## CascadeSummingTests — Gcam.Tests

True (cascade) coincidence summing: two gammas from ONE decay both deposit in the crystal, so their energies SUM into one event (Co-60 1173+1332 → 2505).

Oracle: Co-60 multiplicities follow independent table branchings; angular moments use Legendre coefficients and four sample-mean standard errors, and the histogram uses the stated chi-square quantile.

Tolerance: The slope 1.5–2.8 is supported by the quoted 24-seed slope distribution. Initial branching bands, energy windows, decimal comparisons and minimum near-field yield include fixed margins without derivation. Unexplained margins: tolerance stated without a derivation in the code.

Gates: none.

Source: [tests/Gcam.Tests/CascadeSummingTests.cs](../tests/Gcam.Tests/CascadeSummingTests.cs).

Method groups:

- `Cs137_IsASingleUncorrelatedLine`, `Co60_EmitsThe1173And1332Cascade`, `Na22_511sAreEmittedBackToBack`, `Co60_PhotonCountsPerDecay_FollowTheTableBranching`, `Co60_AngularCorrelation_MatchesW`, `Co60Cosine_IsTheInverseCdfOfW`, `Na22_DecaySchemeUsesTheIsotopeTable_AndPartnersFollowTheKnownPhoton`, `Co60_SumPeakScalesAsEpsilonSquared`: numerical comparisons described above; gate None.
- `Cs137_ShowsNoCascadeSumming`: exact / structural fixture comparisons; gate None.

```json
{"id":"Gcam.Tests::Gcam.Tests.CascadeSummingTests","runner":"dotnet","methods":["Cs137_IsASingleUncorrelatedLine","Co60_EmitsThe1173And1332Cascade","Na22_511sAreEmittedBackToBack","Co60_PhotonCountsPerDecay_FollowTheTableBranching","Co60_AngularCorrelation_MatchesW","Co60Cosine_IsTheInverseCdfOfW","Na22_DecaySchemeUsesTheIsotopeTable_AndPartnersFollowTheKnownPhoton","Co60_SumPeakScalesAsEpsilonSquared","Cs137_ShowsNoCascadeSumming"],"sources":{"tests/Gcam.Tests/CascadeSummingTests.cs":"3509e6fac0bc7c1354071b54ae8ac857259f1c21a648c90eaec1a6309b287c80","tests/Gcam.Tests/Harness/Rigs.cs":"9b89ce04cdcb302fcefc6636d45a0d1640a48c288d1e0a3638b005c44028c3b2","tests/Gcam.Tests/Harness/Stat.cs":"4502dddc6276f9d1de0c41b2ed7246ed7f00c9266daa12a34d6174ae1cb19029"},"trace":[{"requirement":"SR-RUN-16","selectors":["Co60Cosine_IsTheInverseCdfOfW","Co60_AngularCorrelation_MatchesW","Co60_EmitsThe1173And1332Cascade","Co60_PhotonCountsPerDecay_FollowTheTableBranching","Co60_SumPeakScalesAsEpsilonSquared","Cs137_IsASingleUncorrelatedLine","Cs137_ShowsNoCascadeSumming","Na22_511sAreEmittedBackToBack","Na22_DecaySchemeUsesTheIsotopeTable_AndPartnersFollowTheKnownPhoton"]}]}
```


## ChargeDivisionNetworkTests — Gcam.Tests

TODO-19 charge-division network: the Kirchhoff (nodal-analysis) solver behind the four-output readout reproduces hand-solved circuits and, on full 12 × 12 sensor grids, conserves charge with mirror symmetry for every topology; a solved circuit exports as a SPICE netlist (RD-10).

Oracle: Hand-solved circuits whose closed forms are derived in the test comments — a one-row discretised positioning circuit as a series chain, ((k+1)·R + r/2)/((S+1)·R + r); one row of two sensors, left share (2−α)/(3−α); the 2 × 2 corner grid, 7/15, 3/15, 3/15, 2/15 — plus structural charge conservation, mirror symmetry and refusal of a floating node.

Tolerance: 1e-9 absolute: round-off of a Cholesky solve of an at most ~200-node conductance Laplacian (derivation in the class summary); conservation and symmetry to the same bound; the refusal is exact.

Gates: none.

Source: [tests/Gcam.Tests/ChargeDivisionNetworkTests.cs](../tests/Gcam.Tests/ChargeDivisionNetworkTests.cs).

Method groups:

- `Dpc_OneRowOfTwo_MatchesHandSolvedCircuit`, `Dpc_SingleRow_DividesAsASeriesChain`, `CornerGrid_TwoByTwo_MatchesHandSolvedCircuit`: hand-solved closed forms within round-off; gate None.
- `FullGrid_ConservesCharge_AndIsMirrorSymmetric`, `FiniteInputImpedance_StillConservesCharge`: structural conservation and symmetry within round-off; gate None.
- `SolvedCircuit_ExportsAsSpice_IdealDividerHasNone`, `NodeWithoutPathToAnOutput_IsRefused`: exact / structural fixture comparisons; gate None.

```json
{"id":"Gcam.Tests::Gcam.Tests.ChargeDivisionNetworkTests","runner":"dotnet","methods":["Dpc_OneRowOfTwo_MatchesHandSolvedCircuit","CornerGrid_TwoByTwo_MatchesHandSolvedCircuit","Dpc_SingleRow_DividesAsASeriesChain","FullGrid_ConservesCharge_AndIsMirrorSymmetric","FiniteInputImpedance_StillConservesCharge","SolvedCircuit_ExportsAsSpice_IdealDividerHasNone","NodeWithoutPathToAnOutput_IsRefused"],"sources":{"tests/Gcam.Tests/ChargeDivisionNetworkTests.cs":"b753aadf285e21df0193a3f0dcf65179cb501cb74cc5a3b535e97ab6fca65ba6","tests/Gcam.Tests/Harness/Rigs.cs":"9b89ce04cdcb302fcefc6636d45a0d1640a48c288d1e0a3638b005c44028c3b2","tests/Gcam.Tests/Harness/Stat.cs":"4502dddc6276f9d1de0c41b2ed7246ed7f00c9266daa12a34d6174ae1cb19029"},"trace":[]}
```

## ComptonTests — Gcam.Tests

Crystal Compton scattering: physics kinematics, positioning-strategy behaviour, and the multi-isotope spatial-separation claim (contamination is coded from its source).

Oracle: Kinematic energy conservation, Compton edges and unit length are formula oracles; spatial contamination and stripped Cs counts use configured positions and pure-Cs references.

Tolerance: The 1e-6, 2.5 mm, contamination and stripping margins are stated without derivation. Unexplained margins: tolerance stated without a derivation in the code.

Gates: none.

Source: [tests/Gcam.Tests/ComptonTests.cs](../tests/Gcam.Tests/ComptonTests.cs).

Method groups:

- `Scatter_ConservesEnergy_AndStaysWithinKinematicBounds`, `Contamination_IsImagedAtTheContaminantSource_NotTheTarget`, `Stripping_RecoversCsCount_EvenCoLocated`: numerical comparisons described above; gate None.
- `PhotoFraction_DecreasesWithEnergy`, `Argmax_RecoversMoreCountsThanPerPixelWindow`: exact / structural fixture comparisons; gate None.

```json
{"id":"Gcam.Tests::Gcam.Tests.ComptonTests","runner":"dotnet","methods":["Scatter_ConservesEnergy_AndStaysWithinKinematicBounds","PhotoFraction_DecreasesWithEnergy","Argmax_RecoversMoreCountsThanPerPixelWindow","Contamination_IsImagedAtTheContaminantSource_NotTheTarget","Stripping_RecoversCsCount_EvenCoLocated"],"sources":{"tests/Gcam.Tests/ComptonTests.cs":"bf84163e272b5be3034a389a1779d91326d5d50eb7db3abef1e310a2e755a59e","tests/Gcam.Tests/Harness/Rigs.cs":"9b89ce04cdcb302fcefc6636d45a0d1640a48c288d1e0a3638b005c44028c3b2","tests/Gcam.Tests/Harness/Stat.cs":"4502dddc6276f9d1de0c41b2ed7246ed7f00c9266daa12a34d6174ae1cb19029"},"trace":[{"requirement":"EV-14","selectors":["Argmax_RecoversMoreCountsThanPerPixelWindow"]},{"requirement":"EV-15","selectors":["Contamination_IsImagedAtTheContaminantSource_NotTheTarget","Stripping_RecoversCsCount_EvenCoLocated"]}]}
```


## ConfigLoaderTests — Gcam.Tests

Configs are the experiment record: every shipped scenario must load into a usable config, and saving or cloning one must not drop a field (a Codex review once found hand-written clones that reverted mask attenuation and crystal material to defaults — AGENTS.md "Cloning configs").

Oracle: All shipped scenarios are structurally usable; serialized JSON equality and mutation isolation test save/load/clone exactly. No approximate numerical tolerance.

Tolerance: exact

Gates: none.

Source: [tests/Gcam.Tests/ConfigLoaderTests.cs](../tests/Gcam.Tests/ConfigLoaderTests.cs).

```json
{"id":"Gcam.Tests::Gcam.Tests.ConfigLoaderTests","runner":"dotnet","methods":["EveryShippedScenario_LoadsIntoAUsableConfig","SaveThenLoad_KeepsEverySection","Clone_IsADeepEqualCopy","Loader_AcceptsCommentsTrailingCommasAndAnyCase"],"sources":{"tests/Gcam.Tests/ConfigLoaderTests.cs":"6a7340d03be468e8151b0adf2848568ebb992f911b4690b2fd55a13b9d9799f6","tests/Gcam.Tests/Harness/Rigs.cs":"9b89ce04cdcb302fcefc6636d45a0d1640a48c288d1e0a3638b005c44028c3b2","tests/Gcam.Tests/Harness/Stat.cs":"4502dddc6276f9d1de0c41b2ed7246ed7f00c9266daa12a34d6174ae1cb19029"},"trace":[]}
```


## CorrelationSearchTests — Gcam.Tests

Checks that matrix-based correlation reproduces decoder maps and peak estimates across accumulator sizes, wide grids and signed or real-valued inputs. Studentised background correlation must have the expected zero mean and unit variance, and empty data must produce no candidate.

Oracle: The retained decoder supplies exact maps, argmax and sub-cell estimates for integer, signed and real inputs. Multinomial pixel moments give the studentised background mean and variance; an empty image supplies the no-candidate fixture.

Tolerance: Multinomial studentisation uses E=N sum(Gp), Var=N(sum(G²p)−sum(Gp)²); sample-mean and fourth-moment variance standard errors feed Stat.Within.

Gates: none.

Source: [tests/Gcam.Tests/CorrelationSearchTests.cs](../tests/Gcam.Tests/CorrelationSearchTests.cs).

Method groups:

- `MatrixDecode_ReproducesTheDecoderExactly`, `MatrixDecode_IsExactOnBothAccumulatorWidths`, `MatrixDecode_IsExactOnAWideGridAndSignedCounts`, `ExpectedMapDecode_ReproducesTheDecoderOnARealImage`, `Search_GivesNoCandidateWithoutCounts`: exact / structural fixture comparisons; gate None.
- `StudentisedCorrelation_HasZeroMeanAndUnitVarianceUnderItsBackground`: numerical comparisons described above; gate None.

```json
{"id":"Gcam.Tests::Gcam.Tests.CorrelationSearchTests","runner":"dotnet","methods":["MatrixDecode_ReproducesTheDecoderExactly","MatrixDecode_IsExactOnBothAccumulatorWidths","MatrixDecode_IsExactOnAWideGridAndSignedCounts","StudentisedCorrelation_HasZeroMeanAndUnitVarianceUnderItsBackground","ExpectedMapDecode_ReproducesTheDecoderOnARealImage","Search_GivesNoCandidateWithoutCounts"],"sources":{"tests/Gcam.Tests/CorrelationSearchTests.cs":"b49e25bf244989d99eeb3c6bc681d85d4f176b532d534b787d4f7563f7487153","tests/Gcam.Tests/Harness/Rigs.cs":"9b89ce04cdcb302fcefc6636d45a0d1640a48c288d1e0a3638b005c44028c3b2","tests/Gcam.Tests/Harness/Stat.cs":"4502dddc6276f9d1de0c41b2ed7246ed7f00c9266daa12a34d6174ae1cb19029"},"trace":[{"requirement":"EV-34","selectors":["ExpectedMapDecode_ReproducesTheDecoderOnARealImage","MatrixDecode_IsExactOnAWideGridAndSignedCounts","MatrixDecode_IsExactOnBothAccumulatorWidths","MatrixDecode_ReproducesTheDecoderExactly","Search_GivesNoCandidateWithoutCounts","StudentisedCorrelation_HasZeroMeanAndUnitVarianceUnderItsBackground"]}]}
```


## CrrcContractTests — Gcam.Tests

Checks CR-RC coefficient selection, preservation of low-energy pulses and signed full-scale state bounds. It also checks invalid-parameter rejection and the configured contract vectors used for optional C# / RTL comparisons.

Oracle: Pinned preset coefficient values and an independently implemented double-precision deconvolution/RC recurrence supply coefficient and pulse references. The convex state bound and explicit signed-unity-stage outputs supply full-scale expectations; generated vectors reuse the C# shaper and do not independently prove RTL agreement.

Tolerance: Floating reference recurrence error is bounded by the deconvolution floor plus one raw unit per RC update accumulated to 1/K; unit-gain cascades sum these bounds. Optional vectors preserve coefficient/output identity.

Gates: none.

Source: [tests/Gcam.Tests/CrrcContractTests.cs](../tests/Gcam.Tests/CrrcContractTests.cs).

```json
{"id":"Gcam.Tests::Gcam.Tests.CrrcContractTests","runner":"dotnet","methods":["Preset_ShapingTimeIsSeparateFromNoiseWindow","PreciseState_RetainsLowEnergyPulseWithinDerivedArithmeticError","SignedFullScale_StaysInsideTheConvexStateBound","ParametersOutsideTheBound_AreRejected","CsharpContractVectors_UseConfiguredCoefficientsAndOptionalExport"],"sources":{"tests/Gcam.Tests/CrrcContractTests.cs":"88e47ddd0f8d14d45fe44a0601fd8b43a3c11cdaf3cf801b2240d73d33f290a6","tests/Gcam.Tests/Harness/Rigs.cs":"9b89ce04cdcb302fcefc6636d45a0d1640a48c288d1e0a3638b005c44028c3b2","tests/Gcam.Tests/Harness/Stat.cs":"4502dddc6276f9d1de0c41b2ed7246ed7f00c9266daa12a34d6174ae1cb19029"},"trace":[]}
```


## CrystalMaterialTests — Gcam.Tests

The crystal interaction tables (theme 52) against independent references.

Oracle: NIST elemental mixtures and xraylib photo/Compton anchors are references.

Tolerance: Comments explain coherent scattering and the Gd edge, but do not derive the 0.92 lower ratio, decimal precisions, photo-fraction interval or 2× edge margin. Unexplained margins: tolerance stated without a derivation in the code.

Gates: none.

Source: [tests/Gcam.Tests/CrystalMaterialTests.cs](../tests/Gcam.Tests/CrystalMaterialTests.cs).

Method groups:

- `Gagg_IsJustBelowNistTotalWithCoherent`, `Lookup_IsCaseInsensitive_AndUnknownFallsBackToGagg`: exact / structural fixture comparisons; gate None.
- `Gagg_At662_MatchesTheNistMixture`, `PhotoFraction_RisesWithZ_AndFallsWithEnergy`, `KEdge_IsAStep_NotASlope`: numerical comparisons described above; gate None.

```json
{"id":"Gcam.Tests::Gcam.Tests.CrystalMaterialTests","runner":"dotnet","methods":["Gagg_IsJustBelowNistTotalWithCoherent","Gagg_At662_MatchesTheNistMixture","PhotoFraction_RisesWithZ_AndFallsWithEnergy","KEdge_IsAStep_NotASlope","Lookup_IsCaseInsensitive_AndUnknownFallsBackToGagg"],"sources":{"tests/Gcam.Tests/CrystalMaterialTests.cs":"5b121d7d5e2cac08b848286e9e8f142744f1487f9b03dbc984787724351a2549","tests/Gcam.Tests/Harness/Rigs.cs":"9b89ce04cdcb302fcefc6636d45a0d1640a48c288d1e0a3638b005c44028c3b2","tests/Gcam.Tests/Harness/Stat.cs":"4502dddc6276f9d1de0c41b2ed7246ed7f00c9266daa12a34d6174ae1cb19029"},"trace":[{"requirement":"EV-19","selectors":["Gagg_At662_MatchesTheNistMixture","Gagg_IsJustBelowNistTotalWithCoherent","KEdge_IsAStep_NotASlope","Lookup_IsCaseInsensitive_AndUnknownFallsBackToGagg","PhotoFraction_RisesWithZ_AndFallsWithEnergy"]}]}
```


## CsIMaterialTests — Gcam.Tests

CsI host transport against xraylib 4.3.0 / NIST; provenance in samples/materials/CsI_checks.txt.

Oracle: CsI has its own measured maximum NIST discrepancy (rounded upward to 0.00011) plus displayed half-units.

Tolerance: Analytic photo anchors/electrons per gram, density propagation and retained decimal half-units are specified in code. Interpolation is the geometric mean; its 12-decimal equality is identified as double arithmetic, without an operation-count derivation. Unexplained margins: tolerance stated without a derivation in the code.

Gates: none.

Source: [tests/Gcam.Tests/CsIMaterialTests.cs](../tests/Gcam.Tests/CsIMaterialTests.cs).

Method groups:

- `TotalWithCoherent_AgreesWithNist`, `AnalyticExtension_StaysBelowNistTotal`, `AnalyticExtension_MatchesDocumentedFormula`, `ReferenceAnchor_UsesCsIHostDensityAndNormalizesAttenuation`, `KEdges_JumpInAttenuationAndPhotoFraction`: exact / structural fixture comparisons; gate None.
- `Table_ClampsAtEndpointsAndInterpolatesLogLogBetweenNodes`: numerical comparisons described above; gate None.

```json
{"id":"Gcam.Tests::Gcam.Tests.CsIMaterialTests","runner":"dotnet","methods":["TotalWithCoherent_AgreesWithNist","AnalyticExtension_StaysBelowNistTotal","AnalyticExtension_MatchesDocumentedFormula","ReferenceAnchor_UsesCsIHostDensityAndNormalizesAttenuation","KEdges_JumpInAttenuationAndPhotoFraction","Table_ClampsAtEndpointsAndInterpolatesLogLogBetweenNodes"],"sources":{"tests/Gcam.Tests/CsIMaterialTests.cs":"2b1fe2719cc53958d630e3263ab432ed691796a099ebcbac7d9a3b4bc9d39c6f","tests/Gcam.Tests/Harness/Rigs.cs":"9b89ce04cdcb302fcefc6636d45a0d1640a48c288d1e0a3638b005c44028c3b2","tests/Gcam.Tests/Harness/Stat.cs":"4502dddc6276f9d1de0c41b2ed7246ed7f00c9266daa12a34d6174ae1cb19029"},"trace":[]}
```


## CsUnderCoTests — Gcam.Tests

TODO-35 (DA-10): the closed-form parts of Cs-137-under-Co-60 stripping — the stripped-count variance, the exact Poisson sampler, the exact Currie limits (against brute force, zero background and the Cornish–Fisher expansion), the gain-shifted window, the ratio identities on response maps and the linearity of the stripped statistic.

Oracle: Factorials, independent PMF enumeration/other summation order, the zero-background −ln(beta) limit, Cornish–Fisher/lattice error, scaled Gaussian windows and map ratio/linearity identities provide the oracles.

Tolerance: Comments explicitly give truncation/rounding, delta-method and four-standard-error budgets; the smoke probability ranges are structural.

Gates: none.

Source: [tests/Gcam.Tests/CsUnderCoTests.cs](../tests/Gcam.Tests/CsUnderCoTests.cs).

Method groups:

- `LogGamma_MatchesFactorials`, `Poisson_HasPoissonMeanVarianceAndTail`, `StrippedCount_VarianceIsN662PlusRSquaredNRef`, `CurrieExact_ZeroBackground_IsMinusLnBeta`, `CurrieExact_MatchesBruteForceEnumeration`, `CurrieExact_LargeMeans_AgreesWithTheOtherSummationOrder`, `ForGain_IsTheWindowOfAScaledPulseHeight`, `StrippingRatio_SameMapStripsToZero_IndependentMapsAgreeWithinTheirMonteCarloError`, `PerPixelRatios_StripTheirOwnMapToZero_GlobalRatioLeavesAnEdgeHeavyResidual`, `StrippedStatistic_IsLinear_ExpectedNullIsZeroEverywhere`, `Family_SelectionThenValidation_RunsAndReportsProbabilities`: numerical comparisons described above; gate None.
- `CurrieExact_ConvergesToCornishFisher`: exact / structural fixture comparisons; gate None.

```json
{"id":"Gcam.Tests::Gcam.Tests.CsUnderCoTests","runner":"dotnet","methods":["LogGamma_MatchesFactorials","Poisson_HasPoissonMeanVarianceAndTail","StrippedCount_VarianceIsN662PlusRSquaredNRef","CurrieExact_ZeroBackground_IsMinusLnBeta","CurrieExact_MatchesBruteForceEnumeration","CurrieExact_LargeMeans_AgreesWithTheOtherSummationOrder","CurrieExact_ConvergesToCornishFisher","ForGain_IsTheWindowOfAScaledPulseHeight","StrippingRatio_SameMapStripsToZero_IndependentMapsAgreeWithinTheirMonteCarloError","PerPixelRatios_StripTheirOwnMapToZero_GlobalRatioLeavesAnEdgeHeavyResidual","StrippedStatistic_IsLinear_ExpectedNullIsZeroEverywhere","Family_SelectionThenValidation_RunsAndReportsProbabilities"],"sources":{"tests/Gcam.Tests/CsUnderCoTests.cs":"fc3027546b8290b6c085f92801e4d33cdb7973517221c61caa4c36732e0a4f12","tests/Gcam.Tests/Harness/Rigs.cs":"9b89ce04cdcb302fcefc6636d45a0d1640a48c288d1e0a3638b005c44028c3b2","tests/Gcam.Tests/Harness/Stat.cs":"4502dddc6276f9d1de0c41b2ed7246ed7f00c9266daa12a34d6174ae1cb19029"},"trace":[{"requirement":"EV-15","selectors":["CurrieExact_ConvergesToCornishFisher","CurrieExact_LargeMeans_AgreesWithTheOtherSummationOrder","CurrieExact_MatchesBruteForceEnumeration","CurrieExact_ZeroBackground_IsMinusLnBeta","Family_SelectionThenValidation_RunsAndReportsProbabilities","ForGain_IsTheWindowOfAScaledPulseHeight","LogGamma_MatchesFactorials","PerPixelRatios_StripTheirOwnMapToZero_GlobalRatioLeavesAnEdgeHeavyResidual","Poisson_HasPoissonMeanVarianceAndTail","StrippedCount_VarianceIsN662PlusRSquaredNRef","StrippedStatistic_IsLinear_ExpectedNullIsZeroEverywhere","StrippingRatio_SameMapStripsToZero_IndependentMapsAgreeWithinTheirMonteCarloError"]}]}
```


## DeadTimeTests — Gcam.Tests

Counting-system dead time: the recorded rate falls below the true rate as the DAQ's per-pulse dead time τ matters.

Oracle: Hand enumerated arrival decisions are exact.

Tolerance: MC rates compare with R/(1+R tau) and R exp(−R tau). Code mentions sparse survivors under extreme load, but does not derive the 5%, 10% or 0.2 turnover margins. Unexplained margins: tolerance stated without a derivation in the code.

Gates: none.

Source: [tests/Gcam.Tests/DeadTimeTests.cs](../tests/Gcam.Tests/DeadTimeTests.cs).

Method groups:

- `ZeroDeadTime_RecordsEverything`, `NonParalyzable_DeadOnlyAfterRecordedEvents`, `Paralyzable_EveryArrivalExtendsTheDeadPeriod`, `Paralyzable_LosesMoreThanNonParalyzable_UnderLoad`: exact / structural fixture comparisons; gate None.
- `MatchesAnalytic_AndParalyzableTurnsOver`: numerical comparisons described above; gate None.

```json
{"id":"Gcam.Tests::Gcam.Tests.DeadTimeTests","runner":"dotnet","methods":["ZeroDeadTime_RecordsEverything","NonParalyzable_DeadOnlyAfterRecordedEvents","Paralyzable_EveryArrivalExtendsTheDeadPeriod","Paralyzable_LosesMoreThanNonParalyzable_UnderLoad","MatchesAnalytic_AndParalyzableTurnsOver"],"sources":{"tests/Gcam.Tests/DeadTimeTests.cs":"ae3aa7aebf226b5b74499a80cd9e72e2c672297b288dd6edd51e25b83a72c769","tests/Gcam.Tests/Harness/Rigs.cs":"9b89ce04cdcb302fcefc6636d45a0d1640a48c288d1e0a3638b005c44028c3b2","tests/Gcam.Tests/Harness/Stat.cs":"4502dddc6276f9d1de0c41b2ed7246ed7f00c9266daa12a34d6174ae1cb19029"},"trace":[{"requirement":"EV-22","selectors":["MatchesAnalytic_AndParalyzableTurnsOver","NonParalyzable_DeadOnlyAfterRecordedEvents","Paralyzable_EveryArrivalExtendsTheDeadPeriod","Paralyzable_LosesMoreThanNonParalyzable_UnderLoad","ZeroDeadTime_RecordsEverything"]}]}
```


## DecoderInvariantTests — Gcam.Tests

The cross-correlation decoder against an ANALYTIC shadow (no Monte Carlo): each pixel is lit iff the ray from the source to its centre crosses an open cell.

Oracle: Analytic shadows, linear superposition and the retained pedestal behavior are the oracles.

Tolerance: Half a projected resolution element is stated as the plateau/argmax limit; decimal reconstruction comparisons have no operation-count error derivation. Unexplained margins: tolerance stated without a derivation in the code.

Gates: none.

Source: [tests/Gcam.Tests/DecoderInvariantTests.cs](../tests/Gcam.Tests/DecoderInvariantTests.cs).

```json
{"id":"Gcam.Tests::Gcam.Tests.DecoderInvariantTests","runner":"dotnet","methods":["AnalyticShadow_PeaksAtItsSource","Decode_IsLinearInTheImage","Cyclic_RejectsAModestPedestal_ButNotAnUnlimitedOne"],"sources":{"tests/Gcam.Tests/DecoderInvariantTests.cs":"227fa3b6935b670ee5abae0e5171bb7553d7f3a9cf64abb4a86a00c5207f51a5","tests/Gcam.Tests/Harness/Rigs.cs":"9b89ce04cdcb302fcefc6636d45a0d1640a48c288d1e0a3638b005c44028c3b2","tests/Gcam.Tests/Harness/Stat.cs":"4502dddc6276f9d1de0c41b2ed7246ed7f00c9266daa12a34d6174ae1cb19029"},"trace":[{"requirement":"EV-01","selectors":["AnalyticShadow_PeaksAtItsSource","Decode_IsLinearInTheImage"]},{"requirement":"EV-12","selectors":["Cyclic_RejectsAModestPedestal_ButNotAnUnlimitedOne"]}]}
```


## DepthDesignTests — Gcam.Tests

Depth-of-field design study: the depth resolution (FWHM of the sharpness-vs-focal curve) must grow with distance — a source far away is harder to place in depth.

Oracle: The oracle is the sharpness-curve trend with distance and aperture rank×mosaic×pitch.

Tolerance: The 1.5× worsening, 150–250 range and one-decimal aperture margin are stated without derivation. Unexplained margins: tolerance stated without a derivation in the code.

Gates: none.

Source: [tests/Gcam.Tests/DepthDesignTests.cs](../tests/Gcam.Tests/DepthDesignTests.cs).

Method groups:

- `DepthResolution_WorsensWithDistance`: numerical comparisons described above; gate None.
- `EffectiveRange_ReadsOffTheCurve`: exact / structural fixture comparisons; gate None.

```json
{"id":"Gcam.Tests::Gcam.Tests.DepthDesignTests","runner":"dotnet","methods":["DepthResolution_WorsensWithDistance","EffectiveRange_ReadsOffTheCurve"],"sources":{"tests/Gcam.Tests/DepthDesignTests.cs":"d8867fd2c3b94ffb5f7897b2f2a1e17a5cad75b3fc396f98bf9264f49314a24b","tests/Gcam.Tests/Harness/Rigs.cs":"9b89ce04cdcb302fcefc6636d45a0d1640a48c288d1e0a3638b005c44028c3b2","tests/Gcam.Tests/Harness/Stat.cs":"4502dddc6276f9d1de0c41b2ed7246ed7f00c9266daa12a34d6174ae1cb19029"},"trace":[{"requirement":"EV-33","selectors":["DepthResolution_WorsensWithDistance","EffectiveRange_ReadsOffTheCurve"]}]}
```


## DepthLocalizationTests — Gcam.Tests

Automatic per-source depth by refocusing (MixedFieldStudy.LocalizeDepths): decode ONE flood at a sweep of focal planes and take each source's sharpest plane (highest peak SNR) as its distance.

Oracle: Known source coordinates are matched to distinct reconstructed peaks.

Tolerance: Code states true depths 160/260 mm and a 10 mm sweep step; it does not derive the lateral ±1 mm or depth ±25 mm margins. Unexplained margins: tolerance stated without a derivation in the code.

Gates: none.

Source: [tests/Gcam.Tests/DepthLocalizationTests.cs](../tests/Gcam.Tests/DepthLocalizationTests.cs).

```json
{"id":"Gcam.Tests::Gcam.Tests.DepthLocalizationTests","runner":"dotnet","methods":["TwoSourcesAtDifferentDistances_RecoveredIn3D"],"sources":{"tests/Gcam.Tests/DepthLocalizationTests.cs":"b1e46b25452f24149732fa90acd928fb518a6cf09e7e145c8c86a599e7779a34","tests/Gcam.Tests/Harness/Rigs.cs":"9b89ce04cdcb302fcefc6636d45a0d1640a48c288d1e0a3638b005c44028c3b2","tests/Gcam.Tests/Harness/Stat.cs":"4502dddc6276f9d1de0c41b2ed7246ed7f00c9266daa12a34d6174ae1cb19029"},"trace":[]}
```


## DepthTests — Gcam.Tests

Coded-aperture depth (z) estimation by refocusing: the near-field distance is recovered from the shadow magnification, and depth resolution degrades with distance.

Oracle: Configured source depth, relative focus widths and comparative joint/3D search errors are the oracles.

Tolerance: The 20 mm and 5 mm error ceilings are stated without derivation. Unexplained margins: tolerance stated without a derivation in the code.

Gates: none.

Source: [tests/Gcam.Tests/DepthTests.cs](../tests/Gcam.Tests/DepthTests.cs).

Method groups:

- `NearFieldDistance_IsRecovered`, `NearFieldDepth_ConvergesWithCounts`, `Joint_LocalizesLateralBetterThanDepth`: numerical comparisons described above; gate None.
- `DepthResolution_DegradesWithDistance`, `Joint3D_BeatsAlternating_OnNearFieldCoupling`: exact / structural fixture comparisons; gate None.

```json
{"id":"Gcam.Tests::Gcam.Tests.DepthTests","runner":"dotnet","methods":["NearFieldDistance_IsRecovered","DepthResolution_DegradesWithDistance","NearFieldDepth_ConvergesWithCounts","Joint_LocalizesLateralBetterThanDepth","Joint3D_BeatsAlternating_OnNearFieldCoupling"],"sources":{"tests/Gcam.Tests/DepthTests.cs":"1f0d339ba6791b2b90269d250eb3a73e2f430a7983177472d90d7897d970a7f5","tests/Gcam.Tests/Harness/Rigs.cs":"9b89ce04cdcb302fcefc6636d45a0d1640a48c288d1e0a3638b005c44028c3b2","tests/Gcam.Tests/Harness/Stat.cs":"4502dddc6276f9d1de0c41b2ed7246ed7f00c9266daa12a34d6174ae1cb19029"},"trace":[{"requirement":"EV-33","selectors":["DepthResolution_DegradesWithDistance","Joint3D_BeatsAlternating_OnNearFieldCoupling","Joint_LocalizesLateralBetterThanDepth","NearFieldDepth_ConvergesWithCounts","NearFieldDistance_IsRecovered"]}]}
```


## DetectorDefectTests — Gcam.Tests

Bad (dead / hot) detector pixels imprint fixed, non-coded structure on the flood that pulls the ideal decoder's correlation off the true peak; a known bad-pixel map repairs most of it by interpolation (the discrete analogue of flood-field correction).

Oracle: Same-seed maps, exclusive dead/hot membership and clean identity are exact/structural.

Tolerance: Fraction and repair comparisons use the requested defect fractions and clean/raw references; LLN is mentioned but the chosen intervals, decimal precision and 1.3/1.4 multipliers are not derived. Unexplained margins: tolerance stated without a derivation in the code.

Gates: none.

Source: [tests/Gcam.Tests/DetectorDefectTests.cs](../tests/Gcam.Tests/DetectorDefectTests.cs).

Method groups:

- `SameSeed_IsDeterministic`, `APixelIsNeverBothDeadAndHot`, `DefectCounts_TrackTheFractions`: exact / structural fixture comparisons; gate None.
- `BadPixels_DegradeThenRepairRecovers`: numerical comparisons described above; gate None.

```json
{"id":"Gcam.Tests::Gcam.Tests.DetectorDefectTests","runner":"dotnet","methods":["SameSeed_IsDeterministic","APixelIsNeverBothDeadAndHot","DefectCounts_TrackTheFractions","BadPixels_DegradeThenRepairRecovers"],"sources":{"tests/Gcam.Tests/DetectorDefectTests.cs":"523818e76979b61636982e4be0a885e397e89a7fa37820a2377d0662357b34e0","tests/Gcam.Tests/Harness/Rigs.cs":"9b89ce04cdcb302fcefc6636d45a0d1640a48c288d1e0a3638b005c44028c3b2","tests/Gcam.Tests/Harness/Stat.cs":"4502dddc6276f9d1de0c41b2ed7246ed7f00c9266daa12a34d6174ae1cb19029"},"trace":[{"requirement":"EV-32","selectors":["APixelIsNeverBothDeadAndHot","BadPixels_DegradeThenRepairRecovers","DefectCounts_TrackTheFractions","SameSeed_IsDeterministic"]}]}
```


## DoiParallaxTests — Gcam.Tests

Depth-of-interaction (DOI) parallax as reconstruction sees it.

Oracle: The comparison is the difference with and without DOI at known angle/thickness.

Tolerance: Code explains depth×tan(angle); the 0.05 mm, 1.5 multiplier and 0.1–1 mm margins are not derived. Unexplained margins: tolerance stated without a derivation in the code.

Gates: none.

Source: [tests/Gcam.Tests/DoiParallaxTests.cs](../tests/Gcam.Tests/DoiParallaxTests.cs).

```json
{"id":"Gcam.Tests::Gcam.Tests.DoiParallaxTests","runner":"dotnet","methods":["DoiAddsAnOffAxisShiftThatGrowsWithThickness_ZeroOnAxis"],"sources":{"tests/Gcam.Tests/DoiParallaxTests.cs":"1f65681feb17dd01c2d709f50bf1a2d59b7e397e40e261bb0d81a87058afff7a","tests/Gcam.Tests/Harness/Rigs.cs":"9b89ce04cdcb302fcefc6636d45a0d1640a48c288d1e0a3638b005c44028c3b2","tests/Gcam.Tests/Harness/Stat.cs":"4502dddc6276f9d1de0c41b2ed7246ed7f00c9266daa12a34d6174ae1cb19029"},"trace":[{"requirement":"EV-13","selectors":["DoiAddsAnOffAxisShiftThatGrowsWithThickness_ZeroOnAxis"]}]}
```


## DoseTests — Gcam.Tests

Dose rate from the detector spectrum (theme 54, TODO-05): ICRP 74 truth, a G(E) weighting fitted on frontal responses, the collimating mask's angular response, and paralyzable over-range.

Oracle: ICRP 74 anchors/log-log interpolation, held-out frontal G(E) responses and paralyzable analytic ratios are the oracles.

Tolerance: The frontal ±25% is a stated product regression target, not a derived uncertainty; decimal, low-reading and live-clock thresholds have no numerical margin derivation. Unexplained margins: tolerance stated without a derivation in the code.

Gates: none.

Source: [tests/Gcam.Tests/DoseTests.cs](../tests/Gcam.Tests/DoseTests.cs).

Method groups:

- `AmbientDose_MatchesIcrp74AtTablePoints`, `FittedWeighting_TracksDose_FrontallyButNotOffAxis`, `OverRange_FollowsParalyzableDeadTime_AndLiveTimeCorrectsUntilItsResolution`: numerical comparisons described above; gate None.
- `AmbientDose_InterpolatesLogLog_AndRejectsOutOfRange`: exact / structural fixture comparisons; gate None.

```json
{"id":"Gcam.Tests::Gcam.Tests.DoseTests","runner":"dotnet","methods":["AmbientDose_MatchesIcrp74AtTablePoints","AmbientDose_InterpolatesLogLog_AndRejectsOutOfRange","FittedWeighting_TracksDose_FrontallyButNotOffAxis","OverRange_FollowsParalyzableDeadTime_AndLiveTimeCorrectsUntilItsResolution"],"sources":{"tests/Gcam.Tests/DoseTests.cs":"c3ad412aa551c9cfc0b80105c500d6821cdb19c0a48dc80eec5afdf1fa9c5c8d","tests/Gcam.Tests/Harness/Rigs.cs":"9b89ce04cdcb302fcefc6636d45a0d1640a48c288d1e0a3638b005c44028c3b2","tests/Gcam.Tests/Harness/Stat.cs":"4502dddc6276f9d1de0c41b2ed7246ed7f00c9266daa12a34d6174ae1cb19029"},"trace":[{"requirement":"EV-23","selectors":["AmbientDose_InterpolatesLogLog_AndRejectsOutOfRange","AmbientDose_MatchesIcrp74AtTablePoints","FittedWeighting_TracksDose_FrontallyButNotOffAxis","OverRange_FollowsParalyzableDeadTime_AndLiveTimeCorrectsUntilItsResolution"]}]}
```


## EmissionKindTests — Gcam.Tests

The emission-line kind is descriptive data on the isotope table: Cs-137's 32.1 / 36.4 keV lines are Ba K X-rays (K-shell fluorescence of the daughter Ba-137m after internal conversion of the 662 keV transition), every other line is a gamma with no X-ray origin, and adding the kind and origin changed no energy or intensity.

Oracle: Explicit isotope-table gamma/X-ray kind and emitter origin plus unchanged energy/intensity fields are exact data fixtures. No approximate numerical tolerance.

Tolerance: exact

Gates: none.

Source: [tests/Gcam.Tests/EmissionKindTests.cs](../tests/Gcam.Tests/EmissionKindTests.cs).

```json
{"id":"Gcam.Tests::Gcam.Tests.EmissionKindTests","runner":"dotnet","methods":["Cs137_BaKLines_AreXRays_AndThePhotopeakIsGamma","EveryOtherLine_DefaultsToGamma","EnergyIntensityPair_ConvertsToAGammaLine"],"sources":{"tests/Gcam.Tests/EmissionKindTests.cs":"ececf42c8faf6b9420529aa2dc0d054a78d421aa84c5c8df21f8661f5444ed79","tests/Gcam.Tests/Harness/Rigs.cs":"9b89ce04cdcb302fcefc6636d45a0d1640a48c288d1e0a3638b005c44028c3b2","tests/Gcam.Tests/Harness/Stat.cs":"4502dddc6276f9d1de0c41b2ed7246ed7f00c9266daa12a34d6174ae1cb19029"},"trace":[{"requirement":"SR-SPEC-05","selectors":["Cs137_BaKLines_AreXRays_AndThePhotopeakIsGamma","EnergyIntensityPair_ConvertsToAGammaLine","EveryOtherLine_DefaultsToGamma"]}]}
```


## EntranceAbsorberTests — Gcam.Tests

The passive entrance absorber (source encapsulation + detector window) must behave like a real low-energy filter: nearly transparent at 662 keV, strongly attenuating at the 32 keV Ba K X-ray, and monotonic in between — so it tames the soft X-ray lines without eating the photopeak.

Oracle: Zero-thickness, monotonic energy and thickness comparisons are structural. 662/32 keV transmission targets (>0.98 and 0.35–0.55) and decimal precisions are stated without derivation.

Tolerance: structural

Gates: none.

Source: [tests/Gcam.Tests/EntranceAbsorberTests.cs](../tests/Gcam.Tests/EntranceAbsorberTests.cs).

Method groups:

- `ZeroThickness_TransmitsEverything`, `BarelyTouches662_ButStronglyAttenuates32`: numerical comparisons described above; gate None.
- `TransmissionIsMonotonicInEnergy`, `ThickerAbsorbs_More`: exact / structural fixture comparisons; gate None.

```json
{"id":"Gcam.Tests::Gcam.Tests.EntranceAbsorberTests","runner":"dotnet","methods":["ZeroThickness_TransmitsEverything","BarelyTouches662_ButStronglyAttenuates32","TransmissionIsMonotonicInEnergy","ThickerAbsorbs_More"],"sources":{"tests/Gcam.Tests/EntranceAbsorberTests.cs":"fac451fe3238a55857377e7c6ffa6f289a7a33e7d3fff6dc54cda11087f9ab39","tests/Gcam.Tests/Harness/Rigs.cs":"9b89ce04cdcb302fcefc6636d45a0d1640a48c288d1e0a3638b005c44028c3b2","tests/Gcam.Tests/Harness/Stat.cs":"4502dddc6276f9d1de0c41b2ed7246ed7f00c9266daa12a34d6174ae1cb19029"},"trace":[]}
```


## EventStreamTests — Gcam.Tests

The MC→RTL event-stream bridge: the crystal-Compton deposit spectrum tapped per event and overlaid with a Poisson arrival process, ready to drive the RTL trapezoidal shaper (cocotb).

Oracle: Incident line energy, requested Poisson arrival rate, photopeak/continuum presence and text round-trip are the oracles.

Tolerance: Normal/isotropic photopeak fractions compare with three combined binomial standard errors; other deposit, rate, count and decimal margins have no derived budget. Unexplained margins: tolerance stated without a derivation in the code.

Gates: none.

Source: [tests/Gcam.Tests/EventStreamTests.cs](../tests/Gcam.Tests/EventStreamTests.cs).

Method groups:

- `Deposits_AreBoundedAndSpanPhotopeakAndContinuum`, `MeanGap_MatchesRequestedRate`: numerical comparisons described above; gate None.
- `Arrivals_AreMonotonic`, `Background_MergesExtraUncodedEvents`, `IsotropicBackground_PhotopeakFraction_MatchesNormalIncidence_InThisSmallCrystal`, `DarkCounts_AddSubKeVPulses`, `Text_RoundTripsHeaderAndColumns`: exact / structural fixture comparisons; gate None.

```json
{"id":"Gcam.Tests::Gcam.Tests.EventStreamTests","runner":"dotnet","methods":["Deposits_AreBoundedAndSpanPhotopeakAndContinuum","Arrivals_AreMonotonic","MeanGap_MatchesRequestedRate","Background_MergesExtraUncodedEvents","IsotropicBackground_PhotopeakFraction_MatchesNormalIncidence_InThisSmallCrystal","DarkCounts_AddSubKeVPulses","Text_RoundTripsHeaderAndColumns"],"sources":{"tests/Gcam.Tests/EventStreamTests.cs":"c6ba6f83fa74fdf2622d66e186cf628194e0569e9865fa0e145d28aa24bf3b9b","tests/Gcam.Tests/Harness/Rigs.cs":"9b89ce04cdcb302fcefc6636d45a0d1640a48c288d1e0a3638b005c44028c3b2","tests/Gcam.Tests/Harness/Stat.cs":"4502dddc6276f9d1de0c41b2ed7246ed7f00c9266daa12a34d6174ae1cb19029"},"trace":[]}
```


## ExponentialIntegralTests — Gcam.Tests

Checks E1 evaluation against independent numerical integration after a change of variables. Non-positive arguments must be rejected.

Oracle: Independent composite Simpson quadrature evaluates E1(x) = integral(exp(-x*exp(s)), s=0..infinity) after substituting mu=exp(-s); explicit non-positive inputs supply rejection fixtures.

Tolerance: Uses 200000 intervals on [0,40]; the comment states error far below 1e-10 relative, supporting the nine-decimal ratio check.

Gates: none.

Source: [tests/Gcam.Tests/ExponentialIntegralTests.cs](../tests/Gcam.Tests/ExponentialIntegralTests.cs).

Method groups:

- `E1_MatchesIndependentQuadrature`: numerical comparisons described above; gate None.
- `E1_RejectsNonPositiveArgument`: exact / structural fixture comparisons; gate None.

```json
{"id":"Gcam.Tests::Gcam.Tests.ExponentialIntegralTests","runner":"dotnet","methods":["E1_MatchesIndependentQuadrature","E1_RejectsNonPositiveArgument"],"sources":{"tests/Gcam.Tests/ExponentialIntegralTests.cs":"0eabfbe4217e63ae01ad8a5acdfa866288dfbf18fcc19f2fc14ad0b9e868928f","tests/Gcam.Tests/Harness/Rigs.cs":"9b89ce04cdcb302fcefc6636d45a0d1640a48c288d1e0a3638b005c44028c3b2","tests/Gcam.Tests/Harness/Stat.cs":"4502dddc6276f9d1de0c41b2ed7246ed7f00c9266daa12a34d6174ae1cb19029"},"trace":[{"requirement":"EV-34","selectors":["E1_MatchesIndependentQuadrature","E1_RejectsNonPositiveArgument"]}]}
```


## FieldOfViewTests — Gcam.Tests

Field of view at field distance (theme 53, TODO-04): non-cyclic decoding localizes beyond the fully coded field, the flood centroid tells the side of an out-of-field source until the aperture's shadow leaves the array (~15° here), and beyond that only the front plate's leak reaches the detector.

Oracle: Square FCFOV angles, source angle/centroid side and non-cyclic/cyclic localization fractions are the references.

Tolerance: The rounded angles and performance-fraction/efficiency thresholds are stated without uncertainty derivation. Unexplained margins: tolerance stated without a derivation in the code.

Gates: none.

Source: [tests/Gcam.Tests/FieldOfViewTests.cs](../tests/Gcam.Tests/FieldOfViewTests.cs).

Method groups:

- `FullyCodedHalfAngle_IsSquare`, `NonCyclic_LocalizesBeyondTheFullyCodedField_AndCentroidGivesTheSide`: numerical comparisons described above; gate None.
- `Run_IsReproducible`, `NegativeAngles_AreRejected`: exact / structural fixture comparisons; gate None.

```json
{"id":"Gcam.Tests::Gcam.Tests.FieldOfViewTests","runner":"dotnet","methods":["FullyCodedHalfAngle_IsSquare","NonCyclic_LocalizesBeyondTheFullyCodedField_AndCentroidGivesTheSide","Run_IsReproducible","NegativeAngles_AreRejected"],"sources":{"tests/Gcam.Tests/FieldOfViewTests.cs":"acccdb2cd314f0786f7ea2a61248e4bc12226c062c49fd834f7c3c96ea416f5b","tests/Gcam.Tests/Harness/Rigs.cs":"9b89ce04cdcb302fcefc6636d45a0d1640a48c288d1e0a3638b005c44028c3b2","tests/Gcam.Tests/Harness/Stat.cs":"4502dddc6276f9d1de0c41b2ed7246ed7f00c9266daa12a34d6174ae1cb19029"},"trace":[{"requirement":"EV-02","selectors":["FullyCodedHalfAngle_IsSquare","NegativeAngles_AreRejected","NonCyclic_LocalizesBeyondTheFullyCodedField_AndCentroidGivesTheSide","Run_IsReproducible"]}]}
```


## FiniteSourceTests — Gcam.Tests

Finite (extended) source + capsule self-attenuation.

Oracle: Point/small/large source reconstructions and increasing capsule thickness are compared; source broadening and preferential low-energy attenuation are qualitative oracles.

Tolerance: Fixed FWHM, contrast and transmission margins are not derived. Unexplained margins: tolerance stated without a derivation in the code.

Gates: none.

Source: [tests/Gcam.Tests/FiniteSourceTests.cs](../tests/Gcam.Tests/FiniteSourceTests.cs).

```json
{"id":"Gcam.Tests::Gcam.Tests.FiniteSourceTests","runner":"dotnet","methods":["SourceSize_BlursTheRecon_ThenWashesItOut","Capsule_SuppressesTheLowEnergyLineMuchMoreThanThePrimary"],"sources":{"tests/Gcam.Tests/FiniteSourceTests.cs":"93ca6a64fa7246542d75173077109dd0e567e99f38b6070e2df4f8af322f414e","tests/Gcam.Tests/Harness/Rigs.cs":"9b89ce04cdcb302fcefc6636d45a0d1640a48c288d1e0a3638b005c44028c3b2","tests/Gcam.Tests/Harness/Stat.cs":"4502dddc6276f9d1de0c41b2ed7246ed7f00c9266daa12a34d6174ae1cb19029"},"trace":[]}
```


## FloodLutTests — Gcam.Tests

TODO-19 crystal identification: the flood-map look-up table orders its markers into the crystal grid, generalises from a calibration flood to an independent flood (train / test split) and fails explicitly — never forced into a grid — when the flood is unresolved.

Oracle: Synthetic Gaussian spots on a known, barrel-distorted 6 × 6 grid (true centres known) from independent random streams; a transported readout flood (6 × 6 array, ideal divider, 662 keV) with independent transport, light and noise streams for calibration and test; uniform and merged floods for the failure paths.

Tolerance: Marker position within 0.02 raw units: two histogram bins (2/192) plus the centroid spread σ/√400, derived in the code comment. Train vs test mis-identification within k = 4 standard errors of the difference of two binomial fractions at the pooled rate (Stat.Within). Failure messages and unassigned look-ups are exact.

Gates: none.

Source: [tests/Gcam.Tests/FloodLutTests.cs](../tests/Gcam.Tests/FloodLutTests.cs).

Method groups:

- `SeparatedSpots_AreOrderedIntoTheCrystalGrid`: numerical comparison with the derived marker bound; gate None.
- `TrainTestSplit_GivesStatisticallyEqualMisIdentification`, `TransportedFlood_CalibrationGeneralisesToAnIndependentFlood`: binomial k·σ comparison of independent floods; gate None.
- `UnresolvedFlood_FailsExplicitly`, `MergedSpots_FailInsteadOfBeingForcedIntoAGrid`, `PointsOutsideThePlane_AreUnassigned`: exact / structural fixture comparisons; gate None.

```json
{"id":"Gcam.Tests::Gcam.Tests.FloodLutTests","runner":"dotnet","methods":["SeparatedSpots_AreOrderedIntoTheCrystalGrid","TrainTestSplit_GivesStatisticallyEqualMisIdentification","UnresolvedFlood_FailsExplicitly","MergedSpots_FailInsteadOfBeingForcedIntoAGrid","PointsOutsideThePlane_AreUnassigned","TransportedFlood_CalibrationGeneralisesToAnIndependentFlood"],"sources":{"tests/Gcam.Tests/FloodLutTests.cs":"683409d9e632b50bdcce13069c4bb6f3830a7dba96199ec3f174e47114e0ab23","tests/Gcam.Tests/Harness/Rigs.cs":"9b89ce04cdcb302fcefc6636d45a0d1640a48c288d1e0a3638b005c44028c3b2","tests/Gcam.Tests/Harness/Stat.cs":"4502dddc6276f9d1de0c41b2ed7246ed7f00c9266daa12a34d6174ae1cb19029"},"trace":[]}
```

## FocusFusionTests — Gcam.Tests

Focus fusion: cross-check the laser rangefinder against depth-from-focus.

Oracle: The configured source plane and correct/wrong laser plane define comparative focus/SNR expectations.

Tolerance: The 240–380, <450 mm and 1.2× criteria are tolerance stated without a derivation in the code.

Gates: none.

Source: [tests/Gcam.Tests/FocusFusionTests.cs](../tests/Gcam.Tests/FocusFusionTests.cs).

```json
{"id":"Gcam.Tests::Gcam.Tests.FocusFusionTests","runner":"dotnet","methods":["Consistent_WhenLaserMatchesSource","FlagsMismatch_WhenLaserHitsWrongSurface"],"sources":{"tests/Gcam.Tests/FocusFusionTests.cs":"a538a06b3e953ae0708cdffe5c60038ce6f96e1121bc7ef7eb752b4cbb974c7f","tests/Gcam.Tests/Harness/Rigs.cs":"9b89ce04cdcb302fcefc6636d45a0d1640a48c288d1e0a3638b005c44028c3b2","tests/Gcam.Tests/Harness/Stat.cs":"4502dddc6276f9d1de0c41b2ed7246ed7f00c9266daa12a34d6174ae1cb19029"},"trace":[]}
```


## FrontEndPartsTests — Gcam.Tests

Checks that scintillator, sensor and preamplifier presets build the retained front-end configuration and select the expected startup parts. Pulse rise/tail sample counts must reproduce the legacy calculation and its minimum-sample guards.

Oracle: Preset legacy config and startup selection plus the explicit legacy convolution/sample guard calculation are reference expectations.

Tolerance: Exact/structural; rounded pulse duration, where coded, retains its stated precision without an error-budget derivation. Unexplained margins: tolerance stated without a derivation in the code.

Gates: none.

Source: [tests/Gcam.Tests/FrontEndPartsTests.cs](../tests/Gcam.Tests/FrontEndPartsTests.cs).

```json
{"id":"Gcam.Tests::Gcam.Tests.FrontEndPartsTests","runner":"dotnet","methods":["Presets_BuildTheLegacyConfig_DefaultMatchesStartupSelection","PulseSamples_MatchesLegacyConvolutionAndSampleGuards"],"sources":{"tests/Gcam.Tests/FrontEndPartsTests.cs":"549c71618ddfe1d2c093988b27a2427d6f84902c52ad3fe5aa68e8c41f05cc8c","tests/Gcam.Tests/Harness/Rigs.cs":"9b89ce04cdcb302fcefc6636d45a0d1640a48c288d1e0a3638b005c44028c3b2","tests/Gcam.Tests/Harness/Stat.cs":"4502dddc6276f9d1de0c41b2ed7246ed7f00c9266daa12a34d6174ae1cb19029"},"trace":[{"requirement":"SR-SPEC-02","selectors":["Presets_BuildTheLegacyConfig_DefaultMatchesStartupSelection","PulseSamples_MatchesLegacyConvolutionAndSampleGuards"]}]}
```


## FrontEndTests — Gcam.Tests

The physical SiPM front-end model (port of rtl/frontend_model.py) folded into the C# pipeline: energy resolution from the photoelectron budget, energy-dependent (1/√E), and its effect on the crystal-Compton detector's energy discrimination.

Oracle: Photoelectron-budget resolution, 1/sqrt(E) and 1/E terms are formula oracles.

Tolerance: Comments compute 8043 photoelectrons and 4.30% FWHM; the decimal precisions, sampled mean/spread and DCR/window margins have no error-budget derivation. Unexplained margins: tolerance stated without a derivation in the code.

Gates: none.

Source: [tests/Gcam.Tests/FrontEndTests.cs](../tests/Gcam.Tests/FrontEndTests.cs).

```json
{"id":"Gcam.Tests::Gcam.Tests.FrontEndTests","runner":"dotnet","methods":["Fwhm_MatchesPhotoelectronBudget_At662","Fwhm_ImprovesWithEnergy_AsOneOverSqrtE","Measure_IsUnbiased_WithFwhmSpread","Dcr_IsParallelNoise_ScalingOneOverE","Dcr_TermConcentratesAtLowEnergy_AndRaisesTotal","FrontEnd_BroadensPhotopeak_ShrinkingTightWindowCounts"],"sources":{"tests/Gcam.Tests/FrontEndTests.cs":"491c386218f08ea5cb95c1671927815c28fd28ac309dd621fec481adb384fbc4","tests/Gcam.Tests/Harness/Rigs.cs":"9b89ce04cdcb302fcefc6636d45a0d1640a48c288d1e0a3638b005c44028c3b2","tests/Gcam.Tests/Harness/Stat.cs":"4502dddc6276f9d1de0c41b2ed7246ed7f00c9266daa12a34d6174ae1cb19029"},"trace":[{"requirement":"EV-17","selectors":["Dcr_IsParallelNoise_ScalingOneOverE","Dcr_TermConcentratesAtLowEnergy_AndRaisesTotal","FrontEnd_BroadensPhotopeak_ShrinkingTightWindowCounts","Fwhm_ImprovesWithEnergy_AsOneOverSqrtE","Fwhm_MatchesPhotoelectronBudget_At662","Measure_IsUnbiased_WithFwhmSpread"]}]}
```


## GateResponseTests — Gcam.Tests

Checks Gaussian energy-window acceptance and source/ambient rate maps. The CDF must agree with an independent erf series, open windows must recover the process rate and branching intensity must scale the source response.

Oracle: An independent erf Maclaurin series supplies Gaussian CDF/window probabilities. Same-seed engine transport supplies open-window process totals and branching-ratio scaling, so those map identities reuse the transport implementation rather than an independent physics model. The documented erfc error gives the retained 1.2e-7 absolute CDF and 2.4e-7 CDF-difference budgets.

Tolerance: Open-window and branching comparisons reuse the engine transport and are exact identities up to stated summation rounding.

Gates: none.

Source: [tests/Gcam.Tests/GateResponseTests.cs](../tests/Gcam.Tests/GateResponseTests.cs).

```json
{"id":"Gcam.Tests::Gcam.Tests.GateResponseTests","runner":"dotnet","methods":["NormalCdf_AgreesWithTheErfSeries","Acceptance_IsTheWindowProbabilityOfTheSmearedDeposit","AmbientOpenWindowRate_IsTheProcessDetectedRate","AmbientMap_RefusesTheNotValidatedSpectrum","SourceRate_ScalesWithBranchingAndSplitsAcrossWindows"],"sources":{"tests/Gcam.Tests/GateResponseTests.cs":"548d2f526c3d98984d2ac7aca3446dd67ce9bae4e250e72804e696e63dfa9d4c","tests/Gcam.Tests/Harness/Rigs.cs":"9b89ce04cdcb302fcefc6636d45a0d1640a48c288d1e0a3638b005c44028c3b2","tests/Gcam.Tests/Harness/Stat.cs":"4502dddc6276f9d1de0c41b2ed7246ed7f00c9266daa12a34d6174ae1cb19029"},"trace":[{"requirement":"EV-34","selectors":["Acceptance_IsTheWindowProbabilityOfTheSmearedDeposit","AmbientMap_RefusesTheNotValidatedSpectrum","AmbientOpenWindowRate_IsTheProcessDetectedRate","NormalCdf_AgreesWithTheErfSeries","SourceRate_ScalesWithBranchingAndSplitsAcrossWindows"]}]}
```


## HalfSpaceUncollidedTests — Gcam.Tests

Checks the uncollided fluence and zenith distribution from a uniform emitting half-space with air attenuation. Zero-air attenuation must preserve infinite-half-space normalization and the expected density/mass-unit cancellation.

Oracle: Analog air-survival MC is compared with the analytic half-space integral; six binomial standard errors are scaled by the physical normalization. Zero-air and mass-unit relations are structural/exact.

Tolerance: exact

Gates: none.

Source: [tests/Gcam.Tests/HalfSpaceUncollidedTests.cs](../tests/Gcam.Tests/HalfSpaceUncollidedTests.cs).

Method groups:

- `UniformHalfSpace_FluenceAndZenithMatchIndependentAnalogAirSurvival`: numerical comparisons described above; gate None.
- `ZeroAirAttenuation_RetainsInfiniteHalfSpaceNormalizationAndMassUnits`: exact / structural fixture comparisons; gate None.

```json
{"id":"Gcam.Tests::Gcam.Tests.HalfSpaceUncollidedTests","runner":"dotnet","methods":["UniformHalfSpace_FluenceAndZenithMatchIndependentAnalogAirSurvival","ZeroAirAttenuation_RetainsInfiniteHalfSpaceNormalizationAndMassUnits"],"sources":{"tests/Gcam.Tests/HalfSpaceUncollidedTests.cs":"47ceba497a65a8c709ab9c1af428a3850e3276674d390dae7aa0fd210904f944","tests/Gcam.Tests/Harness/Rigs.cs":"9b89ce04cdcb302fcefc6636d45a0d1640a48c288d1e0a3638b005c44028c3b2","tests/Gcam.Tests/Harness/Stat.cs":"4502dddc6276f9d1de0c41b2ed7246ed7f00c9266daa12a34d6174ae1cb19029"},"trace":[]}
```


## IncidentSpectrumFileTests — Gcam.Tests

Checks that incident-spectrum files and sidecars pin the exact LF bytes stored by Git, and that the development placeholder regenerates deterministically. Writers must reject carriage returns or overwrites, and spectrum references must resolve only with the pinned hash.

Oracle: Pinned stored bytes, deterministic regenerated placeholder and exact hash-resolved references supply the oracle.

Tolerance: CR/overwrite/hash refusal is structural; no numerical tolerance.

Gates: none.

Source: [tests/Gcam.Tests/IncidentSpectrumFileTests.cs](../tests/Gcam.Tests/IncidentSpectrumFileTests.cs).

```json
{"id":"Gcam.Tests::Gcam.Tests.IncidentSpectrumFileTests","runner":"dotnet","methods":["PinnedAmbientFiles_HashTheBytesGitStores","DevelopmentPlaceholder_RegeneratesTheCommittedBytes","WriteWithSidecar_RefusesCarriageReturnsAndOverwrites","SpectrumReference_ResolvesOnlyWithItsPinnedHash"],"sources":{"tests/Gcam.Tests/IncidentSpectrumFileTests.cs":"f0db7272a6728a443baae7475c35e906ceb83dc6afa8d9aee0aeac1e30e2c785","tests/Gcam.Tests/Harness/Rigs.cs":"9b89ce04cdcb302fcefc6636d45a0d1640a48c288d1e0a3638b005c44028c3b2","tests/Gcam.Tests/Harness/Stat.cs":"4502dddc6276f9d1de0c41b2ed7246ed7f00c9266daa12a34d6174ae1cb19029"},"trace":[{"requirement":"EV-34","selectors":["DevelopmentPlaceholder_RegeneratesTheCommittedBytes","PinnedAmbientFiles_HashTheBytesGitStores","SpectrumReference_ResolvesOnlyWithItsPinnedHash","WriteWithSidecar_RefusesCarriageReturnsAndOverwrites"]}]}
```


## Ir192Tests — Gcam.Tests

Ir-192 (industrial radiography, URS reference source RS-1): the line table, the tungsten attenuation it relies on between 200 and 620 keV, the explicit "cascade not modelled" guard, and imaging through the full pipeline.

Oracle: ENSDF line/half-life data and NIST tungsten ratios are the external tabular oracles.

Tolerance: Position checks use one projected resolution element (the comment explains the coarser grid). The one-percent attenuation and tabular decimal margins lack a derived error budget. Unexplained margins: tolerance stated without a derivation in the code.

Gates: none.

Source: [tests/Gcam.Tests/Ir192Tests.cs](../tests/Gcam.Tests/Ir192Tests.cs).

Method groups:

- `LineTable_MatchesEnsdf_PrimaryLineFirst`, `TungstenMuRel_MatchesNist_Between200And600keV`, `TungstenMuRel_AtIr192Lines_IsWithinOnePercentOfNist`: numerical comparisons described above; gate None.
- `CascadeStudy_RefusesIr192_InsteadOfSilentlyRunningCs137`, `CascadeStudy_RefusesEveryUnmodelledName`, `CascadeStudy_AcceptsTheModelledNames`, `Ir192Source_LocalisesThroughTheFullPipeline_AsWellAsCs137`, `Ir192_ThroughA316keVWindow_StillLocalises`: exact / structural fixture comparisons; gate None.

```json
{"id":"Gcam.Tests::Gcam.Tests.Ir192Tests","runner":"dotnet","methods":["LineTable_MatchesEnsdf_PrimaryLineFirst","TungstenMuRel_MatchesNist_Between200And600keV","TungstenMuRel_AtIr192Lines_IsWithinOnePercentOfNist","CascadeStudy_RefusesIr192_InsteadOfSilentlyRunningCs137","CascadeStudy_RefusesEveryUnmodelledName","CascadeStudy_AcceptsTheModelledNames","Ir192Source_LocalisesThroughTheFullPipeline_AsWellAsCs137","Ir192_ThroughA316keVWindow_StillLocalises"],"sources":{"tests/Gcam.Tests/Ir192Tests.cs":"dafa88ad97431d5d0e0bb9b72ce2f0bafc23cf740501bd88579fd554d485f3ce","tests/Gcam.Tests/Harness/Rigs.cs":"9b89ce04cdcb302fcefc6636d45a0d1640a48c288d1e0a3638b005c44028c3b2","tests/Gcam.Tests/Harness/Stat.cs":"4502dddc6276f9d1de0c41b2ed7246ed7f00c9266daa12a34d6174ae1cb19029"},"trace":[{"requirement":"EV-20","selectors":["CascadeStudy_AcceptsTheModelledNames","CascadeStudy_RefusesEveryUnmodelledName","CascadeStudy_RefusesIr192_InsteadOfSilentlyRunningCs137","Ir192Source_LocalisesThroughTheFullPipeline_AsWellAsCs137","Ir192_ThroughA316keVWindow_StillLocalises","LineTable_MatchesEnsdf_PrimaryLineFirst","TungstenMuRel_AtIr192Lines_IsWithinOnePercentOfNist","TungstenMuRel_MatchesNist_Between200And600keV"]}]}
```


## KleinNishinaTests — Gcam.Tests

Checks sampled Compton scattering angles against the Klein-Nishina angular distribution at several photon energies and enforces the Compton energy-angle relation. Rotating a sampled direction must preserve unit length and the requested scattering angle.

Oracle: Composite Simpson integration of the analytic Klein-Nishina density supplies angular-bin probabilities. The Compton energy-angle equation, vector length and dot product with the incident direction supply sample and rotation references. Angular-bin comparisons retain four binomial standard errors.

Tolerance: Comments identify rounding in energy ratio/cosine, but do not derive the selected decimal precisions for rotation. Unexplained margins: tolerance stated without a derivation in the code.

Gates: none.

Source: [tests/Gcam.Tests/KleinNishinaTests.cs](../tests/Gcam.Tests/KleinNishinaTests.cs).

```json
{"id":"Gcam.Tests::Gcam.Tests.KleinNishinaTests","runner":"dotnet","methods":["Sample_MatchesKleinNishinaAngularDistribution","Rotate_KeepsUnitLengthAndScatteringAngle"],"sources":{"tests/Gcam.Tests/KleinNishinaTests.cs":"7d29444c9ee9fd986af6bb2a20e0ffb2709bcacc340b136086361ce6b746f47d","tests/Gcam.Tests/Harness/Rigs.cs":"9b89ce04cdcb302fcefc6636d45a0d1640a48c288d1e0a3638b005c44028c3b2","tests/Gcam.Tests/Harness/Stat.cs":"4502dddc6276f9d1de0c41b2ed7246ed7f00c9266daa12a34d6174ae1cb19029"},"trace":[{"requirement":"EV-34","selectors":["Rotate_KeepsUnitLengthAndScatteringAngle","Sample_MatchesKleinNishinaAngularDistribution"]}]}
```


## ListModeBackgroundTests — Gcam.Tests

Checks that adding relative background preserves every source event while changing total rate and the background pixel/deposit distribution as configured. Zero background must replay the source stream exactly, and seeds and cancellation must remain deterministic.

Oracle: The retained source-only stream supplies exact event identities; configured BSR supplies the rate ratio. Background-model pixel weights and deposit counts supply distribution references, with both finite samples contributing uncertainty.

Tolerance: Total/source rate and background pixel/deposit distributions use Poisson/binomial and weighted-reference standard errors, with k=4 or 5 as coded. Reference zero-count bins use the conservative upper rate described in code.

Gates: none.

Source: [tests/Gcam.Tests/ListModeBackgroundTests.cs](../tests/Gcam.Tests/ListModeBackgroundTests.cs).

```json
{"id":"Gcam.Tests::Gcam.Tests.ListModeBackgroundTests","runner":"dotnet","methods":["ZeroBsr_PreservesSourceStreamBitForBit","BsrOne_DoublesRate_PreservesEverySourceEvent_AndMatchesBackgroundModel","Background_SeedAndCancellationAreDeterministic"],"sources":{"tests/Gcam.Tests/ListModeBackgroundTests.cs":"fb151ffe8f3250ab47f726ae24074c13498155933b01d11a4ed07d92a3ecd4d8","tests/Gcam.Tests/Harness/Rigs.cs":"9b89ce04cdcb302fcefc6636d45a0d1640a48c288d1e0a3638b005c44028c3b2","tests/Gcam.Tests/Harness/Stat.cs":"4502dddc6276f9d1de0c41b2ed7246ed7f00c9266daa12a34d6174ae1cb19029"},"trace":[{"requirement":"SR-RUN-22","selectors":["Background_SeedAndCancellationAreDeterministic","BsrOne_DoublesRate_PreservesEverySourceEvent_AndMatchesBackgroundModel","ZeroBsr_PreservesSourceStreamBitForBit"]}]}
```


## ListModeSourceTests — Gcam.Tests

Checks that accepted list-mode photons reproduce independently weighted detector floods, energy-deposit spectra, physical event rates and exponential arrival gaps. It also checks unequal-distance rejection, pixel/energy sink consistency, deterministic seeds, cancellation and measured throughput.

Oracle: Independent weighted Compton floods and weight-resampled event streams supply pixel and deposit distributions. Physical efficiency/emission rate and unit-exponential moments/CDF supply rate and gap expectations; retained transport sinks supply legacy replay references. Weighted-reference uncertainty retains w²<=w_max*w and the stated resampling-pool bound.

Tolerance: Physical rate and exponential gap moments/CDF use four standard errors; pixels use five. Performance timings are measurement only. The acceptance<=0.1 fixture ceiling is not statistically derived. Unexplained margins: tolerance stated without a derivation in the code.

Gates: none.

Source: [tests/Gcam.Tests/ListModeSourceTests.cs](../tests/Gcam.Tests/ListModeSourceTests.cs).

Method groups:

- `PixelHistogram_MatchesIndependentWeightedComptonFlood`, `Deposits_MatchIndependentWeightResampledEventStream`, `RateAndGaps_MatchPhysicalEfficiencyAndExponentialLaw`, `MixedDistances_RejectionPreservesWeightedFloodAndTotalEmissionRate`, `SeedAndCancellation_AreDeterministicAndPrompt`: numerical comparisons described above; gate None.
- `PixelSink_PreservesLegacyEnergySinkAndTransport_WithOpticalCrosstalk`, `Performance_RecordAcceptedThroughputAcceptanceAndDecode`: exact / structural fixture comparisons; gate None.

```json
{"id":"Gcam.Tests::Gcam.Tests.ListModeSourceTests","runner":"dotnet","methods":["PixelHistogram_MatchesIndependentWeightedComptonFlood","Deposits_MatchIndependentWeightResampledEventStream","RateAndGaps_MatchPhysicalEfficiencyAndExponentialLaw","MixedDistances_RejectionPreservesWeightedFloodAndTotalEmissionRate","PixelSink_PreservesLegacyEnergySinkAndTransport_WithOpticalCrosstalk","SeedAndCancellation_AreDeterministicAndPrompt","Performance_RecordAcceptedThroughputAcceptanceAndDecode"],"sources":{"tests/Gcam.Tests/ListModeSourceTests.cs":"c3a231eaec771696a0ae25626422dbe46501d11b0207ee9275a2959574b6fe9b","tests/Gcam.Tests/Harness/Rigs.cs":"9b89ce04cdcb302fcefc6636d45a0d1640a48c288d1e0a3638b005c44028c3b2","tests/Gcam.Tests/Harness/Stat.cs":"4502dddc6276f9d1de0c41b2ed7246ed7f00c9266daa12a34d6174ae1cb19029"},"trace":[{"requirement":"SR-RUN-16","selectors":["Deposits_MatchIndependentWeightResampledEventStream","MixedDistances_RejectionPreservesWeightedFloodAndTotalEmissionRate","Performance_RecordAcceptedThroughputAcceptanceAndDecode","PixelHistogram_MatchesIndependentWeightedComptonFlood","PixelSink_PreservesLegacyEnergySinkAndTransport_WithOpticalCrosstalk","RateAndGaps_MatchPhysicalEfficiencyAndExponentialLaw","SeedAndCancellation_AreDeterministicAndPrompt"]}]}
```


## MaskAttenuationTests — Gcam.Tests

The coded mask's tungsten attenuation μ(E)/μ(662).

Oracle: 662 keV normalization, high/low-energy ordering and endpoint clamping are the oracles.

Tolerance: The >20 and 40–60 attenuation targets have no stated margin derivation. Unexplained margins: tolerance stated without a derivation in the code.

Gates: none.

Source: [tests/Gcam.Tests/MaskAttenuationTests.cs](../tests/Gcam.Tests/MaskAttenuationTests.cs).

```json
{"id":"Gcam.Tests::Gcam.Tests.MaskAttenuationTests","runner":"dotnet","methods":["TungstenMu_IsAnchoredAt662_AndFlatteningAtHighEnergy","TungstenMu_RisesBelow122keV_ForSoftLines"],"sources":{"tests/Gcam.Tests/MaskAttenuationTests.cs":"b85f99e6ada725f7bf1ad5d541839917de2cad29977ede1723eb8883654e1324","tests/Gcam.Tests/Harness/Rigs.cs":"9b89ce04cdcb302fcefc6636d45a0d1640a48c288d1e0a3638b005c44028c3b2","tests/Gcam.Tests/Harness/Stat.cs":"4502dddc6276f9d1de0c41b2ed7246ed7f00c9266daa12a34d6174ae1cb19029"},"trace":[{"requirement":"EV-04","selectors":["TungstenMu_IsAnchoredAt662_AndFlatteningAtHighEnergy","TungstenMu_RisesBelow122keV_ForSoftLines"]}]}
```


## MaskFabricationTests — Gcam.Tests

Mask fabrication tolerances: a real tungsten mask is not the ideal MURA the decoder assumes (mis-placed / mis-sized holes, blocked cells, depth drill wander through a thick slab).

Oracle: Same-seed hole geometry is exact; transmission is compared with the ideal mask.

Tolerance: The degradation t statistic uses 24 paired seeds, k=3, measured effect size and noncentral-t false-failure probability quoted in code. Wander, blocked-transmission, efficiency<0.97 and decimal margins are not derived. Unexplained margins: tolerance stated without a derivation in the code.

Gates: none.

Source: [tests/Gcam.Tests/MaskFabricationTests.cs](../tests/Gcam.Tests/MaskFabricationTests.cs).

```json
{"id":"Gcam.Tests::Gcam.Tests.MaskFabricationTests","runner":"dotnet","methods":["SameSeed_IsDeterministic","Wander_DriftsTheHoleCentreWithDepth","HoleHalfWidth_FollowsNominalFraction","BlockedCells_ReduceTransmission","LooserTolerance_DegradesLocalization"],"sources":{"tests/Gcam.Tests/MaskFabricationTests.cs":"163c8e7517978e6ed11e631c7e28961a84a61081dd5309f026a5e34922578cbe","tests/Gcam.Tests/Harness/Rigs.cs":"9b89ce04cdcb302fcefc6636d45a0d1640a48c288d1e0a3638b005c44028c3b2","tests/Gcam.Tests/Harness/Stat.cs":"4502dddc6276f9d1de0c41b2ed7246ed7f00c9266daa12a34d6174ae1cb19029"},"trace":[{"requirement":"EV-30","selectors":["BlockedCells_ReduceTransmission","HoleHalfWidth_FollowsNominalFraction","LooserTolerance_DegradesLocalization","SameSeed_IsDeterministic","Wander_DriftsTheHoleCentreWithDepth"]}]}
```


## MaskGeometryTests — Gcam.Tests

Mask channel geometry: a smaller hole strictly loses sensitivity, and focused (converging) channels concentrate on the focal point.

Oracle: Hole/focus/taper/pitch alternatives are compared at the same seeded settings.

Tolerance: Relative ordering is structural; the straight-edge<0.95 margin is stated without derivation. Unexplained margins: tolerance stated without a derivation in the code.

Gates: none.

Source: [tests/Gcam.Tests/MaskGeometryTests.cs](../tests/Gcam.Tests/MaskGeometryTests.cs).

Method groups:

- `SmallerHole_LosesEfficiency`, `FocusedChannels_ConcentrateOnFocalPoint`, `CellSize_HasABoundedOptimum`: exact / structural fixture comparisons; gate None.
- `TaperedChannels_WidenTheFovOfAThickMask`: numerical comparisons described above; gate None.

```json
{"id":"Gcam.Tests::Gcam.Tests.MaskGeometryTests","runner":"dotnet","methods":["SmallerHole_LosesEfficiency","FocusedChannels_ConcentrateOnFocalPoint","TaperedChannels_WidenTheFovOfAThickMask","CellSize_HasABoundedOptimum"],"sources":{"tests/Gcam.Tests/MaskGeometryTests.cs":"8fbbfc591df9474f532c943975d21c885fa7b05f469bbe54a1acdeca3b338032","tests/Gcam.Tests/Harness/Rigs.cs":"9b89ce04cdcb302fcefc6636d45a0d1640a48c288d1e0a3638b005c44028c3b2","tests/Gcam.Tests/Harness/Stat.cs":"4502dddc6276f9d1de0c41b2ed7246ed7f00c9266daa12a34d6174ae1cb19029"},"trace":[{"requirement":"EV-05","selectors":["TaperedChannels_WidenTheFovOfAThickMask"]}]}
```


## MaskScatterTests — Gcam.Tests

Checks the forward-scattered mask contribution to the coded image as the mask-detector gap and energy window change. Balanced decoding must suppress the smooth scatter pedestal while retaining source localization.

Oracle: Gap, energy-window and balanced-decoder alternatives are compared against primary-only counts/confidence and the known source.

Tolerance: Fixed contamination, energy, confidence and localization margins are not derived. Unexplained margins: tolerance stated without a derivation in the code.

Gates: none.

Source: [tests/Gcam.Tests/MaskScatterTests.cs](../tests/Gcam.Tests/MaskScatterTests.cs).

```json
{"id":"Gcam.Tests::Gcam.Tests.MaskScatterTests","runner":"dotnet","methods":["Contamination_IsSmall_AndFallsAsTheGapWidens","EnergyWindow_RemovesTheDownShiftedScatter_ButNotTheForwardTail","BalancedDecoder_LargelyRejectsTheSmoothPedestal"],"sources":{"tests/Gcam.Tests/MaskScatterTests.cs":"8942aab2e4f654b40353b5f165edba6c133d524d0da9d4fe938660ea979a5b2a","tests/Gcam.Tests/Harness/Rigs.cs":"9b89ce04cdcb302fcefc6636d45a0d1640a48c288d1e0a3638b005c44028c3b2","tests/Gcam.Tests/Harness/Stat.cs":"4502dddc6276f9d1de0c41b2ed7246ed7f00c9266daa12a34d6174ae1cb19029"},"trace":[]}
```


## MaskSecondaryTests — Gcam.Tests

Mask tungsten secondaries a pure-attenuation mask omits: Compton scatter (dominant at 662 keV) and W K-fluorescence (59/67 keV, only above the K-edge, heavily self-absorbed).

Oracle: Energy-dependent W photoelectric shares, K-edge fluorescence absence and forward-scatter spectrum are the oracles.

Tolerance: Fixed fraction, attenuation, open-share, yield and band multipliers are not derived. Unexplained margins: tolerance stated without a derivation in the code.

Gates: none.

Source: [tests/Gcam.Tests/MaskSecondaryTests.cs](../tests/Gcam.Tests/MaskSecondaryTests.cs).

Method groups:

- `PhotoFraction_DecreasesWithEnergy`, `MuRel_AnchoredAt662_HugeForFluorescence`, `At662_MostlyComptonScatter_LowerEnergy`, `Study_ProducesForwardScatterBackground_FluorescenceNegligible`: numerical comparisons described above; gate None.
- `BelowKEdge_NoFluorescence`: exact / structural fixture comparisons; gate None.

```json
{"id":"Gcam.Tests::Gcam.Tests.MaskSecondaryTests","runner":"dotnet","methods":["PhotoFraction_DecreasesWithEnergy","MuRel_AnchoredAt662_HugeForFluorescence","At662_MostlyComptonScatter_LowerEnergy","BelowKEdge_NoFluorescence","Study_ProducesForwardScatterBackground_FluorescenceNegligible"],"sources":{"tests/Gcam.Tests/MaskSecondaryTests.cs":"38476f3f614aca9fe88bc6e4a704f8071d9f0274268f80146b719525209c25cd","tests/Gcam.Tests/Harness/Rigs.cs":"9b89ce04cdcb302fcefc6636d45a0d1640a48c288d1e0a3638b005c44028c3b2","tests/Gcam.Tests/Harness/Stat.cs":"4502dddc6276f9d1de0c41b2ed7246ed7f00c9266daa12a34d6174ae1cb19029"},"trace":[]}
```


## MixedFieldTests — Gcam.Tests

The mixed-isotope field source: multiple (position, line) emitters imaged in one run, with contributions weighted by activity × line intensity.

Oracle: Single-source/superposition references, activity/intensity weighting, known coordinates, isotope windows and Co-only calibrated downscatter provide the oracles.

Tolerance: Fixed 3%/5%, 2/2.5/4 mm and contamination/stripping margins are not derived. Unexplained margins: tolerance stated without a derivation in the code.

Gates: none.

Source: [tests/Gcam.Tests/MixedFieldTests.cs](../tests/Gcam.Tests/MixedFieldTests.cs).

Method groups:

- `SingleSourceScene_MatchesSinglePath`, `Superposition_IsActivityWeighted`, `MultiSource_AllLocalized_InOneRun`, `MixedField_ThroughEnergyWindow_SeparatesIsotopesSpatially`, `PerNuclideWindow_SeparatesCsFromNa_InTheImage`, `ComptonStripping_RecoversCoLocatedCsCount`, `MultiLine_SplitsByIntensity`, `SingleSourceWithLines_EmitsAllItsLines`: numerical comparisons described above; gate None.
- `MaskAttenuation_IsEnergyDependent`: exact / structural fixture comparisons; gate None.

```json
{"id":"Gcam.Tests::Gcam.Tests.MixedFieldTests","runner":"dotnet","methods":["SingleSourceScene_MatchesSinglePath","Superposition_IsActivityWeighted","MultiSource_AllLocalized_InOneRun","MixedField_ThroughEnergyWindow_SeparatesIsotopesSpatially","PerNuclideWindow_SeparatesCsFromNa_InTheImage","ComptonStripping_RecoversCoLocatedCsCount","MultiLine_SplitsByIntensity","MaskAttenuation_IsEnergyDependent","SingleSourceWithLines_EmitsAllItsLines"],"sources":{"tests/Gcam.Tests/MixedFieldTests.cs":"8377d027d5d0c1a6bafc85202b4efb0db319379f83366f0a950ca48156fc33cd","tests/Gcam.Tests/Harness/Rigs.cs":"9b89ce04cdcb302fcefc6636d45a0d1640a48c288d1e0a3638b005c44028c3b2","tests/Gcam.Tests/Harness/Stat.cs":"4502dddc6276f9d1de0c41b2ed7246ed7f00c9266daa12a34d6174ae1cb19029"},"trace":[{"requirement":"EV-10","selectors":["MultiSource_AllLocalized_InOneRun","Superposition_IsActivityWeighted"]},{"requirement":"EV-15","selectors":["ComptonStripping_RecoversCoLocatedCsCount","MixedField_ThroughEnergyWindow_SeparatesIsotopesSpatially"]}]}
```


## MlemOptionTests — Gcam.Tests

TODO-34 (DR-6, DR-7): the opt-in MLEM forward models and background term, and the guarantee that the default decoder is unchanged bit for bit (EV-01 / EV-11 / EV-34 rest on it).

Oracle: The retained pre-change loop and supplied columns are exact references.

Tolerance: Pixel averages, binary/vector loops and background fixed points use operation-count unit-roundoff bounds quoted in code. The extra 1e-15 entry enclosure and rounded transmission interval include fixed margins with no separate derivation. Unexplained margins: tolerance stated without a derivation in the code.

Gates: none.

Source: [tests/Gcam.Tests/MlemOptionTests.cs](../tests/Gcam.Tests/MlemOptionTests.cs).

Method groups:

- `DefaultDecoder_IsBitIdenticalToThePreChangeAlgorithm`, `Snapshots_OfTheDefaultLoop_EqualDecodeAtEachIterationCount`, `PixelAreaModel_WithOneSampleAndOpaqueCells_IsTheBinaryMatrix`, `PixelAreaModel_EntriesAreSampleAveragesOfOneAndT`, `VectorLoop_OnTheBinaryMatrix_AgreesWithTheDefaultLoop`, `SuppliedColumns_ReproduceTheModelTheyCameFrom`, `Background_DataOfTheModelAtFlatLambda_IsAFixedPoint`, `FarSource_NoiselessModelData_ReconstructAtItsGridPoint`: exact / structural fixture comparisons; gate None.
- `ClosedCellTransmission_IsTheSlabsBeerLambertFactor`: numerical comparisons described above; gate None.

```json
{"id":"Gcam.Tests::Gcam.Tests.MlemOptionTests","runner":"dotnet","methods":["DefaultDecoder_IsBitIdenticalToThePreChangeAlgorithm","Snapshots_OfTheDefaultLoop_EqualDecodeAtEachIterationCount","PixelAreaModel_WithOneSampleAndOpaqueCells_IsTheBinaryMatrix","PixelAreaModel_EntriesAreSampleAveragesOfOneAndT","VectorLoop_OnTheBinaryMatrix_AgreesWithTheDefaultLoop","SuppliedColumns_ReproduceTheModelTheyCameFrom","Background_DataOfTheModelAtFlatLambda_IsAFixedPoint","FarSource_NoiselessModelData_ReconstructAtItsGridPoint","ClosedCellTransmission_IsTheSlabsBeerLambertFactor"],"sources":{"tests/Gcam.Tests/MlemOptionTests.cs":"a1f011a0d6710148ec4d65b633d40cc17d6d11d62eb4508526a4c3443b51c0f9","tests/Gcam.Tests/Harness/Rigs.cs":"9b89ce04cdcb302fcefc6636d45a0d1640a48c288d1e0a3638b005c44028c3b2","tests/Gcam.Tests/Harness/Stat.cs":"4502dddc6276f9d1de0c41b2ed7246ed7f00c9266daa12a34d6174ae1cb19029"},"trace":[{"requirement":"EV-11","selectors":["Background_DataOfTheModelAtFlatLambda_IsAFixedPoint","ClosedCellTransmission_IsTheSlabsBeerLambertFactor","DefaultDecoder_IsBitIdenticalToThePreChangeAlgorithm","FarSource_NoiselessModelData_ReconstructAtItsGridPoint","PixelAreaModel_EntriesAreSampleAveragesOfOneAndT","PixelAreaModel_WithOneSampleAndOpaqueCells_IsTheBinaryMatrix","Snapshots_OfTheDefaultLoop_EqualDecodeAtEachIterationCount","SuppliedColumns_ReproduceTheModelTheyCameFrom","VectorLoop_OnTheBinaryMatrix_AgreesWithTheDefaultLoop"]}]}
```


## MlemReconstructionTests — Gcam.Tests

TODO-36 (MD-1, MD-2): MLEM as a selectable reconstruction.

Oracle: Exact retained factory/decoder construction, matching arithmetic order, request-pinned settings, unique-column grid nodes and recomputed sub-cell positions provide the oracles. No approximate physics acceptance is asserted.

Tolerance: exact

Gates: none.

Source: [tests/Gcam.Tests/MlemReconstructionTests.cs](../tests/Gcam.Tests/MlemReconstructionTests.cs).

```json
{"id":"Gcam.Tests::Gcam.Tests.MlemReconstructionTests","runner":"dotnet","methods":["Method_LoadsFromAJsonString_AndSurvivesCloneAndSave","Method_AbsentMeansCrossCorrelation_AndOtherEnumsStayNumeric","DefaultMethod_IsTheUnchangedCrossCorrelationDecoder","FactoryMlem_EqualsTheAngresStudysConstruction_BitForBit","PixelSubSamples_IsTheValuePinnedInTheAngresRequests","ClosedCellTransmission_IsShared_AndFollowsTheLineEnergy","Mlem_UsesTheInvertedMosaic_WhenTheMaskIsInverted","FactoryMlem_NoiselessModelDataAtAGridNode_PeaksAtThatNode","SubCellArgument_NoneIsTheOriginalDecoder_TentRefinesTheReturnedImage","CorrelationSearch_RefusesAnMlemConfig","SingleRun_DecodesWithMlem_WhenTheScenarioSelectsIt"],"sources":{"tests/Gcam.Tests/MlemReconstructionTests.cs":"0e0985f6d867a65729402d8cb6306b7a4a4fa232d0018beb3ea335ecf92a3186","tests/Gcam.Tests/Harness/Rigs.cs":"9b89ce04cdcb302fcefc6636d45a0d1640a48c288d1e0a3638b005c44028c3b2","tests/Gcam.Tests/Harness/Stat.cs":"4502dddc6276f9d1de0c41b2ed7246ed7f00c9266daa12a34d6174ae1cb19029"},"trace":[{"requirement":"SR-IMG-07","selectors":["ClosedCellTransmission_IsShared_AndFollowsTheLineEnergy","CorrelationSearch_RefusesAnMlemConfig","DefaultMethod_IsTheUnchangedCrossCorrelationDecoder","FactoryMlem_EqualsTheAngresStudysConstruction_BitForBit","FactoryMlem_NoiselessModelDataAtAGridNode_PeaksAtThatNode","Method_AbsentMeansCrossCorrelation_AndOtherEnumsStayNumeric","Method_LoadsFromAJsonString_AndSurvivesCloneAndSave","Mlem_UsesTheInvertedMosaic_WhenTheMaskIsInverted","PixelSubSamples_IsTheValuePinnedInTheAngresRequests","SingleRun_DecodesWithMlem_WhenTheScenarioSelectsIt","SubCellArgument_NoneIsTheOriginalDecoder_TentRefinesTheReturnedImage"]}]}
```


## MlemTests — Gcam.Tests

MLEM (Poisson-likelihood) reconstruction vs cross-correlation.

Oracle: Known source/pair positions and cross-correlation reconstructions are the comparison.

Tolerance: Non-negativity/order and pair verdicts are structural; fixed bias, -1e-6, +0.2 mm and -1e-9 margins are not derived. Unexplained margins: tolerance stated without a derivation in the code.

Gates: none.

Source: [tests/Gcam.Tests/MlemTests.cs](../tests/Gcam.Tests/MlemTests.cs).

```json
{"id":"Gcam.Tests::Gcam.Tests.MlemTests","runner":"dotnet","methods":["SingleSource_MlemIsNonNegativeAndSharp_CrossHasSidelobes","MlemResolvesCloserPairsThanCrossCorrelation"],"sources":{"tests/Gcam.Tests/MlemTests.cs":"e1479e81a7e69245661f6f751555e2383849d4ff63ea67228691a3e50b06774e","tests/Gcam.Tests/Harness/Rigs.cs":"9b89ce04cdcb302fcefc6636d45a0d1640a48c288d1e0a3638b005c44028c3b2","tests/Gcam.Tests/Harness/Stat.cs":"4502dddc6276f9d1de0c41b2ed7246ed7f00c9266daa12a34d6174ae1cb19029"},"trace":[{"requirement":"EV-11","selectors":["MlemResolvesCloserPairsThanCrossCorrelation","SingleSource_MlemIsNonNegativeAndSharp_CrossHasSidelobes"]}]}
```


## MuraGeneratorTests — Gcam.Tests

Checks the rank-7 MURA row/column signature, near-half open fraction and decoding-array origin. Composite ranks must be rejected, and a 2x2 mosaic must repeat the basic pattern with the rank as its period.

Oracle: Gottesman–Fenimore row/column signature, repeated mosaic cells and decoding origin are exact.

Tolerance: The 0.40–0.55 open-fraction interval is stated without derivation. Unexplained margins: tolerance stated without a derivation in the code.

Gates: none.

Source: [tests/Gcam.Tests/MuraGeneratorTests.cs](../tests/Gcam.Tests/MuraGeneratorTests.cs).

```json
{"id":"Gcam.Tests::Gcam.Tests.MuraGeneratorTests","runner":"dotnet","methods":["Rank7_HasCharacteristicMuraSignature","Rank7_OpenFractionIsNearHalf","NonPrimeRank_Throws","Mosaic2x2_TilesTheBasicPattern","DecodingArray_IsPlusOneAtOrigin"],"sources":{"tests/Gcam.Tests/MuraGeneratorTests.cs":"fc2d80d5ad0a48375c3a6a6f5344d0eba4a122791d6ff2b661bd6d16df489fe7","tests/Gcam.Tests/Harness/Rigs.cs":"9b89ce04cdcb302fcefc6636d45a0d1640a48c288d1e0a3638b005c44028c3b2","tests/Gcam.Tests/Harness/Stat.cs":"4502dddc6276f9d1de0c41b2ed7246ed7f00c9266daa12a34d6174ae1cb19029"},"trace":[]}
```


## NonProportionalityTests — Gcam.Tests

Scintillator non-proportionality: the light yield per keV varies with the depositing electron's energy, so a full-energy gamma's total light Σ Eᵢ·nP(Eᵢ) fluctuates with the (random) Compton-cascade composition even at fixed total energy — the INTRINSIC resolution, derived from first principles instead of the hand-set constant floor.

Oracle: 662-normalized response tables, proportional-crystal null and per-energy/preset comparisons are the oracles.

Tolerance: Fixed response deficits/FWHM ratios and decimal comparisons have no derived error budget. Unexplained margins: tolerance stated without a derivation in the code.

Gates: none.

Source: [tests/Gcam.Tests/NonProportionalityTests.cs](../tests/Gcam.Tests/NonProportionalityTests.cs).

```json
{"id":"Gcam.Tests::Gcam.Tests.NonProportionalityTests","runner":"dotnet","methods":["AllPresets_AreNormalizedTo662","Proportional_IsFlat_NaI_HasALowEnergyDeficit","ProportionalCrystal_HasExactlyZeroIntrinsicResolution","NonProportionality_GivesAnEnergyDependentIntrinsicResolution"],"sources":{"tests/Gcam.Tests/NonProportionalityTests.cs":"60d232c7e62927609efce6b0209374926de0ac70e0c78a540c347f591cc81681","tests/Gcam.Tests/Harness/Rigs.cs":"9b89ce04cdcb302fcefc6636d45a0d1640a48c288d1e0a3638b005c44028c3b2","tests/Gcam.Tests/Harness/Stat.cs":"4502dddc6276f9d1de0c41b2ed7246ed7f00c9266daa12a34d6174ae1cb19029"},"trace":[]}
```


## OpticalResponseTests — Gcam.Tests

TODO-19 optical light spread: the scintillation-photon tracer behind the readout response table conserves light (one fate per photon) and reproduces closed forms of geometric optics; the symmetry reduction traces only the fundamental domain and maps each crystal to its own sensor.

Oracle: Closed forms: direct solid angle 4·asin(a²/(a² + 4h²))/4π of the exit square (absorbing walls, index-matched exit); normal-incidence Fresnel transmission 1 − ((n1 − n2)/(n1 + n2))²; total internal reflection beyond asin(n2/n1); a lossless index-matched box delivers every photon to its own sensor. Structural: fates sum to 1, table rows equal their collection, 21 traced classes for a symmetric 12 × 12 layout and 144 for an offset one.

Tolerance: k = 4 binomial standard errors of the traced fraction (Stat.BinomialSigma, N = 200 000 photons). Light accounting to 1e-12 (exact counts divided by the budget). Deterministic cases exact.

Gates: none.

Source: [tests/Gcam.Tests/OpticalResponseTests.cs](../tests/Gcam.Tests/OpticalResponseTests.cs).

Method groups:

- `BlackBox_DetectsTheDirectSolidAngle`, `NormalIncidence_TransmitsTheFresnelFraction`: binomial k·σ comparison with closed forms; gate None.
- `EveryTracedPhoton_HasExactlyOneFate`, `LosslessIndexMatchedBox_DeliversEveryPhoton`, `BeyondTheCriticalAngle_NothingLeavesTheExitFace`, `SymmetricLayout_TracesOnlyTheFundamentalDomain_OffsetLayoutTracesAll`, `MappedTables_PointEachCrystalAtItsOwnSensor`: exact / structural fixture comparisons; gate None.

```json
{"id":"Gcam.Tests::Gcam.Tests.OpticalResponseTests","runner":"dotnet","methods":["EveryTracedPhoton_HasExactlyOneFate","LosslessIndexMatchedBox_DeliversEveryPhoton","BlackBox_DetectsTheDirectSolidAngle","NormalIncidence_TransmitsTheFresnelFraction","BeyondTheCriticalAngle_NothingLeavesTheExitFace","SymmetricLayout_TracesOnlyTheFundamentalDomain_OffsetLayoutTracesAll","MappedTables_PointEachCrystalAtItsOwnSensor"],"sources":{"tests/Gcam.Tests/OpticalResponseTests.cs":"0b431e97a29f65bcffcac94e1dec5ded92412b2111755654de62b5fd494d6cdc","tests/Gcam.Tests/Harness/Rigs.cs":"9b89ce04cdcb302fcefc6636d45a0d1640a48c288d1e0a3638b005c44028c3b2","tests/Gcam.Tests/Harness/Stat.cs":"4502dddc6276f9d1de0c41b2ed7246ed7f00c9266daa12a34d6174ae1cb19029"},"trace":[]}
```

## PileUpTests — Gcam.Tests

Random-coincidence pile-up: overlapping pulses SUM into one recorded event, adding a self-convolution continuum above the photopeak and a count-rate-dependent throughput loss.

Oracle: Hand specified bursts, extending merge behavior and total energy preservation are exact/structural.

Tolerance: The low-rate>0.98 and high-continuum>3× criteria are stated without derivation. Unexplained margins: tolerance stated without a derivation in the code.

Gates: none.

Source: [tests/Gcam.Tests/PileUpTests.cs](../tests/Gcam.Tests/PileUpTests.cs).

Method groups:

- `CloseEvents_MergeAndSumEnergy`, `Pileup_IsExtending_ABurstPilesToThreefold`, `ResolvingTime_FromShaperPulse`, `HigherRate_LosesThroughput_AndBuildsSumContinuum`, `EnergyIsConserved_NoCountsAppearFromNowhere`: numerical comparisons described above; gate None.
- `FarEvents_DoNotMerge`: exact / structural fixture comparisons; gate None.

```json
{"id":"Gcam.Tests::Gcam.Tests.PileUpTests","runner":"dotnet","methods":["CloseEvents_MergeAndSumEnergy","FarEvents_DoNotMerge","Pileup_IsExtending_ABurstPilesToThreefold","ResolvingTime_FromShaperPulse","HigherRate_LosesThroughput_AndBuildsSumContinuum","EnergyIsConserved_NoCountsAppearFromNowhere"],"sources":{"tests/Gcam.Tests/PileUpTests.cs":"394da0354c21d448de49adbb58606fe94eda077d78d1b7cd87c3d8b57443b25b","tests/Gcam.Tests/Harness/Rigs.cs":"9b89ce04cdcb302fcefc6636d45a0d1640a48c288d1e0a3638b005c44028c3b2","tests/Gcam.Tests/Harness/Stat.cs":"4502dddc6276f9d1de0c41b2ed7246ed7f00c9266daa12a34d6174ae1cb19029"},"trace":[]}
```


## PipelineTests — Gcam.Tests

Integration harness: locks in the key physics findings as fast regression tests.

Oracle: Configured source truth, cyclic ghost side, analog-vs-biased efficiency and density/leak alternatives are the oracles.

Tolerance: The 1.5/5 mm and 20% margins are not derived. Unexplained margins: tolerance stated without a derivation in the code.

Gates: none.

Source: [tests/Gcam.Tests/PipelineTests.cs](../tests/Gcam.Tests/PipelineTests.cs).

Method groups:

- `CenteredSource_LocalizesSubMillimeter`, `OffAxisInsideFcfov_Tracks`, `OffAxisOutsideFcfov_GhostsToOppositeSide`, `Biasing_IsUnbiasedVersus4Pi`: numerical comparisons described above; gate None.
- `DenserCrystal_DetectsMore`, `LeakyMask_AddsCountsVersusOpaque`, `NonCyclicDecoding_SuppressesGhost`: exact / structural fixture comparisons; gate None.

```json
{"id":"Gcam.Tests::Gcam.Tests.PipelineTests","runner":"dotnet","methods":["CenteredSource_LocalizesSubMillimeter","OffAxisInsideFcfov_Tracks","OffAxisOutsideFcfov_GhostsToOppositeSide","Biasing_IsUnbiasedVersus4Pi","DenserCrystal_DetectsMore","LeakyMask_AddsCountsVersusOpaque","NonCyclicDecoding_SuppressesGhost"],"sources":{"tests/Gcam.Tests/PipelineTests.cs":"88fbacc44fd79a0995f11db8a052e155a3494af2ef968d88a5a87e80a2c3f2c7","tests/Gcam.Tests/Harness/Rigs.cs":"9b89ce04cdcb302fcefc6636d45a0d1640a48c288d1e0a3638b005c44028c3b2","tests/Gcam.Tests/Harness/Stat.cs":"4502dddc6276f9d1de0c41b2ed7246ed7f00c9266daa12a34d6174ae1cb19029"},"trace":[{"requirement":"EV-01","selectors":["CenteredSource_LocalizesSubMillimeter","NonCyclicDecoding_SuppressesGhost","OffAxisInsideFcfov_Tracks","OffAxisOutsideFcfov_GhostsToOppositeSide"]},{"requirement":"EV-03","selectors":["Biasing_IsUnbiasedVersus4Pi","CenteredSource_LocalizesSubMillimeter","DenserCrystal_DetectsMore","LeakyMask_AddsCountsVersusOpaque","NonCyclicDecoding_SuppressesGhost","OffAxisInsideFcfov_Tracks","OffAxisOutsideFcfov_GhostsToOppositeSide"]},{"requirement":"EV-04","selectors":["LeakyMask_AddsCountsVersusOpaque"]},{"requirement":"EV-07","selectors":["Biasing_IsUnbiasedVersus4Pi"]},{"requirement":"EV-19","selectors":["DenserCrystal_DetectsMore"]}]}
```


## RandomQualityTests — Gcam.Tests

Properties of <see cref="DefaultRandom"/> the engine relies on (TODO-26).

Oracle: Uniform, correlation and KN quadrature moments use four standard errors with explicit formulas; uniform sample variance uses (mu4−sigma4)/N. Replay seeds/unit interval are exact/structural.

Tolerance: exact

Gates: none.

Source: [tests/Gcam.Tests/RandomQualityTests.cs](../tests/Gcam.Tests/RandomQualityTests.cs).

Method groups:

- `NearbySeeds_GiveUncorrelatedStreams`, `SuccessiveDraws_AreUncorrelated`, `NextDouble_IsInTheUnitInterval_WithMeanAndVarianceOfAUniform`, `KleinNishina_MatchesQuadrature`: numerical comparisons described above; gate None.
- `UnseededStream_ExposesAReplayableSeed`: exact / structural fixture comparisons; gate None.

```json
{"id":"Gcam.Tests::Gcam.Tests.RandomQualityTests","runner":"dotnet","methods":["NearbySeeds_GiveUncorrelatedStreams","SuccessiveDraws_AreUncorrelated","UnseededStream_ExposesAReplayableSeed","NextDouble_IsInTheUnitInterval_WithMeanAndVarianceOfAUniform","KleinNishina_MatchesQuadrature"],"sources":{"tests/Gcam.Tests/RandomQualityTests.cs":"4f90b39915d067007c1c1632ef8cc4888b41e6e9ba98015a5dba7b3757b73863","tests/Gcam.Tests/Harness/Rigs.cs":"9b89ce04cdcb302fcefc6636d45a0d1640a48c288d1e0a3638b005c44028c3b2","tests/Gcam.Tests/Harness/Stat.cs":"4502dddc6276f9d1de0c41b2ed7246ed7f00c9266daa12a34d6174ae1cb19029"},"trace":[]}
```


## RangeLocalizationTests — Gcam.Tests

A source-locating camera must work at a realistic standoff, not just right in front.

Oracle: The known lateral source coordinate at each standoff is the oracle; the 8 mm ceiling is stated without derivation.

Tolerance: structural

Gates: none.

Source: [tests/Gcam.Tests/RangeLocalizationTests.cs](../tests/Gcam.Tests/RangeLocalizationTests.cs).

```json
{"id":"Gcam.Tests::Gcam.Tests.RangeLocalizationTests","runner":"dotnet","methods":["LocalizesAtStandoff"],"sources":{"tests/Gcam.Tests/RangeLocalizationTests.cs":"86ce9c7c8c6fbfdd029a12b28c1b8832a1f4794aa43c30a8da2b6c9cb4e1ddd8","tests/Gcam.Tests/Harness/Rigs.cs":"9b89ce04cdcb302fcefc6636d45a0d1640a48c288d1e0a3638b005c44028c3b2","tests/Gcam.Tests/Harness/Stat.cs":"4502dddc6276f9d1de0c41b2ed7246ed7f00c9266daa12a34d6174ae1cb19029"},"trace":[{"requirement":"EV-33","selectors":["LocalizesAtStandoff"]}]}
```


## ReadoutDefaultPathTests — Gcam.Tests

TODO-19 disabled path: adding the physical readout and the pre-optical interaction records leaves the direct crystal assignment bit-identical — list-mode streams, the Compton runner image and the serialised configuration match fixtures recorded before the readout existed; recorded sites conserve each event's deposit; a physical readout requested on a direct path is refused (stage 1 runs it in the study only).

Oracle: SHA-256 fixtures recorded from the engine at commit aeac875 (before TODO-19) with the same seeds and budgets: list-mode event streams of the lab Cs-137 scene, the Co-60 cascade scene and a scene with reflector gap, legacy crosstalk, entrance absorber and backing; the Compton runner flood image; the serialised scenario bytes. Recorded sites: their sum equals the event deposit, their crystal indices follow their XY, their arg-max is the event pixel.

Tolerance: Exact (hash equality). Site deposit sum within 1e-12·E — the summation round-off bound n·ε·E for at most 32 sites, stated in the code.

Gates: none.

Source: [tests/Gcam.Tests/ReadoutDefaultPathTests.cs](../tests/Gcam.Tests/ReadoutDefaultPathTests.cs).

Method groups:

- `DefaultListMode_MatchesPreReadoutFixtures`, `InteractionRecording_LeavesEveryStreamIdentical`, `ExplicitDirectCrystal_IsTheDefault_AndNullIsNotSerialised`, `ComptonRunnerImage_MatchesPreReadoutFixture`, `PhysicalReadoutOnADirectPath_IsRefused`, `Recording_IsRefusedWithBackgroundOrAmbient`: exact / structural fixture comparisons; gate None.
- `RecordedSites_ConserveTheDeposit_AndReproduceTheDirectPixel`: numerical comparison within the stated round-off bound; gate None.

```json
{"id":"Gcam.Tests::Gcam.Tests.ReadoutDefaultPathTests","runner":"dotnet","methods":["DefaultListMode_MatchesPreReadoutFixtures","InteractionRecording_LeavesEveryStreamIdentical","ExplicitDirectCrystal_IsTheDefault_AndNullIsNotSerialised","ComptonRunnerImage_MatchesPreReadoutFixture","RecordedSites_ConserveTheDeposit_AndReproduceTheDirectPixel","PhysicalReadoutOnADirectPath_IsRefused","Recording_IsRefusedWithBackgroundOrAmbient"],"sources":{"tests/Gcam.Tests/ReadoutDefaultPathTests.cs":"37434d464ffdef5da011a5eeeb6b819b4b966f627ea03d31706b6cbe18ca0a6b","tests/Gcam.Tests/Harness/Rigs.cs":"9b89ce04cdcb302fcefc6636d45a0d1640a48c288d1e0a3638b005c44028c3b2","tests/Gcam.Tests/Harness/Stat.cs":"4502dddc6276f9d1de0c41b2ed7246ed7f00c9266daa12a34d6174ae1cb19029"},"trace":[]}
```

## ReadoutDeviceTests — Gcam.Tests

TODO-19 readout chain from interaction sites to channel amplitudes: light and charge are conserved through optics and network, the four-channel covariance equals Wᵀ·Cov(pe)·W, the noiseless correctly calibrated single-site limit returns the original crystal, a multisite Anger position is the light centroid (not the arg-max), and saturation never exceeds the microcell count.

Oracle: Analytic moments: mean CodesPerPe·Wᵀμ and Cov = CodesPerPe²·Wᵀ·[δ_kl·ENF·(μ_k + λ_d) + σ_int²·μ_k·μ_l]·W (Poisson thinning, common intrinsic factor, gain-sum second moment, baseline-subtracted dark counts); light Σ_k μ_k = PDE·LY·Σ_s E_s·collection; the light centroid Σμ_kξ_k/Σμ_k computed independently from the sensor centres; the identity of the ideal limit; the bound N·(1 − e^(−n/N)) < N.

Tolerance: Covariance within k = 4 normal-theory standard errors √((Σ_aa·Σ_bb + Σ_ab²)/(N − 1)), means within 4·√(Σ_aa/N), N = 20 000 (derivation and the negligible 1/12 rounding variance of the Poisson sampler stated in the code). Conservation 1e-9 relative (round-off); light centroid 1e-12; ideal limit and saturation bound exact.

Gates: none.

Source: [tests/Gcam.Tests/ReadoutDeviceTests.cs](../tests/Gcam.Tests/ReadoutDeviceTests.cs).

Method groups:

- `ChannelCovariance_MatchesWTransposeCovPeW`: statistical k·σ comparison with analytic moments; gate None.
- `Expected_ConservesLightThroughOpticsAndNetwork`, `Multisite_AngerPositionIsTheLightCentroid_NotTheArgMax`: numerical comparison within round-off; gate None.
- `SingleSiteIdealLimit_IsTheOriginalCrystal`, `Saturation_NeverExceedsTheMicrocellCount`: exact / structural fixture comparisons; gate None.

```json
{"id":"Gcam.Tests::Gcam.Tests.ReadoutDeviceTests","runner":"dotnet","methods":["Expected_ConservesLightThroughOpticsAndNetwork","ChannelCovariance_MatchesWTransposeCovPeW","SingleSiteIdealLimit_IsTheOriginalCrystal","Multisite_AngerPositionIsTheLightCentroid_NotTheArgMax","Saturation_NeverExceedsTheMicrocellCount"],"sources":{"tests/Gcam.Tests/ReadoutDeviceTests.cs":"6601e3adcb00d9d04a3a579cce203f843a2b96e98e00017ae1ceac1f50d0db39","tests/Gcam.Tests/Harness/Rigs.cs":"9b89ce04cdcb302fcefc6636d45a0d1640a48c288d1e0a3638b005c44028c3b2","tests/Gcam.Tests/Harness/Stat.cs":"4502dddc6276f9d1de0c41b2ed7246ed7f00c9266daa12a34d6174ae1cb19029"},"trace":[]}
```

## ReadoutPulseProcessorTests — Gcam.Tests

TODO-19 time-domain four-output model (RD-3): explicit trigger truth table and units, the isolated-pulse shortcut equal to the full time-domain path, and pile-up summed in every channel so a piled-up event's position is the light centroid of its hits weighted by their pulse heights at the hold instant.

Oracle: Trigger truth table on constructed channel vectors (sum, OR, AND; ADC-code and keV-equivalent thresholds); exact equality of Process and ProcessIsolated for one hit; coincident pile-up = (X1·Σ1 + X2·Σ2)/(Σ1 + Σ2); delayed pile-up = an independent scan of the same grid over the hold window; hits beyond the pulse support stay separate; a pulse in the busy tail is never its own event.

Tolerance: Electronic noise off; code rounding bounds X by 4/Σ (½ code per channel, four channels, |s_c − X| ≤ 2 — derived in the class summary) and the code sum by 2 codes. Truth table, shortcut equality and event bookkeeping exact.

Gates: none.

Source: [tests/Gcam.Tests/ReadoutPulseProcessorTests.cs](../tests/Gcam.Tests/ReadoutPulseProcessorTests.cs).

Method groups:

- `CoincidentPileUp_IsTheChargeWeightedCentroid`, `DelayedPileUp_WeightsHitsByPulseHeightAtTheHold`: numerical comparison within the code-rounding bound; gate None.
- `TriggerTruthTable_WithExplicitUnits`, `IsolatedPulse_TimeDomainEqualsTheShortcut`, `HitsBeyondThePulseSupport_AreSeparateEvents`, `PulseDuringTheBusyTail_IsNotItsOwnEvent`, `UnsortedHits_AreRefused`: exact / structural fixture comparisons; gate None.

```json
{"id":"Gcam.Tests::Gcam.Tests.ReadoutPulseProcessorTests","runner":"dotnet","methods":["TriggerTruthTable_WithExplicitUnits","IsolatedPulse_TimeDomainEqualsTheShortcut","CoincidentPileUp_IsTheChargeWeightedCentroid","DelayedPileUp_WeightsHitsByPulseHeightAtTheHold","HitsBeyondThePulseSupport_AreSeparateEvents","PulseDuringTheBusyTail_IsNotItsOwnEvent","UnsortedHits_AreRefused"],"sources":{"tests/Gcam.Tests/ReadoutPulseProcessorTests.cs":"9fa2347b214c6af4f75c41322fb70de4ac3a78b95c2a0058d5e144e5f943166c","tests/Gcam.Tests/Harness/Rigs.cs":"9b89ce04cdcb302fcefc6636d45a0d1640a48c288d1e0a3638b005c44028c3b2","tests/Gcam.Tests/Harness/Stat.cs":"4502dddc6276f9d1de0c41b2ed7246ed7f00c9266daa12a34d6174ae1cb19029"},"trace":[]}
```

## ReadoutStudyTests — Gcam.Tests

TODO-19 readout study recipe (RD-7): reproducible for a fixed seed, paired (the direct reference and every readout share the transported localisation events), reporting a failed calibration with its reason instead of numbers, refusing invalid requests.

Oracle: Two runs with the same seed compared as JSON with timing fields removed; event counts of the direct reference and every readout; a readout with 500 keV channel noise (σ_X far above the spot spacing) must fail calibration; requests with DirectCrystal as a variant or no lines are refused.

Tolerance: Exact / structural.

Gates: none.

Source: [tests/Gcam.Tests/ReadoutStudyTests.cs](../tests/Gcam.Tests/ReadoutStudyTests.cs).

Method groups:

- `SameSeed_GivesIdenticalNumbers_AndPairsTheReadouts`, `FailedCalibration_ReportsTheReasonAndNoNumbers`, `InvalidRequests_AreRefused`: exact / structural fixture comparisons; gate None.

```json
{"id":"Gcam.Tests::Gcam.Tests.ReadoutStudyTests","runner":"dotnet","methods":["SameSeed_GivesIdenticalNumbers_AndPairsTheReadouts","FailedCalibration_ReportsTheReasonAndNoNumbers","InvalidRequests_AreRefused"],"sources":{"tests/Gcam.Tests/ReadoutStudyTests.cs":"b0b1ae44e2c5c4bafb83682fe048233febb2178f619fa94e16a6901607e8024e","tests/Gcam.Tests/Harness/Rigs.cs":"9b89ce04cdcb302fcefc6636d45a0d1640a48c288d1e0a3638b005c44028c3b2","tests/Gcam.Tests/Harness/Stat.cs":"4502dddc6276f9d1de0c41b2ed7246ed7f00c9266daa12a34d6174ae1cb19029"},"trace":[]}
```

## ResolvedPairTests — Gcam.Tests

TODO-34 (DR-2): the blind resolved-pair test on synthetic images with known answers, and the angle convention.

Oracle: Synthetic tent prominence/widths, order-statistic median and brute-force floor selection supply known answers.

Tolerance: Comments give rational-entry and relative-rounding precision and acos amplification 4u/sin(theta). The +1e-12 exceedance guard is stated without a separate derivation. Unexplained margins: tolerance stated without a derivation in the code.

Gates: none.

Source: [tests/Gcam.Tests/ResolvedPairTests.cs](../tests/Gcam.Tests/ResolvedPairTests.cs).

Method groups:

- `TwoEqualTents_SecondPeakProminence_IsDeltaOverWidthMinusOne`, `Pedestal_DoesNotChangeTheMedianBaselineTest`, `Fwhm_OfATent_IsItsHalfBaseInAngle`, `ElementAngles_AtOneAndFiveMetres`, `SecondPeak_AbsoluteProminence_IsTheAnalyticValue`, `SelectFloor_IsTheSmallestFloorWithExceedanceAtMostAlpha`: numerical comparisons described above; gate None.
- `Plateau_GivesOnePeak`, `Median_IsTheMiddleOrderStatistic`, `AssignmentRadius_IsCappedAtOneElement`, `SingleSource_IsNeverResolved`: exact / structural fixture comparisons; gate None.

```json
{"id":"Gcam.Tests::Gcam.Tests.ResolvedPairTests","runner":"dotnet","methods":["TwoEqualTents_SecondPeakProminence_IsDeltaOverWidthMinusOne","Plateau_GivesOnePeak","Pedestal_DoesNotChangeTheMedianBaselineTest","Median_IsTheMiddleOrderStatistic","AssignmentRadius_IsCappedAtOneElement","SingleSource_IsNeverResolved","Fwhm_OfATent_IsItsHalfBaseInAngle","ElementAngles_AtOneAndFiveMetres","SecondPeak_AbsoluteProminence_IsTheAnalyticValue","SelectFloor_IsTheSmallestFloorWithExceedanceAtMostAlpha"],"sources":{"tests/Gcam.Tests/ResolvedPairTests.cs":"86e17049416065160a9190d5a063fd84ca7a70d2fb466d60a36c912d55ac35cc","tests/Gcam.Tests/Harness/Rigs.cs":"9b89ce04cdcb302fcefc6636d45a0d1640a48c288d1e0a3638b005c44028c3b2","tests/Gcam.Tests/Harness/Stat.cs":"4502dddc6276f9d1de0c41b2ed7246ed7f00c9266daa12a34d6174ae1cb19029"},"trace":[{"requirement":"EV-11","selectors":["AssignmentRadius_IsCappedAtOneElement","ElementAngles_AtOneAndFiveMetres","Fwhm_OfATent_IsItsHalfBaseInAngle","Median_IsTheMiddleOrderStatistic","Pedestal_DoesNotChangeTheMedianBaselineTest","Plateau_GivesOnePeak","SecondPeak_AbsoluteProminence_IsTheAnalyticValue","SelectFloor_IsTheSmallestFloorWithExceedanceAtMostAlpha","SingleSource_IsNeverResolved","TwoEqualTents_SecondPeakProminence_IsDeltaOverWidthMinusOne"]}]}
```


## SamplingTests — Gcam.Tests

The samplers every noise study rests on: Poisson counts (Knuth below λ = 30, a rounded Gaussian above) and the Box–Muller normal.

Oracle: Poisson/normal/spherical analytic moments and tail probabilities use four standard errors with central-moment/binomial formulas.

Tolerance: Comments explain the rounded-Gaussian switch and its roughly 1/12 added variance. Non-positive means and integer/unit domains are exact/structural.

Gates: none.

Source: [tests/Gcam.Tests/SamplingTests.cs](../tests/Gcam.Tests/SamplingTests.cs).

Method groups:

- `Poisson_HasMeanAndVarianceLambda`, `Poisson_IsContinuousAcrossTheMethodSwitch`, `Gaussian_IsStandardNormal`, `UnitSphere_IsIsotropic`, `PoissonExact_MatchesPoissonMomentsAndTheZeroProbability`: numerical comparisons described above; gate None.
- `Poisson_OfNonPositiveMean_IsZero`: exact / structural fixture comparisons; gate None.

```json
{"id":"Gcam.Tests::Gcam.Tests.SamplingTests","runner":"dotnet","methods":["Poisson_HasMeanAndVarianceLambda","Poisson_IsContinuousAcrossTheMethodSwitch","Poisson_OfNonPositiveMean_IsZero","Gaussian_IsStandardNormal","UnitSphere_IsIsotropic","PoissonExact_MatchesPoissonMomentsAndTheZeroProbability"],"sources":{"tests/Gcam.Tests/SamplingTests.cs":"fa659077ae6e8375a38ffcbf13a45404b20b237125658078e721ab0903ba44d1","tests/Gcam.Tests/Harness/Rigs.cs":"9b89ce04cdcb302fcefc6636d45a0d1640a48c288d1e0a3638b005c44028c3b2","tests/Gcam.Tests/Harness/Stat.cs":"4502dddc6276f9d1de0c41b2ed7246ed7f00c9266daa12a34d6174ae1cb19029"},"trace":[{"requirement":"EV-07","selectors":["Gaussian_IsStandardNormal","PoissonExact_MatchesPoissonMomentsAndTheZeroProbability","Poisson_HasMeanAndVarianceLambda","Poisson_IsContinuousAcrossTheMethodSwitch","Poisson_OfNonPositiveMean_IsZero","UnitSphere_IsIsotropic"]}]}
```


## ScattererTests — Gcam.Tests

The entrance/backing material is a real Compton SCATTERER, not a hand-added peak tail: a 662 keV photon that interacts almost always scatters (iron is Compton-dominated there) to a lower energy, while a 32 keV X-ray that interacts is mostly photo-absorbed.

Oracle: Known incident energy, Compton-vs-photoabsorption ordering and bare/entrance-scatter valley counts provide the oracle.

Tolerance: The >5× interaction and >1.5× valley margins are tolerance stated without a derivation in the code.

Gates: none.

Source: [tests/Gcam.Tests/ScattererTests.cs](../tests/Gcam.Tests/ScattererTests.cs).

Method groups:

- `Interact_662_MostlyScattersToLowerEnergy`, `EntranceScatterer_FillsTheSubPhotopeakValley`: numerical comparisons described above; gate None.
- `Interact_32keV_InteractionsMostlyAbsorbed`: exact / structural fixture comparisons; gate None.

```json
{"id":"Gcam.Tests::Gcam.Tests.ScattererTests","runner":"dotnet","methods":["Interact_662_MostlyScattersToLowerEnergy","Interact_32keV_InteractionsMostlyAbsorbed","EntranceScatterer_FillsTheSubPhotopeakValley"],"sources":{"tests/Gcam.Tests/ScattererTests.cs":"106b87d8a3f6efd02dcd91319f5d1b087699779a62574132880a29c94ac49625","tests/Gcam.Tests/Harness/Rigs.cs":"9b89ce04cdcb302fcefc6636d45a0d1640a48c288d1e0a3638b005c44028c3b2","tests/Gcam.Tests/Harness/Stat.cs":"4502dddc6276f9d1de0c41b2ed7246ed7f00c9266daa12a34d6174ae1cb19029"},"trace":[]}
```


## SceneConfigBuilderTests — Gcam.Tests

Checks nearest-prime rank selection and construction of a finite-mask multi-source simulation from scene settings. The runner must report progress and honor cancellation, and non-positive photon budgets must be rejected.

Oracle: Explicit nearest-prime outputs and scene/source/config fields are exact fixtures.

Tolerance: Recon extent inside FCFOV, progress completion and cancellation are structural; no physics accuracy margin.

Gates: none.

Source: [tests/Gcam.Tests/SceneConfigBuilderTests.cs](../tests/Gcam.Tests/SceneConfigBuilderTests.cs).

```json
{"id":"Gcam.Tests::Gcam.Tests.SceneConfigBuilderTests","runner":"dotnet","methods":["NearestPrime_SnapsRankToNearestPrime","Build_MultiSourceFiniteMaskConfig","Build_RejectsNonPositivePhotonBudget","Runner_ReportsProgress_HonoursCancellation"],"sources":{"tests/Gcam.Tests/SceneConfigBuilderTests.cs":"67ff5bcb9353ce3b8ad55495a66d2cabf7858fed46ed403f5ef7d96dd9fece3c","tests/Gcam.Tests/Harness/Rigs.cs":"9b89ce04cdcb302fcefc6636d45a0d1640a48c288d1e0a3638b005c44028c3b2","tests/Gcam.Tests/Harness/Stat.cs":"4502dddc6276f9d1de0c41b2ed7246ed7f00c9266daa12a34d6174ae1cb19029"},"trace":[]}
```


## SceneSimTests — Gcam.Tests

Exercises the exact simulation path the WPF scene editor drives: a multi-source mixed field with per-source distance (Position[2]), activity (Bq), and isotope lines — run + decode (Imaging) and streamed (Spectrum/Waveform).

Oracle: Configured scene sources/lines and runnable decode/list-mode outputs supply structural smoke expectations: data/reconstruction exists, energy classes are present.

Tolerance: This is not a precision assertion.

Gates: none.

Source: [tests/Gcam.Tests/SceneSimTests.cs](../tests/Gcam.Tests/SceneSimTests.cs).

```json
{"id":"Gcam.Tests::Gcam.Tests.SceneSimTests","runner":"dotnet","methods":["MixedScene_RunsAndDecodes","MixedScene_StreamsDepositSpectrum"],"sources":{"tests/Gcam.Tests/SceneSimTests.cs":"b32820c4c76ac5ad1c06df42fb6847a901c51991a87b99f6e8ca1ae887a9ec47","tests/Gcam.Tests/Harness/Rigs.cs":"9b89ce04cdcb302fcefc6636d45a0d1640a48c288d1e0a3638b005c44028c3b2","tests/Gcam.Tests/Harness/Stat.cs":"4502dddc6276f9d1de0c41b2ed7246ed7f00c9266daa12a34d6174ae1cb19029"},"trace":[]}
```


## SoilAirMaterialsTests — Gcam.Tests

Checks composed air attenuation against tabulated NIST values and mass-energy-absorption interpolation at grid points and absorption edges. Tampered or malformed material data must be rejected.

Oracle: Composed air/NIST comparisons allow half a unit of four-significant-figure table rounding (5e-4 relative); grid and edge values are the table oracles.

Tolerance: Selected decimal equality precisions are stated without a separate operation-count derivation. Unexplained margins: tolerance stated without a derivation in the code.

Gates: none.

Source: [tests/Gcam.Tests/SoilAirMaterialsTests.cs](../tests/Gcam.Tests/SoilAirMaterialsTests.cs).

Method groups:

- `Load_ComposedAirReproducesNistTotalWithinTableRounding`, `AirMuEn_ReturnsTabulatedValuesAndTakesTheValueAboveAnEdge`: numerical comparisons described above; gate None.
- `Load_RejectsTamperedOrMalformedData`: exact / structural fixture comparisons; gate None.

```json
{"id":"Gcam.Tests::Gcam.Tests.SoilAirMaterialsTests","runner":"dotnet","methods":["Load_ComposedAirReproducesNistTotalWithinTableRounding","AirMuEn_ReturnsTabulatedValuesAndTakesTheValueAboveAnEdge","Load_RejectsTamperedOrMalformedData"],"sources":{"tests/Gcam.Tests/SoilAirMaterialsTests.cs":"df48fc4b19398f44a319cc8f3124e222934bfa8ad542b95461a83f6bdb95447a","tests/Gcam.Tests/Harness/Rigs.cs":"9b89ce04cdcb302fcefc6636d45a0d1640a48c288d1e0a3638b005c44028c3b2","tests/Gcam.Tests/Harness/Stat.cs":"4502dddc6276f9d1de0c41b2ed7246ed7f00c9266daa12a34d6174ae1cb19029"},"trace":[{"requirement":"EV-34","selectors":["AirMuEn_ReturnsTabulatedValuesAndTakesTheValueAboveAnEdge","Load_ComposedAirReproducesNistTotalWithinTableRounding","Load_RejectsTamperedOrMalformedData"]}]}
```


## SoilAirTransportTests — Gcam.Tests

Checks seeded soil-to-air transport against the slab-averaged analytic uncollided kernel and internal fluence bookkeeping. Pair production must respect its energy threshold, repeated seeds must reproduce tallies and invalid recipes must be rejected.

Oracle: The slab-averaged analytic half-space kernel supplies uncollided fluence by energy and zenith bin. Repeated seeds, zero downward uncollided crossings, energy thresholds and sums of binned plus out-of-range fluence supply transport bookkeeping references. Kernel comparisons retain four per-history second-moment standard errors.

Tolerance: Reproducible tallies, thresholds and bookkeeping identities are exact/structural; decimal bookkeeping precision is stated as rounding only without a derived operation count. Unexplained margins: tolerance stated without a derivation in the code.

Gates: none.

Source: [tests/Gcam.Tests/SoilAirTransportTests.cs](../tests/Gcam.Tests/SoilAirTransportTests.cs).

Method groups:

- `Run_IsReproduciblePerSeed`, `Run_TalliesAreConsistentAndPairProductionNeedsThreshold`, `Constructor_RejectsInvalidRecipes`: exact / structural fixture comparisons; gate None.
- `Run_UncollidedMatchesAnalyticHalfSpaceKernel`: numerical comparisons described above; gate None.

```json
{"id":"Gcam.Tests::Gcam.Tests.SoilAirTransportTests","runner":"dotnet","methods":["Run_IsReproduciblePerSeed","Run_UncollidedMatchesAnalyticHalfSpaceKernel","Run_TalliesAreConsistentAndPairProductionNeedsThreshold","Constructor_RejectsInvalidRecipes"],"sources":{"tests/Gcam.Tests/SoilAirTransportTests.cs":"ae86bbb9ffd7b910856422ca177e1ebfbae117cccb8f88023b63dc6de60005d6","tests/Gcam.Tests/Harness/Rigs.cs":"9b89ce04cdcb302fcefc6636d45a0d1640a48c288d1e0a3638b005c44028c3b2","tests/Gcam.Tests/Harness/Stat.cs":"4502dddc6276f9d1de0c41b2ed7246ed7f00c9266daa12a34d6174ae1cb19029"},"trace":[{"requirement":"EV-34","selectors":["Constructor_RejectsInvalidRecipes","Run_IsReproduciblePerSeed","Run_TalliesAreConsistentAndPairProductionNeedsThreshold","Run_UncollidedMatchesAnalyticHalfSpaceKernel"]}]}
```


## SourceDistanceTests — Gcam.Tests

A source may set its own distance-to-detector via Position[2] (z, mm).

Oracle: Detector-biasing inverse-square weight and old shared-plane replay provide the oracle.

Tolerance: The 0.20–0.30 interval around one quarter is stated without derivation. Unexplained margins: tolerance stated without a derivation in the code.

Gates: none.

Source: [tests/Gcam.Tests/SourceDistanceTests.cs](../tests/Gcam.Tests/SourceDistanceTests.cs).

Method groups:

- `PerSourceDistance_FollowsInverseSquare`: exact / structural fixture comparisons; gate None.
- `ZeroZ_FallsBackToTheSharedPlane`: numerical comparisons described above; gate None.

```json
{"id":"Gcam.Tests::Gcam.Tests.SourceDistanceTests","runner":"dotnet","methods":["PerSourceDistance_FollowsInverseSquare","ZeroZ_FallsBackToTheSharedPlane"],"sources":{"tests/Gcam.Tests/SourceDistanceTests.cs":"af2cf7b0d2af26217d132222a919f175f9f5c4f6a04d8f488ecfc11ee91bd7d9","tests/Gcam.Tests/Harness/Rigs.cs":"9b89ce04cdcb302fcefc6636d45a0d1640a48c288d1e0a3638b005c44028c3b2","tests/Gcam.Tests/Harness/Stat.cs":"4502dddc6276f9d1de0c41b2ed7246ed7f00c9266daa12a34d6174ae1cb19029"},"trace":[]}
```


## SubCellTests — Gcam.Tests

Sub-cell peak interpolation: the bare argmax quantizes the source estimate to the recon-grid step (RMS = step/√12, independent of counts); fitting the correlation-peak shape recovers a fractional offset and beats that floor.

Oracle: Synthetic tent/Gaussian apex and fallback identities are analytic references.

Tolerance: Quantization RMS is step/sqrt(12). Decimal comparisons and the 0.7–1.5 and half-RMS study margins are stated without error-budget derivation. Unexplained margins: tolerance stated without a derivation in the code.

Gates: none.

Source: [tests/Gcam.Tests/SubCellTests.cs](../tests/Gcam.Tests/SubCellTests.cs).

```json
{"id":"Gcam.Tests::Gcam.Tests.SubCellTests","runner":"dotnet","methods":["Tent_IsExactForAnIdealTent","Parabolic_IsBiasedTowardTheCellForATent","Gaussian_IsExactForAGaussianPeak","None_AndGridEdges_ReturnZero","Gaussian_FallsBackToParabolaWhenANeighbourIsNonPositive","Interpolation_BeatsTheQuantizationFloor"],"sources":{"tests/Gcam.Tests/SubCellTests.cs":"d09cfde666978c1db1d8fa5124a9a6a0a56de2c45f3610703aca49f2af784cda","tests/Gcam.Tests/Harness/Rigs.cs":"9b89ce04cdcb302fcefc6636d45a0d1640a48c288d1e0a3638b005c44028c3b2","tests/Gcam.Tests/Harness/Stat.cs":"4502dddc6276f9d1de0c41b2ed7246ed7f00c9266daa12a34d6174ae1cb19029"},"trace":[{"requirement":"EV-08","selectors":["Gaussian_FallsBackToParabolaWhenANeighbourIsNonPositive","Gaussian_IsExactForAGaussianPeak","Interpolation_BeatsTheQuantizationFloor","None_AndGridEdges_ReturnZero","Parabolic_IsBiasedTowardTheCellForATent","Tent_IsExactForAnIdealTent"]}]}
```


## TerrestrialCatalogTests — Gcam.Tests

Checks evaluated terrestrial decay lines and secular-equilibrium branch normalization, including omitted Bi-214 intensities and the Bi-212 alpha feed. Catalog and snapshot hashes must remain pinned, and malformed data must be rejected.

Oracle: Pinned catalog/sidecar hashes, table branch feed and malformed-data rejection are exact/structural; the .9999–1 ratio and decimal precisions have no separate margin derivation.

Tolerance: exact

Gates: none.

Source: [tests/Gcam.Tests/TerrestrialCatalogTests.cs](../tests/Gcam.Tests/TerrestrialCatalogTests.cs).

Method groups:

- `Load_PinnedCatalogFollowsAb4d`: numerical comparisons described above; gate None.
- `Load_RejectsWrongHashAndMalformedCatalogs`, `Snapshot_ManifestHashesMatchFiles`: exact / structural fixture comparisons; gate None.

```json
{"id":"Gcam.Tests::Gcam.Tests.TerrestrialCatalogTests","runner":"dotnet","methods":["Load_PinnedCatalogFollowsAb4d","Load_RejectsWrongHashAndMalformedCatalogs","Snapshot_ManifestHashesMatchFiles"],"sources":{"tests/Gcam.Tests/TerrestrialCatalogTests.cs":"4916d1193739469144a7093adab2f02de846d162015ed36250be4d80cf96729e","tests/Gcam.Tests/Harness/Rigs.cs":"9b89ce04cdcb302fcefc6636d45a0d1640a48c288d1e0a3638b005c44028c3b2","tests/Gcam.Tests/Harness/Stat.cs":"4502dddc6276f9d1de0c41b2ed7246ed7f00c9266daa12a34d6174ae1cb19029"},"trace":[{"requirement":"EV-34","selectors":["Load_PinnedCatalogFollowsAb4d","Load_RejectsWrongHashAndMalformedCatalogs","Snapshot_ManifestHashesMatchFiles"]}]}
```


## TerrestrialSpectrumTests — Gcam.Tests

Checks the committed terrestrial spectrum, angular marginals and upward uncollided lines against its pinned validation records. The accepted reissue must preserve the numerical spectrum, and evidence use must reject unvalidated or tampered records.

Oracle: Committed hashes/version/validation records and a status-only accepted-file transformation are exact references.

Tolerance: Angular-row marginals and uncollided upward direction are structural; no additional measurement tolerance.

Gates: none.

Source: [tests/Gcam.Tests/TerrestrialSpectrumTests.cs](../tests/Gcam.Tests/TerrestrialSpectrumTests.cs).

```json
{"id":"Gcam.Tests::Gcam.Tests.TerrestrialSpectrumTests","runner":"dotnet","methods":["CommittedSpectrum_HashesVersionAndValidationStateHold","CommittedSpectrum_LoadsIntoTheEngineButNotAsEvidence","ValidatedSpectrum_IsTheAcceptedFileWithOnlyItsStatusChanged","ValidatedSpectrum_IsEvidenceGrade_TheNotValidatedOneAndATamperedRecordAreNot","CommittedSpectrum_TableReproducesEveryMarginalAndUncollidedLinesPointUpward"],"sources":{"tests/Gcam.Tests/TerrestrialSpectrumTests.cs":"47cfddeedc4adcbed0e220459bb2cf15431765b024932c1706d79f3682a7ae87","tests/Gcam.Tests/Harness/Rigs.cs":"9b89ce04cdcb302fcefc6636d45a0d1640a48c288d1e0a3638b005c44028c3b2","tests/Gcam.Tests/Harness/Stat.cs":"4502dddc6276f9d1de0c41b2ed7246ed7f00c9266daa12a34d6174ae1cb19029"},"trace":[{"requirement":"EV-34","selectors":["CommittedSpectrum_HashesVersionAndValidationStateHold","CommittedSpectrum_LoadsIntoTheEngineButNotAsEvidence","CommittedSpectrum_TableReproducesEveryMarginalAndUncollidedLinesPointUpward","ValidatedSpectrum_IsEvidenceGrade_TheNotValidatedOneAndATamperedRecordAreNot","ValidatedSpectrum_IsTheAcceptedFileWithOnlyItsStatusChanged"]}]}
```


## ThermalDriftTests — Gcam.Tests

Thermal drift during acquisition: the SiPM gain temperature coefficient walks the photopeak out of the fixed per-crystal window (efficiency droop) and, via the self-heating spatial gradient, leaves a flood- correction residual — while bias-compensation recovers both.

Oracle: Calibration null, spatial temperature/gain pattern and compensated/uncompensated studies are compared.

Tolerance: The 5 mm localization ceiling comes from the quoted 64-seed floor/ghost separation; remaining efficiency/residual multipliers and decimal margins are not derived. Unexplained margins: tolerance stated without a derivation in the code.

Gates: none.

Source: [tests/Gcam.Tests/ThermalDriftTests.cs](../tests/Gcam.Tests/ThermalDriftTests.cs).

Method groups:

- `AtCalibration_NoShift`, `Ambient_IsUniformAcrossArray`, `BiasComp_ShrinksTheShift`, `ShiftReducesAcceptance_ReducesToStaticAtZero`, `DriftDroopsEfficiency_BiasCompRecovers`: numerical comparisons described above; gate None.
- `SelfHeating_CentreHotterThanCorner`: exact / structural fixture comparisons; gate None.

```json
{"id":"Gcam.Tests::Gcam.Tests.ThermalDriftTests","runner":"dotnet","methods":["AtCalibration_NoShift","Ambient_IsUniformAcrossArray","SelfHeating_CentreHotterThanCorner","BiasComp_ShrinksTheShift","ShiftReducesAcceptance_ReducesToStaticAtZero","DriftDroopsEfficiency_BiasCompRecovers"],"sources":{"tests/Gcam.Tests/ThermalDriftTests.cs":"99b070dd5fccbc75c44136e28cff5ffdf263818e4e3d69f6c3c671ed03dd01ee","tests/Gcam.Tests/Harness/Rigs.cs":"9b89ce04cdcb302fcefc6636d45a0d1640a48c288d1e0a3638b005c44028c3b2","tests/Gcam.Tests/Harness/Stat.cs":"4502dddc6276f9d1de0c41b2ed7246ed7f00c9266daa12a34d6174ae1cb19029"},"trace":[{"requirement":"EV-29","selectors":["Ambient_IsUniformAcrossArray","AtCalibration_NoShift","BiasComp_ShrinksTheShift","DriftDroopsEfficiency_BiasCompRecovers","SelfHeating_CentreHotterThanCorner","ShiftReducesAcceptance_ReducesToStaticAtZero"]}]}
```


## ThermalReadoutTests — Gcam.Tests

Thermal readout beyond the gain-centroid drift (theme 36): SiPM dark-count rate doubles every ~8 °C and — unlike the gain — is NOT nulled by bias compensation, so it climbs with temperature.

Oracle: The doubling-interval DCR formula and low-energy/temperature alternatives are the oracles; fixed 3.9/10/0.9 and decimal margins are stated without derivation.

Tolerance: structural

Gates: none.

Source: [tests/Gcam.Tests/ThermalReadoutTests.cs](../tests/Gcam.Tests/ThermalReadoutTests.cs).

```json
{"id":"Gcam.Tests::Gcam.Tests.ThermalReadoutTests","runner":"dotnet","methods":["Dcr_DoublesEveryDoublingInterval_AndBiasCompDoesNotNullIt","DcrHitsLowEnergyResolutionMoreThanThePhotopeak_AndPdeDroops"],"sources":{"tests/Gcam.Tests/ThermalReadoutTests.cs":"67848eb3fce97bcbf5a35dc8c9893454936030413223febec8c393c4ccec20ac","tests/Gcam.Tests/Harness/Rigs.cs":"9b89ce04cdcb302fcefc6636d45a0d1640a48c288d1e0a3638b005c44028c3b2","tests/Gcam.Tests/Harness/Stat.cs":"4502dddc6276f9d1de0c41b2ed7246ed7f00c9266daa12a34d6174ae1cb19029"},"trace":[]}
```


## TransportInvariantTests — Gcam.Tests

Each transport stage against its closed form: the biased source's weight is the detector's solid angle, the crystal stops 1 − e^(−μ·t/cosθ), the mask passes its open fraction plus the leak through tungsten, and the Compton cascade never deposits more than the photon brought.

Oracle: Rectangle solid angle, Beer–Lambert slant absorption and open-fraction-plus-leak are closed-form oracles.

Tolerance: Four weighted-mean/binomial standard errors are used where coded. Decimal absorption and deposit-roundoff ceilings have no separate derived numerical budget. Unexplained margins: tolerance stated without a derivation in the code.

Gates: none.

Source: [tests/Gcam.Tests/TransportInvariantTests.cs](../tests/Gcam.Tests/TransportInvariantTests.cs).

Method groups:

- `BiasedSource_MeanWeightIsTheDetectorSolidAngle`, `Crystal_StopsOneMinusExpOfMuTimesSlantPath`, `Mask_PassesItsOpenFractionPlusTheTungstenLeak`: numerical comparisons described above; gate None.
- `ComptonCascade_NeverDepositsMoreThanThePhotonEnergy`, `Run_IsDeterministicForASeed_AndDiffersAcrossSeeds`, `Run_ReportsMonotoneProgressEndingAtOne`, `Run_StopsWhenCancelled`: exact / structural fixture comparisons; gate None.

```json
{"id":"Gcam.Tests::Gcam.Tests.TransportInvariantTests","runner":"dotnet","methods":["BiasedSource_MeanWeightIsTheDetectorSolidAngle","Crystal_StopsOneMinusExpOfMuTimesSlantPath","Mask_PassesItsOpenFractionPlusTheTungstenLeak","ComptonCascade_NeverDepositsMoreThanThePhotonEnergy","Run_IsDeterministicForASeed_AndDiffersAcrossSeeds","Run_ReportsMonotoneProgressEndingAtOne","Run_StopsWhenCancelled"],"sources":{"tests/Gcam.Tests/TransportInvariantTests.cs":"172ed505e6a4ff6d51684b97203916a21b80e96bc45fb33a7d92ff63fcec38ba","tests/Gcam.Tests/Harness/Rigs.cs":"9b89ce04cdcb302fcefc6636d45a0d1640a48c288d1e0a3638b005c44028c3b2","tests/Gcam.Tests/Harness/Stat.cs":"4502dddc6276f9d1de0c41b2ed7246ed7f00c9266daa12a34d6174ae1cb19029"},"trace":[{"requirement":"EV-04","selectors":["Mask_PassesItsOpenFractionPlusTheTungstenLeak"]},{"requirement":"EV-07","selectors":["BiasedSource_MeanWeightIsTheDetectorSolidAngle"]},{"requirement":"EV-14","selectors":["ComptonCascade_NeverDepositsMoreThanThePhotonEnergy"]},{"requirement":"EV-19","selectors":["Crystal_StopsOneMinusExpOfMuTimesSlantPath"]}]}
```


## WaveformTests — Gcam.Tests

The C# waveform port (Waveform.cs) must reproduce the validated Python RTL reference (rtl/trap_ref.py + rtl/event_stream.py) bit-for-bit on the deterministic path.

Oracle: Captured Python integer-reference golden values are exact on deterministic paths; pulse floating samples use the coded decimal precision.

Tolerance: ADC math is analytic. Integer-filter proportionality .02 and recovered-energy 15 keV margins are stated without an accumulated-error derivation. Unexplained margins: tolerance stated without a derivation in the code.

Gates: none.

Source: [tests/Gcam.Tests/WaveformTests.cs](../tests/Gcam.Tests/WaveformTests.cs).

Method groups:

- `Constants_MatchThePythonReference`, `ExpPulse_MatchesGolden`, `BiexpPulse_MatchesGolden`, `TrapShape_MatchesGolden`, `CrrcInt_MatchesGolden`, `Blr_MatchesGolden`: exact / structural fixture comparisons; gate None.
- `TrapFlatTop_IsProportionalToEnergy`, `DeriveAdc_MatchesDatasheetMath`, `Rasterize_RecoversEnergy_ViaFlatTopCalibration`: numerical comparisons described above; gate None.

```json
{"id":"Gcam.Tests::Gcam.Tests.WaveformTests","runner":"dotnet","methods":["Constants_MatchThePythonReference","ExpPulse_MatchesGolden","BiexpPulse_MatchesGolden","TrapShape_MatchesGolden","CrrcInt_MatchesGolden","Blr_MatchesGolden","TrapFlatTop_IsProportionalToEnergy","DeriveAdc_MatchesDatasheetMath","Rasterize_RecoversEnergy_ViaFlatTopCalibration"],"sources":{"tests/Gcam.Tests/WaveformTests.cs":"9cda2dc770eb9f99806660da6c90820553edea884d948c04e42350f1d0fdccc6","tests/Gcam.Tests/Harness/Rigs.cs":"9b89ce04cdcb302fcefc6636d45a0d1640a48c288d1e0a3638b005c44028c3b2","tests/Gcam.Tests/Harness/Stat.cs":"4502dddc6276f9d1de0c41b2ed7246ed7f00c9266daa12a34d6174ae1cb19029"},"trace":[{"requirement":"EV-17","selectors":["BiexpPulse_MatchesGolden","Blr_MatchesGolden","Constants_MatchThePythonReference","CrrcInt_MatchesGolden","DeriveAdc_MatchesDatasheetMath","ExpPulse_MatchesGolden","Rasterize_RecoversEnergy_ViaFlatTopCalibration","TrapFlatTop_IsProportionalToEnergy","TrapShape_MatchesGolden"]}]}
```


## archived-replay — checker

Verifies retained evidence and byte-exact report regeneration without rebuilding historical binaries.

Oracle: Pinned artifact hashes and canonical sanitized TRX re-normalization must reproduce per-case JSON exactly; pinned selection/catalog/generator/report output must agree byte for byte. An empty archive is reported as zero milestones, not as a passed milestone.

Tolerance: exact

Gates: none.

Source: [samples/testing/test_records.py](../samples/testing/test_records.py).

```json
{"id":"checker::archived-replay","runner":"checker","methods":["archived-replay"],"sources":{"samples/testing/test_records.py":"1af81531d20e73db4f23d501169114547dd0f981d015c892ea7a38fd66e47e31"},"trace":[]}
```


## calibration — checker

Verifies calibration provenance and regenerated calibration documents.

Oracle: Pinned calibration-records.json and SHA-256 inputs, seed/role/executor bindings, recomputed thresholds/counts and exact generated-document bytes. Numerical cross-checks are the expressions in calibration_record.py; the angular-resolution integer recovery requires |rate*N−round(rate*N)|<0.33.

Tolerance: Integer-count recovery allows <0.33 count; tolerance stated without a derivation in the code. Hashes and generated bytes are exact.

Gates: none.

Source: [samples/evidence/calibration_record.py](../samples/evidence/calibration_record.py).

```json
{"id":"checker::calibration","runner":"checker","methods":["calibration"],"sources":{"samples/evidence/calibration_record.py":"99bb390a2b1a185e9e9cc50a357877136f332220163090c25028d634a869b134"},"trace":[]}
```


## test-catalog — checker

Verifies catalog membership, source fingerprints and trace selectors against source and Release discovery.

Oracle: Source-discovered unit/method membership, current source fingerprints and resolving trace selectors. Optional independent Release --list-tests discovery supplies the expanded xUnit counts.

Tolerance: exact

Gates: none.

Source: [samples/testing/test_records.py](../samples/testing/test_records.py).

```json
{"id":"checker::test-catalog","runner":"checker","methods":["test-catalog"],"sources":{"samples/testing/test_records.py":"1af81531d20e73db4f23d501169114547dd0f981d015c892ea7a38fd66e47e31"},"trace":[]}
```


## test-record-self-test — checker

Verifies precise refusals for isolated record, adapter, sanitizer and archive defects.

Oracle: Each named one-defect fixture must raise the specified refusal; synthetic TRX/unittest/cocotb, actual unittest callback/subtest and sanitizer fixtures must normalize to the explicit expected rows.

Tolerance: exact

Gates: none.

Source: [samples/testing/test_records.py](../samples/testing/test_records.py).

```json
{"id":"checker::test-record-self-test","runner":"checker","methods":["test-record-self-test"],"sources":{"samples/testing/test_records.py":"1af81531d20e73db4f23d501169114547dd0f981d015c892ea7a38fd66e47e31"},"trace":[]}
```


## test_blr@baseline_restorer — cocotb

Verifies test_blr under configuration baseline_restorer.

Oracle: Python trap_ref.blr supplies samples from the retained shaped MC stream; matching searches latency 0-4. Baseline/pulse behavior is compared with the uncorrected stream.

Tolerance: raw_min<-15000, |blr_min|<|raw_min|/3 and blr_max>100000: tolerance stated without a derivation in the code. Reference sample equality is exact.

Gates: none.

Source: [rtl/test_blr.py](../rtl/test_blr.py), [rtl/crrc_contract.py](../rtl/crrc_contract.py), [rtl/run_cocotb.py](../rtl/run_cocotb.py).

```json
{"id":"cocotb::test_blr@baseline_restorer","runner":"cocotb","methods":["blr_matches_reference_and_removes_walk"],"sources":{"rtl/test_blr.py":"5bd867d72fd95d24cecab124ff41bd8698aa37ee1bf09767969d4b6e2bc709a9","rtl/crrc_contract.py":"688a834d209a9245552948606c564a7d3fc9e978eec288c1cd7adfa0f3897bf7","rtl/run_cocotb.py":"11c695dd86af561bfe12de33ce672b1a1d34227851524f6650ffa87b0a55fc7d"},"trace":[]}
```


## test_crrc@fast-bgo-f0 — cocotb

Verifies test_crrc under configuration fast-bgo-f0.

Oracle: Python trap_ref.crrc_int supplies integer sample outputs; independent fixture math checks ORDER/A/K/F. Reset/pulse fixtures use seed 20261002; optional C# vectors supply a second exact comparison.

Tolerance: exact

Gates: `GCAM_CRRC_VECTORS`.

Source: [rtl/test_crrc.py](../rtl/test_crrc.py), [rtl/crrc_contract.py](../rtl/crrc_contract.py), [rtl/run_cocotb.py](../rtl/run_cocotb.py).

Method groups:

- `finite_rise_energies_match_reference`, `near_threshold_sweep_matches_reference`, `signed_full_scale_noise_and_valid_gaps_match_reference`: Python integer-reference oracle; exact; gate none.
- `legacy_golden_and_proportionality_are_preserved`: pulse fixture; exact; gate none.
- `csharp_vectors_match_python_and_rtl`: C# vector oracle; exact; gate GCAM_CRRC_VECTORS.

```json
{"id":"cocotb::test_crrc@fast-bgo-f0","runner":"cocotb","methods":["finite_rise_energies_match_reference","near_threshold_sweep_matches_reference","signed_full_scale_noise_and_valid_gaps_match_reference","legacy_golden_and_proportionality_are_preserved","csharp_vectors_match_python_and_rtl"],"sources":{"rtl/test_crrc.py":"fdad77862f70cd62aab1116d154117ffcc27533c8582c33eafad4db826baec7d","rtl/crrc_contract.py":"688a834d209a9245552948606c564a7d3fc9e978eec288c1cd7adfa0f3897bf7","rtl/run_cocotb.py":"11c695dd86af561bfe12de33ce672b1a1d34227851524f6650ffa87b0a55fc7d"},"trace":[]}
```


## test_crrc@fast-bgo-f12 — cocotb

Verifies test_crrc under configuration fast-bgo-f12.

Oracle: Python trap_ref.crrc_int supplies integer sample outputs; independent fixture math checks ORDER/A/K/F. Reset/pulse fixtures use seed 20261002; optional C# vectors supply a second exact comparison.

Tolerance: exact

Gates: `GCAM_CRRC_VECTORS`.

Source: [rtl/test_crrc.py](../rtl/test_crrc.py), [rtl/crrc_contract.py](../rtl/crrc_contract.py), [rtl/run_cocotb.py](../rtl/run_cocotb.py).

Method groups:

- `finite_rise_energies_match_reference`, `near_threshold_sweep_matches_reference`, `signed_full_scale_noise_and_valid_gaps_match_reference`: Python integer-reference oracle; exact; gate none.
- `legacy_golden_and_proportionality_are_preserved`: pulse fixture; exact; gate none.
- `csharp_vectors_match_python_and_rtl`: C# vector oracle; exact; gate GCAM_CRRC_VECTORS.

```json
{"id":"cocotb::test_crrc@fast-bgo-f12","runner":"cocotb","methods":["finite_rise_energies_match_reference","near_threshold_sweep_matches_reference","signed_full_scale_noise_and_valid_gaps_match_reference","legacy_golden_and_proportionality_are_preserved","csharp_vectors_match_python_and_rtl"],"sources":{"rtl/test_crrc.py":"fdad77862f70cd62aab1116d154117ffcc27533c8582c33eafad4db826baec7d","rtl/crrc_contract.py":"688a834d209a9245552948606c564a7d3fc9e978eec288c1cd7adfa0f3897bf7","rtl/run_cocotb.py":"11c695dd86af561bfe12de33ce672b1a1d34227851524f6650ffa87b0a55fc7d"},"trace":[]}
```


## test_crrc@fast-gagg-f0 — cocotb

Verifies test_crrc under configuration fast-gagg-f0.

Oracle: Python trap_ref.crrc_int supplies integer sample outputs; independent fixture math checks ORDER/A/K/F. Reset/pulse fixtures use seed 20261002; optional C# vectors supply a second exact comparison.

Tolerance: exact

Gates: `GCAM_CRRC_VECTORS`.

Source: [rtl/test_crrc.py](../rtl/test_crrc.py), [rtl/crrc_contract.py](../rtl/crrc_contract.py), [rtl/run_cocotb.py](../rtl/run_cocotb.py).

Method groups:

- `finite_rise_energies_match_reference`, `near_threshold_sweep_matches_reference`, `signed_full_scale_noise_and_valid_gaps_match_reference`: Python integer-reference oracle; exact; gate none.
- `legacy_golden_and_proportionality_are_preserved`: pulse fixture; exact; gate none.
- `csharp_vectors_match_python_and_rtl`: C# vector oracle; exact; gate GCAM_CRRC_VECTORS.

```json
{"id":"cocotb::test_crrc@fast-gagg-f0","runner":"cocotb","methods":["finite_rise_energies_match_reference","near_threshold_sweep_matches_reference","signed_full_scale_noise_and_valid_gaps_match_reference","legacy_golden_and_proportionality_are_preserved","csharp_vectors_match_python_and_rtl"],"sources":{"rtl/test_crrc.py":"fdad77862f70cd62aab1116d154117ffcc27533c8582c33eafad4db826baec7d","rtl/crrc_contract.py":"688a834d209a9245552948606c564a7d3fc9e978eec288c1cd7adfa0f3897bf7","rtl/run_cocotb.py":"11c695dd86af561bfe12de33ce672b1a1d34227851524f6650ffa87b0a55fc7d"},"trace":[]}
```


## test_crrc@fast-gagg-f12 — cocotb

Verifies test_crrc under configuration fast-gagg-f12.

Oracle: Python trap_ref.crrc_int supplies integer sample outputs; independent fixture math checks ORDER/A/K/F. Reset/pulse fixtures use seed 20261002; optional C# vectors supply a second exact comparison.

Tolerance: exact

Gates: `GCAM_CRRC_VECTORS`.

Source: [rtl/test_crrc.py](../rtl/test_crrc.py), [rtl/crrc_contract.py](../rtl/crrc_contract.py), [rtl/run_cocotb.py](../rtl/run_cocotb.py).

Method groups:

- `finite_rise_energies_match_reference`, `near_threshold_sweep_matches_reference`, `signed_full_scale_noise_and_valid_gaps_match_reference`: Python integer-reference oracle; exact; gate none.
- `legacy_golden_and_proportionality_are_preserved`: pulse fixture; exact; gate none.
- `csharp_vectors_match_python_and_rtl`: C# vector oracle; exact; gate GCAM_CRRC_VECTORS.

```json
{"id":"cocotb::test_crrc@fast-gagg-f12","runner":"cocotb","methods":["finite_rise_energies_match_reference","near_threshold_sweep_matches_reference","signed_full_scale_noise_and_valid_gaps_match_reference","legacy_golden_and_proportionality_are_preserved","csharp_vectors_match_python_and_rtl"],"sources":{"rtl/test_crrc.py":"fdad77862f70cd62aab1116d154117ffcc27533c8582c33eafad4db826baec7d","rtl/crrc_contract.py":"688a834d209a9245552948606c564a7d3fc9e978eec288c1cd7adfa0f3897bf7","rtl/run_cocotb.py":"11c695dd86af561bfe12de33ce672b1a1d34227851524f6650ffa87b0a55fc7d"},"trace":[]}
```


## test_crrc@fast-lyso-f0 — cocotb

Verifies test_crrc under configuration fast-lyso-f0.

Oracle: Python trap_ref.crrc_int supplies integer sample outputs; independent fixture math checks ORDER/A/K/F. Reset/pulse fixtures use seed 20261002; optional C# vectors supply a second exact comparison.

Tolerance: exact

Gates: `GCAM_CRRC_VECTORS`.

Source: [rtl/test_crrc.py](../rtl/test_crrc.py), [rtl/crrc_contract.py](../rtl/crrc_contract.py), [rtl/run_cocotb.py](../rtl/run_cocotb.py).

Method groups:

- `finite_rise_energies_match_reference`, `near_threshold_sweep_matches_reference`, `signed_full_scale_noise_and_valid_gaps_match_reference`: Python integer-reference oracle; exact; gate none.
- `legacy_golden_and_proportionality_are_preserved`: pulse fixture; exact; gate none.
- `csharp_vectors_match_python_and_rtl`: C# vector oracle; exact; gate GCAM_CRRC_VECTORS.

```json
{"id":"cocotb::test_crrc@fast-lyso-f0","runner":"cocotb","methods":["finite_rise_energies_match_reference","near_threshold_sweep_matches_reference","signed_full_scale_noise_and_valid_gaps_match_reference","legacy_golden_and_proportionality_are_preserved","csharp_vectors_match_python_and_rtl"],"sources":{"rtl/test_crrc.py":"fdad77862f70cd62aab1116d154117ffcc27533c8582c33eafad4db826baec7d","rtl/crrc_contract.py":"688a834d209a9245552948606c564a7d3fc9e978eec288c1cd7adfa0f3897bf7","rtl/run_cocotb.py":"11c695dd86af561bfe12de33ce672b1a1d34227851524f6650ffa87b0a55fc7d"},"trace":[]}
```


## test_crrc@fast-lyso-f12 — cocotb

Verifies test_crrc under configuration fast-lyso-f12.

Oracle: Python trap_ref.crrc_int supplies integer sample outputs; independent fixture math checks ORDER/A/K/F. Reset/pulse fixtures use seed 20261002; optional C# vectors supply a second exact comparison.

Tolerance: exact

Gates: `GCAM_CRRC_VECTORS`.

Source: [rtl/test_crrc.py](../rtl/test_crrc.py), [rtl/crrc_contract.py](../rtl/crrc_contract.py), [rtl/run_cocotb.py](../rtl/run_cocotb.py).

Method groups:

- `finite_rise_energies_match_reference`, `near_threshold_sweep_matches_reference`, `signed_full_scale_noise_and_valid_gaps_match_reference`: Python integer-reference oracle; exact; gate none.
- `legacy_golden_and_proportionality_are_preserved`: pulse fixture; exact; gate none.
- `csharp_vectors_match_python_and_rtl`: C# vector oracle; exact; gate GCAM_CRRC_VECTORS.

```json
{"id":"cocotb::test_crrc@fast-lyso-f12","runner":"cocotb","methods":["finite_rise_energies_match_reference","near_threshold_sweep_matches_reference","signed_full_scale_noise_and_valid_gaps_match_reference","legacy_golden_and_proportionality_are_preserved","csharp_vectors_match_python_and_rtl"],"sources":{"rtl/test_crrc.py":"fdad77862f70cd62aab1116d154117ffcc27533c8582c33eafad4db826baec7d","rtl/crrc_contract.py":"688a834d209a9245552948606c564a7d3fc9e978eec288c1cd7adfa0f3897bf7","rtl/run_cocotb.py":"11c695dd86af561bfe12de33ce672b1a1d34227851524f6650ffa87b0a55fc7d"},"trace":[]}
```


## test_crrc@fast-nai-f0 — cocotb

Verifies test_crrc under configuration fast-nai-f0.

Oracle: Python trap_ref.crrc_int supplies integer sample outputs; independent fixture math checks ORDER/A/K/F. Reset/pulse fixtures use seed 20261002; optional C# vectors supply a second exact comparison.

Tolerance: exact

Gates: `GCAM_CRRC_VECTORS`.

Source: [rtl/test_crrc.py](../rtl/test_crrc.py), [rtl/crrc_contract.py](../rtl/crrc_contract.py), [rtl/run_cocotb.py](../rtl/run_cocotb.py).

Method groups:

- `finite_rise_energies_match_reference`, `near_threshold_sweep_matches_reference`, `signed_full_scale_noise_and_valid_gaps_match_reference`: Python integer-reference oracle; exact; gate none.
- `legacy_golden_and_proportionality_are_preserved`: pulse fixture; exact; gate none.
- `csharp_vectors_match_python_and_rtl`: C# vector oracle; exact; gate GCAM_CRRC_VECTORS.

```json
{"id":"cocotb::test_crrc@fast-nai-f0","runner":"cocotb","methods":["finite_rise_energies_match_reference","near_threshold_sweep_matches_reference","signed_full_scale_noise_and_valid_gaps_match_reference","legacy_golden_and_proportionality_are_preserved","csharp_vectors_match_python_and_rtl"],"sources":{"rtl/test_crrc.py":"fdad77862f70cd62aab1116d154117ffcc27533c8582c33eafad4db826baec7d","rtl/crrc_contract.py":"688a834d209a9245552948606c564a7d3fc9e978eec288c1cd7adfa0f3897bf7","rtl/run_cocotb.py":"11c695dd86af561bfe12de33ce672b1a1d34227851524f6650ffa87b0a55fc7d"},"trace":[]}
```


## test_crrc@fast-nai-f12 — cocotb

Verifies test_crrc under configuration fast-nai-f12.

Oracle: Python trap_ref.crrc_int supplies integer sample outputs; independent fixture math checks ORDER/A/K/F. Reset/pulse fixtures use seed 20261002; optional C# vectors supply a second exact comparison.

Tolerance: exact

Gates: `GCAM_CRRC_VECTORS`.

Source: [rtl/test_crrc.py](../rtl/test_crrc.py), [rtl/crrc_contract.py](../rtl/crrc_contract.py), [rtl/run_cocotb.py](../rtl/run_cocotb.py).

Method groups:

- `finite_rise_energies_match_reference`, `near_threshold_sweep_matches_reference`, `signed_full_scale_noise_and_valid_gaps_match_reference`: Python integer-reference oracle; exact; gate none.
- `legacy_golden_and_proportionality_are_preserved`: pulse fixture; exact; gate none.
- `csharp_vectors_match_python_and_rtl`: C# vector oracle; exact; gate GCAM_CRRC_VECTORS.

```json
{"id":"cocotb::test_crrc@fast-nai-f12","runner":"cocotb","methods":["finite_rise_energies_match_reference","near_threshold_sweep_matches_reference","signed_full_scale_noise_and_valid_gaps_match_reference","legacy_golden_and_proportionality_are_preserved","csharp_vectors_match_python_and_rtl"],"sources":{"rtl/test_crrc.py":"fdad77862f70cd62aab1116d154117ffcc27533c8582c33eafad4db826baec7d","rtl/crrc_contract.py":"688a834d209a9245552948606c564a7d3fc9e978eec288c1cd7adfa0f3897bf7","rtl/run_cocotb.py":"11c695dd86af561bfe12de33ce672b1a1d34227851524f6650ffa87b0a55fc7d"},"trace":[]}
```


## test_crrc@legacy-f0 — cocotb

Verifies test_crrc under configuration legacy-f0.

Oracle: Python trap_ref.crrc_int supplies integer sample outputs; independent fixture math checks ORDER/A/K/F. Reset/pulse fixtures use seed 20261002; optional C# vectors supply a second exact comparison.

Tolerance: Legacy peak proportionality allows |p800/p400-2|<.05: tolerance stated without a derivation in the code. F=0 golden values and sample comparisons are exact.

Gates: `GCAM_CRRC_VECTORS`.

Source: [rtl/test_crrc.py](../rtl/test_crrc.py), [rtl/crrc_contract.py](../rtl/crrc_contract.py), [rtl/run_cocotb.py](../rtl/run_cocotb.py).

Method groups:

- `finite_rise_energies_match_reference`, `near_threshold_sweep_matches_reference`, `signed_full_scale_noise_and_valid_gaps_match_reference`: Python integer-reference oracle; exact; gate none.
- `legacy_golden_and_proportionality_are_preserved`: pulse fixture; Legacy peak proportionality allows |p800/p400-2|<.05: tolerance stated without a derivation in the code. F=0 golden values and sample comparisons are exact.; gate none.
- `csharp_vectors_match_python_and_rtl`: C# vector oracle; exact; gate GCAM_CRRC_VECTORS.

```json
{"id":"cocotb::test_crrc@legacy-f0","runner":"cocotb","methods":["finite_rise_energies_match_reference","near_threshold_sweep_matches_reference","signed_full_scale_noise_and_valid_gaps_match_reference","legacy_golden_and_proportionality_are_preserved","csharp_vectors_match_python_and_rtl"],"sources":{"rtl/test_crrc.py":"fdad77862f70cd62aab1116d154117ffcc27533c8582c33eafad4db826baec7d","rtl/crrc_contract.py":"688a834d209a9245552948606c564a7d3fc9e978eec288c1cd7adfa0f3897bf7","rtl/run_cocotb.py":"11c695dd86af561bfe12de33ce672b1a1d34227851524f6650ffa87b0a55fc7d"},"trace":[]}
```


## test_crrc@legacy-f12 — cocotb

Verifies test_crrc under configuration legacy-f12.

Oracle: Python trap_ref.crrc_int supplies integer sample outputs; independent fixture math checks ORDER/A/K/F. Reset/pulse fixtures use seed 20261002; optional C# vectors supply a second exact comparison.

Tolerance: Legacy peak proportionality allows |p800/p400-2|<.05: tolerance stated without a derivation in the code. F=0 golden values and sample comparisons are exact.

Gates: `GCAM_CRRC_VECTORS`.

Source: [rtl/test_crrc.py](../rtl/test_crrc.py), [rtl/crrc_contract.py](../rtl/crrc_contract.py), [rtl/run_cocotb.py](../rtl/run_cocotb.py).

Method groups:

- `finite_rise_energies_match_reference`, `near_threshold_sweep_matches_reference`, `signed_full_scale_noise_and_valid_gaps_match_reference`: Python integer-reference oracle; exact; gate none.
- `legacy_golden_and_proportionality_are_preserved`: pulse fixture; Legacy peak proportionality allows |p800/p400-2|<.05: tolerance stated without a derivation in the code. F=0 golden values and sample comparisons are exact.; gate none.
- `csharp_vectors_match_python_and_rtl`: C# vector oracle; exact; gate GCAM_CRRC_VECTORS.

```json
{"id":"cocotb::test_crrc@legacy-f12","runner":"cocotb","methods":["finite_rise_energies_match_reference","near_threshold_sweep_matches_reference","signed_full_scale_noise_and_valid_gaps_match_reference","legacy_golden_and_proportionality_are_preserved","csharp_vectors_match_python_and_rtl"],"sources":{"rtl/test_crrc.py":"fdad77862f70cd62aab1116d154117ffcc27533c8582c33eafad4db826baec7d","rtl/crrc_contract.py":"688a834d209a9245552948606c564a7d3fc9e978eec288c1cd7adfa0f3897bf7","rtl/run_cocotb.py":"11c695dd86af561bfe12de33ce672b1a1d34227851524f6650ffa87b0a55fc7d"},"trace":[]}
```


## test_crrc@original-bgo-f0 — cocotb

Verifies test_crrc under configuration original-bgo-f0.

Oracle: Python trap_ref.crrc_int supplies integer sample outputs; independent fixture math checks ORDER/A/K/F. Reset/pulse fixtures use seed 20261002; optional C# vectors supply a second exact comparison.

Tolerance: exact

Gates: `GCAM_CRRC_VECTORS`.

Source: [rtl/test_crrc.py](../rtl/test_crrc.py), [rtl/crrc_contract.py](../rtl/crrc_contract.py), [rtl/run_cocotb.py](../rtl/run_cocotb.py).

Method groups:

- `finite_rise_energies_match_reference`, `near_threshold_sweep_matches_reference`, `signed_full_scale_noise_and_valid_gaps_match_reference`: Python integer-reference oracle; exact; gate none.
- `legacy_golden_and_proportionality_are_preserved`: pulse fixture; exact; gate none.
- `csharp_vectors_match_python_and_rtl`: C# vector oracle; exact; gate GCAM_CRRC_VECTORS.

```json
{"id":"cocotb::test_crrc@original-bgo-f0","runner":"cocotb","methods":["finite_rise_energies_match_reference","near_threshold_sweep_matches_reference","signed_full_scale_noise_and_valid_gaps_match_reference","legacy_golden_and_proportionality_are_preserved","csharp_vectors_match_python_and_rtl"],"sources":{"rtl/test_crrc.py":"fdad77862f70cd62aab1116d154117ffcc27533c8582c33eafad4db826baec7d","rtl/crrc_contract.py":"688a834d209a9245552948606c564a7d3fc9e978eec288c1cd7adfa0f3897bf7","rtl/run_cocotb.py":"11c695dd86af561bfe12de33ce672b1a1d34227851524f6650ffa87b0a55fc7d"},"trace":[]}
```


## test_crrc@original-bgo-f12 — cocotb

Verifies test_crrc under configuration original-bgo-f12.

Oracle: Python trap_ref.crrc_int supplies integer sample outputs; independent fixture math checks ORDER/A/K/F. Reset/pulse fixtures use seed 20261002; optional C# vectors supply a second exact comparison.

Tolerance: exact

Gates: `GCAM_CRRC_VECTORS`.

Source: [rtl/test_crrc.py](../rtl/test_crrc.py), [rtl/crrc_contract.py](../rtl/crrc_contract.py), [rtl/run_cocotb.py](../rtl/run_cocotb.py).

Method groups:

- `finite_rise_energies_match_reference`, `near_threshold_sweep_matches_reference`, `signed_full_scale_noise_and_valid_gaps_match_reference`: Python integer-reference oracle; exact; gate none.
- `legacy_golden_and_proportionality_are_preserved`: pulse fixture; exact; gate none.
- `csharp_vectors_match_python_and_rtl`: C# vector oracle; exact; gate GCAM_CRRC_VECTORS.

```json
{"id":"cocotb::test_crrc@original-bgo-f12","runner":"cocotb","methods":["finite_rise_energies_match_reference","near_threshold_sweep_matches_reference","signed_full_scale_noise_and_valid_gaps_match_reference","legacy_golden_and_proportionality_are_preserved","csharp_vectors_match_python_and_rtl"],"sources":{"rtl/test_crrc.py":"fdad77862f70cd62aab1116d154117ffcc27533c8582c33eafad4db826baec7d","rtl/crrc_contract.py":"688a834d209a9245552948606c564a7d3fc9e978eec288c1cd7adfa0f3897bf7","rtl/run_cocotb.py":"11c695dd86af561bfe12de33ce672b1a1d34227851524f6650ffa87b0a55fc7d"},"trace":[]}
```


## test_crrc@original-gagg-f0 — cocotb

Verifies test_crrc under configuration original-gagg-f0.

Oracle: Python trap_ref.crrc_int supplies integer sample outputs; independent fixture math checks ORDER/A/K/F. Reset/pulse fixtures use seed 20261002; optional C# vectors supply a second exact comparison.

Tolerance: exact

Gates: `GCAM_CRRC_VECTORS`.

Source: [rtl/test_crrc.py](../rtl/test_crrc.py), [rtl/crrc_contract.py](../rtl/crrc_contract.py), [rtl/run_cocotb.py](../rtl/run_cocotb.py).

Method groups:

- `finite_rise_energies_match_reference`, `near_threshold_sweep_matches_reference`, `signed_full_scale_noise_and_valid_gaps_match_reference`: Python integer-reference oracle; exact; gate none.
- `legacy_golden_and_proportionality_are_preserved`: pulse fixture; exact; gate none.
- `csharp_vectors_match_python_and_rtl`: C# vector oracle; exact; gate GCAM_CRRC_VECTORS.

```json
{"id":"cocotb::test_crrc@original-gagg-f0","runner":"cocotb","methods":["finite_rise_energies_match_reference","near_threshold_sweep_matches_reference","signed_full_scale_noise_and_valid_gaps_match_reference","legacy_golden_and_proportionality_are_preserved","csharp_vectors_match_python_and_rtl"],"sources":{"rtl/test_crrc.py":"fdad77862f70cd62aab1116d154117ffcc27533c8582c33eafad4db826baec7d","rtl/crrc_contract.py":"688a834d209a9245552948606c564a7d3fc9e978eec288c1cd7adfa0f3897bf7","rtl/run_cocotb.py":"11c695dd86af561bfe12de33ce672b1a1d34227851524f6650ffa87b0a55fc7d"},"trace":[]}
```


## test_crrc@original-gagg-f12 — cocotb

Verifies test_crrc under configuration original-gagg-f12.

Oracle: Python trap_ref.crrc_int supplies integer sample outputs; independent fixture math checks ORDER/A/K/F. Reset/pulse fixtures use seed 20261002; optional C# vectors supply a second exact comparison.

Tolerance: exact

Gates: `GCAM_CRRC_VECTORS`.

Source: [rtl/test_crrc.py](../rtl/test_crrc.py), [rtl/crrc_contract.py](../rtl/crrc_contract.py), [rtl/run_cocotb.py](../rtl/run_cocotb.py).

Method groups:

- `finite_rise_energies_match_reference`, `near_threshold_sweep_matches_reference`, `signed_full_scale_noise_and_valid_gaps_match_reference`: Python integer-reference oracle; exact; gate none.
- `legacy_golden_and_proportionality_are_preserved`: pulse fixture; exact; gate none.
- `csharp_vectors_match_python_and_rtl`: C# vector oracle; exact; gate GCAM_CRRC_VECTORS.

```json
{"id":"cocotb::test_crrc@original-gagg-f12","runner":"cocotb","methods":["finite_rise_energies_match_reference","near_threshold_sweep_matches_reference","signed_full_scale_noise_and_valid_gaps_match_reference","legacy_golden_and_proportionality_are_preserved","csharp_vectors_match_python_and_rtl"],"sources":{"rtl/test_crrc.py":"fdad77862f70cd62aab1116d154117ffcc27533c8582c33eafad4db826baec7d","rtl/crrc_contract.py":"688a834d209a9245552948606c564a7d3fc9e978eec288c1cd7adfa0f3897bf7","rtl/run_cocotb.py":"11c695dd86af561bfe12de33ce672b1a1d34227851524f6650ffa87b0a55fc7d"},"trace":[]}
```


## test_crrc@original-lyso-f0 — cocotb

Verifies test_crrc under configuration original-lyso-f0.

Oracle: Python trap_ref.crrc_int supplies integer sample outputs; independent fixture math checks ORDER/A/K/F. Reset/pulse fixtures use seed 20261002; optional C# vectors supply a second exact comparison.

Tolerance: exact

Gates: `GCAM_CRRC_VECTORS`.

Source: [rtl/test_crrc.py](../rtl/test_crrc.py), [rtl/crrc_contract.py](../rtl/crrc_contract.py), [rtl/run_cocotb.py](../rtl/run_cocotb.py).

Method groups:

- `finite_rise_energies_match_reference`, `near_threshold_sweep_matches_reference`, `signed_full_scale_noise_and_valid_gaps_match_reference`: Python integer-reference oracle; exact; gate none.
- `legacy_golden_and_proportionality_are_preserved`: pulse fixture; exact; gate none.
- `csharp_vectors_match_python_and_rtl`: C# vector oracle; exact; gate GCAM_CRRC_VECTORS.

```json
{"id":"cocotb::test_crrc@original-lyso-f0","runner":"cocotb","methods":["finite_rise_energies_match_reference","near_threshold_sweep_matches_reference","signed_full_scale_noise_and_valid_gaps_match_reference","legacy_golden_and_proportionality_are_preserved","csharp_vectors_match_python_and_rtl"],"sources":{"rtl/test_crrc.py":"fdad77862f70cd62aab1116d154117ffcc27533c8582c33eafad4db826baec7d","rtl/crrc_contract.py":"688a834d209a9245552948606c564a7d3fc9e978eec288c1cd7adfa0f3897bf7","rtl/run_cocotb.py":"11c695dd86af561bfe12de33ce672b1a1d34227851524f6650ffa87b0a55fc7d"},"trace":[]}
```


## test_crrc@original-lyso-f12 — cocotb

Verifies test_crrc under configuration original-lyso-f12.

Oracle: Python trap_ref.crrc_int supplies integer sample outputs; independent fixture math checks ORDER/A/K/F. Reset/pulse fixtures use seed 20261002; optional C# vectors supply a second exact comparison.

Tolerance: exact

Gates: `GCAM_CRRC_VECTORS`.

Source: [rtl/test_crrc.py](../rtl/test_crrc.py), [rtl/crrc_contract.py](../rtl/crrc_contract.py), [rtl/run_cocotb.py](../rtl/run_cocotb.py).

Method groups:

- `finite_rise_energies_match_reference`, `near_threshold_sweep_matches_reference`, `signed_full_scale_noise_and_valid_gaps_match_reference`: Python integer-reference oracle; exact; gate none.
- `legacy_golden_and_proportionality_are_preserved`: pulse fixture; exact; gate none.
- `csharp_vectors_match_python_and_rtl`: C# vector oracle; exact; gate GCAM_CRRC_VECTORS.

```json
{"id":"cocotb::test_crrc@original-lyso-f12","runner":"cocotb","methods":["finite_rise_energies_match_reference","near_threshold_sweep_matches_reference","signed_full_scale_noise_and_valid_gaps_match_reference","legacy_golden_and_proportionality_are_preserved","csharp_vectors_match_python_and_rtl"],"sources":{"rtl/test_crrc.py":"fdad77862f70cd62aab1116d154117ffcc27533c8582c33eafad4db826baec7d","rtl/crrc_contract.py":"688a834d209a9245552948606c564a7d3fc9e978eec288c1cd7adfa0f3897bf7","rtl/run_cocotb.py":"11c695dd86af561bfe12de33ce672b1a1d34227851524f6650ffa87b0a55fc7d"},"trace":[]}
```


## test_crrc@original-nai-f0 — cocotb

Verifies test_crrc under configuration original-nai-f0.

Oracle: Python trap_ref.crrc_int supplies integer sample outputs; independent fixture math checks ORDER/A/K/F. Reset/pulse fixtures use seed 20261002; optional C# vectors supply a second exact comparison.

Tolerance: exact

Gates: `GCAM_CRRC_VECTORS`.

Source: [rtl/test_crrc.py](../rtl/test_crrc.py), [rtl/crrc_contract.py](../rtl/crrc_contract.py), [rtl/run_cocotb.py](../rtl/run_cocotb.py).

Method groups:

- `finite_rise_energies_match_reference`, `near_threshold_sweep_matches_reference`, `signed_full_scale_noise_and_valid_gaps_match_reference`: Python integer-reference oracle; exact; gate none.
- `legacy_golden_and_proportionality_are_preserved`: pulse fixture; exact; gate none.
- `csharp_vectors_match_python_and_rtl`: C# vector oracle; exact; gate GCAM_CRRC_VECTORS.

```json
{"id":"cocotb::test_crrc@original-nai-f0","runner":"cocotb","methods":["finite_rise_energies_match_reference","near_threshold_sweep_matches_reference","signed_full_scale_noise_and_valid_gaps_match_reference","legacy_golden_and_proportionality_are_preserved","csharp_vectors_match_python_and_rtl"],"sources":{"rtl/test_crrc.py":"fdad77862f70cd62aab1116d154117ffcc27533c8582c33eafad4db826baec7d","rtl/crrc_contract.py":"688a834d209a9245552948606c564a7d3fc9e978eec288c1cd7adfa0f3897bf7","rtl/run_cocotb.py":"11c695dd86af561bfe12de33ce672b1a1d34227851524f6650ffa87b0a55fc7d"},"trace":[]}
```


## test_crrc@original-nai-f12 — cocotb

Verifies test_crrc under configuration original-nai-f12.

Oracle: Python trap_ref.crrc_int supplies integer sample outputs; independent fixture math checks ORDER/A/K/F. Reset/pulse fixtures use seed 20261002; optional C# vectors supply a second exact comparison.

Tolerance: exact

Gates: `GCAM_CRRC_VECTORS`.

Source: [rtl/test_crrc.py](../rtl/test_crrc.py), [rtl/crrc_contract.py](../rtl/crrc_contract.py), [rtl/run_cocotb.py](../rtl/run_cocotb.py).

Method groups:

- `finite_rise_energies_match_reference`, `near_threshold_sweep_matches_reference`, `signed_full_scale_noise_and_valid_gaps_match_reference`: Python integer-reference oracle; exact; gate none.
- `legacy_golden_and_proportionality_are_preserved`: pulse fixture; exact; gate none.
- `csharp_vectors_match_python_and_rtl`: C# vector oracle; exact; gate GCAM_CRRC_VECTORS.

```json
{"id":"cocotb::test_crrc@original-nai-f12","runner":"cocotb","methods":["finite_rise_energies_match_reference","near_threshold_sweep_matches_reference","signed_full_scale_noise_and_valid_gaps_match_reference","legacy_golden_and_proportionality_are_preserved","csharp_vectors_match_python_and_rtl"],"sources":{"rtl/test_crrc.py":"fdad77862f70cd62aab1116d154117ffcc27533c8582c33eafad4db826baec7d","rtl/crrc_contract.py":"688a834d209a9245552948606c564a7d3fc9e978eec288c1cd7adfa0f3897bf7","rtl/run_cocotb.py":"11c695dd86af561bfe12de33ce672b1a1d34227851524f6650ffa87b0a55fc7d"},"trace":[]}
```


## test_crrc@slow-bgo-f0 — cocotb

Verifies test_crrc under configuration slow-bgo-f0.

Oracle: Python trap_ref.crrc_int supplies integer sample outputs; independent fixture math checks ORDER/A/K/F. Reset/pulse fixtures use seed 20261002; optional C# vectors supply a second exact comparison.

Tolerance: exact

Gates: `GCAM_CRRC_VECTORS`.

Source: [rtl/test_crrc.py](../rtl/test_crrc.py), [rtl/crrc_contract.py](../rtl/crrc_contract.py), [rtl/run_cocotb.py](../rtl/run_cocotb.py).

Method groups:

- `finite_rise_energies_match_reference`, `near_threshold_sweep_matches_reference`, `signed_full_scale_noise_and_valid_gaps_match_reference`: Python integer-reference oracle; exact; gate none.
- `legacy_golden_and_proportionality_are_preserved`: pulse fixture; exact; gate none.
- `csharp_vectors_match_python_and_rtl`: C# vector oracle; exact; gate GCAM_CRRC_VECTORS.

```json
{"id":"cocotb::test_crrc@slow-bgo-f0","runner":"cocotb","methods":["finite_rise_energies_match_reference","near_threshold_sweep_matches_reference","signed_full_scale_noise_and_valid_gaps_match_reference","legacy_golden_and_proportionality_are_preserved","csharp_vectors_match_python_and_rtl"],"sources":{"rtl/test_crrc.py":"fdad77862f70cd62aab1116d154117ffcc27533c8582c33eafad4db826baec7d","rtl/crrc_contract.py":"688a834d209a9245552948606c564a7d3fc9e978eec288c1cd7adfa0f3897bf7","rtl/run_cocotb.py":"11c695dd86af561bfe12de33ce672b1a1d34227851524f6650ffa87b0a55fc7d"},"trace":[]}
```


## test_crrc@slow-bgo-f12 — cocotb

Verifies test_crrc under configuration slow-bgo-f12.

Oracle: Python trap_ref.crrc_int supplies integer sample outputs; independent fixture math checks ORDER/A/K/F. Reset/pulse fixtures use seed 20261002; optional C# vectors supply a second exact comparison.

Tolerance: exact

Gates: `GCAM_CRRC_VECTORS`.

Source: [rtl/test_crrc.py](../rtl/test_crrc.py), [rtl/crrc_contract.py](../rtl/crrc_contract.py), [rtl/run_cocotb.py](../rtl/run_cocotb.py).

Method groups:

- `finite_rise_energies_match_reference`, `near_threshold_sweep_matches_reference`, `signed_full_scale_noise_and_valid_gaps_match_reference`: Python integer-reference oracle; exact; gate none.
- `legacy_golden_and_proportionality_are_preserved`: pulse fixture; exact; gate none.
- `csharp_vectors_match_python_and_rtl`: C# vector oracle; exact; gate GCAM_CRRC_VECTORS.

```json
{"id":"cocotb::test_crrc@slow-bgo-f12","runner":"cocotb","methods":["finite_rise_energies_match_reference","near_threshold_sweep_matches_reference","signed_full_scale_noise_and_valid_gaps_match_reference","legacy_golden_and_proportionality_are_preserved","csharp_vectors_match_python_and_rtl"],"sources":{"rtl/test_crrc.py":"fdad77862f70cd62aab1116d154117ffcc27533c8582c33eafad4db826baec7d","rtl/crrc_contract.py":"688a834d209a9245552948606c564a7d3fc9e978eec288c1cd7adfa0f3897bf7","rtl/run_cocotb.py":"11c695dd86af561bfe12de33ce672b1a1d34227851524f6650ffa87b0a55fc7d"},"trace":[]}
```


## test_crrc@slow-gagg-f0 — cocotb

Verifies test_crrc under configuration slow-gagg-f0.

Oracle: Python trap_ref.crrc_int supplies integer sample outputs; independent fixture math checks ORDER/A/K/F. Reset/pulse fixtures use seed 20261002; optional C# vectors supply a second exact comparison.

Tolerance: exact

Gates: `GCAM_CRRC_VECTORS`.

Source: [rtl/test_crrc.py](../rtl/test_crrc.py), [rtl/crrc_contract.py](../rtl/crrc_contract.py), [rtl/run_cocotb.py](../rtl/run_cocotb.py).

Method groups:

- `finite_rise_energies_match_reference`, `near_threshold_sweep_matches_reference`, `signed_full_scale_noise_and_valid_gaps_match_reference`: Python integer-reference oracle; exact; gate none.
- `legacy_golden_and_proportionality_are_preserved`: pulse fixture; exact; gate none.
- `csharp_vectors_match_python_and_rtl`: C# vector oracle; exact; gate GCAM_CRRC_VECTORS.

```json
{"id":"cocotb::test_crrc@slow-gagg-f0","runner":"cocotb","methods":["finite_rise_energies_match_reference","near_threshold_sweep_matches_reference","signed_full_scale_noise_and_valid_gaps_match_reference","legacy_golden_and_proportionality_are_preserved","csharp_vectors_match_python_and_rtl"],"sources":{"rtl/test_crrc.py":"fdad77862f70cd62aab1116d154117ffcc27533c8582c33eafad4db826baec7d","rtl/crrc_contract.py":"688a834d209a9245552948606c564a7d3fc9e978eec288c1cd7adfa0f3897bf7","rtl/run_cocotb.py":"11c695dd86af561bfe12de33ce672b1a1d34227851524f6650ffa87b0a55fc7d"},"trace":[]}
```


## test_crrc@slow-gagg-f12 — cocotb

Verifies test_crrc under configuration slow-gagg-f12.

Oracle: Python trap_ref.crrc_int supplies integer sample outputs; independent fixture math checks ORDER/A/K/F. Reset/pulse fixtures use seed 20261002; optional C# vectors supply a second exact comparison.

Tolerance: exact

Gates: `GCAM_CRRC_VECTORS`.

Source: [rtl/test_crrc.py](../rtl/test_crrc.py), [rtl/crrc_contract.py](../rtl/crrc_contract.py), [rtl/run_cocotb.py](../rtl/run_cocotb.py).

Method groups:

- `finite_rise_energies_match_reference`, `near_threshold_sweep_matches_reference`, `signed_full_scale_noise_and_valid_gaps_match_reference`: Python integer-reference oracle; exact; gate none.
- `legacy_golden_and_proportionality_are_preserved`: pulse fixture; exact; gate none.
- `csharp_vectors_match_python_and_rtl`: C# vector oracle; exact; gate GCAM_CRRC_VECTORS.

```json
{"id":"cocotb::test_crrc@slow-gagg-f12","runner":"cocotb","methods":["finite_rise_energies_match_reference","near_threshold_sweep_matches_reference","signed_full_scale_noise_and_valid_gaps_match_reference","legacy_golden_and_proportionality_are_preserved","csharp_vectors_match_python_and_rtl"],"sources":{"rtl/test_crrc.py":"fdad77862f70cd62aab1116d154117ffcc27533c8582c33eafad4db826baec7d","rtl/crrc_contract.py":"688a834d209a9245552948606c564a7d3fc9e978eec288c1cd7adfa0f3897bf7","rtl/run_cocotb.py":"11c695dd86af561bfe12de33ce672b1a1d34227851524f6650ffa87b0a55fc7d"},"trace":[]}
```


## test_crrc@slow-lyso-f0 — cocotb

Verifies test_crrc under configuration slow-lyso-f0.

Oracle: Python trap_ref.crrc_int supplies integer sample outputs; independent fixture math checks ORDER/A/K/F. Reset/pulse fixtures use seed 20261002; optional C# vectors supply a second exact comparison.

Tolerance: exact

Gates: `GCAM_CRRC_VECTORS`.

Source: [rtl/test_crrc.py](../rtl/test_crrc.py), [rtl/crrc_contract.py](../rtl/crrc_contract.py), [rtl/run_cocotb.py](../rtl/run_cocotb.py).

Method groups:

- `finite_rise_energies_match_reference`, `near_threshold_sweep_matches_reference`, `signed_full_scale_noise_and_valid_gaps_match_reference`: Python integer-reference oracle; exact; gate none.
- `legacy_golden_and_proportionality_are_preserved`: pulse fixture; exact; gate none.
- `csharp_vectors_match_python_and_rtl`: C# vector oracle; exact; gate GCAM_CRRC_VECTORS.

```json
{"id":"cocotb::test_crrc@slow-lyso-f0","runner":"cocotb","methods":["finite_rise_energies_match_reference","near_threshold_sweep_matches_reference","signed_full_scale_noise_and_valid_gaps_match_reference","legacy_golden_and_proportionality_are_preserved","csharp_vectors_match_python_and_rtl"],"sources":{"rtl/test_crrc.py":"fdad77862f70cd62aab1116d154117ffcc27533c8582c33eafad4db826baec7d","rtl/crrc_contract.py":"688a834d209a9245552948606c564a7d3fc9e978eec288c1cd7adfa0f3897bf7","rtl/run_cocotb.py":"11c695dd86af561bfe12de33ce672b1a1d34227851524f6650ffa87b0a55fc7d"},"trace":[]}
```


## test_crrc@slow-lyso-f12 — cocotb

Verifies test_crrc under configuration slow-lyso-f12.

Oracle: Python trap_ref.crrc_int supplies integer sample outputs; independent fixture math checks ORDER/A/K/F. Reset/pulse fixtures use seed 20261002; optional C# vectors supply a second exact comparison.

Tolerance: exact

Gates: `GCAM_CRRC_VECTORS`.

Source: [rtl/test_crrc.py](../rtl/test_crrc.py), [rtl/crrc_contract.py](../rtl/crrc_contract.py), [rtl/run_cocotb.py](../rtl/run_cocotb.py).

Method groups:

- `finite_rise_energies_match_reference`, `near_threshold_sweep_matches_reference`, `signed_full_scale_noise_and_valid_gaps_match_reference`: Python integer-reference oracle; exact; gate none.
- `legacy_golden_and_proportionality_are_preserved`: pulse fixture; exact; gate none.
- `csharp_vectors_match_python_and_rtl`: C# vector oracle; exact; gate GCAM_CRRC_VECTORS.

```json
{"id":"cocotb::test_crrc@slow-lyso-f12","runner":"cocotb","methods":["finite_rise_energies_match_reference","near_threshold_sweep_matches_reference","signed_full_scale_noise_and_valid_gaps_match_reference","legacy_golden_and_proportionality_are_preserved","csharp_vectors_match_python_and_rtl"],"sources":{"rtl/test_crrc.py":"fdad77862f70cd62aab1116d154117ffcc27533c8582c33eafad4db826baec7d","rtl/crrc_contract.py":"688a834d209a9245552948606c564a7d3fc9e978eec288c1cd7adfa0f3897bf7","rtl/run_cocotb.py":"11c695dd86af561bfe12de33ce672b1a1d34227851524f6650ffa87b0a55fc7d"},"trace":[]}
```


## test_crrc@slow-nai-f0 — cocotb

Verifies test_crrc under configuration slow-nai-f0.

Oracle: Python trap_ref.crrc_int supplies integer sample outputs; independent fixture math checks ORDER/A/K/F. Reset/pulse fixtures use seed 20261002; optional C# vectors supply a second exact comparison.

Tolerance: exact

Gates: `GCAM_CRRC_VECTORS`.

Source: [rtl/test_crrc.py](../rtl/test_crrc.py), [rtl/crrc_contract.py](../rtl/crrc_contract.py), [rtl/run_cocotb.py](../rtl/run_cocotb.py).

Method groups:

- `finite_rise_energies_match_reference`, `near_threshold_sweep_matches_reference`, `signed_full_scale_noise_and_valid_gaps_match_reference`: Python integer-reference oracle; exact; gate none.
- `legacy_golden_and_proportionality_are_preserved`: pulse fixture; exact; gate none.
- `csharp_vectors_match_python_and_rtl`: C# vector oracle; exact; gate GCAM_CRRC_VECTORS.

```json
{"id":"cocotb::test_crrc@slow-nai-f0","runner":"cocotb","methods":["finite_rise_energies_match_reference","near_threshold_sweep_matches_reference","signed_full_scale_noise_and_valid_gaps_match_reference","legacy_golden_and_proportionality_are_preserved","csharp_vectors_match_python_and_rtl"],"sources":{"rtl/test_crrc.py":"fdad77862f70cd62aab1116d154117ffcc27533c8582c33eafad4db826baec7d","rtl/crrc_contract.py":"688a834d209a9245552948606c564a7d3fc9e978eec288c1cd7adfa0f3897bf7","rtl/run_cocotb.py":"11c695dd86af561bfe12de33ce672b1a1d34227851524f6650ffa87b0a55fc7d"},"trace":[]}
```


## test_crrc@slow-nai-f12 — cocotb

Verifies test_crrc under configuration slow-nai-f12.

Oracle: Python trap_ref.crrc_int supplies integer sample outputs; independent fixture math checks ORDER/A/K/F. Reset/pulse fixtures use seed 20261002; optional C# vectors supply a second exact comparison.

Tolerance: exact

Gates: `GCAM_CRRC_VECTORS`.

Source: [rtl/test_crrc.py](../rtl/test_crrc.py), [rtl/crrc_contract.py](../rtl/crrc_contract.py), [rtl/run_cocotb.py](../rtl/run_cocotb.py).

Method groups:

- `finite_rise_energies_match_reference`, `near_threshold_sweep_matches_reference`, `signed_full_scale_noise_and_valid_gaps_match_reference`: Python integer-reference oracle; exact; gate none.
- `legacy_golden_and_proportionality_are_preserved`: pulse fixture; exact; gate none.
- `csharp_vectors_match_python_and_rtl`: C# vector oracle; exact; gate GCAM_CRRC_VECTORS.

```json
{"id":"cocotb::test_crrc@slow-nai-f12","runner":"cocotb","methods":["finite_rise_energies_match_reference","near_threshold_sweep_matches_reference","signed_full_scale_noise_and_valid_gaps_match_reference","legacy_golden_and_proportionality_are_preserved","csharp_vectors_match_python_and_rtl"],"sources":{"rtl/test_crrc.py":"fdad77862f70cd62aab1116d154117ffcc27533c8582c33eafad4db826baec7d","rtl/crrc_contract.py":"688a834d209a9245552948606c564a7d3fc9e978eec288c1cd7adfa0f3897bf7","rtl/run_cocotb.py":"11c695dd86af561bfe12de33ce672b1a1d34227851524f6650ffa87b0a55fc7d"},"trace":[]}
```


## test_trap_shaper@trapezoidal_shaper — cocotb

Verifies test_trap_shaper under configuration trapezoidal_shaper.

Oracle: Python trap_ref.trap_shape supplies expected samples from pulse, pile-up and retained MC streams, after latency 0 (direct) or 3 (pipelined). Parameters are read from the DUT, limiting configuration independence.

Tolerance: exact

Gates: none.

Source: [rtl/test_trap_shaper.py](../rtl/test_trap_shaper.py), [rtl/crrc_contract.py](../rtl/crrc_contract.py), [rtl/run_cocotb.py](../rtl/run_cocotb.py).

```json
{"id":"cocotb::test_trap_shaper@trapezoidal_shaper","runner":"cocotb","methods":["single_pulse_matches_reference","piled_pulses_match_reference","mc_event_stream_matches_reference"],"sources":{"rtl/test_trap_shaper.py":"e5c43deb80ef071a2a4a9c5dea4c6ca3111a6d9ec53e2eda5d7ca51218af2d4c","rtl/crrc_contract.py":"688a834d209a9245552948606c564a7d3fc9e978eec288c1cd7adfa0f3897bf7","rtl/run_cocotb.py":"11c695dd86af561bfe12de33ce672b1a1d34227851524f6650ffa87b0a55fc7d"},"trace":[]}
```


## test_trap_shaper@trapezoidal_shaper_pl — cocotb

Verifies test_trap_shaper under configuration trapezoidal_shaper_pl.

Oracle: Python trap_ref.trap_shape supplies expected samples from pulse, pile-up and retained MC streams, after latency 0 (direct) or 3 (pipelined). Parameters are read from the DUT, limiting configuration independence.

Tolerance: exact

Gates: none.

Source: [rtl/test_trap_shaper.py](../rtl/test_trap_shaper.py), [rtl/crrc_contract.py](../rtl/crrc_contract.py), [rtl/run_cocotb.py](../rtl/run_cocotb.py).

```json
{"id":"cocotb::test_trap_shaper@trapezoidal_shaper_pl","runner":"cocotb","methods":["single_pulse_matches_reference","piled_pulses_match_reference","mc_event_stream_matches_reference"],"sources":{"rtl/test_trap_shaper.py":"e5c43deb80ef071a2a4a9c5dea4c6ca3111a6d9ec53e2eda5d7ca51218af2d4c","rtl/crrc_contract.py":"688a834d209a9245552948606c564a7d3fc9e978eec288c1cd7adfa0f3897bf7","rtl/run_cocotb.py":"11c695dd86af561bfe12de33ce672b1a1d34227851524f6650ffa87b0a55fc7d"},"trace":[]}
```


## BackgroundShapeTests — unittest

Checks separation of timing, selection and validation seeds, immutable recipe references and exact acquisition-budget selection rules. It also checks vector-error bounds, deterministic cluster bootstrap scaling and iteration selection only when association is valid.

Oracle: Synthetic opposite/duplicate vectors, fixed geometric scaling, exact one-sided binomial cutoff, pinned disjoint seeds and smallest tied iteration are fixture/definition oracles.

Tolerance: structural

Gates: none.

Source: [samples/evidence/tests/test_background_shape.py](../samples/evidence/tests/test_background_shape.py).

```json
{"id":"unittest::test_background_shape.BackgroundShapeTests","runner":"unittest","methods":["test_budget_revision_preserves_selected_physics_and_immutable_references","test_version_two_families_separate_timing_seeds_from_validation","test_conditional_screen_budget_3200_has_exact_integer_cutoff","test_vector_bound_does_not_confuse_equal_norms_with_equal_vectors","test_bootstrap_resamples_clusters_and_is_deterministic","test_no_association_valid_regime_refuses_iteration_selection","test_iteration_tie_selects_smaller_count_on_common_regimes","test_selection_acquisition_budget_is_derived_from_zero_failure_limit","test_target_scaling_preserves_the_simultaneous_bootstrap","test_phase_seed_lists_are_pinned_and_disjoint"],"sources":{"samples/evidence/tests/test_background_shape.py":"d0e52cf74f6d2df5f158afeccaac3429e35dbee4ee065a83a81d96fd9b7b474d"},"trace":[]}
```


## ReadoutNetlistTests — unittest

TODO-19 RD-10: the independent standard-library nodal solver (samples/readout/netlist_check.py — Gaussian elimination with partial pivoting, no engine call) reproduces hand-solved circuits, and every committed engine-exported charge-division netlist in docs/assets/readout/ yields the engine's own DC charge fractions within the derived floating-point bound; the stored comparison reports are reproduced exactly.

Oracle: hand-solved circuits — 2 × 2 corner grid 7/15, 3/15, 3/15, 2/15; a single-row chain grounded through r/2 at both ends, right share ((k+1)·R + r/2)/((S+1)·R + r); load-resistor outputs dividing inversely (80/140) — and, for the committed netlists, the engine's fractions in the matching <prefix>.config.json.

Tolerance: hand-solved cases 1e-12 (round-off of a ≤ 12-unknown solve). Engine comparison: the normwise forward-error bound of a backward-stable solve, per solver ‖g_out‖₁·‖v‖∞·(k/(1 − k) + γ_d) with k = 2·γ_{3n}·κ∞(G), γ_m = m·u/(1 − m·u), u = 2⁻⁵³ (Higham, Thm 7.2; growth factor ≤ 2), doubled for two solvers — computed per netlist (1.6e-10 … 1.1e-8 against observed deviations ≤ 6.7e-15). Stored reports: exact equality.

Gates: none.

Source: [samples/evidence/tests/test_readout_netlist.py](../samples/evidence/tests/test_readout_netlist.py).

```json
{"id":"unittest::test_readout_netlist.ReadoutNetlistTests","runner":"unittest","methods":["test_corner_grid_two_by_two_matches_hand_solution","test_single_row_chain_divides_as_a_series_chain","test_load_resistor_outputs_conserve_charge","test_committed_netlists_match_the_engine_within_the_derived_bound"],"sources":{"samples/evidence/tests/test_readout_netlist.py":"da70702ca7e3026656c7aaf2c2316a4b3e44e1d685551143f85814dbcfdd5e55"},"trace":[]}
```

## ProvenanceTests — unittest

Checks canonical source, seed, recipe, executor and artifact bindings through evidence collection, staging and aggregation. Tampering, mixed identities and conflicting duplicates must be refused; reuse and staged execution must retain their original provenance.

Oracle: Known canonical hashes, immutable staged bytes, role/seed/executor identities and one-defect refusal fixtures supply exact/structural oracles.

Tolerance: exact

Gates: `GCAM_PROVENANCE_GIT_TESTS`.

Source: [samples/evidence/tests/test_provenance.py](../samples/evidence/tests/test_provenance.py).

Method groups:

- `test_canonical_order`, `test_dirty_is_accepted`, `test_missing_root`, `test_missing_seed_provenance`, `test_malformed_source_binding`, `test_unsupported_schema`, `test_output_tampering`, `test_seed_binding`, `test_mixed_execution`, `test_typed_executors`, `test_conflicting_duplicates`, `test_identical_duplicates`, `test_recipe_conflict`, `test_reuse_preserves_original_source`, `test_reuse_changed_executor`, `test_sidecar_binds_every_member`, `test_preflight_writes_nothing_on_mixed_inputs`, `test_unbound_numerical_file_is_refused`, `test_envelope_and_sidecar_must_agree`, `test_sidecar_binding_across_output_directories`, `test_staged_python_import_and_rtl_survive_original_edits`, `test_staged_managed_bytes_survive_original_rebuild`, `test_source_hash_uses_untracked_contents`, `test_failed_git_query_is_not_clean`, `test_calibration_legacy_and_roles`, `test_real_ambient_aggregator_and_refusal_before_write`, `test_seed_offset`, `test_shared_calibration_file_keeps_role_families_separate`: exact / structural fixture comparisons; gate None.
- `test_dirty_diff_on_temporary_repository`: exact / structural fixture comparisons; gate GCAM_PROVENANCE_GIT_TESTS.

```json
{"id":"unittest::test_provenance.ProvenanceTests","runner":"unittest","methods":["test_canonical_order","test_dirty_is_accepted","test_missing_root","test_missing_seed_provenance","test_malformed_source_binding","test_unsupported_schema","test_output_tampering","test_seed_binding","test_mixed_execution","test_typed_executors","test_conflicting_duplicates","test_identical_duplicates","test_recipe_conflict","test_reuse_preserves_original_source","test_reuse_changed_executor","test_sidecar_binds_every_member","test_preflight_writes_nothing_on_mixed_inputs","test_unbound_numerical_file_is_refused","test_envelope_and_sidecar_must_agree","test_sidecar_binding_across_output_directories","test_staged_python_import_and_rtl_survive_original_edits","test_staged_managed_bytes_survive_original_rebuild","test_source_hash_uses_untracked_contents","test_failed_git_query_is_not_clean","test_calibration_legacy_and_roles","test_real_ambient_aggregator_and_refusal_before_write","test_seed_offset","test_shared_calibration_file_keeps_role_families_separate","test_dirty_diff_on_temporary_repository"],"sources":{"samples/evidence/tests/test_provenance.py":"86c9df989e254140e9005baf11f4bad739d40e4198130ffbcfb125c0f3bb85aa"},"trace":[]}
```


## ReadoutRenderTests — dotnet

Detached production readout XAML and controls: sensor/network, Anger/LUT, calibration, diagnostics, pending/preparing/ready/failed states and five waveform lanes, in both themes at 1280×800 and 1440×900. Synthetic drawing records are not calibration evidence. Snapshot output requires an explicit scratch directory. A normal ungated STA case drives bound combo-box selected indices and text edits, drains the dispatcher and verifies accepted values in the controls, including unsupported geometry, locked readout mode, invalid energy windows and physical-mode optics preset refusal. It parses the production ReadoutPanel and reproduces MainWindow's optics editor bindings without constructing a Window.

Oracle: Production bindings and layout: five nonzero plot surfaces, the sum threshold series, no Window/HWND/desktop input. Engine-generated isolated conversion timestamps must agree with the trace's first sum crossing and peak. After rendering, all five plot rectangles and root-relative zero-time coordinates share exactly the same X mapping at both sizes and themes.

Tolerance: Structural and exact rendered X-coordinate equality; no pixel-difference or elapsed-time acceptance band. Isolated trigger/peak marker-to-trace distance is at most max(engine sample period, scope sample period), 8 ns for this fixture; hold is within the engine-derived search window.

Gates: Snapshot method: GCAM_RENDER_SNAPSHOTS=1; GCAM_RENDER_OUTPUT required. Bound-control regression: none; detached, no Window/HWND/desktop input.

```json
{"runner":"dotnet","sources":{"tests/Gcam.Studio.RenderTests/ReadoutRenderTests.cs":"3a8bd6622204253e24fee5f3504352ae242b2d9b411b5c3f8a47cbf9c5aef6bc","tests/Gcam.Studio.RenderTests/RenderSnapshotFactAttribute.cs":"95dd1088eee764d18a539ff37399a3c518aa75056d29d2de60286d2173ba1edd"},"methods":["BoundReadoutControls_RejectedEditsShowAcceptedSourceValues","ExperimentalDetectorAndFourLanes_BothThemesAndSizes"],"id":"Gcam.Studio.RenderTests::Gcam.Studio.RenderTests.ReadoutRenderTests","trace":[]}
```


## PhysicalReadoutTests — dotnet

Builds the independent reference-head preparation, checks cache/cancellation and real virtual-clock Stop/Continue prefixes. Spectrum and Imaging count populations agree; five realised waveform lanes conserve their sum and replay the stored held analogue response.

Oracle: Uninterrupted fixed-seed acquisition; immutable prior prefixes; exact count partition. Replay round-off follows the pulse derivative and floating-point summation bound. The isolated sum trigger is bracketed independently by the continuous rising-edge root and the next engine sample; the preceding engine sample is below threshold. The hold is within its search window and one engine sample of the analytic peak. Scope markers agree with the first sampled crossing and peak within max(engine period, scope period), 8 ns here.

Tolerance: Exact records/counts. Analogue replay: sum(abs(channel hits)) times [8u max(1, live seconds) 1e9 (1/rise + 1/tail)/normalization + gamma_(8N+20)], u=2^-53, gamma_m=m u/(1-m u). No physics acceptance band.

Gates: none.

```json
{"runner":"dotnet","sources":{"tests/Gcam.Studio.Services.Tests/PhysicalReadoutTests.cs":"a895dfff08fee72e10d3428ea7241be1419649731d6804be0626dc560dc4d4a6","tests/Gcam.Studio.Services.Tests/EvidenceFactAttribute.cs":"898301fdfe0438aa4ea3503809ee81dc90b225dff52defb3e2cee6277085cd60"},"methods":["IsolatedScope_TriggerFirstCrossingAndHoldPeakShareThePulseTimeBase","Prepare_IsIndependentCachedAndCancellationDoesNotPublish","Continuation_ConservesMeasuredRecordsAndImmutablePrefixesExactly","CancellationAtTransportBoundary_PublishesNoPartialArtifact","ManualWindowBoundariesAndUnknowns_AgreeAcrossSpectrumAndImaging","UnsupportedInputs_AreRefusedWithoutMutatingThem"],"id":"Gcam.Studio.Services.Tests::Gcam.Studio.Services.Tests.PhysicalReadoutTests","trace":[]}
```


## ReadoutViewModelTests — dotnet

Checks supported-head selection, actionable field/preparation refusal without silent zeroing, inactive legacy controls, preparation invalidation and explicit keV-window validation.

Oracle: Declared SD-2/SD-3 input policies and readout cache content identity.

Tolerance: Exact/structural.

Gates: none.

```json
{"runner":"dotnet","sources":{"tests/Gcam.Studio.Tests/ReadoutViewModelTests.cs":"f09badc9dca0032f975ffbb92f27d3958059c4859c91bd9eedc4361fc6e8bbe8"},"methods":["GeometryRefusalAndLegacyLocks_HaveActionableReasons","StartRefusesFieldAndMissingPreparationWithoutSilentZeroing","PreparationKeyInvalidationAndWindowValidation_AreExplicit"],"id":"Gcam.Studio.Tests::Gcam.Studio.Tests.ReadoutViewModelTests","trace":[]}
```


## ReadoutScenarioTests — dotnet

Adds desktop reference-head and field refusal, Prepare cancellation/retry/tabs and fixed-seed uninterrupted-versus-continued count scenarios. The count parser oracle runs headlessly and rejects nonconservation.

Oracle: Assigned + unknown = triggered exactly; fixed-seed continued counts equal uninterrupted counts; disabled legacy controls and explicit refusal text.

Tolerance: Exact/structural. Bounded UI waits are operational deadlines, not physics tolerances.

Gates: Desktop methods: GCAM_UI_TESTS=1; count oracle: none.

```json
{"runner":"dotnet","sources":{"tests/Gcam.Studio.UiTests/ReadoutScenarioTests.cs":"598070a9fae2444acfd3caa23a7b327bd91e3e7816bff9d6035a373409a20a1a","tests/Gcam.Studio.UiTests/Harness/FloodOracle.cs":"22db35cb2771a0460e437e47308b5792bf1c22d2dc077af54106223a29cd75be","tests/Gcam.Studio.UiTests/Harness/Pointer.cs":"e56d09c72e552bf5619c088150a0d6ceba0ec2eb75ee06c2744c4677b9db896c","tests/Gcam.Studio.UiTests/Harness/RepoPaths.cs":"2bdc3c25db3c74cfe32ea7f64352f36cc5fb3b8fbf7be92ce03b7184648cc655","tests/Gcam.Studio.UiTests/Harness/RunRecord.cs":"0db3b6cf367fed06ba506db2973593b100ebed4a6ca793552a77e490d0fce377","tests/Gcam.Studio.UiTests/Harness/Scenario.cs":"dac922df393236562f138489ab7a54d4ff09bb4003f7694da421564e0647b085","tests/Gcam.Studio.UiTests/Harness/StudioProcess.cs":"973777303020b10c38bcd701e991764b095cb2f149ed31da6a5d554bf73d87c2","tests/Gcam.Studio.UiTests/Harness/StudioWindow.cs":"59fca517024d117593d9056ed1c5122d2b8632b7ae179b36a8e091aea06b5bf8","tests/Gcam.Studio.UiTests/Harness/WorkspaceOracle.cs":"153e5f5ec778401b877467e405295a940d663d4fea1d32f9101139e9c0b325e5","tests/Gcam.Studio.UiTests/DesktopFactAttribute.cs":"1dcac8c38143a4d38635267e17af2acdbb5f2ee84eca07e2968a2a1e7f073bfc"},"methods":["CountOracle_RefusesNonConservationAndMissingIdentity","UnsupportedGeometryAndField_ExplainRefusalWithoutZeroing","PreparationCancelRetryAndTabs_AreExplicit","FixedSeedRepeatAndContinuation_ConserveCountsAndExposeFourLanes"],"id":"Gcam.Studio.UiTests::Gcam.Studio.UiTests.ReadoutScenarioTests","trace":[]}
```


## ReadoutStreamTests — dotnet

Compares a persistent observed-horizon processor to the original batch processor across three partitions. An open hold waits for the observation horizon; pre-cancelled advancement consumes no noise.

Oracle: Original batch arithmetic and fixed realised hits/RNG streams.

Tolerance: Exact codes, hold times, associations, contributor counts and shares; no numerical tolerance.

Gates: none.

```json
{"runner":"dotnet","sources":{"tests/Gcam.Tests/ReadoutStreamTests.cs":"d9dfd1c263957e59b061f495e41830b07a47e15f1644aa06188504fe712d04fe","tests/Gcam.Tests/Harness/Rigs.cs":"9b89ce04cdcb302fcefc6636d45a0d1640a48c288d1e0a3638b005c44028c3b2","tests/Gcam.Tests/Harness/Stat.cs":"4502dddc6276f9d1de0c41b2ed7246ed7f00c9266daa12a34d6174ae1cb19029"},"methods":["PartitionedObservedStream_MatchesBatchExactly","HoldWaitsForObservedFuture_AndCancellationConsumesNoNoise"],"id":"Gcam.Tests::Gcam.Tests.ReadoutStreamTests","trace":[]}
```
