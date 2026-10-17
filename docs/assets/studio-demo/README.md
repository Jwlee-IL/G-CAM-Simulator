# GCAM Studio demo clips

Three clips for the repository README, converted from the videos the desktop UI tests record while they drive Studio
([AGENTS.UiAutomation](../../AGENTS.UiAutomation.md), "Video of each desktop test"). Documentation only, not evidence:
the tests' verdicts and run records are the evidence, and the source videos stay local. Nothing is hand-edited; the
mouse cursor is drawn in by the recorder.

| Clip | Test case | Run | Build |
|---|---|---|---|
| [navigate.gif](navigate.gif) | `ManualScenarioTests.Navigate_WindowWorkspacesAndViewTabs` | `20261017-195337-fafe9a` | `161e74e`, deployment `2e7e30ec32af` |
| [acquisition.gif](acquisition.gif) | `ManualScenarioTests.Acquisition_StartStopContinueReset` | `20261017-195449-e4b017` | same |
| [imaging.gif](imaging.gif) | `WorkspaceScenarioTests.Imaging_ChannelAndStrip_MatchRetainedFloodAndFoundPeaks` | `20261017-200636-81541b` | same |

Conversion (ffmpeg, from `artifacts/ui-videos/<case-id>.mp4`, 1440 × 900 at 10 fps):

```bash
ffmpeg -i <case-id>.mp4 -vf "fps=10,scale=960:-1:flags=lanczos,split[a][b];[a]palettegen=max_colors=256:stats_mode=diff[p];[b][p]paletteuse=dither=sierra2_4a:diff_mode=rectangle" -loop 0 <clip>.gif
```

Replace a clip only when the screens it shows change: every replacement stays in the git history (the three are 10 MB).
