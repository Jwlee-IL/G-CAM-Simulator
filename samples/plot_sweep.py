"""Cyclic vs non-cyclic decoding over a grid of source positions.
Draws samples/cyclic_vs_noncyclic.png from samples/sweep_cyclic.csv and samples/sweep_noncyclic.csv: localization
error per source position, the fully-coded field (one MURA period) as a box, and the count localized within 3 mm.
The CSVs carry no geometry, so pass the scenario the sweep was run on (the committed CSVs: the hand-held head).
Usage:  montecarlo sweep samples/scenario_handheld.json
        python samples/plot_sweep.py [samples/scenario_handheld.json]
"""
import os, sys, csv, json
import numpy as np
import matplotlib
matplotlib.use("Agg")
import matplotlib.pyplot as plt

here = os.path.dirname(os.path.abspath(__file__))
THRESHOLD_MM = 3.0  # same as the CLI's ghost threshold

scenario = sys.argv[1] if len(sys.argv) > 1 else os.path.join(here, "scenario_handheld.json")
cfg = json.load(open(scenario))
d, s = cfg["geometry"]["maskDetectorDistanceMm"], cfg["geometry"]["sourceMaskDistanceMm"]
fc_half = cfg["mask"]["rank"] * cfg["mask"]["cellPitchMm"] * (d + s) / d / 2.0


def grid(name):
    rows = list(csv.DictReader(open(os.path.join(here, name))))
    xs = sorted({float(r["sx_mm"]) for r in rows})
    ys = sorted({float(r["sy_mm"]) for r in rows})
    err = np.full((len(ys), len(xs)), np.nan)
    for r in rows:
        err[ys.index(float(r["sy_mm"])), xs.index(float(r["sx_mm"]))] = float(r["error_mm"])
    return np.array(xs), np.array(ys), err


panels = [("CYCLIC (classical)", "sweep_cyclic.csv"), ("NON-CYCLIC (finite mask)", "sweep_noncyclic.csv")]
data = [grid(f) for _, f in panels]
vmax = max(np.nanmax(e) for _, _, e in data)

fig, axes = plt.subplots(1, 2, figsize=(10.5, 5.6), sharey=True, layout="constrained")
for ax, (title, _), (xs, ys, err) in zip(axes, panels, data):
    step = xs[1] - xs[0]
    extent = [xs[0] - step / 2, xs[-1] + step / 2, ys[0] - step / 2, ys[-1] + step / 2]
    im = ax.imshow(err, origin="lower", extent=extent, cmap="inferno", vmin=0, vmax=vmax)
    ax.add_patch(plt.Rectangle((-fc_half, -fc_half), 2 * fc_half, 2 * fc_half,
                               fill=False, ec="cyan", ls="--", lw=1.6))
    ok = int(np.sum(err < THRESHOLD_MM))
    ax.set_title(title)
    ax.set_xlabel(f"source x (mm)\n{ok}/{err.size} localized <{THRESHOLD_MM:g} mm")
axes[0].set_ylabel("source y (mm)")
fig.colorbar(im, ax=axes, label="localization error (mm)", shrink=0.9)
fig.suptitle(f"Cyclic vs non-cyclic decoding — {cfg['name']}: rank-{cfg['mask']['rank']} MURA, "
             f"D = {d:g} mm, source plane {s:g} mm from the mask (cyan = fully coded field, ±{fc_half:.1f} mm)")
out = os.path.join(here, "cyclic_vs_noncyclic.png")
fig.savefig(out, dpi=120, bbox_inches="tight")
print(f"wrote {out}")
