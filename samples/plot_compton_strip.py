"""Plot the combined spectral+spatial lever (run `montecarlo compton-strip samples/scenario.json`):
per-pixel Compton stripping inside the coded pipeline, then decode. Two geometries x three maps
(Cs-only truth, raw 662 window, stripped).

Usage:  python samples/plot_compton_strip.py
"""
import os
import numpy as np
import matplotlib
matplotlib.use("Agg")
import matplotlib.pyplot as plt

here = os.path.dirname(os.path.abspath(__file__))
CS, CO = (4.0, 0.0), (-5.0, 3.0)
COLOC = (0.0, 0.0)


def load_recon(path):
    with open(path) as f:
        header = f.readline()
    meta = dict(kv.split("=") for kv in header.replace("#", "").split())
    grid = np.loadtxt(path, delimiter=",", skiprows=1)
    return grid, float(meta["origin_mm"]), float(meta["step_mm"])


scenes = [("separated", CS, CO), ("co-located", COLOC, COLOC)]
cols = [("csonly", "Cs-137 only (truth)"), ("raw", "raw 662 window\n(Cs + Co downscatter)"),
        ("stripped", "per-pixel stripped\n-> decode")]

fig, axes = plt.subplots(2, 3, figsize=(14, 8.6))
for r, (scene, cspos, copos) in enumerate(scenes):
    for c, (tag, title) in enumerate(cols):
        ax = axes[r, c]
        grid, origin, step = load_recon(os.path.join(here, f"strip_{scene}_{tag}.csv"))
        n = grid.shape[0]
        ext = [origin, origin + step * (n - 1), origin, origin + step * (n - 1)]
        im = ax.imshow(grid, origin="lower", extent=ext, cmap="inferno", aspect="equal")
        ax.plot(*cspos, "+", color="#4dd0e1", ms=14, mew=2)
        ax.text(cspos[0] + 0.6, cspos[1] + 0.6, "Cs", color="#4dd0e1", fontsize=9, fontweight="bold")
        if copos != cspos:
            ax.plot(*copos, "x", color="#ff5252", ms=12, mew=2)
            ax.text(copos[0] + 0.6, copos[1] + 0.6, "Co", color="#ff5252", fontsize=9, fontweight="bold")
        else:
            ax.text(copos[0] + 0.6, copos[1] - 1.4, "Co (same spot)", color="#ff5252", fontsize=8)
        if c == 0:
            ax.set_ylabel(f"{scene}\n\ny (mm)", fontsize=10)
        else:
            ax.set_ylabel("y (mm)")
        ax.set_xlabel("x (mm)")
        if r == 0:
            ax.set_title(title, fontsize=10)
        fig.colorbar(im, ax=ax, fraction=0.046)

fig.suptitle("Combined lever: per-pixel Compton stripping + coded decode\n"
             "raw 662 window carries the Co-60 ghost/inflation; stripping removes it — "
             "even when the two sources are CO-LOCATED (spatial decode alone cannot)", fontsize=12)
fig.tight_layout(rect=[0, 0, 1, 0.95])
fig.savefig(os.path.join(here, "compton_strip_combined.png"), dpi=130)
print("saved samples/compton_strip_combined.png")
