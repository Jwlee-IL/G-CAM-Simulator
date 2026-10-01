# Spectrum plot offscreen evidence

Fixed analytic Cs-137 + Co-60 drawing fixture, 256 bins of 6 keV; these images verify rendering,
not detector physics. Produced on 2026-10-01 by `PlotViewRenderTests` through `RenderTargetBitmap`
at 1000 × 600, 96 DPI, using the shipped theme tokens and mono font. No window, focus or mouse input.

| Theme | Full axis | 580–740 keV zoom |
|---|---|---|
| Dark | [full](spectrum-dark-full.png) | [662 keV](spectrum-dark-zoom662.png) |
| Light | [full](spectrum-light-full.png) | [662 keV](spectrum-light-zoom662.png) |

Reproduce in PowerShell:

```powershell
$env:GCAM_RENDER_SNAPSHOTS = '1'
dotnet test tests/Gcam.Studio.RenderTests -c Release
Remove-Item Env:GCAM_RENDER_SNAPSHOTS
```

The test also verifies zoom and grow-only Y retention on data replacement and full-axis reset.
It initializes base WPF resource infrastructure, without constructing the Studio App or calling Run.
Normal solution tests skip it. Desktop tests remain a separate final verification step.

Batch 1 regenerates these plots with grouped log decades, a separate Y-title row, and neutral bands
with edge lines. The same render case also captures [whole-window content](../studio-render/README.md).
